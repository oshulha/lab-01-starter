# Власна карта source-to-sink

Поле: Incident.Description.
Vulnerable commit: 0545af589fc9c90717aaee8b127cfd2b0097c81d. Вхід/Guid запису: 4ccc0040-660f-4d25-ab2e-5e2ba305930b (Description із marker); для другого потоку (коментарі) — 20000000-0000-0000-0000-000000000001.

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `Client/browser-lab.html`, `<form id="edit">` | `<textarea name="description">` | вводить користувач, недовірене |
| Request | `Client/browser-lab.js`, submit-обробник, рядки 22–24 | `POST /api/incidents`, JSON-поле `description` | ![Network → POST → Payload](evidence\POST_api_incidents.jpg) |
| Validation/mapping | `Scaffolding/Lab02Endpoints.cs`, `MapPost("/api/incidents")`; DTO `CreateIncidentRequest` | не порожній, ≤ 4000, `Trim()`, у `Incident.Description` | перевірка довжини не очищає HTML |
| Storage | `Data/SecureLabDbContext.cs` | таблиця `incidents`, колонка `description` | зберігається як є, недовірене |
| Response | `GET /api/incidents/{id}` → `IncidentQueries.cs` → `IncidentDetailsResponse.Description` | JSON-поле `description` | ![Network → GET → Response](evidence\GET_api_incidents_id.jpg) |
| Client transform | `browser-lab.js`, `load()`, рядок 8 | `current = await response.json()` | значення не змінюється |
| Sink/context | `browser-lab.js`, рядок 10, елемент `#description` | `innerHTML = current.description` | HTML-контекст, небезпечно |

## Інвентаризація виведення в `browser-lab.js`

| Файл/функція | Категорія даних | Контекст | Рішення |
|---|---|---|---|
| `load()`: `#title` | `current.title` з БД, недовірене | текст | `textContent`, без змін |
| `load()`: `#description` | `current.description` з БД, недовірене | HTML (було `innerHTML`) → текст | `renderText`: текстовий вузол |
| `load()`: `#comments > p` | `comments[].text` з БД, недовірене | HTML (було `innerHTML`) → текст | `renderText`: текстовий вузол |
| `load()`: `elements.description.value` | `current.description` | значення поля форми | безпечно: `value` не інтерпретує HTML |
| `#status` | HTTP-статус, не дані користувача | текст | `textContent`, без змін |

## Другий потік: IncidentComment.Text

| Крок | Файл і функція/DTO | Поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `Data/DbSeeder.cs`, `SeedAsync`, коментар `…0005` | `Text = Lab05Marker` (у продукті автор коментаря) | seed, недовірене |
| Request | endpoint створення коментаря відсутній | запиту немає, лише читання | — |
| Validation/mapping | `IncidentQueries.cs`: `comment.Text` → `IncidentCommentResponse.Text` | поле `Text` | перевірки немає, HTML не очищається |
| Storage | `SecureLabDbContext.cs` | таблиця `incident_comments`, колонка `text` | збережене значення |
| Response | `GET /api/incidents/{id}` | `comments[].text` | `before-comments.json` |
| Client transform | `browser-lab.js`, цикл `for` (рядки 12–16) | `comment.text` без змін | не очищається |
| Sink/context | `browser-lab.js`, рядок 14 | `paragraph.innerHTML = comment.text`, HTML-контекст | скріншот Elements з `<img>` у `#comments` |

## Після виправлення

У fixed-версії `browser-lab.js` helper `renderText(container, text)` викликає `container.replaceChildren(document.createTextNode(text))`; він використовується для `#description` (рядок 15) і для кожного `comments[].text` (рядок 19). Sink змінено на текстовий вузол, збережене значення в БД та відповідь API не змінюються.
