import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { stripTypeScriptTypes } from 'node:module';
import vm from 'node:vm';
const source = stripTypeScriptTypes(readFileSync(new URL('../src/lib/visual-proposals.ts', import.meta.url), 'utf8').replace(/^import .*;\r?\n/, ''));
async function setup(enabled = true) {
  const nodes = new Map(), listeners = new Map(), stored = new Map();
  const element = () => ({value: '', options: [], classList: {add(){}, remove(){}, toggle(){}},
    addEventListener(name, fn){this[name] = fn;}, replaceChildren(...items){this.options = items;},
    add(item){this.options.push(item);}, append(){}, setAttribute(){}, removeAttribute(){}});
  const get = id => { if (!nodes.has(id)) nodes.set(id, element()); return nodes.get(id); };
  let posts = 0;
  const context = vm.createContext({
    document: {querySelector: get, createElement: element, addEventListener: (n,f) => listeners.set(n,f), dispatchEvent(){}},
    window: {addEventListener(){}}, Option: function(text,value){return {text,value};},
    sessionStorage: {getItem: k=>stored.get(k), setItem: (k,v)=>stored.set(k,v), removeItem:k=>stored.delete(k)},
    getDesignSession: ()=>({user:{id:1},token:'test'}), API_URL:'',
    crypto: {randomUUID:()=> 'request-id'}, CustomEvent: function(){}, URL, setTimeout, clearTimeout,
    apiFetch: async (_url, options) => {
      if(options?.method === 'POST'){posts++; throw new Error('Proveedor temporalmente no disponible');}
      return {enabled, supportsEditing:false, fingerprint:'brief', canGenerate:enabled,
        reason:enabled ? 'Lista para generar' : 'La generación de imágenes no está disponible por ahora.', proposals:[]};
    }
  });
  // A marker from the old implementation must not permanently suppress generation.
  stored.set('andeth-preview-conversation-brief','requested');
  vm.runInContext(source,context);
  listeners.get('design:conversation')({detail:{id:'conversation',busy:false}});
  const flush = async()=>{for(let i=0;i<15;i++) await Promise.resolve();};
  await flush();
  return {get, flush, posts:()=>posts};
}
test('automatic preview retries on explicit refresh after failure, without a retry loop', async()=>{
  const app = await setup();
  assert.equal(app.posts(),1);
  assert.match(app.get('#visual-status').textContent,/Proveedor/);
  await app.flush(); assert.equal(app.posts(),1);
  app.get('#visual-refresh').click(); await app.flush();
  assert.equal(app.posts(),2);
});
test('disabled generation exposes its reason in the empty preview and makes no paid request', async()=>{
  const app = await setup(false);
  assert.equal(app.posts(),0);
  assert.match(app.get('#visual-empty').textContent,/no está disponible/);
  assert.equal(app.get('#visual-generate').disabled,true);
});
