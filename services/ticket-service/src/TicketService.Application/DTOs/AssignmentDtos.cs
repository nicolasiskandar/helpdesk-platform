namespace TicketService.Application.DTOs;

public record AssignAgentRequest(Guid AgentUserId);

public record UnassignAgentRequest(Guid AgentUserId);

public record AssignmentResponse(
    Guid Id,
    Guid AgentUserId,
    Guid AssignedByUserId,
    DateTime AssignedAt,
    DateTime? UnassignedAt
);
