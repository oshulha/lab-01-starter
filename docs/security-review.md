# Security review SecureLab, ЛР 6

Рівень: добрий («Добре»); рівні накопичувальні. Commit початку огляду (review-start): `d27f11a6b133c6b690ed849dad86f700190da5bf` (HEAD із застосованою SDK-політикою). Хеші прийнятого merge commit і тегу v1.0.0 додає скрипт експорту в PDF. Дата: 2026-11-18. Версія SDK: 10.0.401 (політика global.json: 10.0.100, rollForward latestFeature). Профіль запуску: Development (локально, лише навчальні дані й акаунти). Клієнт: браузер (DevTools) та .http-файли.

## 1. Executive summary
Один абзац: що перевірено, скільки знахідок зафіксовано й виправлено, результат фактичного фінального прогону, головні залишкові ризики.
## 2. Scope та exclusions
Поточні відстежувані Git файли — обов’язково. Історія Git: лише фактично переглянуті файли, refs або проміжок дат; інакше напишіть «не перевірено».
## 3. System overview і trust boundaries
Для всіх: див. threat-model.md. Добрий/відмінний: кожна межа → конкретний test; відмінний: asset → threat → control → verification.
## 4. Methodology та test environment
Відомий початковий стан, яка саме локальна БД, хеші початку й кінця, профіль запуску, конкретний клієнт і сеанс, команди та їхні коди завершення.
- Review-start commit: `d27f11a6b133c6b690ed849dad86f700190da5bf` (коміт «Update SDK policy»)
- Базовий тег: v0.5.0 (`3b2c853`, не переміщується); гілка: lab/6-hardening-review
- Дата початку: 2026-11-18
- SDK: 10.0.401 (global.json: version 10.0.100, rollForward latestFeature, allowPrerelease false)
- Локальна БД: PostgreSQL у Docker Compose, 127.0.0.1:54330, база securelab (навчальні дані)
- Профіль запуску: Development (reset/seed); production-like — окремо, `--no-launch-profile`
- Клієнт і сеанс: браузер (DevTools) та .http-файли; акаунти alice, bob, morgan, admin

## 5. Findings summary
Таблиця: ID / актив / вплив / ймовірність / пріоритет / статус / ID повторної перевірки.

