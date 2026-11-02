import {apiFetch,requireAuth,getSession} from "./api";
import {metric} from "./operations-format";
requireAuth("Admin");
type Values=Record<string,number|null>;
type Production={id:string;orderId:number;categoryId:string;quantity:number;grams:number;machineHours:number;notes:string};
type Dashboard={asOf:string;funnel:Values;ai:Values;confirmedQuotes:Values;categories:(Values&{categoryId:string})[];definitions:string[];production:{record:Production;actualCostCop:number;marginPercent:number|null;priceDeviationPercent:number|null;gramsDeviationPercent:number|null;hoursDeviationPercent:number|null}[]};
const status=document.querySelector<HTMLElement>("#operations-status")!,days=document.querySelector<HTMLSelectElement>("#operations-days")!;
const form=document.querySelector<HTMLFormElement>("#production-form")!,productionStatus=document.querySelector<HTMLElement>("#production-status")!;
const orderInput=document.querySelector<HTMLInputElement>("#production-order")!,promote=document.querySelector<HTMLButtonElement>("#production-promote")!;
let orderId=0,manufacturingVersion=0,dashboardRequest=0,contextRequest=0;
let calibration:{manufacturingVersion:number;calibrationVersion:number;outdated:boolean;candidates:{record:Production;sample:{categoryId:string;materialId:string;printerId:string;complexity:string;grams:number;machineHours:number}}[]}|null=null;
function table(target:string,headings:string[],rows:(string|number)[][]){
  const wrapper=document.querySelector(target)!;wrapper.replaceChildren();
  const table=document.createElement("table");table.className="w-full text-sm text-left";
  const header=table.createTHead().insertRow();headings.forEach(h=>{const th=document.createElement("th");th.textContent=h;th.className="p-2 border-b";header.append(th);});
  const body=table.createTBody();rows.forEach(values=>{const row=body.insertRow();values.forEach(value=>{const cell=row.insertCell();cell.textContent=String(value);cell.className="p-2 border-b";});});wrapper.append(table);
}
async function dashboard(){
  const request=++dashboardRequest;const data=await apiFetch<Dashboard>(`/api/operations/dashboard?days=${days.value}`);if(request!==dashboardRequest)return;
  const f=data.funnel,a=data.ai;
  const rows:[string,string][]=[
    ["Confirmadas con estimación comparable",metric(data.confirmedQuotes.withEstimate)],
    ["Desviación media de precio confirmado",metric(data.confirmedQuotes.meanDeviationPercent,"%")],
    ["Confirmadas fuera del rango original",metric(data.confirmedQuotes.outsideOriginalRange)],
    ["Conversaciones de la cohorte",metric(f.conversations)],["Con cotización",metric(f.quoted)],["Convertidas a pedido",metric(f.converted)],
    ["Conversión de conversación a pedido",metric(f.conversionPercent,"%")],["Inactivas sin cotizar (≥7 días)",metric(f.inactiveWithoutQuote)],["Posible abandono / cohorte madura",metric(f.inactivityPercent,"%")],
    ["IA chat · reportado USD",metric(a.chatReportedUsd)],["IA imágenes · reportado USD",metric(a.imagesReportedUsd)],
    ["Intentos con costo desconocido",metric((a.chatUnknownCosts??0)+(a.imagesUnknownCosts??0))],["Reservas chat USD (no gasto)",metric(a.chatReservedUsd)],
    ["Reservas imágenes USD (no gasto)",metric(a.imagesReservedUsd)],["Regeneraciones / ediciones",`${metric(a.regenerations)} / ${metric(a.edits)}`]
  ];
  const grid=document.querySelector("#operations-metrics")!;grid.replaceChildren();rows.forEach(([label,value])=>{const card=document.createElement("div");card.className="bg-white border rounded-xl p-4";const title=document.createElement("p");title.className="text-sm";title.textContent=label;const number=document.createElement("strong");number.className="block text-xl mt-2";number.textContent=value;card.append(title,number);grid.append(card);});
  table("#operations-categories",["Categoría","Pedidos medidos","Muestras de precio","Desviación precio","Error absoluto material","Margen real medio","Costo real COP"],data.categories.map(c=>[c.categoryId,metric(c.count),metric(c.priceSamples),metric(c.meanPriceDeviationPercent,"%"),metric(c.meanAbsoluteGramsErrorPercent,"%"),metric(c.meanMarginPercent,"%"),metric(c.actualCostCop)]));
  table("#production-records",["Pedido","Categoría","Costo COP","Margen","Desviación precio","Desviación gramos","Desviación horas"],data.production.map(p=>[p.record.orderId,p.record.categoryId,metric(p.actualCostCop),metric(p.marginPercent,"%"),metric(p.priceDeviationPercent,"%"),metric(p.gramsDeviationPercent,"%"),metric(p.hoursDeviationPercent,"%") ]));
  const definitions=document.querySelector("#operations-definitions")!;definitions.replaceChildren();data.definitions.forEach(text=>{const li=document.createElement("li");li.textContent=text;definitions.append(li);});
  status.textContent=`Datos hasta ${new Date(data.asOf).toLocaleString("es-CO")}.`;
}
async function loadOrder(){
  const request=++contextRequest,id=Number(orderInput.value);orderId=0;form.classList.add("hidden");
  if(!Number.isSafeInteger(id)||id<=0)throw new Error("Selecciona un pedido válido.");
  const data=await apiFetch<{order:{id:number;title:string;status:string};brief:Record<string,unknown>|null;manufacturingVersion:number;printers:{id:string;name:string}[];materials:{id:string;name:string}[];categories:{id:string;name:string}[];production:Production|null}>(`/api/operations/orders/${id}`);
  if(request!==contextRequest)return;
  manufacturingVersion=data.manufacturingVersion;
  if(data.production){productionStatus.textContent=`Pedido #${id}: datos registrados (${data.production.grams} g y ${data.production.machineHours} h). ${data.production.notes}`;return;}
  if(!["Finished","Delivered"].includes(data.order.status)){productionStatus.textContent="El pedido debe estar terminado o entregado para registrar resultados.";return;}
  orderId=id;form.reset();
  for(const [name,items] of [["printerId",data.printers],["materialId",data.materials],["categoryId",data.categories]] as const){const select=form.elements.namedItem(name) as HTMLSelectElement;select.replaceChildren();items.forEach(item=>select.add(new Option(item.name,item.id)));}
  for(const name of ["categoryId","materialId","quantity"]){const input=form.elements.namedItem(name) as HTMLInputElement|HTMLSelectElement;input.disabled=!!data.brief;if(data.brief)input.value=String(data.brief[name]??"");}
  productionStatus.textContent=`Pedido #${id}: ${data.order.title}. Datos del lote completo. ${data.brief?"Ficha vinculada a la cotización.":"Pedido sin ficha: podrá medirse, pero no convertirse automáticamente en una muestra de calibración."}`;form.classList.remove("hidden");
}
async function candidates(){
  calibration=await apiFetch("/api/operations/calibration");const target=document.querySelector("#production-candidates")!;target.replaceChildren();
  document.querySelector("#production-calibration-status")!.textContent=calibration!.outdated?"La calibración está desactualizada: revísala antes de incorporar muestras.":`Fabricación v${calibration!.manufacturingVersion} · Calibración v${calibration!.calibrationVersion} · ${calibration!.candidates.length} candidatos.`;
  calibration!.candidates.forEach(({record:r,sample:s})=>{const label=document.createElement("label");label.className="block border rounded p-3 text-sm";const input=document.createElement("input");input.type="checkbox";input.value=r.id;input.className="mr-3";input.addEventListener("change",updatePromote);label.append(input,document.createTextNode(`Pedido #${r.orderId} · ${s.categoryId} / ${s.materialId} / ${s.printerId} · ${s.complexity} · ${metric(s.grams)} g/pieza · ${metric(s.machineHours)} h/pieza`));target.append(label);});updatePromote();
}
function updatePromote(){promote.disabled=!calibration||calibration.outdated||!document.querySelector("#production-candidates input:checked");}
form.addEventListener("submit",async event=>{
  event.preventDefault();if(!orderId)return;const id=orderId,button=document.querySelector<HTMLButtonElement>("#production-save")!;button.disabled=true;
  const values:Record<string,unknown>={manufacturingVersion};
  for(const input of form.querySelectorAll<HTMLInputElement|HTMLSelectElement|HTMLTextAreaElement>("[name]"))values[input.name]=input instanceof HTMLInputElement&&input.type==="number"?Number(input.value):input.value.trim();
  const key=`andeth-production-${getSession()?.user.id}-${id}`;const pending=sessionStorage.getItem(key);let requestId=crypto.randomUUID();
  if(pending){try{const old=JSON.parse(pending);if(old.payload===JSON.stringify(values))requestId=old.requestId;}catch{}}
  sessionStorage.setItem(key,JSON.stringify({requestId,payload:JSON.stringify(values)}));
  try{await apiFetch(`/api/operations/orders/${id}/production`,{method:"POST",body:JSON.stringify({...values,requestId})});sessionStorage.removeItem(key);if(Number(orderInput.value)===id)await loadOrder();await Promise.all([dashboard(),candidates()]);}
  catch(e){productionStatus.textContent=(e as Error).message;}finally{button.disabled=false;}
});
promote.addEventListener("click",async()=>{
  if(promote.disabled||!calibration)return;promote.disabled=true;
  const recordIds=[...document.querySelectorAll<HTMLInputElement>("#production-candidates input:checked")].map(i=>i.value);
  try{await apiFetch("/api/operations/calibration",{method:"POST",body:JSON.stringify({expectedCalibrationVersion:calibration.calibrationVersion,manufacturingVersion:calibration.manufacturingVersion,recordIds})});await candidates();}
  catch(e){document.querySelector("#production-calibration-status")!.textContent=(e as Error).message;updatePromote();}
});
document.querySelector("#production-lookup")!.addEventListener("submit",e=>{e.preventDefault();loadOrder().catch(e=>productionStatus.textContent=e.message);});
document.querySelector("#operations-refresh")!.addEventListener("click",()=>dashboard().catch(e=>status.textContent=e.message));
days.addEventListener("change",()=>dashboard().catch(e=>status.textContent=e.message));
Promise.all([dashboard(),candidates()]).catch(e=>status.textContent=e.message);
const initial=new URLSearchParams(location.search).get("orderId");if(initial){orderInput.value=initial;loadOrder().catch(e=>productionStatus.textContent=e.message);}
