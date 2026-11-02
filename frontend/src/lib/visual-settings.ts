import { apiFetch, requireAuth } from "./api";
requireAuth("Admin");
const form = document.querySelector<HTMLFormElement>("#image-settings-form")!;
const status = document.querySelector<HTMLElement>("#image-settings-status")!;
const save = document.querySelector<HTMLButtonElement>("#save-image-settings")!;
let version = 0;
function readPath(value: Record<string, unknown>, path: string): unknown {
  return path.split(".").reduce<unknown>((current, key) => (current as Record<string, unknown>)?.[key], value);
}
function writePath(value: Record<string, unknown>, path: string, data: unknown) {
  const keys = path.split("."); let current = value;
  for (const key of keys.slice(0, -1)) current = (current[key] ??= {}) as Record<string, unknown>;
  current[keys[keys.length - 1]] = data;
}
async function load() {
  const result = await apiFetch<{version: number; keyConfigured: boolean; configuration: Record<string, unknown>}>("/api/visual-settings");
  version = result.version;
  for (const input of form.querySelectorAll<HTMLInputElement | HTMLSelectElement>("[name]")) {
    if (input instanceof HTMLInputElement && input.type === "checkbox") input.checked = Boolean(readPath(result.configuration, input.name));
    else input.value = String(readPath(result.configuration, input.name) ?? "");
  }
  form.classList.remove("hidden");
  status.textContent = `Versión ${version}. ${result.keyConfigured ? "Clave configurada en el servidor." : "Falta configurar la clave privada de OpenRouter en el servidor."}`;
  await history();
}
async function history() {
  const revisions = await apiFetch<{version: number; createdAt: string}[]>("/api/visual-settings/history");
  document.querySelector("#image-settings-history")!.textContent = revisions.map(r => `Versión ${r.version} · ${new Date(r.createdAt).toLocaleString("es-CO")}`).join(" | ") || "Aún no hay versiones.";
  const usage = await apiFetch<{id: string; number: number; status: string; reservedUsd: number; attempts: {reportedCostUsd: number | null}[]}[]>("/api/visual-settings/usage");
  const target = document.querySelector("#image-usage")!; target.replaceChildren();
  const table = document.createElement("table"); table.className = "w-full text-sm text-left";
  const header = table.createTHead().insertRow();
  for (const name of ["Solicitud", "Estado", "Intentos", "Reserva USD", "Consumo reportado USD"]) { const th = document.createElement("th"); th.className = "p-2 border-b"; th.textContent = name; header.append(th); }
  const body = table.createTBody();
  for (const item of usage) {
    const row = body.insertRow();
    const cost = item.attempts.length && item.attempts.every(a => a.reportedCostUsd !== null) ? item.attempts.reduce((sum, a) => sum + a.reportedCostUsd!, 0).toFixed(6) : "Desconocido";
    for (const value of [item.id.slice(0, 8), item.status, String(item.attempts.length), item.reservedUsd.toFixed(3), cost]) { const cell = row.insertCell(); cell.className = "p-2 border-b"; cell.textContent = value; }
  }
  target.append(table);
}
form.addEventListener("submit", async event => {
  event.preventDefault(); save.disabled = true;
  const configuration: Record<string, unknown> = {};
  for (const input of form.querySelectorAll<HTMLInputElement | HTMLSelectElement>("[name]"))
    writePath(configuration, input.name, input instanceof HTMLInputElement && input.type === "checkbox" ? input.checked : input instanceof HTMLInputElement && input.type === "number" ? Number(input.value) : input.value.trim());
  try {
    const result = await apiFetch<{version: number}>("/api/visual-settings", {method: "PUT", body: JSON.stringify({expectedVersion: version, configuration})});
    version = result.version; status.textContent = `Versión ${version} guardada. Las propuestas conservan la configuración con la que se solicitaron.`; await history();
  } catch (e) { status.textContent = (e as Error).message; }
  finally { save.disabled = false; }
});
document.querySelector("#reload-image-settings")!.addEventListener("click", () => { if (confirm("¿Recargar y descartar los cambios sin guardar?")) load().catch(e => status.textContent = e.message); });
load().catch(e => status.textContent = e.message);
