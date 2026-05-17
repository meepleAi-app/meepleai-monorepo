/* MeepleAI Pre-Stage-3 (#1148) — Hub Toolkits (authenticated)
   Route: /hub/toolkits  (AUTHENTICATED — solo utenti loggati)
   File: admin-mockups/design_files/sp4-hub-toolkits.{html,jsx}

   Pattern: catalogo community di toolkit (bundle di tools per giochi).
   Sibling structural di sp4-hub-agents. AC8 (#1148): diff ≤ 30%.
   Diffs vs sp4-hub-agents:
   - Entity color: --c-toolkit (green) invece di --c-agent
   - Cards display: version + toolCount + useCount invece di model + invocations
   - Search placeholder: "categoria" invece di "modello"
   - Sample data: HUB_TOOLKITS (toolkit-shape)

   Componenti v2 nuovi flaggati:
   - HubToolkitsHero       → apps/web/src/components/ui/v2/hub-toolkits-hero/
   - HubFilters            → ✅ shared con hub-games/agents (entity-tinted)
   - HubToolkitCardGrid    → variante `grid` di MeepleCard per toolkit
   - HubInstallButton      → inline install action su hover (shared con hub-agents)

   USE CASE:
     Primary actor: utente autenticato
     Goal: trovare toolkit community (bundle di tools — timer/dadi/counter) per i propri giochi.
     Success: 4 stati renderati (default popolato / filtered-empty / loading / error). Install inline su hover ogni card.
     Non-goal: configurazione tool interno post-install (vedi /toolkits/[id]).
*/

