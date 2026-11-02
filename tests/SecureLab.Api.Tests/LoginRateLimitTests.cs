using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SecureLab.Api.Scaffolding;

namespace SecureLab.Api.Tests;

public sealed class LoginRateLimitTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task Login_ExceedingLimit_Returns429_ThenRecoversAfterWindow()
    {
        // Окремий host: лічильники належать host, а не HttpClient.
        using var host = factory.WithWebHostBuilder(_ => { });
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        var wrong = "x" + Guid.NewGuid().ToString("N");

        for (var i = 0; i < Lab03Identity.LoginPermitLimit; i++)
        {
            using var attempt = await client.PostAsJsonAsync(
                "/api/auth/login", new { userName = "nobody", password = wrong });
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        using var limited = await client.PostAsJsonAsync(
            "/api/auth/login", new { userName = "nobody", password = wrong });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        await Task.Delay(Lab03Identity.LoginWindow + TimeSpan.FromMilliseconds(500));

        using var recovered = await client.PostAsJsonAsync(
            "/api/auth/login", new { userName = "nobody", password = wrong });
        Assert.Equal(HttpStatusCode.Unauthorized, recovered.StatusCode);
    }
}