/* sp4-toolkit-history-ui.jsx — components + interactive app + harness (loads after sp4-toolkit-history.jsx)
   Route: /toolkit/history — vedi header comment del file foundation. */

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const DS = window.DS;
const T = window.__TH;
const {
  eHsl, sem, SESSIONS, TOTAL, PAGE_SIZE, GAME_COUNTS, WINNER_COUNTS, N_GAMES,
  fmtDate, fmtTime, fmtDur, relDate, NOW, MONTHS_FULL, genTimeline, GAME_IDS,
} = T;

const pColor = (h, l = 52, s = 58) => `hsl(${h}, ${s}%, ${l}%)`;

// ═══════════════════════════════════════════════════════
// ─── PRIMITIVES ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const GameChip = ({ s, onClick }) => (
  <button type="button" className="th-egame" title={`Apri ${s.gameName} in libreria`}
    onClick={e => { e.stopPropagation(); onClick && onClick(); }}>
    <span className="cov" aria-hidden="true" style={{ background: s.cover }}>{s.coverEmoji}</span>
    <span className="gt">{s.gameName}</span>
  </button>
);

const AvatarStack = ({ players, max = 3 }) => {
  const shown = players.slice(0, max);
  const extra = players.length - shown.length;
  const label = `${players.length} giocatori: ${players.map(p => p.name).join(', ')}`;
  return (
    <span className="th-avs" role="img" aria-label={label}>
      {shown.map((p, i) => (
        <span key={p.pid} className="th-av" style={{ background: pColor(p.color), zIndex: max - i }} aria-hidden="true">{p.initials}</span>
      ))}
      {extra > 0 && <span className="th-av more" aria-hidden="true">+{extra}</span>}
    </span>
  );
};

const WinnerCell = ({ s }) => {
  if (s.coop) return <span className="th-coop"><span aria-hidden="true">🤝</span>Cooperativa</span>;
  if (!s.winner) return <span className="th-nowin" aria-label="Senza vincitore">—</span>;
  return (
    <span className="th-win" title={`Vincitore: ${s.winner}`}>
      <span aria-hidden="true">🏆</span><span className="wt">{s.winner}</span>
    </span>
  );
};

// click-outside hook
function useOutside(onClose) {
  const ref = useRef(null);
  useEffect(() => {
    const onDoc = e => { if (ref.current && !ref.current.contains(e.target)) onClose(); };
    const onEsc = e => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onEsc);
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onEsc); };
  }, [onClose]);
  return ref;
}

// ═══════════════════════════════════════════════════════
// ─── TOOLBAR ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const MultiDropdown = ({ open, setOpen, label, icon, tint, count, headLabel, options, selected, onToggle, onClear, more }) => {
  const ref = useOutside(() => setOpen(false));
  const [showAll, setShowAll] = useState(false);
  const visible = (more && !showAll) ? options.slice(0, more) : options;
  return (
    <div className="th-fgroup" ref={ref}>
      <button type="button" className={'th-chip ' + tint + (count > 0 ? ' on' : '')} aria-haspopup="listbox" aria-expanded={open}
        onClick={() => setOpen(o => !o)}>
        <span aria-hidden="true">{icon}</span>{label}{count > 0 && <span className="ccount">({count})</span>}
        <span aria-hidden="true" style={{ fontSize: 8, opacity: .7 }}>▼</span>
      </button>
      {open && (
        <div className="th-pop" role="listbox" aria-label={headLabel}>
          <div className="th-pophead">{headLabel}</div>
          <button type="button" className={'th-popitem' + (count === 0 ? ' sel' : '')} role="option" aria-selected={count === 0} onClick={onClear}>
            <span className="check" aria-hidden="true">{count === 0 ? '✓' : ''}</span>
            <span className="lbl">Tutti</span>
          </button>
          {visible.map(o => {
            const on = selected.includes(o.value);
            return (
              <button key={o.value} type="button" className={'th-popitem' + (on ? ' sel' : '')} role="option" aria-selected={on} onClick={() => onToggle(o.value)}>
                <span className="check" aria-hidden="true">{on ? '✓' : ''}</span>
                {o.swatch && <span style={{ width: 16, height: 16, borderRadius: 4, background: o.swatch, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: 9 }}>{o.emoji}</span>}
                {o.avatar && <span className="th-av" style={{ background: o.avatar, margin: 0, width: 18, height: 18, border: 'none' }}>{o.initials}</span>}
                <span className="lbl">{o.label}</span>
                <span className="cnt">{o.count}</span>
              </button>
            );
          })}
          {more && options.length > more && (
            <button type="button" className="th-popmore" onClick={() => setShowAll(a => !a)}>
              {showAll ? 'Mostra meno' : `Mostra altri ${options.length - more}`}
            </button>
          )}
        </div>
      )}
    </div>
  );
};

const DATE_OPTS = [
  { id: 'all', label: 'Tutte le date' },
  { id: '30', label: 'Ultimi 30 giorni' },
  { id: '90', label: 'Ultimi 90 giorni' },
  { id: '365', label: 'Ultimo anno' },
  { id: 'custom', label: 'Date custom' },
];
const SORT_OPTS = [
  { id: 'recent', label: 'Più recente' },
  { id: 'oldest', label: 'Più antica' },
  { id: 'longest', label: 'Più lunga' },
  { id: 'score', label: 'Score più alto' },
];

