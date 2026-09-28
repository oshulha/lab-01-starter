const form = document.querySelector("#greeting-form");
const nameInput = document.querySelector("#name");
const result = document.querySelector("#result");
const historyList = document.querySelector("#history");

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  result.textContent = "Надсилаємо запит…";

  try {
    const response = await fetch("/api/greetings", {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify({ name: nameInput.value })
    });

    const data = await response.json();

    if (!response.ok) {
      result.textContent = `Помилка ${response.status}: ${data.error}`;
      return;
    }

    showGreeting(data.message);
    addHistoryItem(data.message);
  } catch {
    result.textContent = "Не вдалося завершити обмін із локальним API.";
  }
});

function showGreeting(message) {
  // Крок B4: змініть лише підпис перед значенням message.
  result.textContent = `Відповідь API: ${message}`;
}

function addHistoryItem(message) {
  const item = document.createElement("li");
  item.textContent = message;
  historyList.append(item);
}
