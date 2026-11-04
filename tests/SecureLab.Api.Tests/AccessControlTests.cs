using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using Microsoft.AspNetCore.Hosting;

namespace SecureLab.Api.Tests;

public sealed class AccessControlTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private const string AliceCommentVisible = "30000000-0000-0000-0000-000000000001";
    private const string BobCommentVisible = "30000000-0000-0000-0000-000000000002";
    private const string AliceCommentInternal = "30000000-0000-0000-0000-000000000003";

    private static string Password() =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    // ---------- допоміжні методи ----------

    private async Task<Guid> CreateIncidentAsync(Guid ownerId, IncidentStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var now = DateTimeOffset.UtcNow;
        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerId,
            Title = "lab04-" + Guid.NewGuid().ToString("N")[..12],
            Description = "temporary lab04 test incident",
            Severity = IncidentSeverity.Low,
            Status = status,
            OccurredAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        return incident.Id;
    }

    private async Task DeleteIncidentAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident is not null)
        {
            db.Incidents.Remove(incident);
            await db.SaveChangesAsync();
        }
    }

    private async Task<(bool Exists, string Title, string Description, IncidentStatus Status, Guid Owner)>
        ReadStateAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
        var i = await db.Incidents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return i is null
            ? (false, "", "", default, default)
            : (true, i.Title, i.Description, i.Status, i.OwnerUserId);
    }

    private HttpClient Anonymous() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private static readonly Dictionary<string, HttpClient> Clients = new();
    private static readonly SemaphoreSlim LoginGate = new(1, 1);

    private async Task<HttpClient> Login(string user)
    {
        await LoginGate.WaitAsync();
        try
        {
            if (!Clients.TryGetValue(user, out var client))
            {
                client = await factory.LoginAsync(user, Password());
                Clients[user] = client;
            }
            return client;
        }
        finally { LoginGate.Release(); }
    }

    private static async Task<JsonNode> BodyWithoutTraceIdAsync(HttpResponseMessage response)
    {
        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        node.Remove("traceId"); // діагностичний, різний для кожного запиту
        return node;
    }

    // ---------- A-01: анонім ----------

    [Fact]
    public async Task A01_Anonymous_OnEveryIncidentRoute_Returns401()
    {
        using var client = Anonymous();
        var id = DbSeeder.AliceIncidentId;
        var body = JsonContent.Create(new { title = "x", description = "y" });

        var responses = new[]
        {
            await client.GetAsync("/api/incidents"),
            await client.GetAsync($"/api/incidents/{id}"),
            await client.PutAsync($"/api/incidents/{id}", body),
            await client.DeleteAsync($"/api/incidents/{id}"),
            await client.PostAsync($"/api/incidents/{id}/triage", null),
            await client.GetAsync($"/api/incident-comments/{AliceCommentVisible}")
        };

        foreach (var r in responses)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            r.Dispose();
        }
    }

    // ---------- A-02, A-03, A-03R: details ----------

    [Fact]
    public async Task A02_GetDetails_AsOwner_ReturnsOk()
    {
        var alice = await Login("alice");
        using var response = await alice.GetAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A03_GetDetails_OfForeignIncident_ReturnsNotFound()
    {
        var alice = await Login("alice");
        using var response = await alice.GetAsync($"/api/incidents/{DbSeeder.BobIncidentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A03R_GetDetails_Reverse_BobCannotReadAliceIncident()
    {
        var bob = await Login("bob");
        using var response = await bob.GetAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- A-04: mutation perimeter (update, delete) ----------

    [Fact]
    public async Task A04_UpdateAndDelete_AsNonOwner_ReturnNotFound_AndDoNotChangeResource()
    {
        var id = await CreateIncidentAsync(DbSeeder.AliceId, IncidentStatus.New);
        try
        {
            var before = await ReadStateAsync(id);
            var bob = await Login("bob");

            using var put = await bob.PutAsJsonAsync($"/api/incidents/{id}",
                new { title = "hacked-by-bob", description = "hacked" });
            using var delete = await bob.DeleteAsync($"/api/incidents/{id}");

            Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

            var after = await ReadStateAsync(id);
            Assert.True(after.Exists);
            Assert.Equal(before, after);
        }
        finally { await DeleteIncidentAsync(id); }
    }

    // ---------- A-06, A-07: privileged action triage ----------

    [Fact]
    public async Task A06_Triage_AsReporterAdminAndOtherReporter_IsDenied_AndStatusStaysNew()
    {
        var id = await CreateIncidentAsync(DbSeeder.AliceId, IncidentStatus.New);
        try
        {
            var alice = await Login("alice");
            var bob = await Login("bob");
            var admin = await Login("admin");

            using var owner = await alice.PostAsync($"/api/incidents/{id}/triage", null);
            using var other = await bob.PostAsync($"/api/incidents/{id}/triage", null);
            using var administrator = await admin.PostAsync($"/api/incidents/{id}/triage", null);

            Assert.Equal(HttpStatusCode.Forbidden, owner.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, administrator.StatusCode);

            var after = await ReadStateAsync(id);
            Assert.Equal(IncidentStatus.New, after.Status);
            Assert.Equal(DbSeeder.AliceId, after.Owner);
        }
        finally { await DeleteIncidentAsync(id); }
    }

    [Fact]
    public async Task A07_Triage_AsAnalyst_MovesNewToTriaged_ThenSecondCallIsConflict()
    {
        var id = await CreateIncidentAsync(DbSeeder.AliceId, IncidentStatus.New);
        try
        {
            var morgan = await Login("morgan");

            using var first = await morgan.PostAsync($"/api/incidents/{id}/triage", null);
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            Assert.Equal(IncidentStatus.Triaged, (await ReadStateAsync(id)).Status);

            using var second = await morgan.PostAsync($"/api/incidents/{id}/triage", null);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
            Assert.Equal(IncidentStatus.Triaged, (await ReadStateAsync(id)).Status);
        }
        finally { await DeleteIncidentAsync(id); }
    }

    // ---------- A-08a: overposting у update ----------

    [Fact]
    public async Task A08a_Update_WithForgedProtectedFields_IgnoresThem()
    {
        var id = await CreateIncidentAsync(DbSeeder.AliceId, IncidentStatus.New);
        try
        {
            var alice = await Login("alice");
            using var response = await alice.PutAsJsonAsync($"/api/incidents/{id}", new
            {
                title = "lab04-updated",
                description = "updated by owner",
                ownerUserId = DbSeeder.BobId,
                status = "Closed",
                role = "Administrator"
            });

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            var after = await ReadStateAsync(id);
            Assert.Equal("lab04-updated", after.Title);
            Assert.Equal(DbSeeder.AliceId, after.Owner);        // owner не змінився
            Assert.Equal(IncidentStatus.New, after.Status);      // status не змінився
        }
        finally { await DeleteIncidentAsync(id); }
    }

    // ---------- A-10: чужий і неіснуючий ресурс виглядають однаково ----------

    [Fact]
    public async Task A10_ForeignAndUnknownIncident_ReturnIdenticalNotFound()
    {
        var alice = await Login("alice");
        using var foreign = await alice.GetAsync($"/api/incidents/{DbSeeder.BobIncidentId}");
        using var unknown = await alice.GetAsync($"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(foreign.StatusCode, unknown.StatusCode);
        Assert.Equal(foreign.Content.Headers.ContentType?.MediaType,
            unknown.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            (await BodyWithoutTraceIdAsync(foreign)).ToJsonString(),
            (await BodyWithoutTraceIdAsync(unknown)).ToJsonString());
    }

    // ---------- A-12: child resource через parent ----------

    [Fact]
    public async Task A12_Comments_ReporterSeesOnlyVisibleCommentOfOwnIncident()
    {
        var alice = await Login("alice");

        using var own = await alice.GetAsync($"/api/incident-comments/{AliceCommentVisible}");
        using var foreignParent = await alice.GetAsync($"/api/incident-comments/{BobCommentVisible}");
        using var internalOwn = await alice.GetAsync($"/api/incident-comments/{AliceCommentInternal}");

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignParent.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, internalOwn.StatusCode);
    }

    [Fact]
    public async Task A12_Details_ForReporter_DoNotContainInternalComment_AnalystCanReadIt()
    {
        var alice = await Login("alice");
        using var details = await alice.GetAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.DoesNotContain("Внутрішня примітка", await details.Content.ReadAsStringAsync());

        var morgan = await Login("morgan");
        using var internalForAnalyst =
            await morgan.GetAsync($"/api/incident-comments/{AliceCommentInternal}");
        Assert.Equal(HttpStatusCode.OK, internalForAnalyst.StatusCode);
    }

    // ---------- A-09: audit після denied privileged action ----------

    [Fact]
    public async Task A09_DeniedTriage_WritesAuditEventWithSameTraceIdAsResponse()
    {
        var sink = new CapturingLoggerProvider();
        using var loggingFactory = factory.WithWebHostBuilder(
            builder => builder.ConfigureLogging(logging => logging.AddProvider(sink)));

        var id = await CreateIncidentAsync(DbSeeder.AliceId, IncidentStatus.New);
        try
        {
            using var client = loggingFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
            using (var login = await client.PostAsJsonAsync("/api/auth/login",
                       new { userName = "alice", password = Password() }))
                Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

            using var denied = await client.PostAsync($"/api/incidents/{id}/triage", null);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

            var traceId = JsonDocument.Parse(await denied.Content.ReadAsStringAsync())
                .RootElement.GetProperty("traceId").GetString();
            Assert.False(string.IsNullOrEmpty(traceId));

            var audit = Assert.Single(sink.Messages, m => m.Contains("authorization.denied"));
            Assert.Contains(DbSeeder.AliceId.ToString(), audit);   // server-verified subject
            Assert.Contains("incident.triage", audit);             // action
            Assert.Contains(id.ToString(), audit);                 // resource
            Assert.Contains("denied", audit);                      // result
            Assert.Contains(traceId!, audit);                      // той самий trace id
            Assert.DoesNotContain(Password(), audit);              // без секретів
        }
        finally { await DeleteIncidentAsync(id); }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = new();
        public IReadOnlyList<string> Messages { get { lock (_messages) return _messages.ToList(); } }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);
        public void Dispose() { }

        private void Add(string message) { lock (_messages) _messages.Add(message); }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
                => owner.Add(formatter(state, exception));
        }
    }
}