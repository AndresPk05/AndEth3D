import {API_URL,getDesignSession} from "./api";
import {parseStl,type Triangle} from "./stl-geometry";
export function modelPreview(canvas:HTMLCanvasElement,slider:HTMLInputElement,download:HTMLAnchorElement,caption:HTMLElement){
  let sequence=0,blobUrl:string|null=null,triangles:Triangle[]=[],scale=1;
  const ctx=canvas.getContext("2d")!;
  function draw(){
    if(!ctx)return;ctx.clearRect(0,0,canvas.width,canvas.height);
    const yaw=Number(slider.value)*Math.PI/180,pitch=.65;
    const polygons=triangles.map(t=>t.map(p=>{
      const x=p[0]*Math.cos(yaw)-p[1]*Math.sin(yaw),y=p[0]*Math.sin(yaw)+p[1]*Math.cos(yaw);
      return [x,y*Math.sin(pitch)-p[2]*Math.cos(pitch),y*Math.cos(pitch)+p[2]*Math.sin(pitch)];
    })).sort((a,b)=>a.reduce((s,p)=>s+p[2],0)-b.reduce((s,p)=>s+p[2],0));
    for(const points of polygons){
      const light=55+10*Math.sin(points.reduce((s,p)=>s+p[2],0)/30);
      ctx.beginPath();points.forEach((p,i)=>{const x=canvas.width/2+p[0]*scale,y=canvas.height/2+p[1]*scale;i?ctx.lineTo(x,y):ctx.moveTo(x,y);});ctx.closePath();
      ctx.fillStyle=`hsl(192 45% ${light}%)`;ctx.strokeStyle="#164e63";ctx.lineWidth=.5;ctx.fill();ctx.stroke();
    }
  }
  function clear(){sequence++;triangles=[];if(blobUrl)URL.revokeObjectURL(blobUrl);blobUrl=null;canvas.hidden=true;download.hidden=true;download.removeAttribute("href");caption.textContent="";draw();}
  async function load(url:string){
    clear();const request=sequence;
    if(!/^\/api\/parametric\/models\/[0-9a-f-]{36}$/i.test(url))throw new Error("Ruta de modelo inválida.");
    const response=await fetch(API_URL+url,{headers:{Authorization:`Bearer ${getDesignSession()?.token}`},cache:"no-store"});
    if(!response.ok)throw new Error("No se pudo cargar el modelo privado.");
    const buffer=await response.arrayBuffer();if(request!==sequence)return false;
    const mesh=parseStl(buffer);triangles=mesh.triangles;scale=330/(Math.max(...mesh.dimensions)*1.6);
    blobUrl=URL.createObjectURL(new Blob([buffer],{type:"application/octet-stream"}));download.href=blobUrl;download.download="modelo.stl";download.hidden=false;canvas.hidden=false;
    caption.textContent=`Modelo real de plantilla: ${mesh.dimensions.map(n=>Number(n.toFixed(2))).join(" × ")} mm. La vista 3D permite revisar la geometría; no certifica resistencia ni calidad de impresión.`;draw();return true;
  }
  slider.addEventListener("input",draw);window.addEventListener("pagehide",clear);
  return {load,clear};
}
