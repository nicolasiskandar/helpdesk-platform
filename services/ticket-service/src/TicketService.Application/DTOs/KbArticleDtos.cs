namespace TicketService.Application.DTOs;

public record KbArticleResponse(
    Guid Id,
    string Title,
    string Excerpt,
    string Body,
    string Category,
    Guid AuthorUserId,
    int Views,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record KbArticleRequest(
    string Title,
    string Excerpt,
    string Body,
    string Category,
    string Status
);

public record KbArticleListResponse(
    IReadOnlyList<KbArticleResponse> Articles,
    int TotalCount,
    int Page,
    int PageSize
);
