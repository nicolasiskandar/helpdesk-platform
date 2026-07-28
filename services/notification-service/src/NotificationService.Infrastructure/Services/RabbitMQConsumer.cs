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
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMQConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<RabbitMQConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hostName = configuration["RabbitMQ:HostName"] ?? "rabbitmq";
        _port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672");
        _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "ticket.events";
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
                _logger.LogError(ex, "Error processing message {MessageId}", messageId);
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
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
