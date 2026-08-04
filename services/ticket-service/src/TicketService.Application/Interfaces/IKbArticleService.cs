using TicketService.Application.DTOs;

namespace TicketService.Application.Interfaces;

public interface IKbArticleService
{
    Task<KbArticleListResponse> GetArticlesAsync(string? search, string? category, int page, int pageSize, bool includeDraft);
    Task<KbArticleResponse> GetArticleAsync(Guid id, bool includeDraft);
    Task<KbArticleResponse> CreateArticleAsync(KbArticleRequest request, Guid authorUserId);
    Task<KbArticleResponse> UpdateArticleAsync(Guid id, KbArticleRequest request);
    Task DeleteArticleAsync(Guid id);
}
