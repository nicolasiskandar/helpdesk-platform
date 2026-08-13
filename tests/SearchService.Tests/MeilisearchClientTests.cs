using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SearchService.Api.Services;
using Xunit;

namespace SearchService.Tests;

public sealed class MeilisearchClientTests
{
    [Fact]
    public async Task SearchAsync_AppliesFiltersAndMapsHits()
    {
        var handler = new StubHandler("""
            {"hits":[{"id":"ticket-1","referenceNumber":"TKT-000001","title":"VPN unavailable","description":"The VPN client cannot connect.","category":"Network","priority":"High","closedAt":"2026-08-12T00:00:00Z"}],"estimatedTotalHits":1}
            """);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://meilisearch:7700") };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var client = new MeilisearchClient(httpClient, configuration, NullLogger<MeilisearchClient>.Instance);

        var result = await client.SearchAsync("vpn", "Network", "High", null, null, 2, 10, CancellationToken.None);

        result.Total.Should().Be(1);
        result.Page.Should().Be(2);
        result.Items.Should().ContainSingle().Which.TicketId.Should().Be("ticket-1");
        handler.RequestUri!.AbsolutePath.Should().Be("/indexes/closed_tickets/search");
        using var request = JsonDocument.Parse(handler.RequestBody);
        request.RootElement.GetProperty("filter").GetString().Should().Be("category = \"Network\" AND priority = \"High\"");
        request.RootElement.GetProperty("offset").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task EnsureIndexAsync_IndexAlreadyExists_DoesNotThrowAndStillAppliesSettings()
    {
        var handler = new AlreadyExistsHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://meilisearch:7700") };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var client = new MeilisearchClient(httpClient, configuration, NullLogger<MeilisearchClient>.Instance);

        await client.EnsureIndexAsync(CancellationToken.None);

        handler.Requests.Should().Contain(request => request.Method == HttpMethod.Post && request.Path == "/indexes");
        handler.Requests.Should().Contain(request => request.Method == HttpMethod.Patch && request.Path == "/indexes/closed_tickets/settings");
    }

    [Fact]
    public async Task SearchAsync_DateFiltersUseUnixTimestamps()
    {
        var handler = new StubHandler("""{"hits":[],"estimatedTotalHits":0}""");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://meilisearch:7700") };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var client = new MeilisearchClient(httpClient, configuration, NullLogger<MeilisearchClient>.Instance);

        await client.SearchAsync("", null, null,
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc),
            1, 20, CancellationToken.None);

        using var request = JsonDocument.Parse(handler.RequestBody);
        request.RootElement.GetProperty("filter").GetString().Should().Be("closedAtTs >= 1785542400 AND closedAtTs <= 1788134400");
    }

    private sealed class AlreadyExistsHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/indexes")
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""
                        {"message":"Index `closed_tickets` already exists.","code":"index_already_exists","type":"invalid_request","link":"https://docs.meilisearch.com/errors#index_already_exists"}
                        """, Encoding.UTF8, "application/json")
                };
            }

            if (request.RequestUri!.AbsolutePath.StartsWith("/tasks/"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"taskUid":1,"status":"succeeded"}""", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("""{"taskUid":1,"indexUid":"closed_tickets","status":"enqueued","type":"settingsUpdate"}""", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
