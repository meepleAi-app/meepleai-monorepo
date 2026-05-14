/* MeepleAI Pre-Stage-3 (#1148) — Hub Games (public)
   Route: /hub/games  (PUBLIC — accessibile senza login)
   File: admin-mockups/design_files/sp4-hub-games.{html,jsx}

   Pattern: catalogo community read-only con StickyCTA "Accedi per installare".
   Variante public di `sp4-games-index` — niente azioni di libreria, solo browse.
   Diffs vs sp4-games-index:
   - Hero: "Esplora i giochi della community" + badge `/hub/games`
   - Cards: badge `+ N installazioni` invece di status libreria; ConnectionChip ridotta
   - Filtri: Tutti / Featured / Nuovi / Top — niente Wishlist
   - StickyCTA bottom: "Accedi per installare nella tua libreria"
   - Empty filtered: stesso pattern; Empty primario non applica (catalog è popolato)

   Componenti v2 nuovi flaggati:
   - HubGamesHero          → apps/web/src/components/ui/v2/hub-games-hero/
   - HubFilters            → apps/web/src/components/ui/v2/hub-filters/ (shared con hub-agents/toolkits)
   - HubGameCardGrid       → variante `grid-public` di MeepleCard
   - StickyAccessCta       → apps/web/src/components/ui/v2/sticky-access-cta/ (only hub-games)
   - HubEmptyFiltered      → apps/web/src/components/ui/v2/hub-empty/

   USE CASE:
     Primary actor: visitatore (non autenticato)
     Goal: esplorare il catalogo community di giochi per scoprire titoli da installare nella propria libreria.
     Success: 4 stati renderati (default popolato / filtered-empty / loading / error) + StickyCTA sempre visibile sotto.
     Non-goal: install action (richiede login → routes /login?redirect=/hub/games).
*/

const { useState, useEffect, useMemo } = React;
const DS = window.DS;

// ─── ENTITY HSL HELPER ─────────────────
const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.game;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── DATASET COMMUNITY GAMES ─────────────────────
// Catalogo community — augmentazione di DS.games con metriche di popolarità + nuovi entry.
const HUB_GAMES = [
  ...DS.games.map(g => ({
    ...g,
    installCount: 200 + Math.floor(Math.random() * 1500),
    featured: g.rating >= 8.4,
    isNew: g.year >= 2020,
    communityRating: g.rating,
  })),
  { id: 'g-marrakesh', type: 'game', title: 'Marrakesh', publisher: 'Queen Games', year: 2022,
    author: 'Stefan Feld', players: '2–4', duration: '60m', weight: 3.05, rating: 7.6, stars: 4,
    cover: DS.grad(30, 75), coverEmoji: '🕌', badge: 'Featured',
    installCount: 1840, featured: true, isNew: true, communityRating: 7.6 },
  { id: 'g-skymines', type: 'game', title: 'Skymines', publisher: 'eggertspiele', year: 2022,
    author: 'Alexander Pfister', players: '1–4', duration: '120m', weight: 3.61, rating: 8.0, stars: 4,
    cover: DS.grad(220, 60), coverEmoji: '🚀', badge: 'Top 100',
    installCount: 2340, featured: true, isNew: true, communityRating: 8.0 },
  { id: 'g-orient', type: 'game', title: 'Orient Express', publisher: 'Hachette', year: 2023,
    author: 'Antoine Bauza', players: '2–6', duration: '45m', weight: 2.18, rating: 7.3, stars: 4,
    cover: DS.grad(0, 70), coverEmoji: '🚂', badge: 'Nuovo',
    installCount: 412, featured: false, isNew: true, communityRating: 7.3 },
  { id: 'g-revive', type: 'game', title: 'Revive', publisher: 'Aporta', year: 2022,
    author: 'Anna Wermlund', players: '1–4', duration: '90m', weight: 3.20, rating: 8.1, stars: 5,
    cover: DS.grad(140, 55), coverEmoji: '🌱', badge: 'Featured',
    installCount: 1620, featured: true, isNew: true, communityRating: 8.1 },
  { id: 'g-frostpunk', type: 'game', title: 'Frostpunk: The Board Game', publisher: 'Glass Cannon', year: 2022,
    author: 'A. Sieja · J. Wnorowski', players: '1–4', duration: '120–150m', weight: 3.85, rating: 7.9, stars: 4,
    cover: DS.grad(195, 50), coverEmoji: '❄️', badge: 'Heavy',
    installCount: 980, featured: false, isNew: true, communityRating: 7.9 },
  { id: 'g-akropolis', type: 'game', title: 'Akropolis', publisher: 'Gigamic', year: 2022,
    author: 'Jules Messaud', players: '2–4', duration: '20–30m', weight: 1.65, rating: 7.7, stars: 4,
    cover: DS.grad(45, 60), coverEmoji: '🏛️', badge: 'Light',
    installCount: 2890, featured: true, isNew: true, communityRating: 7.7 },
  { id: 'g-dorfromantik', type: 'game', title: 'Dorfromantik', publisher: 'Pegasus', year: 2022,
    author: 'M. Palm · L. Zach', players: '1–6', duration: '45m', weight: 2.10, rating: 7.6, stars: 4,
    cover: DS.grad(110, 55), coverEmoji: '🏞️', badge: 'Cooperativo',
    installCount: 1740, featured: false, isNew: true, communityRating: 7.6 },
];

