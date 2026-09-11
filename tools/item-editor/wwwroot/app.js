/* vyper-mu Item Editor — frontend. Sin frameworks ni build: se sirve tal cual. */
'use strict';

// ------------------------------------------------------------------ constantes

const GROUPS = [
  'Espadas', 'Hachas', 'Mazas y cetros', 'Lanzas', 'Arcos y ballestas', 'Báculos y sticks', 'Escudos',
  'Cascos', 'Armaduras', 'Pantalones', 'Guantes', 'Botas', 'Alas, orbes y varios', 'Pendientes, anillos y varios',
  'Pociones y joyas', 'Pergaminos',
];

// GUIDs de atributos de OpenMU (src/GameLogic/Attributes/Stats.cs). Son constantes del proyecto.
const A = {
  level: '560931ad-0901-4342-b7f4-fd2e2fcc0563',
  reqStr: '7bc30a84-5fde-490b-81a4-ffecb41ca901',
  reqAgi: 'dfe7a14f-bf1c-414f-8b77-b8fe6fd76a7d',
  reqVit: 'b7b77ff8-1833-4739-98ee-0c2dd7344f56',
  reqEne: '5ef1fcd1-0c08-4087-bfce-c655bb121cdd',
  reqCmd: 'e38a897e-ed6f-4c06-ae11-7caa7eaec5a9',
  minDmgW: '1ac59d93-6a52-4e88-8201-5f125a63b2a1',
  maxDmgW: 'ec0d70be-839c-4bad-9fc5-1ad7438c75f8',
  minDmg: '3e8d6a02-e973-4ae4-9df3-cddc3d3183b3',
  maxDmg: '8a918ea2-893a-48b2-a684-3e71526ca71f',
  defense: 'eb098c46-60d4-4ca6-bbd4-5b6270a1407b',
  shieldDef: 'f3db2083-500c-42a2-a487-b0ae68dfd331',
  defRatePvm: 'c520dd2d-1b06-4392-95ee-3c41f33e68da',
  atkSpeed: '45eeedee-c76b-40e6-a0bc-2b493e10b140',
  staffRise: 'db2f48fd-42ab-4204-b863-afee138a9d43',
  scepterRise: 'fb374862-d360-4ff0-ab88-2c170e6a9f85',
  walkSpeed: '9cddc598-e5f3-4372-9294-505455e4a40b',
  maxHp: 'a6c39a5c-295f-415e-a314-5e9f9a748d27',
  maxMana: '17cb8826-0677-4c93-a0c9-c0e3d2da7d73',
  resIce: '47235c36-41bb-44b4-8823-6fc415709f59',
  resPoison: '3d50d0b7-63a2-4da9-8855-12173eae6b39',
  resLightning: '3e339393-2d17-452e-81d9-3987947a407f',
  resFire: '9ae4d80d-5706-48b9-ad11-eac4fe088a81',
  resEarth: '4470890f-00ce-44a6-badb-203684b6014d',
  resWind: '03a29c46-7b7e-424d-8325-8390692570c3',
  resWater: '3af88672-d8db-44e1-937a-7e6484134c39',
};

// Numero de clase de OpenMU -> [familia (indice en RequireClass del cliente), paso (1 base, 2 segunda, 3 tercera), sigla]
const CLASS_MAP = {
  0: [0, 1, 'DW'], 2: [0, 2, 'SM'], 3: [0, 3, 'GM'],
  4: [1, 1, 'DK'], 6: [1, 2, 'BK'], 7: [1, 3, 'BM'],
  8: [2, 1, 'FE'], 10: [2, 2, 'ME'], 11: [2, 3, 'HE'],
  12: [3, 1, 'MG'], 13: [3, 3, 'DM'],
  16: [4, 1, 'DL'], 17: [4, 3, 'LE'],
  20: [5, 1, 'SU'], 22: [5, 2, 'BS'], 23: [5, 3, 'DiM'],
  24: [6, 1, 'RF'], 25: [6, 3, 'FM'],
};
const FAMILIES = ['Dark Wizard', 'Dark Knight', 'Elf', 'Magic Gladiator', 'Dark Lord', 'Summoner', 'Rage Fighter'];
const FAM_SHORT = ['DW', 'DK', 'ELF', 'MG', 'DL', 'SUM', 'RF'];
const AGG = ['Suma (AddRaw)', 'Multiplica', 'Suma final', 'Máximo'];
const CLIENT_SLOT_NAMES = { 0: 'Mano derecha', 1: 'Mano izquierda', 2: 'Casco', 3: 'Armadura', 4: 'Pantalón', 5: 'Guantes', 6: 'Botas', 7: 'Alas', 8: 'Mascota', 9: 'Pendiente', 10: 'Anillo', 255: 'No equipable' };

// ------------------------------------------------------------------ estado

const S = {
  status: null, meta: null,
  items: [], byId: new Map(), client: new Map(),
  attrById: new Map(), classById: new Map(), slotById: new Map(),
  sel: null, draft: null, orig: null, cdraft: null, corig: null,
  tab: 'general',
  q: '', group: null, cls: null, flags: new Set(), sort: 'gn',
  undo: [],
  restartNeeded: false,
};

const $ = (sel, root = document) => root.querySelector(sel);
const $$ = (sel, root = document) => [...root.querySelectorAll(sel)];
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const clone = (o) => JSON.parse(JSON.stringify(o));
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const gn = (g, n) => `${g}/${n}`;
const idx = (g, n) => g * 512 + n;

// ------------------------------------------------------------------ api

async function api(path, opts = {}) {
  const res = await fetch('/api' + path, {
    headers: { 'Content-Type': 'application/json' },
    ...opts,
    body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
  });
  if (res.status === 204) return null;
  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { /* texto plano */ }
  if (!res.ok) {
    throw new Error((data && (data.detail || data.title)) || text || `HTTP ${res.status}`);
  }
  return data;
}

// ------------------------------------------------------------------ carga

async function loadAll() {
  const [status, meta, items, client] = await Promise.all([
    api('/status'), api('/meta'), api('/items'), api('/client/items').catch(() => []),
  ]);
  S.status = status;
  S.meta = meta;
  S.items = items;
  S.byId = new Map(items.map((i) => [i.id, i]));
  S.client = new Map(client.map((c) => [c.index, c]));
  S.attrById = new Map(meta.attributes.map((a) => [a.id, a]));
  S.classById = new Map(meta.classes.map((c) => [c.id, c]));
  S.slotById = new Map(meta.slots.map((s) => [s.id, s]));
  meta.classes.forEach((c) => { c.map = CLASS_MAP[c.number] || [0, 0, '?']; });
}

async function refreshStatus() {
  try { S.status = await api('/status'); } catch { /* ignorar */ }
  renderPills();
}

// ------------------------------------------------------------------ mapeo servidor <-> cliente

function reqValue(item, attrId) {
  const r = item.requirements.find((x) => x.attributeId === attrId);
  return r ? r.minimumValue : 0;
}
function puValue(item, ...attrIds) {
  for (const id of attrIds) {
    const p = item.powerUps.find((x) => x.targetAttributeId === id);
    if (p) return p.baseValue;
  }
  return 0;
}
function isEquippable(item) { return !!item.itemSlotId; }
function clientSlotFor(item) {
  const slot = S.slotById.get(item.itemSlotId);
  if (!slot || !slot.rawItemSlots) return 255;
  const first = parseInt(slot.rawItemSlots.split(';')[0], 10);
  return Number.isFinite(first) ? first : 255;
}
function serverClassesToClient(item) {
  const out = [0, 0, 0, 0, 0, 0, 0];
  for (const id of item.classIds) {
    const c = S.classById.get(id);
    if (!c) continue;
    const [fam, step] = c.map;
    out[fam] = out[fam] === 0 ? step : Math.min(out[fam], step);
  }
  return out;
}
function clientClassesToServer(requireClass) {
  const ids = [];
  for (const c of S.meta.classes) {
    const [fam, step] = c.map;
    const v = requireClass[fam] || 0;
    if (v > 0 && step >= v) ids.push(c.id);
  }
  return ids;
}
const b = (v) => Math.max(0, Math.min(255, Math.round(v)));
const w = (v) => Math.max(0, Math.min(65535, Math.round(v)));

