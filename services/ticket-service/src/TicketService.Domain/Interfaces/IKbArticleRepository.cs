using TicketService.Domain.Entities;

namespace TicketService.Domain.Interfaces;

public interface IKbArticleRepository
{
    Task<IReadOnlyList<KbArticle>> GetAsync(string? search, string? category, int page, int pageSize, bool includeDraft);
    Task<int> GetCountAsync(string? search, string? category, bool includeDraft);
    Task<KbArticle?> GetByIdAsync(Guid id);
    Task AddAsync(KbArticle article);
    Task UpdateAsync(KbArticle article);
    Task DeleteAsync(KbArticle article);
}
