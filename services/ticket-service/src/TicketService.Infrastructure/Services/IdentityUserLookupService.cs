using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TicketService.Application.Interfaces;

namespace TicketService.Infrastructure.Services;

public class IdentityUserLookupService : IUserLookupService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<IdentityUserLookupService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IdentityUserLookupService(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<IdentityUserLookupService> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetRolesByIdsAsync(IEnumerable<Guid> userIds, string accessToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, string>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users?isActive=true&page=1&pageSize=500");
        request.Headers.TryAddWithoutValidation("Authorization", accessToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Identity user lookup failed with status {Status}", response.StatusCode);
            throw new InvalidOperationException("Unable to verify comment recipients. Please try again.");
        }

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<IdentityUserListResponse>(content, JsonOptions);

        return (result?.Users ?? Enumerable.Empty<IdentityUserResponse>())
            .Where(u => ids.Contains(u.Id))
            .ToDictionary(u => u.Id, u => u.Role);
    }

    private sealed class IdentityUserListResponse
    {
        public List<IdentityUserResponse> Users { get; set; } = new();
    }

    private sealed class IdentityUserResponse
    {
        public Guid Id { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
