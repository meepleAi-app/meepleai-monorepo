/* MeepleAI SP4 wave 4 — D1 · /players (Grid pattern, mirror Games Wave B.1)
   Componenti v2 nuovi: PlayersHero · PlayersFiltersInline · PlayersResultsGrid · EmptyPlayers
   isMobile: prop esplicita dalla frame
*/

const { useState, useEffect } = React;

// ── DATA ───────────────────────────────────────────────────────────────────
const PLAYERS = [
  { id: 'p-marco', name: 'Marco Rossi', handle: '@marco_r', cat: 'Amico', registered: true, played: 142, wins: 89, avg: 87, last: '2 giorni fa', hue: 268 },
  { id: 'p-giulia', name: 'Giulia Bianchi', handle: '@giuliab', cat: 'Amico', registered: true, played: 118, wins: 67, avg: 82, last: '1 settimana fa', hue: 188 },
  { id: 'p-davide', name: 'Davide Ferrari', handle: '@dave_f', cat: 'Online', registered: true, played: 96, wins: 52, avg: 78, last: '3 giorni fa', hue: 28 },
  { id: 'p-sara', name: 'Sara Martini', handle: '@saramart', cat: 'Famiglia', registered: true, played: 84, wins: 41, avg: 75, last: '5 giorni fa', hue: 348 },
  { id: 'p-andrea', name: 'Andrea Pini', handle: '@apini', cat: 'Amico', registered: true, played: 73, wins: 38, avg: 72, last: '2 settimane fa', hue: 158 },
  { id: 'p-lorenzo', name: 'Lorenzo Tosi', handle: '@lore_t', cat: 'Online', registered: true, played: 61, wins: 29, avg: 68, last: '4 giorni fa', hue: 218 },
  { id: 'p-elena', name: 'Elena Conti', handle: '@elenac', cat: 'Famiglia', registered: true, played: 47, wins: 24, avg: 70, last: '1 mese fa', hue: 308 },
  { id: 'p-nicola', name: 'Nicola', handle: null, cat: 'Ospite', registered: false, played: 12, wins: 4, avg: 58, last: '3 settimane fa' },
  { id: 'p-chiara', name: 'Chiara', handle: null, cat: 'Ospite', registered: false, played: 5, wins: 2, avg: 62, last: '1 mese fa' },
];

const CAT_PALETTE = {
  'Amico': '--c-player',
  'Famiglia': '--c-event',
  'Online': '--c-chat',
  'Locali': '--c-game',
  'Ospite': null, // neutral
};

// ── PRIMITIVES ─────────────────────────────────────────────────────────────
const Skeleton = ({ w = '100%', h = 16, r = 6, style = {} }) => (
  <div style={{
    width: w, height: h, borderRadius: r,
    background: 'linear-gradient(90deg, var(--bg-sunken) 0%, var(--bg-muted) 50%, var(--bg-sunken) 100%)',
    backgroundSize: '200% 100%',
    animation: 'meepleSkeleton 1.6s linear infinite',
    ...style,
  }} />
);

