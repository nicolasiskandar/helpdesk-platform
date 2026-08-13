namespace SearchService.Api.Models;

public sealed record TicketSearchDocument(
    string Id,
    string ReferenceNumber,
    string Title,
    string Description,
    string? Category,
    string? Priority,
    DateTime ClosedAt)
{
    public long ClosedAtTs => new DateTimeOffset(DateTime.SpecifyKind(ClosedAt, DateTimeKind.Utc)).ToUnixTimeSeconds();
}

public sealed record TicketSearchResult(
    string TicketId,
    string ReferenceNumber,
    string Title,
    string Excerpt,
    string? Category,
    string? Priority,
    DateTime ClosedAt);

public sealed record TicketSearchResponse(IReadOnlyList<TicketSearchResult> Items, int Total, int Page, int PageSize);

public sealed record TicketIndexDocument(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    string CategoryName,
    string PriorityName,
    DateTime ClosedAt);

public sealed record TicketIndexListResponse(
    IReadOnlyList<TicketIndexDocument> Items,
    int TotalCount,
    int Page,
    int PageSize);
