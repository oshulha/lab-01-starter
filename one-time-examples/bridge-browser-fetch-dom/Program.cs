var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/greetings", (GreetingRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new ErrorResponse("Уведіть ім’я."));
    }

    var normalizedName = request.Name.Trim();
    return Results.Ok(new GreetingResponse($"Вітаємо, {normalizedName}!"));
});

app.Run();

public sealed record GreetingRequest(string? Name);

public sealed record GreetingResponse(string Message);

public sealed record ErrorResponse(string Error);
