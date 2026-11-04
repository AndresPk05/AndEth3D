import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { stripTypeScriptTypes } from 'node:module';
import vm from 'node:vm';
const source = stripTypeScriptTypes(readFileSync(new URL('../src/lib/visual-proposals.ts', import.meta.url), 'utf8').replace(/^import .*;\r?\n/, ''));
async function setup(enabled = true, proposals = [], supportsEditing = false) {
  const nodes = new Map(), listeners = new Map(), stored = new Map();
  const element = () => ({value: '', options: [], classList: {add(){}, remove(){}, toggle(){}},
    addEventListener(name, fn){this[name] = fn;}, replaceChildren(...items){this.options = items;},
    add(item){this.options.push(item);}, append(item){this.options.push(item);}, setAttribute(name,value){this[name] = value;}, removeAttribute(){}});
  const get = id => { if (!nodes.has(id)) nodes.set(id, element()); return nodes.get(id); };
  let posts = 0;
  const requests = [];
  const context = vm.createContext({
    document: {querySelector: get, createElement: element, addEventListener: (n,f) => listeners.set(n,f), dispatchEvent(){}},
    window: {addEventListener(){}}, Option: function(text,value){return {text,value};},
    sessionStorage: {getItem: k=>stored.get(k), setItem: (k,v)=>stored.set(k,v), removeItem:k=>stored.delete(k)},
    getDesignSession: ()=>({user:{id:1},token:'test'}), API_URL:'',
    crypto: {randomUUID:()=> 'request-id'}, CustomEvent: function(){}, URL, setTimeout, clearTimeout,
    fetch: async (_url, options) => {
      assert.equal(options.headers.Authorization, 'Bearer test');
      return {ok:true, headers:new Headers({'Content-Type':'image/png'}), blob:async()=>new Blob(['preview'],{type:'image/png'})};
    },
    apiFetch: async (_url, options) => {
      if(options?.method === 'POST'){requests.push(JSON.parse(options.body)); posts++; throw new Error('Proveedor temporalmente no disponible');}
      return {enabled, supportsEditing, fingerprint:'brief', canGenerate:enabled,
        reason:enabled ? 'Lista para generar' : 'La generación de imágenes no está disponible por ahora.', proposals};
    }
  });
  // A marker from the old implementation must not permanently suppress generation.
  stored.set('andeth-preview-conversation-brief','requested');
  vm.runInContext(source,context);
  listeners.get('design:conversation')({detail:{id:'conversation',busy:false}});
  const flush = async()=>{for(let i=0;i<15;i++) await Promise.resolve();};
  await flush();
  return {get, flush, requests, posts:()=>posts};
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

test('a completed preview opens the private image without requesting a second generation', async()=>{
  const app = await setup(true, [{id:'first', number:1, status:'Completed', outdated:false,
    imageUrl:'/api/chat/conversations/conversation/visuals/first/image', message:'Lista'}]);
  assert.match(app.get('#visual-image').src, /^blob:/);
  assert.equal(app.posts(),0);
  URL.revokeObjectURL(app.get('#visual-image').src);
});


test('switching versions shows the selected image without generating another proposal', async () => {
  const app = await setup(true, [
    {id:'first', number:1, status:'Completed', outdated:true, imageUrl:'/first', message:'Lista'},
    {id:'second', number:2, status:'Completed', outdated:false, imageUrl:'/second', message:'Lista'}
  ]);
  assert.equal(app.get('#visual-versions').hidden, false);
  assert.match(app.get('#visual-label').textContent, /Versión 2/);
  const history = app.get('#visual-history');
  assert.equal(history.options[0]['aria-pressed'], 'true');
  history.options[1].click();
  await app.flush();
  assert.match(app.get('#visual-label').textContent, /Versión 1/);
  assert.equal(history.options[1]['aria-pressed'], 'true');
  assert.equal(history.options[0]['aria-pressed'], 'false');
  assert.equal(app.posts(), 0);
  URL.revokeObjectURL(app.get('#visual-image').src);
});


test('a new object never reuses an incompatible previous image', async () => {
  const app = await setup(true, [{id:'plane', number:1, status:'Completed', outdated:true,
    canUseAsReference:false, imageUrl:'/plane', message:'Lista'}], true);
  assert.equal(app.requests.length, 1);
  assert.equal(app.requests[0].parentProposalId, null);
  URL.revokeObjectURL(app.get('#visual-image').src);
});

test('an edit of the same object can reuse its compatible image', async () => {
  const app = await setup(true, [{id:'plane', number:1, status:'Completed', outdated:true,
    canUseAsReference:true, imageUrl:'/plane', message:'Lista'}], true);
  assert.equal(app.requests.length, 1);
  assert.equal(app.requests[0].parentProposalId, 'plane');
  URL.revokeObjectURL(app.get('#visual-image').src);
});
