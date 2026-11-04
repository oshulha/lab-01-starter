namespace SecureLab.Api.Presentation.Contracts;

// УМИСНО ВРАЗЛИВО (локальний PoC ЛР 4): DTO несе protected-поля.
public sealed record TriageIncidentRequest(
    string? Role, string? Status, Guid? OwnerUserId, bool? IsTransitionAllowed);