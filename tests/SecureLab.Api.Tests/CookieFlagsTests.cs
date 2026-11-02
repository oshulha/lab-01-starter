using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

public sealed class CookieFlagsTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task Login_OverHttps_SetsHttpOnlySecureLaxCookie()
    {
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password")
            ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { userName = "alice", password });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        const string cookieName = ".AspNetCore.Identity.Application=";
        var matching = response.Headers.GetValues("Set-Cookie")
            .Where(value => value.StartsWith(cookieName, StringComparison.Ordinal)).ToArray();
        Assert.True(matching.Length == 1);   // значення cookie не потрапить у failure

        var attributes = matching[0].Split(';').Skip(1)
            .Select(part => part.Trim().ToLowerInvariant()).ToArray();
        Assert.Contains("httponly", attributes);
        Assert.Contains("secure", attributes);
        Assert.Contains("samesite=lax", attributes);
    }
}