// ═══════════════════════════════════════════════════════
// ─── STARS ──────────────────────────────────────────
const Stars = ({ value = 0, size = 11 }) => {
  const full = Math.floor(value / 2); // rating /10 → /5
  return (
    <span style={{ display:'inline-flex', gap: 1, color:'hsl(var(--c-agent))', fontSize: size }}>
      {[0,1,2,3,4].map(i => <span key={i} aria-hidden="true">{i < full ? '★' : '☆'}</span>)}
    </span>
  );
};

// ═══════════════════════════════════════════════════════
// ─── HUB GAME CARD (public catalog variant) ────────
// ═══════════════════════════════════════════════════════
const HubGameCardGrid = ({ game, compact }) => (
  <article tabIndex={0} className="mai-card-hub"
    style={{
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      overflow:'hidden',
      cursor:'pointer',
      transition:'transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm), box-shadow var(--dur-sm)',
      display:'flex', flexDirection:'column',
    }}>
    {/* Cover */}
    <div style={{
      position:'relative',
      aspectRatio: compact ? '4 / 3' : '5 / 3',
      background: game.cover,
      display:'flex', alignItems:'center', justifyContent:'center',
      overflow:'hidden',
    }}>
      <span aria-hidden="true" style={{
        fontSize: compact ? 50 : 60,
        filter:'drop-shadow(0 4px 12px rgba(0,0,0,.35))',
      }}>{game.coverEmoji}</span>
      {/* badge top-left */}
      {game.badge && (
        <span style={{
          position:'absolute', top: 8, left: 8,
          padding:'3px 8px', borderRadius:'var(--r-pill)',
          background:'rgba(0,0,0,.5)', color:'#fff',
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.06em',
          backdropFilter:'blur(4px)',
        }}>{game.badge}</span>
      )}
      {/* entity chip top-right */}
      <span style={{
        position:'absolute', top: 8, right: 8,
        padding:'3px 7px', borderRadius:'var(--r-pill)',
        background: entityHsl('game', 0.95), color:'#fff',
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
        display:'inline-flex', alignItems:'center', gap: 3,
      }}>
        <span aria-hidden="true">🎲</span>Game
      </span>
      {/* gradient overlay */}
      <div style={{
        position:'absolute', inset:'auto 0 0 0', height:'40%',
        background:'linear-gradient(180deg, transparent 0%, rgba(0,0,0,.35) 100%)',
      }}/>
      {/* install count bottom-right */}
      <div style={{
        position:'absolute', bottom: 8, right: 8,
        display:'inline-flex', alignItems:'center', gap: 4,
        padding:'3px 8px', borderRadius:'var(--r-pill)',
        background:'rgba(255,255,255,.92)', color:'#1a1a1a',
        fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
      }}>
        <span aria-hidden="true">⬇</span>
        <span style={{ fontVariantNumeric:'tabular-nums' }}>{game.installCount.toLocaleString('it-IT')}</span>
      </div>
    </div>
    {/* Body */}
    <div style={{
      padding: compact ? 10 : 14,
      display:'flex', flexDirection:'column', gap: compact ? 6 : 8, flex: 1,
    }}>
      <div>
        <h3 style={{
          fontFamily:'var(--f-display)', fontWeight: 800,
          fontSize: compact ? 13 : 15, lineHeight: 1.15,
          color:'var(--text)', margin:'0 0 3px',
          display:'-webkit-box', WebkitLineClamp: 1, WebkitBoxOrient:'vertical', overflow:'hidden',
        }}>{game.title}</h3>
        <div style={{
          display:'flex', alignItems:'center', gap: 5,
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 600,
        }}>
          <Stars value={game.rating} size={9}/>
          <span style={{ fontVariantNumeric:'tabular-nums' }}>{game.rating}</span>
          <span aria-hidden="true">·</span>
          <span>{game.players}</span>
          <span aria-hidden="true">·</span>
          <span>{game.year}</span>
        </div>
      </div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-sec)',
        fontWeight: 600,
      }}>{game.publisher}</div>
    </div>
  </article>
);

