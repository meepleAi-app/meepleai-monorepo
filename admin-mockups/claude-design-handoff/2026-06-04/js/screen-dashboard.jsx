/* MeepleAI Nav Prototype — Dashboard (/dashboard), corrected domain model.
   Vertical stack: Prossimi · Recenti · Potresti giocare · Cosa fanno i tuoi.
   GameNight is the top-level entity; no standalone "Sessions live" surface. */

function GnCardUpcoming({ gn, onOpen }) {
  return h('div', { className: 'gn-card up', onClick: () => onOpen('gamenight', gn.id) },
    h('div', { className: 'gc-top' },
      h('h3', { className: 'gc-name' }, gn.name),
      gn.status === 'in-progress'
        ? h('span', { className: 'gc-live' }, h('span', { className: 'live-dot' }), 'IN CORSO')
        : null
    ),
    h('div', { className: 'gc-foot' },
      h('span', { className: 'gc-loc' }, gn.location),
      h('span', { style: { flex: 1 } }),
      h('span', { className: 'gc-rsvp' }, gn.pending ? (gn.confirmed + '/' + (gn.confirmed + gn.pending) + ' pending') : (gn.confirmed + ' ✓'))
    ),
    gn.autoPromoted ? h('div', { className: 'gc-gap' }, h(Gap, { cat: 'GAP-ENTITY', loc: 'Dashboard · "' + gn.name + '"', note: 'auto-promotion planned→in-progress TBD' })) : null,
    gn.taggedPending ? h('div', { className: 'gc-gap' }, h(Gap, { cat: 'GAP-CTA', loc: 'Dashboard · "' + gn.name + '"', note: 'tagging vs RSVP TBD (' + window.GN.pname(gn.taggedPending) + ' taggata, non confermata)' })) : null
  );
}

function GnCardRecent({ gn, onOpen }) {
  const mvp = window.GN.playerById[gn.mvp];
  return h('div', { className: 'gn-card rec', onClick: () => onOpen('gamenight', gn.id) },
    h('div', { className: 'gc-top' },
      h('span', { className: 'gc-date' }, gn.dateLabel),
      h('span', { className: 'gc-count' }, gn.sessions.length + ' partite')
    ),
    h('h3', { className: 'gc-name' }, gn.name),
    mvp ? h('div', { className: 'gc-mvp' },
      h('span', { className: 'avatar sm', style: { background: mvp.cover, color: '#fff' } }, mvp.initials),
      h('span', null, 'MVP ', h('b', null, mvp.title))) : null,
    h('div', { className: 'gc-covers' },
      gn.gameIds.slice(0, 3).map(gid => { const g = window.DS.byId[gid] || {}; return h('span', { key: gid, className: 'mini', style: { background: g.cover }, title: g.title }, g.coverEmoji); }))
  );
}

function SuggCard({ id, onOpen }) {
  const g = window.DS.byId[id] || {};
  return h('div', { className: 'sugg-card', onClick: () => onOpen('entity', id) },
    h('div', { className: 'sugg-cover', style: { background: g.cover } }, h('span', null, g.coverEmoji)),
    h('div', { className: 'sugg-meta' },
      h('div', { className: 'sugg-name' }, g.title),
      h('div', { className: 'sugg-sub' }, g.players + ' · ' + g.duration))
  );
}

function FriendRow({ f, onOpen }) {
  const p = window.GN.playerById[f.playerId] || {};
  const ref = f.kind === 'gn' ? window.GN.nightById[f.refId] : window.DS.byId[f.refId];
  const refName = ref ? (ref.name || ref.title) : f.refId;
  return h('div', { className: 'feed-row' },
    h('span', { className: 'avatar sm', style: { background: p.cover, color: '#fff' } }, p.initials),
    h('div', { className: 'fr-text' },
      h('b', null, p.title), h('span', null, f.action),
      h('button', { className: 'chip-btn ' + (f.kind === 'gn' ? 'e-event' : 'e-game'), onClick: (e) => { e.stopPropagation(); onOpen(f.kind === 'gn' ? 'gamenight' : 'entity', f.refId); } },
        h('span', null, f.kind === 'gn' ? '📅' : '🎲'), h('span', null, refName))),
    h('span', { className: 'fr-time' }, f.at)
  );
}