const { useState, useEffect, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.toolkit;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── DATASET COMMUNITY TOOLKITS ─────────────────────
const HUB_TOOLKITS = [
  ...DS.toolkits.map(t => ({
    ...t,
    installCount: 80 + Math.floor(Math.random() * 600),
    featured: t.useCount >= 25,
    isNew: t.version >= 2,
    communityRating: 4.1 + Math.random() * 0.8,
  })),
  { id: 'tk-wingspan-pro', type: 'toolkit', title: 'Wingspan Pro Pack', subtitle: 'Pubblicato · 5 strumenti',
    cover: DS.grad(85, 65), coverEmoji: '🧰', badge: 'Featured',
    version: 3, toolCount: 5, useCount: 89, gameId: 'g-wingspan', owner: 'p-luca',
    installCount: 542, featured: true, isNew: true, communityRating: 4.8 },
  { id: 'tk-spirit-elements', type: 'toolkit', title: 'Spirit Island Elements', subtitle: 'Pubblicato · 7 strumenti',
    cover: DS.grad(210, 50), coverEmoji: '🧰', badge: 'Top',
    version: 2, toolCount: 7, useCount: 56, gameId: 'g-spirit', owner: 'p-marco',
    installCount: 312, featured: true, isNew: true, communityRating: 4.6 },
  { id: 'tk-tabletop-tk', type: 'toolkit', title: 'Tabletop Essentials', subtitle: 'Pubblicato · 8 strumenti',
    cover: DS.grad(280, 60), coverEmoji: '🧰', badge: 'Universal',
    version: 4, toolCount: 8, useCount: 123, gameId: null, owner: 'p-sara',
    installCount: 892, featured: true, isNew: false, communityRating: 4.9 },
  { id: 'tk-dune-imp', type: 'toolkit', title: 'Dune Imperium Kit', subtitle: 'Pubblicato · 4 strumenti',
    cover: DS.grad(35, 70), coverEmoji: '🧰', badge: 'Nuovo',
    version: 1, toolCount: 4, useCount: 34, gameId: 'g-dune', owner: 'p-giulia',
    installCount: 178, featured: false, isNew: true, communityRating: 4.4 },
  { id: 'tk-coop-helpers', type: 'toolkit', title: 'Coop Game Helpers', subtitle: 'Pubblicato · 6 strumenti',
    cover: DS.grad(160, 50), coverEmoji: '🧰', badge: 'Cooperativo',
    version: 2, toolCount: 6, useCount: 78, gameId: null, owner: 'p-andrea',
    installCount: 423, featured: false, isNew: false, communityRating: 4.5 },
  { id: 'tk-deck-builder', type: 'toolkit', title: 'Deck Builder Pack', subtitle: 'Pubblicato · 9 strumenti',
    cover: DS.grad(0, 60), coverEmoji: '🧰', badge: 'Heavy',
    version: 3, toolCount: 9, useCount: 145, gameId: null, owner: 'p-marco',
    installCount: 678, featured: true, isNew: false, communityRating: 4.7 },
  { id: 'tk-7wonders', type: 'toolkit', title: '7 Wonders Tournament', subtitle: 'Pubblicato · 5 strumenti',
    cover: DS.grad(45, 65), coverEmoji: '🧰', badge: 'Tournament',
    version: 2, toolCount: 5, useCount: 67, gameId: 'g-7wonders', owner: 'p-sara',
    installCount: 289, featured: false, isNew: true, communityRating: 4.5 },
  { id: 'tk-marvel-champs', type: 'toolkit', title: 'Marvel Champions Aid', subtitle: 'Pubblicato · 6 strumenti',
    cover: DS.grad(355, 65), coverEmoji: '🧰', badge: 'LCG',
    version: 1, toolCount: 6, useCount: 41, gameId: 'g-marvel', owner: 'p-luca',
    installCount: 224, featured: false, isNew: true, communityRating: 4.3 },
];

// ═══════════════════════════════════════════════════════
const Stars = ({ value = 0, size = 11 }) => {
  const full = Math.floor(value);
  return (
    <span style={{ display:'inline-flex', gap: 1, color:'hsl(var(--c-agent))', fontSize: size }}>
      {[0,1,2,3,4].map(i => <span key={i} aria-hidden="true">{i < full ? '★' : '☆'}</span>)}
    </span>
  );
};

// ═══════════════════════════════════════════════════════
// ─── HUB TOOLKIT CARD ────────────────────────────
// ═══════════════════════════════════════════════════════
const HubToolkitCardGrid = ({ toolkit, compact }) => {
  const gameRef = toolkit.gameId ? DS.byId[toolkit.gameId] : null;
  return (
    <article tabIndex={0} className="mai-card-hub"
      style={{
        background:'var(--bg-card)',
        border:'1px solid var(--border)',
        borderRadius:'var(--r-lg)',
        overflow:'hidden',
        cursor:'pointer',
        position:'relative',
        transition:'transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm), box-shadow var(--dur-sm)',
        display:'flex', flexDirection:'column',
      }}>
      <div style={{
        position:'relative',
        aspectRatio: compact ? '4 / 3' : '5 / 3',
        background: toolkit.cover,
        display:'flex', alignItems:'center', justifyContent:'center',
        overflow:'hidden',
      }}>
        <span aria-hidden="true" style={{
          fontSize: compact ? 50 : 60,
          filter:'drop-shadow(0 4px 12px rgba(0,0,0,.35))',
        }}>{toolkit.coverEmoji}</span>
        {toolkit.badge && (
          <span style={{
            position:'absolute', top: 8, left: 8,
            padding:'3px 8px', borderRadius:'var(--r-pill)',
            background:'rgba(0,0,0,.5)', color:'#fff',
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.06em',
            backdropFilter:'blur(4px)',
          }}>{toolkit.badge}</span>
        )}
        <span style={{
          position:'absolute', top: 8, right: 8,
          padding:'3px 7px', borderRadius:'var(--r-pill)',
          background: entityHsl('toolkit', 0.95), color:'#fff',
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.06em',
          display:'inline-flex', alignItems:'center', gap: 3,
        }}>
          <span aria-hidden="true">🧰</span>Toolkit
        </span>
        <div style={{
          position:'absolute', inset:'auto 0 0 0', height:'40%',
          background:'linear-gradient(180deg, transparent 0%, rgba(0,0,0,.35) 100%)',
        }}/>
        <div style={{
          position:'absolute', bottom: 8, right: 8,
          display:'inline-flex', alignItems:'center', gap: 4,
          padding:'3px 8px', borderRadius:'var(--r-pill)',
          background:'rgba(255,255,255,.92)', color:'#1a1a1a',
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
        }}>
          <span aria-hidden="true">⬇</span>
          <span style={{ fontVariantNumeric:'tabular-nums' }}>{toolkit.installCount}</span>
        </div>
      </div>
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
          }}>{toolkit.title}</h3>
          <div style={{
            display:'flex', alignItems:'center', gap: 5,
            fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
            fontWeight: 600,
          }}>
            <Stars value={Math.round(toolkit.communityRating)} size={9}/>
            <span style={{ fontVariantNumeric:'tabular-nums' }}>{toolkit.communityRating.toFixed(1)}</span>
            <span aria-hidden="true">·</span>
            <span>v{toolkit.version}.0</span>
          </div>
        </div>
        <div style={{ display:'flex', flexDirection:'column', gap: 3 }}>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-sec)',
            fontWeight: 600,
          }}>
            <span style={{ color:'var(--text-muted)' }}>Strumenti:</span> {toolkit.toolCount} · <span style={{ color:'var(--text-muted)' }}>Uses:</span> {toolkit.useCount}
          </div>
          {gameRef ? (
            <div style={{
              display:'inline-flex', alignItems:'center', gap: 4,
              fontFamily:'var(--f-mono)', fontSize: 10,
              color: entityHsl('game'),
              fontWeight: 700,
            }}>
              <span aria-hidden="true">🎲</span>
              <span style={{ overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap' }}>{gameRef.title}</span>
            </div>
          ) : (
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
              fontWeight: 700, fontStyle:'italic',
            }}>Universale (multi-game)</div>
          )}
        </div>
      </div>
      <button type="button"
        className="mai-card-install"
        style={{
          position:'absolute',
          bottom: compact ? 10 : 14, left: compact ? 10 : 14, right: compact ? 10 : 14,
          padding:'7px 12px', borderRadius:'var(--r-md)',
          background: entityHsl('toolkit'), color:'#fff',
          border:'none',
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
          cursor:'pointer',
          boxShadow:`0 4px 14px ${entityHsl('toolkit', 0.35)}`,
          opacity: 0,
          transform:'translateY(8px)',
          transition:'opacity var(--dur-sm), transform var(--dur-sm)',
        }}>+ Installa toolkit</button>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── HUB TOOLKITS HERO ───────────────────────────
