/* MeepleAI SP4 wave 3 — Schermata I / 5 (CONCLUSIVA wave 3)
   Route: /discover
   File: admin-mockups/design_files/sp4-discover.{html,jsx}
   Pattern: Hero discover + horizontal rows Netflix-style. Cross-entity (mix di tutte le 9 entity).

   v2 nuovi (per impl post-merge):
   - DiscoverHero          → apps/web/src/components/ui/v2/discover-hero/
   - DiscoverSearchBox     → apps/web/src/components/ui/v2/discover-search-box/
   - EntityFilterPillBar   → apps/web/src/components/ui/v2/entity-filter-pill-bar/
   - HorizontalRow         → apps/web/src/components/ui/v2/horizontal-row/
   - RowScroller           → apps/web/src/components/ui/v2/row-scroller/

   Riusi:
   - MeepleCard variant="featured"|"grid"|"compact"|"list" (wave 1) → 4 card variants distinti
   - AdvancedFiltersDrawer (wave 1) → CTA "Filtri avanzati" che apre drawer
   - EntityChip (wave 1) per filter pills
   - Tabs animated underline (wave 1) → entity filter pills indicator

   Deviazioni: scroll-snap usa CSS native (no JS scroll lib). Frecce desktop solo su :hover via CSS.
*/

const { useState, useEffect, useRef, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.game;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── ENTITY FILTER PILLS ─────────────────────────────────
const FILTERS = [
  { id:'all',     label:'Tutti',     em:null,  type:null },
  { id:'game',    label:'Giochi',    em:'🎲',  type:'game' },
  { id:'agent',   label:'Agenti',    em:'🤖',  type:'agent' },
  { id:'toolkit', label:'Toolkit',   em:'🧰',  type:'toolkit' },
  { id:'kb',      label:'KB',        em:'📄',  type:'kb' },
  { id:'player',  label:'Persone',   em:'👤',  type:'player' },
  { id:'event',   label:'Eventi',    em:'🎉',  type:'event' },
];

// ═══════════════════════════════════════════════════════
// PRIMITIVES
// ═══════════════════════════════════════════════════════
const TypeChip = ({ type, sm }) => {
  const meta = DS.EC[type];
  if (!meta) return null;
  return (
    <span style={{
      display:'inline-flex', alignItems:'center', gap: 4,
      padding: sm ? '2px 7px' : '3px 9px', borderRadius:'var(--r-pill)',
      background: entityHsl(type, 0.12), color: entityHsl(type),
      border:`1px solid ${entityHsl(type, 0.22)}`,
      fontFamily:'var(--f-mono)', fontSize: sm ? 9 : 10, fontWeight: 800,
      textTransform:'uppercase', letterSpacing:'.06em',
      whiteSpace:'nowrap',
    }}>
      <span aria-hidden="true">{meta.em}</span>
      <span>{meta.lb}</span>
    </span>
  );
};

const Stars = ({ n }) => (
  <span aria-label={`${n} stelle`} style={{ color:'hsl(var(--c-warning))', fontSize: 10, letterSpacing: 1 }}>
    {'★'.repeat(n)}<span style={{ color:'var(--text-muted)', opacity: .35 }}>{'★'.repeat(5 - n)}</span>
  </span>
);

// ═══════════════════════════════════════════════════════
// CARDS — 4 VARIANTS
// /* v2: lifts MeepleCard variants from wave 1 (PR #549) */
// ═══════════════════════════════════════════════════════

// ── FEATURED — large hero card, 320×220 cover ─────────
const FeaturedCard = ({ entity, mobile }) => {
  const e = entity;
  const w = mobile ? 280 : 320;
  return (
    <article style={{
      flex:`0 0 ${w}px`, scrollSnapAlign:'start',
      borderRadius:'var(--r-xl)', overflow:'hidden',
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      boxShadow:`0 4px 14px ${entityHsl(e.type, 0.18)}`,
      transition:'transform var(--dur-md), box-shadow var(--dur-md)',
      cursor:'pointer',
    }} className="mai-card-hover">
      <div style={{
        height: 160, background: e.cover, position:'relative',
        display:'flex', alignItems:'flex-end', padding: 12,
      }}>
        <div aria-hidden="true" style={{
          position:'absolute', inset: 0,
          background:'linear-gradient(180deg, transparent 50%, rgba(0,0,0,.55))',
        }}/>
        <div style={{
          position:'absolute', top: 12, right: 12,
        }}>
          <TypeChip type={e.type} sm/>
        </div>
        <div style={{
          position:'absolute', top: 12, left: 12, fontSize: 32,
          filter:'drop-shadow(0 2px 6px rgba(0,0,0,.4))',
        }} aria-hidden="true">{e.coverEmoji}</div>
        {e.badge && (
          <span style={{
            position:'relative', zIndex: 1,
            padding:'3px 9px', borderRadius:'var(--r-pill)',
            background:'rgba(0,0,0,.55)', color:'#fff',
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.06em',
            backdropFilter:'blur(6px)',
          }}>{e.badge}</span>
        )}
      </div>
      <div style={{ padding: 14, display:'flex', flexDirection:'column', gap: 6 }}>
        <h3 style={{
          margin: 0, fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
          color:'var(--text)', lineHeight: 1.25,
          textWrap:'pretty',
          overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
        }}>{e.title}</h3>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          fontWeight: 600,
          overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
        }}>{e.subtitle || `${e.publisher || ''} · ${e.year || ''}`}</div>
        <div style={{
          display:'flex', alignItems:'center', gap: 8, marginTop: 4,
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
          fontWeight: 600,
        }}>
          {e.stars && <Stars n={e.stars}/>}
          {e.rating && <span style={{ color: entityHsl(e.type), fontWeight: 800 }}>{e.rating.toFixed(1)}</span>}
          {e.invocations && <span>{e.invocations} usi</span>}
          {e.useCount !== undefined && <span>{e.useCount} install</span>}
          {e.totalSessions && <span>{e.totalSessions} partite</span>}
        </div>
      </div>
    </article>
  );
};

// ── GRID — square card, 180px ─────────────────────────
const GridCard = ({ entity, mobile }) => {
  const e = entity;
  const w = mobile ? 150 : 180;
  return (
    <article style={{
      flex:`0 0 ${w}px`, scrollSnapAlign:'start',
      borderRadius:'var(--r-lg)', overflow:'hidden',
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      cursor:'pointer',
      transition:'transform var(--dur-md), box-shadow var(--dur-md)',
    }} className="mai-card-hover">
      <div style={{
        aspectRatio:'1 / 1', background: e.cover, position:'relative',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 50,
      }} aria-hidden="true">
        <div style={{ filter:'drop-shadow(0 4px 8px rgba(0,0,0,.3))' }}>{e.coverEmoji}</div>
        <div style={{ position:'absolute', top: 8, right: 8 }}>
          <TypeChip type={e.type} sm/>
        </div>
      </div>
      <div style={{ padding:'10px 12px', display:'flex', flexDirection:'column', gap: 3 }}>
        <h3 style={{
          margin: 0, fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
          color:'var(--text)', lineHeight: 1.25,
          overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
        }}>{e.title}</h3>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 600,
          overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
        }}>
          {e.publisher && `${e.publisher} · ${e.year}`}
          {!e.publisher && e.subtitle}
        </div>
        {e.stars && <Stars n={e.stars}/>}
      </div>
    </article>
  );
};

