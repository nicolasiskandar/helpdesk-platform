using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SearchService.Api.Services;
using Xunit;

namespace SearchService.Tests;

public sealed class TicketServiceClientTests
{
    [Fact]
    public async Task GetClosedTicketsAsync_SendsServiceKeyAndParsesResponse()
    {
        var handler = new StubHandler("""
            {"items":[{"id":"3a7f0a2e-1c8b-4d6f-9a2e-000000000001","referenceNumber":"TKT-000001","title":"VPN unavailable","description":"The VPN client cannot connect.","categoryName":"Network","priorityName":"High","closedAt":"2026-08-01T10:00:00Z"}],"totalCount":1,"page":1,"pageSize":100}
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://ticket-service:8080") };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SearchService:ServiceKey"] = "secret-key"
        }).Build();
        var client = new TicketServiceClient(httpClient, configuration);

        var result = await client.GetClosedTicketsAsync(1, 100, CancellationToken.None);

        result.TotalCount.Should().Be(1);
        var item = result.Items.Should().ContainSingle().Subject;
        item.ReferenceNumber.Should().Be("TKT-000001");
        item.ClosedAt.Should().Be(new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc));
        handler.RequestUri!.AbsolutePath.Should().Be("/api/tickets/index-sync");
        handler.RequestHeaders["X-Search-Service-Key"].Should().Be("secret-key");
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public Dictionary<string, string> RequestHeaders { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            if (request.Headers.TryGetValues("X-Search-Service-Key", out var values))
                RequestHeaders["X-Search-Service-Key"] = string.Join(",", values);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
