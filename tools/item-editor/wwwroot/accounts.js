/* Modo "Cuentas": personajes, equipo, inventario y baul. Usa S (estado global), api(), toast(), modal(),
   combo(), esc() y Viewer de app.js/viewer.js. */
'use strict';

const Acc = (() => {
  const EQUIP = [
    [1, 'Mano der.'], [0, 'Mano izq.'], [2, 'Casco'], [3, 'Armadura'], [4, 'Pantalón'], [5, 'Guantes'],
    [6, 'Botas'], [7, 'Alas'], [8, 'Mascota'], [9, 'Pendiente'], [10, 'Anillo 1'], [11, 'Anillo 2'],
  ];
  const CLASS_ICON = { 0: 'DW', 2: 'SM', 3: 'GM', 4: 'DK', 6: 'BK', 7: 'BM', 8: 'FE', 10: 'ME', 11: 'HE', 12: 'MG', 13: 'DM', 16: 'DL', 17: 'LE', 20: 'SU', 22: 'BS', 23: 'DiM', 24: 'RF', 25: 'FM' };
  const T = {
    accounts: [], q: '', accountId: null, detail: null, charId: null,
    sel: null,          // { item, storageId, kind: 'inv'|'vault', charId }
    adding: null,       // { storageId, slot, kind }
    defOptions: null, defOptionsFor: null,
    draft: null, thumbs: new Map(), thumbQueue: [], thumbBusy: false,
  };

  // ------------------------------------------------------------ helpers de grilla

  function occupancy(items, base, rows) {
    const grid = Array.from({ length: rows }, () => new Array(8).fill(null));
    for (const it of items) {
      if (it.slot < base) continue;
      const c = (it.slot - base) % 8, r = Math.floor((it.slot - base) / 8);
      for (let y = r; y < r + it.height && y < rows; y++) for (let x = c; x < c + it.width && x < 8; x++) grid[y][x] = it;
    }
    return grid;
  }
  function fits(grid, col, row, w, h, ignore) {
    if (col < 0 || row < 0 || col + w > 8 || row + h > grid.length) return false;
    for (let y = row; y < row + h; y++) for (let x = col; x < col + w; x++) { const o = grid[y][x]; if (o && o !== ignore) return false; }
    return true;
  }
  function firstFree(items, base, rows, w, h) {
    const grid = occupancy(items, base, rows);
    for (let r = 0; r < rows; r++) for (let c = 0; c < 8; c++) if (fits(grid, c, r, w, h, null)) return base + r * 8 + c;
    return null;
  }
  function invRows(ch) { return 8 + 4 * Math.max(0, Math.min(4, ch.inventoryExtensions || 0)); }
  function vaultRows() { return T.detail.account.isVaultExtended ? 30 : 15; }
  function itemKind(it) {
    const types = new Set(it.options.map((o) => o.typeName || ''));
    if (it.itemOfItemSetId || [...types].some((t) => t.startsWith('Ancient'))) return 'anc';
    if ([...types].some((t) => t.startsWith('Excellent'))) return 'exc';
    return '';
  }
  function itemTitle(it) {
    const parts = [`${it.name} +${it.level}`, `${it.width}×${it.height}`, `dur ${it.durability}`];
    if (it.hasSkill) parts.push('skill');
    for (const o of it.options) parts.push(`${o.typeName || '?'}${o.level ? ' L' + o.level : ''}${o.label ? ': ' + o.label : ''}`);
    return parts.join('\n');
  }
  function shortName(name) { return name.length > 14 ? name.slice(0, 13) + '…' : name; }

  // ------------------------------------------------------------ miniaturas 3D

  function thumb(it) {
    const index = it.group * 512 + it.number;
    if (T.thumbs.has(index)) return T.thumbs.get(index);
    if (!T.thumbQueue.includes(index)) { T.thumbQueue.push(index); pumpThumbs(); }
    return null;
  }
  async function pumpThumbs() {
    if (T.thumbBusy || typeof Viewer === 'undefined' || !Viewer.thumbnail) return;
    T.thumbBusy = true;
    while (T.thumbQueue.length) {
      const index = T.thumbQueue.shift();
      try {
        const url = await Viewer.thumbnail(index);
        T.thumbs.set(index, url || '');
        $$(`[data-thumb="${index}"]`).forEach((el) => { if (url) { el.style.backgroundImage = `url(${url})`; el.classList.add('has-thumb'); } });
      } catch { T.thumbs.set(index, ''); }
    }
    T.thumbBusy = false;
  }

  // ------------------------------------------------------------ carga

  async function loadAccounts() {
    T.accounts = await api('/accounts');
    renderSide();
  }
  async function openAccount(id) {
    T.accountId = id;
    T.detail = await api(`/accounts/${id}`);
    if (!T.detail.characters.some((c) => c.id === T.charId)) T.charId = T.detail.characters[0]?.id || null;
    T.sel = null; T.adding = null;
    renderSide(); renderMain(); renderEditor();
  }
  async function reload(keepSel = true) {
    const sel = T.sel;
    T.detail = await api(`/accounts/${T.accountId}`);
    if (keepSel && sel) {
      T.sel = findItem(sel.item.id);
      if (T.sel) T.draft = draftFrom(T.sel.item);
    } else T.sel = null;
    renderMain(); renderEditor();
    loadAccounts();
  }
  function currentChar() { return T.detail?.characters.find((c) => c.id === T.charId) || null; }

  // ------------------------------------------------------------ render: sidebar

  function renderSide() {
    const root = $('#acc-side');
    const q = T.q.trim().toLowerCase();
    const list = T.accounts.filter((a) => !q || a.loginName.toLowerCase().includes(q) || a.characterNames.some((n) => n.toLowerCase().includes(q)));
    root.innerHTML = `<h4>Cuentas <span class="muted">(${list.length})</span></h4><div style="padding:0 4px 8px"><button class="btn sm primary" id="acc-new" style="width:100%">+ Nueva cuenta</button></div>` + list.map((a) => `
      <button class="acc-row ${a.id === T.accountId ? 'active' : ''}" data-id="${a.id}">
        <span class="login">${esc(a.loginName)}${a.state === 2 || a.state === 3 ? ' <span class="tag info">GM</span>' : a.state === 4 || a.state === 5 ? ' <span class="tag warn">ban</span>' : ''}</span>
        <span class="chars">${a.characters ? esc(a.characterNames.join(', ')) : '<i>sin personajes</i>'}</span>
      </button>`).join('') || '<div class="muted" style="padding:8px">Nada.</div>';
    $$('.acc-row', root).forEach((el) => el.onclick = () => openAccount(el.dataset.id).catch((e) => toast(e.message, 'bad')));
    $('#acc-new', root).onclick = newAccount;
  }

  const STATES = [[0, 'Normal'], [2, 'Game Master'], [3, 'Game Master (invisible)'], [4, 'Baneada'], [5, 'Baneada temporalmente'], [1, 'Espectador']];
  const CHAR_STATES = [[0, 'Normal'], [32, 'Game Master'], [1, 'Baneado']];

  function newAccount() {
    modal({
      title: 'Nueva cuenta',
      body: `<div class="grid two">
        <div class="field"><label>Cuenta (1–10 letras/números)</label><input id="na-login" maxlength="10" autocomplete="off"></div>
        <div class="field"><label>Contraseña (1–20)</label><input id="na-pass" maxlength="20" autocomplete="new-password"></div>
        <div class="field"><label>Email (opcional)</label><input id="na-mail"></div>
        <div class="field"><label>Estado</label><select id="na-state">${STATES.map(([v, l]) => `<option value="${v}">${l}</option>`).join('')}</select></div>
      </div>
      <p class="muted" style="font-size:12px;margin-top:10px">Con la cuenta creada, el jugador entra con ese usuario y contraseña y crea sus personajes en el juego (o los creás vos acá).</p>`,
      okText: 'Crear',
      onOk: async (root) => {
        const body = { loginName: $('#na-login', root).value.trim(), password: $('#na-pass', root).value, eMail: $('#na-mail', root).value.trim(), state: +$('#na-state', root).value };
        const r = await api('/accounts', { method: 'POST', body });
        toast(`Cuenta ${body.loginName} creada.`, 'ok');
        await loadAccounts();
        await openAccount(r.id);
      },
    });
  }

  function editAccount() {
    const a = T.detail.account;
    modal({
      title: `Cuenta ${a.loginName}`,
      body: `<div class="grid two">
        <div class="field"><label>Nueva contraseña (vacío = no cambiar)</label><input id="ea-pass" maxlength="20" autocomplete="new-password"></div>
        <div class="field"><label>Email</label><input id="ea-mail" value="${esc(a.eMail || '')}"></div>
        <div class="field"><label>Estado</label><select id="ea-state">${STATES.map(([v, l]) => `<option value="${v}" ${a.state === v ? 'selected' : ''}>${l}</option>`).join('')}</select></div>
        <div class="field"><label>Baúl</label><label class="check" style="padding-top:8px"><input type="checkbox" id="ea-vault" ${a.isVaultExtended ? 'checked' : ''}> Extendido (8×30)</label></div>
      </div>`,
      okText: 'Guardar',
      onOk: async (root) => {
        const body = { password: $('#ea-pass', root).value || null, eMail: $('#ea-mail', root).value.trim(), state: +$('#ea-state', root).value, isVaultExtended: $('#ea-vault', root).checked };
        await api(`/accounts/${a.id}`, { method: 'PUT', body });
        toast('Cuenta guardada.', 'ok');
        await reload();
      },
    });
  }

  function deleteAccount() {
    const a = T.detail.account;
    modal({
      title: `Borrar la cuenta ${a.loginName}`,
      body: `<p>Se borran la cuenta, sus ${T.detail.characters.length} personaje(s), inventarios y baúl. <b>No hay deshacer.</b></p>
        <div class="field"><label>Escribí el nombre de la cuenta para confirmar</label><input id="da-confirm" autocomplete="off"></div>`,
      okText: 'Borrar todo', okClass: 'danger',
      onOk: async (root) => {
        if ($('#da-confirm', root).value.trim().toLowerCase() !== a.loginName.toLowerCase()) { toast('El nombre no coincide.', 'warn'); return false; }
        await api(`/accounts/${a.id}`, { method: 'DELETE' });
        toast('Cuenta borrada.', 'ok');
        T.accountId = null; T.detail = null; T.sel = null; T.adding = null;
        await loadAccounts(); renderMain(); renderEditor();
      },
    });
  }

  async function newCharacter() {
    const classes = (await api('/classes')).filter((c) => c.canGetCreated);
    const used = new Set(T.detail.characters.map((c) => c.slot));
    const free = [0, 1, 2, 3, 4].filter((s) => !used.has(s));
    if (!free.length) { toast('La cuenta ya tiene 5 personajes.', 'warn'); return; }
    modal({
      title: 'Nuevo personaje',
      body: `<div class="grid two">
        <div class="field"><label>Nombre (1–10 letras/números)</label><input id="nc-name" maxlength="10" autocomplete="off"></div>
        <div class="field"><label>Clase</label><select id="nc-class">${classes.map((c) => `<option value="${c.number}">${esc(c.name)}</option>`).join('')}</select></div>
      </div>
      <p class="muted" style="font-size:12px;margin-top:10px">Nivel 1, stats base de la clase, en el mapa inicial. Después lo podés subir de nivel con "Editar personaje".</p>`,
      okText: 'Crear',
      onOk: async (root) => {
        const body = { name: $('#nc-name', root).value.trim(), classNumber: +$('#nc-class', root).value, slot: free[0] };
        const r = await api(`/accounts/${T.accountId}/characters`, { method: 'POST', body });
        toast(`Personaje ${body.name} creado.`, 'ok');
        T.charId = r.id;
        await reload();
      },
    });
  }

  function editCharacter() {
    const c = currentChar(); if (!c) return;
    const st = c.stats || {};
    const statField = (k, label) => st[k] === undefined ? '' : `<div class="field"><label>${label}</label><input type="number" min="0" max="65535" data-stat="${esc(k)}" value="${Math.round(st[k])}"></div>`;
    modal({
      title: `Editar ${c.name}`,
      body: `<div class="grid two">
        <div class="field"><label>Nombre</label><input id="ec-name" maxlength="10" value="${esc(c.name)}"></div>
        <div class="field"><label>Nivel (1–400)</label><input type="number" id="ec-level" min="1" max="400" value="${c.level}"></div>
        ${statField('Base Strength', 'Fuerza')}${statField('Base Agility', 'Agilidad')}${statField('Base Vitality', 'Vitalidad')}${statField('Base Energy', 'Energía')}${statField('Base Leadership', 'Comando')}
        <div class="field"><label>Puntos libres</label><input type="number" id="ec-points" min="0" value="${c.levelUpPoints}"></div>
        <div class="field"><label>Estado</label><select id="ec-state">${CHAR_STATES.map(([v, l]) => `<option value="${v}" ${c.state === v ? 'selected' : ''}>${l}</option>`).join('')}</select></div>
        <div class="field"><label>Extensiones de inventario (0–4)</label><input type="number" id="ec-ext" min="0" max="4" value="${c.inventoryExtensions}"></div>
        <div class="field"><label>PK (asesinatos)</label><input type="number" id="ec-pk" min="0" value="${c.playerKillCount}"></div>
      </div>
      <p class="muted" style="font-size:12px;margin-top:10px">Al cambiar el nivel se ajustan la experiencia (fórmula de OpenMU) y los puntos libres según la clase. Un personaje "Game Master" puede usar los comandos /item, /level, etc.</p>`,
      okText: 'Guardar',
      onOk: async (root) => {
        const stats = {};
        $$('[data-stat]', root).forEach((el) => { stats[el.dataset.stat] = +el.value || 0; });
        const level = +$('#ec-level', root).value;
        const points = +$('#ec-points', root).value;
        const body = {
          name: $('#ec-name', root).value.trim() !== c.name ? $('#ec-name', root).value.trim() : null,
          level: level !== c.level ? level : null,
          levelUpPoints: level === c.level || points !== c.levelUpPoints ? points : null,
          state: +$('#ec-state', root).value, inventoryExtensions: +$('#ec-ext', root).value, playerKillCount: +$('#ec-pk', root).value, stats,
        };
        await api(`/characters/${c.id}`, { method: 'PUT', body });
        toast('Personaje guardado.', 'ok');
        await reload();
      },
    });
  }

  function deleteCharacter() {
    const c = currentChar(); if (!c) return;
    modal({
      title: `Borrar a ${c.name}`,
      body: `<p>Se borra el personaje con su inventario (${c.items.length} items). El baúl es de la cuenta y no se toca. <b>No hay deshacer.</b></p>`,
      okText: 'Borrar', okClass: 'danger',
      onOk: async () => {
        await api(`/characters/${c.id}`, { method: 'DELETE' });
        toast('Personaje borrado.', 'ok');
        T.charId = null; T.sel = null; T.adding = null;
        await reload(false);
        if (!currentChar()) { T.charId = T.detail.characters[0]?.id || null; renderMain(); }
      },
    });
  }

  // ------------------------------------------------------------ render: centro (personajes + grillas)

  function renderMain() {
    const root = $('#acc-main');
    if (!T.detail) { root.innerHTML = '<div class="empty" style="margin-top:100px">Elegí una cuenta.</div>'; return; }
    const d = T.detail;
    const ch = currentChar();
    root.innerHTML = `
      <div class="acc-head">
        <div><h2>${esc(d.account.loginName)}</h2>
          <div class="muted" style="font-size:12px">${d.characters.length} personaje${d.characters.length === 1 ? '' : 's'} · ${d.account.eMail ? esc(d.account.eMail) + ' · ' : ''}registrada ${d.account.registrationDate ? new Date(d.account.registrationDate).toLocaleDateString() : '—'}${d.account.isVaultExtended ? ' · baúl extendido' : ''}</div></div>
        <div style="display:flex;gap:6px;align-items:center">
          ${S.status?.serverRunning ? '<div class="note warn" style="margin:0 8px 0 0;padding:6px 10px">OpenMU está corriendo: editá solo personajes desconectados.</div>' : ''}
          <button class="btn sm" id="acc-edit">Editar cuenta</button><button class="btn sm danger" id="acc-del">Borrar cuenta</button>
        </div>
      </div>
      <div class="char-cards">${d.characters.map((c) => `
        <button class="char-card ${c.id === T.charId ? 'active' : ''}" data-id="${c.id}">
          <span class="cls">${CLASS_ICON[c.classNumber] || '?'}</span>
          <span class="nm">${esc(c.name)}</span>
          <span class="sub">${esc(c.className)} · Lv ${c.level}${c.masterLevel ? ' (ML ' + c.masterLevel + ')' : ''}</span>
          <span class="sub">${esc(c.mapName || '?')} · ${c.money.toLocaleString()} zen · ${c.items.length} items</span>
        </button>`).join('')}
        ${d.characters.length < 5 ? '<button class="char-card new" id="char-new"><span class="nm">+ Nuevo personaje</span><span class="sub">slot libre: ' + (5 - d.characters.length) + '</span></button>' : ''}
      </div>
      ${ch ? renderCharacter(ch) : ''}
      ${d.vaultId ? renderStorage('vault', d.vaultId, d.vault, 0, vaultRows(), `Baúl${d.account.isVaultExtended ? ' (extendido)' : ''}`, d.vaultMoney) : '<div class="note warn">La cuenta no tiene baúl creado todavía (se crea al entrar al juego por primera vez).</div>'}`;
    $$('.char-card[data-id]', root).forEach((el) => el.onclick = () => { T.charId = el.dataset.id; T.sel = null; T.adding = null; renderMain(); renderEditor(); });
    const cn = $('#char-new', root); if (cn) cn.onclick = () => newCharacter().catch((e) => toast(e.message, 'bad'));
    $('#acc-edit', root).onclick = editAccount;
    $('#acc-del', root).onclick = deleteAccount;
    const ce = $('#char-edit', root); if (ce) ce.onclick = editCharacter;
    const cd = $('#char-del', root); if (cd) cd.onclick = deleteCharacter;
    bindGrids(root);
  }

  function renderCharacter(ch) {
    const st = ch.stats || {};
    const stat = (k) => st[k] !== undefined ? Math.round(st[k]) : '—';
    const equip = EQUIP.map(([slot, label]) => {
      const it = ch.items.find((i) => i.slot === slot);
      return `<div class="eq-slot ${it ? 'filled ' + itemKind(it) : ''}" data-drop-storage="${ch.inventoryId}" data-drop-slot="${slot}" data-kind="inv" title="${label}${it ? '\n' + esc(itemTitle(it)) : ''}">
        ${it ? cellItem(it, 'inv', ch.id, true) : `<span class="eq-label">${label}</span>`}</div>`;
    }).join('');
    return `
      <div class="section"><h3>${esc(ch.name)} · equipo <span class="line"></span>
        <button class="btn sm" id="char-edit">Editar personaje</button><button class="btn sm danger" id="char-del">Borrar</button>
        <span class="muted" style="text-transform:none;letter-spacing:0;font-weight:400">STR ${stat('Base Strength')} · AGI ${stat('Base Agility')} · VIT ${stat('Base Vitality')} · ENE ${stat('Base Energy')}${st['Base Leadership'] !== undefined ? ' · CMD ' + stat('Base Leadership') : ''} · puntos libres ${ch.levelUpPoints}${ch.playerKillCount ? ' · PK ' + ch.playerKillCount : ''}</span></h3>
        <div class="equip">${equip}</div>
      </div>
      ${renderStorage('inv', ch.inventoryId, ch.items, 12, invRows(ch), 'Inventario', ch.money, ch.id)}`;
  }

  function renderStorage(kind, storageId, items, base, rows, title, money, charId) {
    const grid = occupancy(items, base, rows);
    const cells = [];
    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < 8; c++) {
        const slot = base + r * 8 + c;
        const it = grid[r][c];
        const origin = it && it.slot === slot;
        cells.push(`<div class="cell ${it ? 'occ' : ''} ${slot === T.adding?.slot && storageId === T.adding?.storageId ? 'adding' : ''}" data-drop-storage="${storageId}" data-drop-slot="${slot}" data-kind="${kind}" data-char="${charId || ''}" style="grid-column:${c + 1};grid-row:${r + 1}">
          ${origin ? cellItem(it, kind, charId) : ''}</div>`);
      }
    }
    return `
      <div class="section"><h3>${esc(title)} <span class="line"></span>
        <label class="money">zen <input type="number" min="0" max="2000000000" value="${money}" data-money="${storageId}"><button class="btn sm" data-save-money="${storageId}">Guardar</button></label>
        <button class="btn sm" data-add-first="${storageId}" data-kind="${kind}" data-char="${charId || ''}">+ Agregar item</button></h3>
        <div class="inv-grid" style="grid-template-rows:repeat(${rows}, var(--cell))">${cells.join('')}</div>
      </div>`;
  }

  function cellItem(it, kind, charId, inEquip = false) {
    const url = thumb(it);
    const selected = T.sel && T.sel.item.id === it.id;
    return `<div class="item ${itemKind(it)} ${selected ? 'sel' : ''} ${url ? 'has-thumb' : ''}" draggable="true" data-item="${it.id}" data-kind="${kind}" data-char="${charId || ''}" data-thumb="${it.group * 512 + it.number}"
        style="${inEquip ? '' : `width:calc(var(--cell) * ${it.width});height:calc(var(--cell) * ${it.height});`}${url ? `background-image:url(${url})` : ''}" title="${esc(itemTitle(it))}">
      <span class="lvl">${it.level ? '+' + it.level : ''}</span>
      <span class="nm">${esc(shortName(it.name))}</span>
    </div>`;
  }

  function bindGrids(root) {
    // seleccion / agregar
    $$('.item', root).forEach((el) => {
      el.addEventListener('click', (e) => { e.stopPropagation(); selectItem(el.dataset.item); });
      el.addEventListener('dragstart', (e) => { e.dataTransfer.setData('text/plain', el.dataset.item); e.dataTransfer.effectAllowed = 'move'; el.classList.add('dragging'); });
      el.addEventListener('dragend', () => el.classList.remove('dragging'));
    });
    $$('[data-drop-slot]', root).forEach((cell) => {
      cell.addEventListener('click', () => {
        if (cell.classList.contains('occ') || cell.classList.contains('filled')) return;
        startAdd(cell.dataset.dropStorage, +cell.dataset.dropSlot, cell.dataset.kind, cell.dataset.char);
      });
      cell.addEventListener('dragover', (e) => { e.preventDefault(); e.dataTransfer.dropEffect = 'move'; cell.classList.add('over'); });
      cell.addEventListener('dragleave', () => cell.classList.remove('over'));
      cell.addEventListener('drop', async (e) => {
        e.preventDefault(); cell.classList.remove('over');
        const id = e.dataTransfer.getData('text/plain');
        if (!id) return;
        try {
          await api(`/inventory/items/${id}/move`, { method: 'POST', body: { storageId: cell.dataset.dropStorage, slot: +cell.dataset.dropSlot } });
          await reload();
          toast('Movido.', 'ok', 1500);
        } catch (err) { toast(err.message, 'bad'); }
      });
    });
    $$('[data-save-money]', root).forEach((btn) => btn.onclick = async () => {
      const input = $(`[data-money="${btn.dataset.saveMoney}"]`, root);
      try { await api(`/storages/${btn.dataset.saveMoney}/money`, { method: 'PUT', body: { money: +input.value || 0 } }); toast('Zen guardado.', 'ok'); await reload(); }
      catch (e) { toast(e.message, 'bad'); }
    });
    $$('[data-add-first]', root).forEach((btn) => btn.onclick = () => startAdd(btn.dataset.addFirst, null, btn.dataset.kind, btn.dataset.char));
  }

  function findItem(id) {
    for (const c of T.detail.characters) { const it = c.items.find((i) => i.id === id); if (it) return { item: it, kind: 'inv', charId: c.id, storageId: c.inventoryId }; }
    const it = T.detail.vault.find((i) => i.id === id);
    return it ? { item: it, kind: 'vault', charId: null, storageId: T.detail.vaultId } : null;
  }
  function selectItem(id) {
    const f = findItem(id);
    if (!f) return;
    T.sel = f; T.adding = null;
    T.draft = draftFrom(f.item);
    renderMain(); renderEditor();
  }
  function startAdd(storageId, slot, kind, charId) {
    T.sel = null;
    T.adding = { storageId, slot, kind, charId: charId || null };
    T.draft = { definitionId: null, storageId, slot, level: 0, durability: 0, hasSkill: false, socketCount: 0, storePrice: null, petExperience: 0, options: [], itemOfItemSetId: null };
    renderMain(); renderEditor();
  }
  function draftFrom(it) {
    return {
      definitionId: it.definitionId, storageId: it.storageId, slot: it.slot, level: it.level, durability: it.durability, hasSkill: it.hasSkill,
      socketCount: it.socketCount, storePrice: it.storePrice, petExperience: it.petExperience,
      options: it.options.map((o) => ({ itemOptionId: o.itemOptionId, level: o.level, index: o.index })), itemOfItemSetId: it.itemOfItemSetId,
    };
  }

  // ------------------------------------------------------------ render: editor (panel derecho)

  async function renderEditor() {
    const ed = $('#editor');
    if (!T.sel && !T.adding) {
      ed.innerHTML = '<div class="empty" style="margin-top:120px">Hacé clic en un item para editarlo, o en una celda vacía para agregar uno.<br><br><span class="muted" style="font-size:12px">También podés arrastrar items entre inventario, equipo y baúl.</span></div>';
      return;
    }
    const d = T.draft;
    const def = d.definitionId ? S.byId.get(d.definitionId) : null;
    const title = T.sel ? `${T.sel.item.name} +${T.sel.item.level}` : 'Agregar item';
    const where = T.adding ? T.adding : T.sel;
    const whereLabel = where.kind === 'vault' ? 'baúl' : `inventario de ${T.detail.characters.find((c) => c.id === where.charId)?.name || '?'}`;
    ed.innerHTML = `
      <div class="editor-head">
        <div class="editor-title"><h2>${esc(title)}</h2><span class="gn">${whereLabel} · slot ${d.slot ?? '(primer libre)'}</span></div>
        <div style="height:10px"></div>
      </div>
      <div class="viewer" id="viewer">
        <div class="viewer-info" id="viewer-info">${def ? '' : 'elegí un item'}</div>
        <div class="viewer-tools"><button class="btn sm" id="v-rotate" title="Girar">⟳</button><button class="btn sm" id="v-fit" title="Centrar">⤢</button></div>
      </div>
      <div class="editor-body" id="ed-body">
        <div class="section"><h3>Item <span class="line"></span></h3>
          <div class="field"><label>Definición</label><div id="def-combo"></div></div>
          <div class="grid" style="margin-top:10px">
            <div class="field"><label>Nivel (+)</label><input type="number" id="f-level" min="0" max="15" value="${d.level}"></div>
            <div class="field"><label>Durabilidad / cantidad</label><input type="number" id="f-dur" min="0" max="255" step="any" value="${d.durability}"></div>
            <div class="field"><label>Sockets</label><input type="number" id="f-sockets" min="0" max="5" value="${d.socketCount}"></div>
            <div class="field"><label>Slot</label><input type="number" id="f-slot" min="0" max="255" value="${d.slot ?? ''}" placeholder="primer libre"></div>
          </div>
          <div style="margin-top:6px"><label class="check"><input type="checkbox" id="f-skill" ${d.hasSkill ? 'checked' : ''}> Skill</label></div>
        </div>
        <div class="section" id="opt-section"><h3>Opciones <span class="line"></span></h3><div id="opt-body" class="muted" style="font-size:12px">${def ? 'cargando…' : 'elegí primero la definición'}</div></div>
      </div>
      <div class="editor-foot">
        <span class="hint" id="ed-hint"></span>
        ${T.sel ? `<button class="btn" id="b-dup" title="Crear una copia en el primer lugar libre">Duplicar</button>
        <button class="btn" id="b-tovault">${where.kind === 'vault' ? 'Al inventario' : 'Al baúl'}</button>
        <button class="btn danger" id="b-del">Borrar</button>` : ''}
        <button class="btn primary" id="b-save">${T.sel ? 'Guardar' : 'Agregar'}</button>
      </div>`;

    combo($('#def-combo'), {
      items: S.items.map((i) => ({ id: i.id, label: `${i.name}`, sub: `${i.group}/${i.number} · ${i.width}×${i.height}` })),
      value: d.definitionId || '', placeholder: 'Buscar item…',
      onChange: async (id) => { d.definitionId = id; d.options = []; d.itemOfItemSetId = null; await loadDefOptions(true); },
    });
    $('#f-level').oninput = (e) => { d.level = +e.target.value || 0; };
    $('#f-dur').oninput = (e) => { d.durability = +e.target.value || 0; };
    $('#f-sockets').oninput = (e) => { d.socketCount = +e.target.value || 0; };
    $('#f-slot').oninput = (e) => { d.slot = e.target.value === '' ? null : +e.target.value; };
    $('#f-skill').onchange = (e) => { d.hasSkill = e.target.checked; };
    $('#b-save').onclick = save;
    if (T.sel) {
      $('#b-del').onclick = del;
      $('#b-dup').onclick = duplicate;
      $('#b-tovault').onclick = () => moveTo(where.kind === 'vault' ? 'inv' : 'vault');
    }
    if (def) {
      mountViewerFor(def);
      await loadDefOptions(false);
    }
  }

  function mountViewerFor(def) {
    const box = $('#viewer'); if (!box || typeof Viewer === 'undefined') return;
    Viewer.mount(box, def.group * 512 + def.number, (m) => {
      const info = $('#viewer-info'); if (!info) return;
      info.innerHTML = m && m.file ? `<span class="mono">${esc(m.file)}</span>` : 'sin modelo en el cliente';
    });
    $('#v-rotate').onclick = (e) => e.currentTarget.classList.toggle('primary', Viewer.toggleRotate());
    $('#v-fit').onclick = () => Viewer.fit();
  }

  async function loadDefOptions(fresh) {
    const d = T.draft;
    if (!d.definitionId) return;
    const def = S.byId.get(d.definitionId);
    if (T.defOptionsFor !== d.definitionId) {
      T.defOptions = await api(`/definitions/${d.definitionId}/options`);
      T.defOptionsFor = d.definitionId;
    }
    if (fresh) {
      d.durability = T.defOptions.durability || 1;
      d.socketCount = 0;
      d.hasSkill = false;
      $('#f-dur').value = d.durability; $('#f-sockets').value = 0; $('#f-skill').checked = false;
      mountViewerFor(def);
    }
    renderOptions();
  }

  function renderOptions() {
    const d = T.draft, o = T.defOptions, body = $('#opt-body');
    if (!o) return;
    body.classList.remove('muted');
    const byType = new Map();
    for (const opt of o.options) { if (!byType.has(opt.typeName)) byType.set(opt.typeName, []); byType.get(opt.typeName).push(opt); }
    const has = (id) => d.options.find((x) => x.itemOptionId === id);
    let html = '';
    if (!o.hasSkill) html += '<div class="muted" style="font-size:12px;margin-bottom:6px">Este item no tiene skill.</div>';
    for (const [type, opts] of byType) {
      const single = opts.length === 1 && (type.startsWith('Luck') || type === 'Option');
      html += `<div class="opt-group"><h5>${esc(type)}</h5>`;
      for (const opt of opts) {
        const cur = has(opt.id);
        const isLevelled = opt.maxLevel > 1 || type === 'Option';
        const maxLevel = type === 'Option' ? Math.max(opt.maxLevel, 4) : opt.maxLevel;
        html += `<label class="check opt-row"><input type="checkbox" data-opt="${opt.id}" ${cur ? 'checked' : ''}>
          <span>${esc(opt.label || (single ? type : `#${opt.number}`))}</span>
          ${isLevelled ? `<select data-opt-level="${opt.id}" ${cur ? '' : 'disabled'}>${Array.from({ length: maxLevel }, (_, i) => i + 1).map((l) => `<option value="${l}" ${cur && cur.level === l ? 'selected' : (!cur && l === maxLevel && type === 'Option' ? 'selected' : '')}>+${l}</option>`).join('')}</select>` : ''}
        </label>`;
      }
      html += '</div>';
    }
    if (o.ancientSets.length) {
      html += `<div class="opt-group"><h5>Ancient</h5><label class="check opt-row"><span>Set</span>
        <select id="f-ancient"><option value="">— no es ancient —</option>${o.ancientSets.map((a) => `<option value="${a.itemOfItemSetId}" ${d.itemOfItemSetId === a.itemOfItemSetId ? 'selected' : ''}>${esc(a.setName)}${a.discriminator > 1 ? ' (' + a.discriminator + ')' : ''}</option>`).join('')}</select>
        <select id="f-ancient-level"><option value="1">+5</option><option value="2">+10</option></select></label></div>`;
    }
    body.innerHTML = html || '<span class="muted">Este item no admite opciones.</span>';

    $$('[data-opt]', body).forEach((cb) => cb.onchange = () => {
      const id = cb.dataset.opt;
      const sel = $(`[data-opt-level="${id}"]`, body);
      if (cb.checked) { d.options.push({ itemOptionId: id, level: sel ? +sel.value : 0, index: 0 }); if (sel) sel.disabled = false; }
      else { d.options = d.options.filter((x) => x.itemOptionId !== id); if (sel) sel.disabled = true; }
    });
    $$('[data-opt-level]', body).forEach((sel) => sel.onchange = () => {
      const x = d.options.find((y) => y.itemOptionId === sel.dataset.optLevel); if (x) x.level = +sel.value;
    });
    const anc = $('#f-ancient', body);
    if (anc) {
      const lvl = $('#f-ancient-level', body);
      const bonusIds = new Set(o.ancientSets.map((a) => a.bonusOptionId));
      const curBonus = d.options.find((x) => bonusIds.has(x.itemOptionId));
      if (curBonus) lvl.value = String(curBonus.level || 1);
      const apply = () => {
        d.options = d.options.filter((x) => !bonusIds.has(x.itemOptionId));
        d.itemOfItemSetId = anc.value || null;
        const set = o.ancientSets.find((a) => a.itemOfItemSetId === anc.value);
        if (set && set.bonusOptionId) d.options.push({ itemOptionId: set.bonusOptionId, level: +lvl.value, index: 0 });
      };
      anc.onchange = apply; lvl.onchange = apply;
    }
  }

  // ------------------------------------------------------------ acciones

  function targetSlot(storageId, kind, charId, w, h) {
    if (kind === 'vault') return firstFree(T.detail.vault, 0, vaultRows(), w, h);
    const ch = T.detail.characters.find((c) => c.id === charId);
    return ch ? firstFree(ch.items, 12, invRows(ch), w, h) : null;
  }

  async function save() {
    const d = T.draft;
    if (!d.definitionId) { toast('Elegí la definición del item.', 'warn'); return; }
    const def = S.byId.get(d.definitionId);
    const where = T.adding || T.sel;
    if (d.slot === null || d.slot === undefined) {
      d.slot = targetSlot(where.storageId, where.kind, where.charId, def.width, def.height);
      if (d.slot === null) { toast('No hay lugar libre.', 'bad'); return; }
    }
    // sockets: index consecutivo para las opciones de socket
    let si = 0; d.options.forEach((o) => { const t = T.defOptions?.options.find((x) => x.id === o.itemOptionId); if (t && t.typeName.startsWith('Socket')) o.index = si++; });
    try {
      if (T.sel) { await api(`/inventory/items/${T.sel.item.id}`, { method: 'PUT', body: d }); toast('Guardado.', 'ok'); }
      else { const created = await api('/inventory/items', { method: 'POST', body: d }); toast(`Agregado ${created.name} +${created.level}.`, 'ok'); T.adding = null; T.sel = { item: created, kind: where.kind, charId: where.charId, storageId: created.storageId }; }
      await reload();
    } catch (e) { toast(e.message, 'bad', 6000); }
  }
  async function del() {
    const it = T.sel.item;
    modal({
      title: `Borrar ${it.name} +${it.level}`, body: '<p>Se borra del inventario/baúl. No hay deshacer.</p>', okText: 'Borrar', okClass: 'danger',
      onOk: async () => { await api(`/inventory/items/${it.id}`, { method: 'DELETE' }); T.sel = null; toast('Borrado.', 'ok'); await reload(false); },
    });
  }
  async function duplicate() {
    const it = T.sel.item, where = T.sel;
    const slot = targetSlot(where.storageId, where.kind, where.charId, it.width, it.height);
    if (slot === null) { toast('No hay lugar libre.', 'bad'); return; }
    try { await api('/inventory/items', { method: 'POST', body: { ...draftFrom(it), slot } }); toast('Duplicado.', 'ok'); await reload(); }
    catch (e) { toast(e.message, 'bad'); }
  }
  async function moveTo(kind) {
    const it = T.sel.item;
    let storageId, slot;
    if (kind === 'vault') { if (!T.detail.vaultId) { toast('La cuenta no tiene baúl.', 'bad'); return; } storageId = T.detail.vaultId; slot = targetSlot(storageId, 'vault', null, it.width, it.height); }
    else { const ch = currentChar(); storageId = ch.inventoryId; slot = targetSlot(storageId, 'inv', ch.id, it.width, it.height); }
    if (slot === null) { toast('No hay lugar libre.', 'bad'); return; }
    try { await api(`/inventory/items/${it.id}/move`, { method: 'POST', body: { storageId, slot } }); toast('Movido.', 'ok'); await reload(); }
    catch (e) { toast(e.message, 'bad'); }
  }

  return {
    async enter() {
      if (!T.accounts.length) await loadAccounts(); else renderSide();
      renderMain(); renderEditor();
    },
    search(q) { T.q = q; renderSide(); },
    reload,
  };
})();
