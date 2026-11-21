using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using SecureLab.Api.Scaffolding;

namespace SecureLab.Api.Tests;

// F-03: ліміт для публічного пошуку, дві незалежні партиції (анонім за IP і автентифікований користувач).
public sealed class SearchRateLimitTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string Password() =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    private static async Task<HttpStatusCode> SearchAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/incidents/search?q=rate-limit-probe");
        return response.StatusCode;
    }

    private static async Task ExhaustAsync(HttpClient client)
    {
        for (var i = 0; i < Lab03Identity.SearchPermitLimit; i++)
            Assert.Equal(HttpStatusCode.OK, await SearchAsync(client));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SearchAsync(client));
    }

    [Fact]
    public async Task Search_ExceedingLimit_Returns429_PartitionsAreIndependent_ThenRecovers()
    {
        // Окремий host: лічильники належать host, а не HttpClient.
        using var host = factory.WithWebHostBuilder(_ => { });
        using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var bob = await host.LoginAsync("bob", Password());
        using var alice = await host.LoginAsync("alice", Password());

        // Партиція 1: анонім вичерпав квоту.
        await ExhaustAsync(anonymous);

        // Партиція 2: bob не постраждав від квоти аноніма й має власну повну квоту.
        for (var i = 0; i < Lab03Identity.SearchPermitLimit; i++)
            Assert.Equal(HttpStatusCode.OK, await SearchAsync(bob));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SearchAsync(bob));

        // Партиція 3: alice не постраждала ні від аноніма, ні від bob.
        Assert.Equal(HttpStatusCode.OK, await SearchAsync(alice));
    }

    [Fact]
    public async Task Search_Recovers_AfterWindow()
    {
        using var host = factory.WithWebHostBuilder(_ => { });
        using var anonymous = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

        for (var i = 0; i < Lab03Identity.SearchPermitLimit; i++)
            Assert.Equal(HttpStatusCode.OK, await SearchAsync(anonymous));
        Assert.Equal(HttpStatusCode.TooManyRequests, await SearchAsync(anonymous));

        await Task.Delay(Lab03Identity.SearchWindow + TimeSpan.FromMilliseconds(500));

        Assert.Equal(HttpStatusCode.OK, await SearchAsync(anonymous));
    }
}
