using System.Text;
using System.Text.Json;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Infrastructure.Services;

public class RabbitMQConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMQConsumer> _logger;
    private readonly string _hostName;
    private readonly int _port;
    private readonly string _exchangeName;
    private readonly int _maxRedeliveries;
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMQConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<RabbitMQConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hostName = configuration["RabbitMQ:HostName"] ?? "rabbitmq";
        _port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
        _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "ticket.events";
        _maxRedeliveries = int.TryParse(configuration["RabbitMQ:MaxRedeliveries"], out var max) ? max : 5;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ConnectWithRetryAsync(stoppingToken);

        if (_connection == null || _channel == null)
        {
            _logger.LogError("Failed to connect to RabbitMQ after retries. Exiting.");
            return;
        }

        var queueName = _channel.QueueDeclare("notification.events", durable: true, autoDelete: false).QueueName;
        _channel.QueueBind(queueName, _exchangeName, "ticket.*");

        _logger.LogInformation("Listening on queue {Queue} for exchange {Exchange}", queueName, _exchangeName);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            var routingKey = ea.RoutingKey;
            var messageId = ea.BasicProperties?.MessageId ?? string.Empty;

            _logger.LogInformation("Received message: RoutingKey={RoutingKey}, MessageId={MessageId}", routingKey, messageId);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processedRepo = scope.ServiceProvider.GetRequiredService<IProcessedMessageRepository>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                if (!string.IsNullOrEmpty(messageId) && await processedRepo.ExistsAsync(messageId))
                {
                    _logger.LogInformation("Duplicate message {MessageId}, skipping", messageId);
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                await notificationService.ProcessTicketEventAsync(routingKey, body);

                if (!string.IsNullOrEmpty(messageId))
                    await processedRepo.AddAsync(messageId);

                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                var redeliveries = GetRedeliveryCount(ea);
                _logger.LogError(ex, "Error processing message {MessageId} (redelivery {Redeliveries}/{MaxRedeliveries})",
                    messageId, redeliveries + 1, _maxRedeliveries);

                if (redeliveries >= _maxRedeliveries)
                {
                    // Poison message: stop requeueing so it can't hot-loop the
                    // queue forever. Ack-with-drop (no DLQ binding here); the
                    // ticket service already DLQs its own outbox failures.
                    _logger.LogWarning("Poison message {MessageId} dropped after {Redeliveries} redeliveries",
                        messageId, redeliveries);
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
                else
                {
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
                }
            }
        };

        _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

        // TTL sweep: prune processed-message dedup rows so the table doesn't
        // grow unbounded (AGENTS.md documents the dedup table "with TTL").
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var sweepScope = _serviceProvider.CreateScope();
                var processedRepo = sweepScope.ServiceProvider.GetRequiredService<IProcessedMessageRepository>();
                var cutoff = DateTime.UtcNow.AddDays(-7);
                var deleted = await processedRepo.DeleteOlderThanAsync(cutoff);
                if (deleted > 0)
                {
                    _logger.LogInformation("Pruned {Count} processed-message dedup rows older than {Cutoff}", deleted, cutoff);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Processed-message TTL sweep failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
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

    private async Task ConnectWithRetryAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = _hostName, Port = _port };
        var maxRetries = 30;
        var retryDelay = TimeSpan.FromSeconds(2);

        for (var i = 0; i < maxRetries && !stoppingToken.IsCancellationRequested; i++)
        {
            try
            {
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.ExchangeDeclare(exchange: _exchangeName, type: ExchangeType.Topic, durable: true);
                _logger.LogInformation("Connected to RabbitMQ");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("RabbitMQ connection attempt {Attempt}/{Max} failed: {Message}", i + 1, maxRetries, ex.Message);
                await Task.Delay(retryDelay, stoppingToken);
            }
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
