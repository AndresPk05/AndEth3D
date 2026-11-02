import { apiFetch, API_URL, getDesignSession, clearSession } from "./api";

type Proposal = {id: string; number: number; status: string; outdated: boolean; imageUrl: string | null; message: string; brief: Record<string, unknown>; materialName: string};
type VisualState = {enabled: boolean; supportsEditing: boolean; fingerprint: string; canGenerate: boolean; reason: string; proposals: Proposal[]};
const generate = document.querySelector<HTMLButtonElement>("#visual-generate")!;
const refresh = document.querySelector<HTMLButtonElement>("#visual-refresh")!;
const parent = document.querySelector<HTMLSelectElement>("#visual-parent")!;
const status = document.querySelector<HTMLElement>("#visual-status")!;
const image = document.querySelector<HTMLImageElement>("#visual-image")!;
const download = document.querySelector<HTMLAnchorElement>("#visual-download")!;
const empty = document.querySelector<HTMLElement>("#visual-empty")!;
let conversationId = "";
let chatBusy = false;
let posting = false;
let state: VisualState | null = null;
let selectedId = "";
let imageId = "";
let blobUrl: string | null = null;
let imageRequest = 0;
let refreshRequest = 0;
let poll: ReturnType<typeof setTimeout> | undefined;
function pendingKey() { return `andeth-visual-pending-${getDesignSession()?.user.id}-${conversationId}`; }
function controls() {
  generate.disabled = posting || chatBusy || !state?.canGenerate;
  parent.disabled = posting || chatBusy || !state?.supportsEditing;
  refresh.disabled = !conversationId || posting;
  generate.textContent = posting ? "Guardando solicitud…" : parent.value ? "Actualizar desde esta imagen" : "Generar propuesta";
}
function releaseImage() {
  imageRequest++; imageId = ""; image.removeAttribute("src"); image.classList.add("hidden");
  download.classList.add("hidden"); download.removeAttribute("href");
  if (blobUrl) URL.revokeObjectURL(blobUrl); blobUrl = null;
}
async function show(proposal?: Proposal) {
  document.querySelector("#visual-label")!.textContent = proposal ? `Propuesta ${proposal.number} · ${proposal.status === "Completed" ? "Lista" : proposal.status === "Failed" ? "No completada" : "En preparación"}` : "";
  document.querySelector("#visual-outdated")!.classList.toggle("hidden", !proposal?.outdated);
  const b = proposal?.brief;
  document.querySelector("#visual-specification")!.textContent = b ?
    `Objeto: ${b.object}\nUso: ${b.use}\nMedidas: ${b.widthMm} × ${b.depthMm} × ${b.heightMm} mm\nCantidad: ${b.quantity}\nMaterial: ${proposal!.materialName}\nColores: ${(b.colors as string[] ?? []).join(", ")}\nPersonalización: ${b.personalization ?? "Sin especificar"}\nReferencia: ${b.reference ?? "Sin referencia"}` : "Selecciona una propuesta para consultar sus detalles.";
  if (!proposal?.imageUrl) {
    releaseImage(); empty.classList.remove("hidden"); empty.textContent = proposal?.message ?? "Tu propuesta aparecerá aquí."; return;
  }
  if (imageId === proposal.id && blobUrl) return;
  releaseImage(); const request = imageRequest; const active = conversationId;
  empty.classList.remove("hidden"); empty.textContent = "Cargando imagen privada…";
  try {
    const response = await fetch(`${API_URL}${proposal.imageUrl}`, {cache: "no-store", headers: {Authorization: `Bearer ${getDesignSession()?.token ?? ""}`}});
    if (response.status === 401) { clearSession(); window.location.assign("/crear"); throw new Error("Inicia sesión para ver la imagen."); }
    if (!response.ok || response.headers.get("Content-Type")?.split(";")[0] !== "image/png") throw new Error("No pudimos cargar la imagen. Pulsa Actualizar propuestas para reintentar.");
    const blob = await response.blob();
    if (request !== imageRequest || active !== conversationId) return;
    blobUrl = URL.createObjectURL(blob); imageId = proposal.id;
    image.src = blobUrl; image.classList.remove("hidden"); empty.classList.add("hidden");
    download.href = blobUrl; download.download = `propuesta-${proposal.number}.png`; download.classList.remove("hidden");
  } catch (e) { if (request === imageRequest && active === conversationId) { empty.classList.remove("hidden"); empty.textContent = (e as Error).message; } }
}
function render() {
  if (!state) return;
  status.textContent = state.reason;
  const reference = parent.value;
  parent.replaceChildren(new Option("Crear desde la ficha", ""));
  if (state.supportsEditing) for (const p of state.proposals.filter(p => p.status === "Completed")) parent.add(new Option(`Propuesta ${p.number}${p.outdated ? " (anterior)" : ""}`, p.id));
  if ([...parent.options].some(o => o.value === reference)) parent.value = reference;
  const history = document.querySelector("#visual-history")!; history.replaceChildren();
  if (!state.proposals.some(p => p.id === selectedId)) selectedId = state.proposals[0]?.id ?? "";
  for (const p of state.proposals) {
    const button = document.createElement("button"); button.type = "button";
    button.textContent = `Propuesta ${p.number}${p.outdated ? " · Anterior" : ""}`;
    button.className = "rounded-full border border-brand-200 px-3 py-2 text-sm hover:bg-brand-50";
    button.setAttribute("aria-pressed", String(selectedId === p.id));
    button.addEventListener("click", () => { selectedId = p.id; render(); }); history.append(button);
  }
  controls(); void show(state.proposals.find(p => p.id === selectedId));
}
async function reload() {
  clearTimeout(poll); if (!conversationId) return;
  const active = conversationId; const request = ++refreshRequest;
  try {
    const result = await apiFetch<VisualState>(`/api/chat/conversations/${active}/visuals`);
    if (conversationId !== active || request !== refreshRequest) return;
    state = result;
    const pending = sessionStorage.getItem(pendingKey());
    if (pending) { try { if (state.proposals.some(p => p.id === JSON.parse(pending).requestId)) sessionStorage.removeItem(pendingKey()); } catch { sessionStorage.removeItem(pendingKey()); } }
    render();
    document.dispatchEvent(new CustomEvent("design:visuals"));
    if (state.proposals.some(p => p.status === "Queued" || p.status === "Processing")) poll = setTimeout(reload, 3000);
  } catch (e) {
    if (conversationId === active && request === refreshRequest) {
      state = null; controls(); status.textContent = `No pudimos actualizar las propuestas: ${(e as Error).message}`;
    }
  }
}
document.addEventListener("design:conversation", event => {
  const detail = (event as CustomEvent<{id: string; busy: boolean}>).detail;
  const changed = detail.id !== conversationId;
  chatBusy = detail.busy;
  if (changed) {
    clearTimeout(poll); refreshRequest++; releaseImage(); conversationId = detail.id; state = null; selectedId = "";
    parent.replaceChildren(new Option("Crear desde la ficha", "")); document.querySelector("#visual-history")!.replaceChildren();
    void show(); status.textContent = conversationId ? "Cargando propuestas…" : "Comienza una conversación para crear tu primera propuesta.";
  }
  controls(); if (conversationId && (changed || !chatBusy)) void reload();
});
parent.addEventListener("change", controls);
refresh.addEventListener("click", () => void reload());
generate.addEventListener("click", async () => {
  if (!state || generate.disabled) return;
  const active = conversationId;
  let request = {requestId: crypto.randomUUID(), expectedFingerprint: state.fingerprint, parentProposalId: parent.value || null};
  const storageKey = pendingKey();
  const pending = sessionStorage.getItem(storageKey);
  if (pending) { try { const saved = JSON.parse(pending); if (saved.expectedFingerprint === request.expectedFingerprint && saved.parentProposalId === request.parentProposalId) request = saved; } catch {} }
  sessionStorage.setItem(storageKey, JSON.stringify(request));
  posting = true; controls(); status.textContent = "Guardando la solicitud. Puedes cerrar la página una vez que quede en preparación.";
  try {
    const result = await apiFetch<{id: string}>(`/api/chat/conversations/${active}/visuals`, {method: "POST", body: JSON.stringify(request)});
    sessionStorage.removeItem(storageKey);
    if (active === conversationId) { selectedId = result.id; await reload(); }
  } catch (e) { if (active === conversationId) status.textContent = `${(e as Error).message} Puedes actualizar las propuestas para comprobar si la solicitud quedó guardada.`; }
  finally { posting = false; controls(); }
});
window.addEventListener("pagehide", () => { clearTimeout(poll); releaseImage(); });