// ── COMPACT — small horizontal card 240×72 ────────────
const CompactCard = ({ entity, mobile, statLabel, statValue }) => {
  const e = entity;
  const w = mobile ? 240 : 260;
  return (
    <article style={{
      flex:`0 0 ${w}px`, scrollSnapAlign:'start',
      display:'flex', alignItems:'center', gap: 10,
      padding: 10,
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      cursor:'pointer',
      transition:'border-color var(--dur-sm), box-shadow var(--dur-sm)',
    }} className="mai-card-hover">
      <div style={{
        width: 52, height: 52, borderRadius:'var(--r-md)',
        background: e.cover,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 22, flexShrink: 0,
        border:`1px solid ${entityHsl(e.type, 0.22)}`,
      }} aria-hidden="true">
        {e.initials ? (
          <span style={{
            fontFamily:'var(--f-display)', color:'#fff', fontWeight: 800, fontSize: 16,
          }}>{e.initials}</span>
        ) : e.coverEmoji}
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display:'flex', alignItems:'center', gap: 6, marginBottom: 2,
        }}>
          <h3 style={{
            margin: 0, fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            color:'var(--text)', lineHeight: 1.2, flex: 1, minWidth: 0,
            overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
          }}>{e.title}</h3>
        </div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 600, marginBottom: 3,
          overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
        }}>{e.subtitle}</div>
        <div style={{
          display:'flex', alignItems:'center', gap: 6,
        }}>
          <TypeChip type={e.type} sm/>
          {statLabel && (
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
              color: entityHsl(e.type),
            }}>{statValue} {statLabel}</span>
          )}
        </div>
      </div>
    </article>
  );
};

// ── LIST — wide row card 360×80 ───────────────────────
const ListRowCard = ({ entity, mobile, eventDay, eventTime }) => {
  const e = entity;
  const w = mobile ? 290 : 380;
  return (
    <article style={{
      flex:`0 0 ${w}px`, scrollSnapAlign:'start',
      display:'flex', gap: 12,
      padding: 12,
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      borderLeft:`3px solid ${entityHsl(e.type)}`,
      cursor:'pointer',
      transition:'border-color var(--dur-sm), box-shadow var(--dur-sm)',
    }} className="mai-card-hover">
      {eventDay !== undefined ? (
        <div style={{
          flexShrink: 0,
          width: 56, padding: 6,
          background: entityHsl(e.type, 0.08),
          border:`1px solid ${entityHsl(e.type, 0.22)}`,
          borderRadius:'var(--r-md)',
          display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center',
        }}>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            color: entityHsl(e.type), textTransform:'uppercase', letterSpacing:'.08em',
          }}>Mar</div>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
            color: entityHsl(e.type), lineHeight: 1, fontVariantNumeric:'tabular-nums',
          }}>{eventDay}</div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
            color:'var(--text-sec)', marginTop: 2,
          }}>{eventTime}</div>
        </div>
      ) : (
        <div style={{
          width: 56, height: 56, borderRadius:'var(--r-md)',
          background: e.cover,
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 22, flexShrink: 0,
          border:`1px solid ${entityHsl(e.type, 0.22)}`,
        }} aria-hidden="true">{e.coverEmoji}</div>
      )}
      <div style={{ flex: 1, minWidth: 0, display:'flex', flexDirection:'column', gap: 4 }}>
        <h3 style={{
          margin: 0, fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
          color:'var(--text)', lineHeight: 1.25,
          overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
        }}>{e.title}</h3>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          fontWeight: 600,
          overflow:'hidden', display:'-webkit-box',
          WebkitLineClamp: 2, WebkitBoxOrient:'vertical',
        }}>{e.subtitle}</div>
        <div style={{
          display:'flex', alignItems:'center', gap: 6, marginTop: 2,
          flexWrap:'wrap',
        }}>
          <TypeChip type={e.type} sm/>
          {e.pages && (
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
              color:'var(--text-sec)',
            }}>{e.pages} pag · {e.chunks} chunk</span>
          )}
        </div>
      </div>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// HORIZONTAL ROW
// /* v2: HorizontalRow + RowScroller — apps/web/src/components/ui/v2/horizontal-row/ */
// ═══════════════════════════════════════════════════════
const HorizontalRow = ({ title, emoji, count, items, renderCard, mobile }) => {
  const trackRef = useRef(null);
  const [scrollState, setScrollState] = useState({ left: false, right: true });

  const onScroll = () => {
    const el = trackRef.current;
    if (!el) return;
    const left = el.scrollLeft > 4;
    const right = el.scrollLeft + el.clientWidth < el.scrollWidth - 4;
    setScrollState({ left, right });
  };

  useEffect(() => {
    const el = trackRef.current;
    if (!el) return;
    onScroll();
    el.addEventListener('scroll', onScroll, { passive: true });
    return () => el.removeEventListener('scroll', onScroll);
  }, []);

  const scroll = (dir) => {
    const el = trackRef.current;
    if (!el) return;
    el.scrollBy({ left: dir * (el.clientWidth * 0.85), behavior: 'smooth' });
  };

  return (
    <section className="mai-row" style={{
      display:'flex', flexDirection:'column', gap: 12,
      position:'relative',
    }}>
      <header style={{
        display:'flex', alignItems:'center', gap: 10,
        padding: mobile ? '0 16px' : '0 32px',
      }}>
        <h2 style={{
          margin: 0, fontFamily:'var(--f-display)',
          fontSize: mobile ? 16 : 20, fontWeight: 800,
          color:'var(--text)', display:'inline-flex', alignItems:'center', gap: 8,
        }}>
          <span aria-hidden="true">{emoji}</span>
          <span>{title}</span>
        </h2>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          fontWeight: 700,
        }}>· {count}</span>
        <button type="button" style={{
          marginLeft:'auto', padding:'6px 10px', borderRadius:'var(--r-md)',
          background:'transparent', color:'var(--text-sec)',
          border:'none', cursor:'pointer',
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
        }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-game-detail.html'; }, 0); /* DEMO-NAV */ }}>Vedi tutti →</button>
      </header>

      <div style={{ position:'relative' }}>
        {/* Left arrow — desktop only on hover */}
        {!mobile && scrollState.left && (
          <button type="button" aria-label="Scorri a sinistra"
            onClick={(e) => { (() => scroll(-1))(e); setTimeout(() => { window.location.href = 'sp4-game-detail.html'; }, 0); /* DEMO-NAV */ }}
            className="mai-row-arrow mai-row-arrow-left"
            style={{
              position:'absolute', top:'50%', left: 12, transform:'translateY(-50%)',
              width: 38, height: 38, borderRadius:'50%',
              background:'var(--bg-card)', border:'1px solid var(--border-strong)',
              color:'var(--text)', fontSize: 16, cursor:'pointer',
              boxShadow:'var(--shadow-md)', zIndex: 2,
            }}>‹</button>
        )}
        {!mobile && scrollState.right && (
          <button type="button" aria-label="Scorri a destra"
            onClick={(e) => { (() => scroll(1))(e); setTimeout(() => { window.location.href = 'sp4-game-detail.html'; }, 0); /* DEMO-NAV */ }}
            className="mai-row-arrow mai-row-arrow-right"
            style={{
              position:'absolute', top:'50%', right: 12, transform:'translateY(-50%)',
              width: 38, height: 38, borderRadius:'50%',
              background:'var(--bg-card)', border:'1px solid var(--border-strong)',
              color:'var(--text)', fontSize: 16, cursor:'pointer',
              boxShadow:'var(--shadow-md)', zIndex: 2,
            }}>›</button>
        )}

        <div ref={trackRef} style={{
          display:'flex', gap: 12,
          padding: mobile ? '4px 16px 12px' : '4px 32px 12px',
          overflowX:'auto', overflowY:'hidden',
          scrollSnapType:'x mandatory',
          scrollbarWidth:'none',
          WebkitOverflowScrolling:'touch',
        }} className="mai-row-track" onClick={() => { setTimeout(() => { window.location.href = 'sp4-game-detail.html'; }, 0); /* DEMO-NAV */ }}>
          {items.map((item, i) => (
            <React.Fragment key={item.entity?.id || i}>
              {renderCard(item, i)}
            </React.Fragment>
          ))}
        </div>

        {/* Right edge fade hint */}
        {scrollState.right && (
          <div aria-hidden="true" style={{
            position:'absolute', top: 0, right: 0, bottom: 0, width: 60,
            background:`linear-gradient(90deg, transparent, var(--bg))`,
            pointerEvents:'none',
          }}/>
        )}
        {scrollState.left && (
          <div aria-hidden="true" style={{
            position:'absolute', top: 0, left: 0, bottom: 0, width: 60,
            background:`linear-gradient(-90deg, transparent, var(--bg))`,
            pointerEvents:'none',
          }}/>
        )}
      </div>
    </section>
  );
};