// ═══════════════════════════════════════════════════════
// ─── HUB GAMES HERO (public) ─────────────────────
// ═══════════════════════════════════════════════════════
const HubGamesHero = ({ stats, compact }) => (
  <header style={{
    padding: compact ? '20px 16px 14px' : '32px 32px 18px',
    background:`linear-gradient(180deg, ${entityHsl('game', 0.08)} 0%, transparent 100%)`,
    borderBottom:'1px solid var(--border-light)',
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 6, marginBottom: 10 }}>
      <span style={{
        display:'inline-flex', alignItems:'center', gap: 5,
        padding:'3px 9px', borderRadius:'var(--r-pill)',
        background: entityHsl('game', 0.14),
        color: entityHsl('game'),
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.08em',
      }}><span aria-hidden="true">🎲</span>Hub · /hub/games</span>
      <span style={{
        padding:'3px 8px', borderRadius:'var(--r-pill)',
        background:'var(--bg-muted)', color:'var(--text-muted)',
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
      }}>Pubblico</span>
    </div>
    <h1 style={{
      fontFamily:'var(--f-display)', fontWeight: 800,
      fontSize: compact ? 24 : 34, letterSpacing:'-.02em', lineHeight: 1.05,
      color:'var(--text)', margin:'0 0 6px',
    }}>Esplora i giochi della community</h1>
    <p style={{
      color:'var(--text-sec)', fontSize: compact ? 13 : 14, lineHeight: 1.55,
      margin:'0 0 14px', maxWidth: 620,
    }}>
      Sfoglia il catalogo dei giochi indicizzati dalla community.
      Filtra per popolarità, rating e novità. Accedi per installarli nella tua libreria.
    </p>
    <div style={{
      display:'flex', flexWrap:'wrap', gap: compact ? 14 : 22,
      fontFamily:'var(--f-mono)', fontSize: compact ? 11 : 12,
    }}>
      {stats.map(s => (
        <div key={s.label} style={{ display:'flex', flexDirection:'column', gap: 2 }}>
          <span style={{
            color:'var(--text-muted)', fontSize: 9, fontWeight: 700,
            textTransform:'uppercase', letterSpacing:'.08em',
          }}>{s.label}</span>
          <span style={{
            color:'var(--text)', fontSize: compact ? 18 : 22, fontWeight: 800,
            fontFamily:'var(--f-display)', fontVariantNumeric:'tabular-nums',
            lineHeight: 1,
          }}>{s.value}<span style={{ fontSize: 11, color:'var(--text-muted)', fontWeight: 600, marginLeft: 4 }}>{s.unit}</span></span>
        </div>
      ))}
    </div>
  </header>
);

