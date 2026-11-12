'use strict';
// Interface HTML de Dofus Retro Manager. La page n'a pas de logique métier : elle affiche l'état des contrôles WPF
// (reçu par messages, clé = nom x:Name du contrôle) et leur renvoie les actions de l'utilisateur.

const S = {};                                 // état reçu
const local = { sort: {}, limit: {}, open: {} }; // tri, pagination et arbres dépliés, propres à la page
const post = m => window.chrome.webview.postMessage(m);

// Contrôles suivis, par onglet (les autres onglets s'ouvrent dans l'interface classique)
const COMMON = ['VersionText', 'CaptureDot', 'CaptureStatus', 'BankStatus', 'CaptureButton', 'AdminButton', 'UpdateBanner',
  'UpdateTitle', 'UpdateText', 'UpdateActionButton', 'UpdateLaterButton', 'UpdateNotesButton', 'MainTabs'];
const TABS = [
  { key: 'bank', label: 'Banque', watch: ['BankOwner', 'BankCharacter', 'BankSearch', 'BankTypeFilter', 'BankCount', 'BankValue', 'BankGrid'] },
  { key: 'recipes', label: 'Recettes', watch: ['RecipeSearch', 'JobFilter', 'ModeFilter', 'SellMinPanel', 'SellMin',
    'LevelMin', 'LevelMax', 'SlotFilters', 'IncludeInventory', 'InventoryHint', 'RecipeCount', 'IngredientChip', 'IngredientChipText',
    'RecipeGrid', 'DetailTitle', 'DetailSub', 'DetailCostPanel', 'DetailCraftCount', 'DetailCraftMax', 'DetailCostTitle', 'DetailCost',
    'DetailCostNote', 'DetailProfit', 'DetailProfitOwned', 'DetailStatsPanel', 'DetailStats', 'DetailIngredients'] },
  { key: 'brisage', label: 'Brisage', watch: ['BrisageSearch', 'BrisageSource', 'BrisageTypeFilter', 'BrisageLevelMin',
    'BrisageLevelMax', 'BrisageCount', 'BrisageStats', 'BrisageGrid', 'BrisageGainColumn', 'BrisageEmpty', 'BrisageDetailTitle',
    'BrisageDetailSub', 'BrisageGoRecipe', 'BrisageBreakCount', 'BrisageBreakMax', 'BrisageDetailTotal', 'BrisageRunes'] },
  { key: 'hdv', label: 'HDV', watch: ['HdvSearch', 'HdvTypeFilter', 'HdvShowAll', 'HdvCount', 'HdvGrid', 'HdvEmpty',
    'HdvDetailTitle', 'HdvDetailSub', 'HdvChart', 'HdvHistory', 'HdvForget',
    'ShareToggle', 'ShareSync', 'SharePanel', 'ShareEnabled', 'ShareUrl', 'ShareKey', 'ShareTest', 'ShareSave', 'ShareStatus'] },
  { key: 'ttg', label: 'TTG', watch: ['TtgSearch', 'TtgRarity', 'TtgCharacter', 'TtgMissing', 'TtgShow', 'TtgShowAll', 'TtgDaysBox', 'TtgCount',
    'TtgCollection', 'TtgGrid', 'TtgBestColumn', 'TtgEmpty', 'TtgDetailTitle', 'TtgDetailSub', 'TtgOffersMode', 'TtgOffersTitle',
    'TtgOffers', 'TtgMerchantChart'] },
  { key: 'pda', label: 'PDA', watch: ['PdaSearch', 'PdaDaysBox', 'PdaSource', 'PdaCharacter', 'PdaCategory', 'PdaShowAll', 'PdaCount', 'PdaValue',
    'PdaGrid', 'PdaEmpty', 'PdaDetailTitle', 'PdaDetailSub', 'PdaChart'] },
  { key: 'ocre', label: 'Ocre', watch: ['OcreCharacter', 'OcreStep', 'OcreKindFilter', 'OcreSummary', 'OcreGrid', 'OcreDetailTitle',
    'OcreDetailSub', 'OcreSouls'] },
  { key: 'combat', label: 'Combat', watch: ['HuntCharacterBox', 'HuntIcons', 'HuntLevel', 'HuntGroupLevel', 'HuntWisdom', 'HuntProspection',
    'HuntGroupProspection', 'HuntSound', 'HuntVolume', 'HuntVolumeText', 'HuntStatus', 'HuntAlertBanner', 'OverlayEnabled', 'OverlayEconome', 'HuntMap', 'HuntGroupGrid', 'HuntEmpty',
    'HuntSession', 'HuntPauseSession', 'HuntProjection', 'HuntLearned', 'HuntArchiGrid', 'HuntFightGrid', 'HuntFightsGroup', 'HuntDropsTitle', 'HuntDropGrid', 'HuntArchiPopup',
    'FarmItem', 'FarmSuggest', 'FarmTarget', 'FarmStatus', 'FarmState',
    'HuntAlertsToggle', 'HuntAlertMonster', 'HuntAlertSuggest', 'HuntAlertCount', 'HuntAlertMin', 'HuntAlertMax', 'HuntAlertStatus', 'HuntAlertList',
    'ChronoTime', 'ChronoStart', 'ChronoReset', 'ChronoFights', 'ChronoGains', 'ChronoRate', 'ChronoGrid', 'AreneChallenges',
    'AreneOtherChallenges', 'AreneGroup', 'AreneGroupEdit', 'AreneGroupName', 'AreneGroupStatus', 'AreneGroupMembers', 'HuntSessionGrid', 'HuntSessionDetailTitle', 'HuntSessionChars', 'HuntSessionDrops', 'HuntSessionMobs', 'HuntSessionFights'] },
  { key: 'windows', label: 'Switcheur', watch: ['OrganizerEnabled', 'HotkeyDebounce', 'OrganizerClickSwitch', 'OrganizerClickKey', 'OrganizerClickLeft', 'OrganizerClickRight', 'OrganizerFixBounce', 'NotifyAutoSwitchTurn', 'OrganizerSplitMode', 'OrganizerMaximize', 'OrganizerLockOrder', 'HotkeyNext', 'HotkeyPrevious', 'HotkeyMain', 'HotkeySadida', 'SadidaReturn', 'HotkeyStatus', 'NotifyFight', 'NotifyTurn',
    'NotifyExchange', 'NotifyParty', 'NotifyToast', 'NotifyFlash', 'NotifySound', 'NotifyAutoSwitch', 'OrganizerCount', 'OrganizerGrid', 'OverlayEnabled', 'OverlayEconome', 'OverlayGroups', 'OverlayHarvest',
    'OverlayRotation', 'OverlayFmHistory', 'OverlayFmChances', 'OverlayOpacity', 'OverlayOpacityText', 'OverlayMove', 'OrganizerSaveOrder', 'OrganizerResetOrder',
    'OrganizerOrderStatus'] },
  { key: 'fm', label: 'FM', watch: ['FmItemCombo', 'FmItemName', 'FmReliquat', 'FmReliquatInput', 'FmStats', 'FmItemSub', 'FmItemTpl', 'FmChances', 'FmChancesNote',
    'FmHistoryGrid', 'FmItemsGrid', 'FmEmpty', 'FmWeights', 'FmWeightsPanel', 'FmWeightsGrid', 'OverlayEnabled', 'OverlayFmHistory', 'OverlayFmHistoryCount', 'OverlayFmChances'] },
  { key: 'carte', label: 'Carte', watch: ['MiniMapEnabled', 'MiniMapMove', 'CarteHarvestTitle', 'CarteHarvest', 'CarteHarvestList', 'CarteCellPanel', 'CarteCellTitle',
    'CarteCellInfo', 'CarteCellHarvest', 'CarteCellList'] },
  { key: 'journal', label: 'Journal', watch: ['LogBox', 'LogPacketsBox'] },
  // Ajoutés après coup : leur place dans le menu est donnée par GROUPS, pas par cet ordre (index de MainTabs)
  { key: 'patrimoine', label: 'Patrimoine', watch: ['_patrimoine'] },
  { key: 'metier', label: 'Métier', watch: ['MetierJob', 'MetierFrom', 'MetierTo', 'MetierResell', '_metier'] },
  { key: 'elevage', label: 'Élevage', watch: ['_elevage'] },
];
// Contrôles suivis : l'en-tête et l'onglet affiché seulement (et le livre TTG quand il est ouvert) ; l'application ne
// relit et n'envoie que ceux-là. La liste est renvoyée à chaque changement d'onglet.
let watching = '';
function watchTab(active) {
  const names = COMMON.concat(TABS[active]?.watch || [], local.book ? ['_ttgBook'] : [], local.trades && TABS[active]?.key === 'hdv' ? ['_trades'] : []);
  const key = names.join(',');
  if (key === watching) return;
  watching = key;
  post({ type: 'watch', names });
}
watchTab(0);

