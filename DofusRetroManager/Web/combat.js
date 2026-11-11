'use strict';
// Onglet Combat : en-tête (personnage, caractéristiques, session, réglages), réglages repliables, chronomètre,
// puis à gauche l'Économe, les groupes de la map et la rentabilité (session, historique), à droite les listes.

// Chronomètre de rentabilité et challenges (ancien onglet Arène), en bandeau compact
function chronoBlock() {
  const challenges = rows('AreneChallenges');
  const state = c => c.State === 1 ? 'ok' : c.State === -1 ? 'bad' : 'warn';
  const open = local.open.chrono;
  return `<div class="glass card chrono">
      <span class="time acc" title="Chronomètre : compte les fins de combat des personnages choisis (un combat à plusieurs compte une fois)">⏱ ${esc(T('ChronoTime'))}</span>
      ${btn('ChronoStart', '▶ Démarrer', 'small primary')}${btn('ChronoReset', 'Remettre à zéro', 'small')}
      <span title="Personnages comptés (groupes : ⚙ Réglages)">${select('AreneGroup')}</span>
      <div class="chrono-stats" title="Gains : kamas + drops au prix HDV (sinon prix marchand)">
        <b class="ellipsis" title="${esc(T('ChronoFights'))}">${esc(T('ChronoFights'))}</b><b class="acc ellipsis" title="${esc(T('ChronoGains'))}">${esc(T('ChronoGains'))}</b>
        <span class="muted ellipsis" title="${esc(T('ChronoRate'))}">${esc(T('ChronoRate'))}</span></div>
      <div class="chrono-chal">${challenges.map(c => `<span class="chall ${state(c)}" data-tip="${esc(c.Name + ' · ' + c.Status + ' · ' + c.Character + (c.Rule ? ' — ' + c.Rule : '') + (c.Announced ? ' (annoncé par le jeu)' : ''))}">
          ${c.Gfx >= 0 ? `<img src="icon/chall/${c.Gfx}.png" alt="">` : `<span class="fallback">${esc(c.Name)}</span>`}</span>`).join('')}
        ${T('AreneOtherChallenges') ? `<span class="muted ellipsis" title="${esc(T('AreneOtherChallenges'))}">${esc(T('AreneOtherChallenges'))}</span>` : ''}</div>
      <button class="small" data-act="open" data-key="chrono" title="Combats comptés par le chronomètre">Combats ${open ? '▾' : '▸'}</button>
    </div>
    ${open ? `<div class="chrono-grid">${table('ChronoGrid', [
      { h: 'Fin', k: 'TimeSort', f: r => esc(r.Time) },
      { h: 'Personnage(s)', k: 'Characters', f: r => `<span class="ellipsis" title="${esc(r.Characters)}">${esc(r.Characters)}</span>` },
      { h: 'Monstres', k: 'Monsters', f: r => `<span class="ellipsis" title="${esc(r.Monsters)}">${esc(r.Monsters)}</span>` },
      { h: 'XP', k: 'Xp', num: true, f: r => fmt(r.Xp) },
      { h: 'Kamas', k: 'Kamas', num: true, f: r => fmt(r.Kamas) },
      { h: 'Drops', k: 'DropValue', num: true, f: r => fmt(r.DropValue) },
      { h: 'Total', k: 'Total', num: true, f: r => `<span class="acc strong">${fmt(r.Total)}</span>` },
    ], { select: false, empty: 'Démarrez le chronomètre : chaque fin de combat s\'ajoute ici.' })}</div>` : ''}`;
}

// Membres d'un groupe du chronomètre : tête du personnage (classe) et niveau, ceux du groupe d'abord, puis les autres,
// chacun trié par niveau ; un clic ajoute ou retire le personnage
function groupMembers() {
  const all = (S.AreneGroupMembers?.items || []).map((m, i) => { const [gfx, lvl] = (m.d || '').split('|'); return { ...m, i, gfx: Number(gfx) || 0, lvl: Number(lvl) || 0 }; });
  if (!all.length) return '<span class="muted">Aucun personnage connu.</span>';
  const chip = m => `<button class="member ${m.on ? 'on' : ''}" data-act="toggle" data-name="AreneGroupMembers" data-i="${m.i}" title="${esc(m.t + (m.lvl ? ' · niveau ' + m.lvl : '') + (m.on ? ' — retirer du groupe' : ' — ajouter au groupe'))}">
      ${m.gfx ? `<img class="ico" src="icon/head/${m.gfx}.png" alt="">` : '<span class="glyph">?</span>'}<span class="ellipsis">${esc(m.t)}</span>${m.lvl ? `<i>${m.lvl}</i>` : ''}</button>`;
  const block = (title, list) => list.length ? `<div class="members-block"><div class="lbl">${title} (${list.length})</div>
      <div class="members">${list.sort((a, b) => b.lvl - a.lvl || a.t.localeCompare(b.t)).map(chip).join('')}</div></div>` : '';
  return block('Dans le groupe', all.filter(m => m.on)) + block('Autres personnages', all.filter(m => !m.on));
}