// ═══════════════════════════════════════════════════════
// ─── HUB FILTERS (shared shell, entity-tinted) ────
// ═══════════════════════════════════════════════════════
const STATUS_OPTS = [
  { id:'all', label:'Tutti' },
  { id:'featured', label:'Featured' },
  { id:'new', label:'Nuovi' },
  { id:'top', label:'Top 100' },
];
const SORT_OPTS = [
  { id:'popular', label:'Popolarità' },
  { id:'rating', label:'Rating' },
  { id:'title', label:'Titolo A-Z' },
  { id:'year', label:'Anno' },
];

const HubFilters = ({ q, setQ, status, setStatus, sort, setSort, compact, count, entity }) => (
  <div style={{
    padding: compact ? '12px 16px' : '14px 32px',
    display:'flex', flexDirection: compact ? 'column' : 'row',
    alignItems: compact ? 'stretch' : 'center',
    gap: compact ? 10 : 12,
    borderBottom:'1px solid var(--border-light)',
    background:'var(--bg)',
    position:'sticky', top: 0, zIndex: 10,
    backdropFilter:'blur(12px)',
  }}>
    <label style={{
      display:'flex', alignItems:'center', gap: 8,
      flex: compact ? 'none' : '1 1 280px', maxWidth: compact ? 'none' : 360,
      padding:'8px 12px',
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-md)',
    }}>
      <span aria-hidden="true" style={{ color:'var(--text-muted)' }}>🔍</span>
      <input
        type="text" value={q} onChange={e=>setQ(e.target.value)}
        placeholder="Cerca per titolo, autore, publisher…"
        style={{
          flex: 1, border:'none', outline:'none', background:'transparent',
          color:'var(--text)', fontFamily:'var(--f-body)', fontSize: 13,
          minWidth: 0,
        }}
      />
      {q && (
        <button type="button" onClick={()=>setQ('')} aria-label="Pulisci"
          style={{
            border:'none', background:'transparent', color:'var(--text-muted)',
            cursor:'pointer', fontSize: 13, padding: 0,
          }}>✕</button>
      )}
    </label>

    <div role="tablist" style={{
      display:'inline-flex',
      background:'var(--bg-muted)',
      borderRadius:'var(--r-md)',
      padding: 2,
      flexShrink: 0,
    }}>
      {STATUS_OPTS.map(o => (
        <button key={o.id} role="tab" aria-selected={status === o.id}
          onClick={()=>setStatus(o.id)}
          style={{
            padding: compact ? '6px 9px' : '6px 12px',
            border:'none',
            background: status === o.id ? 'var(--bg-card)' : 'transparent',
            color: status === o.id ? entityHsl(entity) : 'var(--text-sec)',
            fontFamily:'var(--f-display)',
            fontSize: compact ? 11 : 12, fontWeight: 700,
            borderRadius: 'var(--r-sm)',
            cursor:'pointer', whiteSpace:'nowrap',
            boxShadow: status === o.id ? 'var(--shadow-xs)' : 'none',
            transition:'all var(--dur-sm) var(--ease-out)',
          }}>{o.label}</button>
      ))}
    </div>

    <div style={{ flex: 1 }}/>

    <label style={{
      display:'inline-flex', alignItems:'center', gap: 6,
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      fontWeight: 600,
    }}>
      <span style={{ textTransform:'uppercase', letterSpacing:'.06em' }}>Ordina</span>
      <select value={sort} onChange={e=>setSort(e.target.value)}
        style={{
          padding:'6px 10px', borderRadius:'var(--r-sm)',
          border:'1px solid var(--border)', background:'var(--bg-card)',
          color:'var(--text)', fontFamily:'var(--f-display)',
          fontSize: 12, fontWeight: 700, cursor:'pointer',
        }}>
        {SORT_OPTS.map(o => <option key={o.id} value={o.id}>{o.label}</option>)}
      </select>
    </label>

    {compact && (
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 700, letterSpacing:'.04em',
      }}>{count} giochi</div>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STICKY ACCESS CTA (only hub-games) ──────────
