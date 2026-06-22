/* MeepleAI SP4 — Schermata /play-records/stats · STATS
   Route: /play-records/stats
   File: admin-mockups/design_files/sp4-play-records-stats.{html,jsx}
   Modello: sp4-dashboard — KPI strip + sezioni entity-tinted, mobile stack + desktop grid 2-col.
   Entity dominante: session 🎯. KPI: partite · giochi · win-rate · gioco preferito.
   Sezioni: Giochi più giocati · Win-rate per gioco.

   ── Stati canonici (G7 SessionStateRenderer, PR 2357) ──────────────
   Export per-stato (anchor #state-NN-* nello stats HTML):
     State01_Default  → state-01-default   (dashboard completa as-shipped)
     State02_Empty    → state-02-empty     (zero partite · banner info · CTA prima partita)
     State03_Loading  → state-03-loading   (skeleton KPI + hero + sezioni · aria-busy)
     State04_Error    → state-04-error     (banner alert · retry · torna-lista · dismiss)
   state-05-sse → SKIPPED: dashboard NON è SSE-driven (fetch-once aggregate, refresh manuale).

   FREEZE: zero hex/hsl numerico hardcoded per gli entity color → solo token --c-*
   via entityHsl(). Pattern .e-bg (color:'#fff' su bg entity) esente. Nessun asset esterno.
*/
const { useState, useEffect } = React;
const DS = window.DS;

// entityHsl(entity, alpha?) — risolve SEMPRE sui token CSS (--c-*), così il colore
// segue automaticamente light/dark ([data-theme]) ed è FREEZE-clean (nessun valore
// hsl numerico hardcoded nel sorgente del mockup).
const entityHsl = (entity, alpha) =>
  alpha === undefined
    ? `hsl(var(--c-${entity}))`
    : `hsl(var(--c-${entity}) / ${alpha})`;

const STATS = DS.stats;
const favGame = DS.byId[STATS.favoriteGame];

