/* MeepleAI Nav Prototype — /discover (sp4-discover). Hero + horizontal-scroll
   sections: trending games · nuovi agent · community toolkits · suggested. */

function DiscFeaturedCard({ g, onNav }) {
  return h('div', { className: 'disc-feat', onClick: () => onNav('/games/' + g.id) },
    h('div', { className: 'disc-feat-cover', style: { background: g.cover } }, g.coverEmoji),
    h('div', { className: 'disc-feat-body' },
      h('div', { className: 'disc-feat-title' }, g.title),
      h('div', { className: 'disc-feat-meta' }, '★ ' + g.rating + ' BGG · ' + g.players + ' · ' + g.duration),
      h('div', { className: 'disc-tags' }, h('span', { className: 'gd-tag' }, 'peso ' + g.weight))));
}
function DiscAgentCard({ a, onNav }) {
  const g = a.gameId ? window.DS.byId[a.gameId] : null;
  return h('div', { className: 'disc-agent', onClick: () => onNav('/agents/' + a.id) },
    h('span', { className: 'disc-agent-av' }, '🤖'),
    h('div', { className: 'disc-agent-body' },
      h('div', { className: 'disc-agent-name' }, a.title),
      h('div', { className: 'disc-agent-sub' }, g ? g.title : 'Multi-game'),
      h('div', { className: 'disc-agent-stats' }, '💬 ' + a.invocations + ' · ' + window.GN.agentAccuracy(a) + '% acc')));
}
function DiscToolkitCard({ t, onNav }) {
  const g = t.gameId ? window.DS.byId[t.gameId] : null;
  return h('div', { className: 'disc-tk', onClick: () => onNav('/toolkit/' + t.id) },
    h('span', { className: 'disc-tk-cover', style: { background: t.cover } }, t.coverEmoji),
    h('div', { className: 'disc-tk-body' },
      h('div', { className: 'disc-tk-title' }, t.title),
      h('div', { className: 'disc-tk-sub' }, (g ? g.title : 'Universale') + ' · ⬇ ' + t.useCount),
      h(Gap, { cat: 'GAP-ROUTE', mini: true, loc: 'Discover · toolkit ' + t.title, note: '/toolkit/[id] detail TBD' })));
}

function Discover({ state, onNav }) {
  if (state === 'loading') {
    return h('div', null,
      h('div', { className: 'skel', style: { height: '160px', borderRadius: 'var(--r-2xl)', marginBottom: 'var(--s-6)' } }),
      h(SkeletonLine, { w: 30, hgt: 18 }), h('div', { className: 'disc-rail' }, [0, 1, 2].map(i => h('div', { key: i, style: { flex: '0 0 240px' } }, h(SkeletonCard, { hgt: 150 })))),
      h('div', { style: { marginTop: 'var(--s-4)' } }, h(Gap, { cat: 'GAP-STATE', loc: 'Discover · loading', note: 'skeleton non nel mockup' })));
  }
  if (state === 'error') {
    return h('div', null, h(ErrorState, { title: 'Discover non disponibile', msg: 'Il motore di raccomandazione non risponde. Riprova.' }),
      h('div', { style: { textAlign: 'center', marginTop: 'var(--s-3)' } }, h(Gap, { cat: 'GAP-STATE', loc: 'Discover · error', note: 'retry per-sezione previsto, qui globale' })));
  }
  if (state === 'empty') {
    return h('div', null, h(EmptyState, { entity: 'game', icon: '🧭', title: 'Discover sarà attivo presto', desc: 'Stiamo preparando i suggerimenti. Torna tra poco.' }),
      h('div', { style: { textAlign: 'center' } }, h(Gap, { cat: 'GAP-STATE', loc: 'Discover · empty', note: 'empty raro nel discover' })));
  }

  const offline = state === 'offline';
  const games = window.DS.games;
  const agents = window.DS.agents;
  const toolkits = window.DS.toolkits;
  const suggested = [
    { type: 'game', id: 'g-wingspan', reason: 'Hai giocato 3× Wingspan questo mese', cta: 'Apri' },
    { type: 'agent', id: 'a-catan-coach', reason: 'Hai una serata Catan in programma', cta: 'Prova' },
    { type: 'toolkit', id: 'tk-azul-v2', reason: 'Possiedi Azul ma nessun toolkit attivo', cta: 'Esplora' },
    { type: 'game', id: 'g-brass', reason: 'Simile a Ark Nova che hai apprezzato', cta: 'Aggiungi' },
  ];

  return h('div', null,
    offline ? h(OfflineBar, { note: 'discover dalla cache (fixture statico)' }) : null,
    h('div', { className: 'disc-hero' },
      h('div', { className: 'disc-hero-inner' },
        h('h1', null, 'Scopri nuovi giochi'),
        h('p', null, 'Suggeriti per te in base alla tua library e alle ultime serate.'),
        h('button', { className: 'gd-cta add', onClick: () => { const el = document.getElementById('disc-trending'); if (el) el.scrollIntoView({ behavior: 'smooth' }); } }, 'Esplora trending'))),

    h('div', { id: 'disc-trending', className: 'section-label', style: { marginTop: 'var(--s-2)' } }, 'Trending questa settimana', h('span', { className: 'ln' }), h('span', { className: 'pip-more' }, games.length + ' giochi')),
    h('div', { className: 'disc-rail' }, games.map(g => h(DiscFeaturedCard, { key: g.id, g, onNav }))),

    h('div', { className: 'section-label' }, 'Nuovi agent della community', h('span', { className: 'ln' }), h('span', { className: 'pip-more' }, agents.length)),
    h('div', { className: 'disc-rail' }, agents.map(a => h(DiscAgentCard, { key: a.id, a, onNav }))),

    h('div', { className: 'section-label' }, 'Toolkit popolari', h('span', { className: 'ln' }), h('span', { className: 'pip-more' }, toolkits.length)),
    h('div', { className: 'disc-tk-grid' }, toolkits.map(t => h(DiscToolkitCard, { key: t.id, t, onNav }))),

    h('div', { className: 'section-label' }, 'Suggeriti per te', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-ENTITY', mini: true, loc: 'Discover · suggeriti', note: 'algoritmo di raccomandazione opaco (fixture)' })),
    h('div', { className: 'feed' }, suggested.map((s, i) => { const e = window.DS.byId[s.id] || {}; const route = s.type === 'agent' ? '/agents/' + s.id : (s.type === 'toolkit' ? '/toolkit/' + s.id : '/games/' + s.id);
      return h('div', { className: 'feed-row', key: i },
        h('span', { className: 'sugg-type ' + ('e-' + s.type) }, s.type),
        h('div', { className: 'fr-text' }, h('b', null, e.title), h('span', null, '· ' + s.reason)),
        h('button', { className: 'btn sm primary e-' + s.type, onClick: () => onNav(route) }, s.cta)); }))
  );
}

Object.assign(window, { Discover });
