# Security review SecureLab, ЛР 6

Рівень: добрий («Добре»); рівні накопичувальні. Commit початку огляду (review-start): `d27f11a6b133c6b690ed849dad86f700190da5bf` (HEAD із застосованою SDK-політикою). Хеші прийнятого merge commit і тегу v1.0.0 додає скрипт експорту в PDF. Дата: 2026-11-18. Версія SDK: 10.0.401 (політика global.json: 10.0.100, rollForward latestFeature). Профіль запуску: Development (локально, лише навчальні дані й акаунти). Клієнт: браузер (DevTools) та .http-файли.

## 1. Executive summary
Один абзац: що перевірено, скільки знахідок зафіксовано й виправлено, результат фактичного фінального прогону, головні залишкові ризики.

Перевірено застосунок SecureLab (ASP.NET Core minimal API, Identity з cookie-автентифікацією, PostgreSQL у Docker, браузерний клієнт) на рівні «Добре». Зафіксовано й виправлено дві знахідки: F-01 (заголовки безпеки зникали на відповідях 500) і F-02 (змінюючі маршрути з cookie-автентифікацією без antiforgery, CSRF). Додатково реалізовано ліміт запитів для публічного пошуку з незалежними партиціями (control, не знахідка). Фактичний прогін: 47 тестів, 47 пройдено, 0 провалів, серед них REG-CSRF-01, REG-CORS-01 та обидва ліміти. Вразливих NuGet-пакетів не виявлено. Головні залишкові ризики: пошук лишається публічним, перевірка CSRF залежить від відсутності XSS і вимагає HTTPS у Production, ліміти живуть у пам'яті одного процесу.

## 2. Scope та exclusions
Поточні відстежувані Git файли — обов’язково. Історія Git: лише фактично переглянуті файли, refs або проміжок дат; інакше напишіть «не перевірено».

- **У межах:** усі поточні відстежувані Git файли гілки `lab/6-hardening-review` (код API, клієнт, тести, інфраструктура, документація); локальний стенд (Development і production-like запуск із `--no-launch-profile`); навчальні дані й акаунти alice, bob, morgan, admin.
- **Поза межами:** історія Git (**не перевірялась**, лише поточні файли); зовнішнє розгортання, TLS-проксі, мережеві атаки; DoS поза рівнем застосунку; каталог `one-time-examples`.
- **Обмеження:** одноразові ручні перевірки Production виконано по HTTP на localhost; автоматичні тести працюють у Development.

## 3. System overview і trust boundaries
Для всіх: див. threat-model.md. Добрий/відмінний: кожна межа → конкретний test; відмінний: asset → threat → control → verification.

Опис компонентів, довірених меж і карти «межа → очікувана властивість → test» див. [threat-model.md](threat-model.md). Для кожної межі зазначено конкретний тест (доступ: `AccessControlTests`; cookie: `CookieFlagsTests`; ліміти: `LoginRateLimitTests`, `SearchRateLimitTests`; CSRF: `CsrfRegressionTests`; CORS: `CorsRegressionTests`).

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

Команда повного прогону (профіль Development, змінну `SeedUsers__Password` передано ззовні):

```
dotnet test SecureLab.sln -c Release --no-build --logger "console;verbosity=normal"
```

Фактичний результат (2026-11-21): `Test Run Successful. Total tests: 47, Passed: 47, Failed: 0`, час 37,7 с. Повний вивід: `docs/evidence/l6-tests-final.txt`. Перевірений commit: `196caf8fc52a5fd80a417ff10c519f872ad12f4e` (далі змінювалась лише документація).

