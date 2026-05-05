/* MeepleAI SP4 wave 1 — Schermata #5 / 5 (FINALE)
   Route: /library
   Pattern: Hub power-user multi-entity desktop

   v2 nuovi (FINALE):
   - LibraryHero            → apps/web/src/components/ui/v2/library-hero/
   - EntityTabBar           → riusato da AgentTabs (entity-aware variant)
   - CrossEntityFilters     → apps/web/src/components/ui/v2/cross-entity-filters/
   - AdvancedFiltersDrawer  → apps/web/src/components/ui/v2/advanced-filters-drawer/  ⭐ standalone reusable
   - LibraryGrid            → apps/web/src/components/ui/v2/library-grid/ (3 view modes)
   - BulkSelectionBar       → apps/web/src/components/ui/v2/bulk-selection-bar/
   - RecentActivityRail     → apps/web/src/components/ui/v2/recent-activity-rail/

   Riusi prod: MeepleCard.grid/list (PR #523), ConnectionChipStrip footer (PR #549/#552), EntityChip.

   Nota: AdvancedFiltersDrawer è progettato come componente STANDALONE reusable.
   Riceve: open, onClose, sections[], activeFilters, onApply, entityScope.
   Riusabile in: sp4-games-index, sp4-agents-index, sp4-library-desktop, future surface.
*/