// Objectif de farm : décompte (icône, total / voulu, barre ; derniers combats au survol), ou formulaire (🎯)
function farmBlock() {
  let g = null;
  try { g = T('FarmState') ? JSON.parse(T('FarmState')) : null; } catch { }
  const form = () => {
    const item = input('FarmItem', 'Objet (ex. Pierre de diamant)', '200px').replace('<input ', '<input list="farm-items" autocomplete="off" ');
    return `<div class="filters farm-form">${item}<datalist id="farm-items">${T('FarmSuggest').split('|').filter(Boolean).map(n => `<option value="${esc(n)}">`).join('')}</datalist>
      <span class="lbl">×</span>${input('FarmTarget', '', '56px')}
      <button class="small primary" data-act="click" data-name="FarmStart" title="Compter cet objet à chaque fin de combat de vos personnages ; bulle et son quand la quantité est atteinte">Lancer</button>
      ${T('FarmStatus') ? `<span class="muted small">${esc(T('FarmStatus'))}</span>` : ''}</div>`;
  };
  if (!g) return local.open.farm ? form() : '';
  const tip = `${g.n} · ${g.f} combat(s)` + (g.s.length ? '\n' + g.s.map(s => `Combat ${s.n} : +${s.g} → ${s.t}/${g.t}`).join('\n') : '');
  const last = g.s[0];
  return `<div class="farm-goal ${g.c >= g.t ? 'done' : ''}" data-tip="${esc(tip)}">${itemImg(g.id, 'sm')}
      <b>${fmt(g.c)}</b><span class="muted">/ ${fmt(g.t)}</span>
      <span class="farm-bar"><i style="width:${Math.min(100, g.c / Math.max(1, g.t) * 100)}%"></i></span>
      ${last ? `<span class="muted small">C${last.n} +${last.g}</span>` : ''}
      <a class="item-link muted" data-act="click" data-name="FarmReset" title="Arrêter l'objectif">✕</a></div>`;
}

// Réglages de l'onglet, regroupés dans un panneau repliable (⚙) : alertes archi-monstres, alertes monstres, groupes
function combatSettings() {
  // Nom du monstre : liste déroulante des noms proposés par l'application pendant la saisie
  const monster = input('HuntAlertMonster', 'Monstre (ex. Craqueleur des plaines)', '190px').replace('<input ', '<input list="hunt-monsters" autocomplete="off" ');
  const suggest = `<datalist id="hunt-monsters">${T('HuntAlertSuggest').split('|').filter(Boolean).map(n => `<option value="${esc(n)}">`).join('')}</datalist>`;
  const alerts = `<div class="filters" title="Monstre, nombre minimum dans le groupe, niveaux">${monster}${suggest}
      <span class="lbl">≥</span>${input('HuntAlertCount', '', '40px')}<span class="lbl">niv.</span>${input('HuntAlertMin', '', '56px')}
      <span class="lbl">à</span>${input('HuntAlertMax', '', '56px')}
      <button class="small primary" data-act="click" data-name="HuntAlertAdd" title="Prévenir (son et bulle) quand un groupe de la map contient au moins ce nombre de ce monstre, à ces niveaux">Ajouter</button>
      <button class="small" data-act="click" data-name="HuntAlertTest">Tester</button></div>
    ${T('HuntAlertStatus') ? `<div class="muted small">${esc(T('HuntAlertStatus'))}</div>` : ''}
    <div class="filters">${rows('HuntAlertList').map((a, i) => `<span class="chip alert-chip ${a.Enabled ? 'on' : 'dim'}" data-tip="${esc(a.Text)}">
      <a class="item-link" ${attrs({ act: 'row', grid: 'HuntAlertList', i, handler: 'HuntAlertToggle_Click' })} title="Activer ou couper cette alerte">${a.Enabled ? '🔔' : '🔕'}</a>
      <img src="icon/monster/${a.MonsterId}.png" alt=""><span class="muted small">${esc(a.Short)}</span> <a class="item-link" ${attrs({ act: 'row', grid: 'HuntAlertList', i, handler: 'HuntAlertDelete_Click' })} title="Supprimer">✕</a></span>`).join('') || '<span class="muted">Aucune alerte.</span>'}</div>`;
  const groups = S.AreneGroupEdit?.on
    ? `<div class="filters"><span class="lbl">Nom</span>${input('AreneGroupName', 'Groupe principal', '160px')}
        <button class="small primary" data-act="click" data-name="AreneGroupSave">Enregistrer</button>
        <button class="small" data-act="click" data-name="AreneGroupDelete">Supprimer</button></div>
      ${T('AreneGroupStatus') ? `<div class="muted small">${esc(T('AreneGroupStatus'))}</div>` : ''}
      ${groupMembers()}`
    : `<div class="muted small">Les personnages comptés par le chronomètre : tous, ou un groupe enregistré.</div>`;
  return `<div class="glass card combat-settings">
      <section><p class="section-title">🔔 Archi-monstres</p>
        <div class="filters">${check('HuntSound', 'Son')}${check('HuntArchiPopup', 'Pop-up')}
          <button class="small ibtn" data-act="click" data-name="HuntTestSound" data-tip="Tester le son (et la bulle archi-monstre)">▶</button></div>
        <label class="volume" title="Volume des alertes (archi-monstres, alertes monstres, Switcheur)">🔊<input type="range" data-set="HuntVolume" data-fk="HuntVolume"
          min="${S.HuntVolume?.min ?? 0}" max="${S.HuntVolume?.max ?? 100}" step="1" value="${S.HuntVolume?.val ?? 70}"
          style="--p:${S.HuntVolume?.val ?? 70}%"><span class="muted">${esc(T('HuntVolumeText'))}</span></label>
        <p class="section-title" style="margin-top:6px">🖥 Superposition en jeu</p>
        <div class="filters" title="Fenêtre posée sur le jeu (réglages complets : onglet Switcheur)">${check('OverlayEnabled', 'Activée')}
          <span data-tip="Pendant un combat : aides aux challenges annoncés (Économe, Versatile, Barbare, Mystique, Borné, Intouchable) : sorts déjà lancés, interdits ou à relancer">${check('OverlayEconome', 'Aides aux challenges')}</span></div></section>
      <section><p class="section-title">🎯 ${esc(T('HuntAlertsToggle') || 'Alertes monstres')}</p>${alerts}</section>
      <section><p class="section-title" style="display:flex;justify-content:space-between;align-items:center">👥 Groupes du chronomètre
        <label class="check small"><input type="checkbox" data-set="AreneGroupEdit" ${S.AreneGroupEdit?.on ? 'checked' : ''}>Modifier</label></p>${groups}</section>
    </div>`;
}

