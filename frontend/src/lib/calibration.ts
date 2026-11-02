import {apiFetch,requireAuth} from "./api";
requireAuth("Admin");
const form=document.querySelector<HTMLFormElement>("#calibration-form")!;
const status=document.querySelector<HTMLElement>("#calibration-status")!;
const save=document.querySelector<HTMLButtonElement>("#calibration-save")!;
type Sample=Record<string,string|number>;
let samples:Sample[]=[],version=0,manufacturingVersion=0;
function render(){
  const target=document.querySelector("#calibration-samples")!;target.replaceChildren();
  samples.forEach((s,index)=>{
    const row=document.createElement("div");row.className="border rounded-xl p-3 text-sm";
    const text=document.createElement("p");text.textContent=`${s.categoryId} / ${s.materialId} / ${s.printerId} · ${s.complexity} · ${s.colorCount} colores · ${s.widthMm}×${s.depthMm}×${s.heightMm} mm · ${s.grams} g · máquina ${s.machineHours} h · diseño ${s.designHours} h · acabado ${s.finishingHours} h · ${s.evidence}`;
    const remove=document.createElement("button");remove.type="button";remove.textContent="Quitar muestra";remove.className="underline text-red-700 mt-2";
    remove.addEventListener("click",()=>{samples.splice(index,1);render();});row.append(text,remove);target.append(row);
  });
}
async function load(){
  const [data,catalog]=await Promise.all([
    apiFetch<{version:number;manufacturingVersion:number;outdated:boolean;samples:Sample[];history:{version:number;createdAt:string}[]}>("/api/estimation/calibration"),
    apiFetch<{version:number;configuration:Record<string,{id:string;name:string}[]>}>("/api/manufacturing")
  ]);
  if(catalog.version!==data.manufacturingVersion)throw new Error("La configuración cambió durante la carga. Recarga la página.");
  samples=data.samples;version=data.version;manufacturingVersion=data.manufacturingVersion;
  for(const [field,list] of [["categoryId","categories"],["materialId","materials"],["printerId","printers"]]){
    const select=form.elements.namedItem(field) as HTMLSelectElement;select.replaceChildren();
    for(const item of catalog.configuration[list])select.add(new Option(item.name,item.id));
  }
  status.textContent=`Calibración v${version}. Fabricación v${manufacturingVersion}. ${data.outdated?"La calibración anterior está desactualizada: revisa todas las muestras antes de guardarlas.":"Sin muestras aplicables, se solicita revisión manual."}`;
  document.querySelector("#calibration-history")!.textContent=data.history.map(r=>`v${r.version} · ${new Date(r.createdAt).toLocaleString("es-CO")}`).join(" | ");
  form.classList.remove("hidden");save.disabled=false;render();
}
form.addEventListener("submit",event=>{
  event.preventDefault();if(samples.length>=1000){status.textContent="Máximo 1000 muestras por versión.";return;}
  const sample:Sample={};for(const input of form.querySelectorAll<HTMLInputElement|HTMLSelectElement>("[name]"))sample[input.name]=input instanceof HTMLInputElement&&input.type==="number"?Number(input.value):input.value.trim();
  samples.push(sample);render();status.textContent="Muestra agregada. Guarda la versión para aplicar los cambios.";
});
save.addEventListener("click",async()=>{
  save.disabled=true;
  try{await apiFetch("/api/estimation/calibration",{method:"PUT",body:JSON.stringify({expectedVersion:version,manufacturingVersion,samples})});await load();status.textContent=`Calibración v${version} guardada.`;}
  catch(e){status.textContent=(e as Error).message;}
  finally{save.disabled=false;}
});
load().catch(e=>status.textContent=e.message);