const { useState, useEffect, useRef, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.game;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ═══════════════════════════════════════════════════════
// ─── LIBRARY HERO (compact 140) ───────────────────────
// /* v2: LibraryHero */
// ═══════════════════════════════════════════════════════
const LibraryHero = ({ stats, compact }) => (
  <div style={{
    padding: compact ? '16px 16px' : '24px 32px',
    background:`linear-gradient(135deg, ${entityHsl('game', 0.10)} 0%, ${entityHsl('agent', 0.06)} 50%, ${entityHsl('kb', 0.08)} 100%)`,
    borderBottom:'1px solid var(--border-light)',
    position:'relative', overflow:'hidden',
  }}>
    <div aria-hidden="true" style={{
      position:'absolute', top:-40, right:-40,
      width: 240, height: 240, borderRadius:'50%',
      background:`radial-gradient(circle, ${entityHsl('game', 0.1)} 0%, transparent 70%)`,
      pointerEvents:'none',
    }}/>
    <div style={{
      position:'relative', zIndex: 1,
      display:'flex', alignItems: compact ? 'flex-start' : 'flex-end',
      flexDirection: compact ? 'column' : 'row',
      gap: compact ? 14 : 24,
      flexWrap:'wrap',
    }}>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display:'inline-flex', alignItems:'center', gap: 5,
          padding:'2px 8px', borderRadius:'var(--r-pill)',
          background:'var(--bg-card)', border:'1px solid var(--border)',
          fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
          fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em',
          marginBottom: 8,
        }}>
          <span aria-hidden="true">📚</span>Library · power-user view
        </div>
        <h1 style={{
          fontFamily:'var(--f-display)', fontWeight: 800,
          fontSize: compact ? 26 : 38,
          letterSpacing:'-.02em', lineHeight: 1.05,
          margin:'0 0 4px', color:'var(--text)',
        }}>La tua libreria</h1>
        <p style={{
          fontFamily:'var(--f-body)', fontSize: compact ? 13 : 14.5,
          color:'var(--text-sec)', margin: 0, fontWeight: 500,
        }}>Tutti i tuoi giochi, agenti e documenti in un posto.</p>
      </div>

      {/* Stats inline */}
      <div style={{
        display:'flex', gap: compact ? 8 : 14, flexWrap:'wrap',
      }}>
        {stats.map(s => (
          <div key={s.label} style={{
            display:'flex', alignItems:'center', gap: 6,
            padding:'7px 11px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)',
            border:`1px solid ${entityHsl(s.entity, 0.2)}`,
          }}>
            <span aria-hidden="true" style={{ fontSize: 14 }}>{DS.EC[s.entity].em}</span>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 14, fontWeight: 800,
              color: entityHsl(s.entity), fontVariantNumeric:'tabular-nums',
            }}>{s.count}</span>
            <span style={{
              fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
              color:'var(--text-sec)',
            }}>{s.label}</span>
          </div>
        ))}
      </div>

      {/* Action bar */}
      {!compact && (
        <div style={{ display:'flex', gap: 8, flexShrink: 0, width:'100%', justifyContent:'flex-end' }}>
          <button type="button" style={{
            padding:'9px 16px', borderRadius:'var(--r-md)',
            background: entityHsl('game'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 6,
            boxShadow:`0 4px 14px ${entityHsl('game', 0.4)}`,
          }}><span aria-hidden="true">+</span>Aggiungi gioco</button>
          <button type="button" style={{
            padding:'9px 14px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', color:'var(--text)',
            border:'1px solid var(--border-strong)',
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
            cursor:'pointer',
          }}>↓ Importa BGG</button>
          <button type="button" aria-label="Esporta" style={{
            padding:'9px 12px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', color:'var(--text)',
            border:'1px solid var(--border-strong)',
            fontSize: 14, cursor:'pointer', minWidth: 38,
          }}>↗</button>
        </div>
      )}
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ENTITY TAB BAR ───────────────────────────────────
// ═══════════════════════════════════════════════════════
const EntityTabBar = ({ tabs, active, onChange, compact }) => {
  const refs = useRef({});
  const [ind, setInd] = useState({ left: 0, width: 0 });
  useEffect(() => {
    const el = refs.current[active];
    if (el) setInd({ left: el.offsetLeft, width: el.offsetWidth });
  }, [active, tabs.length, compact]);

  const accent = active === 'all' ? 'game' : active;

  return (
    <div className="mai-cb-scroll" style={{
      position:'relative',
      display:'flex', alignItems:'center', gap: 2,
      borderBottom:'1px solid var(--border)',
      overflowX:'auto', scrollbarWidth:'none',
      padding: compact ? '0 16px' : '0 32px',
      background:'var(--bg)',
    }} role="tablist">
      {tabs.map(t => {
        const isActive = t.id === active;
        const ent = t.id === 'all' ? 'game' : t.id;
        return (
          <button key={t.id} type="button"
            ref={el => { refs.current[t.id] = el; }}
            onClick={() => onChange(t.id)}
            role="tab" aria-selected={isActive}
            style={{
              display:'inline-flex', alignItems:'center', gap: 7,
              padding:'12px 14px',
              background: isActive ? entityHsl(ent, 0.08) : 'transparent',
              border:'none',
              color: isActive ? entityHsl(ent) : 'var(--text-sec)',
              fontFamily:'var(--f-display)', fontSize: 13,
              fontWeight: isActive ? 800 : 700,
              cursor:'pointer', whiteSpace:'nowrap', flexShrink: 0,
              borderRadius:'var(--r-sm) var(--r-sm) 0 0',
            }}>
            <span aria-hidden="true">{t.icon}</span>{t.label}
            <span style={{
              padding:'1px 7px', borderRadius:'var(--r-pill)',
              background: isActive ? entityHsl(ent) : 'var(--bg-muted)',
              color: isActive ? '#fff' : 'var(--text-muted)',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              fontVariantNumeric:'tabular-nums',
            }}>{t.count}</span>
          </button>
        );
      })}
      <span aria-hidden="true" style={{
        position:'absolute', bottom: -1, left: ind.left, width: ind.width,
        height: 2, background: entityHsl(accent),
        transition:'left .3s cubic-bezier(.4,0,.2,1), width .3s cubic-bezier(.4,0,.2,1), background .3s',
        borderRadius:'2px 2px 0 0',
      }}/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── CROSS-ENTITY FILTERS ROW ────────────────────────
// /* v2: CrossEntityFilters */
// ═══════════════════════════════════════════════════════
const FilterChip = ({ label, value, onClick, hasValue }) => (
  <button type="button" onClick={onClick} style={{
    display:'inline-flex', alignItems:'center', gap: 5,
    padding:'7px 11px', borderRadius:'var(--r-md)',
    background: hasValue ? entityHsl('game', 0.1) : 'var(--bg-card)',
    border: hasValue ? `1px solid ${entityHsl('game', 0.3)}` : '1px solid var(--border)',
    color: hasValue ? entityHsl('game') : 'var(--text-sec)',
    fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
    cursor:'pointer', whiteSpace:'nowrap', flexShrink: 0,
  }}>
    <span style={{
      fontFamily:'var(--f-mono)', fontSize: 9,
      textTransform:'uppercase', letterSpacing:'.06em',
      color: hasValue ? entityHsl('game') : 'var(--text-muted)',
      fontWeight: 800,
    }}>{label}</span>
    <span>{value}</span>
    <span aria-hidden="true" style={{ fontSize: 10, opacity: .6 }}>▾</span>
  </button>
);

const CrossEntityFilters = ({ activeFilterCount, onOpenDrawer, view, onViewChange, compact, search, onSearchChange }) => (
  <div style={{
    padding: compact ? '10px 16px' : '12px 32px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border-light)',
    display:'flex', flexDirection:'column', gap: 8,
  }}>
    {/* Search */}
    <div style={{ position:'relative' }}>
      <span aria-hidden="true" style={{
        position:'absolute', left: 12, top:'50%', transform:'translateY(-50%)',
        color:'var(--text-muted)', fontSize: 14, pointerEvents:'none',
      }}>⌕</span>
      <input
        type="search" placeholder="Cerca in libreria... (premi /)"
        value={search} onChange={e => onSearchChange(e.target.value)}
        style={{
          width:'100%', padding:'9px 12px 9px 34px',
          borderRadius:'var(--r-md)', border:'1px solid var(--border)',
          background:'var(--bg-card)', color:'var(--text)',
          fontFamily:'var(--f-body)', fontSize: 13,
        }}/>
      <kbd aria-hidden="true" style={{
        position:'absolute', right: 12, top:'50%', transform:'translateY(-50%)',
        padding:'1px 6px', borderRadius: 4,
        background:'var(--bg-muted)', border:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 700,
      }}>/</kbd>
    </div>

    {/* Filters row */}
    <div className="mai-cb-scroll" style={{
      display:'flex', alignItems:'center', gap: 6,
      overflowX:'auto', scrollbarWidth:'none',
    }}>
      <FilterChip label="STATO" value="Tutti"/>
      <FilterChip label="GIOCO" value="Tutti"/>
      <FilterChip label="DATA" value="Sempre"/>
      <FilterChip label="SORT" value="Recenti" hasValue/>
      <span style={{
        width: 1, height: 22, background:'var(--border)', flexShrink: 0, margin:'0 4px',
      }}/>
      <button type="button" onClick={onOpenDrawer} style={{
        display:'inline-flex', alignItems:'center', gap: 6,
        padding:'7px 11px', borderRadius:'var(--r-md)',
        background: activeFilterCount > 0 ? entityHsl('agent', 0.1) : 'var(--bg-card)',
        border: activeFilterCount > 0 ? `1px solid ${entityHsl('agent', 0.4)}` : '1px solid var(--border-strong)',
        color: activeFilterCount > 0 ? entityHsl('agent') : 'var(--text)',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
        cursor:'pointer', whiteSpace:'nowrap', flexShrink: 0,
      }}>
        <span aria-hidden="true">⚙</span>Filtri avanzati
        {activeFilterCount > 0 && (
          <span style={{
            padding:'1px 6px', borderRadius:'var(--r-pill)',
            background: entityHsl('agent'), color:'#fff',
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          }}>{activeFilterCount}</span>
        )}
      </button>

      <div style={{ flex: 1 }}/>

      {/* View toggle */}
      <div role="radiogroup" aria-label="Vista" style={{
        display:'inline-flex', borderRadius:'var(--r-md)',
        border:'1px solid var(--border)', background:'var(--bg-card)',
        overflow:'hidden', flexShrink: 0,
      }}>
        {[
          { id:'grid', icon:'▦', label:'Grid' },
          { id:'list', icon:'☰', label:'List' },
          { id:'compact', icon:'≡', label:'Compact' },
        ].map(v => {
          const active = view === v.id;
          return (
            <button key={v.id} type="button" role="radio" aria-checked={active}
              onClick={() => onViewChange(v.id)}
              aria-label={v.label}
              style={{
                padding:'7px 10px',
                background: active ? entityHsl('game', 0.12) : 'transparent',
                color: active ? entityHsl('game') : 'var(--text-muted)',
                border:'none', cursor:'pointer',
                fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              }}>{v.icon}</button>
          );
        })}
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ⭐ ADVANCED FILTERS DRAWER (standalone v2) ───────
// /* v2: AdvancedFiltersDrawer
//    apps/web/src/components/ui/v2/advanced-filters-drawer/
//    Reusable: receives { open, onClose, sections, activeFilters, onApply, entityScope }
//    Same component used in games-index, agents-index, library-desktop. */
// ═══════════════════════════════════════════════════════
const DRAWER_SECTIONS = [
  {
    id:'status', title:'Stato', icon:'●',
    kind:'chips-multi',
    options:[
      { id:'owned', label:'Posseduto', icon:'✓', color:'game' },
      { id:'wishlist', label:'Wishlist', icon:'★', color:'event' },
      { id:'setup', label:'In setup', icon:'⚙', color:'agent' },
      { id:'archived', label:'Archiviato', icon:'⊘', color:'kb' },
    ],
  },
  {
    id:'entity', title:'Tipo entità', icon:'⌗',
    kind:'chips-multi',
    options:[
      { id:'game', label:'Giochi', icon:'🎲', color:'game' },
      { id:'agent', label:'Agenti', icon:'🤖', color:'agent' },
      { id:'kb', label:'Documenti KB', icon:'📚', color:'kb' },
      { id:'session', label:'Sessioni', icon:'🎯', color:'session' },
      { id:'chat', label:'Chat', icon:'💬', color:'chat' },
    ],
  },
  {
    id:'game', title:'Gioco', icon:'🎲',
    kind:'select-multi',
    placeholder:'Filtra per gioco specifico...',
    options: DS.games.map(g => ({ id: g.id, label: g.title })),
  },
  {
    id:'period', title:'Periodo', icon:'📅',
    kind:'period-quick',
    options:[
      { id:'7d', label:'Ultimi 7 giorni' },
      { id:'30d', label:'Ultimi 30 giorni' },
      { id:'1y', label:'Ultimo anno' },
      { id:'all', label:'Sempre' },
      { id:'range', label:'Range personalizzato...' },
    ],
  },
  {
    id:'tags', title:'Tag', icon:'🏷',
    kind:'chips-multi',
    options:[
      { id:'family', label:'Family', color:'event' },
      { id:'strategy', label:'Strategy', color:'session' },
      { id:'coop', label:'Coop', color:'kb' },
      { id:'engine', label:'Engine builder', color:'agent' },
      { id:'auction', label:'Auction', color:'toolkit' },
      { id:'roll-and-write', label:'Roll & Write', color:'player' },
      { id:'card-driven', label:'Card driven', color:'chat' },
      { id:'tableau', label:'Tableau', color:'game' },
    ],
  },
  {
    id:'rating', title:'Rating', icon:'★',
    kind:'range',
    min: 1, max: 10, step: 0.5,
    valueDefault: [6, 10],
  },
  {
    id:'weight', title:'Complessità', icon:'⚖',
    kind:'chips-multi',
    options:[
      { id:'light', label:'Light · 1.0–2.0', color:'kb' },
      { id:'medium', label:'Medium · 2.0–3.0', color:'agent' },
      { id:'heavy', label:'Heavy · 3.0–4.0', color:'game' },
      { id:'extra', label:'Extra heavy · 4.0+', color:'event' },
    ],
  },
];

const DrawerSection = ({ section, value, onChange, defaultOpen }) => {
  const [open, setOpen] = useState(defaultOpen ?? true);
  return (
    <div style={{
      borderBottom:'1px solid var(--border-light)',
    }}>
      <button type="button" onClick={() => setOpen(o => !o)}
        style={{
          width:'100%', padding:'14px 18px',
          background:'transparent', border:'none', cursor:'pointer',
          display:'flex', alignItems:'center', gap: 10,
          color:'var(--text)',
        }}>
        <span aria-hidden="true" style={{
          fontSize: 13, color:'var(--text-muted)',
        }}>{section.icon}</span>
        <span style={{
          flex: 1, textAlign:'left',
          fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800,
        }}>{section.title}</span>
        {Array.isArray(value) && value.length > 0 && (
          <span style={{
            padding:'1px 7px', borderRadius:'var(--r-pill)',
            background: entityHsl('agent'), color:'#fff',
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          }}>{value.length}</span>
        )}
        <span aria-hidden="true" style={{
          transform: open ? 'rotate(90deg)' : 'rotate(0)',
          transition:'transform var(--dur-sm)',
          color:'var(--text-muted)', fontSize: 14,
        }}>›</span>
      </button>
      {open && (
        <div style={{ padding:'0 18px 16px' }}>
          {section.kind === 'chips-multi' && (
            <div style={{ display:'flex', flexWrap:'wrap', gap: 6 }}>
              {section.options.map(o => {
                const active = (value || []).includes(o.id);
                const c = o.color || 'game';
                return (
                  <button key={o.id} type="button"
                    onClick={() => onChange(active
                      ? (value || []).filter(v => v !== o.id)
                      : [...(value || []), o.id])}
                    aria-pressed={active}
                    style={{
                      display:'inline-flex', alignItems:'center', gap: 5,
                      padding:'6px 10px', borderRadius:'var(--r-pill)',
                      background: active ? entityHsl(c, 0.14) : 'var(--bg-card)',
                      border: active ? `1px solid ${entityHsl(c, 0.4)}` : '1px solid var(--border)',
                      color: active ? entityHsl(c) : 'var(--text-sec)',
                      fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
                      cursor:'pointer',
                    }}>
                    {o.icon && <span aria-hidden="true">{o.icon}</span>}
                    {o.label}
                  </button>
                );
              })}
            </div>
          )}
          {section.kind === 'select-multi' && (
            <div>
              <div style={{
                padding:'8px 10px', borderRadius:'var(--r-md)',
                border:'1px solid var(--border)', background:'var(--bg-card)',
                fontSize: 12, color:'var(--text-muted)', marginBottom: 8,
                cursor:'pointer',
              }}>{section.placeholder}</div>
              <div style={{ display:'flex', flexWrap:'wrap', gap: 5 }}>
                {section.options.slice(0, 5).map(o => {
                  const active = (value || []).includes(o.id);
                  return (
                    <button key={o.id} type="button"
                      onClick={() => onChange(active
                        ? (value || []).filter(v => v !== o.id)
                        : [...(value || []), o.id])}
                      style={{
                        padding:'4px 9px', borderRadius:'var(--r-pill)',
                        background: active ? entityHsl('game', 0.12) : 'var(--bg-muted)',
                        border: active ? `1px solid ${entityHsl('game', 0.4)}` : '1px solid transparent',
                        color: active ? entityHsl('game') : 'var(--text-sec)',
                        fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 700,
                        cursor:'pointer',
                      }}>{active && '✓ '}{o.label}</button>
                  );
                })}
              </div>
            </div>
          )}
          {section.kind === 'period-quick' && (
            <div style={{ display:'flex', flexDirection:'column', gap: 4 }}>
              {section.options.map(o => {
                const active = value === o.id;
                return (
                  <button key={o.id} type="button"
                    onClick={() => onChange(o.id)}
                    aria-pressed={active}
                    style={{
                      display:'flex', alignItems:'center', gap: 8,
                      padding:'7px 10px', borderRadius:'var(--r-md)',
                      background: active ? entityHsl('event', 0.12) : 'transparent',
                      border:'none',
                      color: active ? entityHsl('event') : 'var(--text-sec)',
                      fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: active ? 800 : 600,
                      cursor:'pointer', textAlign:'left',
                    }}>
                    <span aria-hidden="true" style={{
                      width: 10, height: 10, borderRadius:'50%',
                      border: active ? `3px solid ${entityHsl('event')}` : '1.5px solid var(--border-strong)',
                      flexShrink: 0,
                    }}/>
                    {o.label}
                  </button>
                );
              })}
            </div>
          )}
          {section.kind === 'range' && (() => {
            const [lo, hi] = value || section.valueDefault;
            return (
              <div>
                <div style={{
                  display:'flex', justifyContent:'space-between', marginBottom: 8,
                  fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700,
                }}>
                  <span style={{ color: entityHsl('event') }}>{lo}</span>
                  <span style={{ color:'var(--text-muted)' }}>—</span>
                  <span style={{ color: entityHsl('event') }}>{hi}</span>
                </div>
                <div style={{ position:'relative', height: 24 }}>
                  <div style={{
                    position:'absolute', top: 11, left: 0, right: 0, height: 4,
                    borderRadius: 2, background:'var(--bg-muted)',
                  }}/>
                  <div style={{
                    position:'absolute', top: 11,
                    left: `${((lo - section.min) / (section.max - section.min)) * 100}%`,
                    right: `${100 - ((hi - section.min) / (section.max - section.min)) * 100}%`,
                    height: 4, borderRadius: 2,
                    background: entityHsl('event'),
                  }}/>
                  {[lo, hi].map((v, i) => (
                    <div key={i} aria-hidden="true" style={{
                      position:'absolute', top: 6,
                      left: `calc(${((v - section.min) / (section.max - section.min)) * 100}% - 7px)`,
                      width: 14, height: 14, borderRadius:'50%',
                      background: entityHsl('event'), border:'2px solid var(--bg)',
                      boxShadow:'0 1px 4px rgba(0,0,0,.2)',
                    }}/>
                  ))}
                </div>
              </div>
            );
          })()}
        </div>
      )}
    </div>
  );
};

const AdvancedFiltersDrawer = ({ open, onClose, activeFilters, onApply, compact }) => {
  const [draft, setDraft] = useState(activeFilters || {});
  useEffect(() => { setDraft(activeFilters || {}); }, [activeFilters, open]);
  const count = Object.values(draft).reduce((acc, v) => {
    if (Array.isArray(v)) return acc + v.length;
    if (v) return acc + 1;
    return acc;
  }, 0);

  if (!open) return null;
  return (
    <>
      <div onClick={onClose} aria-hidden="true" style={{
        position:'absolute', inset: 0, background:'rgba(0,0,0,.4)',
        zIndex: 50, animation:'mai-fade-in .2s var(--ease-out)',
      }}/>
      <aside role="dialog" aria-label="Filtri avanzati" style={{
        position:'absolute', top: 0, right: 0, bottom: 0,
        width: compact ? '100%' : 400,
        background:'var(--bg)', borderLeft:'1px solid var(--border)',
        boxShadow:'-12px 0 40px rgba(0,0,0,.18)',
        zIndex: 51, display:'flex', flexDirection:'column',
        animation: compact ? 'mai-slide-up .25s var(--ease-out)' : 'mai-slide-left .25s var(--ease-out)',
      }}>
        {/* Header */}
        <div style={{
          padding:'14px 18px', borderBottom:'1px solid var(--border)',
          display:'flex', alignItems:'center', gap: 10,
        }}>
          <span aria-hidden="true" style={{
            width: 32, height: 32, borderRadius:'var(--r-md)',
            background: entityHsl('agent', 0.12), color: entityHsl('agent'),
            display:'flex', alignItems:'center', justifyContent:'center',
            fontSize: 14,
          }}>⚙</span>
          <div style={{ flex: 1 }}>
            <div style={{ fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800 }}>Filtri avanzati</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
              fontWeight: 700, textTransform:'uppercase', letterSpacing:'.06em',
            }}>{count} attivi · scope: library</div>
          </div>
          <button onClick={onClose} aria-label="Chiudi" style={{
            width: 30, height: 30, borderRadius:'var(--r-md)',
            background:'var(--bg-muted)', border:'none', color:'var(--text)',
            fontSize: 14, cursor:'pointer',
          }}>✕</button>
        </div>

        {/* Body */}
        <div style={{ flex: 1, overflowY:'auto' }}>
          {DRAWER_SECTIONS.map((s, i) => (
            <DrawerSection key={s.id} section={s}
              value={draft[s.id]}
              onChange={v => setDraft(d => ({ ...d, [s.id]: v }))}
              defaultOpen={i < 3}/>
          ))}
        </div>

        {/* Footer */}
        <div style={{
          padding:'12px 18px', borderTop:'1px solid var(--border)',
          background:'var(--bg-card)',
          display:'flex', gap: 8,
        }}>
          <button type="button" onClick={() => setDraft({})} style={{
            padding:'9px 14px', borderRadius:'var(--r-md)',
            background:'transparent', color:'var(--text-sec)',
            border:'1px solid var(--border-strong)',
            fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700,
            cursor:'pointer',
          }}>Reset</button>
          <button type="button"
            onClick={() => { onApply(draft); onClose(); }}
            style={{
              flex: 1, padding:'9px 14px', borderRadius:'var(--r-md)',
              background: entityHsl('agent'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
              cursor:'pointer',
              boxShadow: `0 3px 10px ${entityHsl('agent', 0.35)}`,
            }}>Applica{count > 0 && ` (${count})`}</button>
        </div>
      </aside>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── MeepleCard variants ─────────────────────────────
// ═══════════════════════════════════════════════════════
const StatusDot = ({ status }) => {
  const map = {
    owned: 'hsl(140, 50%, 45%)', active: 'hsl(140, 50%, 45%)', indexed:'hsl(140, 50%, 45%)',
    wishlist: 'hsl(38, 90%, 55%)', setup: 'hsl(38, 90%, 55%)', processing:'hsl(38, 90%, 55%)',
    archived:'var(--text-muted)', failed:'hsl(var(--c-danger))',
    inprogress: entityHsl('session'), paused:'var(--text-muted)', completed:'hsl(140, 50%, 45%)',
    idle:'var(--text-muted)',
  };
  return <span aria-hidden="true" style={{
    width: 6, height: 6, borderRadius:'50%',
    background: map[status] || 'var(--text-muted)',
    flexShrink: 0,
  }}/>;
};

const MeepleCardGrid = ({ entity, selectable, selected, onSelect }) => {
  const ent = entity.type;
  return (
    <article tabIndex={0} className="mai-card-grid" style={{
      position:'relative',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', overflow:'hidden',
      display:'flex', flexDirection:'column',
      cursor:'pointer',
      borderColor: selected ? entityHsl(ent, 0.5) : 'var(--border)',
      boxShadow: selected ? `0 0 0 2px ${entityHsl(ent, 0.3)}` : 'none',
    }}>
      {/* Top accent bar */}
      <div aria-hidden="true" style={{
        position:'absolute', top: 0, left: 0, right: 0, height: 3,
        background: entityHsl(ent),
        zIndex: 2,
      }}/>
      {/* Cover */}
      <div style={{
        height: 100, background: entity.cover, position:'relative',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 38,
      }} aria-hidden="true">
        <span style={{ filter:'drop-shadow(0 2px 6px rgba(0,0,0,.3))' }}>{entity.coverEmoji || DS.EC[ent].em}</span>
        {/* Entity badge */}
        <div style={{
          position:'absolute', top: 8, left: 8,
          display:'inline-flex', alignItems:'center', gap: 4,
          padding:'2px 7px', borderRadius:'var(--r-pill)',
          background:'rgba(255,255,255,.85)', backdropFilter:'blur(6px)',
          fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800,
          color: entityHsl(ent),
          textTransform:'uppercase', letterSpacing:'.06em',
        }}>
          <span aria-hidden="true">{DS.EC[ent].em}</span>{DS.EC[ent].lb}
        </div>
        {/* Selection checkbox */}
        {selectable && (
          <button type="button" onClick={(e) => { e.stopPropagation(); onSelect(); }}
            aria-label={selected ? 'Deseleziona' : 'Seleziona'}
            style={{
              position:'absolute', top: 8, right: 8,
              width: 24, height: 24, borderRadius:'var(--r-sm)',
              background: selected ? entityHsl(ent) : 'rgba(255,255,255,.85)',
              border: selected ? 'none' : '1.5px solid rgba(255,255,255,.95)',
              backdropFilter:'blur(6px)',
              color: selected ? '#fff' : entityHsl(ent),
              cursor:'pointer', fontSize: 13, fontWeight: 800,
              display:'flex', alignItems:'center', justifyContent:'center',
            }}>{selected ? '✓' : ''}</button>
        )}
        {/* 3-dot menu (hover) */}
        {!selectable && (
          <button type="button" onClick={(e) => e.stopPropagation()}
            aria-label="Azioni" className="mai-card-menu"
            style={{
              position:'absolute', top: 8, right: 8,
              width: 24, height: 24, borderRadius:'var(--r-sm)',
              background:'rgba(255,255,255,.85)', backdropFilter:'blur(6px)',
              border:'none', color:'var(--text)',
              cursor:'pointer', fontSize: 14, fontWeight: 800,
              display:'flex', alignItems:'center', justifyContent:'center',
            }}>⋯</button>
        )}
      </div>
      {/* Body */}
      <div style={{ padding: 12, display:'flex', flexDirection:'column', gap: 4, flex: 1 }}>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800,
          color:'var(--text)', margin: 0, lineHeight: 1.2,
          display:'-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient:'vertical', overflow:'hidden',
        }}>{entity.title}</h3>
        <div style={{
          fontSize: 11, color:'var(--text-muted)', margin:0,
          display:'-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient:'vertical', overflow:'hidden',
          lineHeight: 1.4,
        }}>{entity.subtitle || `${entity.publisher || ''}${entity.year ? ' · ' + entity.year : ''}`}</div>
        <div style={{ flex: 1 }}/>
        <div style={{
          display:'flex', alignItems:'center', gap: 5,
          padding:'5px 0 0', borderTop:'1px solid var(--border-light)', marginTop: 4,
        }}>
          <StatusDot status={entity.status}/>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)',
            textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700,
          }}>{entity.badge || entity.status}</span>
        </div>
      </div>
    </article>
  );
};