// Chiffres clés d'une session (ligne de l'historique) en tuiles : détail d'une session et carte Rentabilité
function sessKpis(row, full = true) {
  if (!row) return '';
  const kpi = (v, l, tip, cls = '') => `<div class="sess-kpi ${cls}" data-tip="${esc(tip)}"><b>${v}</b><span>${l}</span></div>`;
  return `<div class="sess-kpis">
      ${kpi(esc(row.Duration), 'Durée', 'Durée hors pauses')}
      ${kpi(fmt(row.Fights), 'Combats', 'Un combat fait par plusieurs de vos personnages compte une fois · ' + row.Characters + ' personnage(s)')}
      ${full && row.Kills != null ? kpi(fmt(row.Kills), 'Mobs', 'Monstres tués') : ''}
      ${kpi(fmt(row.Xp), 'XP', 'XP / h : ' + fmt(row.XpPerHour))}
      ${full ? kpi(kamas(row.Kamas), 'Kamas', 'Kamas ramassés') + kpi(kamas(row.Drops), 'Drops', 'Drops au prix HDV actuel') : ''}
      ${kpi(kamas(row.Total), 'Total', 'Kamas ' + fmt(row.Kamas) + ' + drops ' + fmt(row.Drops), 'acc')}
      ${kpi(kamas(row.PerHour), '/ heure', 'Total par heure', 'acc')}</div>`;
}

// Objets obtenus : icône et quantité ; nom, valeur et qui l'a obtenu au survol
const dropChips = list => `<span class="drop-chips">${(list || []).map(d => `<span class="drop-chip" data-tip="${esc(d.Name + ' ×' + d.Quantity
  + (d.Total != null ? ' · ' + fmt(d.Total) + ' k' : '') + (d.Who ? '\n' + d.Who : ''))}">${itemImg(d.ItemId, 'sm')}<i>${fmt(d.Quantity)}</i></span>`).join('')}</span>`;

