'use strict';
// Onglet Élevage : vos dragodindes (état « _elevage », MainWindow.Elevage.cs). Une carte par monture : couleurs de la
// race, nom, sexe, niveau, jauges (endurance, maturité, amour, énergie, sérénité, fatigue), reproductions et capacités.

const ELEV_WHERE = { eq: ['🐎', 'Équipée'], enclos: ['🌾', 'Enclos'], etable: ['🏠', 'Étable'] };
const rgb = c => '#' + (c >>> 0 & 0xffffff).toString(16).padStart(6, '0');

function elevBar(label, v, max, tip, cls = '') {
  const p = max > 0 ? Math.max(0, Math.min(100, v / max * 100)) : 0;
  return `<div class="elev-bar ${cls}" data-tip="${esc(tip || `${label} : ${fmt(v)} / ${fmt(max)}`)}"><span>${label}</span>
    <i><b style="width:${p}%"></b></i><em>${p >= 100 ? '✓' : Math.floor(p) + '%'}</em></div>`;
}

// Sérénité : de min (agressive) à max (sereine), 0 au milieu ; jauge centrée
function elevSerenity(m) {
  const min = m.SerenityMin || -10000, max = m.SerenityMax || 10000, v = m.Serenity;
  const left = (Math.min(v, 0) - min) / (max - min) * 100, w = Math.abs(v) / (max - min) * 100;
  const mood = v > 2000 ? 'sereine' : v < -2000 ? 'agressive' : 'équilibrée';
  return `<div class="elev-bar seren" data-tip="${esc(`Sérénité : ${fmt(v)} (${mood})\nDe ${fmt(min)} (agressive) à ${fmt(max)} (sereine). Endurance et maturité montent avec une sérénité basse ou équilibrée, l'amour avec une sérénité haute.`)}">
    <span>Séré.</span><i><b style="left:${left}%;width:${w}%"></b><u></u></i><em>${v > 0 ? '+' : v < 0 ? '−' : ''}${Math.abs(v) >= 1000 ? (Math.abs(v) / 1000).toFixed(1).replace('.', ',') + 'k' : Math.abs(v)}</em></div>`;
}

function elevCard(m) {
  const [ico, place] = ELEV_WHERE[m.where] || ['', ''];
  const name = m.Name && m.Name !== '?' ? m.Name : m.ride.n.replace(/^Dragodinde /, '');
  const fec = m.Fecundation >= 0 ? `<span class="chip ok" data-tip="Fécondée depuis ${m.Fecundation} h">🤰 ${m.Fecundation} h</span>`
    : m.Fecundable ? '<span class="chip ok" data-tip="Prête à être fécondée">💞 Fécondable</span>' : '';
  const anc = (m.anc || []).length ? `\nAncêtres : ${m.anc.join(', ')}` : '';
  return `<div class="elev-card ${m.Fecundable || m.Fecundation >= 0 ? 'fec' : ''}">
    <div class="elev-head" data-tip="${esc(m.ride.n + anc)}">
      <span class="elev-colors">${m.ride.c.map(c => `<i style="background:${rgb(c)}"></i>`).join('')}</span>
      <div class="elev-name"><b class="ellipsis">${esc(name)}</b>${name !== m.ride.n.replace(/^Dragodinde /, '') ? `<div class="muted small ellipsis">${esc(m.ride.n.replace(/^Dragodinde /, ''))}</div>` : ''}</div>
      <span class="elev-sex ${m.Female ? 'f' : 'm'}" data-tip="${m.Female ? 'Femelle' : 'Mâle'}">${m.Female ? '♀' : '♂'}</span>
      <span class="elev-lvl" data-tip="${esc(`Niveau ${m.Level}\nXP ${fmt(m.Xp)} (niveau suivant : ${fmt(m.XpMax)})`)}">${m.Level}</span>
    </div>
    <div class="elev-bars">
      ${elevBar('Endu.', m.Stamina, m.StaminaMax, `Endurance : ${fmt(m.Stamina)} / ${fmt(m.StaminaMax)}`)}
      ${elevBar('Matu.', m.Maturity, m.MaturityMax, `Maturité : ${fmt(m.Maturity)} / ${fmt(m.MaturityMax)}`)}
      ${elevBar('Amour', m.Love, m.LoveMax, `Amour : ${fmt(m.Love)} / ${fmt(m.LoveMax)}`)}
      ${elevBar('Éner.', m.Energy, m.EnergyMax, `Énergie : ${fmt(m.Energy)} / ${fmt(m.EnergyMax)}`, 'energy')}
      ${elevSerenity(m)}
      ${elevBar('Fatig.', m.Tiredness, m.TirednessMax, `Fatigue : ${m.Tiredness} / ${m.TirednessMax}`, 'tired')}
    </div>
    <div class="elev-foot">
      <span class="chip" data-tip="${place}">${ico}</span>
      <span class="chip" data-tip="Reproductions faites / maximum">👶 ${m.Reproductions}/${m.ReproductionsMax}</span>
      ${m.Mountable ? '<span class="chip" data-tip="Montable">🏇</span>' : ''}${m.Wild ? '<span class="chip bad" data-tip="Sauvage">Sauvage</span>' : ''}
      <span class="chip" data-tip="Pods transportés">${fmt(m.Pods)} pods</span>${fec}
      ${(m.caps || []).map(c => `<span class="chip acc" data-tip="${esc(c.d)}">${esc(c.n)}</span>`).join('')}
    </div></div>`;
}

Object.assign(views, {
  elevage() {
    const data = S._elevage;
    if (!data?.length) return '<div class="empty">Aucune dragodinde connue : ouvrez votre étable ou votre enclos en jeu, capture lancée.</div>';
    const f = local.elev ||= { c: '', w: '', s: '', fec: false };
    const pill = (k, v, l, tip) => `<button class="pill ${f[k] === v ? 'on' : ''}" data-act="elev" data-k="${k}" data-v="${v}" title="${esc(tip || '')}">${l}</button>`;
    const chars = data.length > 1 ? `<span class="togs">${pill('c', '', 'Tous')}${data.map(d => pill('c', d.c, d.g ? `<img class="ico xs" src="icon/head/${d.g}.png" alt="">${esc(d.c)}` : esc(d.c))).join('')}</span>` : '';
    const shown = data.filter(d => !f.c || d.c === f.c);
    const all = shown.flatMap(d => d.mounts);
    const keep = m => (!f.w || m.where === f.w) && (!f.s || (f.s === 'f') === m.Female) && (!f.fec || m.Fecundable || m.Fecundation >= 0);
    const n = all.filter(keep).length;
    return `<div class="filters">${chars}
        <span class="togs">${pill('w', '', 'Toutes')}${pill('w', 'eq', '🐎', 'Équipée')}${pill('w', 'enclos', '🌾 Enclos')}${pill('w', 'etable', '🏠 Étable')}</span>
        <span class="togs">${pill('s', '', '♂♀')}${pill('s', 'm', '♂', 'Mâles')}${pill('s', 'f', '♀', 'Femelles')}</span>
        <button class="pill ${f.fec ? 'on' : ''}" data-act="elev" data-k="fec" title="Fécondables ou fécondées">💞</button>
        <span class="chip">${n} / ${all.length} dragodinde(s)</span></div>
      <div class="elev-list">${shown.map(d => {
        const list = d.mounts.filter(keep);
        return list.length ? `${shown.length > 1 ? `<h3 class="elev-char">${d.g ? `<img class="ico" src="icon/head/${d.g}.png" alt="">` : ''}${esc(d.c)}
          ${d.seen ? `<span class="muted small" title="Dernière ouverture de l'étable ou de l'enclos">· ${esc(d.seen)}</span>` : ''}</h3>` : ''}
          <div class="elev-grid">${list.map(elevCard).join('')}</div>` : '';
      }).join('') || '<div class="empty">Aucune dragodinde ne correspond.</div>'}</div>`;
  },
});
