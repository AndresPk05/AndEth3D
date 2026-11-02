import {apiFetch,requireAuth} from "./api";
requireAuth("Admin");
const status=document.querySelector<HTMLElement>("#slicing-status")!;
type Job={id:string;status:string;failure:string|null;version:number;grams:number|null;seconds:number|null;actualGrams:number|null;actualSeconds:number|null;measurementNotes:string|null;gramErrorPercent:number|null;timeErrorPercent:number|null;reproductionHash:string|null};
async function load(){
  const data=await apiFetch<{enabled:boolean;worker:boolean;profiles:unknown[];jobs:Job[]}>("/api/parametric/admin");
  status.textContent=`Laminado ${data.enabled?"habilitado":"pausado"}. Trabajador en esta instancia: ${data.worker?"habilitado":"deshabilitado"}.`;
  document.querySelector("#slicing-profiles")!.textContent=JSON.stringify(data.profiles,null,2);
  const target=document.querySelector("#slicing-jobs")!;target.replaceChildren();
  for(const j of data.jobs){
    const card=document.createElement("article");card.className="bg-white border rounded-xl p-4";
    const detail=document.createElement("p");detail.className="text-sm whitespace-pre-wrap break-all";
    detail.textContent=`${j.id} · ${j.status}${j.failure?` · ${j.failure}`:""}\nLaminado: ${j.grams??"—"} g · ${j.seconds??"—"} s\nReal: ${j.actualGrams??"—"} g · ${j.actualSeconds??"—"} s\nDesviación material: ${j.gramErrorPercent?.toFixed(2)??"—"}% · Tiempo: ${j.timeErrorPercent?.toFixed(2)??"—"}%\n${j.measurementNotes??""}\nIdentidad reproducible: ${j.reproductionHash??"Pendiente"}`;
    card.append(detail);
    if(j.status==="Completed"&&j.actualGrams===null){
      const form=document.createElement("form");form.className="flex flex-wrap gap-3 mt-3";
      for(const [name,label,max] of [["grams","Gramos reales",100000],["seconds","Segundos reales",10000000]] as const){
        const wrapper=document.createElement("label");wrapper.textContent=label;
        const input=document.createElement("input");input.name=name;input.type="number";input.min=name==="grams"?"0.01":"1";input.max=String(max);input.step="0.01";input.required=true;input.className="block border rounded p-2";wrapper.append(input);form.append(wrapper);
      }
      const label=document.createElement("label");label.textContent="Referencia del trabajo y condiciones";
      const notes=document.createElement("input");notes.name="notes";notes.required=true;notes.minLength=3;notes.maxLength=2000;notes.className="block border rounded p-2";label.append(notes);form.append(label);
      const button=document.createElement("button");button.textContent="Registrar medición";button.className="rounded-full bg-brand-600 text-white px-4 py-2";form.append(button);
      form.addEventListener("submit",async e=>{
        e.preventDefault();button.disabled=true;const values=new FormData(form);
        try{await apiFetch(`/api/parametric/models/${j.id}/measurement`,{method:"PUT",body:JSON.stringify({expectedVersion:j.version,grams:Number(values.get("grams")),seconds:Number(values.get("seconds")),notes:values.get("notes")})});await load();}
        catch(err){status.textContent=(err as Error).message;}finally{button.disabled=false;}
      });card.append(form);
    }
    target.append(card);
  }
}
document.querySelector("#slicing-refresh")!.addEventListener("click",()=>load().catch(e=>status.textContent=e.message));
load().catch(e=>status.textContent=e.message);
