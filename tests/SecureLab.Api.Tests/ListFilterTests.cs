// Виданий мутант (OR, без другого Where):
//   .Where(i => i.OwnerUserId == userId || i.Status == requestedStatus)
//
// Альтернативний мутант (після OR додано AND status):
//   .Where(i => i.OwnerUserId == userId || i.Status == requestedStatus)
//   .Where(i => i.Status == requestedStatus)
//
// Правильний (як в IncidentQueries):
//   .Where(i => i.OwnerUserId == userId)
//   .Where(i => i.Status == requestedStatus)

using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Tests;

public sealed class ListFilterTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
    {
        private static string Password() =>
        Environment.GetEnvironmentVariable("SeedUsers__Password") ?? throw new InvalidOperationException("Set local SeedUsers__Pssword");

        private async Task<Guid> CreateAsync(Guid owner, IncidentStatus status)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();
            var now = DateTimeOffset.UtcNow;
            var i = new Incident
            {
                Id = Guid.NewGuid(),
                OwnerUserId = owner,
                Title = "lab4" + Guid.NewGuid().ToString("N")[..8],
                Description = "list filter fixture",
                Severity = IncidentSeverity.Low,
                Status = status,
                OccurredAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now 
            };
            db.Incidents.Add(i);
            await db.SaveChangesAsync();
            return i.Id;
        }

        private async Task DeleteAsync(params Guid[] ids)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

            db.Incidents.RemoveRange(await db.Incidents.Where(i => ids.Contains(i.Id)).ToListAsync());
            await db.SaveChangesAsync();
        }
        
        [Theory]
        [InlineData("InProgress")]
        [InlineData("New")]
        public async Task AliceIncidentsVisibility(string status)
        {
            var i5 = await CreateAsync(DbSeeder.AliceId, IncidentStatus.New);
            var i4 = await CreateAsync(DbSeeder.BobId, IncidentStatus.New);
            try{
                var i1 = DbSeeder.AliceIncidentId;
                var i2 = DbSeeder.BobIncidentId;
                var i3 = DbSeeder.SafeTextIncidentId;
                var fixture = new HashSet<Guid> {i1, i2, i3, i4, i5};
                var bobs = new HashSet<Guid> {i2, i4};
                using var alice = await factory.LoginAsync("alice", Password());
                var list = await alice.GetFromJsonAsync<List<IncidentListItemResponse>>($"/api/incidents?status={status}");
                var seen = list!.Where(x => fixture.Contains(x.Id)).ToList();
                Assert.DoesNotContain(seen, x => bobs.Contains(x.Id));
                Assert.All(seen, x => Assert.Equal(status, x.Status));
                var expected = status == "New" ? new HashSet<Guid> {i3, i5} : new HashSet<Guid>();
                Assert.Equal(expected, seen.Select(x => x.Id).ToHashSet());
            }
            finally{
                await DeleteAsync(i4, i5);
            }
        }
    }