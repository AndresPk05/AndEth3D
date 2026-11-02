import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {stripTypeScriptTypes} from 'node:module';
const source=readFileSync(new URL('../src/lib/operations-format.ts',import.meta.url),'utf8');
const {metric}=await import('data:text/javascript;base64,'+Buffer.from(stripTypeScriptTypes(source)).toString('base64'));
test('operations distinguish no data from measured zero and negative margins',()=>{
 assert.equal(metric(null,'%'),'Sin datos');assert.equal(metric(undefined),'Sin datos');
 assert.equal(metric(0,'%'),'0%');assert.match(metric(-10,'%'),/-10%/);
});
test('operations browser module syntax is supported',()=>{
 assert.doesNotThrow(()=>stripTypeScriptTypes(readFileSync(new URL('../src/lib/operations.ts',import.meta.url),'utf8')));
});