// --- Utilitaires ---
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
// Milliers séparés par une espace insécable ordinaire (l'espace fine du format français n'existe pas dans la police)
const fmt = n => n == null ? '—' : Math.round(n).toLocaleString('fr-FR').replace(/\s/g, ' ');
// Montant en kamas : nombre suivi du symbole des kamas du jeu
const KAMA = '<img class="kama" src="icon/stat/Kama.png" alt="" title="kamas">';
const kamas = n => n == null ? '—' : `<span class="kamas">${fmt(n)}${KAMA}</span>`;
const pct = n => n == null ? '—' : (n > 0 ? '+' : '') + Math.round(n * 100) + ' %';
const T = n => S[n]?.t ?? '';
const shown = n => S[n] != null && S[n].v !== false;
const rows = n => S[n]?.rows || [];
const itemImg = (id, cls = '') => `<img class="ico ${cls}" src="icon/item/${id}.png" alt="" loading="lazy">`;
const attrs = o => Object.entries(o).map(([k, v]) => `data-${k}="${esc(v)}"`).join(' ');

function select(name, extra = '') {
  const s = S[name];
  if (!s?.items) return '';
  return `<select data-set="${name}" ${extra}>${s.items.map((o, i) => `<option value="${i}" ${i === s.i ? 'selected' : ''}>${esc(o)}</option>`).join('')}</select>`;
}
const input = (name, placeholder = '', w = '') =>
  `<input type="search" data-set="${name}" data-fk="${name}" value="${esc(T(name))}" placeholder="${esc(placeholder)}" ${w ? `style="width:${w}"` : ''}>`;
