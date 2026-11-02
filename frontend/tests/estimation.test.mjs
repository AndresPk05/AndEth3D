import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {stripTypeScriptTypes} from 'node:module';
const source=readFileSync(new URL('../src/lib/estimate-format.ts',import.meta.url),'utf8');
const {estimateSummary}=await import('data:text/javascript;base64,'+Buffer.from(stripTypeScriptTypes(source)).toString('base64'));
test('uncalibrated estimate shows review message without a fabricated price',()=>{
  assert.equal(estimateSummary({status:'ReviewRequired',message:'Revisión manual requerida'}),'Revisión manual requerida');
});
test('indicative summary labels quantities, range, time and assumptions',()=>{
  const text=estimateSummary({status:'Indicative',quantity:2,averageTotalCop:60000,totalPriceCop:{min:45000,max:75000},unitPriceCop:{min:22500,max:37500},gramsPerUnit:{min:75,max:125},machineHoursPerUnit:{min:1.5,max:2.5},tariffVersion:4,calibrationVersion:2,method:'observed-envelope-v1',assumptions:['No incluye envío.']});
  assert.match(text,/orientativo/);assert.match(text,/2 unidades/);assert.match(text,/75 – 125 g/);assert.match(text,/no es plazo de entrega/);assert.match(text,/No incluye envío/);assert.doesNotMatch(text,/undefined|NaN/);
});
test('estimation browser modules use supported TypeScript syntax',()=>{
  for(const file of ['calibration','design-estimate','design-quote'])assert.doesNotThrow(()=>stripTypeScriptTypes(readFileSync(new URL(`../src/lib/${file}.ts`,import.meta.url),'utf8')));
});
