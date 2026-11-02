import { apiFetch, ensureDesignSession } from "./api";
import "./visual-proposals";

async function startDesign() {
const session = await ensureDesignSession();
type Assessment = { status: string; question: string; suggestions: string[]; issues: string[]; missing: string[] };
type Turn = { id: string; userText: string; assistantText: string; status: string };
type Conversation = { id: string; title: string; brief: Record<string, unknown>; assessment: Assessment; messages: Turn[]; busy: boolean; remainingTurns: number; budgetAvailable: boolean };
type Summary = { id: string; title: string };
const select = document.querySelector<HTMLSelectElement>("#conversation-select")!;
const input = document.querySelector<HTMLTextAreaElement>("#chat-input")!;
const send = document.querySelector<HTMLButtonElement>("#chat-send")!;
const status = document.querySelector<HTMLElement>("#chat-status")!;
const messages = document.querySelector<HTMLElement>("#chat-messages")!;
const choices = document.querySelector<HTMLElement>("#chat-choices")!;
const more = document.querySelector<HTMLButtonElement>("#more-chats")!;
const create = document.querySelector<HTMLButtonElement>("#new-chat")!;
let active = "";
let enabled = false;
let sending = false;
let loading = false;
let current: Conversation | null = null;
let skip = 0;
let timer: ReturnType<typeof setTimeout> | undefined;
let catalog: {materials: {id: string; name: string}[]; categories: {id: string; name: string}[]} = {materials: [], categories: []};
const key = () => `andeth-chat-pending-${session.user.id}-${active}`;
let visualContext = "";
function lock() {
  const blocked = !enabled || sending || loading || !!current?.busy || (current !== null && (!current.budgetAvailable || current.remainingTurns === 0));
  input.disabled = blocked; send.disabled = blocked;
  create.disabled = sending || loading; select.disabled = sending || loading;
  choices.querySelectorAll("button").forEach(button => button.disabled = blocked);
  send.textContent = sending || current?.busy ? "Preparando respuesta…" : "Enviar";
  const context = `${active}:${sending || loading || !!current?.busy}`;
  if (context !== visualContext) {
    visualContext = context;
    document.dispatchEvent(new CustomEvent("design:conversation", {detail: {id: active, busy: sending || loading || !!current?.busy}}));
  }
}
function showMessage(text: string, user: boolean) {
  const box = document.createElement("div");
  box.className = `rounded-xl p-4 whitespace-pre-wrap break-words ${user ? "bg-brand-600 text-white ml-6" : "bg-brand-50 mr-6"}`;
  const label = document.createElement("strong"); label.textContent = user ? "Tú\n" : "AndEth3D\n";
  box.append(label, document.createTextNode(text)); messages.append(box);
}
function render(data: Conversation) {
  current = data;
  messages.replaceChildren();
  if (!data.messages.length) showMessage(data.assessment.question, false);
  for (const turn of data.messages) {
    showMessage(turn.userText, true);
    showMessage(turn.status === "Processing" ? "Estoy organizando los detalles de tu idea…" : turn.assistantText, false);
  }
  choices.replaceChildren();
  for (const text of data.assessment.suggestions) {
    const button = document.createElement("button"); button.type = "button"; button.textContent = text;
    button.className = "rounded-full border border-brand-200 text-sm px-3 py-2 hover:bg-brand-50 disabled:opacity-50";
    button.addEventListener("click", () => { input.value = text; input.focus(); }); choices.append(button);
  }
  const labels: Record<string, string> = {object: "Objeto", use: "Uso", categoryId: "Categoría", widthMm: "Ancho (mm)", depthMm: "Fondo (mm)", heightMm: "Alto (mm)", quantity: "Cantidad", materialId: "Material", colors: "Colores", personalization: "Personalización", reference: "Referencia descrita"};
  const briefLines: string[] = [];
  const sheet = document.querySelector("#design-brief")!; sheet.replaceChildren();
  for (const [field, label] of Object.entries(labels)) {
    let value = data.brief[field];
    if (field === "categoryId") value = catalog.categories.find(c => c.id === value)?.name ?? value;
    if (field === "materialId") value = catalog.materials.find(m => m.id === value)?.name ?? value;
    const row = document.createElement("div"); row.className = "border-b border-brand-50 pb-2";
    const dt = document.createElement("dt"); dt.className = "font-semibold"; dt.textContent = label;
    const dd = document.createElement("dd"); dd.className = "text-ink-soft break-words whitespace-pre-wrap";
    dd.textContent = value == null || value === "" ? "Por definir" : Array.isArray(value) ? value.join(", ") : String(value);
    row.append(dt, dd); sheet.append(row);
    if (value != null && value !== "") briefLines.push(`${label}: ${dd.textContent}`);
  }
  document.dispatchEvent(new CustomEvent("design:brief", {detail: {text: briefLines.join("\n")}}));
  document.querySelector("#brief-state")!.textContent = {Collecting: "Estamos completando los detalles", NeedsReview: "Hay detalles que debemos revisar", ReadyForReview: "Ficha completa para revisión"}[data.assessment.status] ?? "En preparación";
  const issues = document.querySelector("#design-issues")!; issues.replaceChildren();
  for (const text of data.assessment.issues) { const li = document.createElement("li"); li.textContent = text; issues.append(li); }
  const pending = sessionStorage.getItem(key());
  if (pending) {
    try { const p = JSON.parse(pending); if (data.messages.some(t => t.id === p.requestId)) sessionStorage.removeItem(key()); }
    catch { sessionStorage.removeItem(key()); }
  }
  status.textContent = data.busy ? "Preparando tu ficha…" : !enabled ? "El asistente no está disponible; puedes consultar tus ideas guardadas." : !data.budgetAvailable || !data.remainingTurns ? "Esta conversación alcanzó su límite. Puedes consultar tu ficha o solicitar asesoría." : "Conversación guardada. Puedes continuar o corregir cualquier dato.";
  lock(); messages.scrollTop = messages.scrollHeight;
  clearTimeout(timer);
  if (data.busy) timer = setTimeout(() => load(data.id), 2500);
}
async function load(id: string) {
  clearTimeout(timer); active = id; select.value = id; loading = true; lock();
  try {
    const data = await apiFetch<Conversation>(`/api/chat/conversations/${id}`);
    if (active === id) render(data);
  } catch (e) { if (active === id) { status.textContent = (e as Error).message; current = null; } }
  finally { if (active === id) { loading = false; lock(); } }
}
async function list(reset = false) {
  if (reset) { skip = 0; select.replaceChildren(new Option("Nueva idea", "")); }
  const items = await apiFetch<Summary[]>(`/api/chat/conversations?skip=${skip}`);
  for (const item of items) if (![...select.options].some(o => o.value === item.id)) select.add(new Option(item.title, item.id));
  skip += items.length; more.classList.toggle("hidden", items.length < 30); select.value = active;
}
function fresh() {
  clearTimeout(timer); active = ""; current = null; select.value = ""; input.value = "";
  messages.replaceChildren(); showMessage("¿Qué te gustaría crear? Cuéntame tu idea y para qué la necesitas.", false);
  document.dispatchEvent(new CustomEvent("design:brief", {detail: {text: ""}}));
  choices.replaceChildren(); document.querySelector("#design-brief")!.replaceChildren(); document.querySelector("#design-issues")!.replaceChildren();
  document.querySelector("#brief-state")!.textContent = "Se completará mientras conversamos.";
  status.textContent = enabled ? "Escribe tu primera idea para comenzar." : "El asistente aún no está disponible. Puedes usar Cotizar."; lock();
}
select.addEventListener("change", () => { if (select.value) void load(select.value); else fresh(); });
create.addEventListener("click", fresh);
more.addEventListener("click", () => list().catch(e => status.textContent = e.message));
document.querySelector("#chat-form")!.addEventListener("submit", async event => {
  event.preventDefault(); const text = input.value.trim(); if (!text || send.disabled) return;
  sending = true; lock(); status.textContent = "Preparando tu respuesta…";
  try {
    if (!active) {
      const result = await apiFetch<{id: string}>("/api/chat/conversations", {method: "POST"}); active = result.id;
      select.add(new Option("Nueva idea", active)); select.value = active;
    }
    let request = {requestId: crypto.randomUUID(), message: text};
    const pending = sessionStorage.getItem(key());
    if (pending) { try { const saved = JSON.parse(pending); if (saved.message === text) request = saved; } catch {} }
    sessionStorage.setItem(key(), JSON.stringify(request));
    const data = await apiFetch<Conversation>(`/api/chat/conversations/${active}/messages`, {method: "POST", body: JSON.stringify(request)});
    input.value = ""; render(data);
    const option = [...select.options].find(o => o.value === active); if (option) option.textContent = data.title;
  } catch (e) { status.textContent = `${(e as Error).message} Tu texto sigue aquí para que puedas reintentarlo.`; }
  finally { sending = false; lock(); }
});
async function init() {
  const availability = await apiFetch<{enabled: boolean}>("/api/chat/status"); enabled = availability.enabled;
  catalog = await apiFetch<typeof catalog>("/api/manufacturing/catalog");
  await list(); fresh();
}
init().catch(e => { status.textContent = `No se pudo cargar el asistente: ${e.message}`; lock(); });

}
void startDesign().catch(e => { document.querySelector("#chat-status")!.textContent = e.message; });
