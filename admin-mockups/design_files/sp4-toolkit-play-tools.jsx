/* sp4-toolkit-play-tools.jsx — i 4 tool widget (dice/counter/timer/randomizer) + log panel.
   Loads after sp4-toolkit-play.jsx, before sp4-toolkit-play-ui.jsx. Route: /toolkit/play. */

const { useState, useEffect, useRef, useCallback, useMemo } = React;
const TP = window.__TP;
const { fmtClock, PAD, reducedMotion, TOOL_ENT, entE, DICE, TIMERS, TIMER_PRESETS, RANDOM_ITEMS, ACTORS, mkActor, LOG_FILTERS } = TP;

const rnd = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;
const rollDice = (count, sides) => { let s = 0; for (let i = 0; i < count; i++) s += rnd(1, sides); return s; };

// ═══════════════════════════════════════════════════════
// ─── COUNTER CARD ───────────────────────────────────
// ═══════════════════════════════════════════════════════
const CounterCard = ({ initialValue = 0, initialName = 'Punti', addLog, autoBump }) => {
  const [value, setValue] = useState(initialValue);
  const [name, setName] = useState(initialName);
  const [editing, setEditing] = useState(false);
  const [pop, setPop] = useState(false);
  const [hot, setHot] = useState(null);       // '+' | '-' transient highlight
  const [confirm, setConfirm] = useState(false);
  const lp = useRef(null);
  const popT = useRef(null), hotT = useRef(null);

  const flash = dir => {
    setPop(true); setHot(dir);
    clearTimeout(popT.current); popT.current = setTimeout(() => setPop(false), 430);
    clearTimeout(hotT.current); hotT.current = setTimeout(() => setHot(null), 360);
  };
  const bump = delta => {
    setValue(v => {
      const nv = v + delta;
      addLog({ toolType: 'counter', action: delta > 0 ? 'increment' : 'decrement', result: `${name} ${delta > 0 ? '+1' : '−1'} = ${nv}` });
      return nv;
    });
    flash(delta > 0 ? '+' : '-');
  };
  useEffect(() => {
    if (autoBump) { const t = setTimeout(() => bump(1), 520); return () => clearTimeout(t); }
  }, [autoBump]); // eslint-disable-line
  useEffect(() => () => { clearTimeout(popT.current); clearTimeout(hotT.current); clearTimeout(lp.current); }, []);

  const doReset = () => { setValue(0); setConfirm(false); addLog({ toolType: 'counter', action: 'reset', result: `${name} azzerato` }); };

  return (
    <div className="tp-counter" role="group" aria-labelledby={`cnt-${initialName}`}>
      {confirm && (
        <div className="tp-cconfirm">
          <div className="q">Azzerare “{name}”?</div>
          <div className="row">
            <button type="button" onClick={() => setConfirm(false)}>Annulla</button>
            <button type="button" className="warn" onClick={doReset}>Azzera</button>
          </div>
        </div>
      )}
      <div className="tp-ctop">
        {editing
          ? <input className="tp-cname-input" autoFocus value={name} aria-label="Nome contatore"
              onChange={e => setName(e.target.value)} onBlur={() => setEditing(false)}
              onKeyDown={e => { if (e.key === 'Enter' || e.key === 'Escape') setEditing(false); }} />
          : <button type="button" className="tp-cname" id={`cnt-${initialName}`} onClick={() => setEditing(true)} title="Clicca per rinominare">{name}</button>}
        <button type="button" className="tp-creset" onClick={() => setConfirm(true)} aria-label={`Azzera contatore ${name}`}>
          <span aria-hidden="true">↻</span> reset
        </button>
      </div>
      <div className="tp-cbody">
        <button type="button" className={'tp-cbtn' + (hot === '-' ? ' hot' : '')} onClick={() => bump(-1)} aria-label={`Decrementa contatore ${name}`}>−</button>
        <span className={'tp-cval' + (pop ? ' pop' : '') + (hot ? ' up' : '')} aria-live="polite">{value}</span>
        <button type="button" className={'tp-cbtn' + (hot === '+' ? ' hot' : '')} onClick={() => bump(1)} aria-label={`Incrementa contatore ${name}`}>+</button>
      </div>
      <div className="tp-chint">+ / − per modificare · ↻ per azzerare</div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TIMER CARD ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const TimerCard = ({ timer, addLog, initStatus = 'idle', initSeconds, actor }) => {
  const base = initSeconds != null ? initSeconds : timer.defaultSeconds;
  const [preset, setPreset] = useState(timer.defaultSeconds);
  const [seconds, setSeconds] = useState(base);
  const [status, setStatus] = useState(initStatus); // idle | running | paused | expired
  const tickRef = useRef(null);
  const startTotal = useRef(initStatus === 'running' || initStatus === 'paused' ? Math.max(base, preset) : preset);

  const total = startTotal.current || preset || 1;
  const finishing = status === 'running' && seconds <= 10 && seconds > 0;
  const pct = Math.max(0, Math.min(100, (seconds / total) * 100));

  useEffect(() => {
    if (status !== 'running') { clearInterval(tickRef.current); return; }
    tickRef.current = setInterval(() => {
      setSeconds(s => {
        if (s <= 1) {
          clearInterval(tickRef.current);
          setStatus('expired');
          addLog({ toolType: 'timer', action: 'stop', result: `${timer.name} scaduto` });
          return 0;
        }
        return s - 1;
      });
    }, 1000);
    return () => clearInterval(tickRef.current);
  }, [status]); // eslint-disable-line

  const start = () => { startTotal.current = preset; setSeconds(preset); setStatus('running'); addLog({ toolType: 'timer', action: 'start', result: `${timer.name} avviato (${preset}s)` }); };
  const resume = () => { setStatus('running'); };
  const pause = () => { setStatus('paused'); addLog({ toolType: 'timer', action: 'stop', result: `${timer.name} in pausa (${fmtClock(seconds)})` }); };
  const reset = () => { clearInterval(tickRef.current); setStatus('idle'); setSeconds(preset); startTotal.current = preset; };

  const cls = 'tp-timer ' + status + (finishing ? ' finishing' : '');
  const stateLbl = { idle: 'pronto', running: 'in corso', paused: 'pausa', expired: 'scaduto' }[status];
  const ac = mkActor(actor);

  return (
    <div className={cls} role="group" aria-labelledby={`tm-${timer.id}`}>
      <div className="tlabel" id={`tm-${timer.id}`}>
        <span className="tg" aria-hidden="true">{timer.icon}</span><span className="tnm">{timer.name}</span>
        {ac && status !== 'idle' && (
          <span className="tp-actorchip tp-tactor" title={`Turno di ${ac.name}`}>
            <span className="av" style={{ background: `hsl(${ac.color},58%,52%)` }} aria-hidden="true">{ac.initials}</span>
            <span className="nm">{ac.name}</span>
          </span>
        )}
        <span className="tstate">{stateLbl}</span>
      </div>
      <div className="tp-timer-display">
        <span className="tp-clock" role="timer" aria-live={status === 'running' ? 'off' : 'polite'}>{fmtClock(seconds)}</span>
        <div className="tp-progress"><div className="fill" style={{ width: pct + '%' }} /></div>
        <span className="tp-progress-lbl">{status === 'expired' ? 'tempo scaduto' : `${Math.round(pct)}% rimanente`}</span>
      </div>
      <div className="tp-timer-foot">
        {status === 'idle' && (
          <span className="tp-tselect">
            <select value={preset} aria-label="Durata timer" onChange={e => { const v = +e.target.value; setPreset(v); setSeconds(v); startTotal.current = v; }}>
              {TIMER_PRESETS.map(p => <option key={p} value={p}>{p}s</option>)}
            </select>
          </span>
        )}
        {status === 'idle' && <button type="button" className="tp-tbtn primary" onClick={start}><span aria-hidden="true">▶</span> Avvia</button>}
        {status === 'running' && <button type="button" className="tp-tbtn primary" onClick={pause}><span aria-hidden="true">⏸</span> Pausa</button>}
        {status === 'paused' && <button type="button" className="tp-tbtn primary" onClick={resume}><span aria-hidden="true">▶</span> Riprendi</button>}
        {status === 'expired' && <button type="button" className="tp-tbtn primary danger" onClick={reset}><span aria-hidden="true">↻</span> Reset</button>}
        {(status === 'running' || status === 'paused') && <button type="button" className="tp-tbtn ghost" onClick={reset}><span aria-hidden="true">↻</span> Reset</button>}
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── RANDOMIZER CARD ────────────────────────────────
// ═══════════════════════════════════════════════════════
const RandomizerCard = ({ addLog, autoPick }) => {
  const [items, setItems] = useState([...RANDOM_ITEMS]);
  const [adding, setAdding] = useState('');
  const [phase, setPhase] = useState('idle'); // idle | picking | picked
  const [cycleIdx, setCycleIdx] = useState(-1);
  const [winner, setWinner] = useState(null);
  const [pop, setPop] = useState(false);
  const timers = useRef([]);
  const empty = items.length === 0;

  const clearAll = () => { timers.current.forEach(clearTimeout); timers.current = []; };
  useEffect(() => () => clearAll(), []);

  const pick = useCallback(() => {
    if (empty || phase === 'picking') return;
    const finalIdx = rnd(0, items.length - 1);
    const finalName = items[finalIdx];
    if (reducedMotion()) {
      setWinner(finalName); setPhase('picked'); setPop(true);
      setTimeout(() => setPop(false), 500);
      addLog({ toolType: 'random', action: 'pick', result: `Random pick: ${finalName}` });
      return;
    }
    setPhase('picking'); setWinner(null);
    clearAll();
    // decaying cycle: fast → slow, settling on finalIdx
    let t = 0, delay = 80, idx = 0;
    const steps = 16 + finalIdx;
    for (let i = 0; i < steps; i++) {
      const cur = i % items.length;
      timers.current.push(setTimeout(() => setCycleIdx(cur), t));
      t += delay;
      delay += i > steps - 7 ? 55 : 8; // decay near end
    }
    timers.current.push(setTimeout(() => {
      setCycleIdx(finalIdx); setWinner(finalName); setPhase('picked'); setPop(true);
      addLog({ toolType: 'random', action: 'pick', result: `Random pick: ${finalName}` });
      timers.current.push(setTimeout(() => setPop(false), 500));
      timers.current.push(setTimeout(() => setCycleIdx(-1), 900));
    }, t + 60));
  }, [empty, phase, items, addLog]);

  useEffect(() => {
    if (autoPick) { const t = setTimeout(pick, 480); return () => clearTimeout(t); }
  }, [autoPick]); // eslint-disable-line

  const add = () => { const v = adding.trim(); if (!v) return; setItems(prev => [...prev, v]); setAdding(''); };
  const remove = i => setItems(prev => prev.filter((_, j) => j !== i));

  return (
    <div className="tp-rand" role="group" aria-labelledby="rand-title">
      <div className="tp-rand-list">
        <div className="lh" id="rand-title">Lista items · {items.length}</div>
        {items.map((it, i) => (
          <div key={i} className={'tp-rand-item' + (phase === 'picking' && cycleIdx === i ? ' cycling' : '') + (phase === 'picked' && winner === it && cycleIdx === i ? ' winner' : '')}>
            <span className="dot" aria-hidden="true" />
            <span className="txt">{it}</span>
            <button type="button" className="rm" onClick={() => remove(i)} aria-label={`Rimuovi ${it}`}>✕</button>
          </div>
        ))}
        <div className="tp-rand-add">
          <span aria-hidden="true" style={{ color: 'var(--text-muted)' }}>＋</span>
          <input value={adding} placeholder="Aggiungi item…" aria-label="Aggiungi item alla lista"
            onChange={e => setAdding(e.target.value)} onKeyDown={e => { if (e.key === 'Enter') add(); }} />
        </div>
      </div>
      <div className="tp-rand-right">
        <div className={'tp-rand-result' + (winner ? ' has' : '')} aria-live="polite">
          {winner
            ? <React.Fragment><span className={'big' + (pop ? ' pop' : '')}>{winner} 🎉</span><span className="meta">estratto ora</span></React.Fragment>
            : <span className="ph">{empty ? 'lista vuota' : 'pronto a estrarre'}</span>}
        </div>
        <button type="button" className={'tp-rand-btn' + (empty ? ' disabled' : '')} disabled={empty} onClick={pick} aria-label="Estrai un item a caso">
          <span aria-hidden="true">🎲</span>{phase === 'picking' ? 'Estraendo…' : 'Estrai!'}
        </button>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── LOG PANEL ──────────────────────────────────────
// ═══════════════════════════════════════════════════════
const LogEntry = ({ e }) => {
  const meta = TOOL_ENT[e.toolType] || TOOL_ENT.dice;
  const ac = mkActor(e.actorLabel);
  return (
    <div className={'tp-logentry' + (e.fresh ? ' fresh' : '')} style={{ '--e': entE(meta.ent) }} role="listitem">
      <span className="lic" aria-hidden="true">{meta.icon}</span>
      <div className="lbody">
        <div className="lres">{e.result}</div>
        <div className="lmeta">
          <span className="ltime">{e.time}</span>
          {ac && (
            <span className="lchip" title={ac.name}>
              <span className="av" style={{ background: `hsl(${ac.color},58%,52%)` }} aria-hidden="true">{ac.initials}</span>
              <span className="nm">{ac.name}</span>
            </span>
          )}
        </div>
      </div>
    </div>
  );
};

const LogPanel = ({ log, filter, setFilter, sortNewestFirst, setSort, onClear, mobile }) => {
  const [accOpen, setAccOpen] = useState(true);
  const [showAll, setShowAll] = useState(false);
  const filtered = useMemo(() => filter === 'all' ? log : log.filter(e => e.toolType === filter), [log, filter]);
  const ordered = sortNewestFirst ? filtered : [...filtered].reverse();
  const COLLAPSE = 20;
  const visible = showAll ? ordered : ordered.slice(0, COLLAPSE);
  const hidden = ordered.length - visible.length;

  return (
    <aside className={'tp-logcol' + (mobile ? (accOpen ? ' open' : '') : '')} aria-label="Log eventi">
      {mobile && (
        <button type="button" className={'tp-logacc-btn' + (accOpen ? ' open' : '')} aria-expanded={accOpen} onClick={() => setAccOpen(o => !o)}>
          <span className="li" aria-hidden="true">📋</span>Log eventi
          <span className="grow" /><span className="badge">{log.length}</span>
          <span className="chev" aria-hidden="true">▾</span>
        </button>
      )}
      <div className="tp-loghead">
        <div className="tp-logtop">
          <span className="li" aria-hidden="true">📋</span>
          <div>
            <div className="lt">Log eventi</div>
            <div className="lc">Ultime {Math.max(log.length, 0)} azioni</div>
          </div>
          <span className="grow" />
          <button type="button" className="tp-logclear" onClick={onClear} disabled={log.length === 0}>
            <span aria-hidden="true">🗑</span> Pulisci
          </button>
        </div>
        <div className="tp-logfilters" role="radiogroup" aria-label="Filtra log per tipo">
          {LOG_FILTERS.map(f => (
            <button key={f.id} type="button" role="radio" aria-checked={filter === f.id}
              className={'tp-lfchip' + (filter === f.id ? ' on' : '')} onClick={() => setFilter(f.id)}>
              <span aria-hidden="true">{f.icon}</span>{f.label}
            </button>
          ))}
        </div>
        <div className="tp-logsort">
          <span className="cnt">{filtered.length} {filtered.length === 1 ? 'evento' : 'eventi'}{filter !== 'all' ? ' filtrati' : ''}</span>
          <button type="button" className="tp-sortbtn" onClick={() => setSort(v => !v)} aria-label="Cambia ordinamento log">
            <span aria-hidden="true">{sortNewestFirst ? '⬆' : '⬇'}</span>{sortNewestFirst ? 'Recenti in alto' : 'Recenti in basso'}
          </button>
        </div>
      </div>
      {filtered.length === 0
        ? <div className="tp-logempty">
            <span className="em" aria-hidden="true">📋</span>
            <p>{filter === 'all' ? 'Nessuna azione ancora — interagisci con un tool per popolare il log.' : 'Nessun evento per questo filtro.'}</p>
          </div>
        : <div className="tp-logbody" role="log" aria-live="polite" aria-relevant="additions" aria-label="Eventi recenti">
            {visible.map(e => <LogEntry key={e.id} e={e} />)}
            {hidden > 0 && <button type="button" className="tp-logmore" onClick={() => setShowAll(true)}>Vedi precedenti ({hidden})</button>}
          </div>}
    </aside>
  );
};

Object.assign(window, { CounterCard, TimerCard, RandomizerCard, LogPanel, LogEntry });
