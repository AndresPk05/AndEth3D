export type Point = [number,number,number];
export type Triangle = [Point,Point,Point];
export function parseStl(buffer:ArrayBuffer):{triangles:Triangle[];dimensions:Point} {
  if(buffer.byteLength<84||buffer.byteLength>1_000_000)throw new Error("Modelo fuera del límite de vista previa.");
  const view=new DataView(buffer),count=view.getUint32(80,true);
  if(count<1||count>15000||84+50*count!==buffer.byteLength)throw new Error("Modelo inválido.");
  const min:Point=[Infinity,Infinity,Infinity],max:Point=[-Infinity,-Infinity,-Infinity];
  const triangles:Triangle[]=[];
  for(let i=0;i<count;i++){
    const points:Point[]=[];
    for(let j=0;j<3;j++){
      const point:Point=[0,0,0];
      for(let k=0;k<3;k++){
        const n=view.getFloat32(84+i*50+12+j*12+k*4,true);
        if(!Number.isFinite(n)||Math.abs(n)>5000)throw new Error("Coordenada inválida.");
        point[k]=n;min[k]=Math.min(min[k],n);max[k]=Math.max(max[k],n);
      }points.push(point);
    }triangles.push(points as Triangle);
  }
  const dimensions=max.map((n,i)=>n-min[i]) as Point;
  if(dimensions.some(n=>n<=0))throw new Error("Modelo sin volumen.");
  for(const triangle of triangles)for(const p of triangle)for(let k=0;k<3;k++)p[k]-=(max[k]+min[k])/2;
  return {triangles,dimensions};
}
