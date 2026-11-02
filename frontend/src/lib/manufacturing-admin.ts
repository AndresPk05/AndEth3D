import { apiFetch, escapeHtml as esc, requireAuth } from "./api";
import { manufacturingSummary } from "./manufacturing-summary";

requireAuth("Admin");
type Color = { name: string; available: boolean };
type Material = { id: string; name: string; active: boolean; costPerKgCop: number; colors: Color[] };
type Printer = { id: string; name: string; brand: string; model: string; technology: string; active: boolean;
  widthMm: number; depthMm: number; heightMm: number; marginMm: number; nozzleMm: number; minimumWallMm: number; maxColors: number; materialIds: string[] };
type Category = { id: string; name: string; active: boolean };
type Config = { materials: Material[]; printers: Printer[]; categories: Category[]; pricing: Record<string, number> };
let config: Config;
let version = 0;
const form = document.querySelector<HTMLFormElement>("#manufacturing-form")!;
const status = document.querySelector<HTMLElement>("#config-status")!;
const save = document.querySelector<HTMLButtonElement>("#save-config")!;
const field = (key: string, label: string, value: string | number, type = "text", min = "0", max = "100000000", step = "0.01") =>
  `<label class="block text-sm font-semibold">${esc(label)}<input data-field="${key}" type="${type}" value="${esc(value)}" required ${type === "number" ? `min="${min}" max="${max}" step="${step}"` : 'maxlength="100"'} class="block w-full mt-1 border border-brand-200 rounded-xl px-3 py-2" /></label>`;
const check = (key: string, label: string, value: boolean) => `<label class="flex items-center gap-2 text-sm"><input data-field="${key}" type="checkbox" ${value ? "checked" : ""} />${esc(label)}</label>`;
const card = (kind: string, id: string, content: string) => `<fieldset data-kind="${kind}" data-id="${esc(id)}" class="border border-brand-200 rounded-xl p-4 grid sm:grid-cols-2 gap-4">${content}<button type="button" data-remove class="text-sm underline text-red-700 justify-self-start">Retirar de esta configuración</button></fieldset>`;

function render() {
  document.querySelector("#materials")!.innerHTML = config.materials.map(m => card("material", m.id,
    field("name", "Nombre público", m.name) + field("costPerKgCop", "Costo por kilogramo (COP)", m.costPerKgCop, "number", "0.01") + check("active", "Material habilitado", m.active) +
    `<div class="sm:col-span-2"><p class="font-semibold mb-2">Colores</p><div class="space-y-2">${m.colors.map((c, i) => `<div data-color="${i}" class="flex gap-3 items-center">${field("name", "Color", c.name)}${check("available", "Disponible", c.available)}<button type="button" data-remove-color class="text-sm underline text-red-700">Retirar color</button></div>`).join("")}</div><button type="button" data-add-color="${esc(m.id)}" class="mt-3 underline">Agregar color</button></div>`)).join("");
  document.querySelector("#printers")!.innerHTML = config.printers.map(p => card("printer", p.id,
    field("name", "Nombre interno", p.name) + field("brand", "Marca", p.brand) + field("model", "Modelo", p.model) + check("active", "Equipo activo", p.active) +
    field("widthMm", "Ancho nominal (mm)", p.widthMm, "number", "1", "5000") + field("depthMm", "Fondo nominal (mm)", p.depthMm, "number", "1", "5000") +
    field("heightMm", "Altura nominal (mm)", p.heightMm, "number", "1", "5000") + field("marginMm", "Margen (mm)", p.marginMm, "number", "0", "100") +
    field("nozzleMm", "Boquilla instalada (mm)", p.nozzleMm, "number", "0.1", "2") + field("minimumWallMm", "Pared mínima (mm)", p.minimumWallMm, "number", "0.1", "20") +
    field("maxColors", "Máximo de colores simultáneos", p.maxColors, "number", "1", "16", "1") +
    `<div><p class="font-semibold mb-2">Materiales compatibles</p>${config.materials.map(m => `<label class="flex gap-2"><input type="checkbox" data-material="${esc(m.id)}" ${p.materialIds.includes(m.id) ? "checked" : ""} />${esc(m.name)}</label>`).join("")}</div>`)).join("");
  document.querySelector("#categories")!.innerHTML = config.categories.map(c => card("category", c.id, field("name", "Nombre público", c.name) + check("active", "Categoría habilitada", c.active))).join("");
  const rates = [["machineHourCop", "Hora de máquina (COP)"], ["designHourCop", "Hora de diseño (COP)"], ["finishingHourCop", "Hora de acabado (COP)"], ["minimumOrderCop", "Pedido mínimo (COP)"]];
  document.querySelector("#pricing")!.innerHTML = rates.map(([key, label]) => field(key, label, config.pricing[key], "number")).join("") +
    field("grossMargin", "Margen bruto (%)", Number((config.pricing.grossMargin * 100).toFixed(2)), "number", "0", "95");
}

