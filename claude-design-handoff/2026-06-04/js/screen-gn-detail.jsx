/* MeepleAI Nav Prototype — /game-nights/[id] detail (sp7-game-night-detail-rsvp).
   Full page. Hero + status-aware sections: Player&RSVP (always) · Games (always) ·
   Sessions (in-progress/completed) · Notes/foto/summary (completed). */
const { useState: useStateD2, useEffect: useEffectD2 } = React;

function RsvpIcon({ s }) {
  const map = { confirmed: ['✓', 'ok'], declined: ['✗', 'no'], pending: ['⏳', 'pend'] };
  const [ic, cls] = map[s] || ['⏳', 'pend'];
  return h('span', { className: 'rsvp-ic ' + cls, title: s }, ic);
}

function GameNightDetail({ id, state, onOpen, onNav }) {
  const n = window.GN.nightById[id];
  const [myRsvp, setMyRsvp] = useStateD2(n ? window.GN.perPlayerRsvp(n, window.GN.meId) : 'pending');
  const [order, setOrder] = useStateD2(n ? n.gameIds.slice() : []);
  const [notes, setNotes] = useStateD2('');
  const [showNewSess, setShowNewSess] = useStateD2(false);
  const [, forceTick] = useStateD2(0);
  const force = () => forceTick(t => t + 1);
  const [editDraft, setEditDraft] = useStateD2(null);
  const [toast, setToast] = useStateD2(null);
  useEffectD2(() => { if (!toast) return; const t = setTimeout(() => setToast(null), 6000); return () => clearTimeout(t); }, [toast]);

  if (state === 'loading') {
    return h('div', null, h('div', { className: 'gd-hero' }, h(SkeletonCard, { hgt: 200 }), h('div', null, h(SkeletonLine, { w: 50, hgt: 28 }), h(SkeletonLine, { w: 70 }), h(SkeletonLine, { w: 40 }))),
      h(SkeletonLine, { w: 90 }), h(SkeletonLine, { w: 80 }),
      h('div', { style: { marginTop: 'var(--s-3)' } }, h(Gap, { cat: 'GAP-STATE', loc: 'GameNight detail · loading', note: 'skeleton non nel mockup' })));
  }
  if (!n || state === 'error') {
    return h('div', { className: 'state-block e-event' }, h('div', { className: 'sb-icon' }, '🔍'),
      h('h2', null, n ? 'Errore di caricamento' : 'Game Night non trovata'),
      h('p', null, n ? 'Riprova tra poco.' : 'L\u2019id "' + id + '" non corrisponde a nessuna serata.'),
      h('button', { className: 'sb-cta', onClick: () => onNav('/game-nights') }, '← Torna a Game Nights'));
  }

  const offline = state === 'offline';
  const st = GN_STATUS[n.status];
  const mine = window.GN.isMine(n);
  const drafts = n.drafts || [];
  const liveSess = window.GN.liveSession(n);
  const cta = n.status === 'planned' ? { lb: mine ? 'Modifica serata' : 'Modifica RSVP', act: null }
    : n.status === 'in-progress' ? { lb: 'Apri Live mode', act: () => onNav('/game-nights/' + n.id + '/live'), secondary: 'Termina serata' }
    : { lb: 'Vai al riepilogo', act: () => onNav('/game-nights/' + n.id + '/summary'), gap: true };

  const moveGame = (i, d) => setOrder(o => { const a = o.slice(); const j = i + d; if (j < 0 || j >= a.length) return o; [a[i], a[j]] = [a[j], a[i]]; return a; });
  const playedGames = new Set((n.sessions || []).filter(s => !s.live).map(s => s.gameId));
  const rsvpOf = (pid) => pid === window.GN.meId ? myRsvp : window.GN.perPlayerRsvp(n, pid);
  const confirmedCount = n.playerIds.filter(pid => rsvpOf(pid) === 'confirmed').length;
  const liveScore = (pid, i) => 12 + i * 9 + (pid.length % 7); // synth mid-game score

  return h('div', null,
    offline ? h(OfflineBar, { note: 'serata dalla cache · sync in attesa' }) : null,

    // HERO
    h('div', { className: 'gd-hero' },
      h('div', { className: 'gd-cover', style: { background: window.DS.grad(340, 50) } }, '📍'),
      h('div', { className: 'gd-info' },
        h('div', { className: 'gd-badges' }, h('span', { className: 'status-pill ' + st.cls }, n.status === 'in-progress' ? h('span', { className: 'live-dot' }) : null, st.lb)),
        h('h1', { className: 'gd-title' }, n.name),
        h('div', { className: 'gd-pub' }, n.dateLabel.replace('Oggi · ', '') + ' · ' + (n.time || '') + ' · ' + n.location),
        h('div', { className: 'gd-tags' }, h('span', { className: 'gd-tag' }, '👥 ' + n.playerIds.length + ' player'), h('span', { className: 'gd-tag' }, '🎲 ' + n.gameIds.length + ' giochi'),
          mine ? h('span', { className: 'gd-tag' }, '👑 sei host') : h('span', { className: 'gd-tag' }, 'invitato')),
        h('div', { className: 'gd-cta-stack' },
          h('button', { className: 'gd-cta gn', onClick: cta.act || undefined }, cta.lb),
          cta.secondary ? h('button', { className: 'gd-cta outline' }, cta.secondary) : null,
          cta.gap ? h(Gap, { cat: 'GAP-CTA', loc: 'GameNight · ' + cta.lb, note: '/game-nights/[id]/summary non costruita in questo turno' }) : null))),

    // SECTION 1 — Players & RSVP
    h('div', { className: 'section-label', style: { marginTop: 'var(--s-7)' } }, 'Player',
      h('span', { className: 'count-pill' }, '(' + confirmedCount + ' confermati / ' + n.playerIds.length + ' invitati)'),
      h('span', { className: 'ln' }),
      h(Gap, { cat: 'GAP-DATA', mini: true, loc: 'GameNight · RSVP', note: 'RSVP per-player sintetizzato (data.js ha solo aggregati)' })),
    h('div', { className: 'dr-list' },
      n.playerIds.map(pid => {
        const p = window.GN.playerById[pid] || {};
        const isMe = pid === window.GN.meId;
        const rsvp = rsvpOf(pid);
        return h('div', { className: 'dr-list-row', key: pid },
          h('button', { className: 'avatar sm', style: { background: p.cover, color: '#fff', border: 'none' }, onClick: () => onOpen('player', pid, { gameNightId: n.id }) }, p.initials),
          h('span', { className: 'dlr-name' }, p.title, pid === n.host ? h('span', { title: 'Host', style: { marginLeft: 'var(--s-1)' } }, '👑') : null,
            !p.linked ? h('span', { className: 'pbadge guest', style: { marginLeft: 'var(--s-2)' } }, 'Guest') : null,
            isMe ? h('span', { className: 'pbadge linked', style: { marginLeft: 'var(--s-2)' } }, 'tu') : null),
          h('span', { style: { flex: 1 } }),
          (isMe && window.GN.amInvited(n) && n.status === 'planned')
            ? h('span', { className: 'rsvp-toggle' },
                h('button', { className: 'rsvp-btn' + (myRsvp === 'confirmed' ? ' on' : ''), onClick: () => setMyRsvp('confirmed') }, 'Conferma'),
                h('button', { className: 'rsvp-btn no' + (myRsvp === 'declined' ? ' on' : ''), onClick: () => setMyRsvp('declined') }, 'Decline'))
            : h(RsvpIcon, { s: rsvp }));
      }).concat(mine ? [h('button', { className: 'dr-list-row add-row', key: '__add' }, h('span', { className: 'add-ic' }, '+'), h('span', { className: 'dlr-name' }, 'Aggiungi player'))] : [])),

    // SECTION 2 — Games
    h('div', { className: 'section-label' }, 'Games della serata', h('span', { className: 'ln' }),
      n.status === 'planned' ? h(Gap, { cat: 'GAP-FEATURE', mini: true, loc: 'GameNight · games', note: 'drag-to-reorder gesto TBD (qui via ↑↓)' }) : null),
    h('div', { className: 'dr-list' },
      order.map((gidv, i) => {
        const g = window.DS.byId[gidv] || {};
        return h('div', { className: 'dr-list-row', key: gidv },
          h('span', { className: 'sess-cover', style: { background: g.cover } }, g.coverEmoji),
          h('span', { className: 'dlr-stack' },
            h('span', { className: 'dlr-name' }, h('button', { className: 'game-link', onClick: () => onNav('/games/' + gidv) }, g.title)),
            h('span', { className: 'dlr-sub' }, g.players + ' giocatori')),
          playedGames.has(gidv) && n.status !== 'planned' ? h('span', { className: 'pr-status completed', style: { marginRight: 'var(--s-2)' } }, 'giocato') : null,
          n.status === 'planned'
            ? h('span', { className: 'reorder' },
                h('button', { className: 'reorder-btn', onClick: () => moveGame(i, -1), disabled: i === 0, title: 'Su' }, '↑'),
                h('button', { className: 'reorder-btn', onClick: () => moveGame(i, 1), disabled: i === order.length - 1, title: 'Giù' }, '↓'),
                h('button', { className: 'reorder-btn', onClick: () => setOrder(o => o.filter(x => x !== gidv)), title: 'Rimuovi' }, '✕'))
            : null);
      })),
    n.status === 'planned' ? h('button', { className: 'btn ghost', style: { marginTop: 'var(--s-3)' } }, '+ Aggiungi game') : null,

    // SECTION 3 — Sessions
    (n.status === 'in-progress' || n.status === 'completed')
      ? h('div', null,
          h('div', { className: 'section-label', style: { marginTop: 'var(--s-7)' } }, 'Sessions', h('span', { className: 'ln' }),
            h('span', { className: 'sort-note' }, 'ordine di registrazione'),
            n.status === 'in-progress' ? h('button', { className: 'btn primary e-session', onClick: () => setShowNewSess(true) }, '+ Nuova session') : null),
          (n.sessions && n.sessions.length) || drafts.length
            ? h('div', { className: 'dr-list' },
                (n.sessions || []).map((s, i) => {
                const g = window.DS.byId[s.gameId] || {};
                if (s.live) {
                  return h('div', { className: 'dr-list-row live-sess', key: s.n, onClick: () => onNav('/game-nights/' + n.id + '/live') },
                    h('span', { className: 'sess-cover', style: { background: g.cover } }, g.coverEmoji),
                    h('span', { className: 'dlr-stack' },
                      h('span', { className: 'dlr-name' }, 'Session ' + s.n + ': ' + (g.title || s.gameId),
                        h('span', { className: 'live-badge', style: { marginLeft: 'var(--s-2)' } }, h('span', { className: 'live-dot' }), 'LIVE 12:40')),
                      h('span', { className: 'dlr-sub' }, 'ore ' + (s.createdAt || window.GN.sessionTime(i)) + ' · IN CORSO · turno 4'),
                      h('span', { className: 'live-mini-scores' }, n.playerIds.map((pid, j) => { const pp = window.GN.playerById[pid] || {}; return h('span', { key: pid, className: 'lms', title: pp.title }, h('span', { className: 'avatar sm', style: { background: pp.cover, color: '#fff' } }, pp.initials), liveScore(pid, j)); }))),
                    h('span', { className: 'chev' }, '›'));
                }
                return h('div', { className: 'dr-list-row', key: s.n, onClick: () => onOpen('session', n.id + ':' + s.n, { gameNightId: n.id }) },
                  h('span', { className: 'sess-cover', style: { background: g.cover } }, g.coverEmoji),
                  h('span', { className: 'dlr-stack' },
                    h('span', { className: 'dlr-name' }, 'Session ' + s.n + ': ' + (g.title || s.gameId)),
                    h('span', { className: 'dlr-sub' }, 'ore ' + (s.createdAt || window.GN.sessionTime(i)) + ' · ' + (s.result || '') + (s.startedAt && s.completedAt ? ' · Iniziata ' + s.startedAt + ' · Durata ' + window.GN.durMin(s.startedAt, s.completedAt) + ' min' : ''))),
                  h('span', { className: 'chev' }, '›'));
                })
                .concat(drafts.map(d => { const g = window.DS.byId[d.gameId] || {}; const done = d.status === 'completed'; return h('div', { className: 'dr-list-row', key: 'd' + d.n },
                  h('span', { className: 'sess-cover', style: { background: g.cover } }, g.coverEmoji),
                  h('span', { className: 'dlr-stack' },
                    h('span', { className: 'dlr-name' }, 'Session ' + d.n + ': ' + (g.title || d.gameId),
                      done ? h('span', { className: 'done-chip' }, 'completata') : h('span', { className: 'draft-chip' }, 'draft')),
                    h('span', { className: 'dlr-sub' }, done ? ('MVP: ' + window.GN.pname(d.mvp) + ' · ' + d.score + ' pt') : ('Da compilare · creata ' + (d.createdAt || '—')))),
                  done ? null : h('button', { className: 'reorder-btn', title: 'Modifica', style: { marginRight: 'var(--s-2)' }, onClick: () => setEditDraft(d) }, '✎'),
                  done ? null : h(Gap, { cat: 'GAP-FEATURE', mini: true, loc: 'GameNight · session draft', note: 'editing inline session (drawer)' })); })))
            : h('div', { className: 'dr-empty-note' }, 'Nessuna session ancora giocata.'))
      : null,

    // SECTION 4 — Notes / photos / summary
    n.status === 'completed'
      ? h('div', null,
          h('div', { className: 'section-label', style: { marginTop: 'var(--s-7)' } }, 'Note, foto, riepilogo', h('span', { className: 'ln' })),
          h('textarea', { className: 'wz-input', rows: 2, placeholder: mine ? 'Aggiungi una nota sulla serata…' : 'Nessuna nota.', value: notes, onChange: (e) => setNotes(e.target.value), readOnly: !mine }),
          h('div', { className: 'photo-grid' }, [0, 1, 2].map(i => h('div', { key: i, className: 'photo-ph' }, '📷'))),
          h('div', { style: { marginTop: 'var(--s-2)' } }, h(Gap, { cat: 'GAP-FEATURE', loc: 'GameNight · foto', note: 'upload galleria TBD' })),
          h('div', { className: 'summary-line' }, '🏆 Vincitore serata: ' + (n.mvp ? window.GN.pname(n.mvp) : '—') + ' · ' + n.sessions.length + ' partite giocate'))
      : null,

    showNewSess ? h(NewSessionModal, { night: n, liveSession: liveSess,
      onClose: () => setShowNewSess(false),
      onGoLive: () => { setShowNewSess(false); onNav('/game-nights/' + n.id + '/live'); },
      onCreate: (gameId, live) => {
        setShowNewSess(false);
        if (live && !liveSess) { onNav('/game-nights/' + n.id + '/live'); }
        else { window.GN.addDraftSession(n.id, gameId); force(); }
      } }) : null,

    h(DraftEditDrawer, { draft: editDraft, night: n,
      onClose: () => setEditDraft(null),
      onLive: () => { setEditDraft(null); onNav('/game-nights/' + n.id + '/live'); },
      onSave: (res) => {
        window.GN.completeDraft(n.id, editDraft.n, res); setEditDraft(null); force();
        const ls = window.GN.liveSession(n);
        if (ls) { const lg = window.DS.byId[ls.gameId] || {}; setToast('Session salvata mentre una live è in corso (Session ' + ls.n + ': ' + (lg.title || '') + '). Verifica che le partite siano nell’ordine corretto.'); }
      } }),

    toast ? h('div', { className: 'toast warn' },
      h('span', null, toast),
      h('button', { className: 'toast-go', onClick: () => { setToast(null); onNav('/game-nights/' + n.id + '/live'); } }, 'Vai alla live'),
      h('button', { className: 'toast-x', onClick: () => setToast(null) }, '✕')) : null
  );
}

