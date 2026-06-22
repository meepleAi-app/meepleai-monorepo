/* MeepleAI Nav Prototype — Drawer STACK (slide-over, 250ms).
   Holds a navigation stack: opening a GameNight pushes its drawer; tapping a
   player pushes the Player drawer on top. ESC / back-arrow pop one level
   (return to the previous drawer); the backdrop or the explicit ✕ close the
   whole stack. Kinds: gamenight · player · session · entity. */
const { useEffect: useEffectD } = React;

/* ── helpers ── */
function playerBadge(p) {
  return p.linked
    ? h('span', { className: 'pbadge linked' }, '✓ User')
    : h('span', { className: 'pbadge guest' }, 'Guest');
}
function avatarChip(p, size) {
  return h('span', { className: 'avatar ' + (size || ''), style: { background: p.cover, color: '#fff' } }, p.initials || (p.title || '?')[0]);
}
function drSectionLabel(txt, extra) {
  return h('div', { className: 'dr-sec-label' }, txt, h('span', { className: 'ln' }), extra || null);
}

/* ── GameNight ── */
function renderGameNight(gn, api) {
  const statusMap = { planned: 'Pianificata', 'in-progress': 'In corso', completed: 'Conclusa' };
  const ctas = {
    planned: ['Modifica RSVP', 'Cancel'],
    'in-progress': ['Aggiungi session', 'Termina serata'],
    completed: ['Aggiungi note', 'Aggiungi foto'],
  }[gn.status] || [];

  return {
    accent: 'e-event', cover: window.DS.color('event'), emoji: '📅',
    title: gn.name, subtitle: gn.dateLabel + ' · ' + gn.location,
    body: h('div', null,
      // status row
      h('div', { className: 'dr-status-row' },
        h('span', { className: 'status-pill ' + gn.status },
          gn.status === 'in-progress' ? h('span', { className: 'live-dot' }) : null,
          gn.status === 'in-progress' ? 'IN CORSO' : statusMap[gn.status]),
        gn.status !== 'completed'
          ? h('span', { className: 'rsvp' }, gn.pending ? (gn.confirmed + '/' + (gn.confirmed + gn.pending) + ' RSVP') : (gn.confirmed + ' ✓'))
          : h('span', { className: 'rsvp' }, (gn.sessions.length) + ' partite'),
      ),
      gn.autoPromoted ? h('div', { style: { marginBottom: 'var(--s-3)' } },
        h(Gap, { cat: 'GAP-ENTITY', loc: 'GameNight "' + gn.name + '" · auto-promotion', note: 'planned → in-progress: logica di promozione automatica TBD', block: true })) : null,

      // CTAs
      h('div', { className: 'dr-cta-row' },
        ctas.map((c, i) => h('button', { key: i, className: i === 0 ? 'btn primary e-event' : 'btn ghost' }, c))),

      // Players
      drSectionLabel('Giocatori · ' + gn.playerIds.length),
      h('div', { className: 'dr-list' },
        gn.playerIds.map(pid => {
          const p = window.GN.playerById[pid];
          if (!p) return null;
          const tagged = gn.taggedPending === pid;
          return h('button', { key: pid, className: 'dr-list-row', onClick: () => api.onPush('player', pid, { gameNightId: gn.id }) },
            avatarChip(p, 'sm'),
            h('span', { className: 'dlr-name' }, p.title),
            tagged ? h(Gap, { cat: 'GAP-CTA', loc: 'GameNight "' + gn.name + '" · ' + p.title, note: 'taggata ma RSVP non confermato — side-effect tagging vs RSVP TBD' }) : null,
            h('span', { style: { flex: 1 } }),
            playerBadge(p),
            h('span', { className: 'chev' }, '›'),
          );
        })
      ),

      // Sessions
      drSectionLabel('Sessions · ' + gn.sessions.length),
      gn.sessions.length
        ? h('div', { className: 'dr-list' },
            gn.sessions.map(s => {
              const g = window.DS.byId[s.gameId] || {};
              return h('div', { key: s.n, className: 'dr-list-row', onClick: () => api.onPush('session', gn.id + ':' + s.n, { gameNightId: gn.id }) },
                h('span', { className: 'sess-cover', style: { background: g.cover } }, g.coverEmoji),
                h('span', { className: 'dlr-stack' },
                  h('span', { className: 'dlr-name' }, 'Session ' + s.n + ': ',
                    h('button', { className: 'game-link', title: 'Apri pagina gioco',
                      onClick: (ev) => { ev.stopPropagation(); api.onNavigate && api.onNavigate('/games/' + s.gameId); } }, g.title || s.gameId)),
                  h('span', { className: 'dlr-sub' }, s.live ? h('span', { className: 'inline-live' }, h('span', { className: 'live-dot' }), s.result) : s.result)),
                h('span', { className: 'chev' }, '›'),
              );
            }))
        : h('div', { className: 'dr-empty-note' }, gn.status === 'planned' ? 'Nessuna partita ancora — si gioca la sera dell\u2019evento.' : 'Nessuna session registrata.'),
    ),
  };
}

