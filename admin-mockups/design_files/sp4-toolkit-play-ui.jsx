/* sp4-toolkit-play-ui.jsx — header + tabs, actor input, PlayApp orchestrator, frames, state picker.
   Loads after sp4-toolkit-play.jsx + sp4-toolkit-play-tools.jsx. Route: /toolkit/play. */

const { useState, useEffect, useMemo, useRef, useCallback } = React;
const TP = window.__TP;
const { eHsl, fmtClock, PAD, TOOL_ENT, entE, DICE, TIMERS, COUNTER_INIT, RANDOM_ITEMS, ACTORS, mkActor, LOG_FILTERS, MOCK_LOG } = TP;

const nowClock = () => { const d = new Date(); return `${PAD(d.getHours())}:${PAD(d.getMinutes())}`; };

// ═══════════════════════════════════════════════════════
// ─── HEADER (sticky + tabs) ─────────────────────────
// ═══════════════════════════════════════════════════════
const TABS = [
  { id: 'stats',     icon: '📊', label: 'Stats',     href: 'sp4-toolkit-stats.html' },
  { id: 'history',   icon: '📜', label: 'History',   href: 'sp4-toolkit-history.html' },
  { id: 'templates', icon: '🎨', label: 'Templates', href: 'sp4-toolkit-templates.html' },
  { id: 'play',      icon: '🎮', label: 'Play',      href: null },
];

const ActorInput = ({ actor, setActor, mobile }) => {
  const [draft, setDraft] = useState('');
  const commit = () => { const v = draft.trim().slice(0, 30); if (v) { setActor(v); setDraft(''); } };
  const ac = mkActor(actor);
  return (
    <div className="tp-actor">
      {actor
        ? <span className="tp-actorchip" title={`Giocatore corrente: ${ac.name}`}>
            <span className="av" style={{ background: `hsl(${ac.color},58%,52%)` }} aria-hidden="true">{ac.initials}</span>
            <span className="nm">{ac.name}</span>
            <button type="button" className="x" onClick={() => setActor('')} aria-label="Rimuovi giocatore corrente">✕</button>
          </span>
        : <span className="tp-actor-field">
            <span className="ic" aria-hidden="true">👤</span>
            <input value={draft} maxLength={30} placeholder={mobile ? 'Chi gioca?' : 'Chi gioca? (opzionale)'}
              aria-label="Etichetta giocatore corrente (opzionale)"
              onChange={e => setDraft(e.target.value)} onBlur={commit}
              onKeyDown={e => { if (e.key === 'Enter') commit(); }} />
          </span>}
    </div>
  );
};

