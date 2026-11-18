# Докази безпеки браузера (DEL-01)

Рівень: відмінний («Відмінно»). Рівні накопичувальні; повні критерії й маршрут — на початку практичної частини методички. Заповнюйте лише розділи свого рівня й усі попередні. Не записуйте паролі, cookie/token values чи export browser storage.

## Стенд і Git-стан (Для всіх)

ОС, browser/version, API/probe origins: Windows 10; Chrome 155.
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
XSS-fix commit: 4f643c972dace1cb13e6319f069dea25a4ec242e.
Fixed hash (кінцевий, на нього вказує v0.5.0): 6c5d08827c0da22cfd97b915e2a02a39f656aa9f. Vulnerable: 0545af589fc9c90717aaee8b127cfd2b0097c81d. Передавання в main: Pull request #6, origin/main = 77ccb3f553510447772d4f4434402332dbb3b774 (містить 6c5d088); гілка і тег опубліковані в origin (github.com/oshulha/lab-01-starter).

Механізм сеансу (Application → Storage → Cookies → http://localhost:5080):
.AspNetCore.Identity.Application; Domain: localhost; Path: /; Expires: Session;
HttpOnly: так; Secure: ні; SameSite: Lax — без Value.

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
| Значення в БД/API | `description` у JSON = marker | те саме значення, що до fix (змінився лише спосіб відображення) | docs/evidence/before-description.json, docs/evidence/after-description.json |
| DOM основного Description | `<img src="x" onerror=…>` усередині `#description` | текстовий вузол із рядком `<img src=x onerror=…>`; елемента `<img>` немає (`childElementCount` = 0) | скріншот Elements, DOM-тест PASS |
| Дія marker | команда `document.querySelector("#lab05-marker-target").getAttribute("data-lab05-marker")` → `'executed'` | `null` (DOM-тест PASS: атрибут `data-lab05-marker` не змінився) | скріншот Console |
| Апостроф/лапки, 5 < 7, Unicode, HTML-like рядок | той самий текст; `<b>жирний</b>` стає елементом `<b>` | `<b>` показано як текст; DOM-тест нормального тексту: PASS (`PASS: normal text rendered as text`) | скріншот Console |
| Довжина за власними правилами ЛР 2 | 3900 символів відображено повністю | довгий рядок із символів A (Test4) відображено повністю, без HTML; точну довжину не фіксовано | docs/evidence/l5-042-fix-positive-test4-long.png |
| Вікно Morgan: GET і перевірка marker; контроль Bob | Morgan: GET 200, перевірка → `'executed'`; Bob: 403/404, `null` | Morgan: GET 200, перевірка → `null`; Bob: GET 404, `null` | скріншот |

Finding: актив — сеанс браузера та дані/дії, доступні від імені користувача (Alice, Morgan); передумови — локальний акаунт, контроль над текстовим полем Description (або публічний коментар), інший користувач відкриває запис; першопричина — недовірений текст Incident.Description та IncidentComment.Text з БД через JSON потрапляє в `innerHTML` (browser-lab.js, рядки 10 і 14); вплив на стенді — marker виконав лише безпечну локальну дію (атрибут на `#lab05-marker-target`), у тому числі в сеансі Morgan; safe DOM diff — у `browser-lab.js` додано helper `renderText(container, text)` (`container.replaceChildren(document.createTextNode(text))`) і замінено `innerHTML` на його виклики для `#description` (рядок 15) та `comments[].text` (рядок 19); retest — DOM-тест (`description-dom.js`) на fixed: PASS, значення в БД/API не змінилося; залишковий ризик — CSP є лише додатковим шаром, а не заміною виправлення; сторінка `/scalar` (сторонній UI, лише Development) винесена з-під CSP як задокументований виняток.
Повторювана REG-XSS-01, негативний контроль і очікування:

| Поле | Вміст |
|---|---|
| Передумови | fixed commit (той, на який вказує тег `v0.5.0`; виправлення XSS — `4f643c9`), API запущено, вхід як Alice, Guid запису `4ccc0040-660f-4d25-ab2e-5e2ba305930b` (або ваш), reset: `--reset-database` |
| Вхід | marker із `resources/lab05-marker.txt` |
| Кроки | `/browser-lab.html` → ввести Guid → «Прочитати» → в Elements подивитися `#description` і `#comments` |
| Очікуваний результат | marker не виконується, в DOM текстовий вузол, звичайний текст відображається |
| Негативний контроль | на vulnerable `0545af5` marker виконував дію (Етап 1, до CSP) |
| Доказ | Network/Elements, файли `browser-lab.js`, `DbSeeder.cs` |

## CORS і REG-CORS-01 (Для всіх)

Прогноз (Етап 3, крок 1), заповніть до зміни policy:

| Сценарій | Чи доходить request до API? | Чи може JavaScript прочитати response? | Чому |
|---|---|---|---|
| same-origin 5080 → API | так | так | Той самий origin, SOP не втручається, CORS не потрібен |
| simple GET 4173 → API | так | ні (без CORS-дозволу) | Запит простий, браузер надсилає одразу, але сервер ще не дозволив цей origin, тож response не читається |
| simple GET 4174 → API | так | ні | Те саме, що 4173, origin не дозволено |
| curl/.http → API | так | не застосовується | Це не браузер, SOP/CORS немає, клітинка JS «не застосовується» |
| preflighted 4173 → API | спершу OPTIONS, actual GET залежить від нього | ні без успішного preflight | Власний заголовок X-Lab-Cors-Probe змушує браузер спершу надіслати OPTIONS, без дозволу GET не надсилається |

Команди запуску probes:
py -3 -m http.server 4173 --bind localhost --directory demonstrations/cors-probe
py -3 -m http.server 4174 --bind localhost --directory demonstrations/cors-probe
Policy — лише Development, один exact origin 4173, з AllowCredentials() (рівень «Відмінно»); 4174 — контроль.

| Клієнт | До policy | Fixed state | Status, ACAO, Console / доказ |
|---|---|---|---|
| Same-origin 5080 | 200, JS читає (browser-lab працює) | без змін | 200, ACAO немає і не потрібен; сторінка `browser-lab` рендерить дані |
| Allowed 4173 | request доходить, response заблоковано | 200, JS читає | до: Console «No Access-Control-Allow-Origin»; після: 200, ACAO `http://localhost:4173`, ACAC `true` |
| Disallowed 4174 | request доходить, response заблоковано | так само заблоковано | 200 у Network, ACAO немає, Console «No Access-Control-Allow-Origin» |
| Direct HTTP / curl | не знімалось | 200 завжди; ACAO лише для Origin 4173 | curl: без Origin і з Origin 4174 немає ACAO; з Origin 4173 ACAO `http://localhost:4173` + ACAC `true` |

REG-CORS-01:
- **Browser evidence і дві actual HTTP-відповіді:** з origin 4173 запит `/health` і `/api/incidents` повертають 200 з ACAO 4173, JS читає. З 4174 у Console блокування CORS. Дві відповіді curl (повні заголовки збережено у `docs/evidence/cors-allowed-4173.txt` і `docs/evidence/cors-disallowed-4174.txt`): `Origin: 4173` дає ACAO 4173 (+ACAC true), `Origin: 4174` дає 200 і тіло, але ACAO немає.
- **Перевірка порядку (401 без cross-origin cookie):** запит із 4173 без cookie повернув читабельний `401`. Це означає, що `UseCors` стоїть перед `UseAuthentication`: навіть відмова 401 несе CORS-заголовки, і браузер дає JS її прочитати.
- **Різниця browser/direct HTTP:** сервер сам не відхиляє 4174, а лише не додає заголовки дозволу. Заборону застосовує тільки браузер. curl і інші клієнти бачать повну відповідь, тому CORS не є захистом API, авторизацією чи захистом від CSRF.

## Додатково для Добре та Відмінно

Другий flow IncidentComment.Text: публічний seed-коментар `30000000-0000-0000-0000-000000000005` із текстом marker (`Lab05Marker`) у seed-інциденті Alice `20000000-0000-0000-0000-000000000001`; Alice бачить 2 коментарі (внутрішній `…0003` приховано від неї). DOM до: `<img>` усередині `<p>` у `#comments`; після: текстовий вузол, `childElementCount` = 0 Comments-тест: PASS (`PASS: comments rendered as text, no duplication`; docs/evidence/l5-110-fix-comments-test-pass.png). Єдиний safe helper `renderText` (browser-lab.js, рядки 5–7) використано для обох полів: рядок 15 (`#description`) і рядок 19 (`comments[].text`).

Preflight з 4173 і 4174, OPTIONS → GET / blocked GET, exact origin/header/method: з 4173 — `OPTIONS` 204 No Content, далі `GET 200`; відповідь на `OPTIONS` містить `Access-Control-Allow-Origin: http://localhost:4173`, `Access-Control-Allow-Methods: GET`, `Access-Control-Allow-Headers: X-Lab-Cors-Probe`; з 4174 — preflight без `Access-Control-Allow-Origin`, GET не надсилається (docs/evidence/l5-068…l5-071). Cookie-дослід: без `AllowCredentials()` запит `/api/incidents` з 4173 (`credentials: "include"`) заблоковано помилкою про `Access-Control-Allow-Credentials`; з `AllowCredentials()` відповідь містить ACAC `true` і ACAO `http://localhost:4173`; з 4174 запит доходить до API із cookie (200 у Network), але ACAO немає — відповідь заблоковано; без cookie запит `/api/incidents` з 4173 повертає читабельний `401` (401 problem+json, ACAO 4173, ACAC true: docs/evidence/l5-079-cors-401-readable-incognito.png). Кінцевий `AllowCredentials()` — лише Development policy для 4173. Review XSS/CORS/CSRF/authorization — у звіті DEL-03.

| Директива | Фактичні ресурси | Обране джерело / причина |
|---|---|---|
| default-src | усе з 5080 | `'self'` |
| script-src | власні JS-файли | `'self'` (успадковується від default-src, окремо можна не писати) |
| style-src | власний CSS | `'self'` |
| connect-src | запити до API на 5080 | `'self'` (4173 не додаємо, це лише експериментальний стенд) |
| img-src | власні зображення | `'self'` |
| frame-ancestors | вбудовування не потрібне | `'none'` |

Місце зміни middleware: `Program.cs`, наявний блок `app.Use` після `UseStatusCodePages` і до `UseDefaultFiles`/`UseStaticFiles` (виняток `/scalar` лише у Development). Enforcement: document `/browser-lab.html` — 200, фактичний заголовок `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'` (один заголовок); три сторінки flow (`/`, `/browser-lab.html`, `/auth-lab.html`) Console без порушень CSP (l5-092, l5-094; в auth-lab лише штатний 401 session-check, l5-093); кероване порушення `new Image().src = "http://localhost:4173/x.png"` — Console: «violates … default-src 'self' … The action has been blocked» (l5-097), запит x.png заблоковано в Network (l5-098). Для «Добре» докази report-only не обов’язкові.

| Header / control | Фактичне значення | Ризик і межа захисту / policy rationale |
|---|---|---|
| X-Content-Type-Options | `nosniff` | Зменшує ризик, що браузер вгадає тип файлу за вмістом і виконає, наприклад, текст як скрипт. Не замінює правильний `Content-Type` на сервері та валідацію даних |
| Referrer-Policy | `no-referrer` | Не передає адресу сторінки в заголовку `Referer` на інші сайти, тобто зменшує витік URL. Не замінює заборону класти секрети в URL |
| frame-ancestors (у CSP) | `'none'` | Забороняє вбудовувати застосунок в `<iframe>` чужих сайтів, тобто захищає від clickjacking. Не замінює авторизацію й не виправляє XSS |

## Додатково для Відмінно

DOM-тест: Console на `/browser-lab.html`, вміст `resources/copyable/description-dom.js`, Chrome 155; fixed commit — PASS (`PASS: persisted description rendered as plain text`); vulnerable commit `0545af5` — FAIL: `Uncaught Error: Text changed` (l5-108-neg-dom-test-fail.png; додатково CSP блокує inline event handler). Comments, повторний render без дублювання та позитивні рядки: comments PASS без дублювання (l5-110); позитивні рядки Test1–Test5, seed-запис із `<script>`, normal-text тест PASS (l5-039…l5-044, l5-112). CSP report-only → enforcement: у режимі report-only заголовок `Content-Security-Policy-Report-Only` присутній у відповідях document і `app.js` (200), далі ім'я змінено на `Content-Security-Policy`; report-only: порушення лише логується (l5-095); enforcement: «The action has been blocked» (l5-097). safe-url.js виконано в Console на browser-lab: https і відносний шлях дозволено, javascript: (також у іншому регістрі та з пробілом) і зламаний URL відхилено (null); посилання отримало лише href, onclick немає. Поточний клієнт не створює посилань із даних, тому політика: посилання з користувацьких даних не створюються; якщо знадобляться, лише через new URL() і дозволені протоколи http/https (за потреби allowlist host). Пояснення text/attribute/URL/JavaScript та ризик third-party scripts — у звіті DEL-03 (у дослідженому flow сторонніх скриптів немає, усе завантажується з `localhost:5080`; скрипти, які показував Network, належали розширенню браузера).

Додаткових DEL, навчальних демо або нового журналу не подавайте. Перед поданням повторіть чекліст для свого накопичувального рівня.
