using System.Net.Http.Json;
using System.Text.Json;
using SearchService.Api.Models;

namespace SearchService.Api.Services;

public sealed class MeilisearchClient
{
    private const string IndexName = "closed_tickets";
    private readonly HttpClient _httpClient;
    private readonly ILogger<MeilisearchClient> _logger;
    private readonly string? _apiKey;

    public MeilisearchClient(HttpClient httpClient, IConfiguration configuration, ILogger<MeilisearchClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Meilisearch:ApiKey"];
        if (!string.IsNullOrWhiteSpace(_apiKey))
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("indexes", new { uid = IndexName, primaryKey = "id" }, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            await EnsureTaskSucceededAsync(response, cancellationToken);
        }
        else if (!await IsIndexAlreadyExistsAsync(response, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
        }

        var settings = new
        {
            searchableAttributes = new[] { "referenceNumber", "title", "description", "category", "priority" },
            displayedAttributes = new[] { "id", "referenceNumber", "title", "description", "category", "priority", "closedAt" },
            filterableAttributes = new[] { "category", "priority", "closedAtTs" },
            sortableAttributes = new[] { "closedAt" },
            rankingRules = new[] { "words", "typo", "proximity", "attribute", "sort", "exactness" }
        };
        var settingsResponse = await _httpClient.PatchAsJsonAsync($"indexes/{IndexName}/settings", settings, cancellationToken);
        await EnsureTaskSucceededAsync(settingsResponse, cancellationToken);
    }

    public async Task UpsertAsync(TicketSearchDocument document, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PutAsJsonAsync($"indexes/{IndexName}/documents", new[] { document }, cancellationToken);
        await EnsureTaskSucceededAsync(response, cancellationToken);
    }

    public async Task DeleteAsync(string ticketId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.DeleteAsync($"indexes/{IndexName}/documents/{ticketId}", cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            await EnsureTaskSucceededAsync(response, cancellationToken);
    }

    public async Task<TicketSearchResponse> SearchAsync(string query, string? category, string? priority, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(category)) filters.Add($"category = {JsonSerializer.Serialize(category)}");
        if (!string.IsNullOrWhiteSpace(priority)) filters.Add($"priority = {JsonSerializer.Serialize(priority)}");
        if (from is not null) filters.Add($"closedAtTs >= {ToUnixSeconds(from.Value)}");
        if (to is not null) filters.Add($"closedAtTs <= {ToUnixSeconds(to.Value)}");
        var body = new { q = query, filter = filters.Count == 0 ? null : string.Join(" AND ", filters), limit = pageSize, offset = (page - 1) * pageSize, sort = new[] { "closedAt:desc" } };
        var response = await _httpClient.PostAsJsonAsync($"indexes/{IndexName}/search", body, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<MeilisearchSearchResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Meilisearch returned an empty response.");
        var items = payload.Hits.Select(hit => new TicketSearchResult(hit.Id, hit.ReferenceNumber, hit.Title, hit.Description.Length > 300 ? hit.Description[..300] : hit.Description, hit.Category, hit.Priority, hit.ClosedAt)).ToList();
        return new TicketSearchResponse(items, payload.EstimatedTotalHits, page, pageSize);
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try { return (await _httpClient.GetAsync("health", cancellationToken)).IsSuccessStatusCode; }
        catch (HttpRequestException exception) { _logger.LogWarning(exception, "Meilisearch health check failed"); return false; }
    }

    private static long ToUnixSeconds(DateTime value) =>
        new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private async Task<bool> IsIndexAlreadyExistsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var error = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            if (!error.RootElement.TryGetProperty("code", out var code)) return false;
            if (code.GetString() != "index_already_exists") return false;
            _logger.LogInformation("Index {IndexName} already exists — skipping creation.", IndexName);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task EnsureTaskSucceededAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        using var task = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        if (!task.RootElement.TryGetProperty("taskUid", out var taskUid)) return;

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var statusResponse = await _httpClient.GetAsync($"tasks/{taskUid.GetInt64()}", cancellationToken);
            statusResponse.EnsureSuccessStatusCode();
            using var status = JsonDocument.Parse(await statusResponse.Content.ReadAsStreamAsync(cancellationToken));
            var value = status.RootElement.GetProperty("status").GetString();
            if (value == "succeeded") return;
            if (value == "failed")
            {
                var error = status.RootElement.GetProperty("error");
                var errorCode = error.TryGetProperty("code", out var code) ? code.GetString() : null;
                if (errorCode == "index_already_exists")
                {
                    _logger.LogInformation("Index {IndexName} already exists — skipping creation.", IndexName);
                    return;
                }
                throw new InvalidOperationException($"Meilisearch task {taskUid.GetInt64()} failed: {error.GetRawText()}");
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TimeoutException($"Meilisearch task {taskUid.GetInt64()} did not complete in time.");
    }

    private sealed record MeilisearchSearchResponse(List<MeilisearchHit> Hits, int EstimatedTotalHits);
    private sealed record MeilisearchHit(string Id, string ReferenceNumber, string Title, string Description, string? Category, string? Priority, DateTime ClosedAt);
}
