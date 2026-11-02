export type Range = {min: number; max: number};
export type Estimate = {status: string; message: string; method: string; tariffVersion: number; calibrationVersion: number; complexity: string; quantity: number; gramsPerUnit: Range | null; machineHoursPerUnit: Range | null; unitPriceCop: Range | null; totalPriceCop: Range | null; averageTotalCop: number | null; assumptions: string[] | null};
export const cop = (value: number) => new Intl.NumberFormat("es-CO",{style:"currency",currency:"COP",maximumFractionDigits:2}).format(value);
export function estimateSummary(e: Estimate): string {
  if (e.status !== "Indicative" || !e.totalPriceCop || !e.unitPriceCop || !e.gramsPerUnit || !e.machineHoursPerUnit) return e.message;
  const range = (r: Range, money = false) => `${money ? cop(r.min) : r.min} – ${money ? cop(r.max) : r.max}`;
  return `Promedio orientativo total: ${cop(e.averageTotalCop!)}\nTotal (${e.quantity} unidades): ${range(e.totalPriceCop,true)}\nPor unidad: ${range(e.unitPriceCop,true)}\nFilamento por unidad: ${range(e.gramsPerUnit)} g\nTiempo de fabricación por unidad: ${range(e.machineHoursPerUnit)} h (no es plazo de entrega)\nTarifas v${e.tariffVersion} · Calibración v${e.calibrationVersion} · Método: ${e.method}\n\n${(e.assumptions ?? []).join("\n")}`;
}
