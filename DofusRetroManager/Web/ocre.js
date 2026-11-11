'use strict';
// Onglet Ocre (quête du Dofus Ocre)

Object.assign(views, {
  ocre() {
    const sel = rows('OcreGrid')[S.OcreGrid?.sel ?? -1];
    return `<div class="filters"><span class="lbl">Personnage</span>${select('OcreCharacter')}
        <span class="lbl">Étape atteinte</span>${select('OcreStep')}<span class="lbl">Coût de</span>${select('OcreKindFilter')}</div>
      <div class="strong acc" style="margin-bottom:10px">${esc(T('OcreSummary'))}</div>
      <div class="split">
        ${table('OcreGrid', [
          { h: 'Étape', k: 'Step', num: true, f: r => `<span class="strong">${r.Step}</span>` },
          { h: 'Âmes', k: 'Kind' },
          { h: 'Total', k: 'Total', num: true },
          { h: 'Possédées', k: 'Owned', num: true },
          { h: 'Manquantes', k: 'Missing', num: true },
          { h: 'dont boss', k: 'MissingBoss', num: true },
          { h: 'dont archi', k: 'MissingArchi', num: true },
          { h: 'Coût', k: 'Cost', num: true, f: r => `<span class="acc strong">${esc(r.CostText)}</span>` },
          { h: 'État', k: 'State', f: r => `<span class="${r.Validable ? 'ok strong' : r.Done ? 'muted' : ''}">${esc(r.State)}</span>` },
        ], { empty: T('OcreSummary') || 'Aucune étape.' })}
        <aside class="detail glass">${sel ? `<h2 class="acc">${esc(T('OcreDetailTitle'))}</h2>
          <div class="muted">${esc(T('OcreDetailSub'))}</div>
          ${ocreSouls(rows('OcreSouls'))}` : '<div class="empty">Sélectionnez une étape.</div>'}</aside>
      </div>`;
  },
});

// Âmes de l'étape en illustrations (manquantes d'abord) : prix sous chaque monstre manquant, ✓ sur les possédées ;
// nom, catégorie, niveau et pierre au survol ; clic : sa pierre dans l'onglet PDA
function ocreSouls(list) {
  if (!list.length) return '';
  const missing = list.filter(s => !s.Owned), owned = list.filter(s => s.Owned);
  const tile = s => `<div class="ocre-mob ${s.Owned ? 'owned' : ''} ${s.Price == null && !s.Owned ? 'unpriced' : ''}" data-act="item-pda" data-id="${s.MonsterId}"
      data-tip="${esc(`${s.Name} · ${s.Kind} · niv. ${s.Level}\n${s.Owned ? 'Possédée' + (s.Where ? ' : ' + s.Where : '') : s.PriceText}\nClic : sa pierre dans l'onglet PDA`)}">
      <img src="icon/monster/${s.MonsterId}.png" alt="" loading="lazy">${s.Owned ? '<i class="ok">✓</i>' : ''}
      <span class="nm">${esc(s.Name)}</span><b>${s.Owned ? '' : s.Price != null ? kamas(s.Price) : '?'}</b></div>`;
  const block = (title, l) => l.length ? `<p class="section-title">${title} · ${l.length}</p><div class="ocre-mobs">${l.map(tile).join('')}</div>` : '';
  return block('Manquantes', missing) + block('Possédées', owned);
}
