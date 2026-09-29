# Карта архітектури

Карта описує змінений у ЛР 1 маршрут `GET /api/incidents/severity-summary`
та компоненти, через які він проходить.

## Компоненти

| Компонент | Розташування | Відповідальність |
|---|---|---|
| Browser client | `src/SecureLab.Api/Client/` | Надсилає HTTP-запити, безпечно показує відповідь через DOM API |
| ASP.NET Core API (Presentation) | `src/SecureLab.Api/Presentation/` | Описує endpoints, читає зовнішні параметри, формує HTTP-відповідь і response DTO |
| Application | `src/SecureLab.Api/Application/` | Виконує сценарії читання інцидентів (список, деталі, підсумок за severity) |
| EF Core (Data) | `src/SecureLab.Api/Data/` | Відображає C#-сутності на PostgreSQL через EF Core/Npgsql, містить migration і seed/reset |
| PostgreSQL | `infra/compose.yaml` | Зберігає навчальні дані у локальному контейнері Docker Compose |

## Вибраний маршрут

`GET /api/incidents/severity-summary`: підсумок кількості інцидентів за severity.

```text
кнопка «Показати підсумок» (Client/index.html, #summary-button)
  → loadSeveritySummary у Client/app.js
  → GET /api/incidents/severity-summary
  → IncidentEndpoints.GetSeveritySummaryAsync
  → IncidentQueries.GetSeveritySummaryAsync
  → SecureLabDbContext.Incidents / таблиця incidents
  → IncidentSeveritySummaryResponse як JSON
  → textContent у списку #summary-list
```

Контракт: запит без body; відповідь `200 OK` з JSON-масивом елементів
`{ severity, count }`. Політика нульових груп: повний перелік рівнів (відсутні
мають `count: 0`). Порядок: за критичністю Low, Medium, High, Critical.

## Ключові файли маршруту

| Рівень | Файл | Що там |
|---|---|---|
| Клієнт (розмітка) | `src/SecureLab.Api/Client/index.html` | кнопка, статус і список підсумку |
| Клієнт (логіка) | `src/SecureLab.Api/Client/app.js` | `loadSeveritySummary`, `apiFetch`, запис у DOM через `textContent` |
| Endpoint | `src/SecureLab.Api/Presentation/Endpoints/IncidentEndpoints.cs` | реєстрація маршруту, `GetSeveritySummaryAsync` |
| Application layer | `src/SecureLab.Api/Application/Incidents/IncidentQueries.cs` | `GetSeveritySummaryAsync`: `AsNoTracking`, `GroupBy`, `Count()`, `ToListAsync`, нульові групи, порядок, log |
| Response DTO | `src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs` | `IncidentSeveritySummaryResponse` |
| DbContext | `src/SecureLab.Api/Data/SecureLabDbContext.cs` | `DbSet<Incident> Incidents`, відображення на таблицю |
| Таблиця | `incidents` у PostgreSQL | джерело даних для групування |

## Межі довіри

| Межа | Дані, що її перетинають | Чому даним ще не можна довіряти | Де перевіряємо або обмежуємо |
|---|---|---|---|
| Користувач → Browser client | введення у формі фільтра | користувач контролює введення | `<select>` лише спрощує введення; це не серверний контроль |
| Browser client → API | method, URL, headers | будь-який HTTP-клієнт може надіслати запит без сторінки й кнопки | для summary параметрів немає; для списку сервер перевіряє `status` (400), для деталей діє `:guid` і 404 |
| API → PostgreSQL | умови запиту, агрегація | збережений текст не стає безпечним лише тому, що він у БД | запити EF Core параметризовані, `AsNoTracking()`, явна проєкція |
| API → браузер | JSON відповіді | право читати entity не означає право отримати всі поля | окремий `IncidentSeveritySummaryResponse` тільки з `severity` і `count`, без description, owner ID, email і коментарів |
| Response → DOM | текстові значення з JSON | дані з БД могли походити від раніше введеного користувацького тексту | `textContent` і `createElement`; `innerHTML` не використовується |
| Конфігурація → API | connection string | локальна конфігурація не придатна для іншого середовища | значення поза Git через `ConnectionStrings__SecureLab` |

## Конфігураційні входи

Реальні значення у документі не наводяться.

- `global.json`: версія та смуга .NET SDK;
- `src/SecureLab.Api/appsettings*.json`: режим міграцій і локальний connection string для Development;
- `infra/compose.yaml`: версія PostgreSQL, порт і локальні навчальні облікові дані;
- `infra/.env.example`: лише приклад локального порту PostgreSQL; локальний `infra/.env` ігнорується Git;
- змінна середовища `ConnectionStrings__SecureLab`: безпечний спосіб перевизначити connection string поза репозиторієм.

## Повернення до відомого seed-стану

Зупинити API (Ctrl+C), потім:

```powershell
dotnet run --project src\SecureLab.Api -- --reset-database
```

Команда застосовує migrations, очищує лише відомі навчальні таблиці та
відновлює seed. Працює тільки в Development environment і не запускає API.
Після reset знову запустити API й повторити `GET /api/incidents` та
`GET /api/incidents/severity-summary`.