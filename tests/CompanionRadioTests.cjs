// Dependency-free interaction checks for the actual embedded companion script.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const html = fs.readFileSync(path.join(__dirname, '../companion/index.html'), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
class Element {
  constructor() { this.children = []; this.dataset = {}; this.attributes = {}; this.value = ''; this.textContent = ''; this.disabled = false; this.classList = { add() {}, remove() {} }; }
  replaceChildren(...children) { this.children = children; }
  append(...children) { this.children.push(...children); }
  setAttribute(name, value) { this.attributes[name] = value; }
  querySelectorAll() { return this.children; }
  focus() {}
}
async function fixture() {
  const elements = Object.fromEntries([...html.matchAll(/id="([^"]+)"/g)].map(m => [m[1], new Element()]));
  const requests = [];
  let disconnected = false;
  const state = { actor: 'Michael', status: 'Chill drive / 45 km/h', active: true, route: 'To Paleto Bay', messages: [], places: [],
    radio: { available: true, station: 'RADIO_16_SILVERLAKE', status: 'Radio Mirror Park', stations: [
      { id: 'RADIO_16_SILVERLAKE', name: 'Radio Mirror Park' }, { id: 'RADIO_02_POP', name: 'Non-Stop-Pop FM' }
    ] } };
  const context = vm.createContext({ document: { getElementById: id => elements[id], createElement: () => new Element(), querySelectorAll: () => [] },
    location: { hash: '#test-session', pathname: '/' }, sessionStorage: { getItem() {}, setItem() {} }, history: { replaceState() {} },
    setTimeout() {}, navigator: {}, fetch: async (url, options) => {
      if (disconnected) throw new Error('Disconnected');
      requests.push({ url, options }); return { ok: true, json: async () => url === '/api/state' ? state : { queued: true } };
    } });
  vm.runInContext(script, context);
  await new Promise(setImmediate);
  return { elements, requests, state, context, disconnect() { disconnected = true; } };
}
test('station buttons reflect the game station, send text immediately and preserve route/composer', async () => {
  const f = await fixture();
  const [mirror, pop] = f.elements.radioStations.children;
  assert.equal(mirror.textContent, 'Radio Mirror Park');
  assert.equal(mirror.attributes['aria-pressed'], 'true');
  assert.equal(pop.attributes['aria-pressed'], 'false');
  f.elements.prompt.value = 'Unsent request';
  await pop.onclick();
  const sent = f.requests.at(-1);
  assert.equal(sent.url, '/api/message');
  assert.deepEqual(JSON.parse(sent.options.body), { text: 'Play Non-Stop-Pop FM' });
  assert.equal(sent.options.headers.Authorization, 'Bearer test-session');
  assert.equal(f.elements.route.textContent, 'To Paleto Bay');
  assert.equal(f.elements.prompt.value, 'Unsent request');
  assert.equal(mirror.attributes['aria-pressed'], 'true'); // Wait for GTA's observation, not optimistic UI.
  assert.ok(!f.requests.some(r => r.url === '/api/stop'));
});
test('radio off is not takeover and observed off state is highlighted', async () => {
  const f = await fixture();
  await f.elements.radioOff.onclick();
  assert.deepEqual(JSON.parse(f.requests.at(-1).options.body), { text: 'Radio off' });
  f.state.radio.station = 'off'; f.state.radio.status = 'Radio off';
  vm.runInContext('renderRadio(' + JSON.stringify(f.state.radio) + ',false)', f.context);
  assert.equal(f.elements.radioOff.attributes['aria-pressed'], 'true');
  assert.ok(f.elements.radioStations.children.every(b => b.attributes['aria-pressed'] === 'false'));
});
test('paused, on-foot and older snapshots disable radio controls', async () => {
  const f = await fixture();
  vm.runInContext('renderRadio(' + JSON.stringify(f.state.radio) + ',true)', f.context);
  assert.equal(f.elements.radioOff.disabled, true);
  assert.ok(f.elements.radioStations.children.every(b => b.disabled));
  f.state.radio.available = false; f.state.radio.status = 'Get into a vehicle with a radio.';
  vm.runInContext('renderRadio(' + JSON.stringify(f.state.radio) + ',false)', f.context);
  assert.equal(f.elements.radioState.textContent, 'Get into a vehicle with a radio.');
  vm.runInContext('renderRadio(undefined,false)', f.context);
  assert.equal(f.elements.radioOff.disabled, true);
});
test('disconnect disables controls and does not claim a radio change', async () => {
  const f = await fixture(); f.disconnect();
  await vm.runInContext('poll()', f.context);
  assert.equal(f.elements.radioOff.disabled, true);
  assert.ok(f.elements.radioStations.children.every(b => b.disabled));
  assert.match(f.elements.radioState.textContent, /disconnected/);
  await f.elements.radioOff.onclick();
  assert.equal(f.elements.notice.textContent, 'Disconnected');
});