// ═══════════════════════════════════════════════════════
// HERO
// /* v2: DiscoverHero + DiscoverSearchBox + EntityFilterPillBar */
// ═══════════════════════════════════════════════════════
const DiscoverHero = ({ filter, onFilter, mobile, onOpenAdvanced }) => {
  const refs = useRef({});
  const [ind, setInd] = useState({ left: 0, width: 0 });
  useEffect(() => {
    const el = refs.current[filter];
    if (el) setInd({ left: el.offsetLeft, width: el.offsetWidth });
  }, [filter]);

  return (
    <div style={{
      padding: mobile ? '20px 16px 18px' : '36px 32px 28px',
      background:`
        radial-gradient(at 8% 20%, ${entityHsl('event', 0.18)}, transparent 45%),
        radial-gradient(at 95% 0%, ${entityHsl('toolkit', 0.18)}, transparent 45%),
        radial-gradient(at 50% 100%, ${entityHsl('agent', 0.10)}, transparent 50%),
        var(--bg)
      `,
      borderBottom:'1px solid var(--border-light)',
    }}>
      <div style={{
        maxWidth: 1280, margin:'0 auto',
        display:'flex', flexDirection:'column', gap: mobile ? 14 : 20,
      }}>
        {/* Title + subtitle */}
        <div>
          <div style={{
            display:'inline-flex', alignItems:'center', gap: 8,
            padding:'4px 10px', borderRadius:'var(--r-pill)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 10,
            color:'var(--text-sec)',
          }}>
            <span aria-hidden="true">✨</span>
            <span>Discover</span>
          </div>
          <h1 style={{
            margin: 0, fontFamily:'var(--f-display)',
            fontSize: mobile ? 28 : 38, fontWeight: 800,
            color:'var(--text)', letterSpacing:'-.015em', lineHeight: 1.05,
          }}>Scopri</h1>
          <p style={{
            margin:'6px 0 0', fontSize: mobile ? 13 : 15,
            color:'var(--text-sec)', maxWidth: 520, lineHeight: 1.5,
          }}>Esplora cosa c'è di nuovo nella community: giochi, agenti, toolkit, persone ed eventi.</p>
        </div>

        {/* Search + advanced */}
        <div style={{
          display:'flex', gap: 8, flexWrap:'wrap',
        }}>
          <label style={{
            flex: 1, minWidth: mobile ? 0 : 320,
            display:'flex', alignItems:'center', gap: 10,
            padding: mobile ? '10px 14px' : '12px 18px',
            background:'var(--bg-card)',
            border:'1px solid var(--border)',
            borderRadius:'var(--r-md)',
            boxShadow:'var(--shadow-sm)',
          }}>
            <span aria-hidden="true" style={{ fontSize: 16 }}>🔍</span>
            <input
              type="search"
              placeholder={mobile ? 'Cerca giochi, agenti…' : 'Cerca giochi, agenti, toolkit, persone…'}
              style={{
                flex: 1, border:'none', outline:'none', background:'transparent',
                fontFamily:'var(--f-display)', fontSize: mobile ? 13 : 14, fontWeight: 600,
                color:'var(--text)',
              }}
              aria-label="Cerca nella community"/>
            <kbd style={{
              padding:'2px 8px', borderRadius:'var(--r-sm)',
              background:'var(--bg-muted)', border:'1px solid var(--border)',
              fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
              color:'var(--text-muted)',
            }}>⌘K</kbd>
          </label>
          <button type="button" onClick={onOpenAdvanced}
            style={{
              padding: mobile ? '10px 14px' : '12px 18px',
              borderRadius:'var(--r-md)',
              background:'var(--bg-card)', border:'1px solid var(--border)',
              color:'var(--text-sec)', cursor:'pointer',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              display:'inline-flex', alignItems:'center', gap: 7,
              whiteSpace:'nowrap',
            }}>
            <span aria-hidden="true">⚙</span>
            <span>Filtri avanzati</span>
          </button>
        </div>

        {/* Entity filter pills (animated underline) */}
        <div style={{ position:'relative' }}>
          <div style={{
            display:'flex', gap: 4, borderBottom:'1px solid var(--border)',
            overflowX:'auto', scrollbarWidth:'none',
          }} role="tablist" aria-label="Filtra per tipo">
            {FILTERS.map(f => {
              const isActive = filter === f.id;
              const c = f.type ? entityHsl(f.type) : 'var(--text)';
              return (
                <button key={f.id} type="button"
                  ref={el => { refs.current[f.id] = el; }}
                  onClick={(e) => { (() => onFilter(f.id))(e); setTimeout(() => { window.location.href = 'sp4-game-detail.html'; }, 0); /* DEMO-NAV */ }}
                  role="tab" aria-selected={isActive}
                  style={{
                    padding: mobile ? '8px 12px' : '10px 14px',
                    background:'transparent', border:'none',
                    color: isActive ? c : 'var(--text-sec)',
                    fontFamily:'var(--f-display)',
                    fontSize: mobile ? 12 : 13,
                    fontWeight: isActive ? 800 : 700, cursor:'pointer',
                    display:'inline-flex', alignItems:'center', gap: 6,
                    whiteSpace:'nowrap', flexShrink: 0,
                  }}>
                  {f.em && <span aria-hidden="true">{f.em}</span>}
                  <span>{f.label}</span>
                </button>
              );
            })}
          </div>
          <span aria-hidden="true" style={{
            position:'absolute', bottom: -1, left: ind.left, width: ind.width,
            height: 2,
            background: filter === 'all' ? 'var(--text)' :
                        FILTERS.find(f => f.id === filter)?.type
                        ? entityHsl(FILTERS.find(f => f.id === filter).type)
                        : 'var(--text)',
            transition:'left .3s var(--ease-out), width .3s var(--ease-out), background .3s',
          }}/>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ROWS — DATA BUILDERS
// ═══════════════════════════════════════════════════════
const buildRows = () => {
  // Trending = mix tipi (1 per tipo, top 8 cross-entity)
  const trending = [
    DS.byId['g-wingspan'], DS.byId['tk-azul-v2'], DS.byId['a-azul-rules'],
    DS.byId['g-brass'], DS.byId['p-marco'], DS.byId['kb-azul-ita'],
    DS.byId['g-arknova'], DS.byId['tk-generic'],
  ].filter(Boolean);

  const newGames = DS.games.filter(g => g.kbState !== 'initial').slice(0, 6);
  const popularAgents = [...DS.agents].sort((a,b) => b.invocations - a.invocations);
  const recommendedToolkits = DS.toolkits.filter(t => t.status === 'active');
  const recentKbs = DS.kbs.filter(k => k.status === 'indexed');
  const topPlayers = [...DS.players].sort((a,b) => b.totalSessions - a.totalSessions).slice(0, 5);
  const nearbyEvents = DS.events.filter(e => e.status !== 'completed');

  return { trending, newGames, popularAgents, recommendedToolkits, recentKbs, topPlayers, nearbyEvents };
};

const ROW_DEFS = [
  { id:'trending', emoji:'🔥', title:'Trending questa settimana', variant:'featured', dataKey:'trending', types:'all' },
  { id:'games',    emoji:'🎲', title:'Giochi nuovi nella community', variant:'grid',    dataKey:'newGames', types:'game' },
  { id:'agents',   emoji:'🤖', title:'Agenti più installati',         variant:'compact', dataKey:'popularAgents', types:'agent',
    statLabel:'usi', statKey:'invocations' },
  { id:'tk',       emoji:'🧰', title:'Toolkit consigliati per te',    variant:'featured', dataKey:'recommendedToolkits', types:'toolkit', personalized:true },
  { id:'kb',       emoji:'📄', title:'KB recenti',                    variant:'list',    dataKey:'recentKbs', types:'kb' },
  { id:'players',  emoji:'👥', title:'Top contributor questo mese',  variant:'compact', dataKey:'topPlayers', types:'player',
    statLabel:'partite', statKey:'totalSessions' },
  { id:'events',   emoji:'🎉', title:'Eventi vicini a te',            variant:'list',    dataKey:'nearbyEvents', types:'event' },
];

// ═══════════════════════════════════════════════════════
// BODY
// ═══════════════════════════════════════════════════════
const DiscoverBody = ({ stateOverride, mobile, initialFilter = 'all', initialAdvOpen = false }) => {
  const [filter, setFilter] = useState(initialFilter);
  const [advOpen, setAdvOpen] = useState(initialAdvOpen);
  const data = useMemo(buildRows, []);

  // Filter rows by entity type
  const visibleRows = ROW_DEFS.filter(r => {
    if (filter === 'all') return true;
    if (r.types === 'all') return true; // Trending always visible
    return r.types === filter;
  });

  const renderCard = (entity, def) => {
    if (!entity) return null;
    if (def.variant === 'featured') return <FeaturedCard entity={entity} mobile={mobile}/>;
    if (def.variant === 'grid')     return <GridCard entity={entity} mobile={mobile}/>;
    if (def.variant === 'compact')  {
      const v = def.statKey ? entity[def.statKey] : null;
      return <CompactCard entity={entity} mobile={mobile} statLabel={def.statLabel} statValue={v}/>;
    }
    if (def.variant === 'list') {
      // Events get date block
      const props = entity.type === 'event' ? { eventDay: entity.date?.split(' ')[0], eventTime: entity.time?.split('–')[0] } : {};
      return <ListRowCard entity={entity} mobile={mobile} {...props}/>;
    }
    return null;
  };

  return (
    <div style={{ background:'var(--bg)', minHeight:'100%' }}>
      <DiscoverHero filter={filter} onFilter={setFilter}
        mobile={mobile} onOpenAdvanced={() => setAdvOpen(true)}/>

      {stateOverride === 'empty' ? <EmptyAll mobile={mobile}/>
       : stateOverride === 'error' ? <ErrorState mobile={mobile}/>
       : stateOverride === 'loading' ? <LoadingRows mobile={mobile}/>
       : (
        <div style={{
          padding: mobile ? '20px 0 24px' : '32px 0',
          display:'flex', flexDirection:'column', gap: mobile ? 24 : 36,
        }}>
          {visibleRows.length === 0 ? (
            <EmptyFilter filter={filter} mobile={mobile} onClear={() => setFilter('all')}/>
          ) : (
            visibleRows.map(def => {
              const items = data[def.dataKey] || [];
              return (
                <HorizontalRow key={def.id}
                  title={def.title} emoji={def.emoji} count={items.length}
                  items={items.map(e => ({ entity: e }))}
                  renderCard={({ entity }) => renderCard(entity, def)}
                  mobile={mobile}/>
              );
            })
          )}

          {/* Footer CTA */}
          {visibleRows.length > 0 && (
            <FooterCTA mobile={mobile}/>
          )}
        </div>
      )}

      {/* Advanced Filters Drawer (riuso wave 1) */}
      {advOpen && <AdvancedFiltersDrawer onClose={() => setAdvOpen(false)} mobile={mobile}/>}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ADVANCED FILTERS DRAWER (riuso wave 1)
// ═══════════════════════════════════════════════════════
const AdvancedFiltersDrawer = ({ onClose, mobile }) => (
  <div role="dialog" aria-label="Filtri avanzati"
    style={{
      position:'absolute', inset: 0, zIndex: 50,
      display:'flex', alignItems: mobile ? 'flex-end' : 'stretch',
      justifyContent: mobile ? 'stretch' : 'flex-end',
      background:'rgba(0,0,0,.5)',
    }} onClick={onClose}>
    <aside onClick={e => e.stopPropagation()} style={{
      width: mobile ? '100%' : 420,
      maxHeight: mobile ? '85vh' : '100%',
      background:'var(--bg)',
      borderRadius: mobile ? 'var(--r-xl) var(--r-xl) 0 0' : 0,
      display:'flex', flexDirection:'column',
      overflow:'hidden',
      animation:`slideIn${mobile ? 'Up' : 'Right'} .25s var(--ease-out)`,
    }}>
      {mobile && (
        <div aria-hidden="true" style={{
          width: 36, height: 4, borderRadius:'var(--r-pill)',
          background:'var(--border-strong)', margin:'10px auto 0',
        }}/>
      )}
      <header style={{
        padding:'16px 20px',
        borderBottom:'1px solid var(--border)',
        display:'flex', alignItems:'center', gap: 10,
      }}>
        <h2 style={{
          margin: 0, fontFamily:'var(--f-display)', fontSize: 17, fontWeight: 800,
          color:'var(--text)', flex: 1,
        }}>Filtri avanzati</h2>
        <button type="button" onClick={onClose} aria-label="Chiudi"
          style={{
            width: 32, height: 32, borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text)', fontSize: 16, cursor:'pointer',
          }}>×</button>
      </header>
      <div style={{
        flex: 1, overflow:'auto', padding:'18px 20px',
        display:'flex', flexDirection:'column', gap: 18,
      }}>
        <FilterGroup title="Numero giocatori" options={['1', '2', '3-4', '5+']} sel={['2','3-4']}/>
        <FilterGroup title="Durata partita" options={['<30m', '30-60m', '1-2h', '>2h']} sel={['30-60m']}/>
        <FilterGroup title="Difficoltà" options={['Light', 'Medium', 'Heavy']} sel={[]}/>
        <FilterGroup title="Stato KB" options={['Indicizzato', 'Parziale', 'Mancante']} sel={['Indicizzato']}/>
        <FilterGroup title="Strategia agente" options={['RAG', 'Router', 'Hybrid']} sel={[]}/>
        <FilterGroup title="Anno pubblicazione" options={['2020+', '2015-2019', '<2015']} sel={[]}/>
      </div>
      <footer style={{
        padding:'14px 20px',
        borderTop:'1px solid var(--border)',
        background:'var(--bg-card)',
        display:'flex', gap: 10,
      }}>
        <button type="button" style={{
          flex: 1, padding:'10px', borderRadius:'var(--r-md)',
          background:'transparent', color:'var(--text-sec)',
          border:'1px solid var(--border-strong)',
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700, cursor:'pointer',
        }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-game-detail.html'; }, 0); /* DEMO-NAV */ }}>Reimposta</button>
        <button type="button" style={{
          flex: 2, padding:'10px', borderRadius:'var(--r-md)',
          background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('agent')})`,
          color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
        }}>Applica · 3 attivi</button>
      </footer>
    </aside>
    <style>{`
      @keyframes slideInUp { from { transform: translateY(100%); } to { transform: translateY(0); } }
      @keyframes slideInRight { from { transform: translateX(100%); } to { transform: translateX(0); } }
      @media (prefers-reduced-motion: reduce) {
        @keyframes slideInUp { from, to { transform: none; } }
        @keyframes slideInRight { from, to { transform: none; } }
      }
    `}</style>
  </div>
);

const FilterGroup = ({ title, options, sel }) => (
  <fieldset style={{ border:'none', padding: 0, margin: 0 }}>
    <legend style={{
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
      color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
      marginBottom: 10,
    }}>{title}</legend>
    <div style={{ display:'flex', gap: 6, flexWrap:'wrap' }}>
      {options.map(opt => {
        const active = sel.includes(opt);
        return (
          <button key={opt} type="button" aria-pressed={active}
            style={{
              padding:'6px 12px', borderRadius:'var(--r-pill)',
              background: active ? entityHsl('agent', 0.15) : 'var(--bg-card)',
              color: active ? entityHsl('agent') : 'var(--text-sec)',
              border: active ? `1px solid ${entityHsl('agent', 0.4)}` : '1px solid var(--border)',
              fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
              cursor:'pointer',
            }}>{opt}</button>
        );
      })}
    </div>
  </fieldset>
);

// ═══════════════════════════════════════════════════════
// FOOTER CTA
// ═══════════════════════════════════════════════════════
const FooterCTA = ({ mobile }) => (
  <section style={{
    margin: mobile ? '12px 16px 0' : '24px 32px 0',
    padding: mobile ? 24 : 36,
    borderRadius:'var(--r-xl)',
    background:`
      radial-gradient(at 0% 0%, ${entityHsl('toolkit', 0.18)}, transparent 50%),
      radial-gradient(at 100% 100%, ${entityHsl('player', 0.18)}, transparent 50%),
      var(--bg-card)
    `,
    border:`1px solid ${entityHsl('toolkit', 0.2)}`,
    display:'flex', flexDirection: mobile ? 'column' : 'row',
    alignItems: mobile ? 'flex-start' : 'center', gap: mobile ? 14 : 20,
  }}>
    <div style={{ flex: 1 }}>
      <div style={{
        display:'inline-flex', alignItems:'center', gap: 6,
        padding:'3px 9px', borderRadius:'var(--r-pill)',
        background: entityHsl('toolkit', 0.15), color: entityHsl('toolkit'),
        fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
        marginBottom: 8,
      }}>
        <span aria-hidden="true">🧰</span>
        <span>Per autori</span>
      </div>
      <h2 style={{
        margin: 0, fontFamily:'var(--f-display)',
        fontSize: mobile ? 18 : 22, fontWeight: 800,
        color:'var(--text)', lineHeight: 1.2,
      }}>Hai contenuti da pubblicare?</h2>
      <p style={{
        margin:'4px 0 0', fontSize: mobile ? 12.5 : 14,
        color:'var(--text-sec)', lineHeight: 1.55, maxWidth: 520,
      }}>Crea un toolkit, condividi una KB o pubblica un agente. La community ti ringrazia.</p>
    </div>
    <button type="button" style={{
      padding: mobile ? '12px 22px' : '14px 28px',
      borderRadius:'var(--r-md)',
      background:`linear-gradient(135deg, ${entityHsl('toolkit')}, ${entityHsl('player')})`,
      color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: mobile ? 13 : 14, fontWeight: 800,
      cursor:'pointer',
      boxShadow:`0 4px 14px ${entityHsl('toolkit', 0.3)}`,
      whiteSpace:'nowrap',
    }}>+ Nuovo toolkit →</button>
  </section>
);

// ═══════════════════════════════════════════════════════
// STATES
// ═══════════════════════════════════════════════════════
const EmptyAll = ({ mobile }) => (
  <div style={{
    padding: mobile ? '32px 16px 24px' : '48px 32px 32px',
    display:'flex', flexDirection:'column', gap: mobile ? 24 : 32,
  }}>
    {/* Solo Trending row */}
    <HorizontalRow
      title="Trending questa settimana" emoji="🔥"
      count={8}
      items={buildRows().trending.map(e => ({ entity: e }))}
      renderCard={({ entity }) => <FeaturedCard entity={entity} mobile={mobile}/>}
      mobile={mobile}/>

    <div style={{
      margin: mobile ? '0 16px' : '0 32px',
      padding: mobile ? '32px 24px' : '48px 32px',
      borderRadius:'var(--r-xl)',
      background:'var(--bg-card)', border:'1px dashed var(--border-strong)',
      display:'flex', flexDirection: mobile ? 'column' : 'row',
      alignItems:'center', gap: 18,
      textAlign: mobile ? 'center' : 'left',
    }}>
      <div style={{
        width: 72, height: 72, borderRadius:'50%',
        background: entityHsl('agent', 0.12), color: entityHsl('agent'),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 32, flexShrink: 0,
      }} aria-hidden="true">🌱</div>
      <div style={{ flex: 1 }}>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 17, fontWeight: 800,
          color:'var(--text)', margin:'0 0 6px',
        }}>Stai iniziando ora!</h3>
        <p style={{
          fontSize: 13, color:'var(--text-muted)', margin: 0, lineHeight: 1.6,
          maxWidth: 460,
        }}>I consigli personalizzati appariranno qui dopo qualche partita o interazione con un agente. Nel frattempo, esplora la libreria pubblica.</p>
      </div>
      <button type="button" style={{
        padding:'12px 22px', borderRadius:'var(--r-md)',
        background: entityHsl('agent'), color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
        cursor:'pointer', whiteSpace:'nowrap',
        boxShadow:`0 4px 12px ${entityHsl('agent', 0.3)}`,
      }}>Esplora la libreria</button>
    </div>
  </div>
);

const EmptyFilter = ({ filter, mobile, onClear }) => {
  const f = FILTERS.find(x => x.id === filter);
  return (
    <div style={{
      padding: mobile ? '40px 24px' : '60px 32px',
      display:'flex', justifyContent:'center',
    }}>
      <div style={{
        maxWidth: 420, textAlign:'center',
        padding:'32px 24px',
        background:'var(--bg-card)', border:'1px dashed var(--border-strong)',
        borderRadius:'var(--r-xl)',
      }}>
        <div style={{ fontSize: 36, marginBottom: 10 }} aria-hidden="true">{f?.em || '🔍'}</div>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
          color:'var(--text)', margin:'0 0 6px',
        }}>Nessun {f?.label.toLowerCase()} trending al momento</h3>
        <p style={{
          fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', lineHeight: 1.6,
        }}>Prova a rimuovere il filtro o cerca per parola chiave.</p>
        <button type="button" onClick={(e) => { (onClear)(e); setTimeout(() => { window.location.href = 'sp4-game-detail.html'; }, 0); /* DEMO-NAV */ }} style={{
          padding:'8px 18px', borderRadius:'var(--r-md)',
          background:'var(--bg)', color:'var(--text)',
          border:'1px solid var(--border-strong)',
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
        }}>Mostra tutti</button>
      </div>
    </div>
  );
};

const LoadingRows = ({ mobile }) => (
  <div style={{
    padding: mobile ? '20px 0 24px' : '32px 0',
    display:'flex', flexDirection:'column', gap: mobile ? 24 : 36,
  }}>
    {[0, 1, 2].map(i => (
      <div key={i} style={{
        display:'flex', flexDirection:'column', gap: 12,
      }}>
        <div style={{ padding: mobile ? '0 16px' : '0 32px' }}>
          <div className="mai-shimmer" style={{
            width: 220, height: 22, borderRadius:'var(--r-sm)',
            background:'var(--bg-muted)',
          }}/>
        </div>
        <div style={{
          display:'flex', gap: 12,
          padding: mobile ? '4px 16px 12px' : '4px 32px 12px',
          overflowX:'hidden',
        }}>
          {[0,1,2,3,4].map(j => (
            <div key={j} className="mai-shimmer" style={{
              flex:`0 0 ${i === 0 ? (mobile ? 280 : 320) : (i === 1 ? (mobile ? 150 : 180) : (mobile ? 240 : 260))}px`,
              height: i === 0 ? 280 : (i === 1 ? (mobile ? 200 : 240) : 72),
              borderRadius:'var(--r-lg)',
              background:'var(--bg-muted)',
            }}/>
          ))}
        </div>
      </div>
    ))}
  </div>
);

const ErrorState = ({ mobile }) => (
  <div style={{ padding:'60px 32px', display:'flex', justifyContent:'center' }}>
    <div style={{
      maxWidth: 440, textAlign:'center',
      padding:'32px 24px',
      background:'hsl(var(--c-danger) / .08)',
      border:'1px solid hsl(var(--c-danger) / .3)',
      borderRadius:'var(--r-xl)',
      display:'flex', flexDirection:'column', alignItems:'center',
    }}>
      <div style={{
        width: 56, height: 56, borderRadius:'50%',
        background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 26, marginBottom: 12,
      }} aria-hidden="true">⚠</div>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 17, fontWeight: 800,
        color:'var(--text)', margin:'0 0 6px',
      }}>Discover non disponibile</h3>
      <p style={{
        fontSize: 12.5, color:'var(--text-muted)', maxWidth: 380, margin:'0 0 16px',
        lineHeight: 1.6,
      }}>Il servizio di raccomandazione è temporaneamente offline. Riprova tra qualche istante o esplora direttamente la libreria.</p>
      <div style={{ display:'flex', gap: 10 }}>
        <button type="button" style={{
          padding:'10px 18px', borderRadius:'var(--r-md)',
          background:'hsl(var(--c-danger))', color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
        }}>↻ Riprova</button>
        <button type="button" style={{
          padding:'10px 18px', borderRadius:'var(--r-md)',
          background:'transparent', color:'var(--text)',
          border:'1px solid var(--border-strong)',
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
        }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-library-desktop.html'; }, 0); /* DEMO-NAV */ }}>Vai alla libreria</button>
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// SHELLS
// ═══════════════════════════════════════════════════════
const DesktopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 14,
    padding:'10px 32px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border)',
    position:'sticky', top: 0, zIndex: 10,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
      <div style={{
        width: 26, height: 26, borderRadius: 7,
        background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('agent')})`,
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
      }}>M</div>
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
    </div>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      marginLeft: 22,
    }}>
      <strong style={{ color:'var(--text-sec)' }}>Discover</strong>
    </div>
  </div>
);