const Toolbar = ({ st, set, typing, searchRef, counts }) => {
  const [openGame, setOpenGame] = useState(false);
  const [openWin, setOpenWin] = useState(false);
  const [openDate, setOpenDate] = useState(false);
  const [openSort, setOpenSort] = useState(false);
  const [customMode, setCustomMode] = useState(st.date === 'custom');
  const [cfrom, setCfrom] = useState(st.customFrom || '');
  const [cto, setCto] = useState(st.customTo || '');
  const shortD = iso => { if (!iso) return ''; const [y, m, d] = iso.split('-').map(Number); return `${d} ${['gen','feb','mar','apr','mag','giu','lug','ago','set','ott','nov','dic'][m - 1]}`; };
  const dateLabel = (st.date === 'custom' && st.customFrom && st.customTo) ? `${shortD(st.customFrom)} – ${shortD(st.customTo)}` : DATE_OPTS.find(d => d.id === st.date).label;

  const gameOpts = GAME_COUNTS.map(g => ({ value: g.id, label: DS.byId[g.id].title, count: g.count, swatch: DS.byId[g.id].cover, emoji: DS.byId[g.id].coverEmoji }));
  const winOpts = [
    ...WINNER_COUNTS.players.map(w => ({ value: w.name, label: `${w.name} (vinto)`, count: w.count, avatar: pColor(playerHue(w.name)), initials: initialsOf(w.name) })),
    { value: '__nowin__', label: 'Senza vincitore', count: WINNER_COUNTS.noWin },
  ];
  const winCount = st.winners.length;
  const dateRef = useOutside(() => setOpenDate(false));
  const sortRef = useOutside(() => setOpenSort(false));

  return (
    <div className="th-toolbar">
      {/* search */}
      <div className={'th-search' + (st.search ? ' active' : '')}>
        <span className="ic" aria-hidden="true">🔍</span>
        <input ref={searchRef} value={st.search} onChange={e => set({ search: e.target.value, page: 1 })} role="searchbox"
          aria-label="Cerca per gioco o vincitore" placeholder="Cerca per gioco o vincitore…"
          onKeyDown={e => { if (e.key === 'Escape') { set({ search: '' }); } }} />
        {typing && <span className="busy" aria-hidden="true"><i />Cercando…</span>}
        {st.search && <button className="clear" aria-label="Cancella ricerca" onClick={() => { set({ search: '', page: 1 }); searchRef.current && searchRef.current.focus(); }}>✕</button>}
      </div>

      {/* filters row */}
      <div className="th-filters" role="group" aria-label="Filtri">
        <MultiDropdown open={openGame} setOpen={setOpenGame} label="Giochi" icon="🎲" tint="game" count={st.games.length}
          headLabel="Filtra per gioco" options={gameOpts} selected={st.games} more={4}
          onToggle={v => set({ games: st.games.includes(v) ? st.games.filter(x => x !== v) : [...st.games, v], page: 1 })}
          onClear={() => set({ games: [], page: 1 })} />

        <span className="th-fdiv" />

        {/* date range */}
        <div className="th-fgroup" ref={dateRef}>
          <button type="button" className={'th-chip tk' + (st.date !== 'all' ? ' on' : '')} aria-haspopup="listbox" aria-expanded={openDate} onClick={() => setOpenDate(o => !o)}>
            <span aria-hidden="true">🗓</span>{dateLabel}
            <span aria-hidden="true" style={{ fontSize: 8, opacity: .7 }}>▼</span>
          </button>
          {openDate && (
            <div className="th-pop" role="listbox" aria-label="Intervallo date">
              <div className="th-pophead">Intervallo date</div>
              {DATE_OPTS.map(d => {
                const on = d.id === 'custom' ? (customMode || st.date === 'custom') : st.date === d.id;
                return (
                  <button key={d.id} type="button" className={'th-popitem radio' + (on ? ' sel' : '')} role="option" aria-selected={on}
                    onClick={() => {
                      if (d.id === 'custom') { setCustomMode(true); }
                      else { set({ date: d.id, page: 1 }); setCustomMode(false); setOpenDate(false); }
                    }}>
                    <span className="check" aria-hidden="true">{on ? '•' : ''}</span>
                    <span className="lbl">{d.label}</span>
                  </button>
                );
              })}
              {customMode && (
                <div className="th-daterange">
                  <div className="r2">
                    <label>Da<input type="date" value={cfrom} max={cto || undefined} onChange={e => setCfrom(e.target.value)} aria-label="Data inizio" /></label>
                    <label>A<input type="date" value={cto} min={cfrom || undefined} onChange={e => setCto(e.target.value)} aria-label="Data fine" /></label>
                  </div>
                  <button type="button" className="apply" disabled={!cfrom || !cto}
                    onClick={() => { set({ date: 'custom', customFrom: cfrom, customTo: cto, page: 1 }); setOpenDate(false); }}>Applica intervallo</button>
                </div>
              )}
            </div>
          )}
        </div>

        <span className="th-fdiv" />

        <MultiDropdown open={openWin} setOpen={setOpenWin} label="Vincitori" icon="🏆" tint="win" count={winCount}
          headLabel="Filtra per vincitore" options={winOpts} selected={st.winners} more={5}
          onToggle={v => set({ winners: st.winners.includes(v) ? st.winners.filter(x => x !== v) : [...st.winners, v], page: 1 })}
          onClear={() => set({ winners: [], page: 1 })} />
      </div>

      {/* view + sort */}
      <div className="th-right">
        <div className="th-fgroup" ref={sortRef}>
          <button type="button" className="th-chip" aria-haspopup="listbox" aria-expanded={openSort} onClick={() => setOpenSort(o => !o)}>
            <span aria-hidden="true">↕</span>{SORT_OPTS.find(s => s.id === st.sort).label}
            <span aria-hidden="true" style={{ fontSize: 8, opacity: .7 }}>▼</span>
          </button>
          {openSort && (
            <div className="th-pop right" role="listbox" aria-label="Ordina">
              <div className="th-pophead">Ordina per</div>
              {SORT_OPTS.map(o => (
                <button key={o.id} type="button" className={'th-popitem radio' + (st.sort === o.id ? ' sel' : '')} role="option" aria-selected={st.sort === o.id}
                  onClick={() => { set({ sort: o.id }); setOpenSort(false); }}>
                  <span className="check" aria-hidden="true">{st.sort === o.id ? '•' : ''}</span>
                  <span className="lbl">{o.label}</span>
                </button>
              ))}
            </div>
          )}
        </div>
        <div className="th-vtoggle" role="group" aria-label="Vista">
          <button aria-pressed={st.view === 'table'} onClick={() => set({ view: 'table' })} title="Vista tabella" aria-label="Vista tabella">☰</button>
          <button aria-pressed={st.view === 'cards'} onClick={() => set({ view: 'cards' })} title="Vista cards" aria-label="Vista cards">▦</button>
        </div>
      </div>
    </div>
  );
};