// Détail d'une session (clic sur sa ligne) : fenêtre par-dessus la page. Résumé (personnages, monstres, objets) ou
// liste de tous ses combats avec leurs monstres et leurs drops
function sessionDetail() {
  if (!local.sessDetail || TABS[S.MainTabs?.i ?? 0]?.key !== 'combat' || !T('HuntSessionDetailTitle')) return '';
  const row = rows('HuntSessionGrid')[S.HuntSessionGrid?.sel ?? -1], kills = rows('HuntSessionMobs'), fights = rows('HuntSessionFights');
  const view = local.sessView || 'sum';
  const tab = (k, icon, label) => `<button class="small ${view === k ? 'primary' : ''}" data-act="sess-view" data-v="${k}">${icon}${label}</button>`;
  const summary = `<p class="section-title">Par personnage</p>
        ${table('HuntSessionChars', [
          { h: '', f: r => r.Gfx ? `<img class="ico" src="icon/head/${r.Gfx}.png" alt="">` : '' },
          { h: 'Personnage', k: 'Name', f: r => `<span class="strong">${esc(r.Name)}</span>` },
          { h: 'Combats', k: 'Fights', num: true, f: r => fmt(r.Fights) },
          { h: 'Mobs', k: 'Kills', num: true, f: r => r.Kills == null ? '—' : fmt(r.Kills) },
          { h: 'XP', k: 'Xp', num: true, f: r => `<span data-tip="${esc('XP / h : ' + fmt(r.XpPerHour))}">${fmt(r.Xp)}</span>` },
          { h: 'Kamas', k: 'Kamas', num: true, f: r => kamas(r.Kamas) },
          { h: 'Drops', k: 'Drops', num: true, f: r => kamas(r.Drops) },
          { h: 'Total', k: 'Total', num: true, f: r => `<span class="acc strong">${kamas(r.Total)}</span>` },
          { h: '/ h', k: 'PerHour', num: true, f: r => kamas(r.PerHour) },
        ], { select: false, empty: 'Aucun personnage.' })}
        <p class="section-title">Monstres tués${kills.length ? ' · ' + fmt(kills.reduce((a, m) => a + m.Count, 0)) : ''}</p>
        <div class="sess-mobs">${kills.length ? kills.map(m => `<div class="sess-mob" data-tip="${esc(m.Name + ' ×' + m.Count)}">
            <img src="icon/monster/${m.Id}.png" alt="" loading="lazy"><b>×${fmt(m.Count)}</b><span>${esc(m.Name)}</span></div>`).join('')
          : '<span class="muted small">Inconnus pour cette session (gardée avant leur décompte).</span>'}</div>
        <p class="section-title">Objets obtenus</p>
        ${table('HuntSessionDrops', [
          { h: '', f: r => itemImg(r.ItemId, 'sm') },
          { h: 'Objet', k: 'Name', f: r => `<span class="strong ellipsis" data-tip="${esc(r.Who)}">${esc(r.Name)}</span>` },
          { h: 'Qté', k: 'Quantity', num: true, f: r => fmt(r.Quantity) },
          { h: 'Prix u.', k: 'Unit', num: true, f: r => kamas(r.Unit) },
          { h: 'Total', k: 'Total', num: true, f: r => `<span class="acc strong">${kamas(r.Total)}</span>` },
        ], { select: false, empty: 'Aucun objet obtenu.' })}
        <div class="muted small">Drops au prix HDV actuel (sinon prix marchand) · qui a obtenu chaque objet : au survol.</div>`;
  const list = `<div class="sess-fights">${table('HuntSessionFights', [
          { h: 'Fin', k: 'TimeSort', f: r => `<span data-tip="${esc('Durée ' + r.Duration)}">${esc(r.Time)}</span>` },
          { h: '👥', k: 'Count', num: true, f: r => `<span data-tip="${esc(r.Characters)}">${r.Count}</span>` },
          { h: 'Monstres', f: r => mobs(r.Mobs) },
          { h: 'XP', k: 'Xp', num: true, f: r => fmt(r.Xp) },
          { h: 'Gain', k: 'Total', num: true, f: r => `<span class="acc strong" data-tip="${esc('Kamas ' + fmt(r.Kamas) + ' · drops ' + fmt(r.DropValue))}">${kamas(r.Total)}</span>` },
          { h: 'Drops', f: r => dropChips(r.Drops) },
        ], { select: false, empty: 'Combats de cette session plus gardés (seuls les 3 000 derniers le sont).' })}</div>
        <div class="muted small">Vos personnages du même combat réunis (👥 : leurs noms au survol) · objets : nom, valeur et qui l'a obtenu au survol.</div>`;
  return `<div class="modal-back" data-act="sess-close"><div class="modal glass wide" data-act="none">
      <div class="row"><h2>${row?.Current ? 'Session en cours' : 'Session'}</h2><span class="sub">${esc(T('HuntSessionDetailTitle'))}</span>
        <span class="right filters">${tab('sum', ico('all'), 'Résumé')}${tab('fights', ico('history'), `Combats (${fights.length})`)}</span>
        <button class="small ghost" data-act="sess-close">✕</button></div>
      <div class="modal-body session-detail">${sessKpis(row)}${view === 'fights' ? list : summary}</div></div></div>`;
}

