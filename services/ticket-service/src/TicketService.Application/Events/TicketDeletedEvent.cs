namespace TicketService.Application.Events;

public record TicketDeletedEvent(Guid TicketId, DateTime DeletedAt);
