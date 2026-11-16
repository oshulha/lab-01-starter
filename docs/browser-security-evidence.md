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
| same-origin 5080 → API | | | |
| simple GET 4173 → API | | | |
| simple GET 4174 → API | | | |
| curl/.http → API | | не застосовується | |
| preflighted 4173 → API («Добре / Відмінно») | | | |

Команди запуску probes: … Policy — лише Development, один exact origin 4173; 4174 — контроль. Для «Задовільно» без AllowCredentials().

| Клієнт / простий запит | До policy | Fixed state | Status, ACAO, Console / доказ |
|---|---|---|---|
| Same-origin 5080 | | | |
| Allowed 4173 | | | |
| Disallowed 4174 | | | |
| Direct HTTP / curl | | | |

REG-CORS-01: browser evidence і дві actual HTTP-відповіді для Origin 4173/4174: … Перевірка порядку через читання 401 без cross-origin cookie: … Пояснення різниці browser/direct HTTP: …

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