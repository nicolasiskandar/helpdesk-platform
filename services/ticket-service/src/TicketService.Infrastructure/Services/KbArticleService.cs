using TicketService.Application.DTOs;
using TicketService.Application.Interfaces;
using TicketService.Domain.Entities;
using TicketService.Domain.Interfaces;

namespace TicketService.Infrastructure.Services;

public class KbArticleService : IKbArticleService
{
    private readonly IUnitOfWork _unitOfWork;

    public KbArticleService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<KbArticleListResponse> GetArticlesAsync(string? search, string? category, int page, int pageSize, bool includeDraft)
    {
        var articles = await _unitOfWork.KbArticles.GetAsync(search, category, page, pageSize, includeDraft);
        var totalCount = await _unitOfWork.KbArticles.GetCountAsync(search, category, includeDraft);

        var responses = articles.Select(MapToResponse).ToList();
        return new KbArticleListResponse(responses, totalCount, page, pageSize);
    }

    public async Task<KbArticleResponse> GetArticleAsync(Guid id, bool includeDraft)
    {
        var article = await _unitOfWork.KbArticles.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Article not found.");

        if (!includeDraft && article.Status != "published")
            throw new KeyNotFoundException("Article not found.");

        article.Views += 1;
        await _unitOfWork.KbArticles.UpdateAsync(article);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(article);
    }

    public async Task<KbArticleResponse> CreateArticleAsync(KbArticleRequest request, Guid authorUserId)
    {
        var article = new KbArticle
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Excerpt = request.Excerpt,
            Body = request.Body,
            Category = request.Category,
            AuthorUserId = authorUserId,
            Views = 0,
            Status = request.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.KbArticles.AddAsync(article);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(article);
    }

    public async Task<KbArticleResponse> UpdateArticleAsync(Guid id, KbArticleRequest request)
    {
        var article = await _unitOfWork.KbArticles.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Article not found.");

        article.Title = request.Title;
        article.Excerpt = request.Excerpt;
        article.Body = request.Body;
        article.Category = request.Category;
        article.Status = request.Status;
        article.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.KbArticles.UpdateAsync(article);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(article);
    }

    public async Task DeleteArticleAsync(Guid id)
    {
        var article = await _unitOfWork.KbArticles.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Article not found.");

        await _unitOfWork.KbArticles.DeleteAsync(article);
        await _unitOfWork.SaveChangesAsync();
    }

    private static KbArticleResponse MapToResponse(KbArticle article)
    {
        return new KbArticleResponse(
            article.Id,
            article.Title,
            article.Excerpt,
            article.Body,
            article.Category,
            article.AuthorUserId,
            article.Views,
            article.Status,
            article.CreatedAt,
            article.UpdatedAt
        );
    }
}
