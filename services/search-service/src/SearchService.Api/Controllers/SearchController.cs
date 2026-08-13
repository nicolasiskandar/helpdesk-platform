using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SearchService.Api.Models;
using SearchService.Api.Services;

namespace SearchService.Api.Controllers;

[ApiController]
[Route("api/search/tickets")]
[Authorize]
public sealed class SearchController(MeilisearchClient client) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<TicketSearchResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TicketSearchResponse>> Search(
        [FromQuery] string q = "", [FromQuery] string? category = null, [FromQuery] string? priority = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return ValidationProblem("page must be positive and pageSize must be between 1 and 100.");
        return Ok(await client.SearchAsync(q.Trim(), category, priority, from, to, page, pageSize, cancellationToken));
    }
}
