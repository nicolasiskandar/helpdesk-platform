using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketService.Application.DTOs;
using TicketService.Application.Interfaces;

namespace TicketService.Api.Controllers;

[ApiController]
[Route("api/kb-articles")]
[Authorize]
public class KbArticlesController : ControllerBase
{
    private readonly IKbArticleService _kbArticleService;

    public KbArticlesController(IKbArticleService kbArticleService)
    {
        _kbArticleService = kbArticleService;
    }

    /// <summary>
    /// Lists knowledge base articles (search + category filter, paginated). Drafts are visible to admins only.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(KbArticleListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArticles(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var includeDraft = IsAdmin();
        var result = await _kbArticleService.GetArticlesAsync(search, category, page, pageSize, includeDraft);
        return Ok(result);
    }

    /// <summary>
    /// Gets a single article (admins can view drafts) and increments its view count.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(KbArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticle(Guid id)
    {
        var includeDraft = IsAdmin();
        var article = await _kbArticleService.GetArticleAsync(id, includeDraft);
        return Ok(article);
    }

    /// <summary>
    /// Creates a knowledge base article (admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(KbArticleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateArticle([FromBody] KbArticleRequest request)
    {
        var authorUserId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString());
        var article = await _kbArticleService.CreateArticleAsync(request, authorUserId);
        return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, article);
    }

    /// <summary>
    /// Updates a knowledge base article (admin only).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(KbArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateArticle(Guid id, [FromBody] KbArticleRequest request)
    {
        var article = await _kbArticleService.UpdateArticleAsync(id, request);
        return Ok(article);
    }

    /// <summary>
    /// Deletes a knowledge base article (admin only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteArticle(Guid id)
    {
        await _kbArticleService.DeleteArticleAsync(id);
        return NoContent();
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }
}