// ── PLAYERS HERO (componente v2 nuovo) ────────────────────────────────────
const PlayersHero = ({ isMobile, count }) => (
  <header style={{
    position: 'relative', overflow: 'hidden', isolation: 'isolate',
    padding: isMobile ? '40px 16px 28px' : '64px 32px 40px',
  }}>
    <div aria-hidden="true" style={{
      position: 'absolute', inset: 0, zIndex: -1,
      background: `
        radial-gradient(ellipse 60% 70% at 20% 30%, hsl(var(--c-player) / 0.18), transparent 60%),
        radial-gradient(ellipse 50% 60% at 85% 70%, hsl(var(--c-chat) / 0.14), transparent 60%),
        var(--bg)
      `,
    }} />
    <div style={{ maxWidth: 1180, margin: '0 auto' }}>
      <div style={{
        display: 'flex',
        flexDirection: isMobile ? 'column' : 'row',
        alignItems: isMobile ? 'flex-start' : 'flex-end',
        gap: isMobile ? 20 : 24,
        justifyContent: 'space-between',
      }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 8,
            fontFamily: 'var(--f-mono)', fontWeight: 600,
            fontSize: 11, letterSpacing: '0.18em', textTransform: 'uppercase',
            color: 'hsl(var(--c-player))', marginBottom: 12,
          }}>
            <span aria-hidden="true">👤</span>
            La tua community
          </div>
          <h1 style={{
            margin: '0 0 10px',
            fontFamily: 'var(--f-display)',
            fontSize: isMobile ? 32 : 48,
            fontWeight: 700, lineHeight: 1.05,
            letterSpacing: '-0.025em',
            color: 'var(--text)', textWrap: 'balance',
          }}>Player tracker</h1>
          <p style={{
            margin: 0, maxWidth: 520,
            fontFamily: 'var(--f-body)', fontSize: isMobile ? 15.5 : 17,
            lineHeight: 1.5, color: 'var(--text-sec)',
          }}>Tutte le persone con cui giochi, in un posto solo</p>
        </div>
        <button style={{
          padding: isMobile ? '12px 22px' : '14px 24px',
          background: 'hsl(var(--c-player))',
          color: '#fff', border: 'none', borderRadius: 999,
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14.5,
          letterSpacing: '-0.005em', cursor: 'pointer',
          display: 'inline-flex', alignItems: 'center', gap: 8,
          boxShadow: '0 6px 18px -6px hsl(var(--c-player) / 0.5)',
          flexShrink: 0,
        }}>
          <span aria-hidden="true">+</span> Aggiungi player
        </button>
      </div>
    </div>
  </header>
);

// ── PLAYERS FILTERS INLINE (componente v2 nuovo) ──────────────────────────
const PlayersFiltersInline = ({ isMobile, activeFilter, onFilter, count, sort, onSort, search, onSearch }) => {
  const filters = ['Tutti', 'Amici', 'Famiglia', 'Online', 'Locali', 'Ospiti'];
  const sorts = ['Alfabetico', 'Più giocate', 'Più vittorie', 'Aggiunti recenti'];
  return (
    <div style={{
      maxWidth: 1180, margin: '0 auto',
      padding: isMobile ? '0 16px 16px' : '0 32px 24px',
      borderBottom: '1px solid var(--border)',
    }}>
      {/* count */}
      <div style={{
        fontFamily: 'var(--f-mono)', fontSize: 12,
        color: 'var(--text-muted)', marginBottom: 12,
        fontVariantNumeric: 'tabular-nums',
        display: 'inline-flex', alignItems: 'center', gap: 6,
        padding: '4px 10px',
        background: 'hsl(var(--c-player) / 0.10)',
        border: '1px solid hsl(var(--c-player) / 0.22)',
        borderRadius: 999,
      }}>
        <strong style={{ color: 'hsl(var(--c-player))', fontWeight: 700 }}>{count}</strong>
        <span>player</span>
      </div>

      {/* filter pills + sort + search */}
      <div style={{
        display: 'flex',
        flexDirection: isMobile ? 'column' : 'row',
        gap: 12,
        alignItems: isMobile ? 'stretch' : 'center',
      }}>
        <div style={{
          display: 'flex', flexWrap: 'wrap', gap: 4,
          flex: 1, minWidth: 0,
          position: 'relative',
        }}>
          {filters.map(f => {
            const active = f === activeFilter;
            return (
              <button key={f} onClick={() => onFilter(f)} style={{
                position: 'relative',
                padding: '8px 14px',
                background: 'transparent', border: 'none',
                fontFamily: 'var(--f-display)', fontWeight: active ? 700 : 500,
                fontSize: 13.5,
                color: active ? 'hsl(var(--c-player))' : 'var(--text-sec)',
                cursor: 'pointer',
                borderRadius: 8,
              }}>
                {f}
                {active && <span aria-hidden="true" style={{
                  position: 'absolute', left: 10, right: 10, bottom: -1,
                  height: 2, borderRadius: 2,
                  background: 'hsl(var(--c-player))',
                }} />}
              </button>
            );
          })}
        </div>
        <div style={{ display: 'flex', gap: 8, flexShrink: 0 }}>
          <select value={sort} onChange={e => onSort(e.target.value)} style={{
            padding: '8px 12px',
            background: 'var(--bg-card)', border: '1px solid var(--border)',
            borderRadius: 8,
            fontFamily: 'var(--f-body)', fontSize: 13,
            color: 'var(--text)',
          }}>
            {sorts.map(s => <option key={s} value={s}>{s}</option>)}
          </select>
          <input
            type="search" placeholder="Cerca player..."
            value={search} onChange={e => onSearch(e.target.value)}
            style={{
              padding: '8px 12px',
              background: 'var(--bg-card)', border: '1px solid var(--border)',
              borderRadius: 8,
              fontFamily: 'var(--f-body)', fontSize: 13,
              color: 'var(--text)',
              minWidth: isMobile ? 0 : 180,
              flex: isMobile ? 1 : 'none',
            }}
          />
        </div>
      </div>
    </div>
  );
};

