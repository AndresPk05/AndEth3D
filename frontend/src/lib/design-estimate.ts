import {apiFetch,getSession} from "./api";
import {estimateSummary,type Estimate} from "./estimate-format";
const status=document.querySelector<HTMLElement>("#estimate-status")!;
const summary=document.querySelector<HTMLElement>("#estimate-summary")!;
const complexity=document.querySelector<HTMLSelectElement>("#estimate-complexity")!;
const proposal=document.querySelector<HTMLSelectElement>("#estimate-proposal")!;
const accept=document.querySelector<HTMLInputElement>("#estimate-accept")!;
const submit=document.querySelector<HTMLButtonElement>("#estimate-submit")!;
const resultLink=document.querySelector<HTMLAnchorElement>("#estimate-result")!;
let id="",busy=false,posting=false,sequence=0;
let state: {estimate:Estimate;token:string;busy:boolean}|null=null;
function controls(){submit.disabled=!id||busy||posting||!state||state.busy||!proposal.value||!accept.checked;}
async function load(){
  const request=++sequence,active=id;state=null;accept.checked=false;controls();summary.textContent="";
  if(!id||busy)return;
  status.textContent="Actualizando estimación.";
  try{
    const [estimate,visuals]=await Promise.all([
      apiFetch<{estimate:Estimate;token:string;busy:boolean}>(`/api/estimation/conversations/${active}?complexity=${complexity.value}`),
      apiFetch<{proposals:{id:string;number:number;status:string;outdated:boolean}[]}>(`/api/chat/conversations/${active}/visuals`)
    ]);
    if(request!==sequence||active!==id)return;
    state=estimate;const selected=proposal.value;
    proposal.replaceChildren(new Option("Selecciona una propuesta", ""));
    for(const p of visuals.proposals.filter(p=>p.status==="Completed"&&!p.outdated))proposal.add(new Option(`Propuesta ${p.number}`,p.id));
    if([...proposal.options].some(o=>o.value===selected))proposal.value=selected;
    status.textContent=estimate.estimate.message;summary.textContent=estimateSummary(estimate.estimate);controls();
  }catch(e){if(request===sequence){status.textContent=(e as Error).message;controls();}}
}
document.addEventListener("design:conversation",event=>{
  const detail=(event as CustomEvent<{id:string;busy:boolean}>).detail;
  if(id!==detail.id){resultLink.classList.add("hidden");proposal.replaceChildren();}
  id=detail.id;busy=detail.busy;void load();
});
document.addEventListener("design:visuals",()=>{if(!posting)void load();});
complexity.addEventListener("change",()=>void load());
proposal.addEventListener("change",()=>{accept.checked=false;controls();});accept.addEventListener("change",controls);
document.querySelector("#estimate-refresh")!.addEventListener("click",()=>void load());
submit.addEventListener("click",async()=>{
  if(submit.disabled||!state)return;
  const active=id;
  const key=`andeth-quote-${getSession()?.user.id}-${id}-${proposal.value}-${state.token}`;
  const requestId=sessionStorage.getItem(key)??crypto.randomUUID();sessionStorage.setItem(key,requestId);
  const body={requestId,proposalId:proposal.value,estimateToken:state.token,complexity:complexity.value};
  posting=true;controls();
  try{
    const response=await apiFetch<{quoteId:number}>(`/api/estimation/conversations/${active}/quotes`,{method:"POST",body:JSON.stringify(body)});
    if(active===id){status.textContent=`Solicitud #${response.quoteId} enviada para revisión.`;resultLink.href=`/cotizacion-diseno?id=${response.quoteId}`;resultLink.classList.remove("hidden");accept.checked=false;}
  }catch(e){if(active===id)status.textContent=(e as Error).message;}
  finally{posting=false;controls();}
});
