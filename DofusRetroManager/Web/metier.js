'use strict';
// Onglet Métier : monter un métier au moindre coût (état « _metier », MainWindow.Metier.cs)

Object.assign(views, {
  metier() {
    const m = S._metier;
    const filters = `<div class="filters">${select('MetierJob')}<span class="lbl">Niveau</span>${input('MetierFrom', '', '64px')}
      <span class="lbl">→</span>${input('MetierTo', '', '64px')}
      <span data-tip="Coût net : le prix HDV de l'objet fabriqué est déduit (vous le revendez)">${check('MetierResell', 'Revente déduite')}</span>
      ${m ? `<span class="chip" data-tip="XP du métier à gagner">${fmt(m.xp)} XP</span>
        <span class="chip strong" data-tip="${esc(`${fmt(m.totalCrafts)} fabrications en tout${m.incomplete ? '\nUn palier sans recette au prix connu : non compté' : ''}`)}">${m.resell ? 'Coût net' : 'Coût'} ${kamas(m.totalCost)}${m.incomplete ? ' +?' : ''}</span>` : ''}</div>`;
    if (!m) return filters + '<div class="empty">Choisissez un métier.</div>';
    const opt = (o, best) => `<div class="metier-opt ${best ? 'best' : ''}" data-tip="${esc(`${o.n}\n${o.c} case(s) · ${o.x} XP par fabrication\nCoût ${o.cost == null ? 'inconnu' : fmt(o.cost) + ' k'}${o.sell != null ? ' · revente ' + fmt(o.sell) + ' k' : ''}\n${o.per == null ? 'Sans prix' : (Math.abs(o.per) < 10 ? o.per.toFixed(1).replace('.', ',') : fmt(o.per)) + ' k par XP'}`)}">
        ${itemImg(o.id)}<div class="metier-name"><a class="item-link ellipsis" data-act="item-recipe" data-id="${o.id}">${esc(o.n)}</a>
        <div class="muted small">${o.c} cases · ${o.x} XP${o.own ? ` · <span class="ok">faisable ×${o.own}</span>` : ''}</div></div>
        <div class="num"><b>×${fmt(o.k)}</b><div class="${o.total == null ? 'muted' : o.total < 0 ? 'ok' : 'bad'} small">${o.total == null ? 'sans prix' : kamas(o.total)}</div></div></div>`;
    return filters + `<div class="metier-steps">${m.steps.map(s => `<section class="card metier-step">
        <div class="metier-head"><b>Niv. ${s.a} → ${s.b}</b><span class="muted small">${s.slots} cases · ${fmt(s.xp)} XP</span>
          ${s.unpriced ? `<span class="muted small" data-tip="Recettes qui rapportent de l'XP mais dont un ingrédient n'a pas de prix HDV : consultez-les à l'HDV">${s.unpriced} sans prix</span>` : ''}</div>
        ${s.opts.length ? s.opts.map((o, i) => opt(o, i === 0 && o.per != null)).join('') : '<div class="muted small">Aucune recette ne rapporte d\'XP à ce palier.</div>'}
      </section>`).join('')}</div>
      <div class="muted small" data-tip="Cases : 2 avant le niveau 10, puis niveau ÷ 20 + 3. XP par fabrication selon les cases : 1, 10, 25, 50, 100, 250, 500, 1000 ; plus d'XP quand la recette a moins de (cases − 3) cases. Règles des serveurs 1.29 (émulateurs StarLoco, Ancestra), recoupées avec les guides Retro.">Règles d'XP des métiers 1.29 ⓘ · meilleure recette par palier en haut, nombre de fabrications et coût total</div>`;
  },
});
