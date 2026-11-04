import { apiFetch, API_URL, getDesignSession } from "./api";

type Proposal = {canUseAsReference?: boolean; id: string; number: number; status: string; outdated: boolean; imageUrl: string | null; message: string; brief: Record<string, unknown>; materialName: string};
type VisualState = {enabled: boolean; supportsEditing: boolean; fingerprint: string; canGenerate: boolean; reason: string; proposals: Proposal[]};
const generate = document.querySelector<HTMLButtonElement>("#visual-generate")!;
const refresh = document.querySelector<HTMLButtonElement>("#visual-refresh")!;
let referenceId = "";
const status = document.querySelector<HTMLElement>("#visual-status")!;
const image = document.querySelector<HTMLImageElement>("#visual-image")!;
// Discourage saving from the preview UI; this cannot prevent screenshots or network access.
image.addEventListener("contextmenu", event => event.preventDefault());
image.addEventListener("dragstart", event => event.preventDefault());
const empty = document.querySelector<HTMLElement>("#visual-empty")!;
let conversationId = "";
let chatBusy = false;
let posting = false;
let state: VisualState | null = null;
const autoAttempted = new Set<string>();
let selectedId = "";
let imageId = "";
let blobUrl: string | null = null;
let imageRequest = 0;
let refreshRequest = 0;
let poll: ReturnType<typeof setTimeout> | undefined;
function pendingKey() { return `andeth-visual-pending-${getDesignSession()?.user.id}-${conversationId}`; }
function controls() {
  generate.disabled = posting || chatBusy || !state?.canGenerate;
  refresh.disabled = !conversationId || posting;
  generate.textContent = posting ? "Guardando solicitud…" : referenceId ? "Crear nueva versión" : "Generar propuesta";
}
function releaseImage() {
  imageRequest++; imageId = ""; image.removeAttribute("src"); image.classList.add("hidden");
  if (blobUrl) URL.revokeObjectURL(blobUrl); blobUrl = null;
}
async function show(proposal?: Proposal) {
  document.querySelector("#visual-label")!.textContent = proposal ? `Versión ${proposal.number} · ${proposal.status === "Completed" ? "Lista" : proposal.status === "Failed" ? "No completada" : "En preparación"}` : "";
  document.querySelector("#visual-outdated")!.classList.toggle("hidden", !proposal?.outdated);
  if (!proposal?.imageUrl) {
    releaseImage(); empty.classList.remove("hidden"); empty.textContent = proposal?.message ?? state?.reason ?? "Describe tu idea para preparar la primera imagen."; return;
  }
  if (imageId === proposal.id && blobUrl) return;
  releaseImage(); const request = imageRequest; const active = conversationId;
  empty.classList.remove("hidden"); empty.textContent = "Cargando imagen privada…";
  try {
    const response = await fetch(`${API_URL}${proposal.imageUrl}`, {cache: "no-store", headers: {Authorization: `Bearer ${getDesignSession()?.token ?? ""}`}});
    if (response.status === 401) { throw new Error("Tu visita caducó. Recarga la página para continuar."); }
    if (!response.ok || response.headers.get("Content-Type")?.split(";")[0] !== "image/png") throw new Error("No pudimos cargar la imagen. Pulsa Actualizar propuestas para reintentar.");
    const blob = await response.blob();
    if (request !== imageRequest || active !== conversationId) return;
    blobUrl = URL.createObjectURL(blob); imageId = proposal.id;
    image.src = blobUrl; image.classList.remove("hidden"); empty.classList.add("hidden");
  } catch (e) { if (request === imageRequest && active === conversationId) { empty.classList.remove("hidden"); empty.textContent = (e as Error).message; } }
}
function render() {
  if (!state) return;
  status.textContent = state.proposals.find(p => !p.outdated)?.message ?? state.reason;
  const history = document.querySelector("#visual-history")!; history.replaceChildren();
  const versions = [...state.proposals].sort((a, b) => b.number - a.number);
  document.querySelector<HTMLElement>("#visual-versions")!.hidden = versions.length < 2;
  if (!versions.some(p => p.id === selectedId)) selectedId = versions[0]?.id ?? "";
  const selected = versions.find(p => p.id === selectedId);
  referenceId = state.supportsEditing && selected?.canUseAsReference === true && selected.status === "Completed" ? selected.id : "";
  for (const p of versions) {
    const button = document.createElement("button"); button.type = "button";
    const selected = selectedId === p.id;
    const label = p.status === "Completed" ? (p.id === versions[0].id ? "Más reciente" : "Anterior") : p.status === "Failed" ? "No completada" : "En preparación";
    button.textContent = `Versión ${p.number} · ${label}${selected ? " · Viendo" : ""}`;
    button.className = `min-w-0 rounded-xl border px-4 py-3 text-sm text-left break-words transition focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600 ${selected ? "border-brand-600 bg-brand-600 text-white font-semibold" : "border-brand-200 bg-white text-brand-700 hover:bg-brand-100"}`;
    button.setAttribute("aria-pressed", String(selected));
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
    const autoKey = `${active}:${state.fingerprint}`;
    if (!chatBusy && !posting && state.canGenerate && !state.proposals.some(p => !p.outdated) && !autoAttempted.has(autoKey)) {
      autoAttempted.add(autoKey);
      const previous = state.proposals.find(p => p.status === "Completed" && p.canUseAsReference === true);
      if (previous && state.supportsEditing) referenceId = previous.id;
      void requestPreview(true);
    }
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
    referenceId = ""; document.querySelector("#visual-history")!.replaceChildren();
    void show(); status.textContent = conversationId ? "Cargando propuestas…" : "Comienza una conversación para crear tu primera propuesta.";
  }
  controls(); if (conversationId && (changed || !chatBusy)) void reload();
});
refresh.addEventListener("click", () => {
  if (state) autoAttempted.delete(`${conversationId}:${state.fingerprint}`);
  void reload();
});
async function requestPreview(autoPreview = false) {
  if (!state || generate.disabled) return;
  const active = conversationId;
  let request = {requestId: crypto.randomUUID(), expectedFingerprint: state.fingerprint, parentProposalId: referenceId || null, autoPreview};
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
}
generate.addEventListener("click", () => void requestPreview());
window.addEventListener("pagehide", () => { clearTimeout(poll); releaseImage(); });