const check = (name, label) => `<label class="check"><input type="checkbox" data-set="${name}" ${S[name]?.on ? 'checked' : ''}>${esc(label)}</label>`;
const btn = (name, label, cls = '') => shown(name) || !S[name]
  ? `<button class="${cls}" data-act="click" data-name="${name}" ${S[name]?.e === false ? 'disabled' : ''}>${esc(S[name]?.t || label)}</button>` : '';
const colored = name => `<span style="${S[name]?.c ? `color:${S[name].c}` : ''}">${esc(T(name))}</span>`;


// Tableau triable : cols = [{ h, k (clé de tri), f (rendu), num }] ; clic sur une ligne -> sélection côté WPF
function table(name, cols, opts = {}) {
  const data = rows(name).map((r, i) => ({ ...r, _i: i }));
  const sort = local.sort[name];
  if (sort) {
    const dir = sort.dir;
    data.sort((a, b) => {
      const x = a[sort.k], y = b[sort.k];
      if (x == null && y == null) return 0;
      if (x == null) return 1;
      if (y == null) return -1;
      return (typeof x === 'number' ? x - y : String(x).localeCompare(String(y), 'fr')) * dir;
    });
  }
  const limit = local.limit[name] || 300;
  const sel = S[name]?.sel ?? -1;
  const head = cols.map(c => `<th class="${c.num ? 'num' : ''}" ${c.k ? attrs({ act: 'sort', grid: name, k: c.k }) : ''}>${esc(c.h)}${sort && sort.k === c.k ? `<span class="arrow">${sort.dir > 0 ? '▲' : '▼'}</span>` : ''}</th>`).join('');
  const body = data.slice(0, limit).map(r => {
    const drag = opts.draggable?.(r) ? `draggable="true" data-drag-grid="${name}" data-drag-i="${r._i}"` : '';
    const tr = `<tr class="${opts.select !== false ? 'clickable' : ''} ${r._i === sel ? 'sel' : ''} ${drag ? 'draggable' : ''}" ${drag} ${opts.select !== false ? attrs({ act: 'select', grid: name, i: r._i }) : ''}>`
      + cols.map(c => `<td class="${c.num ? 'num' : ''} ${c.cls || ''}">${c.f ? c.f(r) : esc(r[c.k])}</td>`).join('') + '</tr>';
    return tr + (opts.after ? opts.after(r, cols.length) : '');
  }).join('');
  const more = data.length > limit ? `<button class="more ghost" data-act="more" data-grid="${name}">Afficher plus (${data.length - limit} restantes)</button>` : '';
  const empty = data.length === 0 ? `<div class="empty">${esc(opts.empty || 'Rien à afficher.')}</div>` : '';
  return `<div class="table-wrap"><table><thead><tr>${head}</tr></thead><tbody>${body}</tbody></table>${empty}${more}</div>`;
}