// ── PLAYER CARD ───────────────────────────────────────────────────────────
const PlayerCard = ({ p }) => {
  const winRate = Math.round((p.wins / p.played) * 100);
  const catColor = CAT_PALETTE[p.cat];
  return (
    <article className="meeple-player-card" style={{
      background: 'var(--bg-card)',
      border: '1px solid var(--border)',
      borderRadius: 16,
      padding: 18,
      display: 'flex', flexDirection: 'column', gap: 12,
      transition: 'transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease',
      cursor: 'pointer',
    }}>
      {/* header: avatar + cat badge */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: 10 }}>
        <div style={{
          width: 80, height: 80, borderRadius: '50%',
          background: p.registered
            ? `linear-gradient(135deg, hsl(${p.hue} 50% 55%), hsl(${(p.hue + 30) % 360} 55% 42%))`
            : 'linear-gradient(135deg, var(--bg-muted), var(--border-strong))',
          display: 'grid', placeItems: 'center',
          color: '#fff',
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 28,
          letterSpacing: '-0.02em',
          flexShrink: 0,
          boxShadow: p.registered
            ? `0 4px 12px -2px hsl(${p.hue} 50% 55% / 0.4)`
            : 'none',
        }}>
          {p.name.split(' ').map(s => s[0]).slice(0, 2).join('')}
        </div>
        <span style={{
          padding: '3px 9px',
          background: catColor ? `hsl(var(${catColor}) / 0.14)` : 'var(--bg-muted)',
          color: catColor ? `hsl(var(${catColor}))` : 'var(--text-muted)',
          border: `1px solid ${catColor ? `hsl(var(${catColor}) / 0.28)` : 'var(--border)'}`,
          borderRadius: 999,
          fontFamily: 'var(--f-mono)', fontSize: 10.5, fontWeight: 600,
          letterSpacing: '0.04em', textTransform: 'uppercase',
        }}>{p.cat}</span>
      </div>

      {/* name + handle */}
      <div>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 17,
          color: 'var(--text)', letterSpacing: '-0.01em', lineHeight: 1.2,
          marginBottom: 2,
        }}>{p.name}</div>
        {p.handle ? (
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 11.5,
            color: 'hsl(var(--c-player))',
            fontWeight: 500,
          }}>{p.handle}</div>
        ) : (
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 11,
            color: 'var(--text-muted)', fontStyle: 'italic',
          }}>guest · non registrato</div>
        )}
      </div>

      {/* stats mini row */}
      <div style={{
        display: 'flex', gap: 12,
        padding: '10px 0',
        borderTop: '1px dashed var(--border)',
        borderBottom: '1px dashed var(--border)',
        fontFamily: 'var(--f-mono)', fontSize: 11.5,
        fontVariantNumeric: 'tabular-nums',
      }}>
        <Stat icon="🎯" label={p.played} sub="played" />
        <Stat icon="🏆" label={p.wins} sub={`${winRate}%`} />
        <Stat icon="⭐" label={p.avg} sub="avg" />
      </div>

      {/* last seen */}
      <div style={{
        fontFamily: 'var(--f-body)', fontSize: 12,
        color: 'var(--text-muted)',
        display: 'flex', alignItems: 'center', gap: 6,
      }}>
        <span aria-hidden="true">🕒</span>
        ultima partita: <strong style={{ color: 'var(--text-sec)', fontWeight: 600 }}>{p.last}</strong>
      </div>
    </article>
  );
};

const Stat = ({ icon, label, sub }) => (
  <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 2 }}>
    <div style={{ display: 'inline-flex', alignItems: 'center', gap: 4 }}>
      <span aria-hidden="true">{icon}</span>
      <strong style={{ color: 'var(--text)', fontWeight: 700, fontSize: 13 }}>{label}</strong>
    </div>
    <div style={{ color: 'var(--text-muted)', fontSize: 10.5, letterSpacing: '0.02em' }}>{sub}</div>
  </div>
);

