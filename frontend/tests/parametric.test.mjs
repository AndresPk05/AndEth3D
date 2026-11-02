import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {stripTypeScriptTypes} from 'node:module';
const source=readFileSync(new URL('../src/lib/stl-geometry.ts',import.meta.url),'utf8');
const {parseStl}=await import('data:text/javascript;base64,'+Buffer.from(stripTypeScriptTypes(source)).toString('base64'));
function fixture(){
 const buffer=new ArrayBuffer(134),v=new DataView(buffer);v.setUint32(80,1,true);
 const points=[[0,0,0],[10,0,0],[0,20,30]];
 points.forEach((p,j)=>p.forEach((n,k)=>v.setFloat32(96+j*12+k*4,n,true)));
 return buffer;
}
test('binary STL preview preserves dimensions and centers geometry',()=>{
 const m=parseStl(fixture());assert.deepEqual(m.dimensions,[10,20,30]);assert.deepEqual(m.triangles[0][0],[-5,-10,-15]);
});
test('preview rejects truncated, oversized and nonfinite geometry',()=>{
 assert.throws(()=>parseStl(new ArrayBuffer(50)));assert.throws(()=>parseStl(new ArrayBuffer(1_000_001)));
 const b=fixture();new DataView(b).setFloat32(96,NaN,true);assert.throws(()=>parseStl(b));
 const c=fixture();new DataView(c).setUint32(80,15001,true);assert.throws(()=>parseStl(c));
});
test('parametric browser modules use supported TypeScript syntax',()=>{
 for(const file of ['parametric-design','model-preview','slicing-admin','design-quote'])assert.doesNotThrow(()=>stripTypeScriptTypes(readFileSync(new URL(`../src/lib/${file}.ts`,import.meta.url),'utf8')));
});