/** Registro de cliente "esperado" a partir del item del servidor, partiendo de `base` (o de un registro en blanco). */
function serverToClient(item, base) {
  const c = base ? clone(base) : blankClient(idx(item.group, item.number));
  c.index = idx(item.group, item.number);
  c.name = item.name;
  c.level = w(item.dropLevel);
  c.width = b(item.width);
  c.height = b(item.height);
  if (isEquippable(item)) {
    c.durability = b(item.durability);
    c.slot = clientSlotFor(item);
    c.requireStrength = w(reqValue(item, A.reqStr));
    c.requireDexterity = w(reqValue(item, A.reqAgi));
    c.requireEnergy = w(reqValue(item, A.reqEne));
    c.requireVitality = w(reqValue(item, A.reqVit));
    c.requireCharisma = w(reqValue(item, A.reqCmd));
    c.requireLevel = w(reqValue(item, A.level));
    c.damageMin = b(puValue(item, A.minDmgW, A.minDmg));
    c.damageMax = b(puValue(item, A.maxDmgW, A.maxDmg));
    c.defense = b(puValue(item, A.shieldDef, A.defense));
    c.successfulBlocking = b(puValue(item, A.defRatePvm));
    c.weaponSpeed = b(puValue(item, A.atkSpeed));
    c.magicPower = b(puValue(item, A.staffRise, A.scepterRise) * 2);
    c.walkSpeed = b(puValue(item, A.walkSpeed));
    c.requireClass = serverClassesToClient(item);
  }
  return c;
}

/** Aplica al item del servidor lo que dice el registro del cliente (nombre, nivel, tamaño, requisitos, stats base, clases). */
function clientToServer(item, c) {
  const it = clone(item);
  it.name = c.name;
  it.dropLevel = b(c.level);
  it.width = b(c.width);
  it.height = b(c.height);
  if (isEquippable(it)) {
    it.durability = b(c.durability);
    const setReq = (attr, v) => {
      const i = it.requirements.findIndex((r) => r.attributeId === attr);
      if (v > 0) { if (i >= 0) it.requirements[i].minimumValue = v; else it.requirements.push({ id: null, attributeId: attr, minimumValue: v }); }
      else if (i >= 0) it.requirements.splice(i, 1);
    };
    setReq(A.reqStr, c.requireStrength); setReq(A.reqAgi, c.requireDexterity); setReq(A.reqEne, c.requireEnergy);
    setReq(A.reqVit, c.requireVitality); setReq(A.reqCmd, c.requireCharisma); setReq(A.level, c.requireLevel);
    const setPu = (attrs, v, scale = 1) => {
      const i = it.powerUps.findIndex((p) => attrs.includes(p.targetAttributeId));
      if (i >= 0) { it.powerUps[i].baseValue = v * scale; }
      else if (v > 0) it.powerUps.push({ id: null, targetAttributeId: attrs[0], baseValue: v * scale, aggregateType: 0, bonusPerLevelTableId: null });
    };
    setPu([A.minDmgW, A.minDmg], c.damageMin); setPu([A.maxDmgW, A.maxDmg], c.damageMax);
    setPu([A.shieldDef, A.defense], c.defense); setPu([A.defRatePvm], c.successfulBlocking);
    setPu([A.atkSpeed], c.weaponSpeed); setPu([A.staffRise, A.scepterRise], c.magicPower, 0.5);
    it.classIds = clientClassesToServer(c.requireClass);
  }
  return it;
}

function blankClient(index) {
  return {
    index, name: '', twoHand: false, level: 0, slot: 255, skillIndex: 0, width: 1, height: 1,
    damageMin: 0, damageMax: 0, successfulBlocking: 0, defense: 0, magicDefense: 0, weaponSpeed: 0, walkSpeed: 0,
    durability: 0, magicDur: 0, magicPower: 0, requireStrength: 0, requireDexterity: 0, requireEnergy: 0,
    requireVitality: 0, requireCharisma: 0, requireLevel: 0, value: 0, zen: 0, attType: 0,
    requireClass: [0, 0, 0, 0, 0, 0, 0], resistance: [0, 0, 0, 0, 0, 0, 0, 0],
  };
}

const CMP_FIELDS = [
  ['name', 'Nombre', true], ['level', 'Nivel', true], ['width', 'Ancho', true], ['height', 'Alto', true],
  ['durability', 'Durabilidad'], ['slot', 'Slot'], ['requireLevel', 'Req. nivel'], ['requireStrength', 'Req. fuerza'],
  ['requireDexterity', 'Req. agilidad'], ['requireVitality', 'Req. vitalidad'], ['requireEnergy', 'Req. energía'],
  ['requireCharisma', 'Req. comando'], ['damageMin', 'Daño mín.'], ['damageMax', 'Daño máx.'], ['defense', 'Defensa'],
  ['successfulBlocking', 'Bloqueo'], ['weaponSpeed', 'Velocidad'], ['magicPower', 'Poder mágico'], ['walkSpeed', 'Vel. movimiento'],
  ['requireClass', 'Clases'],
];

/** Diferencias entre el registro del cliente y lo que el servidor implica. */
function diffs(item, crec) {
  if (!crec) return null;
  const expected = serverToClient(item, crec);
  const equip = isEquippable(item);
  const out = [];
  for (const [key, label, always] of CMP_FIELDS) {
    if (!always && !equip) continue;
    const a = crec[key], e = expected[key];
    if (!same(a, e)) out.push({ key, label, client: a, server: e });
  }
  return out;
}

// ------------------------------------------------------------------ helpers de presentacion

function attrName(id) { const a = S.attrById.get(id); return a ? a.designation : (id ? '(atributo desconocido)' : '—'); }
function classShort(id) { const c = S.classById.get(id); return c ? c.map[2] : '?'; }
function slotName(id) { const s = S.slotById.get(id); return s ? s.description : '—'; }
function classSummary(item) {
  if (!item.classIds.length) return '';
  const rc = serverClassesToClient(item);
  return rc.map((v, i) => v ? `${FAM_SHORT[i]}${v > 1 ? '⁺' + v : ''}` : null).filter(Boolean).join(' ');
}
function fmtClasses(arr) { return arr.map((v, i) => v ? `${FAM_SHORT[i]}:${v}` : null).filter(Boolean).join(' ') || '—'; }
function fmtVal(key, v) {
  if (key === 'requireClass') return fmtClasses(v);
  if (key === 'slot') return `${v} (${CLIENT_SLOT_NAMES[v] || '?'})`;
  return String(v);
}

function toast(msg, kind = '', ms = 3200) {
  const el = document.createElement('div');
  el.className = 'toast ' + kind;
  el.textContent = msg;
  $('#toasts').appendChild(el);
  setTimeout(() => el.remove(), ms);
}

