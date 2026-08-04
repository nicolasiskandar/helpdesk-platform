using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;
using TicketService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TicketService.Infrastructure.Repositories;

public class KbArticleRepository : IKbArticleRepository
{
    private readonly TicketDbContext _context;

    public KbArticleRepository(TicketDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<KbArticle>> GetAsync(string? search, string? category, int page, int pageSize, bool includeDraft)
    {
        var query = BuildQuery(search, category, includeDraft);
        return await query
            .OrderByDescending(a => a.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(string? search, string? category, bool includeDraft)
    {
        return await BuildQuery(search, category, includeDraft).CountAsync();
    }

    public async Task<KbArticle?> GetByIdAsync(Guid id)
    {
        return await _context.KbArticles
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task AddAsync(KbArticle article)
    {
        await _context.KbArticles.AddAsync(article);
    }

    public Task UpdateAsync(KbArticle article)
    {
        _context.KbArticles.Update(article);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(KbArticle article)
    {
        _context.KbArticles.Remove(article);
        return Task.CompletedTask;
    }

    private IQueryable<KbArticle> BuildQuery(string? search, string? category, bool includeDraft)
    {
        var query = _context.KbArticles.AsQueryable();
        if (!includeDraft)
            query = query.Where(a => a.Status == "published");
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(a => a.Category == category);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a =>
                a.Title.Contains(search) || a.Body.Contains(search) || a.Excerpt.Contains(search));
        return query;
    }
}
