'use strict';
// Onglet Métier : monter un métier au moindre coût (état « _metier », MainWindow.Metier.cs)

// Bilan d'un montant (coût net, négatif = ça rapporte) : gain en vert « +X », coût en rouge « −X »
const metierBal = net => net == null ? '<span class="muted">sans prix</span>'
  : `<span class="${net <= 0 ? 'ok' : 'bad'}">${net <= 0 ? '+' : '−'}${kamas(Math.abs(net))}</span>`;

Object.assign(views, {
  metier() {
    const m = S._metier;
    const modeTip = 'Ce que devient l\'objet fabriqué : rien (coût brut), revendu à l\'HDV (prix ×1), brisé (runes au jet moyen), ou le plus rentable des deux pour chaque recette';
    const filters = `<div class="filters">${select('MetierJob')}<span class="lbl">Niveau</span>${input('MetierFrom', '', '64px')}
      <span class="lbl">→</span>${input('MetierTo', '', '64px')}
      <span data-tip="${esc(modeTip)}">${select('MetierValue')}</span>
      ${m ? `<span class="chip" data-tip="XP du métier à gagner">${fmt(m.xp)} XP</span>
        <span class="chip strong" data-tip="${esc(`${fmt(m.totalCrafts)} fabrications en tout, avec la meilleure recette de chaque palier${m.incomplete ? '\nUn palier sans recette au prix connu : non compté' : ''}${m.mode ? '\nRevente au prix ×1 actuel : l\'HDV n\'absorbera pas des milliers d\'exemplaires à ce prix, le gain réel sera plus bas' : ''}`)}">${m.totalCost <= 0 ? 'Gain' : 'Coût'} ${metierBal(m.totalCost)}${m.incomplete ? ' +?' : ''}</span>` : ''}</div>`;
    if (!m) return filters + '<div class="empty">Choisissez un métier.</div>';
    const per = v => Math.abs(v) < 10 ? Math.abs(v).toFixed(1).replace('.', ',') : fmt(Math.abs(v));
    const opt = (o, best) => {
      const tip = `${o.n}\n${o.c} case(s) · ${o.x} XP par fabrication · ×${fmt(o.k)}\n`
        + `Coût des ingrédients : ${o.cost == null ? 'inconnu' : fmt(o.cost) + ' k'}\n`
        + `Revente HDV (×1) : ${o.sell == null ? 'pas de prix' : fmt(o.sell) + ' k'}\n`
        + `Brisage (runes, jet moyen) : ${o.runes == null ? '—' : fmt(o.runes) + ' k'}\n`
        + (o.per == null ? 'Sans prix' : `${o.per <= 0 ? 'Gain' : 'Coût'} : ${per(o.per)} k par XP`);
      const back = [o.sell != null ? `<span title="Revente HDV (×1)">🏷 ${fmtShort(o.sell)}</span>` : '',
        o.runes != null ? `<a class="item-link" data-act="item-brisage" data-id="${o.id}" title="Brisage : runes au jet moyen — voir dans le Brisage">🔨 ${fmtShort(o.runes)}</a>` : ''].filter(Boolean).join(' · ');
      return `<div class="metier-opt ${best ? 'best' : ''}" data-tip="${esc(tip)}">
        ${itemImg(o.id)}<div class="metier-name"><a class="item-link ellipsis" data-act="item-recipe" data-id="${o.id}">${esc(o.n)}</a>
        <div class="muted small">${o.c} cases · ${o.x} XP${o.own ? ` · <span class="ok">×${o.own}</span>` : ''}${back ? ' · ' + back : ''}</div></div>
        <div class="num"><b>×${fmt(o.k)}</b><div class="small">${metierBal(o.total)}</div></div></div>`;
    };
    return filters + `<div class="metier-steps">${m.steps.map(s => `<section class="card metier-step">
        <div class="metier-head"><b>Niv. ${s.a} → ${s.b}</b><span class="muted small" data-tip="Cases disponibles ; seules les recettes d'au moins ${Math.max(1, s.slots - 3)} case(s) rapportent de l'XP">${s.slots} cases (≥ ${Math.max(1, s.slots - 3)}) · ${fmt(s.xp)} XP</span>
          ${s.unpriced ? `<span class="muted small" data-tip="Recettes qui rapportent de l'XP mais dont un ingrédient n'a pas de prix HDV : consultez-les à l'HDV">${s.unpriced} sans prix</span>` : ''}</div>
        ${s.opts.length ? s.opts.map((o, i) => opt(o, i === 0 && o.per != null)).join('') : '<div class="muted small">Aucune recette ne rapporte d\'XP à ce palier.</div>'}
      </section>`).join('')}</div>
      <div class="muted small" data-tip="Cases : 2 avant le niveau 10, puis niveau ÷ 20 + 3. XP par fabrication selon les cases : 1, 10, 25, 50, 100, 250, 500, 1000 ; plus d'XP quand la recette a moins de (cases − 3) cases. Règles des serveurs 1.29 (émulateurs StarLoco, Ancestra), recoupées avec les guides Retro.">Règles d'XP des métiers 1.29 ⓘ · meilleure recette par palier en haut · <span class="ok">+ gain</span> / <span class="bad">− coût</span></div>`;
  },
});