const MeepleCardList = ({ entity, selectable, selected, onSelect }) => {
  const ent = entity.type;
  return (
    <article tabIndex={0} className="mai-card-list" style={{
      display:'flex', alignItems:'center', gap: 12, padding: 10,
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderColor: selected ? entityHsl(ent, 0.5) : 'var(--border)',
      borderRadius:'var(--r-lg)', cursor:'pointer',
      borderLeft: `3px solid ${entityHsl(ent)}`,
    }}>
      {selectable && (
        <button type="button" onClick={(e) => { e.stopPropagation(); onSelect(); }}
          aria-label={selected ? 'Deseleziona' : 'Seleziona'}
          style={{
            width: 22, height: 22, borderRadius:'var(--r-sm)',
            background: selected ? entityHsl(ent) : 'transparent',
            border: selected ? 'none' : `1.5px solid var(--border-strong)`,
            color: selected ? '#fff' : 'transparent',
            cursor:'pointer', fontSize: 12, fontWeight: 800,
            display:'flex', alignItems:'center', justifyContent:'center',
            flexShrink: 0,
          }}>{selected ? '✓' : ''}</button>
      )}
      <div style={{
        width: 42, height: 42, borderRadius:'var(--r-md)',
        background: entity.cover,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 20, flexShrink: 0,
      }} aria-hidden="true">{entity.coverEmoji || DS.EC[ent].em}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display:'flex', alignItems:'center', gap: 6, marginBottom: 2, flexWrap:'wrap',
        }}>
          <span style={{
            display:'inline-flex', alignItems:'center', gap: 3,
            padding:'1px 6px', borderRadius:'var(--r-pill)',
            background: entityHsl(ent, 0.12), color: entityHsl(ent),
            fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.06em',
          }}>{DS.EC[ent].em}{DS.EC[ent].lb}</span>
          <h3 style={{
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            color:'var(--text)', margin: 0,
            whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
            maxWidth: 280,
          }}>{entity.title}</h3>
        </div>
        <div style={{
          fontSize: 11, color:'var(--text-muted)', fontWeight: 500,
          whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
        }}>{entity.subtitle || `${entity.publisher || ''}${entity.year ? ' · ' + entity.year : ''}`}</div>
      </div>
      <div style={{
        display:'flex', alignItems:'center', gap: 5, flexShrink: 0,
      }}>
        <StatusDot status={entity.status}/>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700,
        }}>{entity.badge}</span>
      </div>
    </article>
  );
};

