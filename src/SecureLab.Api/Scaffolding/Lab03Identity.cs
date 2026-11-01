using Microsoft.AspNetCore.Identity;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

public static class Lab03Identity
{
    public static void AddLab03Identity(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
        }).AddEntityFrameworkStores<SecureLabDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    }

    public static void MapLab03Identity(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (RegistrationInput input, UserManager<ApplicationUser> users) =>
        {
            var errors = new Dictionary<string, string[]>();

            // Нормалізуємо лише ті поля, для яких це визначає контракт. Пароль не чіпаємо.
            var userName = input.UserName?.Trim() ?? "";
            var email = input.Email?.Trim() ?? "";
            var displayName = input.DisplayName?.Trim() ?? "";

            if (userName.Length == 0)
                errors["userName"] = ["Ім'я користувача обов'язкове."];
            else if (userName.Length > 256)
                errors["userName"] = ["Ім'я користувача не довше за 256 символів."];

            if (email.Length == 0)
                errors["email"] = ["Email обов'язковий."];
            else if (email.Length > 256)
                errors["email"] = ["Email не довший за 256 символів."];

            if (displayName.Length == 0)
                errors["displayName"] = ["Відображуване ім'я обов'язкове."];
            else if (displayName.Length > 120)
                errors["displayName"] = ["Відображуване ім'я не довше за 120 символів."];

            if (string.IsNullOrEmpty(input.Password))
                errors["password"] = ["Пароль обов'язковий."];
            else if (input.Password.Length > 128)
                errors["password"] = ["Пароль не довший за 128 символів."];

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                Email = email,
                DisplayName = displayName
            };

            var result = await users.CreateAsync(user, input.Password!);
            if (!result.Succeeded)
            {
                var codes = result.Errors.Select(error => error.Code).ToHashSet();

                // Однакова відповідь для зайнятого імені й зайнятого email.
                if (codes.Contains("DuplicateUserName") || codes.Contains("DuplicateEmail"))
                    return Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Конфлікт даних",
                        detail: "Користувач із такими даними вже існує.");

                var identityErrors = new Dictionary<string, string[]>();
                if (codes.Contains("InvalidUserName"))
                    identityErrors["userName"] = ["Дозволені латинські літери, цифри та - . _ @ +"];
                if (codes.Contains("InvalidEmail"))
                    identityErrors["email"] = ["Некоректний формат email."];
                if (codes.Any(code => code.StartsWith("Password", StringComparison.Ordinal)))
                    identityErrors["password"] =
                        ["Щонайменше 10 символів, велика й мала літери, цифра та спеціальний символ."];
                if (identityErrors.Count == 0)
                    identityErrors["request"] = ["Реєстрацію не виконано."];
                return Results.ValidationProblem(identityErrors);
            }

            var role = await users.AddToRoleAsync(user, DbSeeder.ReporterRole);
            if (!role.Succeeded)
                throw new InvalidOperationException("Default role assignment failed.");

            return Results.Created("/api/me", new RegisteredUserResponse(user.Id, userName, displayName));
        });
        app.MapPost("/api/auth/login", async (LoginInput input, SignInManager<ApplicationUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(input.UserName) || string.IsNullOrEmpty(input.Password))
                return Results.Unauthorized();
            var result = await signIn.PasswordSignInAsync(input.UserName, input.Password, false, false);
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        });
        app.MapPost("/api/auth/logout", () => Results.StatusCode(StatusCodes.Status501NotImplemented));
        // Навмисно хибна навчальна довіра: ЛР 03 замінює заявлений id на verified principal.
        app.MapGet("/api/me", async (HttpContext context, SecureLabDbContext db) =>
        {
            if (!Guid.TryParse(context.Request.Headers["X-Demo-UserId"], out var claimedId))
                return Results.StatusCode(StatusCodes.Status501NotImplemented);
            var user = await db.Users.FindAsync(claimedId);
            return user is null ? Results.NotFound() : Results.Ok(new { user.Id, user.UserName, user.DisplayName });
        });
        // Приклад лише mechanics: не повертає identity і не реалізує оцінювану operation.
        app.MapGet("/api/auth/session-check", () => Results.NoContent()).RequireAuthorization();
    }
}

public sealed record RegisteredUserResponse(Guid Id, string UserName, string DisplayName);
public sealed record RegistrationInput(string? UserName, string? Email, string? DisplayName, string? Password);
public sealed record LoginInput(string? UserName, string? Password);
