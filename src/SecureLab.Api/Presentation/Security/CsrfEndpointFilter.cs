using Microsoft.AspNetCore.Antiforgery;

namespace SecureLab.Api.Presentation.Security;

// F-02: усі змінюючі маршрути з cookie-автентифікацією вимагають antiforgery-токен
// у заголовку X-CSRF-TOKEN (токен прив'язано до поточного користувача).
public sealed class CsrfEndpointFilter(IAntiforgery antiforgery, ILogger<CsrfEndpointFilter> logger)
    : IEndpointFilter
{
    public const string HeaderName = "X-CSRF-TOKEN";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);
        }
        catch (AntiforgeryValidationException)
        {
            logger.LogWarning(
                "Security event {EventType}: result {Result}, method {Method}, trace {TraceId}",
                "csrf.rejected", "denied", httpContext.Request.Method, httpContext.TraceIdentifier);
            return Results.Problem(
                title: "Відсутній або невалідний CSRF-токен",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