function collect() {
  form.querySelectorAll<HTMLElement>("[data-kind]").forEach(el => {
    const list = el.dataset.kind === "material" ? config.materials : el.dataset.kind === "printer" ? config.printers : config.categories;
    const item = list.find(x => x.id === el.dataset.id)!;
    el.querySelectorAll<HTMLInputElement>("[data-field]").forEach(input => {
      if (input.closest("[data-color]")) return;
      (item as unknown as Record<string, unknown>)[input.dataset.field!] = input.type === "checkbox" ? input.checked : input.type === "number" ? Number(input.value) : input.value.trim();
    });
    if (el.dataset.kind === "printer") (item as Printer).materialIds = [...el.querySelectorAll<HTMLInputElement>("[data-material]:checked")].map(i => i.dataset.material!);
    if (el.dataset.kind === "material") (item as Material).colors = [...el.querySelectorAll<HTMLElement>("[data-color]")].map(row => ({
      name: row.querySelector<HTMLInputElement>('[data-field="name"]')!.value.trim(), available: row.querySelector<HTMLInputElement>('[data-field="available"]')!.checked
    }));
  });
  document.querySelectorAll<HTMLInputElement>("#pricing input").forEach(i => config.pricing[i.dataset.field!] = Number(i.value) / (i.dataset.field === "grossMargin" ? 100 : 1));
}
async function history() {
  const items = await apiFetch<{version: number; createdAt: string}[]>("/api/manufacturing/history");
  document.querySelector("#history")!.innerHTML = items.map(i => `<button type="button" data-version="${i.version}" class="border rounded-xl px-3 py-2 mr-2 mb-2">Versión ${i.version} · ${esc(new Date(i.createdAt).toLocaleString("es-CO"))}</button>`).join("") || "Aún no hay versiones guardadas.";
}
async function load() {
  const data = await apiFetch<{version: number; configuration: Config}>("/api/manufacturing");
  version = data.version; config = data.configuration; render(); form.classList.remove("hidden");
  status.textContent = version ? `Versión actual: ${version}.` : "Configuración inicial sin publicar. Registra costos y disponibilidad reales antes de activar materiales y equipos.";
  await history();
}
form.addEventListener("click", event => {
  const removeColor = (event.target as HTMLElement).closest<HTMLElement>("[data-remove-color]");
  if (removeColor) {
    collect();
    const materialId = removeColor.closest<HTMLElement>('[data-kind="material"]')!.dataset.id;
    const index = Number(removeColor.closest<HTMLElement>("[data-color]")!.dataset.color);
    config.materials.find(m => m.id === materialId)!.colors.splice(index, 1);
    render(); return;
  }
  const remove = (event.target as HTMLElement).closest<HTMLElement>("[data-remove]");
  if (remove) {
    if (!confirm("¿Retirar este elemento? Las versiones anteriores se conservarán. Debes guardar para aplicar el cambio.")) return;
    collect();
    const card = remove.closest<HTMLElement>("[data-kind]")!;
    const id = card.dataset.id!;
    if (card.dataset.kind === "material") {
      config.materials = config.materials.filter(m => m.id !== id);
      config.printers.forEach(p => p.materialIds = p.materialIds.filter(value => value !== id));
    } else if (card.dataset.kind === "printer") config.printers = config.printers.filter(p => p.id !== id);
    else config.categories = config.categories.filter(c => c.id !== id);
    render(); return;
  }
  const target = (event.target as HTMLElement).closest<HTMLElement>("[data-add-color]");
  if (!target) return;
  collect(); config.materials.find(m => m.id === target.dataset.addColor)!.colors.push({ name: "", available: false }); render();
});
document.querySelector("#add-material")!.addEventListener("click", () => { collect(); config.materials.push({id: crypto.randomUUID(), name: "", active: false, costPerKgCop: 0, colors: []}); render(); });
document.querySelector("#add-printer")!.addEventListener("click", () => { collect(); config.printers.push({id: crypto.randomUUID(), name: "", brand: "", model: "", technology: "FDM", active: false, widthMm: 256, depthMm: 256, heightMm: 256, marginMm: 5, nozzleMm: 0.4, minimumWallMm: 1.2, maxColors: 1, materialIds: []}); render(); });
document.querySelector("#add-category")!.addEventListener("click", () => { collect(); config.categories.push({id: crypto.randomUUID(), name: "", active: true}); render(); });
document.querySelector("#reload-config")!.addEventListener("click", () => { if (confirm("¿Descartar cambios sin guardar?")) load().catch(e => status.textContent = e.message); });
document.querySelector("#history")!.addEventListener("click", async event => {
  const button = (event.target as HTMLElement).closest<HTMLElement>("[data-version]");
  if (!button) return;
  try { const data = await apiFetch(`/api/manufacturing/history/${button.dataset.version}`); const panel = document.querySelector<HTMLElement>("#historical-config")!; panel.textContent = manufacturingSummary(data); panel.classList.remove("hidden"); }
  catch (e) { status.textContent = (e as Error).message; }
});
form.addEventListener("submit", async event => {
  event.preventDefault(); collect(); save.disabled = true;
  try {
    const result = await apiFetch<{version: number}>("/api/manufacturing", {method: "PUT", body: JSON.stringify({expectedVersion: version, configuration: config})});
    version = result.version; status.textContent = `Versión ${version} guardada. Las cotizaciones anteriores conservan sus condiciones.`; await history();
  } catch (e) { status.textContent = (e as Error).message; }
  finally { save.disabled = false; status.scrollIntoView({behavior: "smooth", block: "center"}); }
});
load().catch(e => status.textContent = `No se pudo cargar: ${e.message}`);