const Header = ({ mobile, actor, setActor }) => (
  <header className="tp-head">
    <div className="tp-htop">
      <div className="tp-htxt">
        <div className="tp-bread"><span>Toolkit</span><span className="sep" aria-hidden="true">›</span><span className="cur">Play</span></div>
        <div className="tp-titlerow">
          <span className="tp-ico" aria-hidden="true">🎮</span>
          <h1 className="tp-h1">Toolkit play{!mobile && ' · Strumenti partita'}</h1>
        </div>
        {!mobile && <p className="tp-sub">Helper standalone per dadi, contatori, timer e randomizer durante le partite fisiche.</p>}
      </div>
      <div className="tp-hright">
        <ActorInput actor={actor} setActor={setActor} mobile={mobile} />
        <button type="button" className="tp-cfg"><span aria-hidden="true">🔧</span>{mobile ? 'Configura' : 'Configura toolkit'}</button>
      </div>
    </div>
    <nav className="tp-tabs" role="tablist" aria-label="Sezioni toolkit">
      {TABS.map(t => {
        const on = t.id === 'play';
        const cls = 'tp-tab' + (on ? ' on' : '');
        const inner = <React.Fragment><span aria-hidden="true">{t.icon}</span>{t.label}</React.Fragment>;
        return t.href
          ? <a key={t.id} href={t.href} className={cls} role="tab" aria-selected={false}>{inner}</a>
          : <button key={t.id} type="button" role="tab" aria-selected={on} className={cls}>{inner}</button>;
      })}
    </nav>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── TOOL SECTIONS ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const SectionHead = ({ ent, icon, title, sub, children }) => (
  <div className="tp-shead" style={{ '--e': entE(ent) }}>
    <div className="tp-shead-main">
      <span className="tp-sicon" aria-hidden="true">{icon}</span>
      <div className="tp-stext">
        <div className="tp-stitle">{title}</div>
        <div className="tp-ssub">{sub}</div>
      </div>
    </div>
    {children}
  </div>
);

const DiceSection = ({ addLog, actor, dice, onJumpLog }) => (
  <section className="tp-section" style={{ '--e': entE('game') }} role="group" aria-label="Dadi — dice builder">
    <SectionHead ent="game" icon="🎲" title="Dadi" sub="Configura tipo, quantità, modificatori — o usa la formula avanzata" />
    <DiceBuilder addLog={addLog} actor={actor} dice={dice || {}} />
  </section>
);

const CounterSection = ({ addLog, firstValue, autoBump }) => {
  const [counters, setCounters] = useState([{ key: 'c0', name: COUNTER_INIT.name, value: firstValue }]);
  const add = () => setCounters(prev => [...prev, { key: 'c' + Date.now(), name: 'Contatore ' + (prev.length + 1), value: 0 }]);
  return (
    <section className="tp-section" style={{ '--e': entE('toolkit') }} role="group" aria-labelledby="sec-counter">
      <SectionHead ent="toolkit" icon="🔢" title="Contatori" sub={`${counters.length} ${counters.length === 1 ? 'contatore' : 'contatori'} · click +/− per modificare`}>
        <button type="button" className="tp-saction" onClick={add} id="sec-counter"><span aria-hidden="true">＋</span>Nuovo contatore</button>
      </SectionHead>
      <div className="tp-counter-grid">
        {counters.map((c, i) => (
          <CounterCard key={c.key} initialName={c.name} initialValue={c.value} addLog={addLog} autoBump={i === 0 && autoBump} />
        ))}
      </div>
    </section>
  );
};

const TimerSection = ({ addLog, timerStates, actor }) => (
  <section className="tp-section" style={{ '--e': entE('warning') }} role="group" aria-labelledby="sec-timer">
    <SectionHead ent="warning" icon="⏱" title="Timer" sub="Timer countdown + Timer turno" />
    <div className="tp-timer-grid">
      {TIMERS.map(t => {
        const sc = (timerStates || {})[t.id] || {};
        return <TimerCard key={t.id} timer={t} addLog={addLog} initStatus={sc.status || 'idle'} initSeconds={sc.seconds} actor={actor} />;
      })}
    </div>
  </section>
);

const RandomizerSection = ({ addLog, autoPick }) => (
  <section className="tp-section" style={{ '--e': entE('event') }} role="group" aria-labelledby="sec-rand">
    <SectionHead ent="event" icon="🎰" title="Randomizer" sub="Estrai un item a caso da una lista" />
    <RandomizerCard addLog={addLog} autoPick={autoPick} />
  </section>
);

// ═══════════════════════════════════════════════════════
// ─── CLEAR-LOG MODAL ────────────────────────────────
// ═══════════════════════════════════════════════════════
const ClearModal = ({ count, onCancel, onConfirm, mobile }) => {
  const ref = useRef(null);
  useEffect(() => { ref.current && ref.current.focus(); }, []);
  useEffect(() => {
    const onKey = e => { if (e.key === 'Escape') onCancel(); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [onCancel]);
  return (
    <div className="tp-overlay" onMouseDown={e => { if (e.target === e.currentTarget) onCancel(); }}>
      <div className="tp-modal" role="alertdialog" aria-modal="true" aria-labelledby="tp-clear-t" aria-describedby="tp-clear-d">
        <div className="tp-mhead"><span className="mi" aria-hidden="true">🗑</span><div className="mt" id="tp-clear-t">Cancella tutto il log?</div></div>
        <div className="tp-mbody"><p id="tp-clear-d">Stai per rimuovere <span className="cnt">{count} {count === 1 ? 'evento' : 'eventi'}</span> dal log. L’azione non è reversibile.</p></div>
        <div className="tp-mfoot">
          <button type="button" className="tp-mbtn" ref={ref} onClick={onCancel}>Annulla</button>
          <button type="button" className="tp-mbtn warn" onClick={onConfirm}><span aria-hidden="true">🗑</span>Cancella tutto</button>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── PLAY APP (interactive, one per state×viewport) ─────
// ═══════════════════════════════════════════════════════
const PlayApp = ({ scenario, mobile }) => {
  const sc = scenario.sc || {};
  const initLog = useMemo(() => {
    let base = sc.logMock === false ? [] : MOCK_LOG.map(e => ({ ...e }));
    if (sc.extraLog) base = [...sc.extraLog, ...base];
    return base;
  }, [scenario.id]); // eslint-disable-line

  const [log, setLog] = useState(initLog);
  const [actor, setActor] = useState(sc.actor || '');
  const [filter, setFilter] = useState(sc.filter || 'all');
  const [sortNewestFirst, setSort] = useState(true);
  const [clearOpen, setClearOpen] = useState(!!sc.clearModal);
  const idRef = useRef(1000);
  const actorRef = useRef(actor);
  useEffect(() => { actorRef.current = actor; }, [actor]);

  const addLog = useCallback(entry => {
    idRef.current += 1;
    const e = { id: 'lg-' + idRef.current, time: nowClock(), actorLabel: actorRef.current || null, fresh: true, ...entry };
    setLog(prev => [e, ...prev].slice(0, 50));
  }, []);

  const jumpLog = useCallback(() => setFilter('dice'), []);

  // keyboard: "/" focus first log filter chip
  useEffect(() => {
    if (mobile) return;
    const h = e => {
      const tag = document.activeElement.tagName;
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;
      if (e.key === '/') { e.preventDefault(); const c = document.querySelector('.tp-logfilters .tp-lfchip'); c && c.focus(); }
    };
    window.addEventListener('keydown', h);
    return () => window.removeEventListener('keydown', h);
  }, [mobile]);

  return (
    <div className={'tp-app' + (mobile ? ' is-mobile' : '')}>
      <Header mobile={mobile} actor={actor} setActor={setActor} />
      <div className="tp-layout">
        <div className="tp-toolcol">
          <DiceSection addLog={addLog} actor={actor} dice={sc.dice} onJumpLog={jumpLog} />
          <CounterSection addLog={addLog} firstValue={sc.counterValue != null ? sc.counterValue : COUNTER_INIT.value} autoBump={sc.autoBump} />
          <TimerSection addLog={addLog} timerStates={sc.timers} actor={actor} />
          <RandomizerSection addLog={addLog} autoPick={sc.autoPick} />
        </div>
        <LogPanel log={log} filter={filter} setFilter={setFilter} sortNewestFirst={sortNewestFirst} setSort={setSort}
          onClear={() => setClearOpen(true)} mobile={mobile} />
      </div>
      {clearOpen && <ClearModal count={log.length} mobile={mobile} onCancel={() => setClearOpen(false)} onConfirm={() => { setLog([]); setClearOpen(false); }} />}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FRAMES ─────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopFrame = ({ width = '100%', height = 700, children }) => (
  <div style={{ width, borderRadius: 'var(--r-xl)', border: '1px solid var(--border)', background: 'var(--bg-card)', overflow: 'hidden', boxShadow: 'var(--shadow-lg)' }}>
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '9px 14px', background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)', fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)' }}>
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#ff5f57' }} />
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#febc2e' }} />
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: '#28c840' }} />
      <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>meepleai.app/toolkit/play</span>
    </div>
    <div style={{ height }}>{children}</div>
  </div>
);

const PhoneFrame = ({ children }) => (
  <div className="phone" style={{ width: 375, height: 760 }}>
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>16:43</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
    <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>{children}</div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATE PICKER + ROOT ────────────────────────────
// ═══════════════════════════════════════════════════════
const STATES = [
  { id: 'default',              label: 'Default',            view: 'desktop', sc: { logMock: true }, desc: 'Toolkit completo: 4 sezioni (dadi · contatori · timer · randomizer) + log con 5 eventi mock. Tutti i tool interattivi.' },
  { id: 'idle-empty-log',       label: 'Log vuoto',          view: 'desktop', sc: { logMock: false }, desc: 'Utente appena entrato: tool pronti ma log vuoto con empty state “interagisci con un tool per popolare”.' },
  { id: 'dice-idle',            label: 'Dice · idle',        view: 'desktop', sc: { logMock: true, dice: { history: [], showHistory: true } }, desc: 'Dice builder default (1D6), area risultato placeholder “Configura e tira”, storico vuoto.' },
  { id: 'dice-building',        label: 'Dice · building',    view: 'desktop', sc: { logMock: true, dice: { init: { count: 4, sides: 6, kh: 3, mod: 2 }, advOpen: true } }, desc: 'Builder mid-config: 4×D6, keep highest 3, +2. Modificatori avanzati aperti, label CTA “Tira 4D6kh3+2” aggiornata live.' },
  { id: 'dice-preset-applied',  label: 'Dice · preset',      view: 'desktop', sc: { logMock: true, dice: { init: { count: 6, sides: 6, cs: 6 }, pulse: '6D6cs6' } }, desc: 'Preset “6D6cs6” cliccato: builder autofill (count successi ≥6) + pulse animation 220ms sulla chip.' },
  { id: 'dice-formula-input',   label: 'Dice · formula',     view: 'desktop', sc: { logMock: true, dice: { fmOpen: true, fmText: '4D6kh3+2', showSyntax: true } }, desc: 'Modalità formula aperta con “4D6kh3+2”: feedback live ✓ Valido + interpretazione human-readable, pannello sintassi.' },
  { id: 'dice-rolling',         label: 'Dice · rolling',     view: 'desktop', sc: { logMock: true, dice: { init: { count: 4, sides: 6, kh: 3, mod: 2 }, autoRoll: true } }, desc: 'Animazione roll in corso: dadi che ruotano (cycle 600ms) prima del settle, CTA disabilitato durante il tiro.' },
  { id: 'dice-result-shown',    label: 'Dice · result',      view: 'desktop', sc: { logMock: true, dice: { init: { count: 4, sides: 6, kh: 3, mod: 2 }, seedResult: true, showHistory: true } }, desc: 'Risultato visibile: dadi individuali kept/dropped (strike), calcolo inline, totale con pop animation, nuova entry in cima allo storico.' },
  { id: 'dice-formula-invalid', label: 'Dice · invalid',     view: 'desktop', sc: { logMock: true, dice: { fmOpen: true, fmText: '4D6kx' } }, desc: 'Formula parser con errore: feedback ✗ “Sintassi non riconosciuta”, input border --c-danger, CTA “Tira formula” disabilitato.' },
  { id: 'timer-running',        label: 'Timer running',      view: 'desktop', sc: { logMock: true, actor: 'Marco R.', timers: { 'tm-count': { status: 'running', seconds: 94 } } }, desc: 'Timer countdown live (01:34 → 01:32 → 01:30…), progress bar decrementa, attore corrente “Marco R.” visibile.' },
  { id: 'timer-expired',        label: 'Timer expired',      view: 'desktop', sc: { logMock: true, timers: { 'tm-count': { status: 'expired', seconds: 0 } }, extraLog: [{ id: 'lg-exp', time: '16:43', toolType: 'timer', action: 'stop', result: 'Timer countdown scaduto', actorLabel: null }] }, desc: 'Timer a 00:00 con flash --c-danger, badge “scaduto”, action [↻ Reset], log entry “Timer scaduto”.' },
  { id: 'counter-incrementing', label: 'Counter +1',         view: 'desktop', sc: { logMock: true, counterValue: 4, autoBump: true }, desc: 'Contatore “Punti” 4 → 5 con animazione pop, button [+] evidenziato, log entry “Punti +1 = 5”.' },
  { id: 'randomizer-picking',   label: 'Randomizer pick',    view: 'desktop', sc: { logMock: true, autoPick: true }, desc: 'Randomizer in picking: items che ciclano con highlight, decay lento, result pop “Sushi 🎉” + log entry.' },
  { id: 'filter-log-dice',      label: 'Log filter · Dadi',  view: 'desktop', sc: { logMock: true, filter: 'dice' }, desc: 'Log panel con filtro “Dadi” attivo (single-select): solo gli eventi dadi visibili, contatore filtrati.' },
  { id: 'clear-log-confirm',    label: 'Clear log',          view: 'desktop', sc: { logMock: true, clearModal: true }, desc: 'Modal alertdialog “Cancella tutto il log?” --c-warning con conteggio eventi e conferma distruttiva.' },
  { id: 'mobile-stack',         label: 'Mobile · stack',     view: 'mobile',  sc: { logMock: true }, desc: 'Viewport 375px: tool in stack verticale, dadi 2-col, log panel come accordion in fondo, modal come bottom-sheet.' },
];
const SKEY = 'tp-state';

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
      <style dangerouslySetInnerHTML={{ __html: window.__TP_CSS }} />

      {/* state picker bar */}
      <header style={{
        position: 'sticky', top: 12, zIndex: 50, maxWidth: 1320, margin: '0 auto 24px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(16px)', border: '1px solid var(--border)',
        borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-md)', padding: '12px 16px',
        display: 'flex', alignItems: 'center', gap: 14, flexWrap: 'wrap',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <div style={{ width: 30, height: 30, borderRadius: 8, flexShrink: 0, background: `linear-gradient(135deg, ${eHsl('toolkit')}, ${eHsl('game')})`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 800, fontFamily: 'var(--f-display)', fontSize: 14 }}>S</div>
          <div>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 14, lineHeight: 1.1 }}>Toolkit Play</div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>#1490 · 4/4 · /toolkit/play</div>
          </div>
        </div>

        <div role="tablist" aria-label="Stati schermata" style={{ display: 'flex', gap: 6, flexWrap: 'wrap', flex: 1, minWidth: 0 }}>
          {STATES.map(s => {
            const on = s.id === active;
            return (
              <button key={s.id} type="button" role="tab" aria-selected={on} onClick={() => setActive(s.id)} style={{
                padding: '7px 12px', borderRadius: 'var(--r-pill)', cursor: 'pointer',
                background: on ? eHsl('toolkit') : 'var(--bg-muted)', border: on ? 'none' : '1px solid var(--border)',
                color: on ? '#fff' : 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, whiteSpace: 'nowrap',
                boxShadow: on ? `0 3px 10px ${eHsl('toolkit', 0.35)}` : 'none',
              }}>{s.label}</button>
            );
          })}
        </div>

        <button type="button" onClick={() => setTheme(t => t === 'light' ? 'dark' : 'light')} style={{
          padding: '8px 14px', borderRadius: 'var(--r-md)', flexShrink: 0, background: 'var(--bg-card)', border: '1px solid var(--border)',
          color: 'var(--text)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer',
        }}>🌗 {theme === 'light' ? 'Light' : 'Dark'}</button>
      </header>

      {/* active state description */}
      <div style={{ maxWidth: 1320, margin: '0 auto 18px', padding: '0 4px', fontFamily: 'var(--f-mono)', fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.5 }}>
        <strong style={{ color: eHsl('toolkit') }}>{cur.label}</strong> — {cur.desc}
      </div>

      {/* render area */}
      <div style={{ maxWidth: 1320, margin: '0 auto', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 36 }}>
        {cur.view === 'desktop' && (
          <div style={{ width: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Desktop · 1440 — hero + 2-col tool/log (65/35)</VpLabel>
            <DesktopFrame><PlayApp key={'d-' + cur.id} scenario={cur} mobile={false} /></DesktopFrame>
          </div>
        )}
        {cur.view === 'mobile' && (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <VpLabel>Mobile · 375 — stack 1-col + log accordion</VpLabel>
            <PhoneFrame><PlayApp key={'m-' + cur.id} scenario={cur} mobile={true} /></PhoneFrame>
          </div>
        )}
      </div>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
