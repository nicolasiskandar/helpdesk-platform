using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using TicketService.Application.Interfaces;

namespace TicketService.Infrastructure.Services;

public class RabbitMQPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName;
    private readonly string _dlqExchangeName;
    private readonly string _dlqQueueName;

    public RabbitMQPublisher(IConfiguration configuration)
    {
        _exchangeName = configuration["RabbitMQ:ExchangeName"] ?? "ticket.events";
        _dlqExchangeName = configuration["RabbitMQ:DLQExchangeName"] ?? "ticket.events.dlx";
        _dlqQueueName = configuration["RabbitMQ:DLQQueueName"] ?? "ticket.events.dlq";
        var hostName = configuration["RabbitMQ:HostName"] ?? "rabbitmq";
        var port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5672;
        var userName = configuration["RabbitMQ:UserName"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = hostName,
            Port = port,
            UserName = userName,
            Password = password
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(exchange: _exchangeName, type: ExchangeType.Topic, durable: true);

        _channel.ExchangeDeclare(exchange: _dlqExchangeName, type: ExchangeType.Fanout, durable: true);
        _channel.QueueDeclare(queue: _dlqQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
        _channel.QueueBind(queue: _dlqQueueName, exchange: _dlqExchangeName, routingKey: string.Empty);
    }

    public Task PublishAsync(string routingKey, string payload, string messageId)
    {
        var body = Encoding.UTF8.GetBytes(payload);
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = routingKey;
        properties.MessageId = messageId;

        properties.Headers ??= new Dictionary<string, object>();
        var activity = Activity.Current;
        if (activity?.Id != null)
        {
            properties.Headers["traceparent"] = Encoding.UTF8.GetBytes(activity.Id);
            if (!string.IsNullOrEmpty(activity.TraceStateString))
                properties.Headers["tracestate"] = Encoding.UTF8.GetBytes(activity.TraceStateString);
        }

        _channel.BasicPublish(
            exchange: _exchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    public Task PublishToDLQAsync(string routingKey, string payload, string messageId)
    {
        var body = Encoding.UTF8.GetBytes(payload);
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = routingKey;
        properties.MessageId = messageId;
        properties.Headers = new Dictionary<string, object>
        {
            ["x-original-routing-key"] = Encoding.UTF8.GetBytes(routingKey),
            ["x-dlq-reason"] = Encoding.UTF8.GetBytes("max_retries_exceeded")
        };

        _channel.BasicPublish(
            exchange: _dlqExchangeName,
            routingKey: string.Empty,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