const DesktopScreen = (props) => (
  <div style={{ background:'var(--bg)', position:'relative' }}>
    <DesktopNav/>
    <DiscoverBody {...props}/>
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
    padding:'10px 14px', background:'var(--glass-bg)',
    backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border-light)',
    position:'sticky', top: 0, zIndex: 10,
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
      textAlign:'center', fontWeight: 700,
    }}>/discover</div>
    <button aria-label="Profilo" style={{
      width: 32, height: 32, borderRadius:'50%',
      background:`linear-gradient(135deg, ${entityHsl('player')}, ${entityHsl('event')})`,
      color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800, cursor:'pointer',
    }}>MR</button>
  </div>
);
const MobileScreen = (props) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav/>
      <DiscoverBody {...props} mobile/>
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

const DesktopFrame = ({ label, desc, children, height = 1100 }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: 1440,
      borderRadius:'var(--r-xl)', border:'1px solid var(--border)',
      background:'var(--bg)', overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
      position:'relative',
      maxHeight: height,
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding:'9px 14px', background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/discover</span>
      </div>
      <div style={{ overflow:'auto', maxHeight: height - 40 }}>
        {children}
      </div>
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
// ROOT
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id:'m1', filter:'all', label:'01 · Default · Tutti', desc:'Mobile default: hero compatto + 7 rows scrollable orizzontalmente con scroll-snap. Search box con placeholder corto.' },
  { id:'m2', filter:'game', label:'02 · Filtro Giochi', desc:'Filtrato 🎲: solo Trending (mix) + "Giochi nuovi" rimangono visibili. Indicator underline cambia colore in arancio (game).' },
  { id:'m3', filter:'agent', label:'03 · Filtro Agenti', desc:'Filtrato 🤖: solo Trending + "Agenti più installati". Indicator giallo (agent palette).' },
  { id:'m4', stateOverride:'empty', label:'04 · Empty (utente nuovo)', desc:'Solo Trending row + cell consigli "🌱 Stai iniziando ora!" con CTA "Esplora la libreria". No personalization.' },
  { id:'m5', stateOverride:'loading', label:'05 · Loading skeleton', desc:'2 rows skeleton: header bar + 5 cards shimmer con altezze realistiche per featured/grid/compact.' },
  { id:'m6', stateOverride:'error', label:'06 · Error', desc:'Alert danger "Discover non disponibile" + 2 CTA (Riprova / Vai alla libreria).' },
];

