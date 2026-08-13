using System.Net.Http.Json;
using SearchService.Api.Models;

namespace SearchService.Api.Services;

public sealed class TicketServiceClient
{
    private readonly HttpClient _httpClient;

    public TicketServiceClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var serviceKey = configuration["SearchService:ServiceKey"];
        if (!string.IsNullOrWhiteSpace(serviceKey))
            _httpClient.DefaultRequestHeaders.Add("X-Search-Service-Key", serviceKey);
    }

    public async Task<TicketIndexListResponse> GetClosedTicketsAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"api/tickets/index-sync?page={page}&pageSize={pageSize}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Ticket service rejected the search service key (X-Search-Service-Key).");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TicketIndexListResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Ticket service returned an empty index-sync response.");
    }
}
