import {apiFetch,API_URL,getSession,requireAuth} from "./api";
import {cop,estimateSummary,type Estimate} from "./estimate-format";
import {modelPreview} from "./model-preview";
const model=modelPreview(document.querySelector("#quote-model-preview")!,document.querySelector("#quote-model-rotation")!,document.querySelector("#quote-model-download")!,document.querySelector("#quote-model-caption")!);
requireAuth();
const id=Number(new URLSearchParams(location.search).get("id"));
const status=document.querySelector<HTMLElement>("#design-quote-status")!;
const form=document.querySelector<HTMLFormElement>("#design-quote-review")!;
let version=0,blobUrl:string|undefined;
async function load(){
  if(!Number.isSafeInteger(id)||id<=0)throw new Error("Solicitud inválida.");
  const data=await apiFetch<{snapshot:{brief:Record<string,unknown>;proposalNumber?:number;parameters?:Record<string,unknown>;estimate:Estimate;turns:{userText:string;assistantText:string}[]}|null;confirmedPriceCop:number|null;reviewVersion:number;adminNotes:string|null;status:string;imageUrl:string|null;modelUrl:string|null}>(`/api/estimation/quotes/${id}`);
  version=data.reviewVersion;
  if(data.modelUrl){await model.load(data.modelUrl);document.querySelector<HTMLElement>("#quote-model-controls")!.hidden=false;}
  if(!data.snapshot){status.textContent="Esta cotización no proviene del asistente de diseño.";return;}
  status.textContent=`Solicitud #${id} · ${data.modelUrl ? "Modelo paramétrico" : `Propuesta ${data.snapshot.proposalNumber}`} · ${data.confirmedPriceCop===null?"Pendiente de precio confirmado":`Precio confirmado: ${cop(data.confirmedPriceCop)}`} · ${data.status}`;
  document.querySelector("#design-quote-summary")!.textContent=`Estimación original\n${estimateSummary(data.snapshot.estimate)}\n\nRespuesta: ${data.adminNotes??"Pendiente de revisión."}`;
  const b=data.snapshot.brief;
  document.querySelector("#design-quote-brief")!.textContent=`${b.object} · ${b.use}\n${b.widthMm} × ${b.depthMm} × ${b.heightMm} mm · ${b.quantity} unidades\nMaterial: ${b.materialId} · Colores: ${(b.colors as string[]??[]).join(", ")}\nPersonalización: ${b.personalization??""}\nReferencia: ${b.reference??""}`;
  const turns=document.querySelector("#design-quote-turns")!;turns.replaceChildren();
  if(data.snapshot.parameters){
    const p=data.snapshot.parameters;
    const templates:Record<string,string>={box:"Caja abierta",plate:"Placa lisa",organizer:"Organizador",frame:"Marco rectangular"};
    document.querySelector("#design-quote-brief")!.textContent+=`\nPlantilla: ${templates[String(p.template)]??"Modelo"}\nParedes: ${p.wallMm} mm · Base: ${p.baseMm} mm · Divisiones: ${p.columns} × ${p.rows}`;
  }
  for(const t of data.snapshot.turns){const p=document.createElement("p");p.className="whitespace-pre-wrap text-sm";p.textContent=`Cliente: ${t.userText}\nAsistente: ${t.assistantText}`;turns.append(p);}
  if(getSession()?.user.role==="Admin"&&data.status!=="Converted"){
    form.classList.remove("hidden");(form.elements.namedItem("priceCop") as HTMLInputElement).value=String(data.confirmedPriceCop??"");(form.elements.namedItem("notes") as HTMLTextAreaElement).value=data.adminNotes??"";
  }else form.classList.add("hidden");
  if(data.imageUrl){
    const response=await fetch(`${API_URL}${data.imageUrl}`,{headers:{Authorization:`Bearer ${getSession()?.token}`},cache:"no-store"});
    if(!response.ok)throw new Error("No se pudo cargar la imagen privada.");
    const blob=await response.blob();if(blobUrl)URL.revokeObjectURL(blobUrl);blobUrl=URL.createObjectURL(blob);
    const image=document.querySelector<HTMLImageElement>("#design-quote-image")!;image.src=blobUrl;image.classList.remove("hidden");
  }
}
form.addEventListener("submit",async event=>{
  event.preventDefault();const button=form.querySelector("button")!;button.disabled=true;
  try{await apiFetch(`/api/estimation/quotes/${id}/review`,{method:"PUT",body:JSON.stringify({expectedVersion:version,priceCop:Number((form.elements.namedItem("priceCop") as HTMLInputElement).value),notes:(form.elements.namedItem("notes") as HTMLTextAreaElement).value})});await load();}
  catch(e){status.textContent=(e as Error).message;}finally{button.disabled=false;}
});
window.addEventListener("pagehide",()=>{if(blobUrl)URL.revokeObjectURL(blobUrl);});
load().catch(e=>status.textContent=e.message);