| ID | Актив | Вплив | Ймовірність | Пріоритет | Статус | ID повторної перевірки |
|---|---|---|---|---|---|---|
| F-01 | відповіді API (заголовки безпеки) | низький: без `nosniff`, CSP і `Referrer-Policy` на 500 | низька (потрібен збій залежності) | P3 | Fixed (`b055ff1`) | ручний retest `curl` (скрін «до/після»), docs/findings/F-01 |
| F-02 | цілісність даних інцидентів, сеанс | середній: CSRF на змінюючих маршрутах | середня (`SameSite=Lax` частково пом'якшує) | P2 | Fixed (`49f584b`) | REG-CSRF-01 (`CsrfRegressionTests`) |

Окремо від знахідок: контроль «ліміт пошуку» (розділ 8), реалізований у цій же роботі, не є знахідкою за методикою.

## 6. Detailed findings
Щонайменше одна власна знахідка для достатнього рівня, дві виправлені — для доброго; три виправлені — для відмінного, з них третя поза прямим checklist; кожна в окремому файлі docs/findings/*.md.

- [F-01. Заголовки безпеки відсутні у відповіді 500](findings/F-01-error-path-headers.md)
- [F-02. Відсутній antiforgery на змінюючих маршрутах (CSRF)](findings/F-02-missing-antiforgery.md)

## 7. Regression results
Для всіх: мінімальний pack SQLi/authentication/authorization-IDOR/XSS/CSRF. Добрий/відмінний: ще validation/CORS/rate limiting. Відмінний: автоматизований повний REG-CSRF-01 (login/cookie → відмова → незмінний стан → token → успіх → змінений стан), команда й фактичний результат. Початкові ID сценаріїв (T/S/A/E/R/REG), команда або тест, передумова, точний результат, перевірка стану ресурсу, перевірений фінальний commit.
## 8. Hardening controls
Для всіх: наявні controls; наявний login limiter — повторна перевірка (quota, 429, відновлення), відсутній — gap або виключення з причиною. Добрий/відмінний: наявні headers, CORS, search limiter (два partitions, quota, 429, відновлення). CSP, якої не було, — N/A на достатньому/доброму; для відмінного вона обов’язкова й не має ламати client. Добрий/відмінний: змістові відмінності Development/Production. Відмінний: погодження advanced variant до реалізації й фактична перевірка; Redis — shared state, TTL, partition isolation, failure mode.

Зв’язок «control → ризик → фактична перевірка → межа»; у рядках про headers зазначте профіль, клієнт і status.

| ID | Method + route | Джерело / розбіжність | Яка зміна стану | Cookie authentication | Browser-accessible | Antiforgery validation | Regression ID |
|---|---|---|---|---|---|---|---|
| SC-01 | GET /api/incidents | ЛР 3–4, клієнт (`app.js`) | немає (читання) | так | так | не застосовується (безпечний метод) | AccessControlTests |
| SC-02 | GET /api/incidents/{id} | ЛР 3–4, клієнт | немає | так | так | не застосовується | AccessControlTests |
| SC-03 | GET /api/incidents/severity-summary | ЛР 3–4, клієнт | немає | так | так | не застосовується | AccessControlTests |
| SC-04 | GET /api/incident-comments/{id} | ЛР 4 (код) | немає | так | так | не застосовується | AccessControlTests |
| SC-05 | PUT /api/incidents/{id} | ЛР 3–4 (код) | оновлює інцидент | так | так (клієнт не викликає) | explicit (`CsrfEndpointFilter`) | REG-CSRF-01 |
| SC-06 | DELETE /api/incidents/{id} | ЛР 3–4 (код) | видаляє інцидент | так | так (клієнт не викликає) | explicit | REG-CSRF-01 |
| SC-07 | POST /api/incidents/{id}/triage | ЛР 4 (код) | змінює статус | так | так (клієнт не викликає) | explicit | REG-CSRF-01, AccessControlTests |
| SC-08 | POST /api/incidents | ЛР 2–3, клієнт (`browser-lab.js`) | створює інцидент | так | так | explicit | REG-CSRF-01 |
| SC-09 | POST /api/auth/logout | ЛР 3, клієнт (`auth-lab.js`) | завершує сеанс | так | так | explicit | REG-CSRF-01 (logout без токена) |
| SC-10 | POST /api/auth/login | ЛР 3, клієнт | створює сеанс | ні | так | absent (рішення нижче) | LoginRateLimitTests |
| SC-11 | POST /api/auth/register | ЛР 3, клієнт (`register-check.js`) | створює користувача | ні | так | absent (рішення нижче) | RegisterContractTests |
| SC-12 | GET /api/auth/csrf | F-02 (нове) | видає токен | так | так | не застосовується | CsrfRegressionTests |
| SC-13 | GET /api/incidents/search | ЛР 2 (публічний) | немає | ні | так | не застосовується | SearchRateLimitTests, Lab02RegressionTests |
| SC-14 | GET /api/me, /api/auth/session-check, /health | ЛР 3 | немає | так / так / ні | так | не застосовується | AuthenticationMechanicsTests |

Login і register внесіть до таблиці з позначкою Cookie authentication = ні та окремим рішенням щодо ризику й меж огляду. Logout входить до cookie-периметра; перевірка токена — до SignOutAsync.

**Рішення щодо login і register (SC-10, SC-11).** Це анонімні маршрути без існуючого сеансу, який можна використати, тому класичний CSRF (запит від імені автентифікованого користувача) до них не застосовується. Antiforgery на них не вмикається. Ризик на login (підбір паролів) покрито rate limiter; register створює лише акаунт з роллю Reporter і не змінює чужих даних. Це рішення, а не пропуск: перевіряти його слід при появі будь-яких дій, що залежать від сеансу.

### 8.1 Наявні controls і їхня перевірка

| Control | Ризик | Фактична перевірка | Межа |
|---|---|---|---|
| Cookie входу: `HttpOnly`, `SameSite=Lax`, `Secure` (поза Development) | крадіжка cookie скриптом, частково CSRF | CookieFlagsTests; `Set-Cookie` у Production | `Lax` не покриває всі міжсайтові сценарії |
| Antiforgery (F-02) | CSRF | REG-CSRF-01 | лише за відсутності XSS; у Production потрібен HTTPS |
| Login limiter (10 за 10 с, партиція за IP) | підбір паролів | LoginRateLimitTests (429, відновлення) | пам'ять одного процесу |
| Search limiter (20 за 10 с, партиції: користувач / IP) | перевантаження, масове витягування даних | SearchRateLimitTests (429, незалежні партиції bob/alice/анонім, відновлення) | пам'ять одного процесу; пошук лишається публічним |
| Заголовки: `nosniff`, `Referrer-Policy: no-referrer`, CSP `default-src 'self'; frame-ancestors 'none'` | MIME sniffing, витік Referer, вбудовування у frame | спостережено на 200, 401, 500 (F-01 виправлено) | CSP мінімальна, лише `default-src 'self'` і `frame-ancestors 'none'` |
| CORS (`Lab05BrowserProbe`, лише Development) | читання відповідей чужим сайтом | REG-CORS-01 (`CorsRegressionTests`); Production: відповідь без `Access-Control-Allow-Origin` (curl) | CORS не захищає від CSRF, лише від читання |
| Безпечні помилки (ProblemDetails) | витік деталей | відповідь 500 без stack trace; стек лише в журналі | |
| Журнал: події `login`, `authorization.denied`, `csrf.rejected`, `search rate_limited` з єдиним trace id | розслідування інцидентів | рядок журналу `Auth event login ...` і `traceId` відповіді збігаються (curl + журнал) | паролі, cookie й токени не логуються |

### 8.2 Різниці Development і Production (змістові)

| Аспект | Development | Production |
|---|---|---|
| Cookie входу, `Secure` | `SameAsRequest` | `Always` |
| Cookie `sl-csrf` | `SameAsRequest` | `Always`: по HTTP перевірка CSRF відмовляє закрито (500) |
| CORS-політика `Lab05BrowserProbe` | увімкнена | вимкнена |
| `/scalar`, `/openapi` | доступні (у Scalar інший CSP) | недоступні; невідомий шлях віддає `index.html` |
| Рядок підключення до БД | `appsettings.Development.json` | лише `ConnectionStrings__SecureLab` ззовні |
| Міграції й seed при старті, reset БД | `ApplyMigrationsOnStartup` і `AllowReset` увімкнені | вимкнені; reset заборонено |

## 9. Dependency and secret audit
Для всіх рівнів: огляд поточної відстежуваної конфігурації й класифікація кожного збігу пошуку. Для доброго рівня додатково: таблиця рішень щодо залежностей, систематичний пошук у поточних відстежуваних файлах, повний огляд журналів і аудиту. Перевіряти всю історію не обов’язково — зазначте фактичні межі. Перевірки доброго рівня, які ви не виконували на достатньому, позначте «Не входить до обраного рівня», а не PASS.
### 9.1 Залежності (NuGet)

Дата перевірки: 2026-11-21. Джерело: https://api.nuget.org/v3/index.json. Команди (рядки `Out-File` зберігають вивід у `docs/evidence/`):

```
dotnet list SecureLab.sln package --vulnerable --include-transitive   # l6-deps-vulnerable.txt
dotnet list SecureLab.sln package --deprecated                        # l6-deps-deprecated.txt
dotnet list SecureLab.sln package --outdated                          # l6-deps-outdated.txt
```

Результат: вразливих пакетів немає в `SecureLab.Api` і `SecureLab.Api.Tests` (прямі й транзитивні). Застарілий пакет один: `xunit` (Legacy). Нові версії є, переважно нові мажорні.

| Пакет | Проєкт | Версія | Стан | Рішення |
|---|---|---|---|---|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.AspNetCore.OpenApi, Microsoft.EntityFrameworkCore (+ Relational, Design) | Api | 10.0.10 | не вразливі; є 11.0.0 (новий мажор) | Лишити 10.0.10: проєкт на net10.0 і SDK 10.0.x. Перехід на 11.x це зміна платформи, поза межами огляду |
| Npgsql.EntityFrameworkCore.PostgreSQL | Api | 10.0.3 | не вразливий; є 11.0.0 | Лишити: версія узгоджена з EF Core 10 |
| Microsoft.OpenApi | Api | 2.7.5 | не вразливий; є 3.11.1 | Лишити, мажорне оновлення може змінити генерацію OpenAPI |
| Scalar.AspNetCore | Api | 2.16.16 | не вразливий; є 3.1.1 | Лишити. Ендпоінт Scalar підключається лише в Development (`MapScalarApiReference`) |
| xunit | Tests | 2.9.3 | не вразливий; deprecated (Legacy), заміна xunit.v3 | Тимчасово прийняти: пакет тестовий, у застосунок не потрапляє; міграція на xunit.v3 змінює тестовий каркас |
| coverlet.collector, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, Microsoft.AspNetCore.Mvc.Testing | Tests | 6.0.4 / 17.14.1 / 3.1.4 / 10.0.10 | не вразливі; є новіші мажори | Лишити: лише тестові інструменти |

Умова перегляду: нова вразливість у `--vulnerable`, оновлення SDK, перехід на .NET 11. Відновлення відтворюване завдяки `packages.lock.json` (`dotnet restore --locked-mode`).

## 10. Residual risks
Неперевірені й тимчасово прийняті ризики, відповідальний і наступна дія.
## 11. Conclusion
Лише фактичні результати.
## 12. Evidence appendix
Для відмінного: доказ відсутності чутливих даних у логах шести сценаріїв етапу 3, п. 4; обґрунтування пріоритетів findings; відтворювані передумови й retest для іншої людини.
Шляхи, точні команди, ID сценаріїв, безпечний зв’язок відповіді з записом журналу через traceId; без паролів, cookie, токенів і connection strings.
## Декларація й журнал ШІ
Використовував/ла / не використовував/ла. Назва сервісу, для чого, що перевірено й змінено автором, власна інтерпретація.

| Дата | Сервіс | Завдання/фрагмент | Авторська адаптація | Перевірка й результат |
|---|---|---|---|---|
| заповнити або не використовував/ла | | | | |
