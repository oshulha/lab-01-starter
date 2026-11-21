using System.Net;

namespace SecureLab.Api.Tests;

// REG-CORS-01: чужий Origin не отримує дозволу читати відповіді; дозволений лише явно перелічений.
// Політика Lab05BrowserProbe діє лише в Development; у Production CORS вимкнено повністю
// (перевірено вручну: відповідь без Access-Control-Allow-Origin).
public sealed class CorsRegressionTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private const string Allowed = "http://localhost:4173";

    private async Task<HttpResponseMessage> GetWithOriginAsync(string origin)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("X-Lab-Cors-Probe", "1");
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task REG_CORS_01_ForeignOrigin_GetsNoAllowOriginHeader()
    {
        using var response = await GetWithOriginAsync("https://evil.example");
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task REG_CORS_01_ForeignOrigin_Preflight_IsNotAllowed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/incidents");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        using var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task REG_CORS_01_AllowedOrigin_IsEchoedExactly_NotWildcard()
    {
        using var response = await GetWithOriginAsync(Allowed);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Allowed, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}
