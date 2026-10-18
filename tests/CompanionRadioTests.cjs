// Dependency-free interaction checks for the actual embedded companion script.
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const html = fs.readFileSync(path.join(__dirname, '../companion/index.html'), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
class Element {
  constructor(tag = 'div') { this.tagName = tag; this.style = {}; this.children = []; this.dataset = {}; this.attributes = {}; this.value = ''; this.textContent = ''; this.disabled = false; this.classList = { add() {}, remove() {} }; }
  replaceChildren(...children) { this.children = children; }
  append(...children) { this.children.push(...children); }
  setAttribute(name, value) { this.attributes[name] = value; }
  querySelectorAll(tag) { return this.children.flatMap(child => [...(child.tagName === tag ? [child] : []), ...child.querySelectorAll(tag)]); }
  focus() {}
}
async function fixture() {
  const elements = Object.fromEntries([...html.matchAll(/id="([^"]+)"/g)].map(m => [m[1], new Element()]));
  const requests = [];
  let disconnected = false;
  const state = { actor: 'Michael', status: 'Chill drive / 45 km/h', active: true, route: 'To Paleto Bay', messages: [], places: [],
    favorites: { ready: true, canUse: true, items: [] },
    radio: { available: true, station: 'RADIO_16_SILVERLAKE', status: 'Radio Mirror Park', stations: [
      { id: 'RADIO_16_SILVERLAKE', name: 'Radio Mirror Park' }, { id: 'RADIO_02_POP', name: 'Non-Stop-Pop FM' }
    ] } };
  const context = vm.createContext({ document: { getElementById: id => elements[id], createElement: tag => new Element(tag), querySelectorAll: () => [] },
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
test('favorite save sends only the name and waits for actual GTA confirmation', async () => {
  const f = await fixture();
  f.elements.favoriteName.value = '  Casa sul lago  ';
  f.elements.prompt.value = 'Unsent request';
  await f.elements.saveFavorite.onclick();
  const sent = f.requests.at(-1);
  assert.equal(sent.url, '/api/favorites');
  assert.deepEqual(JSON.parse(sent.options.body), { action: 'save', name: 'Casa sul lago' });
  assert.equal(f.elements.favorites.children.length, 0); // No optimistic/fictional saved position.
  assert.equal(f.elements.favoriteName.value, '  Casa sul lago  ');
  assert.equal(f.elements.prompt.value, 'Unsent request');
  assert.equal(f.elements.route.textContent, 'To Paleto Bay');
  assert.ok(!f.requests.some(r => r.url === '/api/stop'));
});
test('observed favorites render safely and drive/walk buttons send stable IDs', async () => {
  const f = await fixture();
  const place = { id: 'fav_0123456789abcdef0123456789abcdef', name: "Trevor's spot" };
  f.state.favorites.items = [place];
  vm.runInContext('renderFavorites(' + JSON.stringify(f.state.favorites) + ',false)', f.context);
  assert.equal(f.elements.favorites.children[0].children[0].textContent, place.name);
  const [drive, walk] = f.elements.favorites.querySelectorAll('button');
  await drive.onclick();
  assert.deepEqual(JSON.parse(f.requests.at(-1).options.body), { action: 'go', id: place.id, mode: 'drive' });
  await walk.onclick();
  assert.deepEqual(JSON.parse(f.requests.at(-1).options.body), { action: 'go', id: place.id, mode: 'walk' });
  assert.match(f.elements.notice.textContent, /replaces your current movement/);
});
test('favorites disable on pause, bad storage, old snapshots and disconnection', async () => {
  const f = await fixture();
  vm.runInContext('renderFavorites(' + JSON.stringify(f.state.favorites) + ',true)', f.context);
  assert.equal(f.elements.saveFavorite.disabled, true);
  const count = f.requests.length;
  f.elements.favoriteName.value = 'No save while paused';
  await f.elements.saveFavorite.onclick();
  assert.equal(f.requests.length, count);
  f.state.favorites.ready = false;
  vm.runInContext('renderFavorites(' + JSON.stringify(f.state.favorites) + ',false)', f.context);
  assert.match(f.elements.favoriteState.textContent, /could not be loaded/);
  assert.equal(f.elements.saveFavorite.disabled, true);
  vm.runInContext('renderFavorites(undefined,false)', f.context);
  assert.equal(f.elements.saveFavorite.disabled, true);
  f.disconnect(); await vm.runInContext('poll()', f.context);
  assert.match(f.elements.favoriteState.textContent, /disconnected/);
});
test('blank favorite name is rejected before sending a request', async () => {
  const f = await fixture(); const count = f.requests.length;
  f.elements.favoriteName.value = '   '; await f.elements.saveFavorite.onclick();
  assert.equal(f.requests.length, count);
  assert.match(f.elements.notice.textContent, /Enter a name/);
});