// Position cliquable (TTG, Combat) : ouvre l'onglet Carte sur sa case ; grisée si la map est inconnue
/** « [12,11] Amakna (Clairière de Brouce Boulgoure) » -> « [12,11] Amakna » ; « 01/11/2026 » -> « 01/11 ». */
const shortMap = t => String(t || '').replace(/\s*\(.*\)\s*$/, '');
const shortDate = t => /^\d\d\/\d\d\/\d{4}$/.test(t || '') ? t.slice(0, 5) : t;
const posLink = (text, go, cls = '') => !text ? '' : go
  ? `<a class="pos-link ${cls}" data-act="goto" data-g="${esc(JSON.stringify(go))}" title="Voir sur la carte : ${esc(text)}">${esc(text)}</a>`
  : `<span class="muted ${cls}" title="${esc(text)}">${esc(text)}</span>`;

// Courbe de prix en SVG (points [ms, prix]) : une ligne par le meilleur prix du jour, chaque relevé en point (détail au
// survol), le dernier mis en avant. Les prix aberrants (plus de 3 fois le prix courant) ne tassent pas l'échelle : ils
// restent en haut, marqués ▲.
const fmtShort = n => n >= 1e6 ? (n / 1e6).toFixed(n < 1e7 ? 1 : 0).replace('.', ',').replace(',0', '') + ' M'
  : n >= 1e4 ? Math.round(n / 1e3) + 'k' : fmt(n);
