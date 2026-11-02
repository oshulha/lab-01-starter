const result = document.querySelector("#result");
const stateText = document.querySelector("#state");
const profile = document.querySelector("#profile");

// Лише власний origin; cookie надсилається явно, нічого не зберігаємо в storage.
async function apiFetch(path, options = {}) {
  return fetch(path, { ...options, credentials: "same-origin" });
}

function showState(name) {
  stateText.textContent = { "signed-in": "Сеанс активний", anonymous: "Не ввійшли",
    unknown: "Стан невідомий" }[name];
}

function clearProfile() { profile.replaceChildren(); }

function showProfile(data) {
  clearProfile();
  for (const key of ["id", "userName", "role"]) {   // лише дозволені поля DTO
    const term = document.createElement("dt");
    term.textContent = key;
    const value = document.createElement("dd");
    value.textContent = String(data[key] ?? "");
    profile.append(term, value);
  }
}

async function refreshSessionState() {
  try {
    const response = await apiFetch("/api/auth/session-check");
    if (response.status === 204) showState("signed-in");
    else if (response.status === 401) { showState("anonymous"); clearProfile(); }
    else showState("unknown");
  } catch { showState("unknown"); }
}

async function loadProfile() {
  const response = await apiFetch("/api/me");
  if (response.status === 401) { clearProfile(); showState("anonymous"); result.textContent = "Профіль: 401"; return; }
  if (!response.ok || !response.headers.get("Content-Type")?.includes("json")) {
    clearProfile();
    result.textContent = `Профіль: помилка ${response.status}`;
    return;
  }
  showProfile(await response.json());
  result.textContent = `Профіль: ${response.status}`;
}

document.querySelector("#register").addEventListener("submit", async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const response = await apiFetch("/api/auth/register", {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify(Object.fromEntries(new FormData(form))) });
  form.elements.password.value = "";
  result.textContent = `Реєстрація: ${response.status}` +
    (response.status === 201 ? " (тепер увійдіть)" : "");   // автоматичного login немає
});

document.querySelector("#login").addEventListener("submit", async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const response = await apiFetch("/api/auth/login", {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify(Object.fromEntries(new FormData(form))) });
  form.elements.password.value = "";
  result.textContent = `Вхід: ${response.status}`;
  await refreshSessionState();
  if (response.status === 204) await loadProfile();
});

document.querySelector("#me").addEventListener("click", loadProfile);

document.querySelector("#incident").addEventListener("submit", async event => {
  event.preventDefault();
  const data = Object.fromEntries(new FormData(event.currentTarget));
  const dto = {                                   // без ownerUserId, role, status, id
    title: data.title,
    description: data.description,
    severity: data.severity,
    occurredAtUtc: new Date(data.occurredAt).toISOString()
  };
  const response = await apiFetch("/api/incidents", {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify(dto) });
  result.textContent = `Створення інциденту: ${response.status}`;
  if (response.status === 400) {
    const problem = await response.json();          // Problem Details, без секретів
    for (const [field, messages] of Object.entries(problem.errors ?? {})) {
      result.textContent += `\n${field}: ${messages.join(" ")}`;
    }
  }
});

document.querySelector("#logout").addEventListener("click", async () => {
  const response = await apiFetch("/api/auth/logout", { method: "POST" });
  clearProfile();
  document.querySelector("#incident").reset();
  result.textContent = `Вихід: ${response.status}`;
  await refreshSessionState();
  const check = await apiFetch("/api/me");          // повторний запит тим самим браузером
  result.textContent += `; профіль після виходу: ${check.status}`;
});

refreshSessionState();