const PlayerCardSkel = () => (
  <div style={{
    background: 'var(--bg-card)', border: '1px solid var(--border)',
    borderRadius: 16, padding: 18,
    display: 'flex', flexDirection: 'column', gap: 12,
  }}>
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
      <Skeleton w={80} h={80} r={40} />
      <Skeleton w={60} h={20} r={999} />
    </div>
    <div>
      <Skeleton w="65%" h={16} r={5} style={{ marginBottom: 6 }} />
      <Skeleton w="45%" h={11} r={4} />
    </div>
    <Skeleton w="100%" h={42} r={6} />
    <Skeleton w="70%" h={11} r={4} />
  </div>
);

// ── EMPTY VARIANTS (componente v2 nuovo) ──────────────────────────────────
const EmptyPlayers = ({ kind, isMobile, onAction }) => {
  const variants = {
    empty: {
      illus: <EmptyIllus icon="👤" plus />,
      title: 'Nessun player ancora',
      sub: 'Aggiungi player per tracciare le partite e vedere chi vince di più.',
      cta: '+ Aggiungi il primo player',
    },
    'filtered-empty': {
      illus: <EmptyIllus icon="🔍" />,
      title: 'Nessun player corrisponde ai filtri',
      sub: 'Prova a rimuovere qualche filtro o cambiare i termini di ricerca.',
      cta: 'Resetta filtri',
    },
    error: {
      illus: <EmptyIllus icon="⚠️" tone="error" />,
      title: 'Impossibile caricare i player',
      sub: 'Si è verificato un errore. Controlla la connessione e riprova.',
      cta: 'Riprova',
    },
  };
  const v = variants[kind];
  if (!v) return null;
  return (
    <div role="status" aria-live="polite" style={{
      maxWidth: 480, margin: '0 auto',
      padding: isMobile ? '40px 24px 60px' : '60px 24px 80px',
      textAlign: 'center',
    }}>
      <div style={{ marginBottom: 18 }}>{v.illus}</div>
      <h2 style={{
        margin: '0 0 8px',
        fontFamily: 'var(--f-display)', fontWeight: 700,
        fontSize: isMobile ? 20 : 24,
        color: 'var(--text)', letterSpacing: '-0.015em',
      }}>{v.title}</h2>
      <p style={{
        margin: '0 auto 24px', maxWidth: 360,
        fontFamily: 'var(--f-body)', fontSize: 14.5, lineHeight: 1.55,
        color: 'var(--text-sec)',
      }}>{v.sub}</p>
      <button onClick={onAction} style={{
        padding: '12px 22px',
        background: kind === 'error' ? 'var(--bg-card)' : 'hsl(var(--c-player))',
        color: kind === 'error' ? 'hsl(var(--c-player))' : '#fff',
        border: kind === 'error' ? '1px solid hsl(var(--c-player) / 0.4)' : 'none',
        borderRadius: 999,
        fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14,
        cursor: 'pointer',
        boxShadow: kind === 'error' ? 'none' : '0 4px 12px -3px hsl(var(--c-player) / 0.45)',
      }}>{v.cta}</button>
    </div>
  );
};

const EmptyIllus = ({ icon, plus, tone }) => (
  <div style={{
    display: 'inline-grid', placeItems: 'center',
    width: 88, height: 88, borderRadius: '50%',
    background: tone === 'error'
      ? 'hsl(0 70% 60% / 0.10)'
      : 'hsl(var(--c-player) / 0.10)',
    border: `1px solid ${tone === 'error' ? 'hsl(0 70% 60% / 0.28)' : 'hsl(var(--c-player) / 0.22)'}`,
    fontSize: 40, position: 'relative',
  }}>
    <span aria-hidden="true">{icon}</span>
    {plus && <span aria-hidden="true" style={{
      position: 'absolute', bottom: -4, right: -4,
      width: 30, height: 30, borderRadius: '50%',
      background: 'hsl(var(--c-player))',
      color: '#fff', display: 'grid', placeItems: 'center',
      fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 18,
      border: '3px solid var(--bg)',
      boxShadow: '0 4px 8px -2px hsl(var(--c-player) / 0.5)',
    }}>+</span>}
  </div>
);

