namespace TicketService.Application.DTOs;

public record AuditLogEntryResponse(
    Guid Id,
    Guid ChangedByUserId,
    string ChangedByType,
    string FieldChanged,
    string? OldValue,
    string? NewValue,
    DateTime ChangedAt
);

public record AuditLogListResponse(
    IReadOnlyList<AuditLogEntryResponse> Entries,
    int TotalCount
);
