using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SearchService.Api.Services;

public sealed class MeilisearchHealthCheck(MeilisearchClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await client.IsReadyAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Meilisearch is unavailable.");
}
