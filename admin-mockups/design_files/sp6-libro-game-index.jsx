/* ===================================================================
   SP6 Libro Game — Index (sp6-libro-game-index.jsx)
   Route: /gamebook
   Descrizione: Lista manuali fotografati + quota mensile + CTA "+ Aggiungi manuale".
   Persona: Sara, 32, casual italian gamer / GM al tavolo (mobile-first).

   Stati implementati: default · empty · loading · error · quota soft (47/50) · quota hard (50/50)
   =================================================================== */

const { useState, useEffect } = React;

/* ─── entityHsl helper (replica apps/web/src/lib/theme/entity-hsl.ts) ─── */
const ENTITY_HSL = {
  game:    '25 95% 45%',
  player:  '262 83% 58%',
  session: '240 60% 55%',
  agent:   '38 92% 50%',
  kb:      '174 60% 40%',
  chat:    '220 80% 55%',
  event:   '350 89% 60%',
  toolkit: '142 70% 45%',
  tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;

/* ─── Mock data: 4 gamebook reali ─── */
const MOCK_GAMEBOOKS = [
  {
    id: 'gb-tainted-grail',
    title: 'Tainted Grail: The Fall of Avalon',
    publisher: 'Awaken Realms',
    year: 2019,
    pages: 47, totalPages: 47, chunks: 142,
    status: 'ready', // ready | indexing | error
    cover: ['hsl(350 60% 22%)', 'hsl(350 80% 38%)'],
    emoji: '⚔️',
    qaCount: 12, sessionsCount: 3,
  },
  {
    id: 'gb-iss-vanguard',
    title: 'ISS Vanguard',
    publisher: 'Awaken Realms',
    year: 2023,
    pages: 24, totalPages: 50, chunks: 68,
    status: 'indexing',
    cover: ['hsl(220 70% 22%)', 'hsl(195 80% 50%)'],
    emoji: '🚀',
    qaCount: 0, sessionsCount: 0,
  },
  {
    id: 'gb-stuffed-fables',
    title: 'Stuffed Fables',
    publisher: 'Plaid Hat Games',
    year: 2018,
    pages: 38, totalPages: 38, chunks: 96,
    status: 'ready',
    cover: ['hsl(28 80% 38%)', 'hsl(38 92% 60%)'],
    emoji: '🧸',
    qaCount: 7, sessionsCount: 2,
  },
  {
    id: 'gb-andor-chronicles',
    title: 'Andor Chronicles',
    publisher: 'Kosmos',
    year: 2021,
    pages: 12, totalPages: 42, chunks: 0,
    status: 'error',
    cover: ['hsl(142 40% 22%)', 'hsl(174 60% 40%)'],
    emoji: '🏰',
    qaCount: 0, sessionsCount: 0,
    errorMsg: 'OCR confidence troppo bassa — luce insufficiente',
  },
];

/* ─── ConfidenceBadge / status helpers ─── */
function StatusPill({ status, indexed, total, onRetry }) {
  if (status === 'ready') {
    return (
      <span className="lg-status-pill" style={{
        background: entityHsl('toolkit', 0.12),
        color: entityHsl('toolkit'),
      }}>
        <span style={{
          width: 6, height: 6, borderRadius: '50%',
          background: entityHsl('toolkit'),
        }}/>
        Pronto
      </span>
    );
  }
  if (status === 'indexing') {
    return (
      <span className="lg-status-pill" style={{
        background: entityHsl('game', 0.12),
        color: entityHsl('game'),
      }}>
        <span className="lg-spinner" aria-hidden="true" style={{
          borderColor: `${entityHsl('game', 0.25)} ${entityHsl('game', 0.25)} ${entityHsl('game', 0.25)} ${entityHsl('game')}`,
        }}/>
        Indicizzazione… {indexed}/{total}
      </span>
    );
  }
  if (status === 'error') {
    return (
      <button className="lg-status-pill" onClick={onRetry} style={{
        background: entityHsl('event', 0.12),
        color: entityHsl('event'),
        border: 'none', cursor: 'pointer', font: 'inherit',
      }}>
        ⚠️ Errore · Riprova
      </button>
    );
  }
  return null;
}

/* ─── ConnectionPip strip (per gamebook card) ─── */
function PipStrip({ kbCount, qaCount, sessionsCount }) {
  const pips = [
    { entity: 'kb',      icon: '📄', count: kbCount,      label: 'Pagine indicizzate' },
    { entity: 'chat',    icon: '💬', count: qaCount,      label: 'Domande' },
    { entity: 'session', icon: '🎯', count: sessionsCount,label: 'Sessioni' },
  ].filter(p => p.count > 0);

  if (pips.length === 0) return null;

  return (
    <div className="lg-pip-strip" aria-label="Connessioni entità">
      {pips.map(p => (
        <span
          key={p.entity}
          className="lg-pip"
          aria-label={`${p.label}: ${p.count}`}
          style={{
            background: entityHsl(p.entity, 0.1),
            color: entityHsl(p.entity),
          }}
        >
          <span aria-hidden="true">{p.icon}</span>
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>{p.count}</span>
        </span>
      ))}
    </div>
  );
}

/* ─── Gamebook Card (MeepleCard variant=grid placeholder) ─── */
function GamebookCard({ gb, compact }) {
  return (
    <article
      className={`lg-gb-card ${compact ? 'compact' : ''}`}
      style={{
        background: 'var(--bg-card)',
        border: '1px solid var(--border)',
        borderRadius: 'var(--r-xl)',
        boxShadow: 'var(--shadow-sm)',
      }}
    >
      <div
        className="lg-gb-cover"
        style={{
          background: `linear-gradient(155deg, ${gb.cover[0]} 0%, ${gb.cover[1]} 100%)`,
        }}
      >
        <span className="lg-gb-cover-em" aria-hidden="true">{gb.emoji}</span>
        <span className="lg-gb-cover-pages" style={{ fontVariantNumeric: 'tabular-nums' }}>
          {gb.pages}/{gb.totalPages} pag
        </span>
      </div>
      <div className="lg-gb-body">
        <h3 className="lg-gb-title" title={gb.title}>{gb.title}</h3>
        <p className="lg-gb-meta">
          <span style={{ fontFamily: 'var(--f-mono)' }}>{gb.publisher}</span>
          <span style={{ opacity: 0.5 }}> · </span>
          <span style={{ fontVariantNumeric: 'tabular-nums', fontFamily: 'var(--f-mono)' }}>{gb.year}</span>
        </p>
        <div className="lg-gb-footer">
          <StatusPill
            status={gb.status}
            indexed={gb.pages}
            total={gb.totalPages}
          />
          {gb.status === 'ready' && (
            <span className="lg-gb-counter" style={{ fontVariantNumeric: 'tabular-nums' }}>
              {gb.chunks} chunks
            </span>
          )}
        </div>
        {gb.status === 'ready' && (
          <PipStrip
            kbCount={gb.chunks}
            qaCount={gb.qaCount}
            sessionsCount={gb.sessionsCount}
          />
        )}
        {gb.status === 'error' && (
          <p className="lg-gb-error-msg">{gb.errorMsg}</p>
        )}
      </div>
    </article>
  );
}

/* ─── Quota Widget ─── */
function QuotaWidget({ used, total, resetDate, variant = 'normal' }) {
  // variant: normal | soft (≥90%) | hard (=100%)
  const pct = Math.min(100, Math.round((used / total) * 100));
  const isHard = used >= total;
  const isSoft = !isHard && pct >= 90;
  const fillEntity = isSoft || isHard ? 'event' : 'toolkit';

  return (
    <div
      className={`lg-quota ${variant}`}
      style={{
        background: 'var(--bg-card)',
        border: `1px solid ${isHard ? entityHsl('event', 0.4) : 'var(--border)'}`,
        borderRadius: 'var(--r-lg)',
      }}
      role="status"
      aria-label={`Quota traduzioni: ${used} di ${total}`}
    >
      <div className="lg-quota-row">
        <span className="lg-quota-label">
          <span aria-hidden="true">📊</span>
          <span>Quota traduzioni</span>
        </span>
        <span
          className="lg-quota-counter"
          style={{
            fontVariantNumeric: 'tabular-nums',
            color: isHard ? entityHsl('event') : 'var(--text)',
          }}
        >
          {used} / {total}
        </span>
      </div>
      <div className="lg-quota-bar" aria-hidden="true">
        <div
          className="lg-quota-fill"
          style={{
            width: pct + '%',
            background: entityHsl(fillEntity, 0.7),
          }}
        />
      </div>
      <div className="lg-quota-footer">
        <span className="lg-quota-reset">
          {isHard ? 'Si resetta il 1° giugno' : `Resetta il 1° giugno`}
        </span>
        {(isSoft || isHard) && (
          <a href="#" className="lg-quota-buy" style={{ color: entityHsl('event') }}>
            Acquista crediti →
          </a>
        )}
      </div>
    </div>
  );
}

/* ─── Hard limit banner (sticky top) ─── */
function HardLimitBanner() {
  return (
    <div
      role="alert"
      className="lg-hard-banner"
      style={{
        background: entityHsl('event', 0.12),
        borderBottom: `1px solid ${entityHsl('event', 0.3)}`,
        color: 'var(--text)',
      }}
    >
      <span aria-hidden="true" style={{ fontSize: 16 }}>⚠️</span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div className="lg-hard-banner-title">Quota raggiunta</div>
        <div className="lg-hard-banner-sub">
          50/50 paragrafi tradotti questo mese. Acquista crediti per continuare.
        </div>
      </div>
      <a
        href="#"
        className="lg-hard-banner-cta"
        style={{
          background: entityHsl('event'),
          color: '#fff',
        }}
      >
        💎 Crediti
      </a>
    </div>
  );
}

/* ─── Skeleton card (loading) ─── */
function SkeletonCard() {
  return (
    <div className="lg-gb-card lg-skeleton-card" aria-hidden="true">
      <div className="lg-gb-cover lg-skeleton-shimmer"/>
      <div className="lg-gb-body">
        <div className="lg-skeleton-line lg-skeleton-shimmer" style={{ width: '80%', height: 18 }}/>
        <div className="lg-skeleton-line lg-skeleton-shimmer" style={{ width: '50%', height: 12, marginTop: 8 }}/>
        <div className="lg-skeleton-line lg-skeleton-shimmer" style={{ width: '40%', height: 22, marginTop: 12, borderRadius: 12 }}/>
      </div>
    </div>
  );
}

/* ─── Hero (compact, mobile + desktop variant) ─── */
function Hero({ desktop, kpis, onAdd }) {
  return (
    <header className={`lg-hero ${desktop ? 'desktop' : 'mobile'}`}>
      <div className="lg-hero-top">
        <div>
          <p className="lg-hero-kicker">📚 Libri game</p>
          <h1 className="lg-hero-title">
            Ciao Sara, <span style={{ color: entityHsl('game') }}>i tuoi manuali</span>
          </h1>
        </div>
        <div
          className="lg-hero-avatar"
          aria-label="Profilo Sara"
          style={{
            background: `linear-gradient(135deg, ${entityHsl('player')}, ${entityHsl('agent')})`,
          }}
        >
          S
        </div>
      </div>
      <div className="lg-hero-kpis">
        {kpis.map(k => (
          <div key={k.label} className="lg-kpi">
            <span
              className="lg-kpi-value"
              style={{ fontVariantNumeric: 'tabular-nums', color: k.color }}
            >
              {k.value}
            </span>
            <span className="lg-kpi-label">{k.label}</span>
          </div>
        ))}
      </div>
      <button
        type="button"
        onClick={onAdd}
        className="lg-hero-cta"
        style={{
          background: entityHsl('game'),
          color: '#fff',
          boxShadow: `0 6px 20px ${entityHsl('game', 0.35)}`,
        }}
      >
        <span aria-hidden="true">＋</span>
        <span>Aggiungi manuale</span>
      </button>
    </header>
  );
}

/* ─── Empty state ─── */
function EmptyState({ desktop }) {
  return (
    <div className={`lg-empty ${desktop ? 'desktop' : ''}`}>
      <div
        className="lg-empty-illu"
        style={{
          background: `linear-gradient(155deg, ${entityHsl('game', 0.18)}, ${entityHsl('event', 0.14)})`,
          border: `1px dashed ${entityHsl('game', 0.4)}`,
        }}
        aria-hidden="true"
      >
        <span style={{ fontSize: 48 }}>📖</span>
        <span style={{ fontSize: 32, marginLeft: -12 }}>🎲</span>
      </div>
      <h2 className="lg-empty-title">Nessun manuale ancora</h2>
      <p className="lg-empty-sub">
        Fotografa il tuo primo gamebook per iniziare a giocare in italiano.
      </p>
      <button
        type="button"
        className="lg-empty-cta"
        style={{
          background: entityHsl('game'),
          color: '#fff',
          boxShadow: `0 6px 20px ${entityHsl('game', 0.35)}`,
        }}
      >
        📷 Scatta il primo manuale
      </button>
      <a href="#" className="lg-empty-link">Come funziona →</a>
    </div>
  );
}

/* ─── Error state ─── */
function ErrorState({ onRetry, desktop }) {
  return (
    <div className={`lg-error ${desktop ? 'desktop' : ''}`} role="alert">
      <div
        className="lg-error-illu"
        style={{
          background: entityHsl('event', 0.14),
          border: `1px solid ${entityHsl('event', 0.4)}`,
        }}
      >
        <span style={{ fontSize: 32 }} aria-hidden="true">⚠️</span>
      </div>
      <h2 className="lg-error-title">Impossibile caricare la libreria</h2>
      <p className="lg-error-sub">
        Connessione instabile. Riprova fra un momento.
      </p>
      <button
        type="button"
        onClick={onRetry}
        className="lg-error-cta"
        style={{
          background: entityHsl('event'),
          color: '#fff',
        }}
      >
        🔄 Riprova
      </button>
    </div>
  );
}

/* ─── IndexScreen — main composition with state variant ─── */
function IndexScreen({ state, desktop }) {
  const isHard = state === 'quota-hard';
  const isSoft = state === 'quota-soft';
  const used = isHard ? 50 : isSoft ? 47 : 12;

  const kpis = [
    { label: 'manuali', value: state === 'empty' ? '0' : '4', color: entityHsl('game') },
    { label: 'questo mese', value: state === 'empty' ? '0' : '2', color: entityHsl('session') },
    { label: `traduzioni`, value: `${used}/50`, color: isHard ? entityHsl('event') : isSoft ? entityHsl('event') : entityHsl('toolkit') },
  ];

  /* Loading: skeleton hero + 4 skeleton cards */
  if (state === 'loading') {
    return (
      <div className="lg-screen">
        <div className="lg-screen-pad">
          <header className="lg-hero mobile">
            <div className="lg-hero-top">
              <div style={{ flex: 1 }}>
                <div className="lg-skeleton-line lg-skeleton-shimmer" style={{ width: 100, height: 12 }}/>
                <div className="lg-skeleton-line lg-skeleton-shimmer" style={{ width: '70%', height: 26, marginTop: 8 }}/>
              </div>
              <div className="lg-skeleton-line lg-skeleton-shimmer" style={{ width: 40, height: 40, borderRadius: '50%' }}/>
            </div>
            <div className="lg-skeleton-line lg-skeleton-shimmer" style={{ width: '100%', height: 60, marginTop: 14, borderRadius: 12 }}/>
          </header>
          <div className={`lg-grid ${desktop ? 'desktop' : ''}`} style={{ marginTop: 20 }}>
            {[0,1,2,3].map(i => <SkeletonCard key={i}/>)}
          </div>
        </div>
      </div>
    );
  }

  if (state === 'error') {
    return (
      <div className="lg-screen">
        <div className="lg-screen-pad">
          <ErrorState desktop={desktop} onRetry={() => {}}/>
        </div>
      </div>
    );
  }

  if (state === 'empty') {
    return (
      <div className="lg-screen">
        <div className="lg-screen-pad">
          <Hero desktop={desktop} kpis={kpis} onAdd={() => {}}/>
          <EmptyState desktop={desktop}/>
        </div>
      </div>
    );
  }

  /* Default + quota soft + quota hard */
  return (
    <div className="lg-screen">
      {isHard && <HardLimitBanner/>}
      <div className="lg-screen-pad">
        <Hero desktop={desktop} kpis={kpis} onAdd={() => {}}/>
        <QuotaWidget
          used={used}
          total={50}
          variant={isHard ? 'hard' : isSoft ? 'soft' : 'normal'}
        />
        <h2 className="lg-section-h2">
          I tuoi manuali
          <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', fontWeight: 400, marginLeft: 8 }}>
            ({MOCK_GAMEBOOKS.length})
          </span>
        </h2>
        <div className={`lg-grid ${desktop ? 'desktop' : ''}`}>
          {MOCK_GAMEBOOKS.map(gb => <GamebookCard key={gb.id} gb={gb}/>)}
        </div>
      </div>
    </div>
  );
}

/* ─── Bottom-bar mobile (pattern shell mobile-app.jsx) ─── */
function MobileBottomBar() {
  const tabs = [
    { icon: '🏠', label: 'Home', active: false },
    { icon: '🔍', label: 'Cerca', active: false },
    { icon: '📚', label: 'Libreria', active: true, entity: 'game' },
    { icon: '💬', label: 'Chat', active: false },
    { icon: '👤', label: 'Profilo', active: false },
  ];
  return (
    <nav className="lg-bottombar" aria-label="Navigazione principale">
      {tabs.map(t => (
        <button
          key={t.label}
          type="button"
          className={`lg-bb-tab ${t.active ? 'active' : ''}`}
          aria-current={t.active ? 'page' : undefined}
          style={{
            color: t.active ? entityHsl(t.entity || 'game') : 'var(--text-muted)',
          }}
        >
          <span aria-hidden="true" className="lg-bb-icon">{t.icon}</span>
          <span className="lg-bb-label">{t.label}</span>
        </button>
      ))}
    </nav>
  );
}

/* ─── Desktop sidebar nav ─── */
function DesktopSidebar() {
  const items = [
    { icon: '🏠', label: 'Home' },
    { icon: '🔍', label: 'Cerca' },
    { icon: '📚', label: 'Libri game', active: true, entity: 'game' },
    { icon: '🎲', label: 'Giochi' },
    { icon: '💬', label: 'Chat' },
    { icon: '🎯', label: 'Sessioni' },
    { icon: '🤖', label: 'Agenti' },
  ];
  return (
    <aside className="lg-sidebar">
      <div className="lg-sidebar-brand">
        <span className="brand-mark">M</span>
        <span style={{ fontFamily: 'var(--f-display)', fontWeight: 700 }}>MeepleAI</span>
      </div>
      <nav>
        {items.map(it => (
          <a
            key={it.label}
            href="#"
            className={`lg-sidebar-item ${it.active ? 'active' : ''}`}
            style={it.active ? {
              background: entityHsl(it.entity, 0.12),
              color: entityHsl(it.entity),
            } : {}}
          >
            <span aria-hidden="true" style={{ fontSize: 18 }}>{it.icon}</span>
            <span>{it.label}</span>
          </a>
        ))}
      </nav>
      <div className="lg-sidebar-footer">
        <div
          className="lg-sidebar-avatar"
          style={{ background: `linear-gradient(135deg, ${entityHsl('player')}, ${entityHsl('agent')})` }}
        >S</div>
        <div style={{ minWidth: 0 }}>
          <div style={{ fontSize: 13, fontWeight: 600 }}>Sara</div>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>Free tier</div>
        </div>
      </div>
    </aside>
  );
}

/* ─── Mobile / Desktop frames ─── */
function MobileFrame({ state, label }) {
  return (
    <div>
      <div className="lg-frame-caption">📱 Mobile 375 — {label}</div>
      <div className="lg-phone">
        <div className="lg-phone-sbar">
          <span>9:41</span>
          <span className="ind">▲ ◆ ▶</span>
        </div>
        <div className="lg-phone-body">
          <IndexScreen state={state} desktop={false}/>
          <div style={{ height: 80 }}/>
        </div>
        <MobileBottomBar/>
      </div>
    </div>
  );
}
function DesktopFrame({ state, label }) {
  return (
    <div style={{ width: '100%' }}>
      <div className="lg-frame-caption">🖥️ Desktop 1440 — {label}</div>
      <div className="lg-desktop">
        <div className="lg-desktop-bar">
          <span className="traffic"/><span className="traffic"/><span className="traffic"/>
          <span className="url">meepleai.app/gamebook</span>
        </div>
        <div className="lg-desktop-body">
          <div style={{ display: 'flex', minHeight: '100%' }}>
            <DesktopSidebar/>
            <main style={{ flex: 1, minWidth: 0 }}>
              <IndexScreen state={state} desktop={true}/>
            </main>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ─── App: state grid ─── */
const STATES = [
  { id: 'default',    label: 'Default (4 manuali)' },
  { id: 'empty',      label: 'Empty (G1 nuovo utente)' },
  { id: 'loading',    label: 'Loading skeleton' },
  { id: 'error',      label: 'Error globale' },
  { id: 'quota-soft', label: 'Quota soft 47/50' },
  { id: 'quota-hard', label: 'Quota hard 50/50 (banner)' },
];

function App() {
  const [stateFilter, setStateFilter] = useState('all');
  const [frameFilter, setFrameFilter] = useState('both');

  useEffect(() => {
    const sh = e => setStateFilter(e.detail);
    const fh = e => setFrameFilter(e.detail);
    document.addEventListener('lg-tweak-state', sh);
    document.addEventListener('lg-tweak-frame', fh);
    return () => {
      document.removeEventListener('lg-tweak-state', sh);
      document.removeEventListener('lg-tweak-frame', fh);
    };
  }, []);

  const visible = stateFilter === 'all' ? STATES : STATES.filter(s => s.id === stateFilter);

  return (
    <div>
      {visible.map(s => (
        <section key={s.id}>
          <div className="lg-section-label">— {s.label}</div>
          <div className="lg-state-grid">
            {(frameFilter === 'both' || frameFilter === 'mobile') && (
              <MobileFrame state={s.id} label={s.label}/>
            )}
            {(frameFilter === 'both' || frameFilter === 'desktop') && (
              <DesktopFrame state={s.id} label={s.label}/>
            )}
          </div>
        </section>
      ))}
    </div>
  );
}

/* ─── Inline component-scoped styles ─── */
const styleEl = document.createElement('style');
styleEl.textContent = `
  /* Screen container */
  .lg-screen { display: flex; flex-direction: column; min-height: 100%; }
  .lg-screen-pad { padding: 20px 16px 24px; display: flex; flex-direction: column; gap: 18px; }
  .lg-desktop .lg-screen-pad { padding: 32px 40px 48px; gap: 24px; max-width: 1100px; margin: 0 auto; width: 100%; }

  /* Hero */
  .lg-hero { display: flex; flex-direction: column; gap: 14px; }
  .lg-hero-top { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
  .lg-hero-kicker {
    font-family: var(--f-mono); font-size: 10px;
    color: var(--text-muted); text-transform: uppercase;
    letter-spacing: 0.1em; margin: 0 0 4px;
  }
  .lg-hero-title {
    font-family: var(--f-display); font-weight: 700;
    font-size: 26px; line-height: 1.15; letter-spacing: -0.01em;
    color: var(--text); margin: 0;
  }
  .lg-hero.desktop .lg-hero-title { font-size: 36px; }
  .lg-hero-avatar {
    width: 40px; height: 40px; border-radius: 50%;
    color: #fff; font-family: var(--f-display); font-weight: 800;
    font-size: 16px; display: flex; align-items: center; justify-content: center;
    flex-shrink: 0;
  }

  .lg-hero-kpis {
    display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px;
    background: var(--bg-card); border: 1px solid var(--border);
    border-radius: var(--r-lg); padding: 12px;
  }
  .lg-kpi { display: flex; flex-direction: column; gap: 2px; }
  .lg-kpi-value {
    font-family: var(--f-display); font-weight: 800; font-size: 20px;
    line-height: 1; letter-spacing: -0.01em;
  }
  .lg-hero.desktop .lg-kpi-value { font-size: 26px; }
  .lg-kpi-label {
    font-family: var(--f-mono); font-size: 10px;
    text-transform: uppercase; letter-spacing: 0.06em;
    color: var(--text-muted); margin-top: 4px;
  }

  .lg-hero-cta {
    display: inline-flex; align-items: center; gap: 6px;
    padding: 12px 18px; border-radius: var(--r-lg);
    font-family: var(--f-display); font-weight: 700; font-size: 14px;
    border: none; cursor: pointer;
    align-self: flex-start;
    transition: transform var(--dur-sm) var(--ease-out), box-shadow var(--dur-sm) var(--ease-out);
  }
  .lg-hero-cta:hover { transform: translateY(-1px); }

  /* Quota widget */
  .lg-quota { padding: 14px 16px; display: flex; flex-direction: column; gap: 10px; }
  .lg-quota-row { display: flex; align-items: center; justify-content: space-between; }
  .lg-quota-label {
    display: inline-flex; align-items: center; gap: 6px;
    font-family: var(--f-display); font-weight: 600; font-size: 13px; color: var(--text);
  }
  .lg-quota-counter {
    font-family: var(--f-mono); font-size: 14px; font-weight: 700;
  }
  .lg-quota-bar {
    height: 8px; border-radius: var(--r-pill);
    background: var(--bg-muted); overflow: hidden;
  }
  .lg-quota-fill {
    height: 100%; border-radius: var(--r-pill);
    transition: width var(--dur-md) var(--ease-out);
  }
  .lg-quota-footer {
    display: flex; align-items: center; justify-content: space-between; gap: 8px;
  }
  .lg-quota-reset {
    font-family: var(--f-mono); font-size: 10px; color: var(--text-muted);
    text-transform: uppercase; letter-spacing: 0.06em;
  }
  .lg-quota-buy {
    font-family: var(--f-display); font-size: 12px; font-weight: 700;
  }

  /* Hard limit banner */
  .lg-hard-banner {
    display: flex; align-items: center; gap: 12px;
    padding: 10px 16px;
    position: sticky; top: 0; z-index: 5;
    backdrop-filter: blur(8px);
  }
  .lg-hard-banner-title { font-family: var(--f-display); font-weight: 700; font-size: 13px; }
  .lg-hard-banner-sub { font-size: 11px; color: var(--text-sec); margin-top: 1px; }
  .lg-hard-banner-cta {
    flex-shrink: 0;
    padding: 6px 12px; border-radius: var(--r-md);
    font-family: var(--f-display); font-weight: 700; font-size: 12px;
    text-decoration: none;
  }

  /* Section h2 */
  .lg-section-h2 {
    font-family: var(--f-display); font-weight: 700; font-size: 18px;
    color: var(--text); margin: 4px 0 0;
  }
  .lg-desktop .lg-section-h2 { font-size: 22px; }

  /* Grid */
  .lg-grid {
    display: grid; grid-template-columns: 1fr; gap: 14px;
  }
  .lg-grid.desktop {
    grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
    gap: 18px;
  }

  /* Gamebook card */
  .lg-gb-card {
    overflow: hidden; display: flex; flex-direction: column;
    transition: transform var(--dur-sm) var(--ease-out), box-shadow var(--dur-sm) var(--ease-out);
    cursor: pointer;
  }
  .lg-gb-card:hover { transform: translateY(-2px); box-shadow: var(--shadow-md); }

  .lg-gb-cover {
    aspect-ratio: 16/9;
    position: relative; display: flex; align-items: center; justify-content: center;
    overflow: hidden;
  }
  .lg-gb-cover-em {
    font-size: 48px; filter: drop-shadow(0 4px 12px rgba(0,0,0,.4));
  }
  .lg-gb-cover-pages {
    position: absolute; top: 8px; right: 10px;
    font-family: var(--f-mono); font-size: 10px; font-weight: 700;
    color: rgba(255,255,255,.92);
    background: rgba(0,0,0,.35); padding: 3px 7px; border-radius: var(--r-sm);
    backdrop-filter: blur(4px);
  }

  .lg-gb-body { padding: 12px 14px 14px; display: flex; flex-direction: column; gap: 8px; }
  .lg-gb-title {
    font-family: var(--f-display); font-weight: 700;
    font-size: 15px; line-height: 1.25; color: var(--text);
    margin: 0;
    overflow: hidden; text-overflow: ellipsis;
    display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical;
  }
  .lg-gb-meta { font-size: 11px; color: var(--text-muted); margin: 0; }
  .lg-gb-footer {
    display: flex; align-items: center; justify-content: space-between; gap: 8px;
    flex-wrap: wrap; margin-top: 2px;
  }
  .lg-gb-counter {
    font-family: var(--f-mono); font-size: 10px; color: var(--text-muted);
    text-transform: uppercase; letter-spacing: 0.05em;
  }
  .lg-gb-error-msg {
    font-size: 11px; color: ${entityHsl('event')};
    margin: 0; padding: 6px 8px;
    background: ${entityHsl('event', 0.08)};
    border-radius: var(--r-sm);
  }

  /* Status pill */
  .lg-status-pill {
    display: inline-flex; align-items: center; gap: 5px;
    padding: 3px 8px; border-radius: var(--r-pill);
    font-family: var(--f-display); font-weight: 700; font-size: 10px;
    text-transform: uppercase; letter-spacing: 0.05em;
  }

  /* Spinner micro */
  .lg-spinner {
    width: 10px; height: 10px; border-radius: 50%;
    border: 1.5px solid;
    animation: lgSpin 0.8s linear infinite;
  }
  @keyframes lgSpin { to { transform: rotate(360deg); } }
  @media (prefers-reduced-motion: reduce) {
    .lg-spinner { animation: none; }
  }

  /* Pip strip */
  .lg-pip-strip { display: flex; gap: 5px; flex-wrap: wrap; }
  .lg-pip {
    display: inline-flex; align-items: center; gap: 4px;
    padding: 2px 7px; border-radius: var(--r-pill);
    font-family: var(--f-display); font-weight: 700; font-size: 10px;
  }

  /* Empty / Error */
  .lg-empty, .lg-error {
    display: flex; flex-direction: column; align-items: center; text-align: center;
    padding: 32px 20px; gap: 12px;
  }
  .lg-empty.desktop, .lg-error.desktop { padding: 48px 24px; }
  .lg-empty-illu, .lg-error-illu {
    width: 100px; height: 100px; border-radius: 24px;
    display: flex; align-items: center; justify-content: center;
    margin-bottom: 6px;
  }
  .lg-empty-title, .lg-error-title {
    font-family: var(--f-display); font-weight: 700; font-size: 20px;
    color: var(--text); margin: 0;
  }
  .lg-empty-sub, .lg-error-sub {
    font-size: 13px; color: var(--text-sec); max-width: 280px; margin: 0; line-height: 1.5;
  }
  .lg-empty-cta, .lg-error-cta {
    margin-top: 8px;
    padding: 12px 22px; border-radius: var(--r-lg);
    font-family: var(--f-display); font-weight: 700; font-size: 14px;
    border: none; cursor: pointer;
  }
  .lg-empty-link {
    font-family: var(--f-display); font-size: 12px; font-weight: 600;
    color: var(--text-sec); margin-top: 6px;
  }

  /* Skeleton */
  .lg-skeleton-shimmer {
    background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%);
    background-size: 200% 100%;
    animation: lgShimmer 1.6s ease-in-out infinite;
  }
  @keyframes lgShimmer { 0% { background-position: 200% 0; } 100% { background-position: -200% 0; } }
  @media (prefers-reduced-motion: reduce) {
    .lg-skeleton-shimmer { animation: none; background: var(--bg-muted); }
  }
  .lg-skeleton-line { border-radius: var(--r-sm); }
  .lg-skeleton-card .lg-gb-cover { aspect-ratio: 16/9; }

  /* Bottom bar mobile */
  .lg-bottombar {
    flex-shrink: 0; height: 64px;
    display: grid; grid-template-columns: repeat(5, 1fr);
    background: var(--bg-card); border-top: 1px solid var(--border);
    padding: 4px 0;
  }
  .lg-bb-tab {
    display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2px;
    background: none; border: none; cursor: pointer;
    font-family: var(--f-display); font-weight: 600;
  }
  .lg-bb-icon { font-size: 18px; }
  .lg-bb-label { font-size: 10px; }

  /* Desktop sidebar */
  .lg-sidebar {
    width: 220px; flex-shrink: 0;
    background: var(--bg-card); border-right: 1px solid var(--border);
    padding: 20px 12px; display: flex; flex-direction: column; gap: 12px;
  }
  .lg-sidebar-brand {
    display: flex; align-items: center; gap: 8px;
    padding: 0 8px 12px; border-bottom: 1px solid var(--border-light);
  }
  .lg-sidebar-item {
    display: flex; align-items: center; gap: 10px;
    padding: 8px 10px; border-radius: var(--r-md);
    font-family: var(--f-display); font-weight: 600; font-size: 13px;
    color: var(--text-sec);
  }
  .lg-sidebar-item:hover { background: var(--bg-hover); color: var(--text); }
  .lg-sidebar-footer {
    margin-top: auto; padding: 10px;
    display: flex; align-items: center; gap: 10px;
    border-top: 1px solid var(--border-light);
  }
  .lg-sidebar-avatar {
    width: 32px; height: 32px; border-radius: 50%;
    color: #fff; font-family: var(--f-display); font-weight: 800;
    font-size: 13px; display: flex; align-items: center; justify-content: center;
    flex-shrink: 0;
  }
`;
document.head.appendChild(styleEl);

/* ─── Mount ─── */
ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
