/* MeepleAI Nav Prototype — /game-nights/[id]/live (sp7-game-night-live).
   Immersive, no sidebar. Opt-in live mode: "LIVE" reflects that the user is
   actively in live mode on a session, NOT a GameNight state. */
const { useState: useStateL, useEffect: useEffectL, useRef: useRefL } = React;

function fmt(sec) { const m = Math.floor(sec / 60), s = sec % 60; return String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0'); }

function ChatOverlay({ agent, onClose }) {
  const [msgs, setMsgs] = useStateL([
    { who: 'agent', t: 'Ciao! Sono ' + (agent ? agent.title : 'l\u2019agente') + '. Chiedimi una regola o un chiarimento.' },
  ]);
  const [val, setVal] = useStateL('');
  const send = () => {
    if (!val.trim()) return;
    const q = val.trim();
    setMsgs(m => [...m, { who: 'me', t: q }, { who: 'agent', t: 'Risposta fixture: consulta il manuale al §' + (3 + q.length % 9) + ' per "' + q.slice(0, 18) + '".' }]);
    setVal('');
  };
  return h('div', { className: 'chat-overlay open' },
    h('div', { className: 'chat-head' }, h('span', null, '💬 ' + (agent ? agent.title : 'Agent')), h('button', { className: 'dr-close', onClick: onClose }, '✕')),
    h('div', { className: 'chat-msgs' }, msgs.map((m, i) => h('div', { key: i, className: 'chat-msg ' + m.who }, m.t))),
    h('div', { className: 'chat-input' },
      h('input', { className: 'wz-input', placeholder: 'Chiedi all\u2019agente…', value: val, onChange: (e) => setVal(e.target.value), onKeyDown: (e) => { if (e.key === 'Enter') send(); } }),
      h('button', { className: 'btn primary e-chat', onClick: send }, 'Invia')),
    h('div', { style: { padding: '0 var(--s-4) var(--s-4)' } }, h(Gap, { cat: 'GAP-DATA', loc: 'Live · chat agent', note: 'risposte agente fixture (nessun backend)' }))
  );
}

function GameNightLive({ id, state, setState, onExit, onGap }) {
  const n = window.GN.nightById[id];
  const liveSess = n ? (n.sessions || []).find(s => s.live) : null;
  const gameId = liveSess ? liveSess.gameId : (n && n.gameIds[0]);
  const game = gameId ? window.DS.byId[gameId] : null;
  const agent = game ? window.DS.agents.find(a => a.gameId === game.id) : null;

  const [secs, setSecs] = useStateL(760); // GameNight elapsed
  const [ssecs, setSsecs] = useStateL(360); // session elapsed
  const [scores, setScores] = useStateL(() => Object.fromEntries((n ? n.playerIds : []).map(p => [p, 0])));
  const [turn, setTurn] = useStateL(0);
  const [log, setLog] = useStateL(['🎲 Marco ha tirato 8', '🔄 Turno passato a Sara', '➕ Sara +3 punti']);
  const [chat, setChat] = useStateL(false);
  const [paused, setPaused] = useStateL(false);
  const [showNewSess, setShowNewSess] = useStateL(false);
  const [toast, setToast] = useStateL(null);

  useEffectL(() => {
    if (paused || state === 'empty') return;
    const t = setInterval(() => { setSecs(s => s + 1); setSsecs(s => s + 1); }, 1000);
    return () => clearInterval(t);
  }, [paused, state]);

  if (!n) return h('div', { className: 'live-empty' }, h('h2', null, 'Game Night non trovata'), h('button', { className: 'btn primary e-event', onClick: onExit }, 'Esci'));

  const players = n.playerIds.map(p => window.GN.playerById[p] || {});
  const bump = (pid, d) => { setScores(s => ({ ...s, [pid]: Math.max(0, (s[pid] || 0) + d) })); setLog(l => [(d > 0 ? '➕ ' : '➖ ') + window.GN.pname(pid) + ' ' + (d > 0 ? '+' : '') + d, ...l].slice(0, 6)); };
  const passTurn = () => { const nt = (turn + 1) % n.playerIds.length; setTurn(nt); setLog(l => ['🔄 Turno → ' + window.GN.pname(n.playerIds[nt]), ...l].slice(0, 6)); };

  return h('div', { className: 'live' },
    h('div', { className: 'live-top' },
      h('span', { className: 'live-mark' }, '📅'),
      h('span', { className: 'live-name' }, n.name, h('span', { className: 'live-badge' }, h('span', { className: 'live-dot' }), 'LIVE')),
      h('span', { className: 'live-timer' }, fmt(secs)),
      h(Gap, { cat: 'GAP-ENTITY', loc: 'Live · badge LIVE', note: 'live = attributo della session attiva, non stato della GameNight', mini: true }),
      h('span', { style: { flex: 1 } }),
      h(StateToggle, { value: state, onChange: setState }),
      h('button', { className: 'gap-pill', onClick: onGap, title: 'Gap audit' }, '⚠ GAP', h('span', { className: 'n' }, h(GapCount))),
      h('button', { className: 'btn ghost', onClick: () => setShowNewSess(true) }, '+ Nuova session'),
      h('span', { className: 'pausa-wrap' },
        h('button', { className: 'btn ghost', onClick: () => setPaused(p => !p) }, paused ? '▶ Riprendi' : '⏸ Pausa live'),
        h(Gap, { cat: 'GAP-ENTITY', mini: true, loc: 'Live · Pausa', note: 'live → draft transition TBD' })),
      h('button', { className: 'icon-btn', onClick: onExit, title: 'Esci' }, '✕')),

    state === 'offline' ? h('div', { className: 'live-offline' }, '⚠ Sei offline. Lo scoring locale verrà sincronizzato al ripristino della connessione.') : null,

    state === 'empty'
      ? h('div', { className: 'live-empty' },
          h('div', { className: 'sb-icon e-session', style: { background: 'hsl(var(--c-session) / 0.12)' } }, '🎯'),
          h('h2', null, 'Nessuna session attiva'),
          h('p', null, 'La serata è in corso ma non c\u2019è una partita avviata. Avvia la prima session per iniziare lo scoring.'),
          h('button', { className: 'sb-cta', style: { background: 'hsl(var(--c-session))' }, onClick: () => setState('default') }, '+ Avvia la prima session'))
      : h('div', { className: 'live-grid' },
          // LEFT — current session
          h('div', { className: 'live-col' },
            h('div', { className: 'live-sec-h' }, 'Session corrente'),
            h('div', { className: 'live-game' },
              h('span', { className: 'sess-cover', style: { background: game ? game.cover : '' } }, game ? game.coverEmoji : '🎲'),
              h('div', null, h('div', { className: 'live-game-name' }, game ? game.title : '—'), h('div', { className: 'live-game-sub' }, 'Session timer ' + fmt(ssecs)))),
            h('div', { className: 'live-scores' },
              players.map((p, i) => h('div', { className: 'live-score-row' + (i === turn ? ' turn' : ''), key: p.id },
                h('span', { className: 'avatar sm', style: { background: p.cover, color: '#fff' } }, p.initials),
                h('span', { className: 'lsr-name' }, p.title),
                h('span', { style: { flex: 1 } }),
                h('button', { className: 'score-btn', onClick: () => bump(p.id, -1) }, '−'),
                h('span', { className: 'lsr-score' }, scores[p.id] || 0),
                h('button', { className: 'score-btn', onClick: () => bump(p.id, 1) }, '+')))),
            h('button', { className: 'btn ghost', style: { marginTop: 'var(--s-3)' }, onClick: passTurn }, '🔄 Passa turno'),
            h('div', { className: 'live-sec-h', style: { marginTop: 'var(--s-4)' } }, 'Action log'),
            h('div', { className: 'live-log' }, log.map((l, i) => h('div', { className: 'live-log-row', key: i }, l)))),
          // RIGHT — player rail
          h('div', { className: 'live-rail' },
            h('div', { className: 'live-sec-h' }, 'Player rail'),
            players.map((p, i) => h('div', { className: 'live-rail-row' + (i === turn ? ' turn' : ''), key: p.id },
              h('span', { className: 'avatar', style: { background: p.cover, color: '#fff' } }, p.initials),
              h('div', { className: 'lrr-info' }, h('span', { className: 'lrr-name' }, p.title), i === turn ? h('span', { className: 'lrr-turn' }, 'turno corrente') : null),
              h('span', { className: 'lrr-score' }, scores[p.id] || 0))))),

    state !== 'empty' ? h('div', { className: 'live-foot' },
      h('button', { className: 'btn primary e-session', onClick: () => { window.GN.terminateLive(n.id, scores); onExit(); } }, 'Termina session'),
      h('button', { className: 'btn ghost', onClick: () => setChat(true) }, '💬 Apri Chat Agent')) : null,

    chat ? h(ChatOverlay, { agent, onClose: () => setChat(false) }) : null,

    showNewSess ? h(window.NewSessionModal, { night: n, liveSession: liveSess,
      onClose: () => setShowNewSess(false),
      onGoLive: () => setShowNewSess(false),
      onCreate: (gameId) => { window.GN.addDraftSession(n.id, gameId); setShowNewSess(false); const g = window.DS.byId[gameId] || {}; setToast('Session draft creata (' + g.title + '). Compila i risultati su ' + n.name + '.'); } }) : null,

    toast ? h('div', { className: 'toast' },
      h('span', null, toast),
      h('button', { className: 'toast-go', onClick: onExit }, 'Vai'),
      h('button', { className: 'toast-x', onClick: () => setToast(null) }, '✕')) : null
  );
}

Object.assign(window, { GameNightLive });