// ═══════════════════════════════════════════════════════
const StickyAccessCta = ({ compact }) => (
  <div style={{
    position: compact ? 'sticky' : 'fixed',
    bottom: compact ? 70 : 24, // above mobile bottom-bar
    left: compact ? 12 : 'auto',
    right: compact ? 12 : 24,
    width: compact ? 'auto' : 360,
    zIndex: 30,
    padding:'12px 16px',
    background:'var(--glass-bg)',
    backdropFilter:'blur(16px)',
    border:`1px solid ${entityHsl('game', 0.35)}`,
    borderRadius:'var(--r-xl)',
    boxShadow:`0 12px 36px ${entityHsl('game', 0.18)}`,
    display:'flex', alignItems:'center', gap: 12,
  }}>
    <div style={{
      width: 36, height: 36, borderRadius:'50%',
      background: entityHsl('game', 0.14),
      color: entityHsl('game'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 18, flexShrink: 0,
    }} aria-hidden="true">🔐</div>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 13,
        color:'var(--text)', marginBottom: 2,
      }}>Installa nella tua libreria</div>
      <div style={{
        fontSize: 11, color:'var(--text-muted)', lineHeight: 1.4,
      }}>Accedi per salvare giochi, agenti e toolkit</div>
    </div>
    <a href="/login?redirect=/hub/games"
      style={{
        padding:'8px 14px', borderRadius:'var(--r-md)',
        background: entityHsl('game'), color:'#fff',
        textDecoration:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
        whiteSpace:'nowrap',
        boxShadow:`0 4px 14px ${entityHsl('game', 0.35)}`,
      }}>Accedi →</a>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── EMPTY / ERROR / SKELETON ─────────────────
const HubEmptyFiltered = ({ onAction, compact }) => (
  <div style={{
    gridColumn:'1 / -1',
    display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
    padding: compact ? '40px 20px' : '64px 32px',
    background:'var(--bg-card)',
    border:'1px dashed var(--border-strong)',
    borderRadius:'var(--r-xl)',
  }}>
    <div style={{
      width: compact ? 64 : 80, height: compact ? 64 : 80,
      borderRadius:'50%',
      background: entityHsl('game', 0.12), color: entityHsl('game'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: compact ? 32 : 40, marginBottom: 16,
    }} aria-hidden="true">🔎</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: compact ? 16 : 19, fontWeight: 800,
      color:'var(--text)', margin:'0 0 6px',
    }}>Nessun risultato</h3>
    <p style={{
      fontSize: 13, color:'var(--text-muted)', maxWidth: 360,
      margin:'0 0 18px', lineHeight: 1.55,
    }}>Nessun gioco corrisponde ai filtri attuali. Prova a modificare la ricerca.</p>
    <button type="button" onClick={onAction}
      style={{
        padding:'10px 18px', borderRadius:'var(--r-md)',
        background: entityHsl('game'), color:'#fff',
        border:'none', fontFamily:'var(--f-display)',
        fontSize: 13, fontWeight: 800, cursor:'pointer',
        boxShadow:`0 4px 14px ${entityHsl('game', 0.35)}`,
      }}>Azzera filtri</button>
  </div>
);