// ═══════════════════════════════════════════════════════
const HubToolkitsHero = ({ stats, compact }) => (
  <header style={{
    padding: compact ? '20px 16px 14px' : '32px 32px 18px',
    background:`linear-gradient(180deg, ${entityHsl('toolkit', 0.08)} 0%, transparent 100%)`,
    borderBottom:'1px solid var(--border-light)',
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 6, marginBottom: 10 }}>
      <span style={{
        display:'inline-flex', alignItems:'center', gap: 5,
        padding:'3px 9px', borderRadius:'var(--r-pill)',
        background: entityHsl('toolkit', 0.14),
        color: entityHsl('toolkit'),
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.08em',
      }}><span aria-hidden="true">🧰</span>Hub · /hub/toolkits</span>
    </div>
    <h1 style={{
      fontFamily:'var(--f-display)', fontWeight: 800,
      fontSize: compact ? 24 : 34, letterSpacing:'-.02em', lineHeight: 1.05,
      color:'var(--text)', margin:'0 0 6px',
    }}>Catalogo toolkit community</h1>
    <p style={{
      color:'var(--text-sec)', fontSize: compact ? 13 : 14, lineHeight: 1.55,
      margin:'0 0 14px', maxWidth: 620,
    }}>
      Bundle pronti all'uso di strumenti (timer, dadi, counter, deck) per i tuoi giochi.
      Ogni toolkit è versionato e pubblicato dalla community.
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
// ─── HUB FILTERS (shared) ────────────────────────
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
  { id:'uses', label:'Uses' },
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
        placeholder="Cerca per nome, gioco, categoria…"
        style={{
          flex: 1, border:'none', outline:'none', background:'transparent',
          color:'var(--text)', fontFamily:'var(--f-body)', fontSize: 13,
          minWidth: 0,
        }}
      />
      {q && (
        <button type="button" onClick={()=>setQ('')} aria-label="Pulisci"
          style={{ border:'none', background:'transparent', color:'var(--text-muted)', cursor:'pointer', fontSize: 13, padding: 0 }}>✕</button>
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
      }}>{count} toolkit</div>
    )}
  </div>
);

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
      background: entityHsl('toolkit', 0.12), color: entityHsl('toolkit'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: compact ? 32 : 40, marginBottom: 16,
    }} aria-hidden="true">🔎</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: compact ? 16 : 19, fontWeight: 800,
      color:'var(--text)', margin:'0 0 6px',
    }}>Nessun toolkit trovato</h3>
    <p style={{
      fontSize: 13, color:'var(--text-muted)', maxWidth: 360,
      margin:'0 0 18px', lineHeight: 1.55,
    }}>Nessun toolkit corrisponde ai filtri attuali. Prova a modificare la ricerca.</p>
    <button type="button" onClick={onAction}
      style={{
        padding:'10px 18px', borderRadius:'var(--r-md)',
        background: entityHsl('toolkit'), color:'#fff',
        border:'none', fontFamily:'var(--f-display)',
        fontSize: 13, fontWeight: 800, cursor:'pointer',
        boxShadow:`0 4px 14px ${entityHsl('toolkit', 0.35)}`,
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
    <button type="button" style={{
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
    out = out.filter(t =>
      t.title.toLowerCase().includes(needle) ||
      ((DS.byId[t.gameId] || {}).title || '').toLowerCase().includes(needle) ||
      (t.badge || '').toLowerCase().includes(needle)
    );
  }
  if (status !== 'all') {
    if (status === 'featured') out = out.filter(t => t.featured);
    else if (status === 'new') out = out.filter(t => t.isNew);
    else if (status === 'top') out = out.filter(t => t.communityRating >= 4.5);
  }
  out = [...out].sort((a, b) => {
    if (sort === 'rating') return b.communityRating - a.communityRating;
    if (sort === 'title') return a.title.localeCompare(b.title);
    if (sort === 'uses') return b.useCount - a.useCount;
    return b.installCount - a.installCount;
  });
  return out;
};

const HubToolkitsBody = ({ stateOverride, compact }) => {
  const [q, setQ] = useState('');
  const [status, setStatus] = useState('all');
  const [sort, setSort] = useState('popular');

  useEffect(() => {
    if (stateOverride === 'filtered-empty') setQ('xyznotfound');
    else { setQ(''); setStatus('all'); }
  }, [stateOverride]);

  const isLoading = stateOverride === 'loading';
  const isError = stateOverride === 'error';

  const filtered = useMemo(() => filterAndSort(HUB_TOOLKITS, q, status, sort),
    [q, status, sort]);

  const stats = [
    { label:'Toolkit', value: HUB_TOOLKITS.length, unit:'' },
    { label:'Installazioni', value: HUB_TOOLKITS.reduce((s, t) => s + t.installCount, 0).toLocaleString('it-IT'), unit:'' },
    { label:'Featured', value: HUB_TOOLKITS.filter(t => t.featured).length, unit:'' },
    { label:'Strumenti totali', value: HUB_TOOLKITS.reduce((s, t) => s + t.toolCount, 0), unit:'' },
  ];

  return (
    <>
      <HubToolkitsHero stats={stats} compact={compact}/>
      {!isError && (
        <HubFilters
          q={q} setQ={setQ}
          status={status} setStatus={setStatus}
          sort={sort} setSort={setSort}
          compact={compact} count={filtered.length} entity="toolkit"
        />
      )}

      <div style={{ padding: compact ? '14px 16px 24px' : '24px 32px 64px' }}>
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
              {filtered.map(t => <HubToolkitCardGrid key={t.id} toolkit={t} compact={compact}/>)}
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
const DesktopAuthNav = () => {
  const items = [
    { id:'home', label:'Home' },
    { id:'library', label:'Libreria' },
    { id:'hub', label:'Hub', active: true },
    { id:'sessions', label:'Sessioni' },
    { id:'toolkit', label:'Toolkit' },
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
              color: it.active ? entityHsl('toolkit') : 'var(--text-sec)',
              background: it.active ? entityHsl('toolkit', 0.1) : 'transparent',
              textDecoration:'none',
            }}>{it.label}</a>
        ))}
      </div>
      <div style={{ flex: 1 }}/>
      <button type="button" style={{
        padding:'7px 14px', borderRadius:'var(--r-md)',
        background: entityHsl('toolkit'), color:'#fff',
        border:'none', fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
        cursor:'pointer', boxShadow:`0 3px 10px ${entityHsl('toolkit', 0.35)}`,
      }}>+ Crea toolkit</button>
    </div>
  );
};

