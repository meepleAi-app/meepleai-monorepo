/* MeepleAI SP4 wave 2 — Schermata #3/3 FINALE — Session summary — EXTENDED
   /sessions/[id]  (+ post-game review sub-routes as tabs)

   Adds a RightColumnTabs (analogous to the live page) hosting 3 review tabs:
     scoreboard · notes · players   (was 3 separate routes)
   Deep link ?tab=<id>. The celebratory body (KPI · scoring · achievements ·
   diary · photos · chat · share · play-again) is preserved unchanged.

   Imports:
     window.MAI               (sp4-parts-common.jsx)
     window.SummaryParts      (sp4-session-summary-parts.jsx)    → SP
     window.SummarySections   (sp4-session-summary-sections.jsx) → Sx
     window.SummaryReviewTabs (sp4-session-summary-tabs.jsx)     → RT
*/

const { useState: us, useEffect: ue } = React;
const M = window.MAI;
const SP = window.SummaryParts;
const Sx = window.SummarySections;
const RT = window.SummaryReviewTabs;
const eHs = SP.eH;

// ─── review tabs registry ────────────────────────────
const RTABS = [
  { id: 'scoreboard', icon: '🏁', label: 'Scoreboard', entity: 'session', render: (st) => <RT.ScoreboardTab state={st} /> },
  { id: 'notes',      icon: '📝', label: 'Note',       entity: 'kb',      render: (st) => <RT.NotesTab state={st} /> },
  { id: 'players',    icon: '👥', label: 'Player',     entity: 'player',  render: (st) => <RT.PlayersTab state={st} /> },
];
const RSTATES = [
  { id: 'default', lb: 'Default' }, { id: 'empty', lb: 'Empty' }, { id: 'loading', lb: 'Loading' },
  { id: 'error', lb: 'Error' }, { id: 'offline', lb: 'Offline' },
];

