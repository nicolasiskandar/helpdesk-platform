namespace TicketService.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(string routingKey, string payload, string messageId);
    Task PublishToDLQAsync(string routingKey, string payload, string messageId);
}