const ErrorState = ({ compact }) => (
  <div style={{
    gridColumn:'1 / -1',
    padding: compact ? '32px 20px' : '48px 32px',
    background:'hsl(var(--c-danger) / .08)',
    border:'1px solid hsl(var(--c-danger) / .3)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
  }}>
    <div style={{
      width: 56, height: 56, borderRadius:'50%',
      background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 26, marginBottom: 12,
    }} aria-hidden="true">⚠</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>Impossibile caricare il catalogo</h3>
    <p style={{
      fontSize: 12.5, color:'var(--text-muted)', maxWidth: 360,
      margin:'0 0 16px',
    }}>Si è verificato un errore di rete. Verifica la connessione e riprova.</p>
    <button type="button"
      style={{
        padding:'8px 16px', borderRadius:'var(--r-md)',
        background:'hsl(var(--c-danger))', color:'#fff',
        border:'none', fontFamily:'var(--f-display)',
        fontSize: 12, fontWeight: 800, cursor:'pointer',
      }}>↻ Riprova</button>
  </div>
);

const SkeletonCard = ({ compact }) => (
  <div style={{
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
    overflow:'hidden',
  }}>
    <div className="mai-shimmer" style={{
      aspectRatio: compact ? '4 / 3' : '5 / 3',
      background:'var(--bg-muted)',
    }}/>
    <div style={{ padding: compact ? 10 : 14, display:'flex', flexDirection:'column', gap: 6 }}>
      <div className="mai-shimmer" style={{ height: 14, width:'72%', borderRadius: 4, background:'var(--bg-muted)' }}/>
      <div className="mai-shimmer" style={{ height: 10, width:'52%', borderRadius: 4, background:'var(--bg-muted)' }}/>
      <div className="mai-shimmer" style={{ height: 10, width:'40%', borderRadius: 4, background:'var(--bg-muted)' }}/>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── PAGE BODY ──────────────────────────────────────
// ═══════════════════════════════════════════════════════
const filterAndSort = (list, q, status, sort) => {
  let out = list;
  if (q) {
    const needle = q.toLowerCase();
    out = out.filter(g =>
      g.title.toLowerCase().includes(needle) ||
      (g.author || '').toLowerCase().includes(needle) ||
      (g.publisher || '').toLowerCase().includes(needle) ||
      String(g.year).includes(needle)
    );
  }
  if (status !== 'all') {
    if (status === 'featured') out = out.filter(g => g.featured);
    else if (status === 'new') out = out.filter(g => g.isNew);
    else if (status === 'top') out = out.filter(g => g.rating >= 8.0);
  }
  out = [...out].sort((a, b) => {
    if (sort === 'rating') return b.rating - a.rating;
    if (sort === 'title') return a.title.localeCompare(b.title);
    if (sort === 'year') return b.year - a.year;
    return b.installCount - a.installCount; // popular
  });
  return out;
};

const HubGamesBody = ({ stateOverride, compact }) => {
  const [q, setQ] = useState('');
  const [status, setStatus] = useState('all');
  const [sort, setSort] = useState('popular');

  useEffect(() => {
    if (stateOverride === 'filtered-empty') setQ('xyznotfound');
    else { setQ(''); setStatus('all'); }
  }, [stateOverride]);

  const isLoading = stateOverride === 'loading';
  const isError = stateOverride === 'error';

  const filtered = useMemo(() => filterAndSort(HUB_GAMES, q, status, sort),
    [q, status, sort]);

  const totalInstalls = HUB_GAMES.reduce((s, g) => s + g.installCount, 0);
  const stats = [
    { label:'Giochi', value: HUB_GAMES.length, unit:'' },
    { label:'Installazioni', value: totalInstalls.toLocaleString('it-IT'), unit:'' },
    { label:'Featured', value: HUB_GAMES.filter(g => g.featured).length, unit:'' },
    { label:'Nuovi 2022+', value: HUB_GAMES.filter(g => g.isNew).length, unit:'' },
  ];

  return (
    <>
      <HubGamesHero stats={stats} compact={compact}/>
      {!isError && (
        <HubFilters
          q={q} setQ={setQ}
          status={status} setStatus={setStatus}
          sort={sort} setSort={setSort}
          compact={compact} count={filtered.length} entity="game"
        />
      )}

      <div style={{
        padding: compact ? '14px 16px 80px' : '24px 32px 100px',
      }}>
        {isError && <ErrorState compact={compact}/>}

        {!isError && isLoading && (
          <div style={{
            display:'grid',
            gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)',
            gap: compact ? 10 : 16,
          }}>
            {Array.from({ length: compact ? 6 : 12 }).map((_, i) => (
              <SkeletonCard key={i} compact={compact}/>
            ))}
          </div>
        )}

        {!isError && !isLoading && (
          filtered.length === 0 ? (
            <HubEmptyFiltered compact={compact}
              onAction={() => { setQ(''); setStatus('all'); }}/>
          ) : (
            <div style={{
              display:'grid',
              gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)',
              gap: compact ? 10 : 16,
            }}>
              {filtered.map(g => <HubGameCardGrid key={g.id} game={g} compact={compact}/>)}
            </div>
          )
        )}
      </div>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── DESKTOP / MOBILE SHELLS ─────────────────
// ═══════════════════════════════════════════════════════
const DesktopPublicNav = () => {
  const items = [
    { id:'home', label:'Home' },
    { id:'hub-games', label:'Giochi', active: true },
    { id:'hub-agents', label:'Agenti' },
    { id:'hub-toolkits', label:'Toolkit' },
    { id:'about', label:'Chi siamo' },
  ];
  return (
    <div style={{
      display:'flex', alignItems:'center', gap: 14,
      padding:'10px 32px',
      background:'var(--glass-bg)', backdropFilter:'blur(12px)',
      borderBottom:'1px solid var(--border)',
    }}>
      <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
        <div style={{
          width: 26, height: 26, borderRadius: 7,
          background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
          color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
          fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
        }}>M</div>
        <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
      </div>
      <div style={{ display:'flex', alignItems:'center', gap: 2, marginLeft: 18 }}>
        {items.map(it => (
          <a key={it.id} href="#" onClick={e=>e.preventDefault()}
            aria-current={it.active ? 'page' : undefined}
            style={{
              padding:'6px 12px', borderRadius:'var(--r-md)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              color: it.active ? entityHsl('game') : 'var(--text-sec)',
              background: it.active ? entityHsl('game', 0.1) : 'transparent',
              textDecoration:'none',
            }}>{it.label}</a>
        ))}
      </div>
      <div style={{ flex: 1 }}/>
      <a href="/login" style={{
        padding:'7px 14px', borderRadius:'var(--r-md)',
        background:'transparent', border:`1px solid ${entityHsl('game', 0.5)}`,
        color: entityHsl('game'),
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
        textDecoration:'none',
      }}>Accedi</a>
    </div>
  );
};

const DesktopScreen = ({ stateOverride, showCta = true }) => (
  <div style={{ minHeight: 720, background:'var(--bg)', position:'relative' }}>
    <DesktopPublicNav/>
    <HubGamesBody stateOverride={stateOverride}/>
    {showCta && <StickyAccessCta/>}
  </div>
);

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);
const PhoneTopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', borderBottom:'1px solid var(--border)',
    background:'var(--bg)',
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{ flex: 1, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700, textAlign:'center' }}>
      Hub · Giochi
    </div>
    <button aria-label="Menu" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 16, cursor:'pointer',
    }}>⋯</button>
  </div>
);
const MobileBottomBar = () => {
  const tabs = [
    { id:'hub-games', icon:'🎲', label:'Giochi', active: true },
    { id:'hub-agents', icon:'🤖', label:'Agenti' },
    { id:'hub-toolkits', icon:'🧰', label:'Toolkit' },
    { id:'login', icon:'🔐', label:'Accedi' },
  ];
  return (
    <div style={{
      position:'absolute', bottom: 0, left: 0, right: 0,
      display:'flex',
      padding:'8px 10px 12px',
      background:'var(--glass-bg)', backdropFilter:'blur(14px)',
      borderTop:'1px solid var(--border)',
      zIndex: 5,
    }}>
      {tabs.map(t => (
        <button key={t.id} style={{
          flex: 1,
          display:'flex', flexDirection:'column', alignItems:'center', gap: 2,
          padding:'4px 0',
          background:'transparent', border:'none',
          color: t.active ? entityHsl('game') : 'var(--text-muted)',
          fontFamily:'var(--f-display)', fontSize: 9, fontWeight: 700,
          cursor:'pointer',
        }}>
          <span aria-hidden="true" style={{ fontSize: 16 }}>{t.icon}</span>
          <span>{t.label}</span>
        </button>
      ))}
    </div>
  );
};
const MobileScreen = ({ stateOverride, showCta = true }) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav/>
      <div style={{ paddingBottom: 70 }}>
        <HubGamesBody stateOverride={stateOverride} compact/>
      </div>
      {showCta && <StickyAccessCta compact/>}
      <MobileBottomBar/>
    </div>
  </>
);