const MeepleCardCompact = ({ entity, selectable, selected, onSelect }) => {
  const ent = entity.type;
  return (
    <article tabIndex={0} style={{
      display:'flex', alignItems:'center', gap: 10, padding:'7px 10px',
      background: selected ? entityHsl(ent, 0.06) : 'transparent',
      borderBottom:'1px solid var(--border-light)', cursor:'pointer',
    }}>
      {selectable && (
        <input type="checkbox" checked={selected} onChange={onSelect} onClick={(e) => e.stopPropagation()}
          style={{ accentColor: entityHsl(ent), flexShrink: 0 }}/>
      )}
      <span aria-hidden="true" style={{ fontSize: 14, width: 18, textAlign:'center', flexShrink: 0 }}>{DS.EC[ent].em}</span>
      <span style={{
        fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700,
        color:'var(--text)', flex: 1,
        whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
      }}>{entity.title}</span>
      <span style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 600,
        whiteSpace:'nowrap', flexShrink: 0,
      }}>{entity.subtitle || ''}</span>
      <StatusDot status={entity.status}/>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── LIBRARY GRID ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const LibraryGrid = ({ items, view, selectable, selectedIds, onToggleSelect, compact, columns }) => {
  if (view === 'list') {
    return (
      <div style={{ display:'flex', flexDirection:'column', gap: 6 }}>
        {items.map(it => (
          <MeepleCardList key={it.id} entity={it}
            selectable={selectable}
            selected={selectedIds.has(it.id)}
            onSelect={() => onToggleSelect(it.id)}/>
        ))}
      </div>
    );
  }
  if (view === 'compact') {
    return (
      <div style={{
        background:'var(--bg-card)', border:'1px solid var(--border)',
        borderRadius:'var(--r-lg)', overflow:'hidden',
      }}>
        {items.map(it => (
          <MeepleCardCompact key={it.id} entity={it}
            selectable={selectable}
            selected={selectedIds.has(it.id)}
            onSelect={() => onToggleSelect(it.id)}/>
        ))}
      </div>
    );
  }
  return (
    <div style={{
      display:'grid',
      gridTemplateColumns: compact
        ? 'repeat(auto-fill, minmax(150px, 1fr))'
        : `repeat(${columns}, minmax(0, 1fr))`,
      gap: compact ? 8 : 12,
    }}>
      {items.map(it => (
        <MeepleCardGrid key={it.id} entity={it}
          selectable={selectable}
          selected={selectedIds.has(it.id)}
          onSelect={() => onToggleSelect(it.id)}/>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── BULK SELECTION BAR ───────────────────────────────
// /* v2: BulkSelectionBar */
// ═══════════════════════════════════════════════════════
const BulkSelectionBar = ({ count, onClear, compact }) => {
  if (count === 0) return null;
  return (
    <div role="region" aria-label="Selezione multipla" style={{
      position:'absolute', bottom: 16, left: '50%', transform:'translateX(-50%)',
      padding:'10px 14px', borderRadius:'var(--r-xl)',
      background:'var(--text)',
      color:'var(--bg)',
      boxShadow:'0 12px 32px rgba(0,0,0,.3)',
      display:'flex', alignItems:'center', gap: 10,
      zIndex: 30, maxWidth: compact ? 'calc(100% - 32px)' : 720,
      animation:'mai-slide-up .25s var(--ease-spring)',
    }}>
      <span style={{
        padding:'2px 8px', borderRadius:'var(--r-pill)',
        background: entityHsl('game'), color:'#fff',
        fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
        fontVariantNumeric:'tabular-nums', flexShrink: 0,
      }}>{count}</span>
      <span style={{
        fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700,
        flex: 1, whiteSpace:'nowrap',
      }}>{compact ? 'sel.' : 'selezionati'}</span>
      {[
        { icon:'⊘', label:'Archivia' },
        { icon:'🏷', label:'Tag' },
        { icon:'↗', label:'Esporta' },
      ].map(a => (
        <button key={a.label} type="button" style={{
          padding: compact ? '6px 8px' : '6px 10px',
          borderRadius:'var(--r-md)',
          background:'rgba(255,255,255,.12)', color:'inherit', border:'none',
          fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
          cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 5,
          flexShrink: 0,
        }}>
          <span aria-hidden="true">{a.icon}</span>{!compact && a.label}
        </button>
      ))}
      <button onClick={onClear} aria-label="Annulla selezione" style={{
        padding:'6px 10px', borderRadius:'var(--r-md)',
        background:'transparent', color:'inherit',
        border:'1px solid rgba(255,255,255,.2)',
        fontSize: 12, cursor:'pointer', flexShrink: 0,
      }}>✕</button>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── RECENT ACTIVITY RAIL (right sidebar collapsible) ─
// /* v2: RecentActivityRail */
// ═══════════════════════════════════════════════════════
const RecentActivityRail = () => (
  <aside style={{
    width: 280, flexShrink: 0,
    background:'var(--bg-card)', borderLeft:'1px solid var(--border)',
    padding: '18px 16px', overflowY:'auto',
  }}>
    <div style={{
      display:'flex', alignItems:'center', justifyContent:'space-between',
      marginBottom: 14,
    }}>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
        color:'var(--text)', margin: 0,
        display:'flex', alignItems:'center', gap: 6,
      }}><span aria-hidden="true">🕐</span>Ultime modifiche</h3>
      <button aria-label="Comprimi pannello" style={{
        width: 24, height: 24, borderRadius:'var(--r-sm)',
        background:'transparent', border:'1px solid var(--border)',
        color:'var(--text-muted)', cursor:'pointer', fontSize: 11,
      }}>›</button>
    </div>
    <div style={{ position:'relative' }}>
      {DS.activity.slice(0, 6).map((a, i) => {
        const ec = DS.EC[a.kind] || DS.EC.game;
        return (
          <div key={a.id} style={{ display:'flex', gap: 9, marginBottom: 10 }}>
            <div style={{
              display:'flex', flexDirection:'column', alignItems:'center', flexShrink: 0,
            }}>
              <div style={{
                width: 22, height: 22, borderRadius:'50%',
                background: entityHsl(a.kind, 0.14), color: entityHsl(a.kind),
                display:'flex', alignItems:'center', justifyContent:'center',
                fontSize: 10, border:`1.5px solid ${entityHsl(a.kind, 0.3)}`,
              }} aria-hidden="true">{ec.em}</div>
              {i < 5 && (
                <div style={{ width: 1.5, flex: 1, background:'var(--border)', minHeight: 14, marginTop: 2 }}/>
              )}
            </div>
            <div style={{ flex: 1, minWidth: 0, paddingBottom: 2 }}>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
                fontWeight: 700, textTransform:'uppercase', letterSpacing:'.06em',
              }}>{a.at}</div>
              <div style={{ fontSize: 11.5, color:'var(--text)', lineHeight: 1.4 }}>
                <strong>{a.who}</strong> {a.what}{' '}
                <a href="#" onClick={e=>e.preventDefault()} style={{
                  color: entityHsl(a.kind), fontWeight: 700, textDecoration:'none',
                }}>{DS.byId[a.ref]?.title || a.ref}</a>
              </div>
            </div>
          </div>
        );
      })}
    </div>
    <div style={{
      marginTop: 12, padding:'10px 12px', borderRadius:'var(--r-md)',
      background:'var(--bg-muted)',
      fontSize: 11, color:'var(--text-muted)', lineHeight: 1.5,
    }}>
      <strong style={{ color:'var(--text-sec)', display:'block', marginBottom: 3 }}>⌨ Shortcuts</strong>
      <div style={{ display:'flex', flexDirection:'column', gap: 2, fontFamily:'var(--f-mono)', fontSize: 10 }}>
        <span><kbd style={kbdSt}>/</kbd> focus search</span>
        <span><kbd style={kbdSt}>f</kbd> filtri avanzati</span>
        <span><kbd style={kbdSt}>?</kbd> tutte le scorciatoie</span>
      </div>
    </div>
  </aside>
);

const kbdSt = {
  padding:'1px 5px', borderRadius: 3,
  background:'var(--bg-card)', border:'1px solid var(--border)',
  fontFamily:'var(--f-mono)', fontWeight: 800, color:'var(--text-sec)',
  marginRight: 4,
};

// ═══════════════════════════════════════════════════════
// ─── EMPTY / LOADING / ERROR ──────────────────────────
// ═══════════════════════════════════════════════════════
const EmptyLibrary = ({ kind, compact }) => {
  if (kind === 'first-run') {
    return (
      <div style={{
        padding:'48px 24px', textAlign:'center',
        background:'var(--bg-card)',
        border:'1px dashed var(--border-strong)',
        borderRadius:'var(--r-xl)',
        display:'flex', flexDirection:'column', alignItems:'center',
      }}>
        <div style={{
          width: 96, height: 96, borderRadius:'50%',
          background:`radial-gradient(circle, ${entityHsl('game', 0.18)} 0%, transparent 70%)`,
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 44, marginBottom: 14,
        }} aria-hidden="true">📚</div>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 20, fontWeight: 800,
          color:'var(--text)', margin:'0 0 6px',
        }}>La tua libreria è vuota</h2>
        <p style={{
          fontSize: 13.5, color:'var(--text-sec)', margin:'0 0 18px', maxWidth: 380, lineHeight: 1.55,
        }}>Inizia aggiungendo il tuo primo gioco. Importa la collezione da BoardGameGeek o cerca per titolo.</p>
        <div style={{ display:'flex', gap: 8, marginBottom: 22, flexWrap:'wrap', justifyContent:'center' }}>
          <button type="button" style={{
            padding:'9px 16px', borderRadius:'var(--r-md)',
            background: entityHsl('game'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            cursor:'pointer', boxShadow:`0 4px 14px ${entityHsl('game', 0.4)}`,
          }}>+ Aggiungi il tuo primo gioco</button>
          <button type="button" style={{
            padding:'9px 14px', borderRadius:'var(--r-md)',
            background:'var(--bg)', color:'var(--text)',
            border:'1px solid var(--border-strong)',
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700, cursor:'pointer',
          }}>↓ Importa da BGG</button>
        </div>
        {/* Suggested */}
        <div style={{ width:'100%', maxWidth: 480 }}>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
            fontWeight: 800, textTransform:'uppercase', letterSpacing:'.08em',
            marginBottom: 8, textAlign:'left',
          }}>Suggerimenti dalla community</div>
          <div style={{
            display:'grid',
            gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(3, 1fr)',
            gap: 8,
          }}>
            {DS.games.slice(0, compact ? 4 : 3).map(g => (
              <div key={g.id} style={{
                padding: 10, borderRadius:'var(--r-md)',
                background:'var(--bg)', border:'1px solid var(--border)',
                cursor:'pointer', display:'flex', alignItems:'center', gap: 8,
              }}>
                <div style={{
                  width: 32, height: 32, borderRadius:'var(--r-sm)',
                  background: g.cover, display:'flex', alignItems:'center', justifyContent:'center',
                  fontSize: 16, flexShrink: 0,
                }} aria-hidden="true">{g.coverEmoji}</div>
                <div style={{ flex: 1, minWidth: 0, textAlign:'left' }}>
                  <div style={{
                    fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 800,
                    color:'var(--text)', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
                  }}>{g.title}</div>
                  <div style={{
                    fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)', fontWeight: 700,
                  }}>★ {g.rating}</div>
                </div>
                <span aria-hidden="true" style={{ color: entityHsl('game'), fontSize: 12, fontWeight: 800 }}>+</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    );
  }
  if (kind === 'no-results') {
    return (
      <div style={{
        padding:'40px 24px', textAlign:'center',
        background:'var(--bg-card)',
        border:'1px dashed var(--border-strong)',
        borderRadius:'var(--r-xl)',
        display:'flex', flexDirection:'column', alignItems:'center',
      }}>
        <div style={{
          width: 60, height: 60, borderRadius:'50%',
          background: entityHsl('agent', 0.12), color: entityHsl('agent'),
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 26, marginBottom: 12,
        }} aria-hidden="true">⌕</div>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
          color:'var(--text)', margin:'0 0 4px',
        }}>Nessun risultato</h3>
        <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 320 }}>
          Nessun elemento corrisponde ai filtri selezionati. Prova a rimuovere alcuni vincoli.
        </p>
        <button type="button" style={{
          padding:'8px 14px', borderRadius:'var(--r-md)',
          background:'transparent', color: entityHsl('agent'), border:`1px solid ${entityHsl('agent', 0.4)}`,
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
        }}>↻ Reset filtri</button>
      </div>
    );
  }
  if (kind === 'tab-empty-agents') {
    return (
      <div style={{
        padding:'40px 24px', textAlign:'center',
        background:'var(--bg-card)',
        border:'1px dashed var(--border-strong)',
        borderRadius:'var(--r-xl)',
        display:'flex', flexDirection:'column', alignItems:'center',
      }}>
        <div style={{
          width: 60, height: 60, borderRadius:'50%',
          background: entityHsl('agent', 0.14), color: entityHsl('agent'),
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 28, marginBottom: 12,
        }} aria-hidden="true">🤖</div>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
          color:'var(--text)', margin:'0 0 4px',
        }}>Nessun agente ancora</h3>
        <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 320 }}>
          Crea un agente per ogni gioco — risponderà a domande sulle regole usando i tuoi documenti.
        </p>
        <button type="button" style={{
          padding:'8px 16px', borderRadius:'var(--r-md)',
          background: entityHsl('agent'), color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800, cursor:'pointer',
          boxShadow:`0 3px 10px ${entityHsl('agent', 0.35)}`,
        }}>+ Crea il tuo primo agente</button>
      </div>
    );
  }
  return null;
};

