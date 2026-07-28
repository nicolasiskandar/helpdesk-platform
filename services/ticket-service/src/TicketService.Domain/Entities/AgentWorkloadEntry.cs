namespace TicketService.Domain.Entities;

public record AgentWorkloadEntry(
    Guid AgentUserId,
    int OpenCount,
    int ResolvedCount,
    IReadOnlyList<AgentWorkloadTicketEntry> OpenTickets,
    IReadOnlyList<AgentWorkloadTicketEntry> ResolvedTickets
);

public record AgentWorkloadTicketEntry(
    Guid TicketId,
    string ReferenceNumber,
    string Title,
    string CategoryName,
    string PriorityName,
    string StatusName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
