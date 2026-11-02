import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { stripTypeScriptTypes } from 'node:module';

const summarySource = readFileSync(new URL('../src/lib/manufacturing-summary.ts', import.meta.url), 'utf8');
const { manufacturingSummary } = await import('data:text/javascript;base64,' + Buffer.from(stripTypeScriptTypes(summarySource)).toString('base64'));
test('historical summary handles legacy quotes and zero rates', () => {
  assert.equal(manufacturingSummary({ message: 'Sin condiciones previas' }), 'Sin condiciones previas');
  const summary = manufacturingSummary({version: 1, configuration: {
    printers: [], materials: [], categories: [],
    pricing: {machineHourCop: 0, designHourCop: 0, finishingHourCop: 0, minimumOrderCop: 0, grossMargin: 0.4}
  }});
  assert.match(summary, /Versión 1/);
  assert.match(summary, /Margen bruto: 40%/);
  assert.doesNotMatch(summary, /NaN|undefined/);
});
test('manufacturing admin TypeScript can be stripped for browser execution', () => {
  const source = readFileSync(new URL('../src/lib/manufacturing-admin.ts', import.meta.url), 'utf8');
  assert.doesNotThrow(() => stripTypeScriptTypes(source));
});