// Rentabilité : session en cours (tuiles de tous vos personnages, projection), ou historique de toutes les sessions
function sessionCard() {
  const hist = local.open.sessHist;
  const all = rows('HuntSessionGrid'), n = all.filter(r => !r.Current).length, ci = all.findIndex(r => r.Current), cur = all[ci];
  const tabsHtml = `<div class="seg"><button class="pill ${hist ? '' : 'on'}" data-act="${hist ? 'open' : ''}" data-key="sessHist">Session</button>
    <button class="pill ${hist ? 'on' : ''}" data-act="${hist ? '' : 'open'}" data-key="sessHist">Historique (${n})</button></div>`;
  if (hist) return panel('session', 'Rentabilité', `<div class="session-hist">${table('HuntSessionGrid', [
      { h: 'Début', k: 'StartSort', f: r => `<span class="${r.Current ? 'acc strong' : ''}" data-tip="${esc(r.Tip)}">${esc(r.Date)}${r.Rebuilt ? ' ≈' : ''}</span>` },
      { h: 'Durée', k: 'Minutes', num: true, f: r => esc(r.Duration) },
      { h: 'Combats', k: 'Fights', num: true, f: r => `<span data-tip="${esc(r.Characters + ' personnage(s)')}">${fmt(r.Fights)}</span>` },
      { h: 'Mobs', k: 'Kills', num: true, f: r => r.Kills == null ? '—' : fmt(r.Kills) },
      { h: 'XP', k: 'Xp', num: true, f: r => fmt(r.Xp) },
      // Kamas + drops : le détail au survol (tableau étroit)
      { h: 'Total', k: 'Total', num: true, f: r => `<span class="acc strong" data-tip="${esc('Kamas : ' + fmt(r.Kamas) + ' · Drops : ' + fmt(r.Drops))}">${kamas(r.Total)}</span>` },
      { h: '/ h', k: 'PerHour', num: true, f: r => `<span data-tip="${esc('XP / h : ' + fmt(r.XpPerHour))}">${kamas(r.PerHour)}</span>` },
      { h: '', f: r => r.Current ? '' : `<a class="item-link muted" ${attrs({ act: 'row', grid: 'HuntSessionGrid', i: r._i, handler: 'HuntSessionForget_Click' })} title="Oublier cette session">✕</a>` },
    ], { empty: 'Aucune session : chaque fin de combat s\'ajoute à la session en cours.' })}</div>
    <div class="muted small">Tous les personnages · drops au prix HDV actuel · clic sur une session : son détail (combats, drops) · ≈ : reconstituée.</div>`, tabsHtml, 'glass card session');
  // Projection : une carte par durée, le total en grand, l'XP dessous ; kamas et drops au survol
  const proj = rows('HuntProjection');
  const projHtml = proj.length ? `<div class="sess-proj">${proj.map(p => `<div class="proj-card" data-tip="${esc(`XP ${fmt(p.Xp)} · kamas ${fmt(p.Kamas)} · drops ${fmt(p.Drops)}`)}">
      <span>${esc(p.Span)}</span><b class="acc">${kamas(p.Total)}</b><small class="muted">${fmt(p.Xp)} XP</small></div>`).join('')}</div>` : '';
  const me = T('HuntSession');
  return panel('session', 'Rentabilité', cur ? `${sessKpis(cur, false)}
      <div class="sess-me"><span class="lbl" data-tip="${esc(me)}">Projection · personnage affiché</span>
        <button class="small" ${attrs({ act: 'select', grid: 'HuntSessionGrid', i: ci })} title="Détail de la session en cours : personnages, monstres, objets et tous les combats">${ico('history')}Détail</button></div>
      ${projHtml}
      <div class="muted small ellipsis" data-tip="${esc(T('HuntLearned'))}">${esc(T('HuntLearned'))}</div>`
    : `<div class="empty small">Aucun combat sur cette session.</div><div class="muted small">${esc(T('HuntLearned'))}</div>`, tabsHtml, 'glass card session');
}

// Panneaux de l'onglet : réduits (▾ / ▸ : titre seul) ou agrandis (⤢ : seuls à l'écran, 🗗 pour revenir). L'état est
// gardé dans le navigateur de l'application, d'un lancement à l'autre (préférence d'affichage seulement)
local.panels ??= (() => { try { return JSON.parse(localStorage.getItem('combatPanels')) || {}; } catch { return {}; } })();
function panel(key, title, body, tools = '', cls = 'mini') {
  const st = local.panels[key];
  return `<section class="panel ${cls} ${st ? 'is-' + st : ''}" data-panel="${key}">
      <div class="panel-bar"><p class="section-title">${title}</p><span class="panel-tools">${st === 'min' ? '' : tools}</span>
        <button class="pbtn" data-act="panel" data-key="${key}" data-v="min" data-tip="${st === 'min' ? 'Déplier' : 'Réduire'}">${st === 'min' ? '▸' : '▾'}</button>
        <button class="pbtn" data-act="panel" data-key="${key}" data-v="max" data-tip="${st === 'max' ? 'Restaurer' : 'Agrandir'}">${st === 'max' ? '🗗' : '⤢'}</button></div>
      ${st === 'min' ? '' : `<div class="panel-body">${body}</div>`}</section>`;
}

