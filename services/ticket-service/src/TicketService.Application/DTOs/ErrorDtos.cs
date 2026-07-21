namespace TicketService.Application.DTOs;

public record ErrorResponse(string Message, IDictionary<string, string[]>? Errors = null);