const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div className="phone">{children}</div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 340,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

const DesktopFrame = ({ label, desc, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1280,
      borderRadius:'var(--r-xl)', border:'1px solid var(--border)',
      background:'var(--bg)', overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
      position:'relative',
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding:'9px 14px',
        background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/hub/games</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ROOT ────────────────────────────────────────────
const MOBILE_STATES = [
  { id:'default', label:'01 · Default', desc:'~15 giochi, grid 2-col compact, install count overlay. StickyCTA bottom (sopra bottom-bar).' },
  { id:'loading', label:'02 · Loading', desc:'6 skeleton cards shimmer. Filtri visibili e interagibili.' },
  { id:'filtered-empty', label:'03 · Filtered empty', desc:'Query "xyznotfound" → empty state filtrato (no risultati) + "Azzera filtri".' },
  { id:'error', label:'04 · Error', desc:'Banner danger inline + retry. Hero rimane visibile, filtri nascosti.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle"
      aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span>
      <span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function App() {
  return (
    <div className="stage">
      <ThemeToggle/>
      <div className="stage-wrap">
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8,
        }}>Pre-Stage-3 · #1148 · Hub Games (public)</div>
        <h1>Hub Games — /hub/games</h1>
        <p className="lead">
          Catalogo community pubblico. Visitatori navigano senza login.
          Variante public di <code>sp4-games-index</code>: install count overlay sulle cover,
          filtri <strong>Featured/Nuovi/Top</strong>, <strong>StickyAccessCta</strong> fissa in basso.
          4 stati mobile (default / loading / filtered-empty / error) e 3 desktop.
        </p>

        <div className="section-label">Mobile · 375 — 4 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <MobileScreen stateOverride={s.id} showCta={s.id !== 'error'}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1280 — 3 stati</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="05 · Desktop · Default"
            desc="Hero stats inline (install totali, featured, nuovi), filtri sticky, grid 4-col. Install count overlay sulle cover. StickyAccessCta fixed bottom-right.">
            <DesktopScreen stateOverride="default"/>
          </DesktopFrame>
          <DesktopFrame label="06 · Desktop · Loading"
            desc="12 skeleton cards (4-col × 3 row) con shimmer. Hero e filtri visibili.">
            <DesktopScreen stateOverride="loading"/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Filtered empty"
            desc="Query 'xyznotfound' → empty filtrato (icona 🔎) con CTA 'Azzera filtri'. Hero e filtri restano.">
            <DesktopScreen stateOverride="filtered-empty"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        .mai-shimmer {
          background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important;
          background-size: 200% 100% !important;
          animation: mai-shimmer-anim 1.4s linear infinite;
        }
        .phones-grid {
          display: grid;
          grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
          gap: var(--s-7);
          align-items: start;
        }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage .lead { line-height: var(--lh-relax); }
        .phone > div::-webkit-scrollbar { display: none; }
        .mai-card-hub { outline: none; }
        .mai-card-hub:hover {
          transform: translateY(-2px);
          border-color: hsl(var(--c-game) / .4) !important;
          box-shadow: var(--shadow-md);
        }
        .mai-card-hub:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
        select:focus-visible, input:focus-visible, button:focus-visible, a:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
