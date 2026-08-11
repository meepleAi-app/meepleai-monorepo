/* MeepleAI Nav Prototype — Game Detail (/games/[id]), aligned to sp4-game-detail.
   Full page (not a drawer). Hero + 5 tabs. Tab "Partite recenti" lists the
   atomic Sessions of THIS game across GameNights — here "Session" is correctly
   user-facing. Session/GameNight/Player taps open the drawer stack; a game-name
   tap elsewhere navigates full-page (handled in the drawer). */

const GD_TABS = [
  { k: 'overview', lb: 'Overview' },
  { k: 'plays', lb: 'Partite recenti' },
  { k: 'house', lb: 'House rules' },
  { k: 'kb', lb: 'Knowledge base' },
  { k: 'stats', lb: 'Statistiche' },
];

function GdPlays({ g, state, onOpen }) {
  const plays = window.GN.sessionsForGame(g.id);
  if (!plays.length) {
    return h(EmptyState, { entity: 'event', icon: '🎯', title: 'Nessuna partita registrata', desc: 'Questo gioco non è ancora stato giocato. Avvia la prima Game Night per registrare una partita.', cta: '+ Avvia Game Night' });
  }
  return h('div', null,
    state === 'offline' ? h('div', { className: 'offline-bar' }, h('span', null, '📡 Partite dalla cache · sync in attesa')) : null,
    plays.map((s, i) => {
      const mvp = window.GN.playerById[s.mvp];
      return h('div', { className: 'play-row', key: i },
        h('div', { className: 'pr-main' },
          h('button', { className: 'pr-date', onClick: () => onOpen('session', s.nightId + ':' + s.n, { gameNightId: s.nightId }) }, 'Sessione del ' + s.date.replace('Oggi · ', '')),
          h('div', { className: 'pr-sub' },
            h('button', { className: 'pr-link', onClick: () => onOpen('gamenight', s.nightId) }, '📅 ' + s.nightName),
            mvp ? h(React.Fragment, null, h('span', null, '·'),
              h('button', { className: 'pr-mvp', onClick: () => onOpen('player', s.mvp, { gameNightId: s.nightId }) }, 'MVP ' + mvp.title + (s.score ? ' · ' + s.score + ' pt' : ''))) : null)),
        h('div', { className: 'pr-players' },
          s.players.slice(0, 4).map(pid => { const p = window.GN.playerById[pid] || {}; return h('span', { key: pid, className: 'avatar sm', style: { background: p.cover, color: '#fff' }, title: p.title }, p.initials); })),
        s.status === 'live'
          ? h('span', { className: 'pr-status live' }, h('span', { className: 'live-dot' }), 'live')
          : h('span', { className: 'pr-status completed' }, 'completata')
      );
    })
  );
}

function GdStats({ g }) {
  const plays = window.GN.sessionsForGame(g.id);
  const kpis = [
    { v: g.totalPlays, l: 'Partite totali', gap: false },
    { v: Math.round((g.totalPlays || 0) * 1.2) + 'h', l: 'Tempo giocato', gap: true },
    { v: Math.round((g.winRate || 0) * 100) + '%', l: 'Win rate', gap: false },
    { v: plays.filter(s => s.mvp).length, l: 'MVP registrati', gap: false },
  ];
  return h('div', { className: 'kpi-grid' },
    kpis.map((k, i) => h('div', { className: 'kpi', key: i },
      h('span', { className: 'kv2' }, k.v),
      h('span', { className: 'kl2' }, k.l, k.gap ? h('span', { style: { marginLeft: 'var(--s-2)' } }, h(Gap, { cat: 'GAP-DATA', mini: true, loc: 'Game stats · ' + k.l, note: 'campo non in data.js, valore stimato' })) : null)))
  );
}