/* ── Player ── */
function renderPlayer(p, ctx, api) {
  const gn = ctx && ctx.gameNightId ? window.GN.nightById[ctx.gameNightId] : null;
  const rel = window.GN.relational(p);
  return {
    accent: 'e-player', cover: p.cover, emoji: '👤',
    title: p.title, subtitle: p.linked ? 'User · ' + (p.subtitle || '') : 'Guest player',
    body: h('div', null,
      // RELATIONAL
      drSectionLabel('Relazione con te'),
      rel
        ? (rel.self
          ? h('div', { className: 'dr-empty-note' }, 'Sei tu — questo è il tuo profilo.')
          : h('div', { className: 'rel-grid' },
            h('div', { className: 'rel-cell' }, h('span', { className: 'rv' }, rel.together), h('span', { className: 'rl' }, 'partite insieme')),
            h('div', { className: 'rel-cell' }, h('span', { className: 'rv' }, rel.winRateVsMe + '%'), h('span', { className: 'rl' }, 'win rate vs te')),
            h('div', { className: 'rel-cell wide' }, h('span', { className: 'rv sm' }, rel.common.join(' · ')), h('span', { className: 'rl' }, 'giochi in comune'))))
        : h('div', { className: 'dr-empty-note' }, 'Guest player — nessun account associato. Nessuna statistica relazionale.'),

      // PROFILE
      drSectionLabel('Profilo'),
      h('div', { className: 'profile-block' },
        h('span', { className: 'avatar lg', style: { background: p.cover, color: '#fff' } }, p.initials),
        h('div', null,
          h('div', { className: 'pf-name' }, p.title),
          h('div', { className: 'pf-bio' }, p.linked ? (p.bio || '—') : 'Aggiunta come ospite alla serata.'))
      ),
      p.linked && p.library ? h('div', { className: 'pf-lib' },
        p.library.map(gidv => { const g = window.DS.byId[gidv] || {}; return h('span', { key: gidv, className: 'pf-cover', style: { background: g.cover }, title: g.title }, g.coverEmoji); })) : null,

      // ACTIONS (contextual to the GameNight we came from)
      drSectionLabel(gn ? 'Azioni · in "' + gn.name + '"' : 'Azioni'),
      h('div', { className: 'dr-action-list' },
        ['Modifica score in questa Session', 'Rimuovi da questa GameNight', 'Aggiungi nota'].map((a, i) =>
          h('button', { key: i, className: 'dr-action' }, a))),
      gn ? h('div', { className: 'dr-back-hint' }, 'ESC o ‹ per tornare a ' + gn.name) : null,
    ),
  };
}

/* ── Session (placeholder) ── */
function renderSession(token, ctx) {
  const [gnId, n] = token.split(':');
  const gn = window.GN.nightById[gnId];
  const s = gn && gn.sessions.find(x => String(x.n) === n);
  const g = s ? (window.DS.byId[s.gameId] || {}) : {};
  return {
    accent: 'e-session', cover: g.cover || window.DS.color('session'), emoji: g.coverEmoji || '🎯',
    title: 'Session ' + (s ? s.n : '?') + ': ' + (g.title || ''),
    subtitle: gn ? gn.name + ' · ' + gn.dateLabel : '',
    body: h('div', null,
      drSectionLabel('Esito'),
      h('div', { className: 'kv' }, h('span', { className: 'k' }, 'Risultato'), h('span', { className: 'v' }, s ? (s.result || '—') : '—')),
      h('div', { className: 'kv' }, h('span', { className: 'k' }, 'Giocatori'), h('span', { className: 'v' }, gn ? gn.playerIds.length : '—')),
      h('div', { className: 'dr-empty-note', style: { marginTop: 'var(--s-4)' } }, 'Dettaglio session ridotto — la live/summary completa è uno schermo a parte.'),
      h('div', { style: { marginTop: 'var(--s-3)' } },
        h(Gap, { cat: 'GAP-FEATURE', loc: 'Session ' + (s ? s.n : '') + ' · ' + (gn ? gn.name : ''), note: 'session detail completo TBD (live/summary, turno 4)', block: true })),
    ),
  };
}

