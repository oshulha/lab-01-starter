# Звіт WEB ЛР 2

## 1. Ідентифікація

- ПІБ і група: Шульга Олександр Сергійович, КБ-41.
- Спільний baseline 2-A «Трекер інцидентів»; індивідуального номера немає.
- Заявлений рівень: добрий («Добре»).
- Гілка: `lab/2-input-sqli` (основна: `main`).
- Release: `lab-02-start-v1`; `base_commit` = `entry_parent` =
  `e997e16410f88fe3da30c3744a57944944bb280b` (тег `v0.1.0` з ЛР 1),
  див. [evidence/f04-lab-02-json.png](evidence/f04-lab-02-json.png).
- Vulnerable commit: `c9f5affe5e9a19dd76e29667df4eab41c5be3bed`.
- Fixed commit коду: `TODO_FIXED_SHA` (вписати результат `git rev-parse HEAD`
  після коміту виправлення).
- Фінальний тег `v0.2.0` стоїть на commit звіту, створеному після fixed commit;
  hash цього commit у звіті не вказується.
- DEL-01: TODO (URL приватного remote з доступом викладача або ім'я bundle
  `securelab-lr02.bundle`).
- Середовище: Windows, .NET SDK `10.0.401` ([evidence/f02-global-json.png](evidence/f02-global-json.png)),
  local `Development`, PostgreSQL у Docker, API на `http://localhost:5080`.

## 2. Контракт і CP-01

Реалізовано `POST /api/incidents` (`Lab02Endpoints.cs`), код обробника:
[evidence/f10-post-handler.png](evidence/f10-post-handler.png).
Порядок перевірок: required і довжина (до `Trim()`) → enum (`Enum.TryParse` +
`Enum.IsDefined`) → дата → `Trim()` один раз → cross-field → відповідь 400 →
предметний конфлікт 409 → entity → response DTO.

| Поле | Правило | Помилка |
|---|---|---|
| `title` | не порожній після `Trim()`, вхідна довжина ≤ 160 | 400, ключ `title` |
| `description` | не порожній, ≤ 4000 | 400, ключ `description` |
| `severity` | `TryParse` без регістру і `IsDefined` | 400, ключ `severity` |
| `occurredAtUtc` | обов'язковий, не пізніше `now + 5 хв`, зберігається в UTC | 400, ключ `occurredAtUtc` |
| High/Critical | опис після `Trim()` ≥ 40 символів | 400, ключ `description` |
| дубль `title` | той самий `title` (з урахуванням регістру) у статусі, відмінному від `Closed` | 409 |

Серверні поля `id`, `ownerUserId` (Alice), `status = New`, `createdAtUtc`,
`updatedAtUtc` встановлює сервер. `CreateIncidentRequest` таких властивостей
не має, тому зайві поля JSON ігноруються. Відповідь — `CreatedIncidentResponse`
(`id`, `title`, `severity`, `status`, `occurredAtUtc`, `createdAtUtc`).

**CP-01.**
- `Enum.TryParse` перетворює рядок `"7"` на число 7, якого немає в enum, тому
  потрібна ще `Enum.IsDefined`.
- Маршрут: JSON → request DTO → server-side validation → 400 Problem Details.
  Перевірка на сервері обов'язкова: будь-який HTTP-клієнт може обійти браузер.
- Request DTO не містить `OwnerUserId`/`status`, тому клієнт не може їх передати
  (сценарій overposting у розділі 4).

## 3. Security-сценарій і CP-02/CP-03

**Контекст.** Локальний навчальний endpoint `GET /api/incidents/search`
(параметри `q`, `sortBy`) на штучних seed-даних. Ризик: недовірене значення `q`
потрапляє в SQL-структуру.

**Стан до.** Vulnerable commit `c9f5affe5e9a19dd76e29667df4eab41c5be3bed`,
файл `src/SecureLab.Api/Scaffolding/Lab02Endpoints.cs`, обробник пошуку:
SQL збирається конкатенацією в рядках 19–20 і виконується через
`FromSqlRaw(sql)` у рядку 21.

