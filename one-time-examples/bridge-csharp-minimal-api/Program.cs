using System.ComponentModel.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IncidentSource>();
builder.Services.AddSingleton<IncidentLookupService>();
builder.Services.AddSingleton<GreetingService>();

var app = builder.Build();

app.MapGet("/hello", () =>
    Results.Ok(new MessageResponse(BridgeText.HelloMessage)));

app.MapGet("/incidents", (string owner, IncidentLookupService service) =>
{
    var incident = service.FindFirst(new IncidentQuery { Owner = owner });

    return incident is null
        ? Results.NotFound()
        : Results.Ok(incident);
});

app.MapGet("/delayed-hello", async (
    GreetingService service,
    CancellationToken cancellationToken) =>
{
    var message = await service.GetDelayedGreetingAsync(cancellationToken);
    return Results.Ok(new MessageResponse(message));
});

app.Run();

internal static class BridgeText
{
    // Крок зміни з мосту: змініть лише цей безпечний навчальний текст.
    internal const string HelloMessage = "Міст C# працює";
}

public sealed record MessageResponse(string Message);

public sealed record IncidentSummary(int Id, string Title);

public sealed class IncidentQuery
{
    [Required]
    public string? Owner { get; init; }
}

public sealed class IncidentSource
{
    public IReadOnlyList<Incident> Items { get; } =
    [
        new(1, "Перевірити журнал входів", "olena", false),
        new(2, "Оновити правила доступу", "maksym", true),
        new(3, "Перевірити резервну копію", "olena", false)
    ];
}

public sealed class IncidentLookupService(IncidentSource source)
{
    public IncidentSummary? FindFirst(IncidentQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return source.Items
            .Where(incident =>
                !incident.IsClosed && incident.Owner == query.Owner)
            .Select(incident =>
                new IncidentSummary(incident.Id, incident.Title))
            .FirstOrDefault();
    }
}

public sealed class GreetingService
{
    public async Task<string> GetDelayedGreetingAsync(
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        return BridgeText.HelloMessage;
    }
}

public sealed record Incident(
    int Id,
    string Title,
    string Owner,
    bool IsClosed);
