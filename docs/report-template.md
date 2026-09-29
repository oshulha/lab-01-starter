# Звіт до лабораторної роботи № 1

## 1. Ідентифікація стану

- Варіант: 2-A «Трекер інцидентів».
- Гілка: `lab/1-system` (основна: `main`).
- Фінальний тег: `v0.1.0`.
- Commit hash: `3157e3058e7123b01b7cdfcaf08b271d89d59577`

## 2. Змінений маршрут

Реалізовано наскрізну функцію «підсумок інцидентів за severity»:

```
кнопка «Показати підсумок» (Client/index.html, #summary-button)
 → loadSeveritySummary (Client/app.js)
 → GET /api/incidents/severity-summary
 → IncidentEndpoints.GetSeveritySummaryAsync (Presentation/Endpoints/IncidentEndpoints.cs)
 → IncidentQueries.GetSeveritySummaryAsync (Application/Incidents/IncidentQueries.cs)
 → SecureLabDbContext.Incidents → таблиця incidents (PostgreSQL)
 → IncidentSeveritySummaryResponse (Presentation/Contracts/IncidentResponses.cs)
 → JSON-масив { severity, count }
 → DOM: createElement("li") + textContent у #summary-list
```

Ключовий фрагмент query (спрощено):

```
dbContext.Incidents.AsNoTracking()
    .GroupBy(i => i.Severity)
    .Select(g => new { Severity = g.Key, Count = g.Count() })
    .ToListAsync(cancellationToken);
```

Після матеріалізації агрегату відсутні рівні доповнюються нулями, результат
впорядковується за критичністю (Low, Medium, High, Critical).

Межі довіри на маршруті:
1. браузер → API: method і URL контролює клієнт; endpoint не приймає параметрів,
   тому вхідних даних для зловживання немає, але будь-який HTTP-клієнт може
   викликати його без кнопки.
2. API → PostgreSQL: запит формує EF Core (параметризація), запит лише на читання
   (`AsNoTracking`).
3. API → браузер: окремий response DTO містить лише `severity` і `count`, без
   description, owner ID, email і коментарів.
4. response → DOM: значення записуються через `textContent`, а не як HTML.

## 3. Виконані зміни

- `IncidentResponses.cs`: додано `IncidentSeveritySummaryResponse(string Severity, int Count)`.
- `IncidentQueries.cs`: додано `GetSeveritySummaryAsync` (`AsNoTracking`, `GroupBy`,
  `Count()`, `ToListAsync(cancellationToken)`), політику нульових груп, сталий
  порядок і структурований log із кількістю груп (без чутливих даних).
- `IncidentEndpoints.cs`: заглушку 501 замінено на `GetSeveritySummaryAsync`
  (DI, `CancellationToken`, `Results.Ok`), metadata
  `.Produces<IReadOnlyList<IncidentSeveritySummaryResponse>>()`.
- `Client/index.html`, `Client/app.js`: кнопка, статус і список; стани
  «Завантаження…», «Даних немає» та безпечне повідомлення про помилку.
- `tests/http/incidents.http`: контракт, політика й порядок для summary, додано
  сценарій `?status=Resolved`.

Політика нульових груп: повний перелік рівнів (відсутні мають `count: 0`).
Порядок: за критичністю Low, Medium, High, Critical (явний порядок після
матеріалізації агрегату, бо SQL-сортування тексту було б лексикографічним).

## 4. Перевірка

| ID | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|
| T-01 | PostgreSQL healthy, API запущено | `GET /health` | 200, API бачить БД | TODO | TODO (вивід `docker compose ... ps` і відповідь `/health`) |
| T-02 | відновлений seed | `GET /api/incidents?status=Triaged` | 200, список відповідає фільтру | TODO | TODO (Network + response) |
| T-03 | відновлений seed | `GET /api/incidents?status=Resolved` | 200 з порожнім масивом | 200 OK, `Content-Type: application/json; charset=utf-8`, тіло `[]`, 91 ms | скріншот відповіді REST Client |
| T-04 | відновлений seed | `GET /api/incidents/99999999-9999-9999-9999-999999999999` | 404 Problem Details | 404 Not Found, `Content-Type: application/problem+json`, title «Інцидент не знайдено», detail «Інцидент '99999999-9999-9999-9999-999999999999' не існує.», traceId `00-331851c097d0dfdcd42cbfa4ca2ec237-c6a0bc48427607b1-00`, 84 ms | скріншот відповіді REST Client |
| T-05 | відновлений seed | `GET /api/incidents?status=Unknown` | 400 Validation Problem Details | 400 Bad Request, `Content-Type: application/problem+json`, title «One or more validation errors occurred.», `errors.status`: «Допустимі значення: New, Triaged, InProgress, Resolved, Closed.», traceId `00-bee5a760955ce8912f4d162a36b5fc5f-a6d70a98bf63728f-00`, 9 ms | скріншот відповіді REST Client |
| T-06 | реалізований етап 3, відновлений seed | `GET /api/incidents/severity-summary` | 200; Low, Medium, High по 1, Critical 0; порядок за критичністю | 200 OK, `Content-Type: application/json; charset=utf-8`, 4 елементи у порядку Low 1, Medium 1, High 1, Critical 0, 48 ms | скріншот відповіді REST Client |
| T-07 | API і клієнт запущено | натиснути «Показати підсумок» | UI безпечно показує результат; є стани завантаження, порожнього результату й помилки | У списку показано Low: 1, Medium: 1, High: 1, Critical: 0. Гілки «Завантаження…», «Даних немає» та повідомлення про помилку присутні в `app.js`. TODO: додати запис Network для цього запиту та результат перевірки стану помилки (зупинити API, натиснути кнопку) | скріншот UI + TODO (Network) |
| T-08 | після зміни даних, якщо робили | `--reset-database`, повторити T-02 і T-06 | seed повертає стенд до відомого стану | TODO | TODO (команда й повторний response) |

Додатково для звіту (TODO):
- відповідь `501` для `severity-summary` до реалізації (baseline);
- Network-запис успішного `GET /api/incidents/20000000-0000-0000-0000-000000000003` і гілки 404 з `traceId` цього запуску (CP-02);
- результат `dotnet test` (очікується 4 із 4, записати лише фактичний вивід);
- підтвердження перегляду `git diff --staged` на відсутність секретів.

## 5. Security-сценарій

Не застосовується: ЛР 1 виконується на baseline без навмисно вразливого стану.
Безпека виражена контролями на маршруті: маршрутне обмеження `:guid` та 404
замість помилки сервера для відсутнього ресурсу, серверна перевірка параметра
`status` (400), явна проєкція в response DTO без зайвих полів, безпечний вивід
у DOM через `textContent`.

## 6. Висновок

Endpoint `GET /api/incidents/severity-summary` замість baseline-відповіді 501
повертає 200 і JSON-масив із чотирьох груп у сталому порядку Low, Medium, High,
Critical; для рівня Critical, якого немає в seed, повертається `count: 0`.
Браузерний клієнт викликає endpoint і показує результат через `textContent`.
Готові сценарії списку та деталей дали очікувані результати: порожній масив для
`Resolved` (200), Problem Details для відсутнього інциденту (404) і Validation
Problem Details для `status=Unknown` (400). Повний висновок дописати після
виконання автоматизованих тестів і перевірки reset (TODO).