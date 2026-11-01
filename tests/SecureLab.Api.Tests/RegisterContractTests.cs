using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Tests;

public sealed class RegisterContractTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string Password =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    private static string NewUserName() => "reg" + Guid.NewGuid().ToString("N")[..8];

    private static Dictionary<string, object?> Body(
        string userName, string displayName = "Test User", string? password = null) => new()
    {
        ["userName"] = userName,
        ["email"] = $"{userName}@example.test",
        ["displayName"] = displayName,
        ["password"] = password ?? Password
    };

    [Fact]
    public async Task T01_Register_ValidRequest_Returns201WithSafeFieldsOnly()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/register", Body(NewUserName()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = document.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[] { "displayName", "id", "userName" }, names);
    }

    [Fact]
    public async Task T02_Register_Duplicate_Returns409ProblemDetails()
    {
        var body = Body(NewUserName());
        using var first = await _client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await _client.PostAsJsonAsync("/api/auth/register", body);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task T02_Register_WeakPassword_Returns400WithPasswordKey()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/auth/register", Body(NewUserName(), password: "short1!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task T02_Register_DisplayNameOver120_Returns400WithDisplayNameKey()
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/auth/register", Body(NewUserName(), displayName: new string('a', 121)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("displayName", out _));
    }

    [Fact]
    public async Task Register_ExtraFields_AreIgnored()
    {
        var userName = NewUserName();
        var sentId = Guid.NewGuid();
        var body = Body(userName);
        body["role"] = "Administrator";
        body["id"] = sentId;

        using var response = await _client.PostAsJsonAsync("/api/auth/register", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.NotEqual(sentId, document.RootElement.GetProperty("id").GetGuid());

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByNameAsync(userName);
        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user, DbSeeder.ReporterRole));
        Assert.False(await users.IsInRoleAsync(user, "Administrator"));
    }
}