function modal({ title, body, okText = 'Aceptar', okClass = 'primary', onOk, onOpen }) {
  const root = $('#modal-root');
  root.innerHTML = `<div class="modal-bg"><div class="modal" role="dialog"><h3>${esc(title)}</h3>${body}
    <div class="actions"><button class="btn" data-act="cancel">Cancelar</button><button class="btn ${okClass}" data-act="ok">${esc(okText)}</button></div></div></div>`;
  const close = () => { root.innerHTML = ''; };
  root.querySelector('[data-act=cancel]').onclick = close;
  root.querySelector('.modal-bg').addEventListener('click', (e) => { if (e.target.classList.contains('modal-bg')) close(); });
  root.querySelector('[data-act=ok]').onclick = async () => {
    const btn = root.querySelector('[data-act=ok]');
    btn.disabled = true;
    try { if (await onOk(root) !== false) close(); } catch (e) { toast(e.message, 'bad'); btn.disabled = false; }
  };
  root.addEventListener('keydown', (e) => { if (e.key === 'Escape') close(); });
  if (onOpen) onOpen(root);
  const first = root.querySelector('input,select');
  if (first) first.focus();
}

// ------------------------------------------------------------------ combobox

function combo(container, { items, value, placeholder = 'Buscar…', onChange }) {
  container.classList.add('combo');
  const input = document.createElement('input');
  input.placeholder = placeholder;
  input.autocomplete = 'off';
  const menu = document.createElement('div');
  menu.className = 'menu';
  menu.hidden = true;
  container.append(input, menu);
  const labelOf = (id) => { const it = items.find((x) => x.id === id); return it ? it.label : ''; };
  let current = value;
  let hl = 0;
  input.value = labelOf(current);

  const render = () => {
    const q = input.value.trim().toLowerCase();
    const list = (q ? items.filter((x) => x.label.toLowerCase().includes(q) || (x.sub || '').toLowerCase().includes(q)) : items).slice(0, 60);
    menu.innerHTML = list.map((x, i) => `<div data-id="${esc(x.id)}" class="${i === hl ? 'hl' : ''}">${esc(x.label)}${x.sub ? `<small>${esc(x.sub)}</small>` : ''}</div>`).join('') || '<div class="muted">Sin resultados</div>';
    menu.hidden = false;
  };
  const pick = (id) => { current = id; input.value = labelOf(id); menu.hidden = true; onChange(id); };
  input.addEventListener('focus', () => { input.select(); hl = 0; render(); });
  input.addEventListener('input', () => { hl = 0; render(); });
  input.addEventListener('keydown', (e) => {
    const rows = $$('div[data-id]', menu);
    if (e.key === 'ArrowDown') { hl = Math.min(hl + 1, rows.length - 1); render(); e.preventDefault(); }
    else if (e.key === 'ArrowUp') { hl = Math.max(hl - 1, 0); render(); e.preventDefault(); }
    else if (e.key === 'Enter') { const r = $$('div[data-id]', menu)[hl]; if (r) pick(r.dataset.id); e.preventDefault(); }
    else if (e.key === 'Escape') { menu.hidden = true; input.value = labelOf(current); }
  });
  input.addEventListener('blur', () => setTimeout(() => { menu.hidden = true; input.value = labelOf(current); }, 150));
  menu.addEventListener('mousedown', (e) => { const r = e.target.closest('[data-id]'); if (r) { e.preventDefault(); pick(r.dataset.id); } });
}

// ------------------------------------------------------------------ render: barra superior / sidebar

function renderPills() {
  const st = S.status || {};
  const pills = [];
  pills.push(`<span class="pill ${st.dbOk ? 'ok' : 'bad'}" title="${esc(st.dbError || 'PostgreSQL openmu')}"><span class="dot"></span>Base</span>`);
  pills.push(`<span class="pill ${st.clientFileOk ? 'ok' : 'warn'}" title="${esc(st.clientFilePath || '')}"><span class="dot"></span>Cliente${st.clientFileOk ? '' : ' (sin Item_eng.bmd)'}</span>`);
  if (S.restartNeeded && st.serverRunning) {
    pills.push(`<span class="pill warn" title="OpenMU carga la configuración al arrancar. Corré scripts\\stop.ps1 y scripts\\start.ps1"><span class="dot"></span>Reiniciá el server para aplicar</span>`);
  } else {
    pills.push(`<span class="pill ${st.serverRunning ? 'ok' : ''}"><span class="dot"></span>OpenMU ${st.serverRunning ? 'corriendo' : 'apagado'}</span>`);
  }
  $('#pills').innerHTML = pills.join('');
  $('#btn-undo').disabled = S.undo.length === 0;
  $('#btn-undo').title = S.undo.length ? `Deshacer: ${S.undo[S.undo.length - 1].label}` : 'Nada para deshacer';
}

function renderGroups() {
  const counts = new Array(16).fill(0);
  S.items.forEach((i) => counts[i.group]++);
  const rows = [`<button class="group-item ${S.group === null ? 'active' : ''}" data-g="all"><span class="n">∗</span>Todos<span class="count">${S.items.length}</span></button>`];
  GROUPS.forEach((name, g) => {
    rows.push(`<button class="group-item ${S.group === g ? 'active' : ''}" data-g="${g}"><span class="n">${g}</span>${esc(name)}<span class="count">${counts[g]}</span></button>`);
  });
  $('#groups').innerHTML = rows.join('');
  $$('#groups .group-item').forEach((el) => el.onclick = () => { S.group = el.dataset.g === 'all' ? null : +el.dataset.g; renderGroups(); renderList(); });

  $('#class-chips').innerHTML = FAM_SHORT.map((s, i) => `<button class="chip ${S.cls === i ? 'active' : ''}" data-f="${i}" title="${esc(FAMILIES[i])}">${s}</button>`).join('');
  $$('#class-chips .chip').forEach((el) => el.onclick = () => { S.cls = S.cls === +el.dataset.f ? null : +el.dataset.f; renderGroups(); renderList(); });
  $$('#flag-chips .chip').forEach((el) => {
    el.classList.toggle('active', S.flags.has(el.dataset.flag));
    el.onclick = () => { S.flags.has(el.dataset.flag) ? S.flags.delete(el.dataset.flag) : S.flags.add(el.dataset.flag); renderGroups(); renderList(); };
  });
}

// ------------------------------------------------------------------ render: lista

function filtered() {
  const q = S.q.trim().toLowerCase();
  let m = q.match(/^(\d{1,2})\s*[\/ ,:-]\s*(\d{1,3})$/);
  let list = S.items.filter((i) => {
    if (S.group !== null && i.group !== S.group) return false;
    if (S.cls !== null && !serverClassesToClient(i)[S.cls]) return false;
    if (S.flags.has('drops') && !i.dropsFromMonsters) return false;
    const crec = S.client.get(idx(i.group, i.number));
    if (S.flags.has('noclient') && crec) return false;
    if (S.flags.has('desync') && !(crec && diffs(i, crec).length)) return false;
    if (m) return i.group === +m[1] && i.number === +m[2];
    if (/^\d+$/.test(q)) { const n = +q; return i.number === n || idx(i.group, i.number) === n || i.dropLevel === n; }
    if (q) {
      const crecName = crec ? crec.name.toLowerCase() : '';
      return i.name.toLowerCase().includes(q) || crecName.includes(q);
    }
    return true;
  });
  if (S.sort === 'name') list.sort((a, b2) => a.name.localeCompare(b2.name));
  else if (S.sort === 'level') list.sort((a, b2) => a.dropLevel - b2.dropLevel || a.group - b2.group || a.number - b2.number);
  return list;
}

