# Модель загроз SecureLab, ЛР 6

| Елемент | Ваш запис |
|---|---|
| Версія та середовище | review-start `d27f11a6b133c6b690ed849dad86f700190da5bf`; профіль Development (локально, `http://localhost:5080`), окремо production-like без launch profile (етап 1); PostgreSQL у Docker Compose на `127.0.0.1:54329`; reset/seed командою `--reset-database`; SDK 10.0.401 (global.json: 10.0.100, latestFeature); лише навчальні дані й акаунти |
| Компоненти в межах огляду | браузерний клієнт (`src/SecureLab.Api/Client`: `index`, `auth-lab`, `browser-lab`, `register-check` — `.html` і `.js`); ASP.NET Core API; PostgreSQL у Docker Compose; конфігурація (`appsettings*.json`, змінні середовища, `infra/compose.yaml`); консольні журнали. Redis не використовується |
| Активи | облікові дані користувачів (хеші паролів Identity) і cookie автентифікації; `Incident` та пов'язані `IncidentComment`, `IncidentStatusHistory` (власні записи, internal-коментарі); привілейований перехід `Incident.Status` (triage); audit-подія `authorization.denied`; секрети конфігурації (`SeedUsers__Password`, `ConnectionStrings__SecureLab`) |
| Суб'єкти | анонімний відвідувач; Reporter (Alice, Bob); Analyst (Morgan); Administrator (Admin); локальний розробник. Права — у `docs/access-matrix.md` |
| Entry points | `POST /api/auth/register`, `/login`, `/logout`; `GET /api/me`, `/api/auth/session-check`; `GET`/`POST /api/incidents`; `GET /api/incidents/{id}`, `/search`, `/severity-summary`; `PUT`/`DELETE /api/incidents/{id}`; `POST /api/incidents/{id}/triage`; `GET /api/incident-comments/{id}`; `GET /health`; статичні сторінки клієнта; завантаження конфігурації; restore NuGet; журнали |
| Межі довіри | browser→API, API→PostgreSQL, repository→build, environment→configuration |
| Наявні controls | validation DTO (тести `T01`–`T05`); Identity cookie (`HttpOnly`, `SameSite=Lax`, `Secure` поза Development; `Lab03Identity.cs`); авторизація за роллю й ownership (`docs/access-matrix.md`, тести `A01`–`A12`); пошук із ЛР 2 (`S02_*`, `T04_*`); безпечний рендер DOM (ЛР 5, тест `ClientScript_DoesNotUseDangerousInnerHtmlSink`); заголовки `nosniff`, `Referrer-Policy`, CSP `default-src 'self'; frame-ancestors 'none'` (`Program.cs`); CORS-політика `Lab05BrowserProbe` лише в Development (`Program.cs`); login rate limiter, 10 спроб за 10 с (`Lab03Identity.cs`, `LoginRateLimitTests`); спільний `AddProblemDetails()` + `UseExceptionHandler()`; audit `authorization.denied` для відмов triage (`IncidentEndpoints.cs`, тест `A09`) |
| Виключення | зовнішній penetration test не виконувався; немає production host і справжнього TLS (локальний HTTP); історія Git не сканувалась повністю; сторонні fork і зовнішній CI не перевірялись; Redis і file upload не входять до обраного рівня; antiforgery і rate limit для `GET /api/incidents/search` на момент початку огляду відсутні — додаються на етапі 2 |

## Схема меж довіри

```
[ Browser client ] ── недовірений ввід + автоматична auth cookie
        │   (локальний HTTP; HTTPS/HSTS — лише конфігурація)
        ▼
[ ASP.NET Core API ] ── validation, authn/authz, rate limit (login), error handling, logging
        │   connection string (з environment) / EF Core
        ▼
[ PostgreSQL (Docker Compose, 127.0.0.1:54329) ]

[ Repository + NuGet ] ──► restore/build
[ Environment variables + appsettings ] ──► старт API
```

## Очікувані властивості на межах

| Межа | Очікувана властивість (усі рівні) | Конкретний test або перевірка (добрий і відмінний) |
|---|---|---|
| browser→API | змінювальний запит із cookie без валідного antiforgery token відхиляється без зміни стану | REG-CSRF-01 (буде створено на етапі 2, п. 5) |
| browser→API | текст користувача не виконується як код у браузері | `IncidentEndpointTests.ClientScript_DoesNotUseDangerousInnerHtmlSink` |
| browser→API | auth cookie має `HttpOnly`, `Secure`, `SameSite=Lax` | `CookieFlagsTests.Login_OverHttps_SetsHttpOnlySecureLaxCookie` |
| browser→API | cross-origin читання дозволене лише потрібному origin | REG-CORS-01 (буде створено на етапі 2, п. 2) |
| browser→API | перебір пароля обмежується (429) і відновлюється після вікна | `LoginRateLimitTests.Login_ExceedingLimit_Returns429_ThenRecoversAfterWindow` |
| browser→API | чужий ресурс недоступний, відповідь не розкриває його існування (IDOR) | `AccessControlTests`: `A03`, `A04`, `A06`, `A10` |
| API→PostgreSQL | параметр пошуку не змінює структуру SQL | `Lab02RegressionTests`: `S02_*`, `T04_Search_WithApostrophe_Returns200AndSeededIncident` |
| repository→build | у tracked files немає реальних секретів; restore не змінює lockfile непомітно | `git grep` (етап 1, п. 3) + `dotnet restore --locked-mode` |
| environment→configuration | у Production не діє Development-конфіг, reset заборонений | доказ `Hosting environment: Production` і відмова `--reset-database` (етап 1, п. 4) |

Відмінний рівень: у `docs/security-review.md` простежте для ключових активів зв'язок «актив → загроза → control → перевірка».