// Zone principale : à gauche les groupes de la map et la rentabilité, à droite les archi-monstres, les combats et les
// drops de la session ; un panneau agrandi occupe seul toute la zone
function combatPanels(byGroup) {
  const groups = panel('groups', `<a class="pos-link" data-act="goto-me" title="Voir sur la carte">${esc(T('HuntMap'))}</a>`, table('HuntGroupGrid', [
    // Monstres en illustrations (nom et niveau au survol) ; tri par leurs noms
    { h: 'Monstres', k: 'Monsters', f: r => mobs(r.Mobs) },
    { h: 'Niv.', k: 'TotalLevel', num: true, f: r => `<span class="acc strong">${fmt(r.TotalLevel)}</span>` },
    { h: '★', k: 'Stars' },
    { h: 'XP ≈', k: 'Xp', num: true, f: r => `<span class="strong">${esc(r.XpText)}</span>` },
    { h: 'Vus', k: 'Drops', f: r => `<span class="muted ellipsis" title="${esc(r.Drops)}">${esc(r.Drops)}</span>` },
    { h: 'Drops (jeu)', k: 'OfficialDrops', f: r => r.HasDropDetails
      ? `<button class="caret ${r.Expanded ? 'open' : ''}" ${attrs({ act: 'row', grid: 'HuntGroupGrid', i: r._i, handler: 'HuntDropsToggle_Click' })}>▶</button> <span class="ellipsis">${esc(r.OfficialDrops)}</span>` : '' },
    { h: 'Drops ≈', k: 'DropValue', num: true, f: r => `<span class="acc strong">${fmt(r.DropValue)}</span>` },
    { h: 'Archi', k: 'Archi', f: r => r.Archi ? `<span class="bad strong">${esc(r.Archi)}</span>` : '' },
    { h: 'Pierre', k: 'ArchiPrice', num: true, f: r => fmt(r.ArchiPrice) },
  ], { select: false, empty: 'Aucun groupe de monstres sur la map.', after: (r, n) => r.Expanded && r.DropDetails?.length ? `<tr><td colspan="${n}" class="drops">
      <table><thead><tr><th></th><th>Objet</th><th class="num">Chance</th><th class="num">Monstres</th><th class="num">PP min.</th><th class="num">Max / combat</th><th class="num">Prix u.</th><th class="num">Valeur / combat</th><th>État</th></tr></thead>
      <tbody>${r.DropDetails.map(d => `<tr class="${d.Muted ? 'dim' : ''}"><td>${itemImg(d.ItemId, 'sm')}</td><td class="strong">${esc(d.Name)}</td>
        <td class="num acc strong">${esc(d.Chance)}</td><td class="num">${esc(d.Monsters)}</td><td class="num">${esc(d.Prospection)}</td>
        <td class="num">${esc(d.MaxPerFight)}</td><td class="num">${esc(d.Price)}</td><td class="num strong">${esc(d.Value)}</td><td>${esc(d.State)}</td></tr>`).join('')}</tbody></table></td></tr>` : '' }));
  const archis = panel('archis', 'Derniers archi-monstres vus', table('HuntArchiGrid', [
    // Date courte, illustration et niveau, position réduite à ses coordonnées et sa zone (texte complet au survol)
    { h: 'Vu', k: 'TimeSort', f: r => `<span class="muted" title="${esc(r.Time)}">${esc(shortDate(r.Time))}</span>` },
    { h: 'Archi-monstre', k: 'Name', f: r => `<span class="strong ellipsis" title="${esc(r.Name)}">${esc(r.Name)}</span>
      <div class="muted small">niv. ${r.Level}</div><div>${posLink(shortMap(r.Map), r.Goto, 'ellipsis')}</div>` },
    { h: 'Pierre', k: 'Price', num: true, f: r => fmt(r.Price) },
  ], { select: false, empty: 'Aucun archi-monstre vu.' }), `<button class="small" data-act="click" data-name="HuntClearArchis">Vider</button>`);
  const fights = panel('fights', 'Combats de la session', table('HuntFightGrid', [
    { h: 'Heure', k: 'TimeSort', f: r => `<span class="muted">${esc(r.Time)}</span>` },
    // Monstres en illustrations ; vue Groupe : nombre de vos personnages du combat, leurs noms au survol
    { h: 'Monstres', k: 'Monsters', f: r => `${mobs(r.Mobs)}${byGroup ? `<div class="muted small" data-tip="${esc(r.Characters)}">👥 ${r.Count}</div>` : ''}` },
    { h: 'XP', k: 'Xp', num: true, f: r => fmt(r.Xp) },
    // Kamas + drops en un seul gain, le détail au survol
    { h: 'Gain', k: 'DropValue', num: true, f: r => `<span class="acc" data-tip="${esc(`Kamas : ${fmt(r.Kamas)} · Drops : ${fmt(r.DropValue)}${r.Drops ? ' — ' + r.Drops : ''}`)}">${fmt((r.Kamas || 0) + (r.DropValue || 0))}</span>` },
  ], { empty: 'Aucun combat sur cette session.' }),
    `<span class="seg" data-tip="Perso : le personnage affiché. Groupe : avec vos autres personnages du même combat (groupés en jeu ou non : même fin et mêmes monstres)">
      <button class="pill ${byGroup ? '' : 'on'}" data-act="set-on" data-name="HuntFightsGroup" data-v="false">Perso</button>
      <button class="pill ${byGroup ? 'on' : ''}" data-act="set-on" data-name="HuntFightsGroup" data-v="true">Groupe</button></span>`);
  const drops = panel('drops', esc(T('HuntDropsTitle')), table('HuntDropGrid', [
    { h: '', f: r => itemImg(r.ItemId, 'sm') },
    { h: 'Objet', k: 'Name', f: r => `<span class="strong ellipsis" title="${esc(r.Name)}">${esc(r.Name)}</span>` },
    { h: 'Qté', k: 'Quantity', num: true, f: r => fmt(r.Quantity) },
    { h: 'Total', k: 'Total', num: true, f: r => `<span class="acc strong" data-tip="${esc('Prix unitaire : ' + fmt(r.Unit) + ' k')}">${fmt(r.Total)}</span>` },
  ], { select: false, empty: 'Aucun objet obtenu.' }));
  const all = { groups, session: sessionCard(), archis, fights, drops };
  const max = Object.keys(all).find(k => local.panels[k] === 'max');
  if (max) return `<div class="combat-max">${all[max]}</div>`;
  // Colonne de droite : un panneau réduit ne prend que la hauteur de son titre ; les autres se partagent la hauteur
  // selon leur poids (bord glissé à la souris). Une poignée entre deux panneaux ouverts, rien à côté d'un panneau réduit
  const sz = local.sizes, { tpl, gut } = sideRows();
  const cells = ['archis', 'fights', 'drops'].map((k, i) => all[k] + (gut[i] ?? '')).join('');
  // Rentabilité : hauteur réglable (session en cours ou historique), sauf si elle ou les groupes sont réduits
  const sess = local.panels.session !== 'min' && local.panels.groups !== 'min';
  return `<div class="split wide combat-split" style="${sz.side ? `--side:min(${sz.side}px, 65%)` : ''}">
      <div class="stack ${sess && sz.sess ? 'sized' : ''}" style="${sz.sess ? `--sess:${sz.sess}px` : ''}">${groups}${sess ? gutter('sess') : ''}${all.session}</div>
      ${gutter('side')}
      <div class="side-tables" style="grid-template-rows:${tpl.join(' ')}">${cells}</div>
    </div>`;
}

