'use strict';
// Onglet FM (forgemagie) : objet comme dans le jeu à gauche ; à droite l'historique des runes, les chances de chaque
// rune (SC / SN / EC) ou le poids des stats

// Mode affiché à droite, gardé dans le navigateur de l'application (préférence d'affichage seulement)
local.fmView ??= (() => { try { return localStorage.getItem('fmView') || 'hist'; } catch { return 'hist'; } })();
document.addEventListener('click', e => {
  const b = e.target.closest('[data-fmview]');
  if (!b) return;
  local.fmView = b.dataset.fmview;
  try { localStorage.setItem('fmView', local.fmView); } catch { }
  schedule();
});

const fxImg = c => `<img class="fx-ico" src="icon/effect/${c}.png" alt="" loading="lazy">`;
const fmPct = v => v < 10 ? (Math.round(v * 10) / 10).toLocaleString('fr-FR') : Math.round(v);
// Résultat d'un passage en pastille : SC (vert), SN (bleu), EC (rouge), EC sans perte (rouge pâle)
const OUTCOME = { SC: ['SC', 'sc'], SN: ['SN', 'sn'], EC: ['EC', 'ec'], EC0: ['EC', 'ec0'] };
const outcomeBadge = r => `<span class="fm-badge ${OUTCOME[r.Code]?.[1] || 'ec'}" data-tip="${esc(r.Outcome)}">${OUTCOME[r.Code]?.[0] || '?'}</span>`;

// Poids des stats de Retro (affichage seul)
function fmWeights() {
  return `<div class="fm-weights">
    <div class="filters"><span class="muted" data-tip="Poids d'un point de chaque stat dans Dofus Retro (valeurs communautaires) : estimation du reliquat et chances des runes.">Poids par point de stat ⓘ</span></div>
    ${table('FmWeightsGrid', [
      { h: '', f: r => itemImg(r.RuneId, 'sm') },
      { h: 'Stat', k: 'Label', f: r => `<span class="strong">${esc(r.Label)}</span>` },
      ...['Rune', 'Pa', 'Ra'].map((h, i) => ({ h, f: r => { const u = r.Runes?.[i]; return u ? `<span class="fm-rw" title="${esc(u.Name)} : +${u.Value} → poids ${esc(u.Weight)}">${itemImg(u.RuneId, 'sm')}<b class="acc">${esc(u.Weight)}</b></span>` : ''; } })),
    ], { select: false })}</div>`;
}

// Gains ou pertes d'un passage : icône de la caractéristique et valeur, texte complet au survol
const fmChips = (list, cls) => `<span class="fm-chips">${(list || []).map(c => `<span class="fm-chg ${cls}" title="${esc(c.Text)}">${fxImg(c.Char)}${esc(c.Value)}</span>`).join('')}</span>`;

// Historique : le plus récent en haut, résultat en pastille, compteurs SC / SN / EC au-dessus
function fmHistory() {
  const h = rows('FmHistoryGrid'), n = c => h.filter(r => r.Code === c || (c === 'EC' && r.Code === 'EC0')).length;
  return `<div class="fm-counts">${h.length ? `<span class="fm-badge sc">SC</span> ${n('SC')} <span class="fm-badge sn">SN</span> ${n('SN')}
      <span class="fm-badge ec">EC</span> ${n('EC')} <span class="muted">· ${h.length} rune(s)</span>` : ''}</div>
    ${table('FmHistoryGrid', [
      { h: '#', k: 'Index', num: true, f: r => `<span class="muted" title="${esc(r.Time)}">${r.Index}</span>` },
      { h: 'Rune', k: 'Rune', f: r => `<span class="fm-rune">${itemImg(r.RuneId, 'sm')}<span class="strong">${esc(r.Rune)}</span>${r.Attempt ? ` <span class="fm-exo" title="${r.Attempt === 'exo' ? 'Tentative d\x27exo : ligne absente du modèle' : 'Tentative d\x27over : au-delà du max de la ligne'}">${r.Attempt}</span>` : ''}</span>` },
      { h: '', k: 'Code', f: outcomeBadge },
      { h: 'Gains', k: 'Gains', f: r => fmChips(r.GainList, 'ok') },
      { h: 'Pertes', k: 'Losses', f: r => fmChips(r.LossList, 'bad') },
      { h: 'Reliquat', k: 'Reliquat', num: true, f: r => `<span class="acc strong" data-tip="${esc('Poids gagné / perdu : ' + r.Weight)}">${esc(r.Reliquat)}</span>` },
    ], { select: false, empty: T('FmEmpty') || 'Aucun passage de rune.' })}`;
}