function renderList() {
  const list = filtered();
  $('#list-count').innerHTML = `<strong>${list.length}</strong> de ${S.items.length} items`;
  if (!list.length) { $('#list').innerHTML = '<div class="empty">Nada que mostrar con esos filtros.</div>'; return; }
  $('#list').innerHTML = list.map((i) => {
    const crec = S.client.get(idx(i.group, i.number));
    const d = crec ? diffs(i, crec) : null;
    const tags = [];
    if (!crec) tags.push('<span class="tag warn" title="No hay registro en Item_eng.bmd: el cliente no lo muestra bien">sin cliente</span>');
    else if (d.length) tags.push(`<span class="tag warn" title="${esc(d.map((x) => x.label).join(', '))}">≠ ${d.length}</span>`);
    if (i.dropsFromMonsters) tags.push('<span class="tag info" title="Dropea de monstruos">drop</span>');
    if (i.maximumSockets) tags.push(`<span class="tag" title="Sockets">${i.maximumSockets}◆</span>`);
    return `<div class="row ${i.id === S.sel ? 'active' : ''}" data-id="${i.id}">
      <span class="gn">${gn(i.group, i.number)}</span>
      <div style="min-width:0"><div class="name">${esc(i.name)}</div>
        <div class="sub"><span>Lv ${i.dropLevel}</span><span>${i.width}×${i.height}</span><span>${esc(classSummary(i))}</span></div></div>
      <div class="tags">${tags.join('')}</div></div>`;
  }).join('');
  $$('#list .row').forEach((el) => el.onclick = () => select(el.dataset.id));
  const active = $('#list .row.active');
  if (active) active.scrollIntoView({ block: 'nearest' });
}

// ------------------------------------------------------------------ seleccion / dirty

function isDirty() { return !!S.draft && !same(S.draft, S.orig); }
function isClientDirty() { return !!S.cdraft && !same(S.cdraft, S.corig); }

function select(id, force = false) {
  if (!force && (isDirty() || isClientDirty()) && id !== S.sel) {
    modal({
      title: 'Cambios sin guardar',
      body: `<p>El item <b>${esc(S.draft.name)}</b> tiene cambios sin guardar. ¿Los descartás?</p>`,
      okText: 'Descartar', okClass: 'danger',
      onOk: () => { select(id, true); },
    });
    return;
  }
  const item = S.byId.get(id);
  if (!item) return;
  S.sel = id;
  S.orig = clone(item);
  S.draft = clone(item);
  const crec = S.client.get(idx(item.group, item.number)) || null;
  S.corig = crec ? clone(crec) : null;
  S.cdraft = crec ? clone(crec) : null;
  renderList();
  renderEditor();
}

// ------------------------------------------------------------------ render: editor

const TABS = [
  ['general', 'General'], ['stats', 'Stats'], ['reqs', 'Requisitos'], ['classes', 'Clases'],
  ['options', 'Opciones y sets'], ['drops', 'Drops'], ['client', 'Cliente'],
];

function renderEditor() {
  const ed = $('#editor');
  if (!S.draft) { ed.innerHTML = '<div class="empty" style="margin-top:120px">Elegí un item de la lista.</div>'; return; }
  const d = S.draft;
  const crec = S.cdraft;
  const dd = crec ? diffs(d, crec) : null;
  const badge = (t) => {
    if (t === 'client') return !crec ? '<span class="badge">sin registro</span>' : (dd.length ? `<span class="badge">≠${dd.length}</span>` : '');
    return '';
  };
  ed.innerHTML = `
    <div class="editor-head">
      <div class="editor-title">
        <h2 id="ed-title">${esc(d.name)}</h2>
        <span class="gn">${gn(d.group, d.number)} · índice ${idx(d.group, d.number)}</span>
        <span class="dirty" id="ed-dirty"></span>
      </div>
      <div class="tabs">${TABS.map(([k, l]) => `<button class="tab ${S.tab === k ? 'active' : ''}" data-tab="${k}">${l}${badge(k)}</button>`).join('')}</div>
    </div>
    <div class="editor-body" id="ed-body"></div>
    <div class="editor-foot">
      <span class="hint" id="ed-hint"></span>
      <button class="btn" id="btn-clone" title="Crear un item nuevo copiando este">Clonar</button>
      <button class="btn danger" id="btn-delete" title="Borrar este item del servidor">Borrar</button>
      <button class="btn primary" id="btn-save" title="Guardar (Ctrl+S)">Guardar</button>
    </div>`;
  $$('.tab', ed).forEach((el) => el.onclick = () => { S.tab = el.dataset.tab; renderEditor(); });
  $('#btn-save').onclick = saveAll;
  $('#btn-clone').onclick = openClone;
  $('#btn-delete').onclick = openDelete;
  renderTab();
  updateDirty();
}

function updateDirty() {
  const parts = [];
  if (isDirty()) parts.push('servidor');
  if (isClientDirty()) parts.push('cliente');
  const el = $('#ed-dirty');
  if (el) el.textContent = parts.length ? `● sin guardar (${parts.join(' + ')})` : '';
  const hint = $('#ed-hint');
  if (hint) hint.textContent = parts.length ? 'Guardar escribe en la base y/o en Item_eng.bmd, con backup previo.' : 'Sin cambios.';
  const t = $('#ed-title');
  if (t && S.draft) t.textContent = S.draft.name;
}

function renderTab() {
  const body = $('#ed-body');
  const r = { general: tabGeneral, stats: tabStats, reqs: tabReqs, classes: tabClasses, options: tabOptions, drops: tabDrops, client: tabClient }[S.tab];
  body.innerHTML = '';
  r(body);
}

// --- campos genéricos ---------------------------------------------------------

function field(label, inner, cls = '') { return `<div class="field ${cls}"><label>${esc(label)}</label>${inner}</div>`; }
function num(key, opts = {}) {
  const v = S.draft[key];
  return `<input type="number" data-key="${key}" data-type="${opts.nullable ? 'intnull' : 'int'}" value="${v ?? ''}" min="${opts.min ?? 0}" max="${opts.max ?? 255}" ${opts.step ? `step="${opts.step}"` : ''} class="${changed(key) ? 'changed' : ''}">`;
}
function txt(key) { return `<input type="text" data-key="${key}" data-type="str" value="${esc(S.draft[key] ?? '')}" class="${changed(key) ? 'changed' : ''}">`; }
function chk(key, label) { return `<label class="check"><input type="checkbox" data-key="${key}" data-type="bool" ${S.draft[key] ? 'checked' : ''}> ${esc(label)}</label>`; }
function sel(key, options, { nullable = true } = {}) {
  const v = S.draft[key];
  return `<select data-key="${key}" data-type="guidnull" class="${changed(key) ? 'changed' : ''}">${nullable ? '<option value="">—</option>' : ''}${options.map((o) => `<option value="${o.id}" ${o.id === v ? 'selected' : ''}>${esc(o.label)}</option>`).join('')}</select>`;
}
function changed(key) { return !same(S.draft[key], S.orig[key]); }

function bindInputs(root) {
  $$('[data-key]', root).forEach((el) => {
    el.addEventListener('input', () => {
      const k = el.dataset.key, t = el.dataset.type;
      let v;
      if (t === 'int') v = el.value === '' ? 0 : Math.round(+el.value);
      else if (t === 'intnull') v = el.value === '' ? null : Math.round(+el.value);
      else if (t === 'float') v = el.value === '' ? 0 : +el.value;
      else if (t === 'bool') v = el.checked;
      else if (t === 'guidnull') v = el.value || null;
      else v = el.value;
      S.draft[k] = v;
      el.classList.toggle('changed', changed(k));
      updateDirty();
    });
  });
}

// --- general ------------------------------------------------------------------

