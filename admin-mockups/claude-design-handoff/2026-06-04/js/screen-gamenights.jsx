/* MeepleAI Nav Prototype — /game-nights index (sp4-game-nights-index).
   Month-grouped vertical timeline. Filter chips (status) + sub-tabs (Le mie /
   Invitato). Cards are denser than the dashboard. Click → /game-nights/[id]
   full page (you enter to manage the night, not to peek). */

const GNI_FILTERS = [
  { k: 'all', lb: 'Tutte' },
  { k: 'planned', lb: 'Pianificate' },
  { k: 'in-progress', lb: 'In corso' },
  { k: 'completed', lb: 'Completate' },
];
const GNI_TABS = [{ k: 'mine', lb: 'Le mie' }, { k: 'invited', lb: 'Invitato' }];
const GN_STATUS = { planned: { lb: 'Pianificata', cls: 'planned' }, 'in-progress': { lb: 'IN CORSO', cls: 'in-progress' }, completed: { lb: 'Completata', cls: 'completed' } };

function GniCard({ n, onNav, onOpen }) {
  const st = GN_STATUS[n.status];
  const mvp = n.mvp ? window.GN.playerById[n.mvp] : null;
  return h('div', { className: 'gni-card', onClick: () => onNav('/game-nights/' + n.id) },
    h('div', { className: 'gni-main' },
      h('div', { className: 'gni-top' },
        h('span', { className: 'status-pill ' + st.cls }, n.status === 'in-progress' ? h('span', { className: 'live-dot' }) : null, st.lb),
        h('span', { className: 'gni-date' }, n.dateLabel.replace('Oggi · ', '') + ' · ' + n.location)),
      h('h3', { className: 'gni-name' }, n.name),
      h('div', { className: 'gni-meta' },
        n.status === 'planned'
          ? h('span', null, n.confirmed + ' ✓ · ' + (n.pending || 0) + ' pending')
          : h('span', null, (n.sessions ? n.sessions.length : 0) + ' partite'),
        mvp ? h('span', null, '· MVP ' + mvp.title) : null)),
    h('div', { className: 'gni-players' },
      n.playerIds.slice(0, 4).map(pid => { const p = window.GN.playerById[pid] || {}; return h('button', { key: pid, className: 'avatar sm', style: { background: p.cover, color: '#fff' }, title: p.title, onClick: (e) => { e.stopPropagation(); onOpen('player', pid, { gameNightId: n.id }); } }, p.initials); }),
      n.playerIds.length > 4 ? h('span', { className: 'pip-more' }, '+' + (n.playerIds.length - 4)) : null)
  );
}

function GameNightsIndex({ state, onOpen, onNav }) {
  const [filter, setFilter] = React.useState('all');
  const [tab, setTab] = React.useState('mine');

  if (state === 'loading') {
    return h('div', null,
      h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Giugno 2026', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Game Nights · loading', note: 'skeleton non nel mockup (nessun JSX twin)' })),
      [0, 1, 2].map(i => h('div', { key: i, style: { marginBottom: 'var(--s-3)' } }, h(SkeletonCard, { hgt: 84 }))));
  }
  if (state === 'error') {
    return h('div', null, h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Game Nights', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Game Nights · error', note: 'stato error non coperto dal mockup' })),
      h(ErrorState, { title: 'Game Nights non disponibili', msg: 'Non riesco a caricare le serate. Riprova tra poco.' }));
  }
  if (state === 'empty') {
    return h('div', null, h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Game Nights', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Game Nights · empty', note: 'stato empty non coperto dal mockup' })),
      h(EmptyState, { entity: 'event', icon: '📅', title: 'Nessuna Game Night', desc: 'Non hai ancora organizzato né ricevuto inviti a serate. Creane una e invita i giocatori.', cta: '+ Crea la tua prima Game Night' }));
  }

  const offline = state === 'offline';
  let list = window.GN.nights.filter(n => tab === 'mine' ? window.GN.isMine(n) : window.GN.amInvited(n));
  if (filter !== 'all') list = list.filter(n => n.status === filter);
  const asc = filter === 'planned';
  list = list.slice().sort((a, b) => asc ? a.dateSort - b.dateSort : b.dateSort - a.dateSort);

  // group by month preserving sorted order
  const months = [];
  list.forEach(n => { let m = months.find(x => x.month === n.month); if (!m) { m = { month: n.month, items: [] }; months.push(m); } m.items.push(n); });

  return h('div', null,
    offline ? h(OfflineBar, { note: 'serate dalla cache · ultimo sync 4h fa' }) : null,
    offline ? h(Gap, { cat: 'GAP-STATE', loc: 'Game Nights · offline', note: 'stato offline non coperto dal mockup', block: true }) : null,

    h('div', { className: 'filter-row' },
      GNI_FILTERS.map(f => h('button', { key: f.k, className: 'filter-chip e-event', 'data-active': filter === f.k, onClick: () => setFilter(f.k) },
        f.lb, f.k === 'in-progress' ? h(Gap, { cat: 'GAP-ENTITY', mini: true, loc: 'Game Nights · filtro In corso', note: 'auto-promotion planned→in-progress rule TBD' }) : null))),

    h('div', { className: 'subtab-row' },
      GNI_TABS.map(t => h('button', { key: t.k, className: 'subtab', 'data-active': tab === t.k, onClick: () => setTab(t.k) }, t.lb))),

    months.length === 0
      ? h('div', { className: 'dr-empty-note', style: { marginTop: 'var(--s-5)' } }, 'Nessuna serata in questo filtro.')
      : months.map((m, mi) => h('div', { key: m.month },
          h('div', { className: 'section-label', style: { marginTop: mi === 0 ? 0 : 'var(--s-7)' } }, m.month, h('span', { className: 'ln' })),
          h('div', { className: 'gni-list' + (offline ? ' cached' : '') }, m.items.map(n => h(GniCard, { key: n.id, n, onNav, onOpen })))))
  );
}

Object.assign(window, { GameNightsIndex });