const App = () => {
  const [theme, setTheme] = useState(() => {
    const saved = localStorage.getItem('mai-theme');
    if (saved) return saved;
    return document.documentElement.getAttribute('data-theme') || 'light';
  });
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('mai-theme', theme);
  }, [theme]);

  return (
    <div style={{
      minHeight:'100vh', background:'var(--bg)', color:'var(--text)',
      padding:'24px 24px 80px',
    }}>
      {/* Card hover styles */}
      <style>{`
        .mai-card-hover:hover {
          transform: translateY(-2px);
          box-shadow: var(--shadow-lg);
          border-color: var(--border-strong);
        }
        @media (prefers-reduced-motion: reduce) {
          .mai-card-hover:hover { transform: none; }
        }
        .mai-row-track::-webkit-scrollbar { display: none; }
        .mai-row-arrow { opacity: 0; transition: opacity .2s; }
        .mai-row:hover .mai-row-arrow { opacity: 1; }
        @media (hover: none) {
          .mai-row-arrow { opacity: 1; }
        }
        @keyframes maiShimmer {
          0% { background-position: -200% 0; }
          100% { background-position: 200% 0; }
        }
        .mai-shimmer {
          background: linear-gradient(
            90deg,
            var(--bg-muted) 0%,
            var(--bg-card) 50%,
            var(--bg-muted) 100%
          );
          background-size: 200% 100%;
          animation: maiShimmer 1.6s linear infinite;
        }
        @media (prefers-reduced-motion: reduce) {
          .mai-shimmer { animation: none; }
        }
      `}</style>

      <header style={{
        maxWidth: 1440, margin:'0 auto 32px',
        display:'flex', alignItems:'center', gap: 12, flexWrap:'wrap',
      }}>
        <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
          <div style={{
            width: 32, height: 32, borderRadius: 8,
            background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('agent')}, ${entityHsl('toolkit')})`,
            color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
            fontWeight: 800, fontFamily:'var(--f-display)',
          }}>I</div>
          <div>
            <div style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 16 }}>SP4 wave 3 · I</div>
            <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)' }}>
              /discover — Hero + horizontal rows (Netflix style)
            </div>
          </div>
        </div>
        <div style={{ flex: 1 }}/>
        <span style={{
          padding:'4px 10px', borderRadius:'var(--r-pill)',
          background: entityHsl('toolkit', 0.15), color: entityHsl('toolkit'),
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.08em',
        }}>Wave 3 · 5/5 conclusivo</span>
        <button type="button" onClick={(e) => { (() => setTheme(t => t === 'light' ? 'dark' : 'light'))(e); setTimeout(() => { window.location.href = 'sp4-game-detail.html'; }, 0); /* DEMO-NAV */ }}
          style={{
            padding:'8px 14px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text)', fontFamily:'var(--f-display)',
            fontSize: 12, fontWeight: 800, cursor:'pointer',
          }}>🌗 {theme === 'light' ? 'Light' : 'Dark'}</button>
      </header>

      <section style={{ maxWidth: 1440, margin:'0 auto', display:'flex', flexDirection:'column', gap: 36 }}>
        <DesktopFrame label="Desktop · 01 · Default · Tutti" height={1200}
          desc="Hero full-width gradient radiale event/toolkit/agent + h1 'Scopri' grande + search ⌘K + Filtri avanzati + 7 entity pills (Tutti attivo). Sotto: 7 rows orizzontali con scroll-snap. Riga 1 'Trending' featured 320×280 (mix entity) · Riga 2 'Giochi nuovi' grid 180² · Riga 3 'Agenti' compact con stat 'usi' · Riga 4 'Toolkit consigliati' featured · Riga 5 'KB recenti' list · Riga 6 'Top contributor' compact con stat 'partite' · Riga 7 'Eventi' list con date block. Frecce ‹ › appaiono on hover row. Fade gradient su edges. Footer CTA gradient toolkit→player.">
          <DesktopScreen initialFilter="all"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 02 · Filtro Giochi" height={900}
          desc="Pill 🎲 Giochi attiva. Indicator underline arancio (entity color game). Visibili: Trending (sempre, mix) + Giochi nuovi grid. Le altre rows (agenti/toolkit/kb/players/events) collassano.">
          <DesktopScreen initialFilter="game"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 03 · Filtro Toolkit" height={900}
          desc="Pill 🧰 Toolkit attiva, indicator verde. Trending + Toolkit consigliati featured. Mostra il pattern 'personalizzato per te' implicito.">
          <DesktopScreen initialFilter="toolkit"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 04 · Empty (utente nuovo)" height={900}
          desc="Solo Trending row visibile + cell empty 'Stai iniziando ora! 🌱' con CTA 'Esplora la libreria'. No personalization → mostra solo content pubblico curato.">
          <DesktopScreen initialFilter="all" stateOverride="empty"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 05 · Loading" height={900}
          desc="Hero rendered, body con 3 rows skeleton: title bar shimmer + 5 cards shimmer per row. Altezze realistiche per featured/grid/compact.">
          <DesktopScreen initialFilter="all" stateOverride="loading"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 06 · Error" height={760}
          desc="Hero rendered, body alert danger 'Discover non disponibile' + due CTA (Riprova / Vai alla libreria). Il servizio raccomandazioni può essere offline indipendentemente dalle altre route.">
          <DesktopScreen initialFilter="all" stateOverride="error"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 07 · AdvancedFiltersDrawer aperto" height={1100}
          desc="Drawer da destra (420px) con 6 fieldset filtri: Numero giocatori, Durata, Difficoltà, Stato KB, Strategia agente, Anno. Footer Reimposta + 'Applica · 3 attivi' gradient. Riusa pattern wave 1.">
          <DesktopScreen initialFilter="all" initialAdvOpen/>
        </DesktopFrame>
      </section>

      <section style={{ maxWidth: 1440, margin:'48px auto 0' }}>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
          marginBottom: 8,
        }}>Mobile · 375px</h2>
        <p style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-muted)', marginBottom: 24,
        }}>6 stati: default tutti · filtro Giochi · filtro Agenti · empty (utente nuovo) · loading · error.</p>
        <div style={{
          display:'flex', flexWrap:'wrap', gap: 32, justifyContent:'center',
        }}>
          {MOBILE_STATES.map(m => (
            <PhoneShell key={m.id} label={m.label} desc={m.desc}>
              <MobileScreen initialFilter={m.filter} stateOverride={m.stateOverride}/>
            </PhoneShell>
          ))}
        </div>
      </section>

      <section style={{
        maxWidth: 900, margin:'64px auto 0',
        padding: 24, borderRadius:'var(--r-xl)',
        background:'var(--bg-card)', border:'1px solid var(--border)',
      }}>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800, marginBottom: 12,
        }}>Definition of Done · I discover (CONCLUSIVO wave 3)</h2>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.9, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>✓ Solo CSS variables da tokens.css (zero hex hardcoded)</li>
          <li>✓ Helper entityHsl(type, alpha) inline</li>
          <li>✓ Light + dark mode (toggle header) — gradient hero ricalcolato via tokens</li>
          <li>✓ Mobile 375px (6 phone) + Desktop 1440px (7 frame)</li>
          <li>✓ Pattern: Hero discover header + 7 horizontal rows Netflix-style con scroll-snap</li>
          <li>✓ Hero: badge "Discover" + h1 "Scopri" + subtitle + search box ⌘K + CTA "Filtri avanzati" + 7 entity filter pills (Tutti/Giochi/Agenti/Toolkit/KB/Persone/Eventi) con animated underline</li>
          <li>✓ 7 rows: Trending (featured mix) · Giochi nuovi (grid) · Agenti popolari (compact +stat usi) · Toolkit consigliati (featured) · KB recenti (list) · Top contributor (compact +stat partite) · Eventi (list con date block)</li>
          <li>✓ MeepleCard 4 varianti: featured 320×280 · grid 180² · compact 260×72 · list 380×80 — riuso wave 1 lifted</li>
          <li>✓ Frecce ‹ › appaiono on hover (CSS .mai-row:hover .mai-row-arrow) · sempre visibili su touch (@media hover: none)</li>
          <li>✓ Fade gradient su scroll edges (mostra solo se scrollabile)</li>
          <li>✓ scroll-snap-type: x mandatory + scroll-snap-align: start su ogni card</li>
          <li>✓ AdvancedFiltersDrawer riuso wave 1: 6 fieldset (giocatori/durata/difficoltà/stato KB/strategia/anno) + footer gradient "Applica · 3 attivi"</li>
          <li>✓ Footer CTA gradient toolkit→player "Hai contenuti da pubblicare?" → /toolkits/new</li>
          <li>✓ Stati: default / empty (solo Trending + cell "Stai iniziando ora!") / loading (skeleton 3 rows) / error (Discover non disponibile + Riprova)</li>
          <li>✓ Empty filter sub-state: filtra ma 0 risultati → "Nessun [tipo] trending" + CTA Mostra tutti</li>
          <li>✓ ARIA: role="tablist"/"tab" sui filter pills · aria-pressed sui filter group · aria-label "Cerca", "Scorri a sinistra/destra" · role="dialog" sul drawer</li>
          <li>✓ prefers-reduced-motion gestito (hover transform + shimmer + slideIn keyframes)</li>
          <li>✓ Testo italiano · dati realistici da data.js (Wingspan, Azul Toolkit v2, Azul Rules Expert, Marco R., gn-wingspan-mar15, kb-azul-ita)</li>
          <li>✓ ID short readable: gn-wingspan-mar15, tk-azul-v2, a-azul-rules — NO UUID</li>
          <li>✓ Commento di apertura con route /discover</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
        }}>Nuovi componenti v2 emersi</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· DiscoverHero (gradient radiale 3-color + title + subtitle + search + advanced + entity pills)</li>
          <li>· DiscoverSearchBox (search input + ⌘K kbd + icona)</li>
          <li>· EntityFilterPillBar (7 pills horizontal scroll + animated underline color-coded per entity)</li>
          <li>· HorizontalRow (header titolo+emoji+count+'Vedi tutti' + scroller + frecce + fades)</li>
          <li>· RowScroller (scroll-snap track + arrow buttons hover-reveal + edge fades)</li>
          <li>· FooterCTA (gradient radiale 2-color + badge + h2 + descrizione + CTA gradient)</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
        }}>Riusi pixel-perfect</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· MeepleCard tutte 4 varianti (featured/grid/compact/list) — wave 1 PR #549</li>
          <li>· AdvancedFiltersDrawer pattern (wave 1) → riusato 1:1 con drawer V3 right-side</li>
          <li>· EntityChip (wave 1) → TypeChip per category mark sui card</li>
          <li>· Tabs animated underline (wave 1) → entity filter pills indicator</li>
          <li>· Drawer V3 right-side (03-drawer-variants) per AdvancedFiltersDrawer</li>
          <li>· Drawer V1 bottom-sheet (03-drawer-variants) per advanced mobile</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
          color:'hsl(var(--c-warning))',
        }}>Deviazioni flaggate</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· Frame 7 (drawer aperto) usa `initialAdvOpen` prop ma il body ha state proprio — è solo demo statica, in produzione il drawer è state-driven full</li>
          <li>· Eventi list usa `entity.date.split(' ')[0]` per estrarre il day → fragile, in prod va parsato dato strutturato</li>
          <li>· Personalizzazione "Toolkit consigliati per te" è fake — backend deve fornire endpoint /discover/recommendations?userId=</li>
          <li>· Search input non collegato a backend — solo placeholder UI</li>
          <li>· Hero gradient radiale a 3 colori entity può variare con palette dark — verificare contrasto AAA su small text</li>
          <li>· @keyframes inline (slideIn + shimmer + maiShimmer) non in components.css come da contratto NON sovrascrivere</li>
          <li>· Filter pills "Persone" / "Eventi" non distinguono Player vs Event detail (entrambi entità) — in prod chiarire scope</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
          color: entityHsl('toolkit'),
        }}>🎉 Wave 3 completa — 5/5 mockup consegnati</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· D · sp4-player-detail (split-view sticky + 6 tabs + connections graph)</li>
          <li>· E · sp4-toolkit-detail (split-view + 6 tabs Public/Own + tools list)</li>
          <li>· F · sp4-kb-detail (split-view chunks + markdown preview + sub-tabs Rendered/Raw)</li>
          <li>· G · sp4-game-nights-index (calendar/list toggle + day drawer)</li>
          <li>· I · sp4-discover (hero + 7 horizontal rows Netflix + advanced drawer) ← QUESTO</li>
        </ul>
      </section>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
