using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SearchService.Api.Models;

namespace SearchService.Api.Services;

public sealed class TicketSearchConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TicketSearchConsumer> _logger;
    private readonly string _hostName;
    private readonly int _port;
    private readonly string _exchange;
    private readonly int _maxRedeliveries;
    private readonly ConcurrentDictionary<string, long> _seenMessageIds = new();
    private readonly TimeSpan _dedupTtl = TimeSpan.FromHours(1);
    private IConnection? _connection;
    private IModel? _channel;

    public TicketSearchConsumer(IServiceProvider services, IConfiguration configuration, ILogger<TicketSearchConsumer> logger)
    {
        _services = services;
        _logger = logger;
        _hostName = configuration["RabbitMQ:HostName"] ?? "rabbitmq";
        _port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
        _exchange = configuration["RabbitMQ:ExchangeName"] ?? "ticket.events";
        _maxRedeliveries = int.TryParse(configuration["RabbitMQ:MaxRedeliveries"], out var max) ? max : 5;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectWithRetryAsync(stoppingToken);
        if (_channel is null) return;
        using (var scope = _services.CreateScope())
        {
            var meilisearchClient = scope.ServiceProvider.GetRequiredService<MeilisearchClient>();
            await meilisearchClient.EnsureIndexAsync(stoppingToken);
            await BackfillClosedTicketsAsync(scope.ServiceProvider, meilisearchClient, stoppingToken);
        }

        var queue = _channel.QueueDeclare("search-index.q", durable: true, autoDelete: false).QueueName;
        foreach (var key in new[] { "ticket.resolved", "ticket.status_changed", "ticket.deleted" })
            _channel.QueueBind(queue, _exchange, key);
        _channel.BasicQos(0, 1, false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (_, eventArgs) =>
        {
            try
            {
                var messageId = eventArgs.BasicProperties?.MessageId ?? string.Empty;
                if (IsDuplicate(messageId))
                {
                    _logger.LogInformation("Duplicate message {MessageId}, skipping", messageId);
                    _channel.BasicAck(eventArgs.DeliveryTag, false);
                    return;
                }

                await ProcessAsync(eventArgs.RoutingKey, Encoding.UTF8.GetString(eventArgs.Body.ToArray()), stoppingToken);
                if (!string.IsNullOrEmpty(messageId))
                    _seenMessageIds.TryAdd(messageId, DateTimeOffset.UtcNow.Add(_dedupTtl).ToUnixTimeSeconds());
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            }
            catch (Exception exception)
            {
                var redeliveries = GetRedeliveryCount(eventArgs);
                _logger.LogError(exception, "Unable to process search event {RoutingKey} (redelivery {Redeliveries}/{MaxRedeliveries})",
                    eventArgs.RoutingKey, redeliveries + 1, _maxRedeliveries);

                if (redeliveries >= _maxRedeliveries)
                {
                    // Upserts/deletes are idempotent by ticket id, so dropping a
                    // poison message can't corrupt the index — it just leaves the
                    // last state stale. Ack-with-drop to avoid an infinite loop.
                    _logger.LogWarning("Poison search event {RoutingKey} dropped after {Redeliveries} redeliveries",
                        eventArgs.RoutingKey, redeliveries);
                    _channel.BasicNack(eventArgs.DeliveryTag, false, false);
                }
                else
                {
                    _channel.BasicNack(eventArgs.DeliveryTag, false, true);
                }
            }
        };
        _channel.BasicConsume(queue, false, consumer);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private bool IsDuplicate(string messageId)
    {
        if (string.IsNullOrEmpty(messageId)) return false;
        SweepExpired();

        if (_seenMessageIds.TryGetValue(messageId, out var expiry))
            return expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return false;
    }

    private void SweepExpired()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var (id, expiry) in _seenMessageIds)
        {
            if (expiry <= now) _seenMessageIds.TryRemove(id, out _);
        }
    }

    private static int GetRedeliveryCount(BasicDeliverEventArgs eventArgs)
    {
        if (!eventArgs.Redelivered || eventArgs.BasicProperties.Headers == null
            || !eventArgs.BasicProperties.Headers.TryGetValue("x-death", out var xDeath)
            || xDeath is not List<object> entries)
        {
            return 0;
        }

        var total = 0L;
        foreach (var entry in entries)
        {
            if (entry is Dictionary<string, object> table
                && table.TryGetValue("count", out var count)
                && count is long c)
            {
                total += c;
            }
        }

        return (int)total;
    }

    private async Task BackfillClosedTicketsAsync(IServiceProvider scopeServices, MeilisearchClient meilisearchClient, CancellationToken cancellationToken)
    {
        var ticketClient = scopeServices.GetRequiredService<TicketServiceClient>();
        const int pageSize = 100;
        var page = 1;
        try
        {
            while (true)
            {
                var response = await ticketClient.GetClosedTicketsAsync(page, pageSize, cancellationToken);
                if (response.Items.Count == 0) break;

                foreach (var item in response.Items)
                {
                    await meilisearchClient.UpsertAsync(new TicketSearchDocument(
                        item.Id.ToString(),
                        item.ReferenceNumber,
                        item.Title,
                        item.Description,
                        item.CategoryName,
                        item.PriorityName,
                        item.ClosedAt), cancellationToken);
                }

                if (response.Items.Count < pageSize || page * pageSize >= response.TotalCount) break;
                page++;
            }
            _logger.LogInformation("Search index backfilled existing closed tickets from the ticket service.");
        }
        catch (Exception exception)
        {
            // Design rule #7: search must never block anything. Failures here only
            // mean the index misses pre-existing closed tickets; new events still flow.
            _logger.LogWarning(exception, "Search index backfill failed; continuing with event-driven indexing only.");
        }
    }

    private async Task ProcessAsync(string routingKey, string json, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var ticketId = root.GetProperty("TicketId").GetGuid().ToString();
        using var scope = _services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<MeilisearchClient>();
        if (routingKey == "ticket.resolved" && root.GetProperty("ResolvedStatusName").GetString() == "Closed")
        {
            await client.UpsertAsync(new TicketSearchDocument(ticketId, root.GetProperty("ReferenceNumber").GetString()!, root.GetProperty("Title").GetString()!, root.GetProperty("Description").GetString()!, root.TryGetProperty("CategoryName", out var category) ? category.GetString() : null, root.TryGetProperty("PriorityName", out var priority) ? priority.GetString() : null, root.GetProperty("ResolvedAt").GetDateTime()), cancellationToken);
            return;
        }
        if (routingKey == "ticket.deleted" || (routingKey == "ticket.status_changed" && root.GetProperty("OldStatus").GetString() == "Closed" && root.GetProperty("NewStatus").GetString() != "Closed"))
            await client.DeleteAsync(ticketId, cancellationToken);
    }

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= 30 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var factory = new ConnectionFactory { HostName = _hostName, Port = _port };
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.ExchangeDeclare(_exchange, ExchangeType.Topic, durable: true);
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "RabbitMQ connection attempt {Attempt}/30 failed", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    public override void Dispose() { _channel?.Dispose(); _connection?.Dispose(); base.Dispose(); }
}