**Мінімальний PoC (лише читання, локально).** Сценарії A–D з методички:
A `q=USB`, B `q=zz-no-match`, C `q=zz-no-match' OR TRUE -- `
(URL-кодування `%27%20OR%20TRUE%20--%20`), D `q=O'Brien`. Інших payload не
застосовувалось.

**Спостереження.**
- A: 200, один запис «Перевірка USB навчальної мережі»; у журналі EF
  `Parameters=[]`, значення вбудоване в текст SQL
  ([evidence/s01-a-usb.png](evidence/s01-a-usb.png)).
- B: 200, `[]`
  ([evidence/s01-b-no-match.png](evidence/s01-b-no-match.png)).
- C: 200, повернуто записи, які не містять рядка пошуку (першим є
  «Підозрілий лист із вкладенням»); кількість записів: TODO (з відповіді C).
  У журналі видно `OR TRUE` і закоментований залишок запиту
  ([evidence/s01-c-control-input.png](evidence/s01-c-control-input.png)).
- D: 500 Problem Details («An error occurred while processing your request.»,
  без внутрішніх деталей у відповіді); у журналі API `SqlState: 42601`,
  `syntax error at or near "Brien"`
  ([evidence/s01-d-apostrophe-500.png](evidence/s01-d-apostrophe-500.png)).

**Першопричина.** Query string `q` → параметр обробника (рядок 12) →
конкатенація в `sql` (рядки 19–20) → `FromSqlRaw` (рядок 21) → PostgreSQL.
СУБД отримує один суцільний текст і не може відрізнити шаблон від даних.
- Звичайне значення (`USB`) лишається всередині `'...'` і є даними.
- Апостроф закриває рядковий літерал; решта тексту інтерпретується як SQL, що
  дає синтаксичну помилку і 500.
- У контрольному вводі `OR TRUE` розширює `WHERE` на всі записи, а `--`
  закоментовує решту рядка разом з `ORDER BY`.
- Додатково `sortBy` потрапляє в `ORDER BY` через гілку `_ => sortBy`
  (рядок 17); параметризація захищає значення, але не ідентифікатори.

**Чому фільтр апострофа або `OR ...` не є виправленням.** Це блокування
окремих символів і слів, а механізм (недовірений текст у структурі SQL)
лишається; крім того, апостроф є легітимним символом (`O'Brien`,
`комп'ютерного`).