function Toggle({ on, onChange, disabled }) {
  return h('button', { className: 'toggle' + (on ? ' on' : '') + (disabled ? ' disabled' : ''), role: 'switch', 'aria-checked': on, disabled: !!disabled, onClick: () => { if (!disabled) onChange(!on); } }, h('span', { className: 'knob' }));
}

function SearchLibSubModal({ onClose }) {
  return h('div', { className: 'modal-scrim open', style: { zIndex: 'var(--z-toast)' }, onClick: onClose },
    h('div', { className: 'modal', style: { width: '380px' }, onClick: (e) => e.stopPropagation() },
      h('div', { className: 'modal-head' }, h('h2', null, 'Aggiungi game alla serata'), h('button', { className: 'dr-close', onClick: onClose }, '✕')),
      h('div', { className: 'modal-body' },
        h('input', { className: 'wz-input', placeholder: 'Cerca nella library…' }),
        h('div', { style: { marginTop: 'var(--s-3)' } }, h(Gap, { cat: 'GAP-CTA', loc: 'Nuova session · aggiungi game', note: 'search library TBD', block: true })),
        h('div', { className: 'dr-cta-row' }, h('button', { className: 'btn ghost', onClick: onClose }, 'Chiudi')))));
}

function NewSessionModal({ night, onClose, onCreate, liveSession, onGoLive }) {
  const games = night.gameIds;
  const [sel, setSel] = useStateD2(games[0] || '');
  const [live, setLive] = useStateD2(false);
  const [sub, setSub] = useStateD2(false);
  const empty = games.length === 0;
  const liveGame = liveSession ? (window.DS.byId[liveSession.gameId] || {}) : null;
  const onSelChange = (e) => { if (e.target.value === '__add') { setSub(true); } else { setSel(e.target.value); } };
  return h('div', { className: 'modal-scrim open', onClick: onClose },
    h('div', { className: 'modal', onClick: (e) => e.stopPropagation() },
      h('div', { className: 'modal-head' }, h('h2', null, 'Nuova session'), h('button', { className: 'dr-close', onClick: onClose }, '✕')),
      h('div', { className: 'modal-body' },
        empty
          ? h('div', null,
              h('div', { className: 'ns-warning' }, '⚠ Aggiungi prima un game alla serata.'),
              h('button', { className: 'btn ghost', onClick: onClose }, '↑ Vai a “Games della serata”'))
          : h('div', null,
              h('div', { className: 'wz-field' }, h('label', null, 'Game'),
                h('select', { className: 'wz-input', value: sel, onChange: onSelChange },
                  games.map((gidv, i) => { const g = window.DS.byId[gidv] || {}; return h('option', { key: gidv + i, value: gidv }, g.coverEmoji + ' ' + g.title); }),
                  h('option', { disabled: true }, '──────────'),
                  h('option', { value: '__add' }, '+ Aggiungi un altro game'))),
              h('div', { className: 'wz-field' },
                h('div', { className: 'ns-toggle-row' }, h('label', { style: { margin: 0 } }, 'Avvia in Live mode'), h(Toggle, { on: liveSession ? false : live, onChange: liveSession ? (() => {}) : setLive, disabled: !!liveSession })),
                liveSession
                  ? h('span', { className: 'ns-sub' }, 'Una session live è già attiva (Session ' + liveSession.n + ': ' + (liveGame.title || '') + '). Termina la live corrente per avviare una nuova sessione live. ',
                      h('button', { className: 'game-link', onClick: onGoLive }, 'Vai alla session live'))
                  : h('span', { className: 'ns-sub' }, live
                    ? 'Entrerai in tracking real-time con timer e scoring incrementale.'
                    : 'La session sarà aggiunta come bozza. Compilerai i risultati a fine partita.'))),
        h('div', { className: 'dr-cta-row' },
          h('button', { className: 'btn primary e-event', disabled: empty || !sel, onClick: () => onCreate(sel, liveSession ? false : live) }, 'Crea session'),
          h('button', { className: 'btn ghost', onClick: onClose }, 'Annulla'))),
      sub ? h(SearchLibSubModal, { onClose: () => setSub(false) }) : null));
}

