using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class CreateIncidentAuthTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string Password() =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    private static object Body(string title, Guid? forgedOwner = null) =>
        forgedOwner is null
            ? new
            {
                title,
                description = "Штучний запис для перевірки власника.",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            }
            : new
            {
                title,
                description = "Штучний запис для перевірки власника.",
                severity = "Low",
                occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                ownerUserId = forgedOwner.Value
            };

    [Fact]
    public async Task A01_Create_Anonymous_Returns401()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        using var response = await client.PostAsJsonAsync(
            "/api/incidents", Body($"A01-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A05_Create_ForgedOwnerInJson_IsIgnored_OwnerIsPrincipal()
    {
        var title = $"A05-{Guid.NewGuid():N}";
        using var client = await factory.LoginAsync("bob", Password());
        try
        {
            var bobId = await GetMyIdAsync(client);

            using var created = await client.PostAsJsonAsync(
                "/api/incidents", Body(title, DbSeeder.AliceId));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var ownerId = await GetOwnerIdByTitleAsync(title);
            Assert.Equal(bobId, ownerId);
            Assert.NotEqual(DbSeeder.AliceId, ownerId);
        }
        finally { await DeleteByTitleAsync(title); }
    }

    [Fact]
    public async Task A04_Create_WithoutOwnerField_OwnerIsPrincipal()
    {
        var title = $"A04-{Guid.NewGuid():N}";
        using var client = await factory.LoginAsync("bob", Password());
        try
        {
            var bobId = await GetMyIdAsync(client);

            using var created = await client.PostAsJsonAsync("/api/incidents", Body(title));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            Assert.Equal(bobId, await GetOwnerIdByTitleAsync(title));
        }
        finally { await DeleteByTitleAsync(title); }
    }

    private static async Task<Guid> GetMyIdAsync(HttpClient client)
    {
        using var me = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var doc = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<Guid> GetOwnerIdByTitleAsync(string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var incident = db.Incidents.Single(item => item.Title == title);
        return incident.OwnerUserId;
    }

    private async Task DeleteByTitleAsync(string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        db.Incidents.RemoveRange(db.Incidents.Where(item => item.Title == title));
        await db.SaveChangesAsync();
    }
}