**Виправлення (CP-03).** Замість `FromSqlRaw` із конкатенацією використано
LINQ і `EF.Functions.ILike` з параметризованим шаблоном: значення `q`
екранується (`\`, `%`, `_`, escape-символ `\`) і передається параметром.
`sortBy` обмежено allowlist `createdAtUtc`, `severity`, `status` через
`switch` (severity і status — явні ранги, додатково `ThenBy(Id)`, `Take(50)`);
невідоме значення дає 400 із ключем `sortBy`.
Різниця: раніше СУБД отримувала один текст після конкатенації
(`Parameters=[]`), тепер значення `pattern` передається окремо від тексту SQL
(`ILIKE @pattern ESCAPE '\'`), а структуру запиту вибирає серверний allowlist.
Diff: `git diff c9f5affe5e9a19dd76e29667df4eab41c5be3bed..TODO_FIXED_SHA`.

**Retest і позитивна регресія.** Після виправлення: A — той самий запис,
B і C — `[]`, D — 200 і запис «O'Brien classroom report», `комп'ютерного` —
200 і запис seed. Підтвердження: автоматичні тести
([evidence/tests-final-11.png](evidence/tests-final-11.png)); ручний retest
і журнал після виправлення: TODO (див. розділ 4, S-02).

**Залишковий ризик.** Виправлення не замінює authorization, обмеження ресурсу
чи пагінацію і не охоплює інші endpoint.

## 4. Фактичні перевірки DEL-02

| ID | Сценарій | Передумови | Дія | Очікувано | Фактично | Доказ |
|---|---|---|---|---|---|---|
| T-01 | Коректне створення | відновлений seed, valid DTO | `POST /api/incidents` (`severity: "medium"`, `title` з пробілами) | 201, response DTO без зайвих полів | 201 Created, `Location`, `status: New`, `severity: Medium`, `title` без пробілів, `occurredAtUtc: 2026-10-28T06:00:00+00:00`, 656 ms | [evidence/t01-created-201.png](evidence/t01-created-201.png) |
| T-02 | Некоректний DTO | — | `POST` із порожніми `title`/`description`, `severity: "7"`, без дати | 400 `application/problem+json` | 400, `errors`: `title`, `description`, `severity`, `occurredAtUtc` | [evidence/t02-multi-400.png](evidence/t02-multi-400.png) |
| T-02 | `severity: "7"` | — | `POST` з коректними іншими полями | 400, ключ `severity` | 400, `errors` лише `severity`, 5 ms | [evidence/t02-severity7-400.png](evidence/t02-severity7-400.png) |
| T-02 | Дата в майбутньому | — | `occurredAtUtc: 2099-01-01T00:00:00Z` | 400, ключ `occurredAtUtc` | 400, ключ `occurredAtUtc` | [evidence/t02-future-date-400.png](evidence/t02-future-date-400.png) |
| T-03 | Предметний конфлікт | після T-01 існує активний інцидент із таким `title` | повторний `POST` із тим самим `title` | 409 Problem Details | 409 Conflict, `application/problem+json`, title «Конфлікт даних», detail «Активний інцидент із таким заголовком уже існує.», 2172 ms | [evidence/t03-conflict-409.png](evidence/t03-conflict-409.png) |
| T-06 | Ресурс не знайдено | відсутній, але коректний id | `GET /api/incidents/99999999-9999-9999-9999-999999999999` | 404 Problem Details | 404 Not Found, `application/problem+json`, title «Інцидент не знайдено», 324 ms | [evidence/t06-not-found-404.png](evidence/t06-not-found-404.png) |
| T-09 | Cross-field, 39 символів | унікальний `title`, `severity: High` | `POST`, опис 39 символів | 400, ключ `description` | TODO (виконати й додати знімок) | TODO `evidence/t09-39-400.png` |
| T-09 | Cross-field, 40 символів | унікальний `title`, `severity: High` | `POST`, опис 40 символів | 201 | 201 Created, `status: New`, `severity: High`, 48 ms | [evidence/t09-40-created-201.png](evidence/t09-40-created-201.png) |
| — | Overposting | — | `POST` із зайвими `ownerUserId`, `status: "Closed"`, `id` | 201; `status: New`, новий `id` | 201 Created, `status: New`, новий `id` (не надісланий `1111…`) | [evidence/overposting-201.png](evidence/overposting-201.png) |
| S-01 | SQLi до виправлення | vulnerable commit, local seed | сценарії A–D | розширена вибірка для C; 500 для D | A: 1 запис; B: `[]`; C: записи без збігу, кількість TODO; D: 500 | розділ 3 і `evidence/s01-*.png` |
| S-02 | Retest SQLi | fixed commit, той самий seed | сценарій C | `[]` | автоматичний тест `S02_Search_ControlInput_ReturnsEmptyList` пройдено (11 з 11); ручний retest: TODO | [evidence/tests-final-11.png](evidence/tests-final-11.png), TODO `evidence/s02-c-after-fix.png` |
| T-04 | Позитивна регресія | fixed commit | `q=USB`, `q=O'Brien`, `q=комп'ютерного` | коректні записи, без 500 | автоматичні тести `S02_Search_Usb_...`, `T04_Search_WithApostrophe_...` пройдено; ручний запуск: TODO | [evidence/tests-final-11.png](evidence/tests-final-11.png), TODO `evidence/t04-obrien-200.png` |
| T-05 | Невідоме сортування | fixed commit | `GET /api/incidents/search?sortBy=price` | 400, ключ `sortBy` | автоматичний тест `T05_Search_WithUnknownSortBy_...` пройдено; ручний запуск: TODO | [evidence/tests-final-11.png](evidence/tests-final-11.png), TODO `evidence/t05-response.txt` |
| A-01 | Огляд data-access points | vulnerable і fixed commits | `git grep -n -e FromSqlRaw -e ExecuteSqlRaw -e "ORDER BY" -- src tests` | для кожного збігу файл/метод, категорія, висновок | таблиця нижче: TODO (вставити фактичний вивід) | таблиця нижче |
| — | Автотести T-02/T-03 | fixed commit, reset | `dotnet test tests/SecureLab.Api.Tests -c Release` | усі тести пройдено | `total: 11; failed: 0; succeeded: 11` (5 baseline + 6 нових: T02, T03, два S02, T04, T05) | [evidence/tests-final-11.png](evidence/tests-final-11.png) |

Baseline: до змін `total 5, failed 0`
([evidence/f07-baseline-tests-5.png](evidence/f07-baseline-tests-5.png)); після
етапу 2 — `total 5, failed 0`
([evidence/tests-after-stage2-5.png](evidence/tests-after-stage2-5.png)); після
виправлення — `total 11, failed 0`; збірка Release успішна
([evidence/fix-build-release.png](evidence/fix-build-release.png)).

**Огляд A-01 (заповнити фактичним виводом `git grep`).**

| Файл:рядок / метод | Категорія | Висновок |
|---|---|---|
| `DatabaseBootstrap.cs`, `ExecuteSqlRawAsync` | статичний службовий SQL без користувацького значення | TODO: підтвердити виводом `git grep` |
| `Lab02Endpoints.cs`, пошук (vulnerable commit) | конкатенація `q` і `sortBy` у SQL, `FromSqlRaw` | потребувало виправлення |
| `Lab02Endpoints.cs`, пошук (fixed commit) | LINQ + `ILike`, значення параметром, `sortBy` через allowlist | безпечно |

Сортування після виправлення (самоперевірка): TODO — `GET /api/incidents/search`
(очікується 0004, 0005, 0003, 0002, 0001), `?sortBy=severity`
(0002, 0001, 0003, 0004, 0005), `?sortBy=status` (0003, 0004, 0005, 0001, 0002).

## 5. Обмеження і висновок

- `AnyAsync` перед записом не гарантує унікальності `title` за паралельних
  створень; у реальному проєкті потрібне обмеження БД або транзакція
  (migration у ЛР 2 не додавались).
- Помилки читання JSON (зайва кома, числове `"severity": 7`) дають 500 ще до
  обробника — це окремий дефект конвеєра, а не результат валідації; у ЛР 2 його
  не виправляли.
- Для `sortBy` у відповіді після виправлення відсутні SQL, stack trace і
  connection string.

Реалізовано серверну валідацію `POST /api/incidents`: некоректні дані дають
400 Problem Details, активний дубль `title` — 409, коректний запит — 201 з
response DTO, серверні поля клієнт не задає. На локальному vulnerable commit
відтворено SQL injection: значення `q` конкатенувалося в SQL, ввід
`zz-no-match' OR TRUE -- ` повертав записи без збігу, а `O'Brien` давав 500.
Першопричину усунено: `q` передається параметром через LINQ і
`EF.Functions.ILike`, `sortBy` обмежено allowlist. Після виправлення збірка
Release пройшла, автоматичні тести виконано без помилок (`total 11, failed 0`).

## 6. Декларація використання ШІ

| Сервіс | Задачі | Авторська адаптація/інтерпретація | Фактична перевірка |
|---|---|---|---|
| Claude: підтвердити/виправити відповідно до того, що було | покрокові інструкції; пояснення понять (DTO, валідація, SQL injection, URL-кодування); допомога зі структурою звіту | код і формулювання адаптовано до власного репозиторію, прочитано методичку | усі сценарії виконано самостійно (`.http`, `dotnet test`), результати наведено у розділі 4 |

## 7. Передавання

- Remote доступний викладачеві або bundle перевірено через `verify`/`list-heads`: TODO.
- У Moodle файли/докази видимі й роботу надіслано: TODO.
- Git diff та історію перевірено на секрети (`.env`, паролі, connection string
  у скріншотах і журналах): TODO.
