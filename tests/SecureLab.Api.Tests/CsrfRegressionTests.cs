using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

// REG-CSRF-01 (F-02): змінюючі маршрути з cookie-автентифікацією вимагають antiforgery-токен.
public sealed class CsrfRegressionTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string Password() =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    private static object Body(string title) => new
    {
        title,
        description = "Штучний запис для перевірки CSRF.",
        severity = "Low",
        occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
    };

    [Fact]
    public async Task REG_CSRF_01_Create_WithoutToken_Rejected_StateUnchanged_ThenTokenSucceeds()
    {
        var title = $"REG-CSRF-01-{Guid.NewGuid():N}";
        using var client = await factory.LoginAsync("bob", Password(), withCsrf: false);
        try
        {
            // 1. cookie є, токена немає: запит відхилено, стан не змінився.
            using var noToken = await client.PostAsJsonAsync("/api/incidents", Body(title));
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
            Assert.False(await ExistsAsync(title));

            // 2. хибний токен: так само.
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "forged-token");
            using var badToken = await client.PostAsJsonAsync("/api/incidents", Body(title));
            Assert.Equal(HttpStatusCode.BadRequest, badToken.StatusCode);
            Assert.False(await ExistsAsync(title));

            // 3. дійсний токен: успіх, стан змінився.
            await client.AddCsrfAsync();
            using var ok = await client.PostAsJsonAsync("/api/incidents", Body(title));
            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
            Assert.True(await ExistsAsync(title));
        }
        finally { await DeleteAsync(title); }
    }

    [Fact]
    public async Task REG_CSRF_01_Logout_WithoutToken_Rejected_SessionStaysValid()
    {
        using var client = await factory.LoginAsync("bob", Password(), withCsrf: false);

        using var rejected = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        using var me = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);   // сеанс не завершено чужим запитом

        await client.AddCsrfAsync();
        using var accepted = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    }

    [Fact]
    public async Task REG_CSRF_01_SafeGet_DoesNotRequireToken()
    {
        using var client = await factory.LoginAsync("bob", Password(), withCsrf: false);
        using var list = await client.GetAsync("/api/incidents");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    private async Task<bool> ExistsAsync(string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        return db.Incidents.Any(item => item.Title == title);
    }

    private async Task DeleteAsync(string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        db.Incidents.RemoveRange(db.Incidents.Where(item => item.Title == title));
        await db.SaveChangesAsync();
    }
}
