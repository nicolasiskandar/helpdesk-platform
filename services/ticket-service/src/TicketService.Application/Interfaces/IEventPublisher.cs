namespace TicketService.Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(string routingKey, string payload);
}
