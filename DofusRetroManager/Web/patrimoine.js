'use strict';
// Onglet Patrimoine : valeur de tous vos biens (état « _patrimoine », MainWindow.Patrimoine.cs)

Object.assign(views, {
  patrimoine() {
    const p = S._patrimoine;
    if (!p) return '<div class="empty">Données du jeu en chargement…</div>';
    if (!p.chars.length) return '<div class="empty">Aucune banque ni inventaire connu : ouvrez votre banque en jeu, capture lancée.</div>';
    const kpi = (v, l, tip, cls = '') => `<div class="sess-kpi ${cls}" data-tip="${esc(tip)}"><b>${v}</b><span>${l}</span></div>`;
    // Tableaux de la page : lignes posées comme celles d'un contrôle (tri par colonne)
    S.PatrChars = { rows: p.chars.map(c => ({ ...c, t: c.b + c.i + c.k })) };
    S.PatrTop = { rows: p.top.map(x => ({ ...x, pct: p.total ? x.v / p.total : 0 })) };
    const hist = p.hist.map(h => [new Date(h[0] + 'T12:00:00').getTime(), h[1]]);
    const prev = p.hist.length > 1 ? p.hist[p.hist.length - 2][1] : null;
    const delta = prev != null ? p.total - prev : null;
    return `<div class="sess-kpis patr-kpis">
        ${kpi(kamas(p.total), 'Total', 'Objets + kamas en banque + offres en vente' + (delta != null ? `\nDepuis le relevé précédent : ${delta >= 0 ? '+' : ''}${fmt(delta)} k` : ''), 'acc')}
        ${kpi(kamas(p.objects), 'Objets', `Banques et inventaires, au prix HDV (sinon marchand)${p.unpriced ? ` · ${p.unpriced} objet(s) sans prix, comptés 0` : ''}`)}
        ${kpi(kamas(p.kamas), 'Kamas', `Kamas en banque (${p.banks} banque(s) distincte(s))`)}
        ${kpi(kamas(p.sales), 'En vente', `${p.salesCount} offre(s) à l'HDV, à leur prix de vente (Mes échanges)`)}
        ${delta != null ? kpi(`<span class="${delta < 0 ? 'bad' : 'ok'}">${delta >= 0 ? '+' : '−'}${fmtShort(Math.abs(delta))}</span>`, 'Évolution', 'Écart avec le jour précédent relevé') : ''}
      </div>
      <div class="split patr-split">
        <div class="patr-main">
          <section class="card"><h3>Évolution</h3>${hist.length > 1 ? chart(hist) : '<div class="muted small">Une valeur par jour : la courbe apparaît à partir du 2e jour.</div>'}</section>
          <h3>Par personnage</h3>
          ${table('PatrChars', [
            { h: '', f: r => r.g ? `<img class="ico" src="icon/head/${r.g}.png" alt="">` : '' },
            { h: 'Personnage', k: 'n', f: r => `<span class="strong">${esc(r.n)}</span>` },
            { h: 'Banque', k: 'b', num: true, f: r => r.b ? kamas(r.b) : '—' },
            { h: 'Inventaire', k: 'i', num: true, f: r => kamas(r.i) },
            { h: 'Kamas', k: 'k', num: true, f: r => r.k ? kamas(r.k) : '—' },
            { h: 'Total', k: 't', num: true, f: r => `<span class="acc strong">${kamas(r.t)}</span>` },
          ], { select: false })}
          <div class="muted small" title="Une banque est partagée par le compte : les banques au même contenu (vues par plusieurs personnages du compte) sont comptées une fois, sur le personnage qui l'a ouverte le plus récemment.">Banque du compte comptée une fois ⓘ</div>
        </div>
        <aside class="detail glass"><h3>Objets les plus précieux</h3>
          ${table('PatrTop', [
            { h: '', f: r => itemImg(r.id) },
            { h: 'Objet', k: 'n', f: r => `<a class="item-link ellipsis patr-name" data-act="item-hdv" data-id="${r.id}" title="${esc(r.n)} — voir à l'HDV">${esc(r.n)}</a><div class="muted small">×${fmt(r.q)}</div>` },
            { h: 'Valeur', k: 'v', num: true, f: r => `${kamas(r.v)}<div class="muted small">${(r.pct * 100).toFixed(1).replace('.', ',')} %</div>` },
          ], { select: false })}
        </aside>
      </div>`;
  },
});
