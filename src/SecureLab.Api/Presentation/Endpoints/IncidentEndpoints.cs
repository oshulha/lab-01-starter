using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;
using System.Diagnostics; 
using System.Security.Claims;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents")
            .RequireAuthorization();   // анонім отримує 401 до виконання endpoint

        group.MapGet("/", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            .Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>();

        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateIncident");

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteIncident");

        group.MapPost("/{id:guid}/triage", TriageAsync)
            .WithName("TriageIncident");

        endpoints.MapGet("/api/incident-comments/{id:guid}", GetCommentAsync)
            .WithName("GetIncidentComment")
            .WithTags("Incidents")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        HttpContext httpContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var user = CurrentUser.From(httpContext.User);
        if (user is null) return IncidentAccess.Unidentified();
        if (!IncidentAccess.CanReadIncidents(user)) return IncidentAccess.Forbidden();

        IncidentStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                });
            }

            parsedStatus = candidate;
        }

        return Results.Ok(await queries.GetListAsync(parsedStatus, user, cancellationToken));
    }

    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        HttpContext httpContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var user = CurrentUser.From(httpContext.User);
        if (user is null) return IncidentAccess.Unidentified();
        if (!IncidentAccess.CanReadIncidents(user)) return IncidentAccess.Forbidden();

        var incident = await queries.GetDetailsAsync(id, user, cancellationToken);
        return incident is null ? IncidentAccess.NotFound() : Results.Ok(incident);
    }

    private static async Task<IResult> GetSeveritySummaryAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: false, out var value)
                || !Enum.IsDefined(value))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = [$"Допустимі значення: {string.Join(", ", Enum.GetNames<IncidentStatus>())}."]
                });
            }

            parsedStatus = value;
        }

        var summary = await queries.GetSeveritySummaryAsync(parsedStatus, cancellationToken);
        return Results.Ok(summary);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateIncidentRequest request,
        HttpContext httpContext,
        SecureLabDbContext db,
        CancellationToken cancellationToken)
    {
        var user = CurrentUser.From(httpContext.User);
        if (user is null) return IncidentAccess.Unidentified();
        if (!IncidentAccess.CanChangeContent(user)) return IncidentAccess.Forbidden();

        // Завантаження БЕЗ AsNoTracking(): інакше SaveChanges нічого не запише.
        var incident = await db.Incidents.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (incident is null || incident.OwnerUserId != user.Id)
        {
            return IncidentAccess.NotFound();   // чужий = неіснуючий
        }

        // Валідація лише ПІСЛЯ рішення про право.
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Title))
            errors["title"] = ["Заголовок обов'язковий."];
        if (string.IsNullOrWhiteSpace(request.Description))
            errors["description"] = ["Опис обов'язковий."];
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        incident.Title = request.Title!.Trim();
        incident.Description = request.Description!.Trim();
        incident.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> TriageAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext db,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var user = CurrentUser.From(httpContext.User);
        if (user is null) return IncidentAccess.Unidentified();

        // Tracked entity (без AsNoTracking): інакше SaveChanges нічого не запише.
        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        // 1) Рішення за роллю й ресурсом, ДО будь-якої зміни стану.
        if (!user.IsAnalyst)
        {
            // Administrator або власник-Reporter: роль не дозволена -> 403 + audit.
            if (user.IsAdministrator || (incident is not null && incident.OwnerUserId == user.Id))
            {
                AuditDenied(logger, httpContext, user.Id, "incident.triage", id);
                return IncidentAccess.Forbidden();
            }
            // Чужий або неіснуючий ресурс: однакове 404 без id у detail.
            return IncidentAccess.NotFound();
        }

        if (incident is null) return IncidentAccess.NotFound();

        // 2) Допустимість поточного status.
        if (incident.Status != IncidentStatus.New)
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Конфлікт стану",
                detail: "Перехід у Triaged можливий лише зі статусу New.");

        // 3) Next status визначає СЕРВЕР; request нічого не передає.
        incident.Status = IncidentStatus.Triaged;
        incident.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static void AuditDenied(
        ILogger logger, HttpContext httpContext, Guid subjectId, string action, Guid resourceId)
    {
        // Той самий trace id, що Problem Details віддає клієнтові.
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        logger.LogWarning(
            "Audit event {EventName} subject={SubjectId} action={Action} resource=Incident/{ResourceId} " +
            "result={Result} occurredAtUtc={OccurredAtUtc:o} traceId={TraceId}",
            "authorization.denied", subjectId, action, resourceId, "denied",
            DateTimeOffset.UtcNow, traceId);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        HttpContext httpContext,
        SecureLabDbContext db,
        CancellationToken cancellationToken)
    {
        var user = CurrentUser.From(httpContext.User);
        if (user is null) return IncidentAccess.Unidentified();
        if (!IncidentAccess.CanChangeContent(user)) return IncidentAccess.Forbidden();

        var incident = await db.Incidents.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (incident is null || incident.OwnerUserId != user.Id)
        {
            return IncidentAccess.NotFound();
        }

        db.Incidents.Remove(incident);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCommentAsync(
        Guid id,
        HttpContext httpContext,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var user = CurrentUser.From(httpContext.User);
        if (user is null) return IncidentAccess.Unidentified();
        if (!IncidentAccess.CanReadIncidents(user)) return IncidentAccess.Forbidden();

        var comment = await queries.GetCommentAsync(id, user, cancellationToken);
        return comment is null ? IncidentAccess.NotFound() : Results.Ok(comment);
    }
}