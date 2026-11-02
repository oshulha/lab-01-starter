using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class AuthScenarioTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string Password() =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    [Fact]
    public async Task A02_LoginThenMe_ReturnsPrincipalFieldsOnly()
    {
        using var client = await factory.LoginAsync("alice", Password());
        using var response = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(DbSeeder.AliceId, root.GetProperty("id").GetGuid());
        Assert.Equal("alice", root.GetProperty("userName").GetString());
        // Лише дозволені поля DTO: жодних email, хешів чи службових даних.
        Assert.Equal(new[] { "id", "role", "userName" },
            root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task A03_ValidCookieWithForeignUserIdHeader_MeIsUnchanged()
    {
        using var client = await factory.LoginAsync("alice", Password());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Demo-UserId", DbSeeder.BobId.ToString());
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(DbSeeder.AliceId, id);
        Assert.NotEqual(DbSeeder.BobId, id);
    }

    [Fact]
    public async Task A06_Logout_ThenMe_Returns401()
    {
        using var client = await factory.LoginAsync("alice", Password());
        using (var before = await client.GetAsync("/api/me"))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using (var logout = await client.PostAsync("/api/auth/logout", null))
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // Той самий cookie-aware client: головний доказ у наступному запиті.
        using var after = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task A07_UnknownUserAndWrongPassword_LookTheSameFromOutside()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var wrong = "x" + Guid.NewGuid().ToString("N");

        using var unknown = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "nobody" + Guid.NewGuid().ToString("N")[..8], password = wrong });
        using var badPassword = await client.PostAsJsonAsync("/api/auth/login",
            new { userName = "alice", password = wrong });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(unknown.StatusCode, badPassword.StatusCode);
        Assert.Equal(unknown.Content.Headers.ContentType?.MediaType,
            badPassword.Content.Headers.ContentType?.MediaType);

        if (unknown.Content.Headers.ContentType is not null)
        {
            // traceId навмисно не порівнюємо: він діагностичний і різний.
            Assert.Equal(await ProblemShape.ReadAsync(unknown),
                await ProblemShape.ReadAsync(badPassword));
        }
        else
        {
            Assert.Empty(await unknown.Content.ReadAsByteArrayAsync());
            Assert.Empty(await badPassword.Content.ReadAsByteArrayAsync());
        }
    }
}