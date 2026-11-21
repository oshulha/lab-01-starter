using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

public static class CookieClient
{
    public static async Task<HttpClient> LoginAsync(
        this SecureLabApiFactory factory, string userName, string password, bool withCsrf = true)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
        });
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        if (!response.IsSuccessStatusCode)
        {
            client.Dispose();
            throw new InvalidOperationException("Study login failed (credentials omitted).");
        }
        if (withCsrf) await client.AddCsrfAsync();
        return client;
    }

    // F-02: отримує antiforgery-токен для поточного сеансу й додає його до всіх запитів клієнта.
    public static async Task AddCsrfAsync(this HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/csrf");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", json.RootElement.GetProperty("token").GetString());
    }
}
