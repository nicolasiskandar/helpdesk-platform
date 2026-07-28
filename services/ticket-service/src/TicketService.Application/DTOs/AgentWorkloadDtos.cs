namespace TicketService.Application.DTOs;

public record AgentWorkloadResponse(
    Guid AgentUserId,
    int OpenCount,
    int ResolvedCount,
    IReadOnlyList<AgentWorkloadTicketResponse> OpenTickets,
    IReadOnlyList<AgentWorkloadTicketResponse> ResolvedTickets
);

public record AgentWorkloadTicketResponse(
    Guid TicketId,
    string ReferenceNumber,
    string Title,
    string CategoryName,
    string PriorityName,
    string StatusName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