// ═══════════════════════════════════════════════════════
// ─── KPI CARD ──────────────────────────────────────────
// ═══════════════════════════════════════════════════════
let kpiSeq = 0;
const KpiCard = ({ label, value, unit = '', icon, entity, sub, compact, placeholder }) => {
  const labelId = `kpi-lbl-${++kpiSeq}`;
  return (
    <div role="group" aria-labelledby={labelId} style={{ padding: compact ? '12px 14px' : '16px 18px', background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-md)', display:'flex', alignItems:'center', gap: compact ? 10 : 14, opacity: placeholder ? 0.72 : 1 }}>
      <div style={{ width: compact ? 36 : 46, height: compact ? 36 : 46, borderRadius:'var(--r-sm)', background: entityHsl(entity, 0.12), color: entityHsl(entity), display:'flex', alignItems:'center', justifyContent:'center', fontSize: compact ? 17 : 22, flexShrink: 0 }} aria-hidden="true">{icon}</div>
      <div style={{ minWidth: 0 }}>
        <div id={labelId} style={{ color:'var(--text-muted)', fontSize: 9, fontWeight: 700, fontFamily:'var(--f-mono)', textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 1 }}>{label}</div>
        <div style={{ color:'var(--text)', fontSize: compact ? 20 : 26, fontWeight: 800, fontFamily:'var(--f-display)', fontVariantNumeric:'tabular-nums', lineHeight: 1, whiteSpace:'nowrap' }} aria-label={placeholder ? 'Statistica non disponibile' : undefined}>
          {value}<span style={{ fontSize: 12, color:'var(--text-muted)', fontWeight: 600, marginLeft: 2 }}>{unit}</span>
        </div>
        {sub && <div style={{ fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)', fontWeight: 600, marginTop: 2 }}>{sub}</div>}
      </div>
    </div>
  );
};

const KpiStrip = ({ compact, empty }) => (
  <div style={{ display:'grid', gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)', gap: compact ? 8 : 14, maxWidth: 820 }}>
    <KpiCard label="Partite" value={empty ? 0 : STATS.totals.plays} icon="🎯" entity="session" sub={empty ? 'nessuna ancora' : `${STATS.totals.hoursPlayed}h totali`} compact={compact} placeholder={empty}/>
    <KpiCard label="Giochi" value={empty ? 0 : STATS.totals.games} icon="🎲" entity="game" sub={empty ? '—' : 'in libreria'} compact={compact} placeholder={empty}/>
    <KpiCard label="Win rate" value={empty ? '—' : (STATS.totals.winRate * 100).toFixed(0)} unit={empty ? '' : '%'} icon="🏆" entity="toolkit" sub={empty ? '—' : `${Math.round(STATS.totals.plays * STATS.totals.winRate)} vittorie`} compact={compact} placeholder={empty}/>
    <KpiCard label="Preferito" value={empty ? 'n.d.' : favGame.coverEmoji} icon="⭐" entity="player" sub={empty ? '—' : favGame.title} compact={compact} placeholder={empty}/>
  </div>
);

const StatsHero = ({ compact, empty }) => (
  <header style={{ padding: compact ? '20px 16px 16px' : '30px 32px 22px', background:`linear-gradient(135deg, ${entityHsl('session', 0.08)} 0%, ${entityHsl('game', 0.05)} 50%, ${entityHsl('toolkit', 0.06)} 100%)`, borderBottom:'1px solid var(--border-light)' }}>
    <div style={{ display:'flex', alignItems:'center', gap: 6, marginBottom: 10 }}>
      <span style={{ display:'inline-flex', alignItems:'center', gap: 5, padding:'3px 9px', borderRadius:'var(--r-pill)', background: entityHsl('session', 0.12), color: entityHsl('session'), fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em', border:`1px solid ${entityHsl('session', 0.25)}` }}><span aria-hidden="true">🎯</span>Statistiche · /play-records/stats</span>
    </div>
    <h1 style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: compact ? 24 : 34, letterSpacing:'-.02em', lineHeight: 1.05, color:'var(--text)', margin:'0 0 4px' }}>Le tue statistiche <span aria-hidden="true">📊</span></h1>
    <p style={{ color:'var(--text-sec)', fontSize: compact ? 13 : 14, lineHeight: 1.55, margin:'0 0 16px', maxWidth: 620 }}>Una panoramica delle partite registrate: quante ne hai giocate, i giochi preferiti e il tuo win-rate.</p>
    <KpiStrip compact={compact} empty={empty}/>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── SECTION WRAPPER ───────────────────────────────────
// ═══════════════════════════════════════════════════════
const StatsSection = ({ entity, icon, title, meta, children, compact, ariaLabel }) => (
  <section role="region" aria-label={ariaLabel || title} style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-xl)', padding: compact ? 14 : 18, display:'flex', flexDirection:'column', gap: compact ? 10 : 14 }}>
    <header style={{ display:'flex', alignItems:'center', gap: compact ? 8 : 10 }}>
      <div style={{ width: compact ? 26 : 32, height: compact ? 26 : 32, borderRadius:'var(--r-sm)', background: entityHsl(entity, 0.12), color: entityHsl(entity), display:'flex', alignItems:'center', justifyContent:'center', fontSize: compact ? 14 : 17, flexShrink: 0 }} aria-hidden="true">{icon}</div>
      <h2 style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: compact ? 14 : 17, lineHeight: 1.15, color:'var(--text)', margin: 0 }}>{title}</h2>
      <div style={{ flex: 1 }}/>
      {meta && <span style={{ fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 700 }}>{meta}</span>}
    </header>
    {children}
  </section>
);

// ═══════════════════════════════════════════════════════
// ─── MOST PLAYED (bar list) ────────────────────────────
// ═══════════════════════════════════════════════════════
const MostPlayed = ({ items }) => {
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
// ─── WIN RATE BY GAME (bar list) ───────────────────────
// ═══════════════════════════════════════════════════════
const WinByGame = ({ items }) => {
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
// ─── EMPTY DASHBOARD CARD (state-02) ───────────────────
// ═══════════════════════════════════════════════════════
const EmptyDashCard = ({ compact }) => (
  <div style={{ gridColumn:'1 / -1', display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center', padding: compact ? '40px 22px' : '56px 28px', background:'var(--bg-card)', border:'1px dashed var(--border-strong)', borderRadius:'var(--r-xl)' }}>
    <div aria-hidden="true" style={{ width: 88, height: 88, borderRadius:'50%', background:`radial-gradient(circle, ${entityHsl('session', 0.18)} 0%, transparent 70%)`, display:'flex', alignItems:'center', justifyContent:'center', fontSize: 42, marginBottom: 14 }}>📊</div>
    <h2 style={{ fontFamily:'var(--f-display)', fontSize: compact ? 18 : 20, fontWeight: 800, color:'var(--text)', margin:'0 0 8px' }}>Nessun dato ancora</h2>
    <p style={{ fontFamily:'var(--f-body)', fontSize: compact ? 13 : 13.5, color:'var(--text-sec)', margin:'0 0 20px', maxWidth: 360, lineHeight: 1.55, fontWeight: 500 }}>Registra le tue partite per vedere statistiche aggregate</p>
    <a href="sp4-play-records-new.html" style={{ padding:'10px 18px', borderRadius:'var(--r-md)', background: entityHsl('session'), color:'#fff', fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800, display:'inline-flex', alignItems:'center', gap: 6, boxShadow:`0 4px 14px ${entityHsl('session', 0.4)}` }}><span aria-hidden="true">+</span>Registra prima partita</a>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── SKELETON PRIMITIVES (state-03) ────────────────────
// Pulse 0.4→0.8→0.4 (2s) via .skel; prefers-reduced-motion → snap.
// ═══════════════════════════════════════════════════════
const SkelRect = ({ w, h, r, style }) => (
  <div aria-hidden="true" className="skel" style={{ width: w, height: h, borderRadius: r || 'var(--r-sm)', background: entityHsl('session', 0.1), flexShrink: 0, ...style }}/>
);

const KpiCardSkel = ({ compact }) => (
  <div aria-hidden="true" style={{ padding: compact ? '12px 14px' : '16px 18px', background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-md)', display:'flex', alignItems:'center', gap: compact ? 10 : 14 }}>
    <SkelRect w={compact ? 36 : 46} h={compact ? 36 : 46} r="var(--r-sm)"/>
    <div style={{ flex: 1, minWidth: 0, display:'flex', flexDirection:'column', gap: 6 }}>
      <SkelRect w="52%" h={9} r="var(--r-xs)"/>
      <SkelRect w="70%" h={compact ? 18 : 22} r="var(--r-xs)"/>
    </div>
  </div>
);

const SectionRowSkel = () => (
  <div aria-hidden="true" style={{ display:'flex', alignItems:'center', gap: 10 }}>
    <SkelRect w={32} h={32} r="var(--r-sm)"/>
    <div style={{ flex: 1, display:'flex', flexDirection:'column', gap: 5 }}>
      <SkelRect w="60%" h={12} r="var(--r-xs)"/>
      <SkelRect w="100%" h={8} r="var(--r-pill)"/>
    </div>
  </div>
);

const SectionSkel = ({ rows = 5, compact }) => (
  <div aria-hidden="true" style={{ background:'var(--bg-card)', border:'1px solid var(--border)', borderRadius:'var(--r-xl)', padding: compact ? 14 : 18, display:'flex', flexDirection:'column', gap: 12 }}>
    <div style={{ display:'flex', alignItems:'center', gap: 10 }}>
      <SkelRect w={compact ? 26 : 32} h={compact ? 26 : 32} r="var(--r-sm)"/>
      <SkelRect w="44%" h={15} r="var(--r-xs)"/>
    </div>
    {Array.from({ length: rows }).map((_, i) => <SectionRowSkel key={i}/>)}
  </div>
);

const HeroSkel = ({ compact }) => (
  <header aria-hidden="true" style={{ padding: compact ? '20px 16px 16px' : '30px 32px 22px', background:`linear-gradient(135deg, ${entityHsl('session', 0.06)} 0%, ${entityHsl('toolkit', 0.04)} 100%)`, borderBottom:'1px solid var(--border-light)', display:'flex', flexDirection:'column', gap: 12 }}>
    <SkelRect w={compact ? 190 : 230} h={18} r="var(--r-pill)"/>
    <SkelRect w={compact ? '70%' : 320} h={compact ? 26 : 34} r="var(--r-md)"/>
    <SkelRect w={compact ? '92%' : 460} h={13} r="var(--r-xs)" style={{ marginBottom: 6 }}/>
    <div style={{ display:'grid', gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)', gap: compact ? 8 : 14, maxWidth: 820 }}>
      {[0,1,2,3].map(i => <KpiCardSkel key={i} compact={compact}/>)}
    </div>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── BANNERS (empty info / error) ──────────────────────
// ═══════════════════════════════════════════════════════
const EmptyInfoBanner = ({ compact }) => (
  <div role="status" aria-live="polite" style={{ display:'flex', alignItems:'center', gap: compact ? 9 : 12, padding: compact ? '11px 16px' : '13px 32px', background: entityHsl('session', 0.06), borderLeft:`4px solid ${entityHsl('session', 0.45)}`, borderBottom:'1px solid var(--border-light)' }}>
    <span aria-hidden="true" style={{ fontSize: compact ? 16 : 18, lineHeight: 1 }}>ℹ️</span>
    <div style={{ flex: 1, minWidth: 0, fontFamily:'var(--f-body)', fontSize: compact ? 12.5 : 13.5, fontWeight: 600, color:'var(--text-sec)' }}>Le statistiche compaiono dopo la prima partita registrata</div>
  </div>
);

const ErrorBanner = ({ compact }) => (
  <div role="alert" style={{ display:'flex', alignItems:'center', gap: compact ? 10 : 14, padding: compact ? '12px 16px' : '14px 32px', background: entityHsl('event', 0.08), borderLeft:`4px solid ${entityHsl('event', 0.6)}`, borderBottom:'1px solid var(--border-light)' }}>
    <span aria-hidden="true" style={{ fontSize: compact ? 18 : 20, lineHeight: 1 }}>⚠️</span>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{ fontFamily:'var(--f-display)', fontSize: compact ? 13 : 14.5, fontWeight: 800, color:'var(--text)' }}>Impossibile caricare le statistiche</div>
      <div style={{ fontFamily:'var(--f-body)', fontSize: compact ? 11.5 : 12.5, fontWeight: 600, color:'var(--text-muted)', marginTop: 2 }}>Verifica la connessione e riprova</div>
    </div>
    <button type="button" aria-label="Riprova caricamento statistiche" style={{ padding:'7px 14px', borderRadius:'var(--r-md)', background:'transparent', color: entityHsl('event'), border:`1px solid ${entityHsl('event', 0.5)}`, fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 5, whiteSpace:'nowrap', flexShrink: 0 }}><span aria-hidden="true">↻</span>Riprova</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── DASHBOARD BODY (shared) ───────────────────────────
// ═══════════════════════════════════════════════════════
const SectionGrid = ({ compact, children }) => (
  <div style={{ padding: compact ? '14px 16px 24px' : '24px 32px 64px', display:'grid', gridTemplateColumns: compact ? '1fr' : 'repeat(2, 1fr)', gap: compact ? 12 : 16 }}>{children}</div>
);

// ═══════════════════════════════════════════════════════
// ─── APP CHROME ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopNav = () => {
  const items = [{ id:'dash', label:'Dashboard' }, { id:'lib', label:'Libreria' }, { id:'rec', label:'Play records' }, { id:'stats', label:'Statistiche', active: true }];
  return (
    <div style={{ display:'flex', alignItems:'center', gap: 14, padding:'10px 32px', background:'var(--glass-bg)', backdropFilter:'blur(12px)', borderBottom:'1px solid var(--border)' }}>
      <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
        <div style={{ width: 26, height: 26, borderRadius: 7, background:`linear-gradient(135deg, ${entityHsl('game')}, ${entityHsl('event')})`, color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)' }}>M</div>
        <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
      </div>
      <div style={{ display:'flex', alignItems:'center', gap: 2, marginLeft: 18 }}>
        {items.map(it => (
          <span key={it.id} style={{ padding:'6px 12px', borderRadius:'var(--r-md)', fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700, color: it.active ? entityHsl('session') : 'var(--text-sec)', background: it.active ? entityHsl('session', 0.1) : 'transparent' }}>{it.label}</span>
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

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}><span>14:32</span><div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div></div>
);
const PhoneTopNav = () => (
  <div style={{ display:'flex', alignItems:'center', gap: 9, padding:'10px 14px', borderBottom:'1px solid var(--border)', background:'var(--bg)' }}>
    <a href="sp4-play-records-index.html" aria-label="Indietro" style={{ width: 32, height: 32, borderRadius:'var(--r-md)', background:'transparent', border:'1px solid var(--border)', color:'var(--text)', fontSize: 14, display:'flex', alignItems:'center', justifyContent:'center' }}>←</a>
    <div style={{ flex: 1, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700, textAlign:'center' }}>Statistiche</div>
    <span style={{ width: 32, height: 32, borderRadius:'var(--r-md)', border:'1px solid var(--border)', color:'var(--text)', fontSize: 14, display:'flex', alignItems:'center', justifyContent:'center' }} aria-hidden="true">⚙</span>
  </div>
);
const MobileBottomBar = () => {
  const tabs = [{ id:'home', icon:'⌂', label:'Home' }, { id:'lib', icon:'📚', label:'Libreria' }, { id:'rec', icon:'🎯', label:'Partite' }, { id:'stats', icon:'📊', label:'Stats', active: true }, { id:'me', icon:'👤', label:'Profilo' }];
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

// Screen: renders one state's full dashboard, in mobile (compact) or desktop chrome.
const Screen = ({ children, compact }) => {
  if (compact) {
    return (
      <>
        <PhoneSbar/>
        <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
          <PhoneTopNav/>
          <div style={{ paddingBottom: 70 }}>{children}</div>
          <MobileBottomBar/>
        </div>
      </>
    );
  }
  return (
    <div style={{ display:'flex', flexDirection:'column', minHeight: 0, background:'var(--bg)' }}>
      <DesktopNav/>
      {children}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── STATO 01 · DEFAULT ────────────────────────────────
// state-01-default — dashboard completa as-shipped (INVARIATO).
// ═══════════════════════════════════════════════════════
const State01_Default = ({ compact }) => (
  <Screen compact={compact}>
    <StatsHero compact={compact}/>
    <SectionGrid compact={compact}>
      <StatsSection entity="game" icon="🎲" title="Giochi più giocati" ariaLabel="Giochi più giocati per numero di partite" meta={`top ${STATS.mostPlayed.length}`} compact={compact}>
        <MostPlayed items={STATS.mostPlayed}/>
      </StatsSection>
      <StatsSection entity="toolkit" icon="🏆" title="Win-rate per gioco" ariaLabel="Win-rate per gioco ordinato per percentuale" meta="ordinato per %" compact={compact}>
        <WinByGame items={STATS.winByGame}/>
      </StatsSection>
    </SectionGrid>
  </Screen>
);

// ═══════════════════════════════════════════════════════
// ─── STATO 02 · EMPTY ──────────────────────────────────
// state-02-empty — utente nuovo, zero partite. Banner info role="status",
// KPI a 0/—/n.d. (placeholder), sezioni sostituite da empty-state card + CTA.
// ═══════════════════════════════════════════════════════
const State02_Empty = ({ compact }) => (
  <Screen compact={compact}>
    <EmptyInfoBanner compact={compact}/>
    <StatsHero compact={compact} empty/>
    <SectionGrid compact={compact}>
      <EmptyDashCard compact={compact}/>
    </SectionGrid>
  </Screen>
);

// ═══════════════════════════════════════════════════════
// ─── STATO 03 · LOADING ────────────────────────────────
// state-03-loading — fetch aggregati in corso. aria-busy sul wrapper,
// screen-reader span, skeleton aria-hidden. Pulse 2s / snap reduced-motion.
// ═══════════════════════════════════════════════════════
const State03_Loading = ({ compact }) => (
  <Screen compact={compact}>
    <div aria-busy="true" style={{ position:'relative' }}>
      <span style={{ position:'absolute', width: 1, height: 1, padding: 0, margin: -1, overflow:'hidden', clip:'rect(0 0 0 0)', whiteSpace:'nowrap', border: 0 }}>
        Caricamento statistiche partite…
      </span>
      <HeroSkel compact={compact}/>
      <SectionGrid compact={compact}>
        <SectionSkel rows={5} compact={compact}/>
        <SectionSkel rows={5} compact={compact}/>
      </SectionGrid>
    </div>
  </Screen>
);

// ═══════════════════════════════════════════════════════
// ─── STATO 04 · ERROR ──────────────────────────────────
// state-04-error — banner full-width role="alert" + retry, area vuota con
// link torna-lista, footer dismiss.
// ═══════════════════════════════════════════════════════
const State04_Error = ({ compact }) => (
  <Screen compact={compact}>
    <ErrorBanner compact={compact}/>
    <div style={{ flex: 1, padding: compact ? '28px 16px' : '56px 32px', display:'flex', alignItems:'flex-start', justifyContent:'center' }}>
      <div style={{ display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center', maxWidth: 360 }}>
        <div aria-hidden="true" style={{ width: 54, height: 54, borderRadius:'50%', background:'var(--bg-muted)', display:'flex', alignItems:'center', justifyContent:'center', fontSize: 24, marginBottom: 12 }}>🃏</div>
        <div style={{ fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700, color:'var(--text-sec)', marginBottom: 4 }}>Nessun dato disponibile</div>
        <p style={{ fontFamily:'var(--f-body)', fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 16px', lineHeight: 1.5 }}>Le statistiche verranno mostrate qui una volta ripristinata la connessione.</p>
        <a href="sp4-play-records-index.html" aria-label="Torna alla lista partite" style={{ padding:'8px 14px', borderRadius:'var(--r-md)', background:'transparent', color: entityHsl('session'), border:`1px solid ${entityHsl('session', 0.4)}`, fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800 }}>Torna alla lista partite</a>
      </div>
    </div>
    <div style={{ padding: compact ? '12px 16px' : '14px 32px', borderTop:'1px solid var(--border-light)', display:'flex', justifyContent:'center' }}>
      <button type="button" style={{ background:'transparent', border:'none', color:'var(--text-muted)', fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700, cursor:'pointer', textDecoration:'underline', textUnderlineOffset: 3 }}>Chiudi</button>
    </div>
  </Screen>
);

// ═══════════════════════════════════════════════════════
// ─── STATO 05 · SSE — SKIPPED ──────────────────────────
// La dashboard /play-records/stats NON è SSE-driven: gli aggregati sono
// fetch-once (refresh manuale), non uno stream di eventi live. Nessun
// State05_SSE renderizzato (cfr. G7 SessionStateRenderer: lo stato `sse` si
// applica solo agli hub con sottoscrizione eventi live).
// ═══════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════
// ─── GALLERY (scaffold allineato a sp4-play-records-index) ──
// ═══════════════════════════════════════════════════════
const MobileFrame = ({ Comp }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 8 }}>
    <div className="frame-tag">Mobile · 375</div>
    <div className="phone"><Comp compact/></div>
  </div>
);

const DesktopFrame = ({ Comp }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 8, flex: 1, minWidth: 0 }}>
    <div className="frame-tag">Desktop · 1440</div>
    <div className="desktop-frame">
      <div className="desktop-bar"><span className="traffic"/><span className="traffic"/><span className="traffic"/><span className="url">meepleai.app/play-records/stats</span></div>
      <div style={{ display:'flex', flexDirection:'column', minHeight: 640, background:'var(--bg)' }}><Comp/></div>
    </div>
  </div>
);

const StateMatrix = ({ Comp }) => (
  <div className="matrix"><div className="matrix-row"><MobileFrame Comp={Comp}/><DesktopFrame Comp={Comp}/></div></div>
);

const STATES = [
  { id:'state-01-default', num:'01', title:'Default', sub:'Dashboard completa as-shipped: KPI strip (89 partite · 9 giochi · 47% win-rate · preferito) + hero session 🎯 + Giochi più giocati + Win-rate per gioco. Stato base, invariato.', Comp: State01_Default },
  { id:'state-02-empty',   num:'02', title:'Empty',   sub:'Utente nuovo, zero partite. Banner info role="status", KPI a 0/—/n.d., sezioni sostituite da empty-state card 📊 + CTA "Registra prima partita".', Comp: State02_Empty },
  { id:'state-03-loading', num:'03', title:'Loading', sub:'Fetch aggregati in corso: skeleton KPI strip + hero + 2 sezioni (pulse 2s, snap con reduced-motion). aria-busy + screen-reader span.', Comp: State03_Loading },
  { id:'state-04-error',   num:'04', title:'Error',   sub:'Errore fetch (role="alert") con retry inline, area vuota 🃏 + link "Torna alla lista partite", footer "Chiudi".', Comp: State04_Error },
];

const NAV = [
  { id:'state-01-default', label:'01 · Default' },
  { id:'state-02-empty',   label:'02 · Empty' },
  { id:'state-03-loading', label:'03 · Loading' },
  { id:'state-04-error',   label:'04 · Error' },
];

function ThemeToggle() {
  const initial = (() => { try { return localStorage.getItem('sp4-pr-theme') === 'dark'; } catch (e) { return false; } })();
  const [dark, setDark] = useState(initial);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    try { localStorage.setItem('sp4-pr-theme', dark ? 'dark' : 'light'); } catch (e) {}
  }, [dark]);
  return (
    <button type="button" className="theme-toggle" onClick={() => setDark(d => !d)} aria-pressed={dark}
      aria-label={dark ? 'Passa a tema chiaro' : 'Passa a tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function GalleryNav() {
  return (
    <nav className="gallery-nav" aria-label="Stati canonici">
      <div className="gallery-nav-brand"><span aria-hidden="true">📊</span> SP4 · /play-records/stats</div>
      <div className="gallery-nav-links">
        {NAV.map(n => <a key={n.id} href={`#${n.id}`}>{n.label}</a>)}
      </div>
      <a className="gallery-nav-ghost" href="#state-05-sse-skipped" aria-disabled="true" title="state-05-sse: skipped — dashboard non SSE-driven (fetch-once aggregate)">05 · SSE · skip</a>
      <ThemeToggle/>
    </nav>
  );
}

function StateSection({ id, num, title, sub, Comp }) {
  return (
    <section id={id} className="state-section" data-screen-label={id}>
      <header className="state-head">
        <div className="state-num">{num}</div>
        <div className="state-head-text">
          <h2>{title}</h2>
          <p>{sub}</p>
        </div>
        <code className="state-anchor">#{id}</code>
      </header>
      <StateMatrix Comp={Comp}/>
    </section>
  );
}

function App() {
  return (
    <div className="gallery">
      <GalleryNav/>
      <div className="gallery-body">
        <header className="gallery-intro">
          <div className="kicker">SP4 · /play-records/stats · Stats 📊 — canonical states</div>
          <h1>Statistiche — Stati canonici</h1>
          <p className="lead">
            Dashboard statistiche partite allineata al pattern <strong>G7 SessionStateRenderer</strong> (PR 2357).
            4 stati canonici × viewport mobile&nbsp;375 / desktop&nbsp;1440, × tema light/dark via toggle.
            Entity dominante <strong>session 🎯</strong>; colori esclusivamente da token <code>--c-*</code> via <code>entityHsl()</code>.
            Lo stato <code>state-05-sse</code> è intenzionalmente <strong>saltato</strong> (dashboard non SSE-driven, fetch-once aggregate).
          </p>
        </header>

        {STATES.map(s => <StateSection key={s.id} {...s}/>)}
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