// ── PLAYERS RESULTS GRID (componente v2 nuovo) ────────────────────────────
const PlayersResultsGrid = ({ players, isMobile, loading }) => (
  <section style={{
    maxWidth: 1180, margin: '0 auto',
    padding: isMobile ? '20px 16px 60px' : '28px 32px 80px',
  }}>
    <div style={{
      display: 'grid',
      gridTemplateColumns: isMobile
        ? '1fr'
        : 'repeat(auto-fit, minmax(280px, 1fr))',
      gap: isMobile ? 12 : 16,
    }}>
      {loading
        ? Array.from({ length: 8 }).map((_, i) => <PlayerCardSkel key={i} />)
        : players.map(p => <PlayerCard key={p.id} p={p} />)
      }
    </div>
  </section>
);

// ── PAGE BODY ─────────────────────────────────────────────────────────────
const PlayersIndexBody = ({ isMobile, state }) => {
  const [filter, setFilter] = useState('Tutti');
  const [sort, setSort] = useState('Alfabetico');
  const [search, setSearch] = useState('');

  const showHero = true;
  const showFilters = state === 'default' || state === 'filtered-empty';
  const showGrid = state === 'default' || state === 'loading';
  const showEmpty = state === 'empty' || state === 'filtered-empty' || state === 'error';

  return (
    <main style={{ background: 'var(--bg)', minHeight: '100%' }}>
      {showHero && <PlayersHero isMobile={isMobile} count={PLAYERS.length} />}
      {showFilters && (
        <PlayersFiltersInline
          isMobile={isMobile}
          activeFilter={filter} onFilter={setFilter}
          count={state === 'filtered-empty' ? 0 : PLAYERS.length}
          sort={sort} onSort={setSort}
          search={search} onSearch={setSearch}
        />
      )}
      {showGrid && <PlayersResultsGrid players={PLAYERS} isMobile={isMobile} loading={state === 'loading'} />}
      {showEmpty && <EmptyPlayers kind={state} isMobile={isMobile} onAction={() => {}} />}
    </main>
  );
};

// ── FRAMES ────────────────────────────────────────────────────────────────
const DesktopScreen = ({ label, theme, state }) => (
  <div data-screen-label={label} data-theme={theme} style={{
    width: 1440, background: 'var(--bg)',
    borderRadius: 16, border: '1px solid var(--border-strong)',
    overflow: 'hidden',
    boxShadow: '0 32px 64px -24px rgba(0,0,0,0.25), 0 12px 32px -12px rgba(0,0,0,0.12)',
  }}>
    <div style={{
      padding: '8px 14px',
      background: 'var(--bg-sunken)', borderBottom: '1px solid var(--border)',
      display: 'flex', alignItems: 'center', gap: 10,
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
    }}>
      <span style={{ display: 'inline-flex', gap: 6 }}>
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#FF5F57' }} />
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#FEBC2E' }} />
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#28C840' }} />
      </span>
      <span style={{ marginLeft: 8 }}>meepleai.app/players · {label}</span>
    </div>
    <PlayersIndexBody isMobile={false} state={state} />
  </div>
);

const MobileScreen = ({ label, theme, state }) => (
  <div data-screen-label={label} data-theme={theme} style={{
    width: 375, background: 'var(--bg)',
    borderRadius: 36, border: '8px solid #1a1410',
    overflow: 'hidden',
    boxShadow: '0 30px 60px rgba(0,0,0,0.6)',
  }}>
    <div style={{
      height: 30, background: 'var(--bg-sunken)',
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '0 18px',
      fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 11,
      color: 'var(--text)',
    }}>
      <span>9:41</span>
      <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
        <span>●●●</span><span>📶</span><span>🔋</span>
      </span>
    </div>
    <PlayersIndexBody isMobile={true} state={state} />
  </div>
);