function tabGeneral(body) {
  const m = S.meta;
  body.innerHTML = `
    <div class="section"><h3>Identidad <span class="line"></span></h3>
      <div class="grid">
        ${field('Nombre (servidor)', txt('name'), 'wide')}
        ${field('Grupo', `<select data-key="group" data-type="int" class="${changed('group') ? 'changed' : ''}">${GROUPS.map((g, i) => `<option value="${i}" ${i === S.draft.group ? 'selected' : ''}>${i} · ${esc(g)}</option>`).join('')}</select>`)}
        ${field('Número (0–511)', num('number', { max: 511 }))}
        ${field('Slot de equipo', sel('itemSlotId', m.slots.map((s) => ({ id: s.id, label: `${s.description} (${s.rawItemSlots})` }))))}
      </div>
      <p class="muted" style="font-size:12px;margin:8px 0 0">Cambiar grupo/número mueve el item de índice: el cliente busca el modelo 3D por índice, así que el modelo que se ve cambia.</p>
    </div>
    <div class="section"><h3>Tamaño y niveles <span class="line"></span></h3>
      <div class="grid">
        ${field('Ancho (celdas)', num('width', { min: 1, max: 8 }))}
        ${field('Alto (celdas)', num('height', { min: 1, max: 8 }))}
        ${field('Nivel del item (drop level)', num('dropLevel'))}
        ${field('Nivel máx. de drop', num('maximumDropLevel', { nullable: true }))}
        ${field('Nivel máximo (+N)', num('maximumItemLevel', { max: 15 }))}
        ${field('Durabilidad', num('durability'))}
        ${field('Valor (precio base)', num('value', { max: 2147483647 }))}
        ${field('Sockets máx.', num('maximumSockets', { max: 5 }))}
        ${field('Límite por personaje', num('storageLimitPerCharacter', { max: 2147483647 }))}
      </div>
    </div>
    <div class="section"><h3>Comportamiento <span class="line"></span></h3>
      <div class="grid two">
        <div>${chk('dropsFromMonsters', 'Dropea de monstruos')}${chk('isAmmunition', 'Es munición (flechas / virotes)')}</div>
        <div>${chk('isBoundToCharacter', 'Ligado al personaje')}${chk('isQuestItem', 'Item de quest')}</div>
      </div>
      <div class="grid two" style="margin-top:8px">
        ${field('Skill del item', '<div id="skill-combo"></div>')}
        ${field('Efecto al consumir', sel('consumeEffectId', m.effects.map((e) => ({ id: e.id, label: e.name }))))}
        ${field('Fórmula de experiencia (mascotas)', txt('petExperienceFormula'), 'wide')}
      </div>
    </div>`;
  bindInputs(body);
  combo($('#skill-combo', body), {
    items: [{ id: '', label: '— sin skill —' }, ...m.skills.map((s) => ({ id: s.id, label: s.name, sub: `#${s.number}` }))],
    value: S.draft.skillId || '',
    onChange: (id) => { S.draft.skillId = id || null; updateDirty(); },
  });
}

// --- stats --------------------------------------------------------------------

const QUICK_PU = [
  ['Daño mín. (arma)', A.minDmgW], ['Daño máx. (arma)', A.maxDmgW], ['Velocidad de ataque', A.atkSpeed],
  ['Defensa', A.defense], ['Defensa de escudo', A.shieldDef], ['Bloqueo (def. rate PvM)', A.defRatePvm],
  ['Rise de báculo %', A.staffRise], ['Rise de cetro %', A.scepterRise], ['Vida máx.', A.maxHp], ['Maná máx.', A.maxMana],
  ['Vel. movimiento', A.walkSpeed],
];

function tabStats(body) {
  const attrs = S.meta.attributes.map((a) => ({ id: a.id, label: a.designation, sub: a.description ? a.description.slice(0, 60) : '' }));
  body.innerHTML = `
    <div class="section"><h3>Stats base (ItemBasePowerUpDefinition) <span class="line"></span></h3>
      <p class="muted" style="font-size:12px;margin:0 0 10px">Cada fila suma (o multiplica) un atributo del personaje mientras el item está equipado. La tabla de bonus define cuánto crece con el +nivel del item.</p>
      <div class="rrow pu" style="font-size:11px;color:var(--fg-3);padding:0 4px"><span>Atributo</span><span>Valor</span><span>Modo</span><span>Bonus por nivel</span><span></span></div>
      <div class="rows" id="pu-rows"></div>
      <div class="quick"><button class="btn sm" data-add="">+ Atributo…</button>${QUICK_PU.map(([l, id]) => `<button class="btn sm" data-add="${id}">+ ${esc(l)}</button>`).join('')}</div>
    </div>`;
  const rows = $('#pu-rows', body);
  const draw = () => {
    rows.innerHTML = '';
    S.draft.powerUps.forEach((p, i) => {
      const row = document.createElement('div');
      row.className = 'rrow pu';
      row.innerHTML = `<div class="c"></div>
        <input type="number" step="any" value="${p.baseValue}" data-i="${i}" data-f="baseValue">
        <select data-i="${i}" data-f="aggregateType">${AGG.map((a, k) => `<option value="${k}" ${k === p.aggregateType ? 'selected' : ''}>${a}</option>`).join('')}</select>
        <select data-i="${i}" data-f="bonusPerLevelTableId"><option value="">— sin bonus —</option>${S.meta.bonusTables.map((t) => `<option value="${t.id}" ${t.id === p.bonusPerLevelTableId ? 'selected' : ''}>${esc(t.name)}</option>`).join('')}</select>
        <button class="x" title="Quitar" data-i="${i}">✕</button>`;
      rows.appendChild(row);
      combo($('.c', row), { items: attrs, value: p.targetAttributeId, placeholder: 'Atributo…', onChange: (id) => { p.targetAttributeId = id; updateDirty(); } });
      $$('input,select', row).forEach((el) => el.addEventListener('input', () => {
        const f = el.dataset.f;
        p[f] = f === 'baseValue' ? +el.value : f === 'aggregateType' ? +el.value : (el.value || null);
        updateDirty();
      }));
      $('.x', row).onclick = () => { S.draft.powerUps.splice(i, 1); draw(); updateDirty(); };
    });
    if (!S.draft.powerUps.length) rows.innerHTML = '<div class="muted" style="font-size:12px">Este item no da stats.</div>';
  };
  draw();
  $$('[data-add]', body).forEach((btn) => btn.onclick = () => {
    S.draft.powerUps.push({ id: null, targetAttributeId: btn.dataset.add || '', baseValue: 0, aggregateType: 0, bonusPerLevelTableId: null });
    draw(); updateDirty();
    const last = $$('#pu-rows .rrow input[type=number]', body).pop();
    if (last) last.focus();
  });
}

// --- requisitos ---------------------------------------------------------------

const QUICK_REQ = [['Nivel', A.level], ['Fuerza', A.reqStr], ['Agilidad', A.reqAgi], ['Vitalidad', A.reqVit], ['Energía', A.reqEne], ['Comando', A.reqCmd]];