const DesktopScreen = ({ stateOverride }) => (
  <div style={{ minHeight: 720, background:'var(--bg)' }}>
    <DesktopAuthNav/>
    <HubToolkitsBody stateOverride={stateOverride}/>
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
      Hub · Toolkit
    </div>
    <button aria-label="Aggiungi" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background: entityHsl('toolkit'), border:'none',
      color:'#fff', fontSize: 16, cursor:'pointer', fontWeight: 800,
    }}>＋</button>
  </div>
);
const MobileBottomBar = () => {
  const tabs = [
    { id:'home', icon:'⌂', label:'Home' },
    { id:'library', icon:'📚', label:'Libreria' },
    { id:'hub', icon:'🌐', label:'Hub', active: true },
    { id:'chat', icon:'💬', label:'Chat' },
    { id:'profile', icon:'👤', label:'Profilo' },
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
          color: t.active ? entityHsl('toolkit') : 'var(--text-muted)',
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
const MobileScreen = ({ stateOverride }) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav/>
      <div style={{ paddingBottom: 70 }}>
        <HubToolkitsBody stateOverride={stateOverride} compact/>
      </div>
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
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/hub/toolkits</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ─── ROOT ────────────────────────────────────────────
const MOBILE_STATES = [
  { id:'default', label:'01 · Default', desc:'~12 toolkit, grid 2-col, install count overlay + install button hover-revealed.' },
  { id:'loading', label:'02 · Loading', desc:'6 skeleton cards shimmer.' },
  { id:'filtered-empty', label:'03 · Filtered empty', desc:'Query "xyznotfound" → empty filtrato + "Azzera filtri".' },
  { id:'error', label:'04 · Error', desc:'Banner danger + retry.' },
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
        }}>Pre-Stage-3 · #1148 · Hub Toolkits (authenticated)</div>
        <h1>Hub Toolkits — /hub/toolkits</h1>
        <p className="lead">
          Catalogo toolkit community per utenti loggati. Sibling structural di hub-agents
          (AC8: diff ≤ 30%). Entity color <strong>--c-toolkit</strong>, card variant
          con version + toolCount + useCount + game reference (o "Universale"). Install hover-revealed.
          4 stati mobile, 3 desktop.
        </p>

        <div className="section-label">Mobile · 375 — 4 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <MobileScreen stateOverride={s.id}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1280 — 3 stati</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="05 · Desktop · Default"
            desc="Hero stats inline (toolkit, installazioni, featured, strumenti totali), filtri sticky, grid 4-col. Hover su card rivela install button toolkit-tinted.">
            <DesktopScreen stateOverride="default"/>
          </DesktopFrame>
          <DesktopFrame label="06 · Desktop · Loading"
            desc="12 skeleton cards (4-col × 3 row) con shimmer.">
            <DesktopScreen stateOverride="loading"/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Filtered empty"
            desc="Query 'xyznotfound' → empty filtrato (icona 🔎) con CTA 'Azzera filtri' toolkit-tinted.">
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
          border-color: hsl(var(--c-toolkit) / .4) !important;
          box-shadow: var(--shadow-md);
        }
        .mai-card-hub:hover .mai-card-install,
        .mai-card-hub:focus-within .mai-card-install {
          opacity: 1 !important;
          transform: translateY(0) !important;
        }
        .mai-card-hub:focus-visible {
          outline: 2px solid hsl(var(--c-toolkit));
          outline-offset: 2px;
        }
        select:focus-visible, input:focus-visible, button:focus-visible, a:focus-visible {
          outline: 2px solid hsl(var(--c-toolkit));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
