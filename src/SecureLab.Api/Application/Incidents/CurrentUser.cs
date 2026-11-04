using System.Security.Claims;
using SecureLab.Api.Data;

namespace SecureLab.Api.Application.Incidents;

// Поточний користувач береться лише з verified principal (cookie), а не з body, URL чи заголовка.
public sealed record CurrentUser(Guid Id, bool IsAnalyst, bool IsAdministrator)
{
    public static CurrentUser? From(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(raw, out var id))
        {
            return null;
        }

        return new CurrentUser(
            id,
            principal.IsInRole(DbSeeder.AnalystRole),
            principal.IsInRole(DbSeeder.AdministratorRole));
    }
}