export function manufacturingSummary(data: any): string {
  if (!data.configuration) return data.message ?? "Sin configuración guardada.";
  const c = data.configuration;
  const money = (value: number) => new Intl.NumberFormat("es-CO", {style: "currency", currency: "COP"}).format(value);
  return [
    `Versión ${data.version}`,
    "", "EQUIPOS",
    ...c.printers.map((p: any) => `${p.name} · ${p.brand} ${p.model} · ${p.active ? "Activo" : "Inactivo"}\n  Volumen nominal: ${p.widthMm} × ${p.depthMm} × ${p.heightMm} mm; margen: ${p.marginMm} mm\n  Boquilla: ${p.nozzleMm} mm; pared mínima: ${p.minimumWallMm} mm; colores simultáneos: ${p.maxColors}\n  Materiales: ${p.materialIds.map((id: string) => c.materials.find((m: any) => m.id === id)?.name ?? id).join(", ")}`),
    "", "MATERIALES",
    ...c.materials.map((m: any) => `${m.name} · ${m.active ? "Habilitado" : "Deshabilitado"} · ${money(m.costPerKgCop)}/kg\n  Colores: ${m.colors.map((x: any) => `${x.name} (${x.available ? "disponible" : "no disponible"})`).join(", ") || "Sin colores registrados"}`),
    "", "CATEGORÍAS",
    ...c.categories.map((x: any) => `${x.name}: ${x.active ? "habilitada" : "deshabilitada"}`),
    "", "TARIFAS",
    `Máquina: ${money(c.pricing.machineHourCop)}/hora`,
    `Diseño: ${money(c.pricing.designHourCop)}/hora`,
    `Acabado: ${money(c.pricing.finishingHourCop)}/hora`,
    `Pedido mínimo: ${money(c.pricing.minimumOrderCop)}`,
    `Margen bruto: ${Number((c.pricing.grossMargin * 100).toFixed(2))}%`
  ].join("\n");
}
