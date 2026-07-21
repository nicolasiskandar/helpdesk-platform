namespace TicketService.Application.DTOs;

public record CategoryResponse(int Id, string Name);
public record PriorityResponse(int Id, string Name, int Level);
public record StatusResponse(int Id, string Name);