// Lignes de la colonne de droite : panneau réduit = hauteur de son titre, sinon sa part (poids) de la hauteur ; entre
// deux panneaux, une poignée qui partage la hauteur des deux panneaux ouverts les plus proches
function sideRows() {
  const side = ['archis', 'fights', 'drops'], open = side.filter(k => local.panels[k] !== 'min'), tpl = [], gut = [];
  side.forEach((k, i) => {
    tpl.push(local.panels[k] === 'min' ? 'auto' : `minmax(0, ${local.sizes['w_' + k] || 1}fr)`);
    if (i === side.length - 1) return;
    const a = open.filter(o => side.indexOf(o) <= i).pop(), b = open.find(o => side.indexOf(o) > i);
    tpl.push('12px');
    gut.push(a && b ? gutter('row', `data-a="${a}" data-b="${b}"`) : '<div></div>');
  });
  return { tpl, gut };
}

// Poignées de redimensionnement : glisser le bord entre deux zones ; double-clic : taille par défaut. Les tailles sont
// gardées dans le navigateur de l'application (préférence d'affichage seulement)
local.sizes ??= (() => { try { return JSON.parse(localStorage.getItem('combatSizes')) || {}; } catch { return {}; } })();
const gutter = (g, extra = '') => `<div class="gutter g-${g}" data-gut="${g}" ${extra} title="Glisser pour redimensionner · double-clic : taille par défaut"></div>`;
const saveSizes = () => { try { localStorage.setItem('combatSizes', JSON.stringify(local.sizes)); } catch { } };
document.addEventListener('pointerdown', e => {
  const g = e.target.closest('.gutter[data-gut]');
  if (!g || e.button !== 0) return;
  e.preventDefault();
  const d = g.dataset, sz = local.sizes, split = g.closest('.combat-split'), x0 = e.clientX, y0 = e.clientY;
  let move;
  if (d.gut === 'side') {
    const w0 = split.querySelector('.side-tables').offsetWidth, max = split.offsetWidth - 320;
    move = ev => { sz.side = Math.round(Math.min(max, Math.max(300, w0 - (ev.clientX - x0)))); split.style.setProperty('--side', `min(${sz.side}px, 65%)`); };
  } else if (d.gut === 'sess') {
    const stack = g.parentElement, el = stack.querySelector('.session'), h0 = el.offsetHeight;
    // Vers le haut : la rentabilité peut prendre presque toute la place, les groupes de la map gardent leur titre et une ligne
    const max = h0 + stack.querySelector('[data-panel="groups"]').offsetHeight - 70;
    stack.classList.add('sized');
    move = ev => { sz.sess = Math.round(Math.min(max, Math.max(120, h0 - (ev.clientY - y0)))); stack.style.setProperty('--sess', sz.sess + 'px'); };
  } else {
    // Deux panneaux voisins : leur hauteur totale et leur poids total restent les mêmes, seul le partage change
    const box = g.parentElement, pa = box.querySelector(`[data-panel="${d.a}"]`), pb = box.querySelector(`[data-panel="${d.b}"]`);
    const h0 = pa.offsetHeight, H = h0 + pb.offsetHeight, W = (sz['w_' + d.a] || 1) + (sz['w_' + d.b] || 1);
    move = ev => {
      const h = Math.min(H - 70, Math.max(70, h0 + ev.clientY - y0));
      sz['w_' + d.a] = +(W * h / H).toFixed(3); sz['w_' + d.b] = +(W - sz['w_' + d.a]).toFixed(3);
      box.style.gridTemplateRows = sideRows().tpl.join(' ');
    };
  }
  document.body.classList.add('resizing', d.gut === 'side' ? 'col' : 'row');
  const up = () => {
    document.removeEventListener('pointermove', move); document.removeEventListener('pointerup', up);
    document.body.classList.remove('resizing', 'col', 'row'); saveSizes(); schedule();
  };
  document.addEventListener('pointermove', move); document.addEventListener('pointerup', up);
});
document.addEventListener('dblclick', e => {
  const g = e.target.closest('.gutter[data-gut]');
  if (!g) return;
  const d = g.dataset, sz = local.sizes;
  if (d.gut === 'row') { delete sz['w_' + d.a]; delete sz['w_' + d.b]; } else delete sz[d.gut];
  saveSizes(); schedule();
});

