let current;
const id = () => encodeURIComponent(document.querySelector("#incident-id").value.trim());
const status = document.querySelector("#status");

function renderText(container, text) {
  container.replaceChildren(document.createTextNode(text));
}

// F-02: токен antiforgery для змінюючих запитів (токен прив'язано до поточного сеансу).
async function csrfHeaders() {
  const response = await fetch("/api/auth/csrf", { credentials: "same-origin" });
  if (!response.ok) return {};
  const { headerName, token } = await response.json();
  return { [headerName]: token };
}

async function load() {
  const response = await fetch(`/api/incidents/${id()}`);
  status.textContent = `GET: ${response.status}`;
  if (!response.ok) return;
  current = await response.json();
  document.querySelector("#title").textContent = current.title;
  renderText(document.querySelector("#description"), current.description);
  document.querySelector("#comments").replaceChildren();
  for (const comment of current.comments ?? []) {
    const paragraph = document.createElement("p");
    renderText(paragraph, comment.text);
    document.querySelector("#comments").append(paragraph);
  }
  document.querySelector("#edit").elements.description.value = current.description;
}
document.querySelector("#load").addEventListener("click", load);
document.querySelector("#edit").addEventListener("submit", async event => {
  event.preventDefault();
  const response = await fetch("/api/incidents", { method: "POST", headers: { "Content-Type": "application/json", ...(await csrfHeaders()) },
    body: JSON.stringify({ title: event.currentTarget.elements.title.value, description: event.currentTarget.elements.description.value,
      severity: "Low", occurredAtUtc: new Date().toISOString() }) });
  status.textContent = `POST: ${response.status}`;
  if (response.ok) {
    const created = await response.json();
    document.querySelector("#incident-id").value = created.id;
    await load();
  }
});