// Chances : une ligne par stat de l'objet, ses runes avec la chance de succès (SC + SN) en gros, la barre SC / SN / EC
// dessous et le détail au survol ; au-dessus du max (ou ligne exotique) : cadre rouge
function fmChances() {
  const cell = r => `<div class="fm-chance ${r.Over ? 'over' : ''}" data-tip="${esc(r.Tip)}">
      ${itemImg(r.RuneId, 'sm')}<b>${fmPct(r.Sc + r.Sn)}%</b>
      <span class="fm-bar"><i class="sc" style="width:${r.Sc}%"></i><i class="sn" style="width:${r.Sn}%"></i><i class="ec" style="width:${r.Ec}%"></i></span></div>`;
  return `<div class="fm-counts"><span class="muted" data-tip="Formule communautaire publiée pour Dofus (DoFocus), vérifiée sur ses exemples. Pour Retro c'est une estimation : elle utilise les poids des stats de l'onglet (⚖) et les runes de Retro. Le jet minimum vient des objets vus à l'HDV ou dans vos banques (sinon : 80 % du max).">${esc(T('FmChancesNote'))} ⓘ</span>
      <span class="fm-legend"><i class="sc"></i>SC <i class="sn"></i>SN <i class="ec"></i>EC</span></div>
    ${table('FmChances', [
      { h: '', f: r => fxImg(r.Char) },
      { h: 'Stat', k: 'Name', f: r => `<span class="strong">${esc(r.Name)}</span>${r.Exo ? ' <span class="fm-exo" data-tip="Ligne absente du modèle de l\'objet">exo</span>' : ''}` },
      { h: 'Jet', k: 'Max', num: true, f: r => r.Exo ? '<span class="muted">—</span>' : `<span class="muted">${r.Min === '?' ? '≤ ' + r.Max : esc(r.Min) + '–' + r.Max}</span>` },
      { h: 'Actuel', k: 'Value', num: true, f: r => `<span class="strong ${r.Fill > 1 ? 'bad' : ''}">${r.Value}</span>
        ${r.Exo ? '' : `<span class="fm-fill"><i style="width:${Math.min(100, r.Fill * 100)}%"></i></span>`}` },
      { h: 'Poids', k: 'Weight', num: true, f: r => `<span class="muted">${esc(r.Weight)}</span>` },
      { h: 'Runes', f: r => `<div class="fm-chances">${(r.Runes || []).map(cell).join('') || '<span class="muted">aucune rune</span>'}</div>` },
    ], { select: false, empty: 'Aucun objet.' })}`;
}

// Kamas courts : 950, 12,3 k, 1,25 M
const fmKamas = n => n >= 1e6 ? (Math.round(n / 1e4) / 100).toLocaleString('fr-FR') + ' M'
  : n >= 1e4 ? (Math.round(n / 100) / 10).toLocaleString('fr-FR') + ' k' : fmt(n);
const fmCostTip = r => `Coût des runes passées : ${fmt(r.Cost)} kamas (prix HDV au passage, sinon prix actuel)`
  + (r.Unpriced ? `\n${r.Unpriced} rune(s) sans prix connu : consultez-les à l'HDV` : '');