const StateSwitch = ({ value, onChange }) => (
  <div className="mai-cb-scroll" style={{ display: 'flex', gap: 4, overflowX: 'auto', padding: '7px 10px', borderBottom: '1px solid var(--border-light)', background: 'var(--bg)' }}>
    <span style={{ fontFamily: 'var(--f-mono)', fontSize: 8.5, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', alignSelf: 'center', flexShrink: 0, marginRight: 2 }}>Stato</span>
    {RSTATES.map(s => {
      const on = value === s.id;
      return <button key={s.id} type="button" onClick={() => onChange(s.id)} aria-pressed={on} style={{ padding: '3px 9px', borderRadius: 'var(--r-pill)', flexShrink: 0, background: on ? eHs('session', 0.14) : 'var(--bg-card)', border: on ? `1px solid ${eHs('session', 0.4)}` : '1px solid var(--border)', color: on ? eHs('session') : 'var(--text-sec)', fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, cursor: 'pointer' }}>{s.lb}</button>;
    })}
  </div>
);

// ─── RightColumnTabs (summary · 3 review tabs) ───────
const RightColumnTabs = ({ initial = 'scoreboard', width = 360, embedded }) => {
  const [tab, setTab] = us(initial);
  const [st, setSt] = us('default');
  const active = RTABS.find(t => t.id === tab);
  return (
    <aside style={{ width: embedded ? '100%' : width, flexShrink: 0, height: '100%', background: 'var(--bg-card)', borderLeft: embedded ? 'none' : '1px solid var(--border)', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
      <div role="tablist" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0 }}>
        {RTABS.map(t => {
          const on = tab === t.id;
          return (
            <button key={t.id} type="button" role="tab" aria-selected={on} onClick={() => setTab(t.id)} style={{ flex: 1, padding: '10px 6px', background: on ? eHs(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHs(t.entity)}` : '2px solid transparent', color: on ? eHs(t.entity) : 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 5 }}>
              <span aria-hidden="true">{t.icon}</span>{t.label}
            </button>
          );
        })}
      </div>
      <StateSwitch value={st} onChange={setSt} />
      <div role="tabpanel" style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
    </aside>
  );
};

// ─── celebratory body (preserved) ────────────────────
const SummaryBody = ({ stateOverride, compact, emptyAch, emptyPhotos, emptyChat }) => {
  if (stateOverride === 'loading') {
    return (
      <div style={{ padding: compact ? 14 : 24, display: 'flex', flexDirection: 'column', gap: 14 }}>
        <M.Shimmer h={compact ? 200 : 280} r="var(--r-xl)" />
        <M.Shimmer h={100} r="var(--r-lg)" />
        <M.Shimmer h={240} r="var(--r-lg)" />
      </div>
    );
  }
  if (stateOverride === 'error') {
    return (
      <div style={{ padding: '40px 24px', textAlign: 'center', margin: 24, background: eHs('event', 0.06), border: `1px solid ${eHs('event', 0.25)}`, borderRadius: 'var(--r-xl)' }}>
        <div style={{ fontSize: 32, marginBottom: 8 }} aria-hidden="true">⚠</div>
        <h3 style={{ fontFamily: 'var(--f-display)', fontSize: 16, fontWeight: 800, margin: '0 0 4px' }}>Errore caricamento riepilogo</h3>
        <p style={{ fontSize: 12.5, color: 'var(--text-muted)', margin: '0 0 14px' }}>Impossibile recuperare i dati della partita.</p>
        <button type="button" style={{ padding: '8px 14px', borderRadius: 'var(--r-md)', background: eHs('event'), color: '#fff', border: 'none', fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor: 'pointer' }}>↻ Riprova</button>
      </div>
    );
  }
  return (
    <div style={{ padding: compact ? '14px 14px 32px' : '24px 28px 56px', display: 'flex', flexDirection: 'column', gap: compact ? 18 : 24, width: '100%' }}>
      <section id="section-session"><SP.KpiGrid compact={compact} /></section>
      <section id="section-player"><SP.ScoringBreakdownTable compact={compact} /></section>
      <Sx.Achievements empty={emptyAch} compact={compact} />
      <Sx.Diary />
      <Sx.PhotoGallery empty={emptyPhotos} compact={compact} />
      <Sx.ChatHighlights empty={emptyChat} />
      <Sx.ShareCard compact={compact} />
      <Sx.PlayAgain />
    </div>
  );
};

// ─── chrome ──────────────────────────────────────────
const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color: 'var(--text)' }}>
    <span style={{ fontFamily: 'var(--f-mono)' }}>15:33</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">88%</span></div>
  </div>
);

const TopNav = ({ compact, onOpenReview }) => (
  <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: compact ? '8px 12px' : '10px 24px', background: 'var(--glass-bg)', backdropFilter: 'blur(12px)', borderBottom: '1px solid var(--border)' }}>
    <button type="button" aria-label="Indietro" style={{ width: 30, height: 30, borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13, fontWeight: 800 }}>←</button>
    <div style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '2px 8px', borderRadius: 'var(--r-pill)', background: eHs('game', 0.1), color: eHs('game'), border: `1px solid ${eHs('game', 0.3)}`, fontFamily: 'var(--f-mono)', fontSize: 9.5, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '.05em' }}>🦜 Wingspan</div>
    <span style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: 'var(--text)' }}>Serata #3</span>
    <M.OutcomeBadge status="win" size="sm" />
    <div style={{ flex: 1 }} />
    {compact && (
      <button type="button" onClick={onOpenReview} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, padding: '6px 11px', borderRadius: 'var(--r-pill)', background: eHs('session', 0.12), color: eHs('session'), border: `1px solid ${eHs('session', 0.3)}`, fontFamily: 'var(--f-display)', fontSize: 11.5, fontWeight: 800, cursor: 'pointer' }}>🏁 Review</button>
    )}
  </div>
);

// ─── mobile review sheet ─────────────────────────────
const MobileReviewSheet = ({ open, onClose }) => {
  const [tab, setTab] = us('scoreboard');
  const [st, setSt] = us('default');
  const active = RTABS.find(t => t.id === tab);
  return (
    <div aria-hidden={!open} style={{ position: 'absolute', inset: 0, zIndex: 50, pointerEvents: open ? 'auto' : 'none' }}>
      <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'rgba(15,12,30,.5)', backdropFilter: 'blur(3px)', opacity: open ? 1 : 0, transition: 'opacity var(--dur-md) var(--ease-out)' }} />
      <div role="dialog" aria-label="Review" style={{ position: 'absolute', left: 0, right: 0, bottom: 0, height: '84%', background: 'var(--bg-card)', borderTopLeftRadius: 'var(--r-2xl)', borderTopRightRadius: 'var(--r-2xl)', borderTop: `3px solid ${eHs(active.entity)}`, boxShadow: 'var(--shadow-drawer)', display: 'flex', flexDirection: 'column', minHeight: 0, transform: open ? 'translateY(0)' : 'translateY(101%)', transition: 'transform var(--dur-lg) var(--ease-spring)' }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', paddingTop: 8, flexShrink: 0 }}>
          <div style={{ width: 38, height: 4, borderRadius: 999, background: 'var(--border-strong)' }} />
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px 6px', flexShrink: 0 }}>
          <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', flex: 1 }}>Post-game review</span>
          <button type="button" onClick={onClose} aria-label="Chiudi" style={{ width: 28, height: 28, borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border)', color: 'var(--text-sec)', cursor: 'pointer', fontSize: 13 }}>✕</button>
        </div>
        <div role="tablist" style={{ display: 'flex', padding: '0 8px', gap: 4, flexShrink: 0 }}>
          {RTABS.map(t => {
            const on = tab === t.id;
            return <button key={t.id} type="button" role="tab" aria-selected={on} onClick={() => setTab(t.id)} style={{ flex: 1, padding: '8px 6px', borderRadius: 'var(--r-md)', background: on ? eHs(t.entity, 0.1) : 'transparent', border: on ? `1px solid ${eHs(t.entity, 0.3)}` : '1px solid transparent', color: on ? eHs(t.entity) : 'var(--text-sec)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 5 }}><span aria-hidden="true">{t.icon}</span>{t.label}</button>;
          })}
        </div>
        <StateSwitch value={st} onChange={setSt} />
        <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>{active.render(st)}</div>
      </div>
    </div>
  );
};

// ─── frames ──────────────────────────────────────────
const PhoneScreen = (props) => {
  const [review, setReview] = us(props.openReview || false);
  return (
    <>
      <PhoneSbar />
      <div style={{ flex: 1, position: 'relative', display: 'flex', flexDirection: 'column', background: 'var(--bg)', overflow: 'hidden', minHeight: 0 }}>
        <div style={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column' }}>
          <TopNav compact onOpenReview={() => setReview(true)} />
          <SP.SummaryHeroPodium variant={props.variant} compact />
          <SP.ConnectionBar compact />
          <SummaryBody {...props} compact />
        </div>
        <MobileReviewSheet open={review} onClose={() => setReview(false)} />
      </div>
    </>
  );
};

const DesktopFrameInner = (props) => (
  <div style={{ display: 'flex', height: 720, background: 'var(--bg)', overflow: 'hidden' }}>
    <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
      <TopNav />
      <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column' }}>
        <SP.SummaryHeroPodium variant={props.variant} />
        <SP.ConnectionBar />
        <div style={{ maxWidth: 900, width: '100%', margin: '0 auto' }}><SummaryBody {...props} /></div>
      </div>
    </div>
    <RightColumnTabs initial={props.initialReviewTab} />
  </div>
);

const PhoneShell = ({ label, desc, dark, children }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700 }}>{label}{dark && <span style={{ color: eHs('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div className="phone" data-theme={dark ? 'dark' : undefined}>{children}</div>
    {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 340, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

const DesktopFrame = ({ label, desc, dark, children }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, width: '100%' }} data-theme={dark ? 'dark' : undefined}>
    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700 }}>{label}{dark && <span style={{ color: eHs('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div style={{ width: '100%', maxWidth: 1280, borderRadius: 'var(--r-xl)', border: '1px solid var(--border)', background: 'var(--bg)', overflow: 'hidden', boxShadow: 'var(--shadow-lg)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '9px 14px', background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)', fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)' }}>
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-event))' }} />
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-warning))' }} />
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-toolkit))' }} />
        <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>meepleai.app/sessions/wing-3?tab=scoreboard</span>
      </div>
      {children}
    </div>
    {desc && <div style={{ fontSize: 11, color: 'var(--text-muted)', maxWidth: 820, textAlign: 'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

const PanelFrame = ({ label, entity, children, dark }) => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }} data-theme={dark ? 'dark' : undefined}>
    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.07em', fontWeight: 800 }}>{label}</div>
    <div style={{ width: 320, height: 480, borderRadius: 'var(--r-lg)', border: `1px solid ${eHs(entity, 0.3)}`, background: 'var(--bg-card)', overflow: 'hidden', display: 'flex', flexDirection: 'column', boxShadow: 'var(--shadow-sm)' }}>{children}</div>
  </div>
);

function ThemeToggle() {
  const [dark, setDark] = us(false);
  ue(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function App() {
  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Sessions consolidation · Summary 🏆</div>
        <h1>Session summary — con RightColumnTabs review</h1>
        <p className="lead">
          Le 3 sub-route post-game <strong>scoreboard · notes · players</strong> diventano tab in un
          <strong> RightColumnTabs</strong> analogo a quello della live. Il corpo celebrativo (KPI · breakdown ·
          achievements · diario · foto · chat · share · play-again) resta invariato. Desktop: rail 360 sticky.
          Mobile 375: bottone <strong>Review</strong> → bottom-sheet. Deep link <code>?tab=&lt;id&gt;</code>.
        </p>

        <div className="section-label">Interattivo · Desktop 1280 — corpo summary + rail review (clicca tab, cambia stato)</div>
        <DesktopFrame label="Desktop · summary · review rail"
          desc="Layout 2-col: corpo celebrativo scroll (con ConnectionBar pip → smooth scroll alle sezioni) + RightColumnTabs 360 con scoreboard/note/player e selettore stato (default/empty/loading/error/offline).">
          <DesktopFrameInner variant="default" initialReviewTab="scoreboard" />
        </DesktopFrame>

        <div className="section-label">Interattivo · Mobile 375 — bottone Review → bottom-sheet</div>
        <div className="phones-grid">
          <PhoneShell label="01 · Mobile · summary (sheet chiuso)" desc="Corpo summary scrollabile. Bottone Review in top nav apre il sheet.">
            <PhoneScreen variant="default" />
          </PhoneShell>
          <PhoneShell label="02 · Mobile · review sheet aperto" desc="Bottom-sheet con le 3 tab review + selettore stato.">
            <PhoneScreen variant="default" openReview />
          </PhoneShell>
          <PhoneShell label="03 · Mobile · dark" dark desc="Confetti glow piu` visibile, accent entity-aware.">
            <PhoneScreen variant="default" openReview />
          </PhoneShell>
        </div>

        <div className="section-label">Gallery stati — ogni tab review × 5 stati (default · empty · loading · error · offline)</div>
        {RTABS.map(t => (
          <div key={t.id} style={{ marginBottom: 28 }}>
            <div style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHs(t.entity), marginBottom: 10, display: 'flex', alignItems: 'center', gap: 7 }}>
              <span aria-hidden="true">{t.icon}</span>{t.label} <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 700 }}>· /sessions/[id]/{t.id}</span>
            </div>
            <div style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 8 }}>
              {RSTATES.map(s => (
                <PanelFrame key={s.id} label={s.lb} entity={t.entity} dark={s.id === 'offline'}>{t.render(s.id)}</PanelFrame>
              ))}
            </div>
          </div>
        ))}
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-confetti-fall { 0% { transform: translateY(-30px) rotate(0deg); opacity: 0; } 15% { opacity: 1; } 100% { transform: translateY(220px) rotate(540deg); opacity: 0; } }
        .mai-confetti-piece { animation: mai-confetti-fall 2.6s cubic-bezier(.4,0,.2,1) forwards; }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        @keyframes mai-pulse { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-pulse-dot { animation: mai-pulse 1.5s ease-in-out infinite; display: inline-block; transform-origin: center; }
        .mai-cb-scroll::-webkit-scrollbar { height: 0; display: none; }
        .mai-cb-scroll { scrollbar-width: none; }
        @media (prefers-reduced-motion: reduce) { .mai-confetti-piece, .mai-shimmer, .mai-pulse-dot { animation: none !important; } .mai-confetti-piece { opacity: .35; } }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; }
        .phone > div::-webkit-scrollbar { display: none; }
        .mai-cb-scroll::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
