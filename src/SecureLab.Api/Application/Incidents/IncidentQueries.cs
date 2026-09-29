using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

using Microsoft.Extensions.Logging;

namespace SecureLab.Api.Application.Incidents;

public sealed class IncidentQueries(SecureLabDbContext dbContext, ILogger<IncidentQueries> logger)
{
    
    private static readonly IncidentSeverity[] SeverityOrder =
    [
        IncidentSeverity.Low,
        IncidentSeverity.Medium,
        IncidentSeverity.High,
        IncidentSeverity.Critical
    ];

    public async Task<IReadOnlyList<IncidentSeveritySummaryResponse>> GetSeveritySummaryAsync(
        IncidentStatus? status,
        CancellationToken cancellationToken)
    {
        var incidents = dbContext.Incidents.AsNoTracking();

        if (status is not null)
        {
            incidents = incidents.Where(incident => incident.Status == status);
        }

        var grouped = await incidents
            .GroupBy(incident => incident.Severity)
            .Select(group => new { Severity = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var counts = grouped.ToDictionary(item => item.Severity, item => item.Count);

        var result = SeverityOrder
            .Select(severity => new IncidentSeveritySummaryResponse(
                severity.ToString(),
                counts.GetValueOrDefault(severity)))
            .ToList();

        logger.LogInformation(
            "Built incident severity summary with {GroupCount} groups for status {Status}",
            result.Count,
            status?.ToString() ?? "any");

        return result;
    }
    
    public async Task<IReadOnlyList<IncidentListItemResponse>> GetListAsync(
        IncidentStatus? status,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading incidents with status filter {Status}", status);

        var query = dbContext.Incidents.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(incident => incident.Status == status);
        }

        return await query
            .OrderByDescending(incident => incident.CreatedAtUtc)
            .Select(incident => new IncidentListItemResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public Task<IncidentDetailsResponse?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading incident {IncidentId}", id);

        return dbContext.Incidents
            .AsNoTracking()
            .Where(incident => incident.Id == id)
            .Select(incident => new IncidentDetailsResponse(
                incident.Id,
                incident.Title,
                incident.Description,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.Owner.DisplayName,
                incident.Comments
                    .Where(comment => !comment.IsInternal)
                    .OrderBy(comment => comment.CreatedAtUtc)
                    .Select(comment => new IncidentCommentResponse(
                        comment.Id,
                        comment.Author.DisplayName,
                        comment.Text,
                        comment.CreatedAtUtc))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