function playerHue(name) { const p = DS.players.find(x => x.title === name); return p ? p.color : 262; }
function initialsOf(name) { const p = DS.players.find(x => x.title === name); return p ? p.initials : name.slice(0, 2).toUpperCase(); }

// ═══════════════════════════════════════════════════════
// ─── TABLE (desktop) ────────────────────────────────
// ═══════════════════════════════════════════════════════
const TH = ({ label, k, sort, onSort, align }) => {
  const active = sort.key === k;
  const aria = active ? (sort.dir === 'asc' ? 'ascending' : 'descending') : 'none';
  return (
    <div className={'th-th' + (k ? ' sortable' : '') + (align ? ' ' + align : '')} role="columnheader" aria-sort={k ? aria : undefined}
      tabIndex={k ? 0 : undefined} onClick={k ? () => onSort(k) : undefined}
      onKeyDown={k ? e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); onSort(k); } } : undefined}>
      {label}{k && <span className="arr">{active && sort.dir === 'asc' ? '▲' : '▼'}</span>}
    </div>
  );
};

const Row = ({ s, selected, onOpen, rowRef }) => (
  <div className={'th-row' + (s.mine ? ' mine' : '') + (selected ? ' sel' : '')} role="row" tabIndex={0} ref={rowRef}
    onClick={() => onOpen(s)} onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); onOpen(s); } }}>
    <div className="th-rowmain">
      <div className="th-cell" role="cell">
        <div className="th-dt">{fmtDate(s.date)} · {fmtTime(s.date)}<span className="rel">{relDate(s.date, NOW)}</span></div>
      </div>
      <div className="th-cell" role="cell"><GameChip s={s} /></div>
      <div className="th-cell" role="cell"><span className="th-durpill"><span aria-hidden="true">⏱</span>{fmtDur(s.durationMinutes)}</span></div>
      <div className="th-cell" role="cell"><AvatarStack players={s.players} /></div>
      <div className="th-cell" role="cell"><WinnerCell s={s} /></div>
      <div className="th-cell" role="cell">
        {s.coop ? <span className="th-score coop">co-op</span> : <span className="th-score" title="Punteggio vincitore">{s.winScore}</span>}
      </div>
      <div className="th-cell" role="cell" style={{ textAlign: 'center' }}>
        <button type="button" className={'th-noteic' + (s.notes ? '' : ' empty')} title={s.notes || 'Nessuna nota'}
          aria-label={s.notes ? 'Nota presente' : 'Nessuna nota'} onClick={e => { e.stopPropagation(); if (s.notes) onOpen(s); }}>📝</button>
      </div>
      <div className="th-cell" role="cell">
        <div className="th-acts">
          <button type="button" className="th-act see" aria-label="Vedi dettagli" title="Dettagli" onClick={e => { e.stopPropagation(); onOpen(s); }}>👁</button>
          <button type="button" className="th-act" aria-label="Stats del gioco" title="Stats gioco" onClick={e => e.stopPropagation()}>📊</button>
        </div>
      </div>
    </div>
  </div>
);

const Table = ({ rows, selectedId, onOpen, sort, onSort, firstRef }) => (
  <div className="th-table" role="table" aria-label="Storico sessioni">
    <div className="th-thead" role="row">
      <TH label="Data" k="recent" sort={sort} onSort={onSort} />
      <TH label="Gioco" k="game" sort={sort} onSort={onSort} />
      <TH label="Durata" k="longest" sort={sort} onSort={onSort} />
      <div className="th-th" role="columnheader">Giocatori</div>
      <div className="th-th" role="columnheader">Vincitore</div>
      <TH label="Score" k="score" sort={sort} onSort={onSort} />
      <div className="th-th center" role="columnheader">Note</div>
      <div className="th-th right" role="columnheader">Azioni</div>
    </div>
    <div role="rowgroup">
      {rows.map((s, i) => <Row key={s.id} s={s} selected={s.id === selectedId} onOpen={onOpen} rowRef={i === 0 ? firstRef : null} />)}
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── CARDS (mobile) ─────────────────────────────────
// ═══════════════════════════════════════════════════════
const SessionCard = ({ s, onOpen }) => (
  <div className={'th-card' + (s.mine ? ' mine' : '')} role="button" tabIndex={0}
    onClick={() => onOpen(s)} onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); onOpen(s); } }}>
    <div className="chead">
      <GameChip s={s} />
      <span className="grow" />
      <span className="cdate">{relDate(s.date, NOW)}</span>
    </div>
    <div className="cmid">
      <AvatarStack players={s.players} />
      <WinnerCell s={s} />
      <span className="th-durpill"><span aria-hidden="true">⏱</span>{fmtDur(s.durationMinutes)}</span>
    </div>
    <div className="cfoot">
      {s.coop
        ? <span className="th-cscore" style={{ background: 'var(--bg-muted)', color: 'var(--text-muted)' }}><span aria-hidden="true">🤝</span>Co-op</span>
        : <span className="th-cscore"><span aria-hidden="true">🏆</span>{s.winScore}</span>}
      <span className="grow" />
      {s.notes && <span className="th-noteic" title={s.notes} aria-label="Nota presente">📝</span>}
      <span className="cdate">{fmtDate(s.date)}</span>
    </div>
  </div>
);