function DraftEditDrawer({ draft, night, onClose, onSave, onLive }) {
  const active = !!draft;
  const slide = useSlideAnim(active, 250);
  const [d, setD] = useStateD2(draft);
  const [checked, setChecked] = useStateD2({});
  const [scores, setScores] = useStateD2({});
  const [detOpen, setDetOpen] = useStateD2(false);
  const [agOpen, setAgOpen] = useStateD2(false);
  const [notes, setNotes] = useStateD2('');
  const [pop, setPop] = useStateD2(false);
  useEffectD2(() => {
    if (draft) {
      setD(draft);
      const init = {}; night.playerIds.forEach(p => init[p] = true);
      setChecked(init); setScores({}); setDetOpen(false); setAgOpen(false); setNotes('');
    }
  }, [draft]);
  useEffectD2(() => {
    const onKey = (ev) => { if (ev.key === 'Escape' && active) onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [active, onClose]);

  const cur = d;
  const game = cur ? (window.DS.byId[cur.gameId] || {}) : {};
  const agent = cur ? window.DS.agents.find(a => a.gameId === cur.gameId) : null;
  const hasLive = window.GN.liveSession(night);
  const checkedPids = (cur ? night.playerIds : []).filter(p => checked[p]);
  const scored = (p) => scores[p] !== undefined && scores[p] !== '' && Number.isInteger(+scores[p]) && +scores[p] >= 0;
  const allScored = checkedPids.length > 0 && checkedPids.every(scored);
  const valid = allScored;
  const ranking = checkedPids.slice().sort((a, b) => (+scores[b]) - (+scores[a]));

  const save = () => {
    const mvp = ranking[0];
    const score = +scores[mvp];
    onSave({ mvp, score, result: 'MVP ' + window.GN.pname(mvp) + ' · ' + score + ' pt' });
  };

  return h(React.Fragment, null,
    h('div', { ref: slide.scrimRef, className: 'drawer-scrim' + (active ? ' open' : ''), onClick: onClose }),
    h('aside', { ref: slide.panelRef, className: 'drawer e-session' + (active ? ' open' : ''), 'aria-hidden': !active },
      cur ? h(React.Fragment, null,
        h('div', { className: 'dr-head' },
          h('div', { className: 'dr-top' },
            h('div', { className: 'dr-cover', style: { background: game.cover } }, game.coverEmoji),
            h('div', { className: 'dr-titles' },
              h('h2', null, 'Session ' + cur.n + ': ' + (game.title || cur.gameId)),
              h('div', { className: 'dr-sub' }, h('button', { className: 'game-link', style: { color: 'var(--text-muted)' }, onClick: onClose }, night.name + ' · ' + night.dateLabel.replace('Oggi · ', ''))),
              (cur.startedAt || cur.completedAt) ? h('div', { className: 'ts-row' },
                cur.startedAt ? h('span', { className: 'ts-item' }, 'Iniziata alle ' + cur.startedAt) : null,
                cur.completedAt ? h('span', { className: 'ts-item' }, 'Terminata alle ' + cur.completedAt) : null,
                (cur.startedAt && cur.completedAt) ? h('span', { className: 'ts-item' }, 'Durata ' + window.GN.durMin(cur.startedAt, cur.completedAt) + ' min') : null) : null),
            h('button', { className: 'dr-close', onClick: onClose }, '✕')),
          h('div', { className: 'dr-status-row', style: { marginTop: 'var(--s-3)' } },
            h('span', { className: 'draft-chip' }, 'draft'),
            h('span', { style: { flex: 1 } }),
            hasLive
              ? h('span', { className: 'live-disabled-wrap' },
                  h('span', { className: 'game-link disabled', title: 'Una session live è già attiva. Termina la live corrente per aprire questa session in live.' }, '⏱ Apri Live mode'),
                  h('button', { className: 'info-btn', onClick: () => setPop(p => !p), title: 'Info' }, 'ⓘ'),
                  pop ? h('div', { className: 'info-pop' },
                    h('div', null, 'Una session live è già attiva (Session ' + hasLive.n + '). Termina la live corrente per aprire questa session in live.'),
                    h('button', { className: 'game-link', style: { marginTop: 'var(--s-2)' }, onClick: onLive }, 'Vai alla session live →')) : null)
              : h('button', { className: 'game-link', onClick: onLive, title: 'Converti in live' }, '⏱ Apri Live mode'))),
        h('div', { className: 'dr-content', style: { paddingBottom: '88px' } },
          // SECTION 1 — Player & score
          h('div', { className: 'dr-sec-label', style: { marginTop: 0 } }, 'Hanno giocato?', h('span', { className: 'ln' }),
            h(Gap, { cat: 'GAP-ENTITY', mini: true, loc: 'Session edit · scoring', note: 'scoring polymorphic — solo Points qui, TBD per BinaryWin/Objectives' })),
          h('div', { className: 'edit-players' },
            night.playerIds.map(pid => {
              const p = window.GN.playerById[pid] || {};
              const on = !!checked[pid];
              return h('div', { className: 'edit-prow', key: pid },
                h('button', { className: 'ck' + (on ? ' on' : ''), onClick: () => setChecked(c => ({ ...c, [pid]: !c[pid] })) }, on ? '✓' : ''),
                h('span', { className: 'avatar sm', style: { background: p.cover, color: '#fff' } }, p.initials),
                h('span', { className: 'edit-pname' }, p.title, !p.linked ? h('span', { className: 'pbadge guest', style: { marginLeft: 'var(--s-2)' } }, 'Guest') : null),
                on ? h('input', { className: 'score-input', type: 'number', min: 0, placeholder: 'Score', value: scores[pid] === undefined ? '' : scores[pid], onChange: (e) => setScores(s => ({ ...s, [pid]: e.target.value })) }) : null);
            })),
          allScored ? h('div', { className: 'rank-mini' }, 'Posizione: ' + ranking.map((p, i) => (i + 1) + '. ' + window.GN.pname(p) + ' (' + scores[p] + ')').join('  ')) : null,

          // SECTION 2 — Dettagli (collapsible)
          h('button', { className: 'collapse-head', onClick: () => setDetOpen(o => !o) }, h('span', null, 'Dettagli (opzionale)'), h('span', { className: 'chev' }, detOpen ? '▾' : '▸')),
          detOpen ? h('div', { className: 'collapse-body' },
            h('div', { className: 'wz-field' }, h('label', null, 'Note'), h('textarea', { className: 'wz-input', rows: 2, maxLength: 500, placeholder: 'Note libere, momenti chiave, battute memorabili.', value: notes, onChange: (e) => setNotes(e.target.value) })),
            h('div', { className: 'photo-grid' }, [0, 1, 2].map(i => h('div', { key: i, className: 'photo-ph' }, '📷'))),
            h('div', { style: { marginTop: 'var(--s-2)' } }, h(Gap, { cat: 'GAP-FEATURE', loc: 'Session edit · foto', note: 'upload TBD' }))) : null,

          // SECTION 3 — Agent (collapsible)
          h('button', { className: 'collapse-head', onClick: () => setAgOpen(o => !o) }, h('span', { className: 'e-fg e-agent' }, '🤖 Chiedi all\u2019agent'), h('span', { className: 'chev' }, agOpen ? '▾' : '▸')),
          agOpen ? h('div', { className: 'collapse-body' },
            h('button', { className: 'btn ghost' }, 'Apri chat con ' + (agent ? agent.title : 'l\u2019agent di ' + game.title)),
            h('div', { style: { marginTop: 'var(--s-2)' } }, h(Gap, { cat: 'GAP-CTA', loc: 'Session edit · agent', note: 'navigate /agents/' + (agent ? agent.id : '<id>') + ' TBD' }))) : null
        ),
        h('div', { className: 'dr-foot' },
          h('button', { className: 'btn primary e-session', disabled: !valid, onClick: save }, 'Salva session'),
          h('button', { className: 'btn ghost', onClick: onClose }, 'Annulla'))
      ) : null)
  );
}

Object.assign(window, { GameNightDetail, NewSessionModal });
