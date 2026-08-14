using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;

namespace NotificationService.Infrastructure.Services;

public class IdentityUserEmailResolver : IUserEmailResolver
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentityUserEmailResolver> _logger;
    private readonly ConcurrentDictionary<Guid, CacheEntry> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public IdentityUserEmailResolver(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<IdentityUserEmailResolver> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string?> GetEmailAsync(Guid userId)
    {
        if (_cache.TryGetValue(userId, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow) return entry.Email;
            _cache.TryRemove(userId, out _);
        }

        try
        {
            var email = await ResolveAsync(userId);
            _cache[userId] = new CacheEntry(email, DateTime.UtcNow.Add(CacheTtl));
            return email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve email for user {UserId}", userId);
            return null;
        }
    }

    private async Task<string?> ResolveAsync(Guid userId)
    {
        var serviceKey = _configuration["NOTIFICATION_SERVICE_KEY"];
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/emails")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { userIds = new[] { userId } }),
                Encoding.UTF8,
                "application/json")
        };
        if (!string.IsNullOrEmpty(serviceKey))
            request.Headers.TryAddWithoutValidation("X-Notification-Service-Key", serviceKey);

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Identity email lookup failed with status {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UserEmailsResponse>(content, JsonOptions);
        return result?.Users.FirstOrDefault()?.Email;
    }

    private sealed record CacheEntry(string? Email, DateTime ExpiresAt);

    private sealed class UserEmailsResponse
    {
        public List<UserEmailResponse> Users { get; set; } = new();
    }

    private sealed class UserEmailResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
    }
}