// Monstres en illustrations du jeu (nom et niveau au survol), avec leur nombre s'il y en a plusieurs
const mobs = list => `<span class="mobs">${(list || []).map(m => `<span class="mob" data-tip="${esc(m.Name + (m.Count > 1 ? ' ×' + m.Count : '') + (m.Name.includes('(niv.') || !m.Level ? '' : ' · niv. ' + m.Level))}">
    <img src="icon/monster/${m.Id}.png" alt="" loading="lazy">${m.Count > 1 ? `<i>${m.Count}</i>` : m.Level ? `<b>${m.Level}</b>` : ''}</span>`).join('')}</span>`;

Object.assign(views, {
  combat() {
    const char = S.HuntCharacterBox;
    // Caractéristiques en icônes du jeu (tête du personnage, sagesse et prospection de sa fiche) ; le libellé et le détail au survol
    const head = Number(T('HuntIcons').split('|')[0]);
    const headImg = head ? `<img class="ico" src="icon/head/${head}.png" alt="">` : '<span class="glyph">Niv</span>';
    const group = `<img class="ico" src="icon/stat/Group.png" alt="">`; // icône de groupe du jeu
    const ist = (img, name, label, cls = '') => `<div class="istat" data-tip="${esc(label + (S[name]?.tt ? ' — ' + S[name].tt : ''))}">${img}<b class="${cls}">${esc(T(name))}</b></div>`;
    const statImg = n => `<img class="ico" src="icon/stat/${n}.png" alt="">`;
    const saImg = statImg('Wisdom'), ppImg = statImg('PP');
    const ibtn = (name, icon, label) => `<button class="small ibtn" data-act="click" data-name="${name}" data-tip="${esc(label)}">${icon}</button>`;
    const pause = T('HuntPauseSession') || '⏸ Pause';
    const settings = local.open.combatSettings;
    const byGroup = S.HuntFightsGroup?.on;
    // État des alertes, rappelé sur le bouton des réglages (son coupé, pop-up, alertes monstres actives)
    const flags = (S.HuntSound?.on ? '🔔' : '🔕') + (S.HuntArchiPopup?.on ? '💬' : '') + (rows('HuntAlertList').some(a => a.Enabled) ? '🎯' : '');
    return `<div class="combat-top">
        <div class="stat-row">
          ${char?.items?.length ? `<span data-tip="Personnage affiché">${select('HuntCharacterBox')}</span>` : ''}
          ${ist(headImg, 'HuntLevel', 'Niveau')}${ist(group, 'HuntGroupLevel', 'Niveau du groupe', 'acc')}
          ${ist(saImg, 'HuntWisdom', 'Sagesse')}${ist(ppImg, 'HuntProspection', 'Prospection')}${ist(`<span class="grp">${ppImg}<img src="icon/stat/Group.png" alt=""></span>`, 'HuntGroupProspection', 'Prospection du groupe', 'acc')}
        </div>
        <div class="combat-actions">
          ${T('FarmState') ? farmBlock() : `<button class="small ibtn ${local.open.farm ? 'primary' : ''}" data-act="open" data-key="farm" data-tip="Objectif de farm : un objet et une quantité, comptés à chaque fin de combat">🎯</button>`}
          <span class="lbl">Session</span>
          ${ibtn('HuntNewSession', '↺', 'Nouvelle session : la session en cours passe dans l\'historique')}
          ${ibtn('HuntPauseSession', esc(pause.split(' ')[0]), pause.slice(pause.indexOf(' ') + 1) + ' : le temps ne compte plus dans la rentabilité, un combat la relance')}
          <button class="small ${settings ? 'primary' : ''}" data-act="open" data-key="combatSettings" data-tip="Alertes archi-monstres, alertes monstres, groupes du chronomètre">⚙ Réglages <span class="flags">${flags}</span></button>
        </div>
      </div>
      ${!T('FarmState') && local.open.farm ? farmBlock() : ''}
      ${settings ? combatSettings() : ''}
      ${T('HuntStatus') ? `<div class="alert">${esc(T('HuntStatus'))}</div>` : ''}
      ${T('HuntAlertBanner') ? `<div class="notice">${esc(T('HuntAlertBanner'))}</div>` : ''}
      ${chronoBlock()}
      ${combatPanels(byGroup)}`;  },
});
