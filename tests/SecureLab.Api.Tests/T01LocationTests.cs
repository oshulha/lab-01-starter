using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using Xunit.Abstractions;

namespace SecureLab.Api.Tests;

public sealed class T01LocationTests(SecureLabApiFactory factory, ITestOutputHelper output)
    : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task T01LocationTest(){
        using var client = factory.CreateClient();
        var title = $"T-01 {Guid.NewGuid():N}";
        Guid? createdId = null;

        try{
            using var post = await client.PostAsJsonAsync("/api/incidents", new
            {
                title,
                description = "new some interesting description",
                severity = "Medium",
                occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            });

            Assert.Equal(HttpStatusCode.Created, post.StatusCode); // A1

            using var document = JsonDocument.Parse(await post.Content.ReadAsStringAsync());
            var bodyId = document.RootElement.GetProperty("id").GetGuid();
            createdId = bodyId;

            Assert.NotEqual(Guid.Empty, bodyId); // A2

            var location = post.Headers.Location;

            Assert.NotNull(location); // A3

            output.WriteLine($"POST: status={(int)post.StatusCode} bodyId={bodyId} location={location}");

            var resolved = new Uri(client.BaseAddress!, location);

            Assert.Equal(client.BaseAddress!.GetLeftPart(UriPartial.Authority), resolved.GetLeftPart(UriPartial.Authority)); // A4

            Assert.Equal($"/api/incidents/{bodyId}", resolved.PathAndQuery); // A5

            using var get = await client.GetAsync(resolved.PathAndQuery);

            Assert.Equal(HttpStatusCode.OK, get.StatusCode); // A6

            using var getDocument = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
            var getId = getDocument.RootElement.GetProperty("id").GetGuid();
            output.WriteLine($"GET: status={(int)get.StatusCode} getId={getId}");
            Assert.Equal(bodyId, getId); // A7
        }
        finally{
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
            if (createdId is { } id)
                await db.Incidents.Where(item => item.Id == id).ExecuteDeleteAsync();
            await db.Incidents.Where(item => item.Title == title).ExecuteDeleteAsync();
        }
    }

}