const Cards = ({ rows, onOpen }) => (
  <div className="th-cards">{rows.map(s => <SessionCard key={s.id} s={s} onOpen={onOpen} />)}</div>
);

// ═══════════════════════════════════════════════════════
// ─── DETAIL MODAL ───────────────────────────────────
// ═══════════════════════════════════════════════════════
function gameStats(s) {
  const scores = s.players.map(p => p.score);
  const mean = scores.reduce((a, b) => a + b, 0) / scores.length;
  const sigma = Math.sqrt(scores.reduce((a, b) => a + (b - mean) ** 2, 0) / scores.length);
  const sameGame = SESSIONS.filter(x => x.gameId === s.gameId && !x.coop);
  const best = sameGame.length ? Math.max(...sameGame.map(x => x.winScore)) : s.winScore;
  let highlights;
  if (s.id === SESSIONS[0].id) highlights = ['🛣️ Strada più lunga ×2', '⚔️ Armata più grande ×1'];
  else if (s.coop) highlights = ['🤝 Vittoria cooperativa', '🌋 Spiriti in sinergia'];
  else if (sigma < (mean * 0.12)) highlights = ['⚖️ Partita equilibrata'];
  else highlights = ['🚀 Vittoria netta'];
  return { sigma: sigma.toFixed(1), best, mine: s.winScore === best && s.gameId === s.gameId };
}

