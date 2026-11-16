# Власна карта source-to-sink

Поле: Incident.Description. 
Vulnerable commit: 0545af589fc9c90717aaee8b127cfd2b0097c81d. Вхід/Guid запису: 20000000-0000-0000-0000-000000000001.

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `Client/browser-lab.html`, `<form id="edit">` | `<textarea name="description">` | вводить користувач, недовірене |
| Request | `Client/browser-lab.js`, submit-обробник, рядки 22–24 | `POST /api/incidents`, JSON-поле `description` | ![Network → POST → Payload](evidence\POST_api_incidents.jpg) |
| Validation/mapping | `Scaffolding/Lab02Endpoints.cs`, `MapPost("/api/incidents")`; DTO `CreateIncidentRequest` | не порожній, ≤ 4000, `Trim()`, у `Incident.Description` | перевірка довжини не очищає HTML |
| Storage | `Data/SecureLabDbContext.cs` | таблиця `incidents`, колонка `description` | зберігається як є, недовірене |
| Response | `GET /api/incidents/{id}` → `IncidentQueries.cs` → `IncidentDetailsResponse.Description` | JSON-поле `description` | ![Network → GET → Response](evidence\GET_api_incidents_id.jpg) |
| Client transform | `browser-lab.js`, `load()`, рядок 8 | `current = await response.json()` | значення не змінюється |
| Sink/context | `browser-lab.js`, рядок 10, елемент `#description` | `innerHTML = current.description` | HTML-контекст, небезпечно |

Інвентаризація: файл/функція → категорія даних → контекст → обґрунтоване рішення. Для «Добре / Відмінно» додайте повний другий потік IncidentComment.Text за тією самою таблицею. Для «Відмінно» додайте результат поглибленої перевірки comments/повторного render. Рівні накопичувальні; основний Description потрібний для всіх.
