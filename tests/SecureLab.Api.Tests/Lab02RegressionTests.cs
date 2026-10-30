using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class Lab02RegressionTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object ValidBody(string title, string severity = "Low") => new
    {
        title,
        description = "Штучний запис для регресійного тесту контракту.",
        severity,
        occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
    };

    [Fact]
    public async Task T02_CreateIncident_WithSeverity7_Returns400WithSeverityKey()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/incidents", ValidBody($"Regression-{Guid.NewGuid():N}", "7"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("severity", out _));
        Assert.DoesNotContain("Exception", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task T03_CreateIncident_WithActiveDuplicateTitle_Returns409()
    {
        var title = $"Regression-{Guid.NewGuid():N}";
        try
        {
            using var first = await _client.PostAsJsonAsync("/api/incidents", ValidBody(title));
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            using var second = await _client.PostAsJsonAsync("/api/incidents", ValidBody(title));
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
            await db.Incidents.Where(item => item.Title == title).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task S02_Search_ControlInput_ReturnsEmptyList()
    {
        var q = Uri.EscapeDataString("zz-no-match' OR TRUE -- ");
        using var response = await _client.GetAsync($"/api/incidents/search?q={q}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, document.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task S02_Search_Usb_ReturnsSingleSeededIncident()
    {
        using var response = await _client.GetAsync("/api/incidents/search?q=USB");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000005"),
            item.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task T04_Search_WithApostrophe_Returns200AndSeededIncident()
    {
        using var response = await _client.GetAsync(
            "/api/incidents/search?q=" + Uri.EscapeDataString("O'Brien"));

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(document.RootElement.EnumerateArray(),
            item => item.GetProperty("title").GetString() == "O'Brien classroom report");
    }

    [Fact]
    public async Task T05_Search_WithUnknownSortBy_Returns400WithSortByKey()
    {
        using var response = await _client.GetAsync("/api/incidents/search?sortBy=price");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("sortBy", out _));
    }
}