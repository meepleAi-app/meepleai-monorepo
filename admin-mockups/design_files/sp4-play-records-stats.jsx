/* MeepleAI SP4 — /play-records/stats · STATS
   Route: /play-records/stats
   File: admin-mockups/design_files/sp4-play-records-stats.{html,jsx}
   Modello: sp4-dashboard — KPI strip + sezioni entity-tinted, mobile stacked + desktop grid.
   Entity dominante: session 🎯. KPI: partite · giochi · win-rate · gioco preferito.
   Sezioni: Giochi più giocati · Win-rate per gioco.
*/
const { useState, useEffect } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.session;
  return alpha !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

const STATS = DS.stats;
const favGame = DS.byId[STATS.favoriteGame];

// ═══════════════════════════════════════════════════════
// ─── HERO + KPI STRIP ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const KpiCard = ({ label, value, unit = '', icon, entity, sub, compact }) => (
  <div style={{ padding: compact ? '12px 14px' : '16px 18px', background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-md)', display:'flex', alignItems:'center', gap: compact ? 10 : 14 }}>
    <div style={{ width: compact ? 36 : 46, height: compact ? 36 : 46, borderRadius:'var(--r-sm)', background: entityHsl(entity, 0.12), color: entityHsl(entity), display:'flex', alignItems:'center', justifyContent:'center', fontSize: compact ? 17 : 22, flexShrink: 0 }} aria-hidden="true">{icon}</div>
    <div style={{ minWidth: 0 }}>
      <div style={{ color:'var(--text-muted)', fontSize: 9, fontWeight: 700, fontFamily:'var(--f-mono)', textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 1 }}>{label}</div>
      <div style={{ color:'var(--text)', fontSize: compact ? 20 : 26, fontWeight: 800, fontFamily:'var(--f-display)', fontVariantNumeric:'tabular-nums', lineHeight: 1, whiteSpace:'nowrap' }}>
        {value}<span style={{ fontSize: 12, color:'var(--text-muted)', fontWeight: 600, marginLeft: 2 }}>{unit}</span>
      </div>
      {sub && <div style={{ fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)', fontWeight: 600, marginTop: 2 }}>{sub}</div>}
    </div>
  </div>
);

const StatsHero = ({ compact, empty }) => (
  <header style={{ padding: compact ? '20px 16px 16px' : '30px 32px 22px', background:`linear-gradient(135deg, ${entityHsl('session', 0.08)} 0%, ${entityHsl('game', 0.05)} 50%, ${entityHsl('toolkit', 0.06)} 100%)`, borderBottom:'1px solid var(--border-light)' }}>
    <div style={{ display:'flex', alignItems:'center', gap: 6, marginBottom: 10 }}>
      <span style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'3px 9px', borderRadius:'var(--r-pill)', background: entityHsl('session', 0.12), color: entityHsl('session'), fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em', border:`1px solid ${entityHsl('session', 0.25)}` }}><span aria-hidden="true">🎯</span>Statistiche · /play-records/stats</span>
    </div>
    <h1 style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: compact ? 24 : 34, letterSpacing:'-.02em', lineHeight: 1.05, color:'var(--text)', margin:'0 0 4px' }}>Le tue statistiche <span aria-hidden="true">📊</span></h1>
    <p style={{ color:'var(--text-sec)', fontSize: compact ? 13 : 14, lineHeight: 1.55, margin:'0 0 16px', maxWidth: 620 }}>Una panoramica delle partite registrate: quante ne hai giocate, i giochi preferiti e il tuo win-rate.</p>
    <div style={{ display:'grid', gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)', gap: compact ? 8 : 14, maxWidth: 820 }}>
      <KpiCard label="Partite" value={empty ? 0 : STATS.totals.plays} icon="🎯" entity="session" sub={empty ? 'nessuna ancora' : `${STATS.totals.hoursPlayed}h totali`} compact={compact}/>
      <KpiCard label="Giochi" value={empty ? 0 : STATS.totals.games} icon="🎲" entity="game" sub={empty ? '—' : 'in libreria'} compact={compact}/>
      <KpiCard label="Win rate" value={empty ? '—' : (STATS.totals.winRate * 100).toFixed(0)} unit={empty ? '' : '%'} icon="🏆" entity="toolkit" sub={empty ? '—' : `${Math.round(STATS.totals.plays * STATS.totals.winRate)} vittorie`} compact={compact}/>
      <KpiCard label="Preferito" value={empty ? '—' : favGame.coverEmoji} icon="⭐" entity="player" sub={empty ? '—' : favGame.title} compact={compact}/>
    </div>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── SECTION WRAPPER ───────────────────────────────────
// ═══════════════════════════════════════════════════════
const StatsSection = ({ entity, icon, title, meta, children, compact, fullWidth }) => (
  <section style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-xl)', padding: compact ? 14 : 18, display:'flex', flexDirection:'column', gap: compact ? 10 : 14, gridColumn: fullWidth ? '1 / -1' : 'auto' }}>
    <header style={{ display:'flex', alignItems:'center', gap: compact ? 8 : 10 }}>
      <div style={{ width: compact ? 26 : 32, height: compact ? 26 : 32, borderRadius:'var(--r-sm)', background: entityHsl(entity, 0.12), color: entityHsl(entity), display:'flex', alignItems:'center', justifyContent:'center', fontSize: compact ? 14 : 17, flexShrink: 0 }} aria-hidden="true">{icon}</div>
      <h2 style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: compact ? 14 : 17, lineHeight: 1.15, color:'var(--text)', margin: 0 }}>{title}</h2>
      <div style={{ flex: 1 }}/>
      {meta && <span style={{ fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 700 }}>{meta}</span>}
    </header>
    {children}
  </section>
);