function tabReqs(body) {
  const attrs = S.meta.attributes.map((a) => ({ id: a.id, label: a.designation }));
  body.innerHTML = `
    <div class="section"><h3>Requisitos para equipar <span class="line"></span></h3>
      <p class="muted" style="font-size:12px;margin:0 0 10px">Los de fuerza/agilidad/etc. son el valor base del item; OpenMU los ajusta por nivel y opciones igual que el juego original.</p>
      <div class="rows" id="req-rows"></div>
      <div class="quick"><button class="btn sm" data-add="">+ Atributo…</button>${QUICK_REQ.map(([l, id]) => `<button class="btn sm" data-add="${id}">+ ${esc(l)}</button>`).join('')}</div>
    </div>`;
  const rows = $('#req-rows', body);
  const draw = () => {
    rows.innerHTML = '';
    S.draft.requirements.forEach((r, i) => {
      const row = document.createElement('div');
      row.className = 'rrow';
      row.innerHTML = `<div class="c"></div><input type="number" min="0" max="65535" value="${r.minimumValue}"><button class="x" title="Quitar">✕</button>`;
      rows.appendChild(row);
      combo($('.c', row), { items: attrs, value: r.attributeId, placeholder: 'Atributo…', onChange: (id) => { r.attributeId = id; updateDirty(); } });
      $('input[type=number]', row).addEventListener('input', (e) => { r.minimumValue = Math.round(+e.target.value || 0); updateDirty(); });
      $('.x', row).onclick = () => { S.draft.requirements.splice(i, 1); draw(); updateDirty(); };
    });
    if (!S.draft.requirements.length) rows.innerHTML = '<div class="muted" style="font-size:12px">Sin requisitos.</div>';
  };
  draw();
  $$('[data-add]', body).forEach((btn) => btn.onclick = () => {
    const id = btn.dataset.add;
    if (id && S.draft.requirements.some((r) => r.attributeId === id)) { toast('Ese requisito ya está.', 'warn'); return; }
    S.draft.requirements.push({ id: null, attributeId: id || '', minimumValue: 0 });
    draw(); updateDirty();
    const last = $$('#req-rows input[type=number]', body).pop();
    if (last) last.focus();
  });
}

// --- clases -------------------------------------------------------------------

function tabClasses(body) {
  const fams = FAMILIES.map((name, f) => {
    const classes = S.meta.classes.filter((c) => c.map[0] === f).sort((a, b2) => a.map[1] - b2.map[1]);
    return `<div class="fam"><h5>${esc(name)}</h5>${classes.map((c) => `<label class="check"><input type="checkbox" data-cls="${c.id}" ${S.draft.classIds.includes(c.id) ? 'checked' : ''}> ${esc(c.name)} <span class="muted" style="margin-left:auto;font-size:11px">${c.map[1] === 1 ? 'base' : c.map[1] === 2 ? '2ª' : '3ª'}</span></label>`).join('')}</div>`;
  });
  body.innerHTML = `
    <div class="section"><h3>Clases que pueden equiparlo <span class="line"></span>
      <button class="btn sm" id="cls-all">Todas</button><button class="btn sm" id="cls-none">Ninguna</button></h3>
      <div class="classes">${fams.join('')}</div>
      <p class="muted" style="font-size:12px;margin-top:10px">En el cliente esto se guarda por familia como “paso mínimo” (1 = clase base, 2 = segunda, 3 = tercera). Si marcás solo Blade Master, el cliente lo mostrará como DK paso 3.</p>
    </div>`;
  const sync = () => { S.draft.classIds = $$('[data-cls]:checked', body).map((el) => el.dataset.cls); updateDirty(); };
  $$('[data-cls]', body).forEach((el) => el.addEventListener('change', sync));
  $('#cls-all', body).onclick = () => { $$('[data-cls]', body).forEach((el) => el.checked = true); sync(); };
  $('#cls-none', body).onclick = () => { $$('[data-cls]', body).forEach((el) => el.checked = false); sync(); };
}

// --- opciones y sets -----------------------------------------------------------

function picklist(container, { items, selected, onChange, placeholder }) {
  let onlySel = false, q = '';
  const draw = () => {
    const ql = q.toLowerCase();
    const list = items.filter((x) => (!onlySel || selected.has(x.id)) && (!ql || x.label.toLowerCase().includes(ql)));
    container.innerHTML = `<div class="top"><input class="input" placeholder="${esc(placeholder)}" value="${esc(q)}"><button class="btn sm ${onlySel ? 'primary' : ''}" data-only>Solo marcados (${selected.size})</button></div>
      <div class="items">${list.slice(0, 400).map((x) => `<label class="check"><input type="checkbox" data-id="${x.id}" ${selected.has(x.id) ? 'checked' : ''}> ${esc(x.label)}${x.sub ? `<small>${esc(x.sub)}</small>` : ''}</label>`).join('') || '<div class="muted" style="padding:6px 0;font-size:12px">Nada.</div>'}</div>`;
    const inp = $('input.input', container);
    inp.addEventListener('input', () => { q = inp.value; const pos = inp.selectionStart; draw(); const i2 = $('input.input', container); i2.focus(); i2.setSelectionRange(pos, pos); });
    $('[data-only]', container).onclick = () => { onlySel = !onlySel; draw(); };
    $$('[data-id]', container).forEach((el) => el.addEventListener('change', () => { el.checked ? selected.add(el.dataset.id) : selected.delete(el.dataset.id); onChange([...selected]); $('[data-only]', container).textContent = `Solo marcados (${selected.size})`; }));
  };
  container.classList.add('picklist');
  draw();
}

function tabOptions(body) {
  body.innerHTML = `
    <div class="section"><h3>Opciones posibles <span class="line"></span></h3>
      <p class="muted" style="font-size:12px;margin:0 0 8px">Qué opciones puede tener el item al dropear o al crearse: Luck, Skill, opciones excelentes, ancient, harmony, sockets…</p>
      <div id="pl-options"></div></div>
    <div class="section"><h3>Sets (ancient / set bonus) <span class="line"></span></h3>
      <div id="pl-sets"></div></div>`;
  picklist($('#pl-options', body), {
    items: S.meta.options.map((o) => ({ id: o.id, label: o.name })), selected: new Set(S.draft.optionIds), placeholder: 'Buscar opción…',
    onChange: (ids) => { S.draft.optionIds = ids; updateDirty(); },
  });
  picklist($('#pl-sets', body), {
    items: S.meta.setGroups.map((s) => ({ id: s.id, label: s.name, sub: `nivel ${s.setLevel}` })), selected: new Set(S.draft.setGroupIds), placeholder: 'Buscar set…',
    onChange: (ids) => { S.draft.setGroupIds = ids; updateDirty(); },
  });
}

// --- drops --------------------------------------------------------------------

function tabDrops(body) {
  body.innerHTML = `
    <div class="section"><h3>Drop <span class="line"></span></h3>
      <div class="grid">
        <div>${chk('dropsFromMonsters', 'Dropea de monstruos')}</div>
        ${field('Nivel de drop (mín.)', num('dropLevel'))}
        ${field('Nivel de drop (máx.)', num('maximumDropLevel', { nullable: true }))}
      </div>
      <p class="muted" style="font-size:12px;margin:8px 0 0">Con “dropea de monstruos”, cae de cualquier monstruo cuyo nivel esté entre el nivel de drop y el máximo (si está vacío, sin tope). Los grupos de abajo son drops especiales (jewels, cajas, eventos).</p>
    </div>
    <div class="section"><h3>Grupos de drop especiales <span class="line"></span></h3><div id="pl-drops"></div></div>`;
  bindInputs(body);
  picklist($('#pl-drops', body), {
    items: S.meta.dropGroups.map((g) => ({ id: g.id, label: g.name || '(sin descripción)' })), selected: new Set(S.draft.dropGroupIds), placeholder: 'Buscar grupo…',
    onChange: (ids) => { S.draft.dropGroupIds = ids; updateDirty(); },
  });
}

// --- cliente ------------------------------------------------------------------

