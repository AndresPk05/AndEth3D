import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { stripTypeScriptTypes } from 'node:module';
const source = readFileSync(new URL('../src/lib/api.ts', import.meta.url), 'utf8').replaceAll('import.meta.env', '({ DEV: true })');
const api = await import('data:text/javascript;base64,' + Buffer.from(stripTypeScriptTypes(source)).toString('base64'));
const storage = () => { const values = new Map(); return { getItem: k => values.get(k), setItem: (k,v) => values.set(k,v), removeItem: k => values.delete(k) }; };
globalThis.localStorage = storage();
globalThis.sessionStorage = storage();
globalThis.window = { location: { origin: 'https://andeth.example', pathname: '/pedidos', assign: () => {} } };
test('HTML and attribute injection are escaped', () => {
 assert.equal(api.escapeHtml(`<img src=x onerror="alert('x')">&`), '&lt;img src=x onerror=&quot;alert(&#39;x&#39;)&quot;&gt;&amp;');
});
test('redirects reject external, protocol-relative and backslash destinations', () => {
 for (const input of ['https://evil.example', '//evil.example', '/\\evil.example', 'javascript:alert(1)', '/\nevil']) assert.equal(api.safeNext(input, '/cotizar'), '/cotizar');
 assert.equal(api.safeNext('/pedidos?x=1#status', '/'), '/pedidos?x=1#status');
});
test('public file URLs reject scripts, external hosts and private files', () => {
 for (const path of ['javascript:alert(1)', 'https://evil.example/a.png', '/private/a.pdf', '/uploads/../a.png']) assert.equal(api.fileUrl(path), '');
 assert.ok(api.fileUrl('/uploads/' + 'a'.repeat(32) + '.png').endsWith('.png'));
});
test('sessions expire and old persistent credentials are discarded', () => {
 const token = exp => 'header.' + Buffer.from(JSON.stringify({ exp })).toString('base64url') + '.signature';
 localStorage.setItem('andeth3d_session', 'old');
 api.saveSession({ token: token(Date.now()/1000 + 60), user: { id: 1, role: 'Client' } });
 assert.equal(api.getSession().user.id, 1);
 assert.equal(localStorage.getItem('andeth3d_session'), undefined);
 api.saveSession({ token: token(1), user: { id: 1 } });
 assert.equal(api.getSession(), null);
 sessionStorage.setItem('andeth3d_session', '{broken');
 assert.equal(api.getSession(), null);
});
test('401 clears the session and redirects to login', async () => {
 api.saveSession({ token: 'h.' + Buffer.from(JSON.stringify({ exp: Date.now()/1000 + 60 })).toString('base64url') + '.s', user: { id: 1 } });
 let target; window.location.assign = value => { target = value; };
 globalThis.fetch = async () => ({ status: 401, ok: false, json: async () => null });
 await assert.rejects(api.apiFetch('/api/orders'));
 assert.equal(api.getSession(), null);
 assert.equal(target, '/login?next=%2Fpedidos');
});