const fmCost = r => `<span class="fm-cost" data-tip="${esc(fmCostTip(r))}">${r.Cost || !r.Unpriced ? fmKamas(r.Cost) : '?'}${r.Unpriced ? '<sup>+?</sup>' : ''}</span>`;
const runeFxImg = c => `<img class="fx-ico" src="icon/rune/${c}.png" alt="" loading="lazy">`;
// Tentatives d'exo PA / PM / PO : icône de la rune, nombre, réussies en vert
const FM_EXO = [['Pa', 1, 'PA'], ['Pm', 23, 'PM'], ['Po', 19, 'PO']];
const fmExo = r => `<span class="fm-exos">${FM_EXO.map(([k, c, n]) => `<span class="fm-chg ${r['Exo' + k] ? '' : 'muted'}" title="Exo ${n} : ${r['Exo' + k]} tentative(s), ${r['Exo' + k + 'Ok']} réussie(s)">${runeFxImg(c)}${r['Exo' + k]}${r['Exo' + k + 'Ok'] ? `<b class="ok">✓${r['Exo' + k + 'Ok']}</b>` : ''}</span>`).join('')}</span>`;

// Objets forgemagés : un par ligne, cases à cocher pour en retirer plusieurs ; clic sur le nom : son historique
local.fmSel ??= new Set();
document.addEventListener('click', e => {
  const a = e.target.closest('[data-fm]');
  if (!a) {
    // Clic ailleurs sur la ligne : l'objet devient l'objet choisi (fiche à gauche), sans changer de vue
    const uid = e.target.closest('.fm-items tbody tr')?.querySelector('[data-uid]')?.dataset.uid;
    if (uid) { post({ type: 'fm-open', uid: +uid }); schedule(); }
    return;
  }
  const list = rows('FmItemsGrid'), sel = local.fmSel;
  switch (a.dataset.fm) {
    case 'open': post({ type: 'fm-open', uid: +a.dataset.uid }); local.fmView = 'hist'; break;
    case 'check': { const u = +a.dataset.uid; sel.has(u) ? sel.delete(u) : sel.add(u); break; }
    case 'all': if (list.every(r => sel.has(r.Uid))) sel.clear(); else list.forEach(r => sel.add(r.Uid)); break;
    case 'forget': local.confirm = { text: `Retirer ${sel.size} objet(s) et leur historique ?`, msg: { type: 'fm-forget', list: [...sel] } }; sel.clear(); break;
  }
  schedule();
});

function fmItems() {
  const list = rows('FmItemsGrid'), sel = local.fmSel;
  for (const u of [...sel]) if (!list.some(r => r.Uid === u)) sel.delete(u);
  const all = list.length > 0 && list.every(r => sel.has(r.Uid));
  const total = list.reduce((s, r) => s + r.Cost, 0);
  return `<div class="fm-counts"><label class="check"><input type="checkbox" data-fm="all" ${all ? 'checked' : ''}>Tout</label>
      <button class="small ghost" data-fm="forget" ${sel.size ? '' : 'disabled'} title="Retirer les objets cochés et leur historique">🗑 ${sel.size || ''}</button>
      <span class="right muted" title="Coût total des runes passées sur ces objets">${list.length} objet(s) · ${fmKamas(total)}${KAMA}</span></div>
    <div class="fm-items">${table('FmItemsGrid', [
      { h: '', f: r => `<input type="checkbox" data-fm="check" data-uid="${r.Uid}" ${sel.has(r.Uid) ? 'checked' : ''}>` },
      { h: 'Objet', k: 'Name', f: r => `<a class="fm-rune ${r.Current ? 'acc' : ''}" data-fm="open" data-uid="${r.Uid}" title="Voir l'historique de ${esc(r.Name)} (niv. ${r.Level})">${itemImg(r.TemplateId, 'sm')}<span class="ellipsis strong">${esc(r.Name)}</span></a>` },
      { h: 'Runes', k: 'Runes', num: true, f: r => `<span title="SC ${r.Sc} · SN ${r.Sn} · EC ${r.Ec}">${r.Runes}</span>` },
      { h: 'SC', k: 'Sc', num: true, f: r => `<span class="muted">${r.Sc}</span>` },
      { h: 'Coût', k: 'Cost', num: true, f: fmCost },
      { h: 'Exo', k: 'Exo', f: fmExo },
      { h: 'Date', k: 'LastTicks', num: true, f: r => `<span class="muted">${esc(r.Last)}</span>` },
    ], { select: false, empty: 'Aucun objet forgemagé.' })}</div>`;
}