const CLIENT_FIELDS = [
  ['name', 'Nombre (cliente)', 'str'], ['level', 'Nivel', 'w'], ['slot', 'Slot', 'slot'], ['width', 'Ancho', 'b'], ['height', 'Alto', 'b'],
  ['durability', 'Durabilidad', 'b'], ['twoHand', 'Dos manos', 'bool'], ['skillIndex', 'Skill (índice)', 'w'],
  ['damageMin', 'Daño mín.', 'b'], ['damageMax', 'Daño máx.', 'b'], ['weaponSpeed', 'Velocidad', 'b'], ['magicPower', 'Poder mágico', 'b'],
  ['defense', 'Defensa', 'b'], ['magicDefense', 'Def. mágica', 'b'], ['successfulBlocking', 'Bloqueo', 'b'], ['walkSpeed', 'Vel. movimiento', 'b'],
  ['requireLevel', 'Req. nivel', 'w'], ['requireStrength', 'Req. fuerza', 'w'], ['requireDexterity', 'Req. agilidad', 'w'],
  ['requireVitality', 'Req. vitalidad', 'w'], ['requireEnergy', 'Req. energía', 'w'], ['requireCharisma', 'Req. comando', 'w'],
  ['value', 'Valor', 'b'], ['zen', 'Precio (zen)', 'int'], ['attType', 'Tipo de ataque', 'b'], ['magicDur', 'Dur. mágica', 'b'],
];

function tabClient(body) {
  const d = S.draft;
  const index = idx(d.group, d.number);
  if (!S.status.clientFileOk) {
    body.innerHTML = '<div class="note bad">No está <code>client\\runtime\\Data\\Local\\Eng\\Item_eng.bmd</code>. Corré <code>scripts\\setup-client.ps1</code> para bajar los assets del cliente.</div>';
    return;
  }
  if (!S.cdraft) {
    body.innerHTML = `
      <div class="note warn">Este item no tiene registro en el cliente (índice ${index}). El juego no va a poder mostrar nombre, tamaño ni requisitos.</div>
      <button class="btn primary" id="cl-create">Crear registro en el cliente a partir del servidor</button>
      <p class="muted" style="font-size:12px;margin-top:14px">El modelo 3D lo elige el cliente por índice (está hardcodeado en <code>OpenItems()</code> de MuMain). Si el índice no tiene modelo asignado, el item se ve sin modelo hasta recompilar el cliente.</p>`;
    $('#cl-create', body).onclick = () => { S.cdraft = serverToClient(d, null); renderEditor(); updateDirty(); };
    return;
  }
  const c = S.cdraft;
  const expected = serverToClient(d, c);
  const equip = isEquippable(d);
  const rows = CMP_FIELDS.filter(([, , always]) => always || equip).map(([key, label]) => {
    const diff = !same(c[key], expected[key]);
    return `<tr class="${diff ? 'diff' : ''}"><td>${esc(label)}</td><td>${esc(fmtVal(key, c[key]))}</td><td>${esc(fmtVal(key, expected[key]))}</td></tr>`;
  });
  const nDiff = rows.filter((r) => r.includes('class="diff"')).length;
  body.innerHTML = `
    <div class="section"><h3>Cliente vs. servidor <span class="line"></span>
      <button class="btn sm" id="cl-apply" ${nDiff ? '' : 'disabled'}>← Aplicar valores del servidor al cliente</button>
      <button class="btn sm" id="cl-import" ${nDiff ? '' : 'disabled'}>Importar del cliente al servidor →</button></h3>
      ${nDiff ? `<div class="note warn">${nDiff} campo(s) difieren. El jugador ve lo del cliente; el servidor aplica lo suyo.</div>` : '<div class="note">Cliente y servidor coinciden.</div>'}
      <table class="cmp"><thead><tr><th>Campo</th><th>Cliente (Item_eng.bmd)</th><th>Servidor (esperado)</th></tr></thead><tbody>${rows.join('')}</tbody></table>
    </div>
    <div class="section"><h3>Registro del cliente (índice ${index}) <span class="line"></span></h3>
      <div class="grid" id="cl-grid">${CLIENT_FIELDS.map(([k, l, t]) => field(l, clientInput(c, k, t))).join('')}</div>
      <div class="grid" style="margin-top:10px">${FAM_SHORT.map((f, i) => field(`Clase ${f}`, `<select data-ck="requireClass" data-ci="${i}">${['no', 'base (1)', '2ª (2)', '3ª (3)'].map((l, v) => `<option value="${v}" ${c.requireClass[i] === v ? 'selected' : ''}>${l}</option>`).join('')}</select>`)).join('')}</div>
      <div class="grid" style="margin-top:10px">${['Hielo', 'Veneno', 'Rayo', 'Fuego', 'Tierra', 'Viento', 'Agua', 'Res. 7'].map((l, i) => field(`Res. ${l}`, `<input type="number" min="0" max="255" data-ck="resistance" data-ci="${i}" value="${c.resistance[i]}">`)).join('')}</div>
      <p class="muted" style="font-size:12px;margin-top:10px">Nombre: máximo ${S.status.clientNameLength - 1} bytes UTF-8. El modelo 3D no se define acá: MuMain lo asocia por índice en el código.</p>
    </div>`;
  $('#cl-apply', body).onclick = () => { S.cdraft = expected; renderEditor(); updateDirty(); };
  $('#cl-import', body).onclick = () => { S.draft = clientToServer(S.draft, c); renderEditor(); updateDirty(); };
  $$('[data-ck]', body).forEach((el) => el.addEventListener('input', () => {
    const k = el.dataset.ck, t = el.dataset.ct, i = el.dataset.ci;
    if (i !== undefined) { c[k][+i] = b(+el.value); }
    else if (t === 'bool') c[k] = el.checked;
    else if (t === 'str') c[k] = el.value;
    else if (t === 'b' || t === 'slot') c[k] = b(+el.value || 0);
    else if (t === 'w') c[k] = w(+el.value || 0);
    else c[k] = Math.round(+el.value || 0);
    el.classList.toggle('changed', !S.corig || !same(c[k], S.corig[k]));
    updateDirty();
  }));
}

function clientInput(c, k, t) {
  const ch = S.corig && !same(c[k], S.corig[k]) ? 'changed' : '';
  if (t === 'bool') return `<label class="check"><input type="checkbox" data-ck="${k}" data-ct="bool" ${c[k] ? 'checked' : ''}> sí</label>`;
  if (t === 'str') return `<input type="text" data-ck="${k}" data-ct="str" value="${esc(c[k])}" class="${ch}">`;
  if (t === 'slot') return `<select data-ck="${k}" data-ct="slot" class="${ch}">${Object.entries(CLIENT_SLOT_NAMES).map(([v, l]) => `<option value="${v}" ${+v === c[k] ? 'selected' : ''}>${v} · ${l}</option>`).join('')}</select>`;
  const max = t === 'b' ? 255 : t === 'w' ? 65535 : 2147483647;
  return `<input type="number" min="0" max="${max}" data-ck="${k}" data-ct="${t}" value="${c[k]}" class="${ch}">`;
}

// ------------------------------------------------------------------ acciones

