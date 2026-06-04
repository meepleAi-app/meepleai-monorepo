/* MeepleAI Nav Prototype — Library (/library), aligned to sp4-library-desktop.
   Filter chips (Tutti/Owned/Wishlist/Played) + MeepleCard "game" grid variant.
   Played → sort by last-played desc (derived from GameNight sessions); else A→Z.
   Card click → /games/[id]. JSX twin absent → reconstructed from SP4 §H. */

const LIB_FILTERS = [
  { k: 'all', lb: 'Tutti' },
  { k: 'owned', lb: 'Owned' },
  { k: 'wishlist', lb: 'Wishlist' },
  { k: 'played', lb: 'Played' },
];

function addedDaysAgo(id) {
  let s = 0; for (let i = 0; i < id.length; i++) s += id.charCodeAt(i);
  return (s % 88) + 2;
}
function isPlayed(g) { return (g.totalPlays || 0) > 0; }

function GameCard({ g, onNav }) {
  const status = g.status === 'wishlist' ? 'wishlist' : (isPlayed(g) ? 'played' : 'owned');
  const statusLb = { owned: 'owned', wishlist: 'wishlist', played: 'played' }[status];
  return h('div', { className: 'game-card', onClick: () => onNav('/games/' + g.id) },
    h('div', { className: 'gca-cover', style: { background: g.cover } },
      h('span', null, g.coverEmoji),
      h('span', { className: 'gca-status ' + status }, statusLb)),
    h('div', { className: 'gca-body' },
      h('div', { className: 'gca-title' }, g.title),
      h('div', { className: 'gca-pub' }, g.publisher),
      h('div', { className: 'gca-foot' },
        h('span', { className: 'gca-rating' }, isPlayed(g) ? ('★ ' + g.stars + '/5') : ('BGG ' + g.rating)),
        h('span', { className: 'gca-meta' }, isPlayed(g) ? (g.totalPlays + ' partite') : ('aggiunto ' + addedDaysAgo(g.id) + 'g fa'))))
  );
}

function Library({ state, onOpen, onNav }) {
  const [filter, setFilter] = React.useState('all');

  if (state === 'loading') {
    return h('div', null,
      h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Library', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Library · loading', note: 'skeleton non nel mockup (nessun JSX twin)' })),
      h('div', { className: 'lib-grid' }, Array.from({ length: 8 }).map((_, i) => h('div', { key: i }, h(SkeletonCard, { hgt: 210 })))));
  }
  if (state === 'error') {
    return h('div', null,
      h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Library', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Library · error', note: 'stato error non coperto dal mockup' })),
      h(ErrorState, { title: 'Library non disponibile', msg: 'Non riesco a caricare la tua collezione. Riprova tra poco.' }));
  }
  if (state === 'empty') {
    return h('div', null,
      h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Library', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Library · empty', note: 'stato empty non coperto dal mockup' })),
      h(EmptyState, { entity: 'game', icon: '📚', title: 'La tua library è vuota', desc: 'Non hai ancora giochi. Aggiungi il primo o esplora il catalogo.', cta: '+ Aggiungi il tuo primo gioco' }),
      h('div', { style: { textAlign: 'center', marginTop: 'var(--s-4)' } },
        h('button', { className: 'btn ghost', onClick: () => onNav('/discover') }, 'Esplora Discover →')));
  }

  const offline = state === 'offline';
  let games = window.DS.games.slice();
  if (filter === 'owned') games = games.filter(g => g.status === 'owned');
  else if (filter === 'wishlist') games = games.filter(g => g.status === 'wishlist');
  else if (filter === 'played') games = games.filter(isPlayed);
  games.sort(filter === 'played'
    ? (a, b) => window.GN.lastPlayedSort(b.id) - window.GN.lastPlayedSort(a.id)
    : (a, b) => a.title.localeCompare(b.title));
  if (offline) games = games.slice(0, 6);

  return h('div', null,
    offline ? h(OfflineBar, { note: 'collezione dalla cache · ultimo sync 4h fa' }) : null,
    offline ? h(Gap, { cat: 'GAP-STATE', loc: 'Library · offline', note: 'stato offline non coperto dal mockup', block: true }) : null,

    h('div', { className: 'filter-row' },
      LIB_FILTERS.map(f => h('button', { key: f.k, className: 'filter-chip', 'data-active': filter === f.k, onClick: () => setFilter(f.k) },
        f.lb,
        f.k === 'played' ? h(Gap, { cat: 'GAP-ENTITY', mini: true, loc: 'Library · filtro Played', note: 'regola di derivazione "played" TBD (≥1 Session? totalPlays>0?)' }) : null)),
      h('span', { style: { flex: 1 } }),
      h(Gap, { cat: 'GAP-DATA', loc: 'Library · griglia', note: 'dataset fixture limitato a ' + window.DS.games.length + ' giochi' })),

    h('div', { className: 'lib-grid' + (offline ? ' cached' : '') },
      games.map(g => h(GameCard, { key: g.id, g, onNav }))),
    games.length === 0 ? h('div', { className: 'dr-empty-note', style: { marginTop: 'var(--s-4)' } }, 'Nessun gioco in questo filtro.') : null
  );
}

Object.assign(window, { Library });
