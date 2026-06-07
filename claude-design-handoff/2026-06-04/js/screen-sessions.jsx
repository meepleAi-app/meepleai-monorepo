/* MeepleAI Nav Prototype — /sessions (cross-GameNight session index).
   Every atomic session across all GameNights, grouped by game. Complements
   /game-nights/[id] (single night). Use case: "how many times Wingspan?". */

const SES_CHIPS = [{ k: 'all', lb: 'Tutte' }, { k: 'live', lb: 'Live' }, { k: 'draft', lb: 'Draft' }, { k: 'completed', lb: 'Completate' }];
const SES_TABS = [{ k: 'mine', lb: 'Le mie' }, { k: 'friend', lb: 'Con i friend' }];

function SesStatusChip({ s }) {
  const map = { live: ['LIVE', 'live'], draft: ['Draft', 'draft'], completed: ['Completata', 'completed'] };
  const [lb, cls] = map[s] || ['—', 'completed'];
  return h('span', { className: 'ses-chip s-' + cls }, cls === 'live' ? h('span', { className: 'live-dot' }) : null, lb);
}

function SesFilters({ tab, setTab, chip, setChip, q, setQ }) {
  return h('div', { className: 'ses-filters' },
    h('div', { className: 'subtab-row' }, SES_TABS.map(t => h('button', { key: t.k, className: 'subtab', 'data-active': tab === t.k, onClick: () => setTab(t.k) }, t.lb))),
    h('div', { className: 'filter-row', style: { marginBottom: 0 } }, SES_CHIPS.map(c => h('button', { key: c.k, className: 'filter-chip e-session', 'data-active': chip === c.k, onClick: () => setChip(c.k) }, c.lb))),
    h('div', { className: 'ses-search-row' },
      h('span', { className: 'ses-search-ico' }, '🔍'),
      h('input', { className: 'ses-search', placeholder: 'Cerca per game o player', value: q, onChange: (e) => setQ(e.target.value) }))
  );
}

function SessionsIndex({ state, onOpen, onNav }) {
  const [tab, setTab] = React.useState('mine');
  const [chip, setChip] = React.useState('all');
  const [q, setQ] = React.useState('');

  if (state === 'loading') {
    return h('div', null, h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Sessions', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Sessions · loading', note: 'skeleton non nel mockup' })),
      [0, 1, 2].map(g => h('div', { key: g, style: { marginBottom: 'var(--s-5)' } }, h(SkeletonLine, { w: 30, hgt: 20 }), h(SkeletonCard, { hgt: 56 }), h(SkeletonCard, { hgt: 56 }))));
  }
  if (state === 'error') {
    return h('div', null, h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Sessions', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Sessions · error', note: 'stato error non coperto dal mockup' })),
      h(ErrorState, { title: 'Sessions non disponibili', msg: 'Non riesco a caricare le tue partite. Riprova tra poco.' }));
  }
  if (state === 'empty') {
    return h('div', null, h(SesFilters, { tab, setTab, chip, setChip, q, setQ }),
      h(EmptyState, { entity: 'session', icon: '🎯', title: 'Nessuna session registrata', desc: 'Non hai ancora giocato partite. Avvia una Game Night per iniziare a tracciarle.', cta: '+ Avvia prima Game Night' }),
      h('div', { style: { textAlign: 'center', marginTop: 'var(--s-4)' } }, h('button', { className: 'btn ghost', onClick: () => onNav('/game-nights/new') }, 'Crea Game Night →'), h(Gap, { cat: 'GAP-STATE', loc: 'Sessions · empty', note: 'stato empty non coperto dal mockup' })));
  }

  const offline = state === 'offline';
  let list = window.GN.allSessions().filter(s => tab === 'mine' ? s.mine : s.invited);
  if (chip !== 'all') list = list.filter(s => s.status === chip);
  if (q.trim()) {
    const term = q.toLowerCase();
    list = list.filter(s => { const g = window.DS.byId[s.gameId] || {}; const players = (s.playerIds || []).map(p => window.GN.pname(p)).join(' '); return (g.title || '').toLowerCase().includes(term) || players.toLowerCase().includes(term); });
  }
  const groups = [];
  list.forEach(s => { let gr = groups.find(x => x.gameId === s.gameId); if (!gr) { gr = { gameId: s.gameId, items: [] }; groups.push(gr); } gr.items.push(s); });
  groups.forEach(gr => gr.items.sort((a, b) => b.createdAt.localeCompare(a.createdAt)));

  return h('div', null,
    offline ? h(OfflineBar, { note: 'partite dalla cache · ultimo sync 4h fa' }) : null,
    h(SesFilters, { tab, setTab, chip, setChip, q, setQ }),

    groups.length === 0
      ? h('div', { className: 'dr-empty-note', style: { marginTop: 'var(--s-5)' } }, 'Nessuna session in questo filtro.')
      : groups.map(gr => { const g = window.DS.byId[gr.gameId] || {};
          return h('div', { key: gr.gameId, className: 'ses-group' + (offline ? ' cached' : '') },
            h('button', { className: 'ses-ghead', onClick: () => onNav('/games/' + gr.gameId) },
              h('span', { className: 'sess-cover', style: { background: g.cover } }, g.coverEmoji),
              h('span', { className: 'ses-gtitle' }, g.title || gr.gameId),
              h('span', { className: 'ses-gcount' }, gr.items.length + (gr.items.length === 1 ? ' partita' : ' partite')),
              h('span', { className: 'chev' }, '›')),
            h('div', { className: 'ses-rows' }, gr.items.map(s => {
              const mvp = s.mvp ? window.GN.playerById[s.mvp] : null;
              const dur = (s.startedAt && s.completedAt) ? window.GN.durMin(s.startedAt, s.completedAt) : null;
              return h('div', { className: 'ses-row', key: s.nightId + s.n, onClick: () => onOpen('session', s.nightId + ':' + s.n, { gameNightId: s.nightId }) },
                h(SesStatusChip, { s: s.status }),
                mvp && s.status === 'completed' ? h('span', { className: 'avatar sm', style: { background: mvp.cover, color: '#fff' }, title: mvp.title }, mvp.initials) : null,
                h('div', { className: 'ses-row-main' },
                  h('span', { className: 'ses-date' }, s.nightDate.replace('Oggi · ', '') + (s.startedAt ? ' · ' + s.startedAt : '')),
                  h('span', { className: 'ses-night' }, 'in ', h('button', { className: 'pr-link', onClick: (e) => { e.stopPropagation(); onOpen('gamenight', s.nightId); } }, s.nightName))),
                s.status === 'live'
                  ? h('span', { className: 'ses-live-meta' }, h('span', { className: 'ses-timer' }, '12:40'), h('span', { className: 'ses-live-sub' }, 'in corso · turno 4'))
                  : (s.status === 'completed'
                    ? h('span', { className: 'ses-meta' }, h('span', { className: 'ses-mvp' }, 'MVP ' + (mvp ? mvp.title : '—')), h('span', { className: 'ses-meta-sub' }, (s.score != null ? s.score + ' pt' : '') + (dur ? ' · ' + dur + ' min' : '')))
                    : h('span', { className: 'ses-meta' }, h('span', { className: 'ses-meta-sub' }, 'da compilare'))),
                h('span', { className: 'chev' }, '›'));
            })));
        })
  );
}

Object.assign(window, { SessionsIndex });