// Superposition en jeu : blocs FM affichés ou non (pendant une séance de forgemagie)
function fmOverlayTogs() {
  const tog = (n, ico, tip) => `<label class="tog" title="${tip}"><input type="checkbox" data-set="${n}" ${S[n]?.on ? 'checked' : ''}><span>${ico}</span></label>`;
  const off = !S.OverlayEnabled?.on;
  return `<span class="togs fm-overlay"><span class="lbl ${off ? 'muted' : ''}" title="Superposition en jeu${off ? ' : à activer dans l\'onglet Switcheur' : ''}">🖥</span>
    ${tog('OverlayFmHistory', '📜', 'Superposition : dernières runes passées')}${select('OverlayFmHistoryCount', `class="fm-count" title="Superposition : nombre de runes de l'historique affichées"`)}${tog('OverlayFmChances', '🎲', 'Superposition : chances des runes')}</span>`;
}

Object.assign(views, {
  fm() {
    const tpl = T('FmItemTpl'), view = local.fmView;
    const tab = (k, ico, label, tip) => `<button class="pill ${view === k ? 'on' : ''}" data-fmview="${k}" title="${tip}">${ico} ${label}</button>`;
    const stats = rows('FmStats'), cur = rows('FmItemsGrid').find(r => r.Current);
    return `<div class="filters fm-top"><span class="lbl">Objet</span>${select('FmItemCombo', 'class="fm-select"')}
        <span class="right"></span>${fmOverlayTogs()}<span class="seg">${tab('items', '🗂', 'Objets', 'Tous les objets forgemagés : coût, exos PA / PM / PO')}${tab('hist', '📜', 'Historique', 'Runes passées sur l\'objet')}${tab('chances', '🎲', 'Chances', 'Chances SC / SN / EC de chaque rune sur chaque ligne')}${tab('weights', '⚖', 'Poids', 'Poids des stats (reliquat et chances)')}</span></div>
      <div class="split fm">
        <aside class="detail glass fm-item">
          <div class="fm-head">${tpl ? itemImg(tpl, 'fm-big') : ''}<div><h2 class="acc">${esc(T('FmItemName'))}</h2><div class="muted small">${esc(T('FmItemSub'))}</div></div></div>
          <div class="fm-effects">${stats.map(s => `<div class="fx ${s.Neg ? 'neg' : ''}" title="${esc(s.Range)}"><span>${esc(s.Text)}${s.Max ? ` <span class="fm-max">/${s.Max}</span>` : ''}</span>${fxImg(s.Char)}</div>`).join('')
            || '<div class="muted small">Posez l\'objet dans un atelier de forgemagie en jeu.</div>'}</div>
          <div class="fm-reliquat" data-tip="Estimation : gagné sur les pertes, consommé par les gains au-delà. Corrigez-le si besoin.">
            <span class="lbl">Reliquat</span><span class="huge">${esc(T('FmReliquat'))}</span>
            <span class="fm-fix">${input('FmReliquatInput', 'Valeur', '74px')}<button class="small" data-act="click" data-name="FmSetReliquat" title="Remplacer le reliquat estimé par la valeur saisie">✓</button></span></div>
          ${cur ? `<div class="fm-sum"><span class="lbl">${KAMA}</span>${fmCost(cur)}<span class="lbl" title="Tentatives d'exo PA / PM / PO">Exo</span>${fmExo(cur)}</div>` : ''}
          <div class="fm-actions">
            <button class="small ghost" data-act="confirm" data-name="FmClear" data-text="Effacer l'historique de cet objet et remettre son reliquat à 0 ?" title="Effacer l'historique et remettre le reliquat à 0">🗑 Historique</button>
            <button class="small ghost" data-act="confirm" data-name="FmForget" data-text="Retirer cet objet et tout son historique de la liste ?" title="Retirer l'objet de la liste">✕ Oublier</button></div>
        </aside>
        <div class="fm-main">${view === 'weights' ? fmWeights() : view === 'items' ? fmItems() : view === 'chances' ? fmChances() : fmHistory()}</div>
      </div>`;
  },
});
