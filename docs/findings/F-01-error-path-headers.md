# F-01. Заголовки безпеки відсутні у відповіді 500 (error path)

**Статус:** Open (виправлення — етап 2; після fix змінити на Fixed)  
**Актив:** браузер користувача, що отримує відповіді API (захист від MIME sniffing, CSP, витоку Referer)  
**Компонент:** `src/SecureLab.Api/Program.cs`, порядок middleware (`UseExceptionHandler` та middleware заголовків)  
**Пріоритет:** P3

## Передумови і межі огляду

Гілка lab/6-hardening-review, review-start `d27f11a`. Локально: Production-профіль (`ASPNETCORE_ENVIRONMENT=Production`, `--no-launch-profile`), http://localhost:5080, PostgreSQL у Docker Compose (127.0.0.1:54330, навчальні дані). Помилку спричинено зупинкою контейнера БД; код не змінювався.

## Відтворення

1. Запустити API у Production із `ConnectionStrings__SecureLab` (порт 54330).
2. `docker compose -f infra/compose.yaml stop`.
3. `curl.exe -i "http://localhost:5080/api/incidents/search?q=test"`.
4. `docker compose -f infra/compose.yaml start`.

Expected: 500 у форматі ProblemDetails із заголовками `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Content-Security-Policy`.
Actual: 500 `application/problem+json`; заголовків безпеки немає, натомість `Cache-Control: no-cache,no-store`, `Expires: -1`, `Pragma: no-cache`. Для порівняння, відповіді 200 (`/health`) і 401 (`/api/auth/login`) ці заголовки мають.

## Вплив і ймовірність

**Статус перевірки:** спостережено локально.

Відповіді з помилкою сервера лишаються без захисних заголовків. Тіло відповіді безпечне: загальне повідомлення, `traceId`, без stack trace, SQL чи рядка підключення. Збій потребує несправності залежності (наприклад, БД), тому ймовірність низька, вплив обмежений.

## Першопричина

У `Program.cs` `app.UseExceptionHandler()` стоїть раніше за middleware, що ставить заголовки. Під час обробки винятку ASP.NET Core скидає заголовки відповіді й формує ProblemDetails заново, тож заголовки, виставлені до винятку, втрачаються (оцінено за кодом; підтверджено спостереженням).

## Виправлення

Заповнити на етапі 2.

## Retest

Заповнити на етапі 2: повторити кроки 1–4 і перевірити наявність трьох заголовків на 500.

## Positive regression

Заповнити на етапі 2: 200 і 401 лишаються із заголовками; тіло 500 не розкриває деталей.

## Залишковий ризик

Заповнити після виправлення.

## Доказ

Команда з кроку 3; статус 500, traceId відповіді, відсутність `X-Content-Type-Options`, `Content-Security-Policy`, `Referrer-Policy`. Скріншот: docs/evidence/ (додати).