const EmptySection = ({ entity, icon, message, compact }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center', padding: compact ? '20px 12px' : '28px 16px', background:'var(--bg)', border:`1px dashed ${entityHsl(entity, 0.3)}`, borderRadius:'var(--r-md)', flex: 1 }}>
    <div style={{ width: 48, height: 48, borderRadius:'50%', background: entityHsl(entity, 0.12), color: entityHsl(entity), display:'flex', alignItems:'center', justifyContent:'center', fontSize: 22, marginBottom: 8 }} aria-hidden="true">{icon}</div>
    <p style={{ fontSize: 12, color:'var(--text-muted)', maxWidth: 240, margin:'0 0 10px', lineHeight: 1.4 }}>{message}</p>
    <a href="sp4-play-records-new.html" style={{ padding:'6px 12px', borderRadius:'var(--r-sm)', background: entityHsl(entity), color:'#fff', fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800 }}>+ Registra partita</a>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── MOST PLAYED ───────────────────────────────────────
// ═══════════════════════════════════════════════════════
const MostPlayed = ({ items, compact }) => {
  if (!items.length) return <EmptySection entity="game" icon="🎲" message="Nessuna partita registrata. Le statistiche appariranno qui." compact={compact}/>;
  const max = Math.max(...items.map(i => i.plays), 1);
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
      {items.map((it, i) => {
        const g = DS.byId[it.game];
        const pct = Math.round((it.plays / max) * 100);
        return (
          <div key={it.game} style={{ display:'flex', alignItems:'center', gap: 10 }}>
            <span style={{ fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800, color:'var(--text-muted)', width: 14, textAlign:'center', flexShrink: 0 }}>{i + 1}</span>
            <div style={{ width: 34, height: 34, borderRadius:'var(--r-sm)', background: g.cover, display:'flex', alignItems:'center', justifyContent:'center', fontSize: 17, flexShrink: 0 }} aria-hidden="true">{g.coverEmoji}</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ display:'flex', alignItems:'baseline', justifyContent:'space-between', gap: 8, marginBottom: 4 }}>
                <span style={{ fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, color:'var(--text)', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis' }}>{g.title}</span>
                <span style={{ fontFamily:'var(--f-mono)', fontSize: 12, fontWeight: 800, color: entityHsl('game'), flexShrink: 0, fontVariantNumeric:'tabular-nums' }}>{it.plays}<span style={{ color:'var(--text-muted)', fontWeight: 600, fontSize: 9.5 }}> partite</span></span>
              </div>
              <div style={{ height: 8, borderRadius:'var(--r-pill)', background:'var(--bg-muted)', overflow:'hidden' }}>
                <div style={{ width:`${pct}%`, height:'100%', borderRadius:'var(--r-pill)', background:`linear-gradient(90deg, ${entityHsl('game', 0.7)}, ${entityHsl('game')})` }}/>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── WIN RATE BY GAME ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const WinByGame = ({ items, compact }) => {
  if (!items.length) return <EmptySection entity="toolkit" icon="🏆" message="Nessun esito registrato. Gioca per vedere il tuo win-rate." compact={compact}/>;
  const sorted = [...items].sort((a, b) => (b.won / b.played) - (a.won / a.played));
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
      {sorted.map(it => {
        const g = DS.byId[it.game];
        const rate = Math.round((it.won / it.played) * 100);
        return (
          <div key={it.game} style={{ display:'flex', alignItems:'center', gap: 10 }}>
            <div style={{ width: 30, height: 30, borderRadius:'var(--r-sm)', background: g.cover, display:'flex', alignItems:'center', justifyContent:'center', fontSize: 15, flexShrink: 0 }} aria-hidden="true">{g.coverEmoji}</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ display:'flex', alignItems:'baseline', justifyContent:'space-between', gap: 8, marginBottom: 4 }}>
                <span style={{ fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800, color:'var(--text)', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis' }}>{g.title}</span>
                <span style={{ fontFamily:'var(--f-mono)', fontSize: 11.5, fontWeight: 800, color: entityHsl('toolkit'), flexShrink: 0, fontVariantNumeric:'tabular-nums' }}>{rate}% <span style={{ color:'var(--text-muted)', fontWeight: 600, fontSize: 9.5 }}>{it.won}/{it.played}</span></span>
              </div>
              <div style={{ height: 8, borderRadius:'var(--r-pill)', background:'var(--bg-muted)', overflow:'hidden' }}>
                <div style={{ width:`${rate}%`, height:'100%', borderRadius:'var(--r-pill)', background:`linear-gradient(90deg, ${entityHsl('toolkit', 0.65)}, ${entityHsl('toolkit')})` }}/>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SKELETON / ERROR ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const SectionSkeleton = ({ compact, height = 200 }) => (
  <div style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-xl)', padding: compact ? 14 : 18, display:'flex', flexDirection:'column', gap: 10, minHeight: height }}>
    <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
      <div className="mai-shimmer" style={{ width: 28, height: 28, borderRadius:'var(--r-sm)', background:'var(--bg-muted)' }}/>
      <div className="mai-shimmer" style={{ height: 14, width:'40%', borderRadius: 4, background:'var(--bg-muted)' }}/>
    </div>
    {[0,1,2,3].map(i => <div key={i} className="mai-shimmer" style={{ height: 22, borderRadius: 4, background:'var(--bg-muted)' }}/>)}
  </div>
);

const ErrorState = ({ compact }) => (
  <div style={{ gridColumn:'1 / -1', padding: compact ? '32px 20px' : '48px 32px', background:'hsl(var(--c-danger) / .08)', border:'1px solid hsl(var(--c-danger) / .3)', borderRadius:'var(--r-xl)', display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center' }}>
    <div style={{ width: 56, height: 56, borderRadius:'50%', background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))', display:'flex', alignItems:'center', justifyContent:'center', fontSize: 26, marginBottom: 12 }} aria-hidden="true">⚠</div>
    <h3 style={{ fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, color:'var(--text)', margin:'0 0 4px' }}>Impossibile caricare le statistiche</h3>
    <p style={{ fontSize: 12.5, color:'var(--text-muted)', maxWidth: 360, margin:'0 0 16px' }}>Si è verificato un errore di rete. Verifica la connessione e riprova.</p>
    <button type="button" style={{ padding:'8px 16px', borderRadius:'var(--r-md)', background:'hsl(var(--c-danger))', color:'#fff', border:'none', fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer' }}>↻ Riprova</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── BODY ──────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const StatsBody = ({ stateOverride, compact }) => {
  const isLoading = stateOverride === 'loading';
  const isError = stateOverride === 'error';
  const isEmpty = stateOverride === 'empty';

  if (isError) {
    return (
      <>
        <StatsHero compact={compact}/>
        <div style={{ padding: compact ? '16px' : '24px 32px' }}><ErrorState compact={compact}/></div>
      </>
    );
  }

  const mostPlayed = isEmpty ? [] : STATS.mostPlayed;
  const winByGame = isEmpty ? [] : STATS.winByGame;

  return (
    <>
      <StatsHero compact={compact} empty={isEmpty}/>
      <div style={{ padding: compact ? '14px 16px 24px' : '24px 32px 64px', display:'grid', gridTemplateColumns: compact ? '1fr' : 'repeat(2, 1fr)', gap: compact ? 12 : 16 }}>
        {isLoading ? (
          <>
            <SectionSkeleton compact={compact} height={compact ? 200 : 240}/>
            <SectionSkeleton compact={compact} height={compact ? 200 : 240}/>
          </>
        ) : (
          <>
            <StatsSection entity="game" icon="🎲" title="Giochi più giocati" meta={isEmpty ? '' : `top ${mostPlayed.length}`} compact={compact}>
              <MostPlayed items={mostPlayed} compact={compact}/>
            </StatsSection>
            <StatsSection entity="toolkit" icon="🏆" title="Win-rate per gioco" meta={isEmpty ? '' : 'ordinato per %'} compact={compact}>
              <WinByGame items={winByGame} compact={compact}/>
            </StatsSection>
          </>
        )}
      </div>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SHELLS ────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopNav = () => {
  const items = [{ id:'dash', label:'Dashboard' }, { id:'lib', label:'Libreria' }, { id:'rec', label:'Play records', active: true }, { id:'stats', label:'Statistiche', active: true }];
  return (
    <div style={{ display:'flex', alignItems:'center', gap: 14, padding:'10px 32px', background:'var(--glass-bg)', backdropFilter:'blur(12px)', borderBottom:'1px solid var(--border)' }}>
      <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
        <div style={{ width: 26, height: 26, borderRadius: 7, background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))', color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)' }}>M</div>
        <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
      </div>
      <div style={{ display:'flex', alignItems:'center', gap: 2, marginLeft: 18 }}>
        {items.map(it => (
          <span key={it.id} style={{ padding:'6px 12px', borderRadius:'var(--r-md)', fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700, color: it.active && it.id === 'stats' ? entityHsl('session') : 'var(--text-sec)', background: it.active && it.id === 'stats' ? entityHsl('session', 0.1) : 'transparent' }}>{it.label}</span>
        ))}
      </div>
      <div style={{ flex: 1 }}/>
      <div style={{ display:'flex', alignItems:'center', gap: 8, padding:'5px 10px', borderRadius:'var(--r-pill)', background:'var(--bg-muted)', fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700 }}>
        <span style={{ width: 24, height: 24, borderRadius:'50%', background: entityHsl('player'), color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontWeight: 800, fontSize: 11 }}>M</span>
        <span>{STATS.user}</span>
      </div>
    </div>
  );
};

const DesktopScreen = ({ stateOverride }) => (
  <div style={{ minHeight: 640, background:'var(--bg)' }}>
    <DesktopNav/>
    <StatsBody stateOverride={stateOverride}/>
  </div>
);

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}><span>14:32</span><div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div></div>
);
const PhoneTopNav = () => (
  <div style={{ display:'flex', alignItems:'center', gap: 9, padding:'10px 14px', borderBottom:'1px solid var(--border)', background:'var(--bg)' }}>
    <a href="sp4-play-records-index.html" aria-label="Indietro" style={{ width: 32, height: 32, borderRadius:'var(--r-md)', background:'transparent', border:'1px solid var(--border)', color:'var(--text)', fontSize: 14, display:'flex', alignItems:'center', justifyContent:'center' }}>←</a>
    <div style={{ flex: 1, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700, textAlign:'center' }}>Statistiche</div>
    <button aria-label="Filtra periodo" style={{ width: 32, height: 32, borderRadius:'var(--r-md)', background:'transparent', border:'1px solid var(--border)', color:'var(--text)', fontSize: 14, cursor:'pointer' }}>⚙</button>
  </div>
);
const MobileBottomBar = () => {
  const tabs = [{ id:'home', icon:'⌂', label:'Home' }, { id:'lib', icon:'📚', label:'Libreria' }, { id:'rec', icon:'🎯', label:'Partite', active: true }, { id:'chat', icon:'💬', label:'Chat' }, { id:'me', icon:'👤', label:'Profilo' }];
  return (
    <div style={{ position:'absolute', bottom: 0, left: 0, right: 0, display:'flex', padding:'8px 10px 12px', background:'var(--glass-bg)', backdropFilter:'blur(14px)', borderTop:'1px solid var(--border)', zIndex: 5 }}>
      {tabs.map(t => (
        <span key={t.id} style={{ flex: 1, display:'flex', flexDirection:'column', alignItems:'center', gap: 2, padding:'4px 0', color: t.active ? entityHsl('session') : 'var(--text-muted)', fontFamily:'var(--f-display)', fontSize: 9, fontWeight: 700 }}>
          <span aria-hidden="true" style={{ fontSize: 16 }}>{t.icon}</span><span>{t.label}</span>
        </span>
      ))}
    </div>
  );
};
const MobileScreen = ({ stateOverride }) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav/>
      <div style={{ paddingBottom: 70 }}><StatsBody stateOverride={stateOverride} compact/></div>
      <MobileBottomBar/>
    </div>
  </>
);

const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}>
    <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)', textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700 }}>{label}</div>
    <div className="phone">{children}</div>
    {desc && <div style={{ fontSize: 11, color:'var(--text-muted)', maxWidth: 340, textAlign:'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

const DesktopFrame = ({ label, desc, dark, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }} data-theme={dark ? 'dark' : undefined}>
    <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)', textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700 }}>{label}{dark && <span style={{ color: entityHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div className="desktop-frame">
      <div className="desktop-bar"><span className="traffic"/><span className="traffic"/><span className="traffic"/><span className="url">meepleai.app/play-records/stats</span></div>
      {children}
    </div>
    {desc && <div style={{ fontSize: 11, color:'var(--text-muted)', maxWidth: 720, textAlign:'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

const MOBILE_STATES = [
  { id:'m1', label:'01 · Default', desc:'KPI strip 2×2 (partite/giochi/win-rate/preferito) + Giochi più giocati + Win-rate per gioco con barre.' },
  { id:'m2', state:'empty', label:'02 · Empty (first-run)', desc:'KPI a 0 + sezioni con empty-state entity-tinted + CTA Registra partita.' },
  { id:'m3', state:'loading', label:'03 · Loading', desc:'KPI server-rendered + 2 section skeleton shimmer.' },
  { id:'m4', state:'error', label:'04 · Error', desc:'Banner danger inline + retry, hero resta visibile.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function App() {
  return (
    <div className="stage">
      <ThemeToggle/>
      <div className="stage-wrap">
        <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8 }}>SP4 · /play-records · Stats 📊</div>
        <h1>Play records — /play-records/stats</h1>
        <p className="lead">
          Dashboard statistiche partite. <strong>KPI strip</strong> (partite · giochi · win-rate · gioco preferito) +
          sezioni <strong>Giochi più giocati</strong> (barre per partite) e <strong>Win-rate per gioco</strong> (barre %).
          Mobile stacked + bottom-bar, desktop grid 2-col. 4 stati mobile + 3 desktop.
        </p>

        <div className="section-label">Mobile · 375 — 4 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <MobileScreen stateOverride={s.state}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1280 — 3 stati</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="05 · Desktop · Default" desc="Hero gradient + 4 KPI 4-col. Grid 2-col: Giochi più giocati + Win-rate per gioco.">
            <DesktopScreen stateOverride={null}/>
          </DesktopFrame>
          <DesktopFrame label="06 · Desktop · Dark mode" dark desc="Barre entity game/toolkit + KPI verificati in dark.">
            <DesktopScreen stateOverride={null}/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Empty (first-run)" desc="Tutte le sezioni in empty-state con CTA Registra partita.">
            <DesktopScreen stateOverride="empty"/>
          </DesktopFrame>
        </div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