const LoadingGrid = ({ columns, compact }) => (
  <div style={{
    display:'grid',
    gridTemplateColumns: compact
      ? 'repeat(auto-fill, minmax(150px, 1fr))'
      : `repeat(${columns}, minmax(0, 1fr))`,
    gap: compact ? 8 : 12,
  }}>
    {Array.from({ length: columns * 2 }).map((_, i) => (
      <div key={i} className="mai-shimmer" style={{
        height: 180, borderRadius:'var(--r-lg)', background:'var(--bg-muted)',
      }}/>
    ))}
  </div>
);

const ErrorState = () => (
  <div style={{
    padding:'40px 24px', textAlign:'center',
    background:'hsl(var(--c-danger) / .06)',
    border:'1px solid hsl(var(--c-danger) / .25)',
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
      fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>Errore caricamento libreria</h3>
    <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 12px', maxWidth: 320 }}>
      Impossibile recuperare i dati. Verifica la connessione.
    </p>
    <button type="button" style={{
      padding:'7px 14px', borderRadius:'var(--r-md)',
      background:'hsl(var(--c-danger))', color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
    }}>↻ Riprova</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── BODY ─────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const LibraryBody = ({ stateOverride, initialTab='all', initialView='grid', drawerOpen=false, withRail, compact, columns=4, withBulk=false }) => {
  const [tab, setTab] = useState(initialTab);
  const [view, setView] = useState(initialView);
  const [drawer, setDrawer] = useState(drawerOpen);
  const [search, setSearch] = useState('');
  const [filters, setFilters] = useState({
    status: ['owned'],
    entity: ['game', 'agent', 'kb'],
  });
  const [selected, setSelected] = useState(new Set(withBulk ? ['g-azul', 'g-wingspan', 'a-azul-rules'] : []));

  useEffect(() => { setTab(initialTab); }, [initialTab]);
  useEffect(() => { setView(initialView); }, [initialView]);
  useEffect(() => { setDrawer(drawerOpen); }, [drawerOpen]);

  const items = useMemo(() => {
    if (tab === 'game') return DS.games;
    if (tab === 'agent') return DS.agents;
    if (tab === 'kb') return DS.kbs;
    if (tab === 'session') return DS.sessions;
    if (tab === 'chat') return DS.chats;
    // all → mix
    return [
      ...DS.games.slice(0, 4),
      ...DS.agents.slice(0, 3),
      ...DS.kbs.slice(0, 3),
      ...DS.sessions.slice(0, 2),
      ...DS.chats.slice(0, 2),
    ];
  }, [tab]);

  const tabs = [
    { id:'all',     icon:'⌗',  label:'Tutti',    count: 88 },
    { id:'game',    icon:'🎲', label:'Giochi',   count: DS.games.length },
    { id:'agent',   icon:'🤖', label:'Agenti',   count: DS.agents.length },
    { id:'kb',      icon:'📚', label:'KB',       count: DS.kbs.length },
    { id:'session', icon:'🎯', label:'Sessioni', count: DS.sessions.length },
    { id:'chat',    icon:'💬', label:'Chat',     count: DS.chats.length },
  ];

  const stats = [
    { entity:'game', count: DS.games.length, label:'giochi' },
    { entity:'agent', count: DS.agents.length, label:'agenti' },
    { entity:'kb', count: DS.kbs.length, label:'documenti' },
    { entity:'chat', count: 312, label:'chat' },
  ];

  const activeFilterCount = Object.values(filters).reduce((acc, v) => {
    if (Array.isArray(v)) return acc + v.length;
    if (v) return acc + 1;
    return acc;
  }, 0);

  const toggleSelect = (id) => {
    setSelected(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const renderBody = () => {
    if (stateOverride === 'loading') return <LoadingGrid columns={columns} compact={compact}/>;
    if (stateOverride === 'error') return <ErrorState/>;
    if (stateOverride === 'empty-first-run') return <EmptyLibrary kind="first-run" compact={compact}/>;
    if (stateOverride === 'empty-filtered') return <EmptyLibrary kind="no-results"/>;
    if (stateOverride === 'empty-tab-agents') return <EmptyLibrary kind="tab-empty-agents"/>;
    return <LibraryGrid items={items} view={view}
      selectable={withBulk} selectedIds={selected} onToggleSelect={toggleSelect}
      compact={compact} columns={columns}/>;
  };

  return (
    <div style={{
      flex: 1, display:'flex', flexDirection:'column', position:'relative',
      background:'var(--bg)', minHeight: 0,
    }}>
      <LibraryHero stats={stats} compact={compact}/>
      <div style={{ position:'sticky', top: 0, zIndex: 9, background:'var(--bg)' }}>
        <EntityTabBar tabs={tabs} active={tab} onChange={setTab} compact={compact}/>
      </div>
      <div style={{ position:'sticky', top: 45, zIndex: 8 }}>
        <CrossEntityFilters
          activeFilterCount={activeFilterCount}
          onOpenDrawer={() => setDrawer(true)}
          view={view} onViewChange={setView}
          compact={compact}
          search={search} onSearchChange={setSearch}/>
      </div>
      <div style={{
        display:'flex', flex: 1, minHeight: 0,
      }}>
        <div style={{
          flex: 1, padding: compact ? '14px 16px 80px' : '20px 32px 80px',
          overflowY:'auto',
        }}>
          {renderBody()}
        </div>
        {withRail && !compact && <RecentActivityRail/>}
      </div>

      <BulkSelectionBar count={selected.size} onClear={() => setSelected(new Set())} compact={compact}/>
      <AdvancedFiltersDrawer
        open={drawer} onClose={() => setDrawer(false)}
        activeFilters={filters} onApply={setFilters}
        compact={compact}/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FRAMES ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const TopNav = ({ compact }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 14,
    padding: compact ? '9px 14px' : '10px 32px',
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
      {!compact && <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>}
    </div>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      marginLeft: compact ? 0 : 14, fontWeight: 700,
      whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
    }}>
      <strong style={{ color:'var(--text-sec)' }}>Library</strong>
    </div>
    {!compact && (
      <span aria-label="Notifiche" style={{
        padding:'4px 8px', borderRadius:'var(--r-pill)',
        background:'var(--bg-muted)', color:'var(--text-sec)',
        fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      }}>⌨ ? scorciatoie</span>
    )}
  </div>
);

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const PhoneScreen = (props) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflow:'hidden', display:'flex', flexDirection:'column', position:'relative', background:'var(--bg)' }}>
      <TopNav compact/>
      <LibraryBody {...props} compact columns={2}/>
    </div>
  </>
);

const DesktopFrameInner = (props) => (
  <div style={{ display:'flex', flexDirection:'column', minHeight: 740, position:'relative', background:'var(--bg)' }}>
    <TopNav/>
    <LibraryBody {...props}/>
  </div>
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

const DesktopFrame = ({ label, desc, children, fullWidth }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width:'100%', maxWidth: fullWidth ? 1600 : 1280,
      borderRadius:'var(--r-xl)', border:'1px solid var(--border)',
      background:'var(--bg)', overflow:'hidden',
      boxShadow:'var(--shadow-lg)',
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
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/library</span>
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
// ─── ROOT ──────────────────────────────────────────────
const MOBILE_STATES = [
  { id:'m1', tab:'all', view:'grid', label:'01 · All · Grid 2-col', desc:'Hero compatto + tabs entity-aware + filter chips orizzontali + 2-col grid mix entity (giochi+agenti+KB+sessioni+chat).' },
  { id:'m2', tab:'game', view:'grid', label:'02 · Giochi · Grid', desc:'Tab Giochi attiva (underline + bg subtle entity-game). Filtri sticky + 2-col cards entity-game con accent bar.' },
  { id:'m3', tab:'agent', view:'list', label:'03 · Agenti · List', desc:'Tab Agenti attiva, vista list più densa con border-left entity-agent + status dot + badge mono.' },
  { id:'m4', tab:'all', view:'compact', label:'04 · Compact view', desc:'Vista compact: una row per item, dot status + emoji entity. Massima densità per power-user.' },
  { id:'m5', tab:'all', view:'grid', drawerOpen:true, label:'05 · Drawer aperto', desc:'AdvancedFiltersDrawer come bottom-sheet 100% width: 7 sezioni accordion (Stato/Entity/Gioco/Periodo/Tag/Rating/Complessità). Footer fisso Reset + Applica(N).' },
  { id:'m6', tab:'all', view:'grid', state:'empty-first-run', label:'06 · Empty first-run', desc:'Hero + tabs visibili. Body: illustrazione + CTA Aggiungi/Importa + 4 suggerimenti BGG con add inline.' },
  { id:'m7', tab:'all', view:'grid', state:'empty-filtered', label:'07 · Nessun risultato', desc:'Filtri attivi visibili nel chip "SORT". Body empty centered con CTA Reset filtri colore agent.' },
  { id:'m8', tab:'all', view:'grid', state:'loading', label:'08 · Loading', desc:'Hero pieno + tabs + filtri reali, body 4 skeleton card altezza 180.' },
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
        }}>SP4 wave 1 · #5/5 (FINALE) · Hub power-user multi-entity</div>
        <h1>Library desktop — /library</h1>
        <p className="lead">
          Hub power-user: hero compatto stats inline · tabs entity-aware (Tutti/Giochi/Agenti/KB/Sessioni/Chat) · filtri sticky cross-entity · search ⌘/.
          ⭐ <strong>AdvancedFiltersDrawer</strong> standalone reusable (7 sezioni accordion: status/entity/game/period/tags/rating/weight) — usabile anche in games-index e agents-index.
          3 view modes (Grid/List/Compact) · bulk select con sticky bar · activity rail collapsible · keyboard shortcuts.
        </p>

        <div className="section-label">Mobile · 375 — 8 stati / variazioni</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <PhoneScreen
                stateOverride={s.state || null}
                initialTab={s.tab}
                initialView={s.view}
                drawerOpen={s.drawerOpen || false}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — 5 stati chiave</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="09 · Desktop · All · Grid 4-col + Activity rail"
            desc="Vista power-user completa: hero pieno (stats + 3 CTA), 6 tabs, filtri cross-entity sticky, grid 4-col mix entity. Sidebar destra 'Ultime modifiche' timeline + scorciatoie.">
            <DesktopFrameInner stateOverride={null} initialTab="all" initialView="grid" withRail/>
          </DesktopFrame>

          <DesktopFrame label="10 · Desktop · Giochi · Grid + Bulk select"
            desc="Tab Giochi · 3 card pre-selezionate (border + glow + checkbox visibile). Bulk selection bar floating bottom: count chip game, [Archivia] [Tag] [Esporta] + [✕].">
            <DesktopFrameInner stateOverride={null} initialTab="game" initialView="grid" withBulk/>
          </DesktopFrame>

          <DesktopFrame label="11 · Desktop · AdvancedFiltersDrawer aperto"
            desc="⭐ Componente standalone v2: side panel 400px right, 7 sezioni accordion. Stato (chips multi) · Entity (chips multi · 5 tipi) · Gioco (search+chips) · Periodo (radio quick) · Tag (chips) · Rating (range slider 1-10) · Complessità (chips). Footer Reset + Applica(N).">
            <DesktopFrameInner stateOverride={null} initialTab="all" initialView="grid" drawerOpen/>
          </DesktopFrame>

          <DesktopFrame label="12 · Desktop · List view + Search active"
            desc="Vista list: rows dense con avatar 42 + entity chip + title + subtitle + status. Hover lift entity-coloured. Adatta a power-user con tanti item.">
            <DesktopFrameInner stateOverride={null} initialTab="all" initialView="list" withRail/>
          </DesktopFrame>

          <DesktopFrame label="13 · Desktop · Empty first-run"
            desc="Nuovo utente: hero (stats a zero) + tabs visibili (count 0) + body con illustrazione 96 + CTA Aggiungi/Importa + 3 suggerimenti BGG con +.">
            <DesktopFrameInner stateOverride="empty-first-run" initialTab="all" initialView="grid"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        @keyframes mai-fade-in { from { opacity: 0; } to { opacity: 1; } }
        @keyframes mai-slide-left { from { transform: translateX(100%); } to { transform: translateX(0); } }
        @keyframes mai-slide-up { from { transform: translateY(100%); opacity: 0; } to { transform: translateY(0); opacity: 1; } }
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
        .mai-cb-scroll::-webkit-scrollbar { display: none; }
        .mai-card-grid {
          outline: none;
          transition: transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm), box-shadow var(--dur-sm);
        }
        .mai-card-grid:hover {
          transform: translateY(-2px);
          border-color: hsl(var(--c-game) / .35);
          box-shadow: 0 8px 22px hsl(var(--c-game) / .14);
        }
        .mai-card-list {
          outline: none;
          transition: transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm), box-shadow var(--dur-sm);
        }
        .mai-card-list:hover {
          transform: translateX(2px);
          box-shadow: var(--shadow-xs);
        }
        .mai-card-grid:focus-visible, .mai-card-list:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
        .mai-card-menu { opacity: 0; transition: opacity var(--dur-sm); }
        .mai-card-grid:hover .mai-card-menu { opacity: 1; }
        button:focus-visible, a:focus-visible, input:focus-visible, select:focus-visible {
          outline: 2px solid hsl(var(--c-agent));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
