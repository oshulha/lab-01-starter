namespace SecureLab.Api.Application.Incidents;

// Єдине місце правил матриці доступу та єдині відповіді-відмови.
public static class IncidentAccess
{
    // Один helper для чужого й неіснуючого ресурсу: тіло однакове, без id.
    public static IResult NotFound() => Results.Problem(
        title: "Інцидент не знайдено",
        detail: "Інцидент не знайдено.",
        statusCode: StatusCodes.Status404NotFound);

    public static IResult Forbidden() => Results.Problem(
        title: "Forbidden",
        detail: "Ця дія недоступна для вашої ролі.",
        statusCode: StatusCodes.Status403Forbidden);

    // Автентифікація пройшла, але claim id відсутній: помилка сервера, не користувача.
    public static IResult Unidentified() => Results.Problem(
        statusCode: StatusCodes.Status500InternalServerError);

    // За матрицею: Administrator не читає інциденти.
    public static bool CanReadIncidents(CurrentUser user) => !user.IsAdministrator;

    // За матрицею: змінює вміст лише Reporter-власник (Analyst і Administrator: 403).
    public static bool CanChangeContent(CurrentUser user) => !user.IsAnalyst && !user.IsAdministrator;
}