namespace TicketService.Application.DTOs;

public record CreateTicketRequest(
    string Title,
    string Description,
    int CategoryId,
    int PriorityId
);

public record UpdateTicketRequest(
    string? Title,
    string? Description,
    int? CategoryId,
    int? PriorityId
);

public record ChangeStatusRequest(
    int StatusId,
    string? Comment
);

public record TicketResponse(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    string CategoryName,
    string PriorityName,
    string StatusName,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record TicketListResponse(
    IReadOnlyList<TicketResponse> Tickets,
    int TotalCount,
    int Page,
    int PageSize
);