// name : contrôle du graphique (HdvChart…) : bouton ⤢ pour l'agrandir dans une fenêtre ; big : graphique agrandi
function chart(points, name = '', big = false) {
  if (!points?.length) return '<div class="empty">Aucun relevé pour l\'instant.</div>';
  const W = big ? 900 : 360, H = big ? 380 : 170, L = 42, R = 12, Tp = 12, B = 22;
  const sorted = points.slice().sort((a, b) => a[0] - b[0]);
  const ps = sorted.map(p => p[1]).sort((a, b) => a - b);
  const q = k => ps[Math.min(ps.length - 1, Math.floor(k * (ps.length - 1)))];
  const cap = ps.length >= 3 ? q(.75) * 3 : Infinity;
  const kept = ps.filter(p => p <= cap);
  let p0 = kept[0], p1 = kept[kept.length - 1];
  if (p1 === p0) { p0 = p0 * .9; p1 = p1 * 1.1 || 1; }
  // Échelle « ronde » : 3 ou 4 graduations
  const raw = (p1 - p0) / 3, mag = 10 ** Math.floor(Math.log10(raw)), step = [1, 2, 2.5, 5, 10].map(m => m * mag).find(s => s >= raw);
  p0 = Math.max(0, Math.floor(p0 / step) * step); p1 = Math.ceil(p1 / step) * step;
  const t0 = sorted[0][0], t1 = sorted[sorted.length - 1][0];
  const x = t => L + (t1 === t0 ? (W - L - R) / 2 : (t - t0) / (t1 - t0) * (W - L - R));
  const y = p => Tp + (1 - (Math.min(p, p1) - p0) / (p1 - p0)) * (H - Tp - B);
  const d = t => new Date(t).toLocaleDateString('fr-FR', { day: '2-digit', month: '2-digit' });
  const dh = t => new Date(t).toLocaleString('fr-FR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });
  // Meilleur prix de chaque jour
  const days = new Map();
  for (const [t, p] of sorted) { const k = new Date(t).toDateString(), v = days.get(k); if (!v || p < v[1]) days.set(k, [t, p]); }
  const best = [...days.values()].filter(p => p[1] <= cap);
  const line = best.map(p => `${x(p[0]).toFixed(1)},${y(p[1]).toFixed(1)}`).join(' ');
  const area = best.length > 1 ? `<polygon class="area" points="${x(best[0][0]).toFixed(1)},${H - B} ${line} ${x(best[best.length - 1][0]).toFixed(1)},${H - B}"/>` : '';
  let grid = '';
  for (let p = p0; p <= p1 + step / 2; p += step)
    grid += `<line class="grid" x1="${L}" x2="${W - R}" y1="${y(p)}" y2="${y(p)}"/><text class="axis" x="${L - 5}" y="${y(p) + 3}" text-anchor="end">${fmtShort(p)}</text>`;
  const nx = t1 === t0 ? 1 : Math.min(big ? 10 : 4, days.size);
  for (let i = 0; i < nx; i++) {
    const t = nx === 1 ? t0 : t0 + (t1 - t0) * i / (nx - 1), a = nx === 1 ? 'middle' : i === 0 ? 'start' : i === nx - 1 ? 'end' : 'middle';
    grid += `<text class="axis" x="${x(t)}" y="${H - 6}" text-anchor="${a}">${d(t)}</text>`;
  }
  const last = sorted[sorted.length - 1];
  const dot = (p, cls) => {
    const out = p[1] > cap, cx = x(p[0]).toFixed(1), cy = out ? Tp : y(p[1]).toFixed(1);
    const tip = `<title>${dh(p[0])} : ${fmt(p[1])} k${out ? ' (hors échelle)' : ''}</title>`;
    return `<g class="pt ${cls}${out ? ' out' : ''}">${out ? `<path d="M${cx - 4},${+cy + 4}h8l-4,-7z"/>` : `<circle cx="${cx}" cy="${cy}" r="${cls ? 4 : 2.5}"/>`}
      <circle class="hit" cx="${cx}" cy="${cy}" r="8"/>${tip}</g>`;
  };
  const med = q(.5);
  const sum = `<div class="chart-sum"><span title="Prix le plus bas relevé">Min <b>${fmtShort(ps[0])}</b></span>
    <span title="Prix médian des relevés">Méd. <b>${fmtShort(med)}</b></span>
    <span title="Dernier relevé : ${dh(last[0])}">Dernier <b>${fmtShort(last[1])}</b></span>
    <span class="muted" title="Nombre de relevés">${sorted.length} rel.</span>${name && !big ? `<button class="pbtn" data-act="chart-zoom" data-name="${name}" data-tip="Agrandir le graphique">⤢</button>` : ''}</div>`;
  // Clic sur le graphique : agrandi dans une fenêtre (comme le bouton ⤢)
  return `${sum}<svg class="chart${big ? ' big' : ''}${name && !big ? ` zoomable" data-act="chart-zoom" data-name="${name}` : ''}" viewBox="0 0 ${W} ${H}">
    <defs><linearGradient id="chartGrad" x1="0" x2="1"><stop offset="0" stop-color="#8b5cf6"/><stop offset="1" stop-color="#06b6d4"/></linearGradient>
    <linearGradient id="chartFill" x1="0" x2="0" y1="0" y2="1"><stop offset="0" stop-color="#8b5cf6" stop-opacity=".3"/><stop offset="1" stop-color="#8b5cf6" stop-opacity="0"/></linearGradient></defs>
    ${grid}${area}${best.length > 1 ? `<polyline class="line" points="${line}"/>` : ''}
    ${sorted.slice(0, -1).map(p => dot(p, '')).join('')}${dot(last, 'last')}
  </svg>`;
}

// Graphique agrandi (bouton ⤢) : fenêtre par-dessus la page, fermée au changement d'onglet
function chartZoom() {
  const z = local.chartZoom;
  if (!z || z.tab !== (S.MainTabs?.i ?? 0)) return '';
  const title = { HdvChart: T('HdvDetailTitle'), PdaChart: T('PdaDetailTitle'), TtgMerchantChart: 'Prix de la carte' }[z.name] || 'Évolution du prix';
  return `<div class="modal-back" data-act="chart-close"><div class="modal glass chart-modal" data-act="none">
      <div class="row"><h2 class="ellipsis">${esc(title)}</h2><button class="small ghost right" data-act="chart-close">✕</button></div>
      <div class="modal-body">${chart(S[z.name]?.pts, z.name, true)}</div></div></div>`;
}

// --- Rendu ---
function header() {
  return `<header class="top glass">
    <img class="logo" src="logo.png" alt="DRM">
    <div class="brand">
      <h1><span class="grad">Dofus Retro Manager</span><small>${esc(T('VersionText'))}</small></h1>
      <div class="status"><i class="dot" style="background:${S.CaptureDot?.c || 'gray'};color:${S.CaptureDot?.c || 'gray'}"></i>
        <b>${esc(T('CaptureStatus'))}</b><span>·</span><span class="bank">${esc(T('BankStatus'))}</span></div>
    </div>
    <div class="actions">
      <div class="theme-picker" title="Style">${['Classique', 'Moderne'].map(s =>
        `<button class="${(S._style || 'Moderne') === s ? 'on' : ''}" data-act="style" data-v="${s}">${s}</button>`).join('')}</div>
      <div class="theme-picker" title="Thème">${[['Jour', '☀ Jour'], ['Crepuscule', '◐ Crépuscule'], ['Nuit', '☾ Nuit']].map(([t, l]) =>
        `<button class="${(S._theme || 'Crepuscule') === t ? 'on' : ''}" data-act="theme" data-v="${t}">${l}</button>`).join('')}</div>
      ${btn('AdminButton', 'Relancer en administrateur')}
      ${btn('CaptureButton', 'Démarrer la capture', 'primary')}
      <button data-act="changelog" title="Les changements de chaque version">Nouveautés</button>
    </div>
  </header>`;
}

// Journal des changements (CHANGELOG.md, servi par l'application sous changelog.js)
function changelog() {
  if (!local.changelog) return '';
  const log = window.CHANGELOG || { current: '', entries: [] };
  const inline = s => esc(s).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
  return `<div class="modal-back" data-act="changelog-close"><div class="modal glass" data-act="none">
      <div class="row"><h2>Nouveautés</h2><button class="small ghost right" data-act="changelog-close">✕</button></div>
      <div class="sub">Vous utilisez la version ${esc(log.current)}.</div>
      <div class="modal-body">${log.entries.length ? log.entries.map(e => `<div class="card changelog ${e.version === log.current ? 'current' : ''}">
          <div class="row"><b class="acc">Version ${esc(e.version)}</b>${e.version === log.current ? '<span class="chip accent">installée</span>' : ''}
            <span class="muted right">${esc(e.date)}</span></div>
          <ul>${e.items.map(i => `<li>${inline(i)}</li>`).join('')}</ul></div>`).join('')
        : '<div class="empty">Le journal des changements est introuvable.</div>'}</div>
    </div></div>`;
}


// Confirmation dans la page (ex. « Oublier l'objet » de l'onglet FM), puis l'action est envoyée à l'application
function confirmBox() {
  if (!local.confirm) return '';
  return `<div class="modal-back" data-act="confirm-close"><div class="modal glass confirm" data-act="none">
      <h2>Confirmer</h2><p>${esc(local.confirm.text)}</p>
      <div class="row"><span class="right"></span><button data-act="confirm-close">Annuler</button>
        <button class="primary" data-act="confirm-ok">Confirmer</button></div>
    </div></div>`;
}

function banner() {
  if (!shown('UpdateBanner')) return '';
  return `<div class="banner glass"><div class="txt"><b>${esc(T('UpdateTitle'))}</b><p>${esc(T('UpdateText'))}</p></div>
    ${btn('UpdateNotesButton', 'Nouveautés')}${btn('UpdateActionButton', 'Télécharger et installer', 'primary')}${btn('UpdateLaterButton', 'Plus tard')}</div>`;
}

// Icônes des onglets (SVG dessinés au trait, couleur du texte) et ce que fait chaque onglet (infobulle)
const TAB_ICONS = {
  bank: '<rect x="3" y="9" width="18" height="11" rx="2"/><path d="M3 9a4 4 0 0 1 4-4h10a4 4 0 0 1 4 4"/><path d="M3 13h18"/><rect x="10.5" y="11.5" width="3" height="4" rx="1"/>',
  recipes: '<path d="M12 6.5C10 5 7 4.5 4 5v14c3-.5 6 0 8 1.5 2-1.5 5-2 8-1.5V5c-3-.5-6 0-8 1.5z"/><path d="M12 6.5v14"/>',
  brisage: '<path d="M6.5 3h11L22 9 12 21 2 9z"/><path d="M12.5 3 10 8.5l3.5 3.5-2.5 4 1 5"/>',
  hdv: '<path d="M3 12V4a1 1 0 0 1 1-1h8l9 9-9 9z"/><circle cx="7.5" cy="7.5" r="1.5"/><path d="M11 14l3-3"/>',
  ttg: '<path d="M14.5 6.5V5.4a2 2 0 0 1 2.4-1.9l2.9.6a2 2 0 0 1 1.6 2.3l-2.3 11.8a2 2 0 0 1-2.3 1.6l-2.3-.4"/><rect x="3.5" y="5.5" width="11" height="15" rx="2"/><path d="M9 10.5l1 2 2 .3-1.5 1.4.4 2.1L9 15.3l-1.9 1 .4-2.1L6 12.8l2-.3z"/>',
  pda: '<path d="M12 2.5l7.5 6.5L12 21.5 4.5 9z"/><path d="M4.5 9h15"/><path d="M9 9l3 12.5L15 9"/><path d="M9 9l3-6.5L15 9"/>',
  ocre: '<path d="M12 2.5C8 2.5 4.5 9 4.5 14a7.5 7.5 0 0 0 15 0C19.5 9 16 2.5 12 2.5z"/><path d="M8 13l3-3 2 3 3-3"/>',
  combat: '<path d="M14.5 17.5 3 6V3h3l11.5 11.5"/><path d="M13 19l6-6"/><path d="M16 16l4 4"/><path d="M9.5 17.5 21 6V3h-3L6.5 14.5"/><path d="M11 19l-6-6"/><path d="M8 16l-4 4"/>',
  windows: '<path d="M4 8h14"/><path d="M14 4l4 4-4 4"/><path d="M20 16H6"/><path d="M10 12l-4 4 4 4"/>',
  fm: '<path d="M4 20 15 9"/><path d="M13.5 7.5l3 3"/><path d="M18 3v4M16 5h4"/><path d="M20.5 11.5v3M19 13h3"/><path d="M9.5 3v2.5M8.25 4.25h2.5"/>',
  carte: '<path d="M3 6.5 9 4l6 2.5L21 4v13.5L15 20l-6-2.5L3 20z"/><path d="M9 4v13.5"/><path d="M15 6.5V20"/>',
  journal: '<rect x="4.5" y="3" width="15" height="18" rx="2"/><path d="M8.5 8h7M8.5 12h7M8.5 16h4"/>',
  patrimoine: '<path d="M3 20h18"/><path d="M5 20V10l7-5 7 5v10"/><path d="M9.5 20v-5h5v5"/><circle cx="12" cy="10.5" r="1.5"/>',
  metier: '<path d="M4 20l8-8"/><path d="M14.5 3.5l6 6-3.5 3.5-6-6z"/><path d="M15 21h6M18 15v6"/>',
  elevage: '<path d="M5 19c0-4 2-7 6-8l2-5 2 3 3 1-1 3c1 2 1 4 0 6"/><path d="M8 19v-3M15 19v-3"/><circle cx="16" cy="8.5" r=".6"/>',
};
const TAB_TIPS = {
  bank: 'Contenu de votre banque et sa valeur estimée', recipes: 'Recettes faisables avec vos ressources, coût et bénéfice',
  brisage: 'Objets à briser pour générer des runes', hdv: "Prix relevés à l'hôtel de vente", ttg: 'Cartes TTG : collection et prix des marchands',
  pda: "Pierres d'âme : prix par monstre et vos pierres", ocre: 'Quête du Dofus Ocre : étapes, âmes possédées et à acheter', combat: 'Groupes de la map, drops, XP et rentabilité',
  windows: "Passer d'un personnage à l'autre (multi-compte)", fm: 'Forgemagie : runes passées et reliquat',
  carte: "Carte du monde et points d'intérêt", journal: "Messages de l'application",
  patrimoine: 'Valeur de tous vos biens : banques, inventaires, kamas, ventes en cours', metier: 'Monter un métier : recettes les moins chères par XP',
  elevage: 'Vos dragodindes : étable, enclos et monture équipée',
};
// Icônes des boutons (même trait que les onglets) : ico('trades') devant le libellé d'un bouton
const BTN_ICONS = {
  trades: '<path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/><path d="M14 7l3-3 3 3M17 4v6"/>',
  book: '<path d="M2.5 5.5C5 4.5 8.5 4.5 12 6.5c3.5-2 7-2 9.5-1v13c-2.5-1-6-1-9.5 1-3.5-2-7-2-9.5-1z"/><path d="M12 6.5v13"/><path d="M5.5 9h3.5M5.5 12h3.5M15 9h3.5"/>',
  history: '<path d="M3 12a9 9 0 1 0 2.6-6.4L3 8"/><path d="M3 3v5h5"/><path d="M12 7.5V12l3 2"/>',
  trash: '<path d="M4 7h16M10 11v6M14 11v6"/><path d="M5.5 7l1 12a2 2 0 0 0 2 2h7a2 2 0 0 0 2-2l1-12"/><path d="M9 7V4.5a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1V7"/>',
  forget: '<circle cx="12" cy="12" r="9"/><path d="M9 9l6 6M15 9l-6 6"/>',
  craft: '<path d="M14 6l4 4"/><path d="M3 21l9-9"/><path d="M12.5 4.5l7 7-3 3-7-7z"/>',
  almost: '<path d="M6 3h12M6 21h12"/><path d="M7 3c0 5 10 5 10 9s-10 4-10 9M17 3c0 3-3 4-5 5"/>',
  owned: '<path d="M4 8l8-4.5L20 8v8l-8 4.5L4 16z"/><path d="M4 8l8 4.5L20 8M12 12.5v8"/>',
  profit: '<path d="M3 17l6-6 4 4 8-8"/><path d="M15 7h6v6"/>',
  all: '<path d="M8 6h13M8 12h13M8 18h13"/><circle cx="3.5" cy="6" r="1"/><circle cx="3.5" cy="12" r="1"/><circle cx="3.5" cy="18" r="1"/>',
};
const ico = key => `<svg class="btn-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"
  stroke-linejoin="round" aria-hidden="true">${BTN_ICONS[key] || ''}</svg>`;
const tabIcon = key => `<svg class="tab-ico" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"
  stroke-linejoin="round" aria-hidden="true">${TAB_ICONS[key] || ''}</svg>`;

// Menu : onglets regroupés par famille. La famille active montre ses onglets ; une autre famille s'ouvre sur son
// dernier onglet consulté (gardé dans le navigateur), ses onglets au survol.
const GROUPS = [
  { key: 'inv', label: 'Inventaire', icon: 'bank', tabs: ['bank', 'patrimoine'] },
  { key: 'craft', label: 'Artisanat', icon: 'recipes', tabs: ['recipes', 'metier', 'brisage', 'fm'] },
  { key: 'trade', label: 'Commerce', icon: 'hdv', tabs: ['hdv', 'ttg', 'pda'] },
  { key: 'adv', label: 'Aventure', icon: 'combat', tabs: ['combat', 'ocre', 'elevage', 'carte'] },
  { key: 'tools', label: 'Outils', icon: 'windows', tabs: ['windows', 'journal'] },
];
const tabIndex = key => TABS.findIndex(t => t.key === key);
const groupOf = key => GROUPS.find(g => g.tabs.includes(key)) || GROUPS[0];
function groupLast(g) {
  let k = null;
  try { k = localStorage.getItem('tabGroup.' + g.key); } catch { }
  return g.tabs.includes(k) ? k : g.tabs[0];
}
function tabs(active) {
  const cur = TABS[active]?.key;
  const curGroup = groupOf(cur);
  try { localStorage.setItem('tabGroup.' + curGroup.key, cur); } catch { }
  const tab = k => `<button class="tab ${k === cur ? 'on' : ''}" data-act="tab" data-i="${tabIndex(k)}"
    title="${esc(TAB_TIPS[k] || '')}">${tabIcon(k)}${esc(TABS[tabIndex(k)]?.label || k)}</button>`;
  return `<nav class="tabs glass">${GROUPS.map(g => g === curGroup
    ? `<span class="tab-group on"><span class="tab-group-name">${esc(g.label)}</span>${g.tabs.map(tab).join('')}</span>`
    : `<button class="tab tab-group-btn" data-act="tab" data-i="${tabIndex(groupLast(g))}"
        title="${esc(g.label + ' : ' + g.tabs.map(k => TABS[tabIndex(k)]?.label).join(', '))}">${tabIcon(groupLast(g))}${esc(g.label)}</button>`).join('')}</nav>`;
}

// Rendu de chaque onglet : ajouté par son fichier (banque.js, combat.js…), chargé après celui-ci
const views = {
  journal() {
    return `<div class="filters">${check('LogPacketsBox', 'Journaliser les paquets (banque, inventaire, atelier : packets.log)')}</div>
      <pre class="log glass" id="log">${esc(T('LogBox'))}</pre>`;
  },
};

const seenCol = { h: 'Relevé', k: 'SeenAt', f: r => `<span class="muted">${esc(r.Seen)}</span>` };
const detailHead = (icon, title, sub) => `<div class="head">${icon}<div><h2>${esc(T(title))}</h2></div></div>
  ${T(sub) ? `<div class="sub">${esc(T(sub))}</div>` : ''}`;

// Montants en kamas écrits en texte (« 1 234 k », « 1 234 kamas », venus de l'application) : remplacés à l'affichage par
// le nombre et le symbole des kamas. Seul le texte affiché change : ni les balises et leurs attributs (infobulles), ni
// les graphiques (svg), ni les listes déroulantes. Les abréviations « 12k » / « 1,2 M » (milliers, millions) ne sont pas touchées.
const KAMAS_TEXT = /(?<![\d,.\w])(\d{1,3}(?:[   .]\d{3})+|\d+)[   ](?:k|kamas)(?![\wÀ-ſ])/g;
function kamasIcons(html) {
  let skip = 0;
  return html.split(/(<[^>]*>)/).map(part => {
    if (part[0] === '<') {
      if (/^<(svg|select|textarea)\b/i.test(part)) skip++;
      else if (/^<\/(svg|select|textarea)>/i.test(part)) skip = Math.max(0, skip - 1);
      return part;
    }
    return skip || !part ? part : part.replace(KAMAS_TEXT, (_, n) => `<span class="kamas">${n.replace(/[  ]/g, ' ')}${KAMA}</span>`);
  }).join('');
}
