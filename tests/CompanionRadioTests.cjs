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
    speedometerVisible: true, driving: {speedKmh: 60, actualSpeedKmh: 53, style: 'normal', canUse: true},
    randomMode: {active:false,status:'Random mode off.',canStart:true},
    favorites: { ready: true, canUse: true, items: [] },
    timedDrive: {active:false,canStart:true,stage:'idle',status:'Choose how long to drive before returning.',minutes:0,remainingSeconds:0},
    tour: { active: false, canStart: true, id: 'none', status: 'Choose a coastal or countryside drive.', result: 'idle', step: 0, total: 0 },
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
test('theme buttons send a single named request without optimistic progress', async () => {
  const f = await fixture();
  await f.elements.coastalTour.onclick();
  assert.deepEqual(JSON.parse(f.requests.at(-1).options.body), { text: 'Start coastal drive' });
  assert.equal(f.requests.at(-1).url, '/api/message');
  assert.equal(f.elements.coastalTour.attributes['aria-pressed'], 'false');
  assert.equal(f.elements.route.textContent, 'To Paleto Bay');
  await f.elements.countrysideTour.onclick();
  assert.deepEqual(JSON.parse(f.requests.at(-1).options.body), { text: 'Start countryside drive' });
  assert.ok(!f.requests.some(r => r.url === '/api/stop'));
});
test('observed tour progress and completion are reflected without changing the composer', async () => {
  const f = await fixture(); f.elements.prompt.value = 'Unsent request';
  Object.assign(f.state.tour, { active: true, id: 'coast', status: 'Coastal drive / 2 of 4 / Chumash', step: 2, total: 4 });
  vm.runInContext('renderTour(' + JSON.stringify(f.state.tour) + ',false)', f.context);
  assert.equal(f.elements.coastalTour.attributes['aria-pressed'], 'true');
  assert.equal(f.elements.countrysideTour.attributes['aria-pressed'], 'false');
  assert.equal(f.elements.tourState.textContent, 'Coastal drive / 2 of 4 / Chumash');
  Object.assign(f.state.tour, { active: false, status: 'Coastal drive completed.', result: 'completed' });
  vm.runInContext('renderTour(' + JSON.stringify(f.state.tour) + ',false)', f.context);
  assert.equal(f.elements.coastalTour.attributes['aria-pressed'], 'false');
  assert.equal(f.elements.tourState.textContent, 'Coastal drive completed.');
  assert.equal(f.elements.prompt.value, 'Unsent request');
});
test('tour buttons disable for pause, blocked gameplay, old snapshots and disconnection', async () => {
  const f = await fixture();
  vm.runInContext('renderTour(' + JSON.stringify(f.state.tour) + ',true)', f.context);
  assert.equal(f.elements.coastalTour.disabled, true);
  const count = f.requests.length; await f.elements.coastalTour.onclick();
  assert.equal(f.requests.length, count);
  f.state.tour.canStart = false;
  vm.runInContext('renderTour(' + JSON.stringify(f.state.tour) + ',false)', f.context);
  assert.equal(f.elements.countrysideTour.disabled, true);
  vm.runInContext('renderTour(undefined,false)', f.context);
  assert.equal(f.elements.coastalTour.disabled, true);
  f.disconnect(); await vm.runInContext('poll()', f.context);
  assert.match(f.elements.tourState.textContent, /disconnected/);
});

test('timed button sends minutes plus return intent, preserving composer and observed state', async () => {
  const f = await fixture(); f.elements.durationMinutes.value = '20'; f.elements.prompt.value = 'Unsent request';
  await f.elements.timedStart.onclick();
  assert.deepEqual(JSON.parse(f.requests.at(-1).options.body), {text:'Drive for 20 minutes then return to the starting point'});
  assert.equal(f.requests.at(-1).url, '/api/message');
  assert.equal(f.elements.timedStart.attributes['aria-pressed'], 'false');
  assert.equal(f.elements.timedState.textContent, f.state.timedDrive.status);
  assert.equal(f.elements.prompt.value, 'Unsent request');
  assert.ok(!f.requests.some(r=>r.url==='/api/stop'));
});
test('invalid, fractional or missing duration does not send any movement', async () => {
  const f = await fixture(); const count = f.requests.length;
  for (const value of ['', '0', '-5', '1.5', '121', 'garbage']) {
    f.elements.durationMinutes.value = value; await f.elements.timedStart.onclick();
    assert.equal(f.requests.length, count);
    assert.match(f.elements.notice.textContent, /1 to 120 whole minutes/);
  }
});
test('countdown, return and completion use GTA snapshots without overwriting edited duration', async () => {
  const f = await fixture(); f.elements.durationMinutes.value = '30';
  for(const [stage,active,status] of [['cruising',true,'Timed drive / 19:30 until return'], ['returning',true,'Timed drive / returning to the starting point'], ['completed',false,'Timed drive completed. Back near the starting point.']]) {
    Object.assign(f.state.timedDrive, {stage,active,status});
    vm.runInContext('renderTimed('+JSON.stringify(f.state.timedDrive)+',false)', f.context);
    assert.equal(f.elements.timedState.textContent, status);
    assert.equal(f.elements.timedStart.attributes['aria-pressed'], String(active));
    assert.equal(f.elements.durationMinutes.value, '30');
  }
});
test('timed controls cannot start on pause, blocked gameplay, older snapshots or disconnect', async () => {
  const f = await fixture();
  vm.runInContext('renderTimed('+JSON.stringify(f.state.timedDrive)+',true)', f.context);
  assert.equal(f.elements.timedStart.disabled, true); assert.equal(f.elements.durationMinutes.disabled, true);
  const count=f.requests.length; await f.elements.timedStart.onclick(); assert.equal(f.requests.length,count);
  f.state.timedDrive.canStart=false;
  vm.runInContext('renderTimed('+JSON.stringify(f.state.timedDrive)+',false)', f.context);
  assert.equal(f.elements.timedStart.disabled,true);
  vm.runInContext('renderTimed(undefined,false)', f.context); assert.equal(f.elements.timedStart.disabled,true);
  f.disconnect(); await vm.runInContext('poll()',f.context);
  assert.match(f.elements.timedState.textContent,/disconnected/); assert.equal(f.elements.timedStart.disabled,true);
});

