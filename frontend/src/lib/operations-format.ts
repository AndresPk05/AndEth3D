export function metric(value:number|null|undefined,suffix=""):string{
  return value===null||value===undefined?"Sin datos":`${new Intl.NumberFormat("es-CO",{maximumFractionDigits:2}).format(value)}${suffix}`;
}
