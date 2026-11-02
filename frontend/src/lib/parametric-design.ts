import {apiFetch,getSession} from "./api";
import {estimateSummary,type Estimate} from "./estimate-format";
import {modelPreview} from "./model-preview";
type Model={id:string;status:string;outdated:boolean;modelUrl:string;message:string;estimate:Estimate|null;parameters:{template:string;wallMm:number;baseMm:number;rows:number;columns:number}};
type State={fingerprint:string;slicingEnabled:boolean;busy:boolean;models:Model[]};
const form=document.querySelector<HTMLFormElement>("#parametric-form")!,status=document.querySelector<HTMLElement>("#parametric-status")!;
const history=document.querySelector<HTMLSelectElement>("#parametric-history")!,create=document.querySelector<HTMLButtonElement>("#parametric-create")!,quote=document.querySelector<HTMLButtonElement>("#parametric-quote")!;
const confirm=document.querySelector<HTMLInputElement>("#parametric-confirm")!,result=document.querySelector<HTMLAnchorElement>("#parametric-result")!;
const preview=modelPreview(document.querySelector("#parametric-preview")!,document.querySelector("#parametric-rotation")!,document.querySelector("#parametric-download")!,document.querySelector("#parametric-caption")!);
let id="",busy=false,posting=false,serial=0,state:State|null=null,loaded="",viewed="",poll:ReturnType<typeof setTimeout>|undefined;
function selected(){return state?.models.find(m=>m.id===history.value);}
function controls(){const model=selected();create.disabled=!id||busy||posting||!state||state.busy||state.models.some(m=>["Queued","Processing"].includes(m.status));quote.disabled=!model||viewed!==model.id||model.outdated||busy||posting||!confirm.checked||["Queued","Processing"].includes(model.status);}
async function show(){
  const m=selected();controls();if(!m){preview.clear();loaded="";document.querySelector("#parametric-estimate")!.textContent="";return;}
  status.textContent=m.outdated?"Este modelo usa requisitos o condiciones anteriores. Crea una nueva versión para cotizar.":m.message;
  document.querySelector("#parametric-estimate")!.textContent=`Plantilla: ${m.parameters.template} · Pared ${m.parameters.wallMm} mm · Base ${m.parameters.baseMm} mm · ${m.parameters.columns} × ${m.parameters.rows} divisiones\n\n${m.estimate?estimateSummary(m.estimate):"Sin precio automático. Puedes solicitar revisión manual del modelo."}`;
  if(loaded!==m.id){loaded=m.id;viewed="";controls();try{if(await preview.load(m.modelUrl)&&loaded===m.id){viewed=m.id;controls();}}catch(e){if(loaded===m.id){loaded="";status.textContent=(e as Error).message;}}}
}
async function load(){
  clearTimeout(poll);if(!id||busy)return;const active=id,request=++serial;
  try{
    const data=await apiFetch<State>(`/api/parametric/conversations/${active}`);if(request!==serial||active!==id)return;
    state=data;const chosen=history.value;history.replaceChildren();
    data.models.forEach((m,i)=>history.add(new Option(`Modelo ${data.models.length-i} · ${m.status}${m.outdated?" · anterior":""}`,m.id)));
    if(data.models.some(m=>m.id===chosen))history.value=chosen;
    if(!data.models.length)status.textContent=data.slicingEnabled?"Selecciona una plantilla compatible con tu ficha.":"Puedes crear modelos. El cálculo automático aún no está habilitado; enviaremos el modelo a revisión.";
    await show();controls();if(data.models.some(m=>["Queued","Processing"].includes(m.status)))poll=setTimeout(load,3000);
  }catch(e){if(request===serial){state=null;controls();status.textContent=(e as Error).message;}}
}
document.addEventListener("design:conversation",event=>{
  const detail=(event as CustomEvent<{id:string;busy:boolean}>).detail;serial++;clearTimeout(poll);state=null;confirm.checked=false;
  if(id!==detail.id){preview.clear();loaded="";history.replaceChildren();result.hidden=true;document.querySelector("#parametric-estimate")!.textContent="";}
  id=detail.id;busy=detail.busy;controls();if(!busy)void load();
});
history.addEventListener("change",()=>{confirm.checked=false;void show();});confirm.addEventListener("change",controls);
document.querySelector("#parametric-refresh")!.addEventListener("click",()=>void load());
form.addEventListener("submit",async event=>{
  event.preventDefault();if(create.disabled||!state)return;const active=id;
  const values=new FormData(form);const parameters={template:String(values.get("template")),wallMm:Number(values.get("wallMm")),baseMm:Number(values.get("baseMm")),columns:Number(values.get("columns")),rows:Number(values.get("rows"))};
  const key=`andeth-model-${getSession()?.user.id}-${active}-${state.fingerprint}-${JSON.stringify(parameters)}`;
  const requestId=sessionStorage.getItem(key)??crypto.randomUUID();sessionStorage.setItem(key,requestId);
  const body={requestId,expectedFingerprint:state.fingerprint,parameters,acceptTemplate:true};posting=true;controls();
  try{await apiFetch(`/api/parametric/conversations/${active}`,{method:"POST",body:JSON.stringify(body)});sessionStorage.removeItem(key);if(id===active){await load();history.value=requestId;confirm.checked=false;await show();}}
  catch(e){if(id===active)status.textContent=(e as Error).message;}finally{posting=false;controls();}
});
quote.addEventListener("click",async()=>{
  const m=selected();if(quote.disabled||!m)return;const active=id;
  const key=`andeth-model-quote-${getSession()?.user.id}-${m.id}`,requestId=sessionStorage.getItem(key)??crypto.randomUUID();sessionStorage.setItem(key,requestId);posting=true;controls();
  try{const r=await apiFetch<{quoteId:number}>(`/api/parametric/models/${m.id}/quotes`,{method:"POST",body:JSON.stringify({requestId})});if(id===active){result.href=`/cotizacion-diseno?id=${r.quoteId}`;result.hidden=false;status.textContent=`Cotización #${r.quoteId} enviada para revisión.`;confirm.checked=false;}}
  catch(e){if(id===active)status.textContent=(e as Error).message;}finally{posting=false;controls();}
});
window.addEventListener("pagehide",()=>clearTimeout(poll));
