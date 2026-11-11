using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using Xunit.Abstractions;

namespace SecureLab.Api.Tests;

public sealed class DisplayNameTests(SecureLabApiFactory factory, ITestOutputHelper output)
    : IClassFixture<SecureLabApiFactory>
{
    private static string Password => Environment.GetEnvironmentVariable("SeedUsers__Password") ?? throw new InvalidOperationException("Set local SeedUsers_Password");
    private static string NewUserName() => "dn" + Guid.NewGuid().ToString("N")[..8];
    private static Dictionary<string, object?> Body(string userName, string displayName) => new()
    {
        ["userName"] = userName,
        ["email"] = $"{userName}@example.test",
        ["displayName"] = displayName,
        ["password"] = Password
    };

    [Fact]
    public async Task DisplayNameTest()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);

        var maxLength = db.Model.FindEntityType(typeof(ApplicationUser))!.FindProperty(nameof(ApplicationUser.DisplayName))!.GetMaxLength();

        Assert.Equal(120, maxLength);
        var l = maxLength!.Value;

        var okName = NewUserName();
        var badName = NewUserName();

        try{
            using var ok = await client.PostAsJsonAsync("/api/auth/register", Body(okName, new string('x', l)));
            output.WriteLine($"L={l}, status={(int)ok.StatusCode}");

            Assert.Equal(HttpStatusCode.Created, ok.StatusCode);

            var saved = await users.FindByNameAsync(okName);
            Assert.NotNull(saved);
            Assert.Equal(l, saved!.DisplayName.Length);

            using var bad = await client.PostAsJsonAsync("/api/auth/register", Body(badName, new string('x', l + 1)));
            output.WriteLine($"L+1={l + 1} status={(int)bad.StatusCode}");

            Assert.NotEqual(HttpStatusCode.InternalServerError, bad.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

            Assert.Null(await users.FindByNameAsync(badName));
            Assert.Null(await users.FindByEmailAsync($"{badName}@example.test"));
            Assert.False(await db.Users.AnyAsync(u => u.NormalizedUserName == badName.ToUpperInvariant()));
        }
        finally{
            foreach (var name in new[] {okName, badName})
            {
                var leftover = await users.FindByNameAsync(name);
                if (leftover is not null)
                    await users.DeleteAsync(leftover);
            }
        }
    }
}