"use strict";

// The order statuses can occur in, which is also the order they are rendered in.
// Each occurs at most once per order, so a status's position here orders it
// exactly - a catch-up can supply an early status after a later one has arrived.
const SEQUENCE = [
  "Submitted", "PaymentAuthorized", "StockReserved", "Packing",
  "Completed", "Rejected", "Cancelled",
];

// Shown greyed out before they arrive, so the page says what is still to come
// rather than growing a list from nothing.
const HAPPY_PATH = ["Submitted", "PaymentAuthorized", "StockReserved", "Packing", "Completed"];
const TERMINAL = ["Completed", "Rejected", "Cancelled"];
const FAILED = ["Rejected", "Cancelled"];

const LABELS = {
  Submitted: "Submitted",
  PaymentAuthorized: "Payment authorized",
  StockReserved: "Stock reserved",
  Packing: "Packing",
  Completed: "On its way",
  Rejected: "Rejected",
  Cancelled: "Cancelled",
};

const PRESETS = {
  happy: { sku: "MUG-1", name: "Mug", quantity: 2, unitPrice: "4.50" },
  declined: { sku: "CHAIR-1", name: "Chair", quantity: 2, unitPrice: "600.00" },
  soldout: { sku: "SOLD-OUT-1", name: "Rare Vinyl", quantity: 1, unitPrice: "42.00" },
};

const timeline = document.getElementById("timeline");
const meta = document.getElementById("meta");
const submit = document.getElementById("submit");
const field = (id) => document.getElementById(id);

let steps = [];
let connection = null;
let watching = null;

document.querySelectorAll("[data-preset]").forEach((button) =>
  button.addEventListener("click", () => {
    const preset = PRESETS[button.dataset.preset];
    Object.entries(preset).forEach(([key, value]) => (field(key).value = value));
  }));

submit.addEventListener("click", placeOrder);

connect();

// Opened on load, and the button stays disabled until it succeeds. Placing an
// order the page cannot then follow would record a real order while telling the
// customer it failed.
async function connect() {
  if (typeof signalR === "undefined") {
    stop("Live updates unavailable - the SignalR client did not load.");
    return;
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/orders")
    .withAutomaticReconnect()
    .build();

  connection.on("orderStatusChanged", (orderId, step) => {
    // The order is named on every push rather than assumed: a connection can
    // still be in a previous order's group for a moment after switching.
    if (orderId !== watching) {
      return;
    }

    record(step);
    render();
  });

  // A reconnect mints a new connection id and loses group membership, so rejoin
  // and read what arrived while the socket was down.
  connection.onreconnected(() => watching && rejoin(watching));
  connection.onclose(() => stop("Live updates stopped - reload to reconnect."));

  try {
    await connection.start();
    submit.disabled = false;
  } catch {
    stop("Live updates unavailable - could not reach the order hub.");
  }
}

async function placeOrder() {
  submit.disabled = true;
  steps = [];
  render();
  meta.textContent = "Placing order…";

  try {
    const response = await fetch("/api/orders", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        customerId: field("customerId").value,
        items: [{
          sku: field("sku").value,
          name: field("name").value,
          quantity: Number(field("quantity").value),
          unitPrice: Number(field("unitPrice").value),
        }],
      }),
    });

    const body = await response.json();

    if (!response.ok) {
      meta.textContent = describeProblem(body);
      submit.disabled = false;
      return;
    }

    meta.innerHTML =
      `Order <code>${escapeHtml(body.orderId)}</code> — total ${escapeHtml(String(body.total))}`;

    await follow(body.orderId);
  } catch (error) {
    meta.textContent = `Could not place the order: ${error.message}`;
    submit.disabled = false;
  }
}

// Join the order's group, then make sure nothing was missed getting there.
async function follow(orderId) {
  const previous = watching;
  watching = orderId;

  try {
    if (previous && previous !== orderId) {
      await connection.invoke("Unwatch", previous);
    }

    await connection.invoke("Watch", orderId);
  } catch {
    stop("Live updates unavailable - could not follow this order.");
    return;
  }

  // The order is accepted before the group can be joined, so the opening status
  // may be published to nobody. Checked for rather than fetched blindly: the
  // projection is slower than the live feed, so asking every time fetched a
  // document that did not exist yet and 404ed on every order.
  window.setTimeout(() => {
    if (watching === orderId && !steps.some((step) => step.status === SEQUENCE[0])) {
      catchUp(orderId);
    }
  }, 1500);
}

// After a reconnect the group is gone and arbitrary pushes were missed, so
// rejoin and read the projection outright rather than checking first.
async function rejoin(orderId) {
  try {
    await connection.invoke("Watch", orderId);
  } catch {
    stop("Live updates unavailable - could not follow this order.");
    return;
  }

  await catchUp(orderId);
}

async function catchUp(orderId) {
  try {
    const response = await fetch(`/api/orders/${orderId}`);

    // 404 until the projection catches up, which is the eventual consistency the
    // read model is built on. Anything missed still arrives on the live feed.
    if (!response.ok) {
      return;
    }

    const order = await response.json();

    if (order.orderId === watching) {
      order.history.forEach(record);
      render();
    }
  } catch {
    // Nothing to do: the next live push still arrives.
  }
}

function record(step) {
  if (steps.some((existing) => existing.status === step.status)) {
    return;
  }

  steps.push(step);
  steps.sort((left, right) => SEQUENCE.indexOf(left.status) - SEQUENCE.indexOf(right.status));
}

function render() {
  const reached = new Set(steps.map((step) => step.status));
  const finished = steps.some((step) => TERMINAL.includes(step.status));
  const failed = steps.some((step) => FAILED.includes(step.status));

  // Everything still expected, unless the order has already ended: a rejected
  // order is never going to be packed, so showing packing as pending would be a
  // promise the workflow has already broken.
  const upcoming = finished ? [] : HAPPY_PATH.filter((status) => !reached.has(status));

  const rows = [
    ...steps.map((step) => `
      <li class="${FAILED.includes(step.status) ? "failed" : "done"}">
        <span class="status">${escapeHtml(LABELS[step.status] ?? step.status)}</span>
        <span class="when">${escapeHtml(time(step.occurredAt))}</span>
        <div class="desc">${escapeHtml(step.description)}</div>
      </li>`),
    ...upcoming.map((status) => `
      <li class="pending"><span class="status">${escapeHtml(LABELS[status] ?? status)}</span></li>`),
  ];

  timeline.innerHTML = rows.join("") ||
    `<li class="pending"><span class="empty">No order yet.</span></li>`;

  // submit stays disabled for the length of one order, so this fires once, on
  // the transition into a terminal status rather than on every render.
  if (finished && submit.disabled) {
    submit.disabled = false;
    meta.innerHTML += failed ? " — ended early" : " — delivered";
  }
}

function stop(reason) {
  meta.textContent = reason;
  submit.disabled = true;
}

function time(value) {
  return value ? new Date(value).toLocaleTimeString() : "";
}

function escapeHtml(value) {
  const node = document.createElement("div");
  node.textContent = value ?? "";
  return node.innerHTML;
}

function describeProblem(problem) {
  if (!problem?.errors) {
    return problem?.title ?? "The order was rejected.";
  }

  return Object.entries(problem.errors)
    .map(([fieldName, messages]) => `${fieldName}: ${messages.join(" ")}`)
    .join("  ");
}