| Клас перевірки | ID / тест | Передумова | Результат | Стан ресурсу |
|---|---|---|---|---|
| SQL injection | `Lab02RegressionTests.T04_Search_WithApostrophe_Returns200AndSeededIncident`, `S02_Search_*` | публічний пошук | Passed (апостроф і керівні символи не ламають запит, пошук параметризований) | дані не змінено |
| Authentication | `AuthScenarioTests` (в т.ч. A07: невідомий користувач і хибний пароль виглядають однаково), `CookieFlagsTests.Login_OverHttps_SetsHttpOnlySecureLaxCookie` | HTTPS | Passed | сеанс створюється лише з валідними обліковими даними |
| Authorization / IDOR | `AccessControlTests` (A01–A12, у т.ч. A09: відмова записується в аудит) | акаунти alice, bob, morgan | Passed | чужі інциденти недоступні, стан не змінюється |
| XSS | `IncidentEndpointTests.ClientScript_DoesNotUseDangerousInnerHtmlSink` | вихідний код клієнта | Passed | |
| CSRF | **REG-CSRF-01**: `CsrfRegressionTests.REG_CSRF_01_*` (3 тести) | вхід (cookie) → запит без / з хибним токеном → 400 → валідний токен → успіх | Passed | без токена інцидент не створено, з токеном створено; logout без токена не завершує сеанс |
| Validation | `Lab02RegressionTests.T05_Search_WithUnknownSortBy_Returns400WithSortByKey`, `RegisterContractTests` (T01/T02) | | Passed | |
| CORS | **REG-CORS-01**: `CorsRegressionTests.REG_CORS_01_*` (3 тести) | Development | Passed | чужий Origin без `Access-Control-Allow-Origin`; дозволений повертається точно |
| Rate limiting | `LoginRateLimitTests.Login_ExceedingLimit_Returns429_ThenRecoversAfterWindow`, `SearchRateLimitTests.*` (2 тести) | окремий host | Passed | 429 після квоти, відновлення після вікна, партиції незалежні |

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

### 9.2 Секрети (поточні відстежувані файли)

Межі: перевірено лише поточні файли під Git (`git ls-files`, `git grep`); історію Git **не перевіряв**. Команди:

```
git ls-files | findstr /i ".env secret appsettings .pfx .key"
git check-ignore -v .env infra/.env
git --no-pager grep -n -i -E "password|secret|apikey|api_key|connectionstring|bearer|private key|Pwd=" -- . ":!docs" ":!one-time-examples"
```

Результат: файлів `.env`, `.pfx`, `.key`, `secrets*` під Git немає; `.env` ігнорується (`.gitignore:15`). `infra/.env.example` містить лише `POSTGRES_PORT=54330`; `appsettings.json` не містить секретів (журнал, `AllowedHosts`, прапорці БД).

| Знахідка пошуку | Клас | Рішення |
|---|---|---|
| `infra/compose.yaml:9` `POSTGRES_PASSWORD: local-study-password` | відкрите локальне навчальне значення (не секрет); порт БД прив'язаний до 127.0.0.1 | прийняти, задокументовано в README |
| `appsettings.Development.json:3` рядок підключення з `local-study-password` | те саме навчальне значення, файл діє лише в Development | прийняти; у Production рядок передається змінною `ConnectionStrings__SecureLab` (перевірено) |
| `SecureLabDesignTimeDbContextFactory.cs:9-15` резервний рядок підключення | те саме значення; резерв лише для EF design-time, перекривається змінною середовища | прийняти |
| `README.md:63,66,135,138` | документація: описує відкрите навчальне значення й забороняє комітити реальні | не секрет |
| `DbSeeder.cs`, `SeedUsers:Password` | пароль seed передається ззовні (`SeedUsers__Password`), у коді лише ім'я ключа | ок |
| `tests/*` (`SeedUsers__Password`, значення `wrong`, `short1!`) | пароль із середовища; решта це негативні тестові значення | не секрет |
| `tests/http/lab-04-access.http` `{{$processEnv SeedUsers__Password}}` | посилання на змінну середовища | ок |
| `Client/auth-lab.html`, `auth-lab.js` (поля `password`) | елементи форми, не значення | хибне спрацювання |
| міграції, snapshot (`PasswordHash`), `packages.lock.json` (`UserSecrets`) | назви колонок і пакета | хибне спрацювання |
| `Lab03Identity.cs` (`input.Password`, `RequiredLength = 10`) | логіка автентифікації | не секрет |

Примітка: резервний рядок використовує порт 54329, а фактичний порт стенда 54330 (`infra/.env`); розбіжність не стосується безпеки, рядок перекривається змінною.