function Dashboard({ state, onOpen }) {
  const up = window.GN.upcoming, rec = window.GN.recent;

  if (state === 'loading') {
    return h('div', null,
      h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Prossimi', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Dashboard · loading', note: 'skeleton non nel mockup originale (nessun JSX twin)' })),
      h('div', { className: 'gn-grid' }, [0, 1, 2].map(i => h(SkeletonCard, { key: i, hgt: 120 }))),
      h('div', { className: 'section-label' }, 'Recenti', h('span', { className: 'ln' })),
      h('div', { className: 'gn-grid' }, [0, 1, 2].map(i => h(SkeletonCard, { key: i, hgt: 120 })))
    );
  }
  if (state === 'error') {
    return h('div', null,
      h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Dashboard', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Dashboard · error', note: 'stato error non coperto dal mockup' })),
      h(ErrorState, { title: 'Dashboard non disponibile', msg: 'Non riesco a caricare le tue Game Night. Riprova tra poco.' })
    );
  }
  if (state === 'empty') {
    return h('div', null,
      h('div', { className: 'section-label', style: { marginTop: 0 } }, 'Prossimi', h('span', { className: 'ln' }), h(Gap, { cat: 'GAP-STATE', loc: 'Dashboard · empty', note: 'stato empty non coperto dal mockup' })),
      h(EmptyState, { entity: 'event', icon: '📅', title: 'Nessuna Game Night', desc: 'Non hai serate in programma né concluse. Crea la tua prima Game Night e invita i giocatori.', cta: '+ Crea Game Night' })
    );
  }

  const offline = state === 'offline';
  return h('div', null,
    offline ? h(OfflineBar, { note: 'dati dalla cache locale' }) : null,
    offline ? h(Gap, { cat: 'GAP-STATE', loc: 'Dashboard · offline', note: 'stato offline non coperto dal mockup', block: true }) : null,

    h('div', { className: 'section-label', style: { marginTop: offline ? 'var(--s-5)' : 0 } }, 'Prossimi', h('span', { className: 'ln' })),
    h('div', { className: 'gn-grid' + (offline ? ' cached' : '') }, up.map(gn => h('div', { key: gn.id, className: 'gn-wrap' },
      h(GnCardUpcoming, { gn, onOpen }),
      h('span', { className: 'micro-date' }, gn.dateLabel.toLowerCase()),
      offline ? h('span', { className: 'cache-tag' }, 'agg. ' + (gn.cachedAgo || '1h') + ' fa') : null
    ))),

    h('div', { className: 'section-label' }, 'Recenti', h('span', { className: 'ln' })),
    h('div', { className: 'gn-grid' + (offline ? ' cached' : '') }, rec.map(gn => h('div', { key: gn.id, className: 'gn-wrap' },
      h(GnCardRecent, { gn, onOpen }),
      offline ? h('span', { className: 'cache-tag' }, 'agg. ' + (gn.cachedAgo || '1h') + ' fa') : null
    ))),

    h('div', { className: 'section-label' }, 'Potresti giocare', h('span', { className: 'ln' }), h('span', { className: 'pip-more' }, 'dalla tua libreria')),
    h('div', { className: 'sugg-rail' }, window.GN.suggestions.map(id => h(SuggCard, { key: id, id, onOpen }))),

    h('div', { className: 'section-label' }, 'Cosa fanno i tuoi', h('span', { className: 'ln' })),
    h('div', { className: 'feed' }, window.GN.friends.map((f, i) => h(FriendRow, { key: i, f, onOpen })))
  );
}

Object.assign(window, { Dashboard });