function GameDetail({ id, state, onOpen, onNav }) {
  const [tab, setTab] = React.useState('overview');
  const g = window.DS.byId[id];

  if (state === 'loading') {
    return h('div', null,
      h('div', { className: 'gd-hero' }, h(SkeletonCard, { hgt: 240 }), h('div', null, h(SkeletonLine, { w: 60, hgt: 32 }), h(SkeletonLine, { w: 40 }), h(SkeletonLine, { w: 80 }), h(SkeletonLine, { w: 50 }))),
      h('div', { className: 'gd-tabs' }, GD_TABS.map(t => h('button', { key: t.k, className: 'gd-tab', disabled: true }, t.lb))),
      h(SkeletonLine, { w: 90 }), h(SkeletonLine, { w: 70 }),
      h('div', { style: { marginTop: 'var(--s-3)' } }, h(Gap, { cat: 'GAP-STATE', loc: 'Game detail · loading', note: 'skeleton non nel mockup (nessun JSX twin)' })));
  }
  if (!g || state === 'error') {
    return h('div', { className: 'state-block e-game' },
      h('div', { className: 'sb-icon' }, '🔍'),
      h('h2', null, g ? 'Errore di caricamento' : 'Gioco non trovato'),
      h('p', null, g ? 'Non riesco a caricare questo gioco.' : 'L\u2019id "' + id + '" non corrisponde a nessun gioco.'),
      h('button', { className: 'sb-cta', onClick: () => onNav('/library') }, '← Torna alla Library'),
      h('div', { style: { marginTop: 'var(--s-3)' } }, h(Gap, { cat: 'GAP-STATE', loc: 'Game detail · error/404', note: 'pattern 404 ricostruito (nessun JSX twin)' })));
  }

  const offline = state === 'offline';
  const owned = g.status === 'owned' || g.status === 'wishlist';
  const cta = owned
    ? { cls: 'gn', lb: 'Avvia Game Night' }
    : { cls: 'add', lb: 'Aggiungi alla Library' };

  const body = {
    overview: h('div', { className: 'gd-body' },
      h('p', null, g.title + ' è un gioco di ' + g.author + ' (' + g.year + '), edito da ' + g.publisher + '. Da ' + g.players + ' giocatori, durata ' + g.duration + ', complessità ' + g.weight + '/5.'),
      h('p', { style: { marginTop: 'var(--s-3)' } }, 'Descrizione estesa, meccaniche dettagliate e categoria non sono presenti nel dataset fixture.'),
      h('div', { className: 'gd-mech' },
        h('span', { className: 'gd-tag' }, 'Peso ' + g.weight), h('span', { className: 'gd-tag' }, g.players + ' giocatori'), h('span', { className: 'gd-tag' }, g.duration),
        h(Gap, { cat: 'GAP-DATA', loc: 'Game detail · Overview', note: 'description/meccaniche/categoria assenti in data.js' }))),
    plays: h(GdPlays, { g, state, onOpen }),
    house: h('div', { className: 'gd-body' },
      h('p', null, 'Regole della casa per ' + g.title + '. Nessuna ancora definita.'),
      h('div', { style: { marginTop: 'var(--s-3)' } }, h(Gap, { cat: 'GAP-FEATURE', loc: 'Game detail · House rules', note: 'editor house-rules TBD', block: true }))),
    kb: h('div', { className: 'gd-body' },
      h('p', null, 'Documenti e knowledge base collegati a ' + g.title + '.'),
      h('div', { style: { marginTop: 'var(--s-3)', display: 'flex', gap: 'var(--s-3)', alignItems: 'center', flexWrap: 'wrap' } },
        h('button', { className: 'btn primary e-kb', onClick: () => onNav('/knowledge-base') }, 'Apri Knowledge Base →'),
        h(Gap, { cat: 'GAP-FEATURE', loc: 'Game detail · Knowledge base', note: 'link a /knowledge-base (hub non costruito)' }))),
    stats: h(GdStats, { g }),
  }[tab];

  return h('div', null,
    offline ? h(OfflineBar, { note: 'scheda gioco dalla cache' }) : null,
    state === 'empty' ? h('div', { className: 'dr-empty-note', style: { marginBottom: 'var(--s-4)' } }, 'Stato empty non applicabile a livello pagina (il gioco esiste o è 404). Vedi tab “Partite recenti”.') : null,

    h('div', { className: 'gd-hero' },
      h('div', { className: 'gd-cover', style: { background: g.cover } }, g.coverEmoji),
      h('div', { className: 'gd-info' },
        h('div', { className: 'gd-badges' },
          h('span', { className: 'gd-status ' + (g.status === 'wishlist' ? 'wishlist' : 'owned') }, g.status === 'wishlist' ? 'wishlist' : 'owned')),
        h('h1', { className: 'gd-title' }, g.title),
        h('div', { className: 'gd-pub' }, g.publisher + ' · ' + g.author),
        h('div', { className: 'gd-rating-row' },
          isPlayed(g) ? h(React.Fragment, null, h('span', { className: 'gd-rating' }, '★ ' + g.stars + '/5'), h('span', { className: 'gd-rating-sec' }, 'tuo · BGG ' + g.rating))
                      : h(React.Fragment, null, h('span', { className: 'gd-rating' }, g.rating), h('span', { className: 'gd-rating-sec' }, 'BGG · non ancora giocato'))),
        h('div', { className: 'gd-tags' },
          h('span', { className: 'gd-tag' }, '👥 ' + g.players), h('span', { className: 'gd-tag' }, '⏱ ' + g.duration), h('span', { className: 'gd-tag' }, '🎯 peso ' + g.weight)),
        h('div', { className: 'gd-cta-row' },
          h('button', { className: 'gd-cta ' + cta.cls }, cta.lb),
          h(Gap, { cat: 'GAP-CTA', loc: 'Game detail · "' + cta.lb + '"', note: cta.cls === 'gn' ? 'flow create Game Night TBD' : 'flow add-to-library TBD' })))),

    h('div', { className: 'gd-tabs' },
      GD_TABS.map(t => h('button', { key: t.k, className: 'gd-tab', 'data-active': tab === t.k, onClick: () => setTab(t.k) },
        t.lb, t.k === 'plays' && window.GN.sessionsForGame(g.id).length ? h('span', { style: { marginLeft: 'var(--s-1)', fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', color: 'var(--text-muted)' } }, window.GN.sessionsForGame(g.id).length) : null))),
    body
  );
}

Object.assign(window, { GameDetail });