## 10. Residual risks
Неперевірені й тимчасово прийняті ризики, відповідальний і наступна дія.

| Ризик | Статус | Відповідальний | Наступна дія |
|---|---|---|---|
| Пошук `/api/incidents/search` публічний (без входу) | тимчасово прийнято (вимога варіанта, обмежено лімітом) | автор | розглянути обов'язкову автентифікацію пошуку |
| Antiforgery не діє при XSS; у Production потрібен HTTPS | прийнято | автор | тримати XSS закритим; при розгортанні TLS із коректними forwarded headers |
| Немає перевірки `Origin` на сервері як другого шару CSRF | прийнято | автор | можливе покращення |
| Ліміти (login, search) в пам'яті одного процесу | прийнято | автор | спільне сховище при кількох екземплярах |
| `AllowedHosts: "*"` у `appsettings.json` | прийнято (локальний навчальний стенд) | автор | задати перелік хостів при розгортанні |
| Журнал SQL (`EntityFrameworkCore.Database.Command: Information`), значення параметрів приховано | прийнято | автор | знизити рівень у Production |
| Історія Git не перевірялась на секрети | не перевірено | автор | за потреби перевірити історію |
| Залежності з новими мажорними версіями (11.x), `xunit` deprecated | прийнято | автор | оновити після переходу на .NET 11 / xunit.v3 |

## 11. Conclusion
Лише фактичні результати.

Виправлено дві знахідки (F-01, F-02) з повторною перевіркою; додано ліміт пошуку з незалежними партиціями; фактичний прогін: 47 тестів, 47 пройдено. Вразливих залежностей немає, секретів у відстежуваних файлах немає (є лише відкрите навчальне значення `local-study-password`). Залишкові ризики наведено в розділі 10. Перевірений фінальний commit: `196caf8fc52a5fd80a417ff10c519f872ad12f4e`; тег v1.0.0 ставиться на commit із цією документацією (хеші тегу й merge commit додає скрипт експорту).

## 12. Evidence appendix
Для відмінного: доказ відсутності чутливих даних у логах шести сценаріїв етапу 3, п. 4; обґрунтування пріоритетів findings; відтворювані передумови й retest для іншої людини.
Шляхи, точні команди, ID сценаріїв, безпечний зв’язок відповіді з записом журналу через traceId; без паролів, cookie, токенів і connection strings.

- Файли доказів: `docs/evidence/l6-tests-final.txt`, `l6-deps-vulnerable.txt`, `l6-deps-deprecated.txt`, `l6-deps-outdated.txt`; скріншоти етапу 6 (`l6-*.png`: Production, заголовки, 500 до/після, журнал, тести).
- Приклад зв'язку відповіді із записом журналу: невдалий вхід (`POST /api/auth/login`, 401) має в тілі `traceId` `00-3f950df48fb1cc78e4e5a7a70e9dff44-ef17cf8b674e9dc7-00`; у рядку журналу `Auth event login: result failure, trace ...` той самий ідентифікатор. Пароль, cookie і токени в журнал не потрапляють.
- Відтворюваність: передумови (Docker Compose з PostgreSQL, змінні `SeedUsers__Password`, `ConnectionStrings__SecureLab` ззовні), команди запуску й перевірок наведено в розділах 4, 7, 8, 9 і в кожному файлі `docs/findings/*.md`.

## Декларація й журнал ШІ
Використовував. Назва сервісу, для чого, що перевірено й змінено автором, власна інтерпретація.

| Дата | Сервіс | Завдання/фрагмент | Авторська адаптація | Перевірка й результат |
|---|---|---|---|---|
| 2026-11-18…21 | Claude (Anthropic) | План і пояснення етапів ЛР 6; чернетки `threat-model.md`, `security-review.md`, `F-01`, `F-02`; | Автор виконував усі команди, збирав проєкт і запускав тести, перевіряв виводи й заголовки в терміналі, робив скріншоти, обрав рівень і набір знахідок, ставив уточнювальні запитання, писав фінальний код, який потрапив у репозиторій | 47 тестів з 47 пройдено; залежності перевірено `dotnet list package`; відповіді API перевірено `curl` у Production |
