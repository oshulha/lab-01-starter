# Докази безпеки браузера (DEL-01)

Рівень: достатній («Задовільно») / добрий («Добре») / відмінний («Відмінно») — оберіть. Рівні накопичувальні; повні критерії й маршрут — на початку практичної частини методички. Заповнюйте лише розділи свого рівня й усі попередні. Не записуйте паролі, cookie/token values чи export browser storage.

## Стенд і Git-стан (Для всіх)

ОС, browser/version, API/probe ОС: Windows 10; Chrome 155.
API/client: http://localhost:5080; probes: 4173 (дозволений) і 4174 (контрольний).
release, base_commit, entry_parent: 
```json
{
  "release": "lab-05-start-v1",
  "inherits": "v0.4.0",
  "base_commit": "9e8db3a2006a0ca2d5b4fb23e75b92b10b397bf0",
  "entry_parent": "9e8db3a2006a0ca2d5b4fb23e75b92b10b397bf0"
}
```
Vulnerable commit: 0545af589fc9c90717aaee8b127cfd2b0097c81d.
Fixed hash, v0.5.0

Механізм сеансу (Application → Storage → Cookies → http://localhost:5080):
.AspNetCore.Identity.Application; Domain: localhost; Path: /; Expires: Session;
HttpOnly: так; Secure: ні; SameSite: Lax — без Value.

Значення з поля форми «Опис нового інциденту» (`<textarea name="description">` на browser-lab.html) зберігається в PostgreSQL (таблиця `incidents`, колонка `description`), повертається через JSON-поле `description` у відповіді `GET /api/incidents/{id}` і передається у sink `document.querySelector("#description").innerHTML` (browser-lab.js, рядок 10) як HTML-контекст; тому за умови, що користувач ввів у опис рядок із HTML-розміткою (наприклад, тег з обробником події), браузер може інтерпретувати дані не як звичайний текст, а як код.

## Гіпотеза й REG-XSS-01 (Для всіх)

Карта Description: docs/source-to-sink.md.
Гіпотеза: значення з поля форми «Опис нового інциденту» (`<textarea name="description">` на browser-lab.html) зберігається в PostgreSQL (таблиця `incidents`, колонка `description`), повертається через JSON-поле `description` у відповіді `GET /api/incidents/{id}` і передається у sink `document.querySelector("#description").innerHTML` (browser-lab.js, рядок 10) як HTML-контекст; тому за умови, що користувач ввів у опис HTML-розмітку, браузер може інтерпретувати дані не як звичайний текст, а як код.

Передумови: стенд на http://localhost:5080, БД у Docker, змінні SeedUsers__Password і ConnectionStrings__SecureLab задано в терміналі (значення не зберігаються в Git).
Користувачі: alice (Reporter, автор запису), morgan (Analyst, глядач), bob (Reporter, контроль).
Marker: з resources/lab05-marker.txt.
Guid запису з marker у Description: 4ccc0040-660f-4d25-ab2e-5e2ba305930b.
Seed-інцидент Alice із коментарем з marker (comments[].text, коментар …0005): 20000000-0000-0000-0000-000000000001.

Команди reset і start:
```
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
dotnet run --project src/SecureLab.Api -- --reset-database
dotnet run --project src/SecureLab.Api
```

Create/read: на /browser-lab.html під alice ввести назву й marker в «Опис нового інциденту», натиснути «Створити й прочитати» (POST /api/incidents → GET /api/incidents/{id}); для коментаря — вказати Guid seed-інциденту й натиснути «Прочитати». Перед кожною перевіркою оновлювати сторінку й переконатися, що команда перевірки повертає null.

| Спостереження | До fix | Після fix | Посилання на доказ |
|---|---|---|---|
| Значення в БД/API | `description` у JSON = marker | | docs/evidence/before-description.json |
| DOM основного Description | `<img src="x" onerror=…>` усередині `#description` | | скріншот Elements |
| Дія marker | команда `document.querySelector("#lab05-marker-target").getAttribute("data-lab05-marker")` → `'executed'` | | скріншот Console |
| Апостроф/лапки, 5 < 7, Unicode, HTML-like рядок | той самий текст; `<b>жирний</b>` стає елементом `<b>` | | скріншот Elements |
| Довжина за власними правилами ЛР 2 | 3900 символів відображено повністю | | |
| Вікно Morgan: GET і перевірка marker; контроль Bob | Morgan: GET 200, перевірка → `'executed'`; Bob: 403/404, `null` | | скріншот |

Finding: актив — сеанс браузера та дані/дії, доступні від імені користувача (Alice, Morgan); передумови — локальний акаунт, контроль над текстовим полем Description (або публічний коментар), інший користувач відкриває запис; першопричина — недовірений текст Incident.Description та IncidentComment.Text з БД через JSON потрапляє в `innerHTML` (browser-lab.js, рядки 10 і 14); вплив на стенді — marker виконав лише безпечну локальну дію (атрибут на `#lab05-marker-target`), у тому числі в сеансі Morgan; safe DOM diff, retest, залишковий ризик — заповнити після виправлення.
Повторювана REG-XSS-01, негативний контроль і очікування: заповнити після Етапу 4.

## CORS і REG-CORS-01 (Для всіх)

Прогноз (Етап 3, крок 1), заповніть до зміни policy:

| Сценарій | Чи доходить request до API? | Чи може JavaScript прочитати response? | Чому |
|---|---|---|---|
| same-origin 5080 → API | так | так | Той самий origin, SOP не втручається, CORS не потрібен |
| simple GET 4173 → API | так | ні (без CORS-дозволу) | Запит простий, браузер надсилає одразу, але сервер ще не дозволив цей origin, тож response не читається |
| simple GET 4174 → API | так | ні | Те саме, що 4173, origin не дозволено |
| curl/.http → API | так | не застосовується | Це не браузер, SOP/CORS немає, клітинка JS «не застосовується» |
| preflighted 4173 → API | спершу OPTIONS, actual GET залежить від нього | ні без успішного preflight | Власний заголовок X-Lab-Cors-Probe змушує браузер спершу надіслати OPTIONS, без дозволу GET не надсилається |

Команди запуску probes: … Policy — лише Development, один exact origin 4173; 4174 — контроль. Для «Задовільно» без AllowCredentials().

Колонки: **До policy / Fixed state / Status, ACAO, Console**. «До policy» це стан до кроку 4, «Fixed state» це кінцевий стан із `AllowCredentials()`.

| Клієнт | До policy | Fixed state | Status, ACAO, Console / доказ |
|---|---|---|---|
| Same-origin 5080 | 200, JS читає (browser-lab працює) | без змін | 200, ACAO немає і не потрібен; сторінка `browser-lab` рендерить дані |
| Allowed 4173 | request доходить, response заблоковано | 200, JS читає | до: Console «No Access-Control-Allow-Origin»; після: 200, ACAO `http://localhost:4173`, ACAC `true` |
| Disallowed 4174 | request доходить, response заблоковано | так само заблоковано | 200 у Network, ACAO немає, Console «No Access-Control-Allow-Origin» |
| Direct HTTP / curl | не знімалось | 200 завжди; ACAO лише для Origin 4173 | curl: без Origin і з Origin 4174 немає ACAO; з Origin 4173 ACAO `http://localhost:4173` + ACAC `true` |

REG-CORS-01:
- **Browser evidence і дві actual HTTP-відповіді:** з origin 4173 запит `/health` і `/api/incidents` повертають 200 з ACAO 4173, JS читає. З 4174 у Console блокування CORS. Дві відповіді curl: `Origin: 4173` дає ACAO 4173 (+ACAC true), `Origin: 4174` дає 200 і тіло, але ACAO немає.
- **Перевірка порядку (401 без cross-origin cookie):** запит із 4173 без cookie повернув читабельний `401`. Це означає, що `UseCors` стоїть перед `UseAuthentication`: навіть відмова 401 несе CORS-заголовки, і браузер дає JS її прочитати.
- **Різниця browser/direct HTTP:** сервер сам не відхиляє 4174, а лише не додає заголовки дозволу. Заборону застосовує тільки браузер. curl і інші клієнти бачать повну відповідь, тому CORS не є захистом API, авторизацією чи захистом від CSRF.

## Додатково для Добре та Відмінно

Другий flow IncidentComment.Text: … Public seed comment Guid/текст, похідні counts за потреби, DOM до/після: … Єдиний safe helper і виклики для обох полів: …

Preflight з 4173 і 4174, OPTIONS → GET / blocked GET, exact origin/header/method: … Cookie-дослід 4173/4174/без AllowCredentials()/401 без входу: … Кінцевий AllowCredentials() — лише Development policy для 4173. Review XSS/CORS/CSRF/authorization — посилання на DEL-03: …

| Директива CSP | Фактичні ресурси застосунку | Обране джерело / причина |
|---|---|---|
| default-src | | |
| script-src | | |
| style-src | | |
| connect-src | | |
| img-src | | |
| frame-ancestors | | |

Місце зміни middleware: … Enforcement: document 200 і фактичний CSP header; три сторінки flow; кероване порушення new Image() до 4173 — результат: … Для «Добре» докази report-only не обов’язкові.

| Header / control | Фактичне значення | Ризик і межа захисту / policy rationale |
|---|---|---|
| X-Content-Type-Options | | |
| Referrer-Policy | | |
| frame-ancestors | | |

## Додатково для Відмінно

DOM-тест: файл/команда, browser, vulnerable commit і FAIL, fixed commit і PASS: … Comments, повторний render без дублювання та позитивні рядки: … CSP report-only → enforcement: фактичні headers, flow і кероване порушення в обох режимах: … safe-url.js / атрибут: … Пояснення text/attribute/URL/JavaScript та ризик third-party scripts — посилання на DEL-03: …

Додаткових DEL, навчальних демо або нового журналу не подавайте. Перед поданням повторіть чекліст для свого накопичувального рівня.

safe-url.js виконано в Console на browser-lab: https і відносний шлях дозволено, javascript: (також у іншому регістрі та з пробілом) і зламаний URL відхилено (null); посилання отримало лише href, onclick немає. Поточний клієнт не створює посилань із даних, тому політика: посилання з користувацьких даних не створюються; якщо знадобляться, лише через new URL() і дозволені протоколи http/https (за потреби allowlist host).