const DetailModal = ({ s, mobile, onClose }) => {
  const closeRef = useRef(null);
  const modalRef = useRef(null);
  const [tlOpen, setTlOpen] = useState(true);
  useEffect(() => { closeRef.current && closeRef.current.focus(); }, []);
  useEffect(() => {
    const onKey = e => {
      if (e.key === 'Escape') { onClose(); return; }
      if (e.key === 'Tab' && modalRef.current) {
        const f = modalRef.current.querySelectorAll('button, textarea, [tabindex]:not([tabindex="-1"])');
        if (!f.length) return;
        const first = f[0], last = f[f.length - 1];
        if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
      }
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onClose]);

  const leaderboard = [...s.players].sort((a, b) => b.score - a.score);
  const tl = genTimeline(s);
  const gs = gameStats(s);

  return (
    <div className="th-overlay" onMouseDown={e => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="th-modal" ref={modalRef} role="dialog" aria-modal="true" aria-labelledby="th-mtitle">
        <div className="th-mhead">
          <span className="mcov" aria-hidden="true" style={{ background: s.cover }}>{s.coverEmoji}</span>
          <div className="th-mhtxt">
            <div className="th-mtitle" id="th-mtitle">{s.gameName} · {fmtDate(s.date)}</div>
            <div className="th-msub">Durata {fmtDur(s.durationMinutes)} · {s.players.length} giocatori · {fmtTime(s.date)}</div>
          </div>
          <button type="button" className="th-mclose" ref={closeRef} aria-label="Chiudi" onClick={onClose}>✕</button>
        </div>

        <div className="th-mbody">
          {/* leaderboard */}
          <div className="th-msec">
            <h4>Classifica finale<span className="gr" /></h4>
            <div className="th-lb">
              {leaderboard.map((p, i) => (
                <div key={p.pid} className={'th-lbrow' + (i === 0 && !s.coop ? ' first' : '')}>
                  <span className="th-lbrank">{s.coop ? '·' : `#${i + 1}`}</span>
                  <span className="th-lbplayer">
                    <span className="th-av" style={{ background: pColor(p.color), margin: 0 }} aria-hidden="true">{p.initials}</span>
                    <span className="nm">{p.name}{p.name === 'Marco R.' && <span className="th-lbtag"> · tu</span>}</span>
                  </span>
                  <span className="th-lbscore">{s.coop ? '—' : p.score}{i === 0 && !s.coop && <span aria-hidden="true" style={{ marginLeft: 6 }}>🏆</span>}</span>
                </div>
              ))}
            </div>
          </div>

          {/* timeline */}
          <div className="th-msec">
            <button type="button" className="th-tl-toggle" aria-expanded={tlOpen} onClick={() => setTlOpen(o => !o)}>
              <span className="car" style={{ transform: tlOpen ? 'rotate(90deg)' : 'none' }} aria-hidden="true">▶</span>
              Timeline eventi<span className="gr" />
            </button>
            {tlOpen && (
              <ul className="th-tl">
                {tl.map((ev, i) => (
                  <li key={i} className="th-tlrow">
                    <span className="th-tltime">{ev.t}</span>
                    <span className="th-tldot" aria-hidden="true">{ev.icon}</span>
                    <span className="th-tle">{ev.e}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>

          {/* notes */}
          <div className="th-msec">
            <h4>Note<span className="gr" /></h4>
            {s.notes
              ? <textarea className="th-noteta" readOnly value={s.notes} aria-label="Note della sessione" />
              : <p className="th-noteempty">Nessuna nota per questa sessione.</p>}
          </div>

          {/* game stats */}
          <div className="th-msec">
            <h4>Statistiche partita<span className="gr" /></h4>
            <div className="th-gstats">
              <div className="th-gstat"><div className="gl">Turni totali</div><div className="gv">{s.turns}</div></div>
              <div className="th-gstat"><div className="gl">Score vincitore</div><div className="gv">{s.coop ? '—' : <>{s.winScore} <small>· best {gs.best}</small></>}</div></div>
              <div className="th-gstat"><div className="gl">Varianza score</div><div className="gv">σ {gs.sigma}</div></div>
              <div className="th-gstat"><div className="gl">Highlights</div>
                <div className="th-hl">{highlightsFor(s).map((h, i) => <span key={i} className="th-hlchip">{h}</span>)}</div>
              </div>
            </div>
          </div>
        </div>

        <div className="th-mfoot">
          <button type="button" className="th-mbtn tk"><span aria-hidden="true">📊</span>Stats gioco</button>
          <button type="button" className="th-mbtn game"><span aria-hidden="true">🎮</span>{mobile ? 'Rigioca' : 'Gioca di nuovo'}</button>
          <span className="grow" />
          {!mobile && <button type="button" className="th-mbtn"><span aria-hidden="true">📝</span>Modifica note</button>}
          <button type="button" className="th-mbtn danger"><span aria-hidden="true">🗑</span>Elimina</button>
        </div>
      </div>
    </div>
  );
};
function highlightsFor(s) {
  if (s.id === SESSIONS[0].id) return ['🛣️ Strada più lunga ×2', '⚔️ Armata più grande ×1'];
  if (s.coop) return ['🤝 Vittoria cooperativa'];
  const scores = s.players.map(p => p.score);
  const mean = scores.reduce((a, b) => a + b, 0) / scores.length;
  const sigma = Math.sqrt(scores.reduce((a, b) => a + (b - mean) ** 2, 0) / scores.length);
  return sigma < mean * 0.12 ? ['⚖️ Partita equilibrata'] : ['🚀 Vittoria netta'];
}

// ═══════════════════════════════════════════════════════
// ─── EMPTY / LOADING / ERROR ────────────────────────
// ═══════════════════════════════════════════════════════
const EmptyAll = () => (
  <div className="th-pad">
    <div className="th-empty tk">
      <div className="em" aria-hidden="true">📜</div>
      <h3>Nessuna sessione ancora</h3>
      <p>Le partite finalizzate appariranno qui: filtra per gioco, data o vincitore e apri il dettaglio di ognuna.</p>
      <button className="cta"><span aria-hidden="true">🎮</span>Crea prima sessione</button>
    </div>
  </div>
);
const FilterEmpty = ({ onClear }) => (
  <div className="th-pad" aria-live="polite">
    <div className="th-empty warn">
      <div className="em" aria-hidden="true">🔍</div>
      <h3>Nessuna sessione corrisponde ai filtri</h3>
      <p>Prova a modificare la ricerca o a rimuovere i filtri di gioco, data o vincitore attivi.</p>
      <button className="cta warn" onClick={onClear}>Rimuovi filtri</button>
    </div>
  </div>
);
const Sk = ({ w, h, r = 'var(--r-sm)', style }) => <div className="th-sk th-shimmer" style={{ width: w, height: h, borderRadius: r, ...style }} />;
const Skeleton = ({ mobile }) => {
  if (mobile) return (
    <div className="th-cards" aria-busy="true">
      {[0, 1, 2, 3].map(i => (
        <div className="th-card" key={i}>
          <div className="chead"><Sk w={130} h={22} r="var(--r-pill)" /><span style={{ flex: 1 }} /><Sk w={50} h={12} /></div>
          <div className="cmid"><Sk w={90} h={26} r="var(--r-pill)" /><Sk w={110} h={22} r="var(--r-pill)" /></div>
          <div className="cfoot"><Sk w={70} h={28} r="var(--r-md)" /></div>
        </div>
      ))}
    </div>
  );
  return (
    <div className="th-table" aria-busy="true">
      <div className="th-thead">{['Data', 'Gioco', 'Durata', 'Gioc.', 'Vinc.', 'Score', 'N', 'Az'].map((k, i) => <Sk key={i} h={11} w={i === 1 ? '60%' : '52%'} />)}</div>
      {[0, 1, 2, 3, 4, 5].map(i => (
        <div className="th-rowmain" key={i} style={{ borderBottom: '1px solid var(--border-light)' }}>
          <div><Sk w="86%" h={13} style={{ marginBottom: 6 }} /><Sk w="50%" h={10} /></div>
          <Sk w="90%" h={26} r="var(--r-pill)" />
          <Sk w={52} h={13} />
          <Sk w={86} h={26} r="var(--r-pill)" />
          <Sk w="78%" h={24} r="var(--r-pill)" />
          <Sk w={32} h={16} />
          <Sk w={24} h={24} r="var(--r-sm)" style={{ margin: '0 auto' }} />
          <Sk w={70} h={30} r="var(--r-sm)" style={{ marginLeft: 'auto' }} />
        </div>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── PAGINATION ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
function pageList(cur, total) {
  const out = [];
  const add = n => out.push(n);
  if (total <= 7) { for (let i = 1; i <= total; i++) add(i); return out; }
  add(1);
  let lo = Math.max(2, cur - 1), hi = Math.min(total - 1, cur + 1);
  if (cur <= 3) { lo = 2; hi = 4; }
  if (cur >= total - 2) { lo = total - 3; hi = total - 1; }
  if (lo > 2) add('…l');
  for (let i = lo; i <= hi; i++) add(i);
  if (hi < total - 1) add('…r');
  add(total);
  return out;
}
const Pagination = ({ page, pages, total, count, pageSize, from, to, onPage, onSize, mobile }) => {
  if (mobile) return (
    <nav className="th-pag" role="navigation" aria-label="Pagine storico sessioni">
      <button className="th-pagbtn ar" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Pagina precedente">←</button>
      <span className="th-pagmeta">Pag. <b>{page}</b> di {pages} · {total} sess.</span>
      <button className="th-pagbtn ar" disabled={page >= pages} onClick={() => onPage(page + 1)} aria-label="Pagina successiva">→</button>
    </nav>
  );
  return (
    <nav className="th-pag" role="navigation" aria-label="Pagine storico sessioni">
      <button className="th-pagbtn" disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Pagina precedente"><span aria-hidden="true">←</span> Prec.</button>
      <div className="th-pagnums">
        {pageList(page, pages).map((p, i) => typeof p === 'number'
          ? <button key={i} className={'th-pagnum' + (p === page ? ' on' : '')} aria-current={p === page ? 'page' : undefined} onClick={() => onPage(p)}>{p}</button>
          : <span key={i} className="th-pagell" aria-hidden="true">···</span>)}
      </div>
      <button className="th-pagbtn" disabled={page >= pages} onClick={() => onPage(page + 1)} aria-label="Pagina successiva">Succ. <span aria-hidden="true">→</span></button>
      <span className="grow" />
      <span className="th-pagmeta">{count ? `${from}–${to} di ${total} sessioni` : '0 sessioni'}</span>
      <label className="th-pagsize">Per pagina
        <select value={pageSize} onChange={e => onSize(Number(e.target.value))} aria-label="Sessioni per pagina">
          {[10, 20, 50, 100].map(n => <option key={n} value={n}>{n}</option>)}
        </select>
      </label>
    </nav>
  );
};

// ═══════════════════════════════════════════════════════
// ─── HEADER (sticky + tabs) ─────────────────────────
// ═══════════════════════════════════════════════════════
const TABS = [
  { id: 'stats', icon: '📊', label: 'Stats', href: 'sp4-toolkit-stats.html' },
  { id: 'history', icon: '📜', label: 'History', href: null },
  { id: 'templates', icon: '🎨', label: 'Templates', href: null },
  { id: 'play', icon: '🎮', label: 'Play', href: null },
];
const Header = ({ mobile }) => (
  <header className="th-head">
    <div className="th-htop">
      <div className="th-htxt">
        <div className="th-bread"><span>Toolkit</span><span className="sep" aria-hidden="true">›</span><span className="cur">History</span></div>
        <div className="th-titlerow">
          <span className="th-ico" aria-hidden="true">🧰</span>
          <h1 className="th-h1">Storico sessioni</h1>
        </div>
        {!mobile && <p className="th-sub">Tutte le partite finalizzate, filtrabili per gioco, data e vincitore.</p>}
      </div>
      <div className="th-hright">
        <span className="th-qstat"><b>{TOTAL}</b> sessioni<span aria-hidden="true">·</span><b>{N_GAMES}</b> giochi<span aria-hidden="true">·</span><b>{WINNER_COUNTS.players.length}</b> vincitori</span>
        <button type="button" className="th-export"><span aria-hidden="true">📊</span>{mobile ? 'CSV' : 'Esporta CSV'}</button>
      </div>
    </div>
    <nav className="th-tabs" role="tablist" aria-label="Sezioni toolkit">
      {TABS.map(t => (
        <button key={t.id} type="button" role="tab" aria-selected={t.id === 'history'} className={'th-tab' + (t.id === 'history' ? ' on' : '')}>
          <span aria-hidden="true">{t.icon}</span>{t.label}
        </button>
      ))}
    </nav>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── HISTORY APP (interactive, one per state×viewport) ─
// ═══════════════════════════════════════════════════════
const DEFAULT_ST = { search: '', games: [], date: 'all', customFrom: '', customTo: '', winners: [], sort: 'recent', view: 'table', page: 1, pageSize: PAGE_SIZE };

const HistoryApp = ({ scenario, mobile }) => {
  const sc = scenario.sc || {};
  const [st, setSt] = useState({ ...DEFAULT_ST, ...sc, view: mobile ? 'cards' : (sc.view || 'table') });
  const [sortState, setSortState] = useState({ key: sc.sort || 'recent', dir: sc.sort === 'oldest' ? 'asc' : 'desc' });
  const [openSession, setOpenSession] = useState(sc.openId ? SESSIONS.find(s => s.id === sc.openId) : null);
  const [typing, setTyping] = useState(false);
  const searchRef = useRef(null);
  const firstRef = useRef(null);
  const typingTimer = useRef(null);
  const loading = sc.loading, error = sc.error, emptyAll = sc.empty;

  const set = patch => setSt(prev => ({ ...prev, ...patch }));

  // debounce visual
  useEffect(() => {
    if (!st.search) { setTyping(false); return; }
    setTyping(true);
    clearTimeout(typingTimer.current);
    typingTimer.current = setTimeout(() => setTyping(false), 650);
    return () => clearTimeout(typingTimer.current);
  }, [st.search]);

  // "/" focus search (desktop)
  useEffect(() => {
    if (mobile) return;
    const h = e => {
      if (e.key === '/' && document.activeElement.tagName !== 'INPUT' && document.activeElement.tagName !== 'TEXTAREA') {
        e.preventDefault(); searchRef.current && searchRef.current.focus();
      }
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [mobile]);

  const onSort = key => {
    setSortState(s => s.key === key ? { key, dir: s.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: key === 'recent' ? 'desc' : 'desc' });
    set({ sort: key });
  };

  // ── filter ──
  const filtered = useMemo(() => {
    const q = st.search.trim().toLowerCase();
    const cutoff = st.date === 'all' || st.date === 'custom' ? null : new Date(NOW.getTime() - Number(st.date) * 86400000);
    let rows = SESSIONS.filter(s => {
      if (st.games.length && !st.games.includes(s.gameId)) return false;
      if (cutoff && s.date < cutoff) return false;
      if (st.date === 'custom' && st.customFrom && st.customTo) {
        const from = new Date(st.customFrom + 'T00:00:00'), to = new Date(st.customTo + 'T23:59:59');
        if (s.date < from || s.date > to) return false;
      }
      if (st.winners.length) {
        const wantNoWin = st.winners.includes('__nowin__');
        const matchWin = s.winner && st.winners.includes(s.winner);
        if (!(matchWin || (wantNoWin && !s.winner))) return false;
      }
      if (q && !(s.gameName.toLowerCase().includes(q) || (s.winner && s.winner.toLowerCase().includes(q)) || s.players.some(p => p.name.toLowerCase().includes(q)))) return false;
      return true;
    });
    const dir = sortState.dir === 'asc' ? 1 : -1;
    rows = [...rows].sort((a, b) => {
      let r;
      if (sortState.key === 'game') r = a.gameName.localeCompare(b.gameName);
      else if (sortState.key === 'longest') r = a.durationMinutes - b.durationMinutes;
      else if (sortState.key === 'score') r = (a.winScore || 0) - (b.winScore || 0);
      else r = a.date - b.date; // recent/oldest
      return r * dir;
    });
    return rows;
  }, [st.search, st.games, st.date, st.customFrom, st.customTo, st.winners, sortState]);

  const total = filtered.length;
  const pages = Math.max(1, Math.ceil(total / st.pageSize));
  const page = Math.min(st.page, pages);
  const from = total ? (page - 1) * st.pageSize + 1 : 0;
  const to = Math.min(total, page * st.pageSize);
  const pageRows = filtered.slice((page - 1) * st.pageSize, page * st.pageSize);

  const nActive = (st.games.length ? 1 : 0) + (st.date !== 'all' ? 1 : 0) + (st.winners.length ? 1 : 0) + (st.search.trim() ? 1 : 0);
  const clearAll = () => set({ search: '', games: [], date: 'all', winners: [], page: 1 });

  let body;
  if (loading) body = <Skeleton mobile={mobile} />;
  else if (emptyAll) body = <EmptyAll />;
  else if (total === 0) body = <FilterEmpty onClear={clearAll} />;
  else if (mobile || st.view === 'cards') body = <Cards rows={pageRows} onOpen={setOpenSession} />;
  else body = <Table rows={pageRows} selectedId={openSession && openSession.id} onOpen={setOpenSession} sort={sortState} onSort={onSort} firstRef={firstRef} />;

  return (
    <div className={'th-app' + (mobile ? ' is-mobile' : '')}>
      {error && (
        <div className="th-errbar" role="alert">
          <span aria-hidden="true">⚠</span>Impossibile caricare lo storico — riprova.
          <span className="grow" />
          <button className="retry"><span aria-hidden="true">↻</span> Riprova</button>
        </div>
      )}
      <Header mobile={mobile} />
      {!error && !emptyAll && <Toolbar st={st} set={set} typing={typing} searchRef={searchRef} />}
      {!error && !emptyAll && !loading && nActive > 0 && (
        <div className="th-fsum">
          <span className="badge"><span aria-hidden="true">⚑</span>{nActive} {nActive === 1 ? 'filtro attivo' : 'filtri attivi'}</span>
          <span>·</span>
          <span>{total} {total === 1 ? 'sessione' : 'sessioni'} su {TOTAL}</span>
          <span className="grow" />
          <button className="clear" onClick={clearAll}>Cancella tutto</button>
        </div>
      )}
      <div className="th-body" aria-busy={loading ? 'true' : undefined}>{body}</div>
      {!error && !emptyAll && !loading && total > 0 && (
        <Pagination page={page} pages={pages} total={total} count={total} pageSize={st.pageSize} from={from} to={to} mobile={mobile}
          onPage={p => set({ page: p })} onSize={n => set({ pageSize: n, page: 1 })} />
      )}
      {openSession && <DetailModal s={openSession} mobile={mobile} onClose={() => setOpenSession(null)} />}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FRAMES (desktop chrome + phone) ────────────────
// ═══════════════════════════════════════════════════════
const DesktopFrame = ({ children }) => (
  <div style={{
    width: '100%', borderRadius: 'var(--r-xl)', border: '1px solid var(--border)',
    background: 'var(--bg-card)', overflow: 'hidden', boxShadow: 'var(--shadow-lg)',
  }}>
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8, padding: '9px 14px',
      background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)',
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
    }}>
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#ff5f57' }} />
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#febc2e' }} />
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#28c840' }} />
      <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>meepleai.app/toolkit/history</span>
    </div>
    <div style={{ height: 686 }}>{children}</div>
  </div>
);

const PhoneFrame = ({ children }) => (
  <div className="phone" style={{ width: 375, height: 760 }}>
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>14:32</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
    <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>{children}</div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATE PICKER + ROOT ────────────────────────────
// ═══════════════════════════════════════════════════════
const STATES = [
  { id: 'default',      label: 'Default',            view: 'both',    sc: {}, desc: 'Pagina 1 di 8 · 20 sessioni · nessun filtro attivo · ordinamento "più recente".' },
  { id: 'filter-game',  label: 'Filter · gioco',     view: 'desktop', sc: { games: ['g-catan'] }, desc: 'Game filter "Catan" attivo → badge "1 filtro attivo", solo sessioni Catan.' },
  { id: 'filter-multi', label: 'Filter · multi',     view: 'desktop', sc: { games: ['g-catan'], date: '30', winners: ['Marco R.'] }, desc: 'Game (Catan) + Data (ultimi 30gg) + Vincitore (Marco R.) → badge "3 filtri attivi · Cancella tutto".' },
  { id: 'filter-empty', label: 'Filter · 0 match',   view: 'desktop', sc: { search: 'Monopoly' }, desc: 'Ricerca senza corrispondenze → empty state filtrato con CTA "Rimuovi filtri".' },
  { id: 'pagination-3', label: 'Pagination · p3',    view: 'desktop', sc: { page: 3 }, desc: 'Su pagina 3 di 8: prev/next attivi, items 41–60 di 156, page button compatto con ellissi.' },
  { id: 'modal-detail', label: 'Modal detail',       view: 'desktop', sc: { openId: SESSIONS[0].id }, desc: 'Modal dettaglio Catan: classifica + timeline + note + statistiche partita, tabella dimmed dietro.' },
  { id: 'loading',      label: 'Loading',            view: 'desktop', sc: { loading: true }, desc: 'Skeleton shimmer: header + toolbar + tabella 6 righe.' },
  { id: 'error',        label: 'Error',              view: 'desktop', sc: { error: true }, desc: 'Banner danger in alto + Riprova; toolbar e body nascosti.' },
  { id: 'mobile-cards', label: 'Mobile · cards',     view: 'mobile',  sc: { view: 'cards' }, desc: 'Vista 375px: stack di cards, filtri con scroll orizzontale, modal come bottom-sheet.' },
];
const SKEY = 'th-state';

const VpLabel = ({ children }) => (
  <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700 }}>{children}</div>
);

const App = () => {
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || document.documentElement.getAttribute('data-theme') || 'light');
  const [active, setActive] = useState(() => {
    const s = localStorage.getItem(SKEY);
    return STATES.some(x => x.id === s) ? s : 'default';
  });
  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); localStorage.setItem('mai-theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem(SKEY, active); }, [active]);

  const cur = STATES.find(s => s.id === active) || STATES[0];

  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg)', color: 'var(--text)', padding: '20px 20px 80px' }}>
      <style dangerouslySetInnerHTML={{ __html: window.__TH_CSS }} />

      {/* state picker bar */}
      <header style={{
        position: 'sticky', top: 12, zIndex: 50, maxWidth: 1280, margin: '0 auto 24px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(16px)',
        border: '1px solid var(--border)', borderRadius: 'var(--r-xl)',
        boxShadow: 'var(--shadow-md)', padding: '12px 16px',
        display: 'flex', alignItems: 'center', gap: 14, flexWrap: 'wrap',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <div style={{
            width: 30, height: 30, borderRadius: 8, flexShrink: 0,
            background: `linear-gradient(135deg, ${eHsl('toolkit')}, ${eHsl('session')})`,
            color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontWeight: 800, fontFamily: 'var(--f-display)', fontSize: 14,
          }}>S</div>
          <div>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 14, lineHeight: 1.1 }}>Toolkit History</div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>#1490 · 2/4 · /toolkit/history</div>
          </div>
        </div>

        <div role="tablist" aria-label="Stati schermata" style={{ display: 'flex', gap: 6, flexWrap: 'wrap', flex: 1, minWidth: 0 }}>
          {STATES.map(s => {
            const on = s.id === active;
            return (
              <button key={s.id} type="button" role="tab" aria-selected={on} onClick={() => setActive(s.id)} style={{
                padding: '7px 12px', borderRadius: 'var(--r-pill)', cursor: 'pointer',
                background: on ? eHsl('toolkit') : 'var(--bg-muted)',
                border: on ? 'none' : '1px solid var(--border)',
                color: on ? '#fff' : 'var(--text-sec)',
                fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, whiteSpace: 'nowrap',
                boxShadow: on ? `0 3px 10px ${eHsl('toolkit', 0.35)}` : 'none',
              }}>{s.label}</button>
            );
          })}
        </div>

        <button type="button" onClick={() => setTheme(t => t === 'light' ? 'dark' : 'light')} style={{
          padding: '8px 14px', borderRadius: 'var(--r-md)', flexShrink: 0,
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          color: 'var(--text)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer',
        }}>🌗 {theme === 'light' ? 'Light' : 'Dark'}</button>
      </header>

      {/* active state description */}
      <div style={{ maxWidth: 1280, margin: '0 auto 18px', padding: '0 4px', fontFamily: 'var(--f-mono)', fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.5 }}>
        <strong style={{ color: eHsl('toolkit') }}>{cur.label}</strong> — {cur.desc}
      </div>

      {/* render area */}
      <div style={{ maxWidth: 1280, margin: '0 auto', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 36 }}>
        {(cur.view === 'both' || cur.view === 'desktop') && (
          <div style={{ width: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Desktop · 1440 — hero + table + pagination</VpLabel>
            <DesktopFrame>
              <HistoryApp key={'d-' + cur.id} scenario={cur} mobile={false} />
            </DesktopFrame>
          </div>
        )}
        {(cur.view === 'both' || cur.view === 'mobile') && (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Mobile · 375 — cards stack + bottom-sheet</VpLabel>
            <PhoneFrame>
              <HistoryApp key={'m-' + cur.id} scenario={cur} mobile={true} />
            </PhoneFrame>
          </div>
        )}
      </div>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