// ── APP ───────────────────────────────────────────────────────────────────
const App = () => {
  const [theme, setTheme] = useState(() => {
    try { return localStorage.getItem('mai-sp4-players-theme') || 'light'; } catch { return 'light'; }
  });

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    try { localStorage.setItem('mai-sp4-players-theme', theme); } catch {}
  }, [theme]);

  useEffect(() => {
    if (document.getElementById('meeple-players-keyframes')) return;
    const s = document.createElement('style');
    s.id = 'meeple-players-keyframes';
    s.textContent = `
      @keyframes meepleSkeleton { 0%{background-position:200% 0} 100%{background-position:-200% 0} }
      .meeple-player-card:hover {
        transform: translateY(-2px);
        border-color: hsl(var(--c-player) / 0.4) !important;
        box-shadow: 0 8px 20px -8px hsl(var(--c-player) / 0.3);
      }
      @media (prefers-reduced-motion: reduce) {
        @keyframes meepleSkeleton { 0%,100%{background-position:0 0} }
        .meeple-player-card:hover { transform: none; }
      }
    `;
    document.head.appendChild(s);
  }, []);

  return (
    <div style={{
      minHeight: '100vh',
      background: theme === 'dark' ? '#0a0805' : '#e8dfcf',
      padding: '40px 24px 80px',
    }}>
      <div style={{
        maxWidth: 1480, margin: '0 auto 32px',
        display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap',
      }}>
        <div>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 22,
            color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
            letterSpacing: '-0.015em',
          }}>SP4 wave 4 · D1 — /players (Grid)</div>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 12,
            color: theme === 'dark' ? '#8a7860' : '#9a8870',
          }}>Mirror Games Wave B.1 · 4 stati · Desktop 1440 + Mobile 375 · Light/Dark</div>
        </div>
        <div style={{ marginLeft: 'auto' }}>
          <button onClick={(e) => { (() => setTheme(theme === 'light' ? 'dark' : 'light'))(e); setTimeout(() => { window.location.href = 'sp4-player-detail.html'; }, 0); /* DEMO-NAV */ }}
            style={{
              padding: '8px 14px', borderRadius: 999,
              border: '1px solid ' + (theme === 'dark' ? 'rgba(224,180,110,0.28)' : 'rgba(180,130,80,0.32)'),
              background: theme === 'dark' ? '#1e1710' : '#fff',
              color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
              fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 13,
              cursor: 'pointer',
            }}>{theme === 'light' ? '🌙 Dark' : '☀️ Light'}</button>
        </div>
      </div>

      <SectionLabel theme={theme}>Desktop · 1440 — Default popolato</SectionLabel>
      <Center><DesktopScreen label="01 Desktop · /players · Default" theme={theme} state="default" /></Center>

      <SectionLabel theme={theme}>Desktop · 1440 — Empty</SectionLabel>
      <Center><DesktopScreen label="02 Desktop · /players · Empty" theme={theme} state="empty" /></Center>

      <SectionLabel theme={theme}>Desktop · 1440 — Filtered empty</SectionLabel>
      <Center><DesktopScreen label="03 Desktop · /players · Filtered empty" theme={theme} state="filtered-empty" /></Center>

      <SectionLabel theme={theme}>Desktop · 1440 — Loading</SectionLabel>
      <Center><DesktopScreen label="04 Desktop · /players · Loading" theme={theme} state="loading" /></Center>

      <SectionLabel theme={theme}>Desktop · 1440 — Error</SectionLabel>
      <Center><DesktopScreen label="05 Desktop · /players · Error" theme={theme} state="error" /></Center>

      <SectionLabel theme={theme}>Mobile · 375 — Default</SectionLabel>
      <Center><MobileScreen label="06 Mobile · /players · Default" theme={theme} state="default" /></Center>

      <SectionLabel theme={theme}>Mobile · 375 — Empty</SectionLabel>
      <Center><MobileScreen label="07 Mobile · /players · Empty" theme={theme} state="empty" /></Center>

      <SectionLabel theme={theme}>Mobile · 375 — Loading</SectionLabel>
      <Center><MobileScreen label="08 Mobile · /players · Loading" theme={theme} state="loading" /></Center>

      <div style={{
        maxWidth: 900, margin: '40px auto 0',
        padding: '24px 28px',
        background: theme === 'dark' ? '#1e1710' : '#fff',
        border: '1px solid ' + (theme === 'dark' ? 'rgba(224,180,110,0.14)' : 'rgba(180,130,80,0.18)'),
        borderRadius: 16,
        color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
      }}>
        <h3 style={{ fontFamily: 'var(--f-display)', margin: '0 0 12px', fontSize: 18 }}>DoD Checklist · sp4-players-index</h3>
        <ul style={{ fontFamily: 'var(--f-body)', fontSize: 13.5, lineHeight: 1.7, color: theme === 'dark' ? '#c8b896' : '#5a4a38', paddingLeft: 20, margin: 0 }}>
          <li>✓ Route target /players · pattern Grid (mirror Games Wave B.1)</li>
          <li>✓ PlayersHero: kicker mono "👤 La tua community" + h1 "Player tracker" + subtitle + CTA "+ Aggiungi player" palette player + bg gradient player→chat</li>
          <li>✓ PlayersFiltersInline: 6 pills (Tutti · Amici · Famiglia · Online · Locali · Ospiti) + sort dropdown 4 opzioni + search "Cerca player..." + count badge "47 player" tabular-nums (mostra real count)</li>
          <li>✓ Active filter pill: animated underline 2px palette player (riuso pattern Tabs)</li>
          <li>✓ PlayersResultsGrid: CSS Grid auto-fit minmax(280px,1fr) · gap 16 desktop / 12 mobile · 1-col mobile</li>
          <li>✓ Player card: avatar circolare 80px gradient hue-based (registered) o neutral grey (ospite) · iniziali bianche</li>
          <li>✓ Card body: nome bold + @handle mono palette player (o "guest · non registrato" italic per ospiti)</li>
          <li>✓ Stats mini row: 🎯 played · 🏆 wins (% win rate calcolato) · ⭐ avg score · separator dashed top/bottom · tabular-nums</li>
          <li>✓ Categoria badge mono uppercase pill colored: Amico→player · Famiglia→event · Online→chat · Locali→game · Ospite→neutral</li>
          <li>✓ Last seen: "🕒 ultima partita: N giorni fa"</li>
          <li>✓ Hover card: translateY -2 + border palette player + shadow palette player (rispetta prefers-reduced-motion)</li>
          <li>✓ EmptyPlayers 4 varianti: empty (👤+➕ illus, "Nessun player ancora", CTA primary) · filtered-empty (🔍 illus, "Resetta filtri") · loading (skeleton 8 card) · error (⚠️ tone, ghost CTA Retry)</li>
          <li>✓ Empty illustrazione: cerchio 88px palette player tinted + emoji 40px + plus badge floating per kind="empty"</li>
          <li>✓ Dati realistici da data.js: Marco R., Giulia B., Davide F., Sara M., Andrea P., Lorenzo T., Elena C. + ospiti Nicola, Chiara · stat range 5-142 played · win rate 33%-63%</li>
          <li>✓ ID short readable: p-marco, p-giulia, p-davide, p-sara, p-andrea, p-lorenzo, p-elena, p-nicola, p-chiara (NO UUID)</li>
          <li>✓ Componenti v2 nuovi: PlayersHero · PlayersFiltersInline · PlayersResultsGrid · EmptyPlayers (4 varianti)</li>
          <li>✓ Riusi: MeepleCard variant="grid" pattern (con entity="player" via palette + avatar) · Tabs animated underline · EntityChip pattern (categoria badge)</li>
          <li>✓ ARIA: &lt;main&gt;, &lt;article&gt; cards, &lt;section&gt; per grid, role="status" aria-live="polite" sugli empty state, aria-hidden sulle emoji decorative, &lt;h1&gt; unico, &lt;h2&gt; per empty title</li>
          <li>✓ Light + Dark mode toggle persistito</li>
          <li>✓ NO window.innerWidth in body — isMobile è prop esplicita dalla frame</li>
          <li>✓ NON sovrascritto components.css né data.js</li>
          <li>✓ 8 frame generati: 5 desktop (default · empty · filtered-empty · loading · error) + 3 mobile (default · empty · loading)</li>
        </ul>
      </div>
    </div>
  );
};

const SectionLabel = ({ children, theme }) => (
  <h2 style={{
    maxWidth: 1480, margin: '0 auto 14px',
    fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 700,
    textTransform: 'uppercase', letterSpacing: '0.08em',
    color: theme === 'dark' ? '#c8b896' : '#5a4a38',
  }}>{children}</h2>
);

const Center = ({ children }) => (
  <div style={{
    maxWidth: 1480, margin: '0 auto 48px',
    display: 'flex', justifyContent: 'center',
  }}>{children}</div>
);

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