test('driving buttons update speed and traffic style without stop or composer changes', async () => {
  const f = await fixture();f.elements.prompt.value='Unsent request';
  f.elements.targetSpeed.value='135';await f.elements.applySpeed.onclick();
  assert.deepEqual(JSON.parse(f.requests.at(-1).options.body),{text:'Set speed to 135 km/h'});
  await f.elements.rushDriving.onclick();assert.deepEqual(JSON.parse(f.requests.at(-1).options.body),{text:'Drive fast'});
  await f.elements.normalDriving.onclick();assert.deepEqual(JSON.parse(f.requests.at(-1).options.body),{text:'Normal driving'});
  assert.equal(f.elements.prompt.value,'Unsent request');assert.equal(f.elements.route.textContent,'To Paleto Bay');
  assert.ok(!f.requests.some(r=>r.url==='/api/stop'));
});
test('invalid speed does not queue a request', async () => {
  const f = await fixture();const before=f.requests.length;
  for(const value of ['0','161','90.5','invalid']){f.elements.targetSpeed.value=value;await f.elements.applySpeed.onclick();}
  assert.equal(f.requests.length,before);assert.match(f.elements.notice.textContent,/15 to 160/);
});
test('speed HUD follows observed state independently of task HUD', async () => {
  const f = await fixture();await f.elements.speedometer.onclick();
  assert.deepEqual(JSON.parse(f.requests.at(-1).options.body),{text:'Hide speedometer'});
  assert.equal(f.elements.speedometer.textContent,'Vehicle speed: ON');
  f.state.speedometerVisible=false;f.state.taskHudVisible=false;vm.runInContext('render('+JSON.stringify(f.state)+')',f.context);
  assert.equal(f.elements.speedometer.textContent,'Vehicle speed: OFF');assert.equal(f.elements.taskHud.textContent,'Show task HUD');
  await f.elements.speedometer.onclick();assert.deepEqual(JSON.parse(f.requests.at(-1).options.body),{text:'Show speedometer'});
  assert.match(f.elements.drivingState.textContent,/Actual: 53 km\/h/);
});
test('random mode starts and stops through local text commands and observed state', async () => {
  const f = await fixture();assert.equal(f.elements.randomOff.disabled,true);await f.elements.randomOn.onclick();
  assert.deepEqual(JSON.parse(f.requests.at(-1).options.body),{text:'Start random mode'});
  f.state.randomMode.active=true;f.state.randomMode.status='Random mode / visiting Mirror Park';vm.runInContext('renderVibe('+JSON.stringify(f.state)+')',f.context);
  assert.equal(f.elements.randomOff.disabled,false);assert.equal(f.elements.randomOn.attributes['aria-pressed'],'true');
  await f.elements.randomOff.onclick();assert.deepEqual(JSON.parse(f.requests.at(-1).options.body),{text:'Stop random mode'});
});
test('pause, mission state, disconnect and older versions disable new controls', async () => {
  const f = await fixture();f.state.paused=true;vm.runInContext('renderVibe('+JSON.stringify(f.state)+')',f.context);
  let before=f.requests.length;await f.elements.rushDriving.onclick();await f.elements.randomOn.onclick();assert.equal(f.requests.length,before);
  f.state.paused=false;f.state.driving.canUse=false;f.state.randomMode.canStart=false;vm.runInContext('renderVibe('+JSON.stringify(f.state)+')',f.context);
  assert.equal(f.elements.applySpeed.disabled,true);assert.equal(f.elements.randomOn.disabled,true);
  vm.runInContext('renderVibe({})',f.context);assert.equal(f.elements.speedometer.disabled,true);
  f.disconnect();await vm.runInContext('poll()',f.context);assert.equal(f.elements.rushDriving.disabled,true);assert.equal(f.elements.randomOff.disabled,true);
});