/* ── Entity (generic, e.g. games) ── */
function renderEntity(id, api) {
  const e = window.DS.byId[id];
  if (!e) return { accent: 'e-game', emoji: '•', title: '—', subtitle: '', body: null };
  const rows = detailRows(e);
  const chatMsgs = e.type === 'chat' ? [
    { who: 'me', t: e.title },
    { who: 'agent', t: 'Secondo il manuale, ' + (e.title || '').toLowerCase().replace('?', '') + ' si risolve così…' },
    { who: 'me', t: 'E in caso di pareggio?' },
    { who: 'agent', t: 'Vince chi ha completato più obiettivi. Vedi §7 del regolamento.' },
  ] : null;
  return {
    accent: ENT_CLASS[e.type] || 'e-game', cover: e.cover || window.DS.color(e.type), emoji: e.coverEmoji || entEmoji(e.type),
    title: e.title, subtitle: entLabel(e.type).toUpperCase() + (e.subtitle ? ' · ' + e.subtitle : ''),
    connBar: e,
    body: h('div', null,
      drSectionLabel('Info'),
      rows.map(([k, v], i) => h('div', { className: 'kv', key: i }, h('span', { className: 'k' }, k), h('span', { className: 'v' }, String(v)))),
      chatMsgs ? h('div', null, drSectionLabel('Ultimi messaggi'),
        h('div', { className: 'chat-preview' }, chatMsgs.map((m, i) => h('div', { key: i, className: 'chat-msg ' + m.who }, m.t)))) : null
    ),
  };
}

function detailRows(e) {
  const G = window.DS;
  const game = e.gameId ? G.byId[e.gameId] : null;
  const r = []; const push = (k, v) => { if (v !== undefined && v !== null && v !== '') r.push([k, v]); };
  if (e.type === 'game') {
    push('Autore', e.author); push('Editore', e.publisher); push('Anno', e.year);
    push('Giocatori', e.players); push('Durata', e.duration); push('Peso', e.weight + ' / 5');
    push('Valutazione', e.rating + ' ★'); push('Partite giocate', e.totalPlays);
    push('Win rate', Math.round((e.winRate || 0) * 100) + '%');
  } else if (e.type === 'player') {
    push('Partite', e.totalSessions); push('Vittorie', e.totalWins); push('Gioco preferito', e.fav);
  }
  return r;
}

function ConnectionBar({ entity, onOpen }) {
  const conns = buildConnections(entity);
  if (!conns.length) return null;
  return h('div', { className: 'conn-bar' },
    conns.map((c, i) => h('button', {
      key: i, className: 'conn-pip ' + (ENT_CLASS[c.type] || ''), 'data-empty': c.isEmpty,
      onClick: () => { if (!c.isEmpty) onOpen('entity', c.ids[0]); },
      title: c.isEmpty ? 'Nessun ' + c.label.toLowerCase() : c.label,
    }, h('span', null, entEmoji(c.type)), h('span', null, c.label), h('span', { className: 'cnt' }, c.isEmpty ? '+' : c.count)))
  );
}

/* ── Stack host ── */
function DrawerStack({ stack, onPush, onPop, onClose, onNavigate }) {
  const active = stack.length > 0;
  const { mounted, panelRef, scrimRef } = useSlideAnim(active, 250);
  const depth = stack.length;
  const top = depth ? stack[depth - 1] : null;

  useEffectD(() => {
    const onKey = (ev) => { if (ev.key === 'Escape') { depth > 1 ? onPop() : onClose(); } };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [depth, onPop, onClose]);

  let view = null;
  if (mounted && top) {
    const api = { onPush, onNavigate };
    if (top.kind === 'gamenight') view = renderGameNight(window.GN.nightById[top.id], api);
    else if (top.kind === 'player') view = renderPlayer(window.GN.playerById[top.id], top.ctx, api);
    else if (top.kind === 'session') view = renderSession(top.id, top.ctx);
    else view = renderEntity(top.id, api);
  }

  return h(React.Fragment, null,
    h('div', { ref: scrimRef, className: 'drawer-scrim' + (active ? ' open' : ''), onClick: onClose }),
    h('aside', { ref: panelRef, className: 'drawer ' + (view ? view.accent : '') + (active ? ' open' : ''), 'aria-hidden': !active },
      view ? h(React.Fragment, null,
        h('div', { className: 'dr-head' },
          h('div', { className: 'dr-top' },
            depth > 1 ? h('button', { className: 'dr-back', onClick: onPop, title: 'Indietro (Esc)' }, '‹') : null,
            h('div', { className: 'dr-cover', style: { background: view.cover } }, view.emoji),
            h('div', { className: 'dr-titles' },
              h('h2', null, view.title),
              h('div', { className: 'dr-sub' }, view.subtitle)),
            h('button', { className: 'dr-close', onClick: onClose, title: 'Chiudi stack' }, '✕'))
        ),
        view.connBar ? h(ConnectionBar, { entity: view.connBar, onOpen: onPush }) : null,
        h('div', { className: 'dr-content' }, view.body)
      ) : null)
  );
}

Object.assign(window, { DrawerStack });