async function saveAll() {
  if (!S.draft) return;
  const btn = $('#btn-save');
  btn.disabled = true;
  try {
    let any = false;
    if (isDirty()) {
      const before = clone(S.orig);
      const saved = await api(`/items/${S.draft.id}`, { method: 'PUT', body: S.draft });
      replaceItem(saved);
      S.undo.push({ label: `servidor: ${before.name}`, run: async () => { const s = await api(`/items/${before.id}`, { method: 'PUT', body: before }); replaceItem(s); } });
      S.restartNeeded = true;
      any = true;
    }
    if (isClientDirty()) {
      const before = S.corig ? clone(S.corig) : null;
      const index = S.cdraft.index;
      const saved = await api(`/client/items/${index}`, { method: 'PUT', body: S.cdraft });
      S.client.set(index, saved);
      S.corig = clone(saved); S.cdraft = clone(saved);
      S.undo.push({
        label: `cliente: ${saved.name}`,
        run: async () => {
          if (before) { const s = await api(`/client/items/${index}`, { method: 'PUT', body: before }); S.client.set(index, s); }
          else { await api(`/client/items/${index}`, { method: 'DELETE' }); S.client.delete(index); }
        },
      });
      any = true;
    }
    if (any) {
      toast('Guardado. Backup en tools\\item-editor\\backups\\', 'ok');
      // si cambió grupo/número, el registro del cliente que corresponde es otro
      const it = S.byId.get(S.sel);
      const crec = S.client.get(idx(it.group, it.number)) || null;
      S.corig = crec ? clone(crec) : null; S.cdraft = crec ? clone(crec) : null;
      renderList(); renderEditor(); renderPills();
    } else {
      toast('No hay cambios para guardar.');
    }
  } catch (e) {
    toast('No se pudo guardar: ' + e.message, 'bad', 6000);
  } finally {
    btn.disabled = false;
  }
}

function replaceItem(saved) {
  const i = S.items.findIndex((x) => x.id === saved.id);
  if (i >= 0) S.items[i] = saved; else S.items.push(saved);
  S.items.sort((a, b2) => a.group - b2.group || a.number - b2.number);
  S.byId.set(saved.id, saved);
  if (S.sel === saved.id) { S.orig = clone(saved); S.draft = clone(saved); }
}

async function undo() {
  const u = S.undo.pop();
  if (!u) return;
  try {
    await u.run();
    toast('Deshecho: ' + u.label, 'ok');
    if (S.sel) select(S.sel, true); else renderList();
    renderPills();
  } catch (e) { toast('No se pudo deshacer: ' + e.message, 'bad', 6000); S.undo.push(u); }
}

function nextFree(group) {
  const used = new Set(S.items.filter((i) => i.group === group).map((i) => i.number));
  for (let n = 0; n < 512; n++) if (!used.has(n)) return n;
  return 0;
}

function openClone() {
  const d = S.draft;
  modal({
    title: `Clonar “${d.name}”`,
    body: `<p>Crea un item nuevo con los mismos stats, requisitos, clases y opciones. No entra en grupos de drop.</p>
      <div class="grid three">
        <div class="field"><label>Grupo</label><select id="cl-g">${GROUPS.map((g, i) => `<option value="${i}" ${i === d.group ? 'selected' : ''}>${i} · ${esc(g)}</option>`).join('')}</select></div>
        <div class="field"><label>Número</label><input id="cl-n" type="number" min="0" max="511" value="${nextFree(d.group)}"></div>
        <div class="field wide"><label>Nombre</label><input id="cl-name" type="text" value="${esc(d.name)} (copia)"></div>
      </div>
      <label class="check" style="margin-top:8px"><input type="checkbox" id="cl-client" ${S.cdraft ? 'checked' : ''}> Crear también el registro en el cliente (Item_eng.bmd)</label>
      <div class="note warn" style="margin-top:8px">El índice nuevo usará el modelo 3D que el cliente tenga asignado a ese índice (o ninguno). Para verlo con un modelo concreto hay que recompilar MuMain.</div>`,
    okText: 'Clonar',
    onOpen: (root) => { $('#cl-g', root).addEventListener('change', (e) => { $('#cl-n', root).value = nextFree(+e.target.value); }); },
    onOk: async (root) => {
      const group = +$('#cl-g', root).value, number = +$('#cl-n', root).value, name = $('#cl-name', root).value.trim();
      const created = await api(`/items/${d.id}/clone`, { method: 'POST', body: { group, number, name } });
      replaceItem(created);
      S.restartNeeded = true;
      if ($('#cl-client', root).checked) {
        const base = S.cdraft || null;
        const rec = serverToClient(created, base);
        const saved = await api(`/client/items/${rec.index}`, { method: 'PUT', body: rec });
        S.client.set(rec.index, saved);
      }
      S.undo.push({ label: `clon ${created.name}`, run: async () => { await api(`/items/${created.id}`, { method: 'DELETE' }); removeItem(created.id); } });
      toast(`Creado ${gn(group, number)} · ${created.name}`, 'ok');
      renderGroups();
      select(created.id, true);
      renderPills();
    },
  });
}

function removeItem(id) {
  S.items = S.items.filter((x) => x.id !== id);
  S.byId.delete(id);
  if (S.sel === id) { S.sel = null; S.draft = S.orig = S.cdraft = S.corig = null; }
}

function openDelete() {
  const d = S.draft;
  modal({
    title: `Borrar “${d.name}”`,
    body: `<p>Se borra la definición del servidor (${gn(d.group, d.number)}). Si algún personaje tiene uno, el servidor lo rechaza y no borra nada.</p>
      <label class="check"><input type="checkbox" id="del-client" ${S.cdraft ? 'checked' : ''}> Vaciar también el registro del cliente</label>
      <div class="note bad" style="margin-top:8px">Esto no se puede deshacer desde el editor (queda el JSON en <code>backups\\items\\</code>).</div>`,
    okText: 'Borrar', okClass: 'danger',
    onOk: async (root) => {
      await api(`/items/${d.id}`, { method: 'DELETE' });
      if ($('#del-client', root).checked && S.cdraft) { await api(`/client/items/${S.cdraft.index}`, { method: 'DELETE' }); S.client.delete(S.cdraft.index); }
      removeItem(d.id);
      S.restartNeeded = true;
      toast('Item borrado.', 'ok');
      renderGroups(); renderList(); renderEditor(); renderPills();
    },
  });
}

// ------------------------------------------------------------------ eventos globales

function bindGlobal() {
  const search = $('#search');
  search.addEventListener('input', () => { S.q = search.value; renderList(); });
  $('#sort').addEventListener('change', (e) => { S.sort = e.target.value; renderList(); });
  $('#btn-undo').onclick = undo;
  $('#btn-reload').onclick = async () => {
    if (isDirty() || isClientDirty()) { toast('Guardá o descartá los cambios antes de recargar.', 'warn'); return; }
    try { await loadAll(); S.restartNeeded = false; renderPills(); renderGroups(); renderList(); if (S.sel && S.byId.has(S.sel)) select(S.sel, true); else { S.sel = null; S.draft = null; renderEditor(); } toast('Recargado.'); }
    catch (e) { toast(e.message, 'bad'); }
  };
  document.addEventListener('keydown', (e) => {
    const inInput = /^(INPUT|SELECT|TEXTAREA)$/.test(e.target.tagName);
    if (e.key === '/' && !inInput) { e.preventDefault(); search.focus(); search.select(); }
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') { e.preventDefault(); saveAll(); }
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z' && !inInput) { e.preventDefault(); undo(); }
    if ((e.key === 'ArrowDown' || e.key === 'ArrowUp') && (e.target === search || !inInput)) {
      const list = filtered();
      if (!list.length) return;
      let i = list.findIndex((x) => x.id === S.sel);
      i = e.key === 'ArrowDown' ? Math.min(i + 1, list.length - 1) : Math.max(i - 1, 0);
      e.preventDefault();
      select(list[i].id);
    }
  });
  setInterval(refreshStatus, 15000);
}

// ------------------------------------------------------------------ init

(async function init() {
  bindGlobal();
  try {
    await loadAll();
  } catch (e) {
    $('#editor').innerHTML = `<div class="empty"><div class="note bad">No pude cargar: ${esc(e.message)}<br><br>¿Está corriendo PostgreSQL? Probá <code>scripts\\start.ps1</code> y recargá.</div></div>`;
    return;
  }
  renderPills(); renderGroups(); renderList(); renderEditor();
})();
