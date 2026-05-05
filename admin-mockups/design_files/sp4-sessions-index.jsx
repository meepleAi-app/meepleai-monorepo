/* MeepleAI SP4 wave 2 — Schermata #1 / 3
   Route: /sessions
   File: admin-mockups/design_files/sp4-sessions-index.{html,jsx}
   Pattern: Hero leggero + filtri sticky + list (default) / grid view

   Entity: session — palette 240 60% 55% (indaco)

   v2 nuovi:
   - SessionsHero       → apps/web/src/components/ui/v2/sessions-hero/
   - SessionFilters     → apps/web/src/components/ui/v2/session-filters/ (chip + dropdowns + search + view toggle)
   - SessionCardList    → MeepleCard.list variant entity=session con scoring inline + esito badge
   - SessionCardGrid    → MeepleCard.grid variant entity=session con cover gioco prominente
   - EmptySessions      → apps/web/src/components/ui/v2/empty-sessions/

   Riusi prod: ConnectionChipStrip footer (max 3: game/player/chat).
*/

const { useState, useEffect, useRef, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.session;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// Sessioni espanse con scoring e meta
const SESSIONS = [
  { id:'s1', game:'g-wingspan', date:'23 apr 2026', when:'2 giorni fa', duration:'1h 24m', playerCount: 4,
    status:'completed', outcome:'won',
    scores:[
      { name:'Marco', score: 89, winner: true },
      { name:'Anna', score: 76 },
      { name:'Luca', score: 64 },
      { name:'Sara', score: 58 },
    ], hasChat: true, chatCount: 3 },
  { id:'s2', game:'g-azul', date:'21 apr 2026', when:'4 giorni fa', duration:'42m', playerCount: 3,
    status:'completed', outcome:'lost',
    scores:[
      { name:'Sara', score: 81, winner: true },
      { name:'Marco', score: 72 },
      { name:'Luca', score: 65 },
    ], hasChat: false },
  { id:'s3', game:'g-brass', date:'oggi · live', when:'in corso', duration:'1h 12m', playerCount: 4,
    status:'inprogress', outcome:null,
    scores:[
      { name:'Marco', score: 24 },
      { name:'Anna', score: 31 },
      { name:'Luca', score: 19 },
      { name:'Sara', score: 28 },
    ], hasChat: true, chatCount: 7,
    turn: '12/18' },
  { id:'s4', game:'g-catan', date:'18 apr 2026', when:'1 settimana fa', duration:'2h 05m', playerCount: 4,
    status:'completed', outcome:'won',
    scores:[
      { name:'Marco', score: 10, winner: true },
      { name:'Anna', score: 8 },
      { name:'Luca', score: 7 },
      { name:'Sara', score: 6 },
    ], hasChat: false },
  { id:'s5', game:'g-arknova', date:'15 apr 2026', when:'10g fa', duration:'2h 48m', playerCount: 2,
    status:'completed', outcome:'tie',
    scores:[
      { name:'Marco', score: 124 },
      { name:'Anna', score: 124 },
    ], hasChat: true, chatCount: 2 },
  { id:'s6', game:'g-spirit', date:'12 apr 2026', when:'2 sett fa', duration:'1h 55m', playerCount: 3,
    status:'completed', outcome:'lost',
    scores:[
      { name:'Anna', score: 0, winner: true, note:'Win cooperativo' },
      { name:'Marco', score: 0 },
      { name:'Sara', score: 0 },
    ], hasChat: false },
  { id:'s7', game:'g-7wonders', date:'5 apr 2026', when:'3 sett fa', duration:'28m', playerCount: 2,
    status:'abandoned', outcome:null,
    scores:[
      { name:'Marco', score: 32 },
      { name:'Sara', score: 41 },
    ], hasChat: false },
  { id:'s8', game:'g-wingspan', date:'oggi · pausa', when:'in pausa', duration:'45m', playerCount: 3,
    status:'inprogress', outcome:null,
    scores:[
      { name:'Marco', score: 38 },
      { name:'Luca', score: 42 },
      { name:'Anna', score: 35 },
    ], hasChat: true, chatCount: 1,
    turn: '6/10', paused: true },
  { id:'s9', game:'g-azul', date:'30 mar 2026', when:'1 mese fa', duration:'52m', playerCount: 4,
    status:'completed', outcome:'won',
    scores:[
      { name:'Marco', score: 95, winner: true },
      { name:'Sara', score: 78 },
      { name:'Luca', score: 71 },
      { name:'Anna', score: 64 },
    ], hasChat: false },
  { id:'s10', game:'g-brass', date:'28 mar 2026', when:'1 mese fa', duration:'2h 30m', playerCount: 3,
    status:'completed', outcome:'lost',
    scores:[
      { name:'Anna', score: 158, winner: true },
      { name:'Marco', score: 142 },
      { name:'Luca', score: 119 },
    ], hasChat: false },
];

// ─── ConnectionChipStrip (footer card, max 3) ───────
const ChipStrip = ({ chips }) => (
  <div style={{ display:'flex', gap: 5, flexWrap:'wrap' }}>
    {chips.map((c, i) => {
      const isEmpty = c.count === 0 || c.empty;
      return (
        <span key={i} style={{
          display:'inline-flex', alignItems:'center', gap: 3,
          padding:'2px 7px', borderRadius:'var(--r-pill)',
          background: isEmpty ? 'transparent' : entityHsl(c.entity, 0.1),
          border: isEmpty
            ? `1px dashed ${entityHsl(c.entity, 0.4)}`
            : `1px solid ${entityHsl(c.entity, 0.2)}`,
          color: entityHsl(c.entity),
          fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
          opacity: isEmpty ? 0.55 : 1,
          textTransform:'uppercase', letterSpacing:'.04em',
        }}>
          <span aria-hidden="true">{DS.EC[c.entity].em}</span>
          {c.label && <span>{c.label}</span>}
          {c.count !== undefined && !c.label && <span>{c.count}</span>}
        </span>
      );
    })}
  </div>
);

// ─── Outcome badge ───────────────────────────────────
const OutcomeBadge = ({ outcome, status }) => {
  if (status === 'inprogress') {
    return (
      <span className="mai-pulse" style={{
        display:'inline-flex', alignItems:'center', gap: 4,
        padding:'2px 7px', borderRadius:'var(--r-pill)',
        background: entityHsl('session', 0.14),
        color: entityHsl('session'),
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
        border:`1px solid ${entityHsl('session', 0.3)}`,
      }}>
        <span aria-hidden="true" style={{
          width: 6, height: 6, borderRadius:'50%',
          background: entityHsl('session'),
        }}/>
        Live
      </span>
    );
  }
  if (status === 'abandoned') {
    return (
      <span style={{
        padding:'2px 7px', borderRadius:'var(--r-pill)',
        background:'var(--bg-muted)', color:'var(--text-muted)',
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
      }}>⊘ Abbandonata</span>
    );
  }
  const map = {
    won:  { label:'🏆 Vinta', bg:'hsl(140, 60%, 45% / .14)', fg:'hsl(140, 50%, 35%)', bd:'hsl(140, 60%, 45% / .3)' },
    lost: { label:'△ Persa', bg:'hsl(var(--c-event) / .12)', fg:'hsl(var(--c-event))', bd:'hsl(var(--c-event) / .3)' },
    tie:  { label:'= Pareggio', bg:'var(--bg-muted)', fg:'var(--text-sec)', bd:'var(--border)' },
  };
  const m = map[outcome] || map.tie;
  return (
    <span style={{
      padding:'2px 7px', borderRadius:'var(--r-pill)',
      background: m.bg, color: m.fg,
      border: `1px solid ${m.bd}`,
      fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
      textTransform:'uppercase', letterSpacing:'.06em',
    }}>{m.label}</span>
  );
};

// ─── Scoring inline ──────────────────────────────────
const ScoringInline = ({ scores, compact }) => (
  <div style={{
    display:'flex', flexWrap:'wrap', gap: '3px 8px',
    fontFamily:'var(--f-mono)', fontSize: compact ? 10.5 : 11.5,
    fontVariantNumeric:'tabular-nums', alignItems:'center',
  }}>
    {scores.map((s, i) => (
      <span key={i} style={{
        display:'inline-flex', alignItems:'center', gap: 3,
        color: s.winner ? entityHsl('session') : 'var(--text-sec)',
        fontWeight: s.winner ? 800 : 600,
      }}>
        {s.winner && <span aria-hidden="true" style={{ fontSize: compact ? 9 : 10 }}>🏆</span>}
        <span>{s.name}</span>
        <span style={{
          padding:'0 5px', borderRadius: 4,
          background: s.winner ? entityHsl('session', 0.14) : 'var(--bg-muted)',
          fontWeight: 800,
        }}>{s.score}</span>
      </span>
    ))}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── SESSIONS HERO ──────────────────────────────────
// /* v2: SessionsHero */
// ═══════════════════════════════════════════════════════
const SessionsHero = ({ compact }) => {
  const stats = [
    { entity:'session', icon:'🎯', count: 47, label:'partite' },
    { entity:'toolkit', icon:'🏆', count: 28, label:'vittorie' },
    { entity:'event',   icon:'⏱', count:'84h', label:'totali' },
    { entity:'chat',    icon:'📅', count:'2g', label:'ultima' },
  ];
  return (
    <div style={{
      padding: compact ? '14px 16px' : '22px 32px',
      background:`radial-gradient(circle at 0% 0%, ${entityHsl('session', 0.14)} 0%, transparent 60%), linear-gradient(180deg, var(--bg) 0%, var(--bg) 100%)`,
      borderBottom:'1px solid var(--border-light)',
      position:'relative', overflow:'hidden',
    }}>
      <div aria-hidden="true" style={{
        position:'absolute', top:-30, right:-30,
        width: 220, height: 220, borderRadius:'50%',
        background:`radial-gradient(circle, ${entityHsl('session', 0.1)} 0%, transparent 70%)`,
        pointerEvents:'none',
      }}/>
      <div style={{
        position:'relative', zIndex: 1,
        display:'flex', alignItems: compact ? 'flex-start' : 'flex-end',
        flexDirection: compact ? 'column' : 'row',
        gap: compact ? 12 : 22, flexWrap:'wrap',
      }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display:'inline-flex', alignItems:'center', gap: 5,
            padding:'2px 8px', borderRadius:'var(--r-pill)',
            background: entityHsl('session', 0.12),
            color: entityHsl('session'),
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.08em',
            marginBottom: 6,
            border:`1px solid ${entityHsl('session', 0.25)}`,
          }}>
            <span aria-hidden="true">🎯</span>Sessions
          </div>
          <h1 style={{
            fontFamily:'var(--f-display)', fontWeight: 800,
            fontSize: compact ? 24 : 34,
            letterSpacing:'-.02em', lineHeight: 1.05,
            margin:'0 0 3px', color:'var(--text)',
          }}>Le tue partite</h1>
          <p style={{
            fontFamily:'var(--f-body)', fontSize: compact ? 12.5 : 14,
            color:'var(--text-sec)', margin: 0, fontWeight: 500,
          }}>Storico, esiti e diari di gioco.</p>
        </div>

        {/* Stats */}
        <div style={{
          display:'flex', gap: compact ? 6 : 12, flexWrap:'wrap',
        }}>
          {stats.map(s => (
            <div key={s.label} style={{
              display:'flex', alignItems:'center', gap: 5,
              padding: compact ? '5px 8px' : '6px 10px',
              borderRadius:'var(--r-md)',
              background:'var(--bg-card)',
              border:`1px solid ${entityHsl(s.entity, 0.22)}`,
            }}>
              <span aria-hidden="true" style={{ fontSize: compact ? 12 : 13 }}>{s.icon}</span>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: compact ? 12 : 14, fontWeight: 800,
                color: entityHsl(s.entity), fontVariantNumeric:'tabular-nums',
              }}>{s.count}</span>
              <span style={{
                fontFamily:'var(--f-display)', fontSize: compact ? 10.5 : 11.5, fontWeight: 700,
                color:'var(--text-sec)',
              }}>{s.label}</span>
            </div>
          ))}
        </div>

        {/* Action bar */}
        <div style={{
          display:'flex', gap: 7, flexShrink: 0,
          width: compact ? '100%' : 'auto',
          justifyContent: compact ? 'flex-start' : 'flex-end',
        }}>
          <button type="button" style={{
            padding: compact ? '8px 12px' : '9px 16px',
            borderRadius:'var(--r-md)',
            background: entityHsl('session'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
            cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 5,
            boxShadow:`0 4px 14px ${entityHsl('session', 0.4)}`,
            flex: compact ? 1 : 'none',
            justifyContent:'center',
          }}><span aria-hidden="true">+</span>Registra partita</button>
          <button type="button" style={{
            padding: compact ? '8px 12px' : '9px 14px',
            borderRadius:'var(--r-md)',
            background:'transparent', color: entityHsl('session'),
            border:`1px solid ${entityHsl('session', 0.4)}`,
            fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
            cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 5,
            flex: compact ? 1 : 'none',
            justifyContent:'center',
          }}><span aria-hidden="true">▶</span>Avvia live</button>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FILTERS ─────────────────────────────────────────
// /* v2: SessionFilters */
// ═══════════════════════════════════════════════════════
const StatusChip = ({ active, label, onClick, count }) => (
  <button type="button" onClick={onClick} aria-pressed={active}
    style={{
      display:'inline-flex', alignItems:'center', gap: 5,
      padding:'6px 11px', borderRadius:'var(--r-pill)',
      background: active ? entityHsl('session', 0.14) : 'var(--bg-card)',
      border: active ? `1px solid ${entityHsl('session', 0.4)}` : '1px solid var(--border)',
      color: active ? entityHsl('session') : 'var(--text-sec)',
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
      cursor:'pointer', whiteSpace:'nowrap', flexShrink: 0,
    }}>
    {label}
    {count !== undefined && (
      <span style={{
        padding:'1px 6px', borderRadius:'var(--r-pill)',
        background: active ? entityHsl('session') : 'var(--bg-muted)',
        color: active ? '#fff' : 'var(--text-muted)',
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
      }}>{count}</span>
    )}
  </button>
);

const Dropdown = ({ label, value }) => (
  <button type="button" style={{
    display:'inline-flex', alignItems:'center', gap: 5,
    padding:'6px 10px', borderRadius:'var(--r-md)',
    background:'var(--bg-card)', border:'1px solid var(--border)',
    color:'var(--text-sec)',
    fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
    cursor:'pointer', whiteSpace:'nowrap', flexShrink: 0,
  }}>
    <span style={{
      fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
      textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 800,
    }}>{label}</span>
    <span>{value}</span>
    <span aria-hidden="true" style={{ fontSize: 10, opacity: .6 }}>▾</span>
  </button>
);

const SessionFilters = ({ statusFilter, onStatusChange, view, onViewChange, search, onSearchChange, compact, counts }) => (
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
        type="search" placeholder="Cerca partita o gioco..."
        value={search} onChange={e => onSearchChange(e.target.value)}
        style={{
          width:'100%', padding:'8px 12px 8px 34px',
          borderRadius:'var(--r-md)', border:'1px solid var(--border)',
          background:'var(--bg-card)', color:'var(--text)',
          fontFamily:'var(--f-body)', fontSize: 13,
        }}/>
    </div>

    {/* Status chips */}
    <div className="mai-cb-scroll" style={{
      display:'flex', alignItems:'center', gap: 6,
      overflowX:'auto', scrollbarWidth:'none',
    }}>
      <StatusChip active={statusFilter==='all'} onClick={() => onStatusChange('all')} label="Tutti" count={counts.all}/>
      <StatusChip active={statusFilter==='inprogress'} onClick={() => onStatusChange('inprogress')} label="● In corso" count={counts.inprogress}/>
      <StatusChip active={statusFilter==='completed'} onClick={() => onStatusChange('completed')} label="✓ Completate" count={counts.completed}/>
      <StatusChip active={statusFilter==='abandoned'} onClick={() => onStatusChange('abandoned')} label="⊘ Abbandonate" count={counts.abandoned}/>
    </div>

    {/* Dropdowns + view */}
    <div className="mai-cb-scroll" style={{
      display:'flex', alignItems:'center', gap: 6,
      overflowX:'auto', scrollbarWidth:'none',
    }}>
      <Dropdown label="GIOCO" value="Tutti"/>
      <Dropdown label="PERIODO" value="Sempre"/>
      <Dropdown label="ESITO" value="Tutti"/>
      <Dropdown label="SORT" value="Data ↓"/>
      <div style={{ flex: 1 }}/>
      {/* View toggle */}
      <div role="radiogroup" aria-label="Vista" style={{
        display:'inline-flex', borderRadius:'var(--r-md)',
        border:'1px solid var(--border)', background:'var(--bg-card)',
        overflow:'hidden', flexShrink: 0,
      }}>
        {[
          { id:'list', icon:'☰', label:'List · default' },
          { id:'grid', icon:'▦', label:'Grid' },
        ].map(v => {
          const active = view === v.id;
          return (
            <button key={v.id} type="button" role="radio" aria-checked={active}
              onClick={() => onViewChange(v.id)}
              aria-label={v.label}
              style={{
                padding:'6px 10px',
                background: active ? entityHsl('session', 0.14) : 'transparent',
                color: active ? entityHsl('session') : 'var(--text-muted)',
                border:'none', cursor:'pointer',
                fontSize: 13, fontWeight: 700,
              }}>{v.icon}</button>
          );
        })}
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── SESSION CARD · LIST ─────────────────────────────
// ═══════════════════════════════════════════════════════
const SessionCardList = ({ session, compact }) => {
  const game = DS.byId[session.game];
  const isInProgress = session.status === 'inprogress';
  const isAbandoned = session.status === 'abandoned';

  return (
    <article tabIndex={0} className="mai-card-row" style={{
      position:'relative',
      display:'flex', alignItems:'stretch', gap: 0,
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', overflow:'hidden',
      cursor:'pointer',
      borderLeft: `3px solid ${entityHsl('session')}`,
      opacity: isAbandoned ? 0.7 : 1,
    }}>
      {/* Cover */}
      <div style={{
        width: compact ? 56 : 76,
        background: game?.cover || entityHsl('session', 0.12),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: compact ? 24 : 32, flexShrink: 0,
      }} aria-hidden="true">
        <span style={{ filter:'drop-shadow(0 2px 4px rgba(0,0,0,.3))' }}>{game?.coverEmoji || '🎯'}</span>
      </div>

      {/* Body */}
      <div style={{
        flex: 1, minWidth: 0,
        padding: compact ? '10px 12px' : '12px 14px',
        display:'flex', flexDirection: compact ? 'column' : 'row',
        gap: compact ? 8 : 14, alignItems: compact ? 'stretch' : 'center',
      }}>
        {/* Left: title + meta */}
        <div style={{ flex: compact ? 'none' : 1, minWidth: 0 }}>
          <div style={{
            display:'flex', alignItems:'center', gap: 6, flexWrap:'wrap', marginBottom: 3,
          }}>
            <h3 style={{
              fontFamily:'var(--f-display)', fontSize: compact ? 13.5 : 14.5, fontWeight: 800,
              color:'var(--text)', margin: 0, lineHeight: 1.2,
            }}>{game?.title || 'Sessione'}</h3>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 10.5, color:'var(--text-muted)',
              fontWeight: 700,
            }}>· {session.date}</span>
            <OutcomeBadge outcome={session.outcome} status={session.status}/>
            {isInProgress && session.turn && (
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
                color: entityHsl('session'),
                padding:'1px 6px', borderRadius:'var(--r-pill)',
                background: entityHsl('session', 0.1),
                textTransform:'uppercase', letterSpacing:'.06em',
              }}>Turno {session.turn}{session.paused ? ' · pausa' : ''}</span>
            )}
          </div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
            fontWeight: 600, marginBottom: compact ? 6 : 8,
          }}>
            ⏱ {session.duration} · 👥 {session.playerCount} giocatori · {session.when}
          </div>
          {/* Scoring */}
          <ScoringInline scores={session.scores} compact={compact}/>
        </div>

        {/* Right: footer chips + CTA */}
        <div style={{
          display:'flex', flexDirection:'column', gap: 6,
          alignItems: compact ? 'flex-start' : 'flex-end',
          flexShrink: 0,
        }}>
          {isInProgress && !isAbandoned && (
            <button type="button" onClick={(e) => e.stopPropagation()} style={{
              padding:'7px 14px', borderRadius:'var(--r-md)',
              background: entityHsl('session'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 800,
              cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 4,
              boxShadow: `0 3px 10px ${entityHsl('session', 0.35)}`,
              whiteSpace:'nowrap',
            }}><span aria-hidden="true">▶</span>Riprendi</button>
          )}
          {isAbandoned && (
            <button type="button" onClick={(e) => e.stopPropagation()} style={{
              padding:'6px 12px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text-sec)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
              cursor:'pointer', whiteSpace:'nowrap',
            }}>Riprendi o archivia</button>
          )}
          <ChipStrip chips={[
            { entity:'game', label: game?.title?.slice(0, 8) || 'gioco' },
            { entity:'player', count: session.playerCount },
            ...(session.hasChat ? [{ entity:'chat', count: session.chatCount }] : [{ entity:'chat', empty: true, count: 0 }]),
          ]}/>
        </div>
      </div>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SESSION CARD · GRID ─────────────────────────────
// ═══════════════════════════════════════════════════════
const SessionCardGrid = ({ session, compact }) => {
  const game = DS.byId[session.game];
  const isInProgress = session.status === 'inprogress';
  const isAbandoned = session.status === 'abandoned';

  return (
    <article tabIndex={0} className="mai-card-grid" style={{
      position:'relative',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', overflow:'hidden',
      display:'flex', flexDirection:'column',
      cursor:'pointer',
      opacity: isAbandoned ? 0.7 : 1,
    }}>
      {/* Top accent bar */}
      <div aria-hidden="true" style={{
        position:'absolute', top: 0, left: 0, right: 0, height: 3,
        background: entityHsl('session'),
        zIndex: 2,
      }}/>
      {/* Cover prominent */}
      <div style={{
        height: compact ? 90 : 110,
        background: game?.cover || entityHsl('session', 0.12),
        position:'relative',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: compact ? 36 : 44,
      }} aria-hidden="true">
        <span style={{ filter:'drop-shadow(0 2px 6px rgba(0,0,0,.3))' }}>{game?.coverEmoji || '🎯'}</span>
        {/* Entity badge */}
        <div style={{
          position:'absolute', top: 8, left: 8,
          display:'inline-flex', alignItems:'center', gap: 4,
          padding:'2px 7px', borderRadius:'var(--r-pill)',
          background:'rgba(255,255,255,.85)', backdropFilter:'blur(6px)',
          fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800,
          color: entityHsl('session'),
          textTransform:'uppercase', letterSpacing:'.06em',
        }}>
          <span aria-hidden="true">🎯</span>Session
        </div>
        {/* Outcome top-right */}
        <div style={{ position:'absolute', top: 8, right: 8 }}>
          <OutcomeBadge outcome={session.outcome} status={session.status}/>
        </div>
      </div>
      {/* Body */}
      <div style={{ padding: 12, display:'flex', flexDirection:'column', gap: 6, flex: 1 }}>
        <div>
          <h3 style={{
            fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
            color:'var(--text)', margin: 0, lineHeight: 1.2,
          }}>{game?.title || 'Sessione'}</h3>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 10.5, color:'var(--text-muted)',
            fontWeight: 700, marginTop: 2,
          }}>{session.date} · ⏱ {session.duration} · 👥 {session.playerCount}</div>
        </div>
        <div style={{
          padding:'6px 8px', borderRadius:'var(--r-sm)',
          background:'var(--bg-muted)',
        }}>
          <ScoringInline scores={session.scores.slice(0, 3)} compact/>
          {session.scores.length > 3 && (
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)',
              marginTop: 2, fontWeight: 700,
            }}>+{session.scores.length - 3} altri</div>
          )}
        </div>
        {isInProgress && (
          <button type="button" onClick={(e) => e.stopPropagation()} style={{
            padding:'7px 12px', borderRadius:'var(--r-md)',
            background: entityHsl('session'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 800,
            cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 5,
            justifyContent:'center',
            boxShadow:`0 3px 10px ${entityHsl('session', 0.35)}`,
          }}><span aria-hidden="true">▶</span>Riprendi {session.turn && `· ${session.turn}`}</button>
        )}
        <div style={{ flex: 1 }}/>
        <div style={{
          padding:'5px 0 0', borderTop:'1px solid var(--border-light)',
        }}>
          <ChipStrip chips={[
            { entity:'game', label: game?.title?.slice(0, 10) || 'gioco' },
            { entity:'player', count: session.playerCount },
            ...(session.hasChat ? [{ entity:'chat', count: session.chatCount }] : [{ entity:'chat', empty: true, count: 0 }]),
          ]}/>
        </div>
      </div>
    </article>
  );
};

// ═══════════════════════════════════════════════════════
// ─── EMPTY / LOADING / ERROR ─────────────────────────
// ═══════════════════════════════════════════════════════
const EmptyState = ({ kind, filter, compact }) => {
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
          width: 92, height: 92, borderRadius:'50%',
          background:`radial-gradient(circle, ${entityHsl('session', 0.18)} 0%, transparent 70%)`,
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 42, marginBottom: 14,
        }} aria-hidden="true">🎯</div>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 19, fontWeight: 800,
          color:'var(--text)', margin:'0 0 6px',
        }}>Nessuna partita ancora</h2>
        <p style={{
          fontSize: 13, color:'var(--text-sec)', margin:'0 0 18px', maxWidth: 360, lineHeight: 1.5,
        }}>Registra la tua prima partita giocata o avvia una sessione live per tracciare turni, punteggi e diari.</p>
        <div style={{ display:'flex', gap: 8, flexWrap:'wrap', justifyContent:'center' }}>
          <button type="button" style={{
            padding:'9px 16px', borderRadius:'var(--r-md)',
            background: entityHsl('session'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            cursor:'pointer', boxShadow:`0 4px 14px ${entityHsl('session', 0.4)}`,
          }}>+ Registra prima partita</button>
          <button type="button" style={{
            padding:'9px 14px', borderRadius:'var(--r-md)',
            background:'transparent', color: entityHsl('session'),
            border:`1px solid ${entityHsl('session', 0.4)}`,
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 5,
          }}><span aria-hidden="true">▶</span>Avvia live</button>
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
          width: 56, height: 56, borderRadius:'50%',
          background: entityHsl('session', 0.12), color: entityHsl('session'),
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 24, marginBottom: 12,
        }} aria-hidden="true">⌕</div>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
          color:'var(--text)', margin:'0 0 4px',
        }}>{filter === 'week' ? 'Niente questa settimana' : 'Nessuna partita per questi filtri'}</h3>
        <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 320 }}>
          {filter === 'week'
            ? 'Vuoi avviare una partita ora?'
            : 'Prova a rimuovere alcuni vincoli o cambiare periodo.'}
        </p>
        <div style={{ display:'flex', gap: 6, flexWrap:'wrap', justifyContent:'center' }}>
          {filter === 'week' && (
            <button type="button" style={{
              padding:'7px 14px', borderRadius:'var(--r-md)',
              background: entityHsl('session'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
            }}>▶ Avvia live</button>
          )}
          <button type="button" style={{
            padding:'7px 14px', borderRadius:'var(--r-md)',
            background:'transparent', color: entityHsl('session'),
            border:`1px solid ${entityHsl('session', 0.4)}`,
            fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
          }}>↻ Reset filtri</button>
        </div>
      </div>
    );
  }
  return null;
};

const LoadingList = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
    {[0,1,2,3,4].map(i => (
      <div key={i} className="mai-shimmer" style={{
        height: 92, borderRadius:'var(--r-lg)', background:'var(--bg-muted)',
      }}/>
    ))}
  </div>
);

const LoadingGrid = ({ compact }) => (
  <div style={{
    display:'grid',
    gridTemplateColumns: compact ? '1fr' : 'repeat(3, minmax(0, 1fr))',
    gap: 12,
  }}>
    {[0,1,2,3,4,5].map(i => (
      <div key={i} className="mai-shimmer" style={{
        height: 220, borderRadius:'var(--r-lg)', background:'var(--bg-muted)',
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
    }}>Errore caricamento</h3>
    <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 12px', maxWidth: 320 }}>
      Impossibile recuperare le partite. Verifica la connessione.
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
const SessionsBody = ({ stateOverride, initialView='list', initialFilter='all', compact }) => {
  const [view, setView] = useState(initialView);
  const [statusFilter, setStatusFilter] = useState(initialFilter);
  const [search, setSearch] = useState('');

  useEffect(() => { setView(initialView); }, [initialView]);
  useEffect(() => { setStatusFilter(initialFilter); }, [initialFilter]);

  const counts = useMemo(() => ({
    all: SESSIONS.length,
    inprogress: SESSIONS.filter(s => s.status === 'inprogress').length,
    completed: SESSIONS.filter(s => s.status === 'completed').length,
    abandoned: SESSIONS.filter(s => s.status === 'abandoned').length,
  }), []);

  const items = useMemo(() => {
    if (statusFilter === 'all') return SESSIONS;
    return SESSIONS.filter(s => s.status === statusFilter);
  }, [statusFilter]);

  const renderBody = () => {
    if (stateOverride === 'loading') return view === 'list' ? <LoadingList/> : <LoadingGrid compact={compact}/>;
    if (stateOverride === 'error') return <ErrorState/>;
    if (stateOverride === 'empty-first-run') return <EmptyState kind="first-run" compact={compact}/>;
    if (stateOverride === 'empty-week') return <EmptyState kind="no-results" filter="week"/>;
    if (stateOverride === 'empty-filter') return <EmptyState kind="no-results"/>;

    if (view === 'grid') {
      return (
        <div style={{
          display:'grid',
          gridTemplateColumns: compact ? '1fr' : 'repeat(3, minmax(0, 1fr))',
          gap: 12,
        }}>
          {items.map(s => <SessionCardGrid key={s.id} session={s} compact={compact}/>)}
        </div>
      );
    }
    return (
      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        {items.map(s => <SessionCardList key={s.id} session={s} compact={compact}/>)}
      </div>
    );
  };

  return (
    <div style={{
      flex: 1, display:'flex', flexDirection:'column', position:'relative',
      background:'var(--bg)', minHeight: 0,
    }}>
      <SessionsHero compact={compact}/>
      <div style={{ position:'sticky', top: 0, zIndex: 8 }}>
        <SessionFilters
          statusFilter={statusFilter} onStatusChange={setStatusFilter}
          view={view} onViewChange={setView}
          search={search} onSearchChange={setSearch}
          compact={compact} counts={counts}/>
      </div>
      <div style={{
        flex: 1, padding: compact ? '14px 16px 32px' : '20px 32px 64px',
        overflowY:'auto',
      }}>
        <div style={{
          maxWidth: view === 'list' ? 1280 : 'none',
          margin: '0 auto',
        }}>
          {renderBody()}
        </div>
      </div>
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
    }}>
      <strong style={{ color:'var(--text-sec)' }}>Sessions</strong>
    </div>
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
      <SessionsBody {...props} compact/>
    </div>
  </>
);

const DesktopFrameInner = (props) => (
  <div style={{ display:'flex', flexDirection:'column', minHeight: 720, background:'var(--bg)' }}>
    <TopNav/>
    <SessionsBody {...props}/>
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
        padding:'9px 14px', background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/sessions</span>
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
const MOBILE_STATES = [
  { id:'m1', view:'list', filter:'all', label:'01 · List · default', desc:'Hero compatto + filter chips scrollabili. Card list con cover gioco 56 + scoring inline + outcome badge + ChipStrip game/player/chat.' },
  { id:'m2', view:'list', filter:'inprogress', label:'02 · In corso', desc:'Solo sessioni live. Card con badge "Live" pulsante entity-session + turno display + CTA [▶ Riprendi] entity-session bold.' },
  { id:'m3', view:'grid', filter:'all', label:'03 · Grid view', desc:'Vista alternativa: cover prominente 90, outcome top-right, scoring boxed in bg-muted, ChipStrip footer. Più visual, meno denso.' },
  { id:'m4', view:'list', filter:'abandoned', label:'04 · Abbandonate', desc:'Card opacity .7 + CTA "Riprendi o archivia" outline. Outcome badge muted.' },
  { id:'m5', view:'list', state:'empty-first-run', label:'05 · Empty first-run', desc:'Illustrazione 92 entity-session + 2 CTA: "+ Registra prima partita" primary + "▶ Avvia live" outline.' },
  { id:'m6', view:'list', state:'empty-week', label:'06 · Niente questa settimana', desc:'Empty filter contestuale: "Niente questa settimana — vuoi avviare una partita?" + CTA Live + Reset.' },
  { id:'m7', view:'list', state:'loading', label:'07 · Loading', desc:'Hero + filtri visibili + 5 skeleton row 92px shimmer.' },
  { id:'m8', view:'list', state:'error', label:'08 · Error', desc:'Errore con CTA Riprova in danger color.' },
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
        }}>SP4 wave 2 · #1/3 · Sessions index</div>
        <h1>Sessions index — /sessions</h1>
        <p className="lead">
          Hero compatto stats inline · 4 status chips + 4 dropdowns + search · view toggle <strong>List default</strong> / Grid alt.
          Card list con cover + scoring inline winner-highlighted + outcome badge contestuale (Vinta/Persa/Pareggio/Live/Abbandonata).
          Pattern speciali: card "in corso" con CTA Riprendi prominente · "abbandonata" con opacity .7 + CTA Riprendi/Archivia.
        </p>

        <div className="section-label">Mobile · 375 — 8 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <PhoneScreen
                stateOverride={s.state || null}
                initialView={s.view}
                initialFilter={s.filter || 'all'}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — 4 stati chiave</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="09 · Desktop · List · default"
            desc="Container 1280 max. Card row complete: cover 76 + title+date+outcome+turn (se live) + meta row + scoring inline winner-highlighted + ChipStrip + CTA Riprendi quando live.">
            <DesktopFrameInner stateOverride={null} initialView="list" initialFilter="all"/>
          </DesktopFrame>
          <DesktopFrame label="10 · Desktop · Grid · 3-col"
            desc="Vista alternativa: cover 110 prominente + entity badge top-left + outcome top-right + scoring boxed + CTA Riprendi quando live + ChipStrip footer.">
            <DesktopFrameInner stateOverride={null} initialView="grid" initialFilter="all"/>
          </DesktopFrame>
          <DesktopFrame label="11 · Desktop · Solo in corso"
            desc="Variant filter=inprogress: solo card live/pausa con CTA Riprendi prominente. Badge live pulsante entity-session, turno display.">
            <DesktopFrameInner stateOverride={null} initialView="list" initialFilter="inprogress"/>
          </DesktopFrame>
          <DesktopFrame label="12 · Desktop · Empty first-run"
            desc="Hero pieno + filtri visibili + body con illustrazione 92 entity-session + 2 CTA Registra/Live.">
            <DesktopFrameInner stateOverride="empty-first-run" initialView="list" initialFilter="all"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        @keyframes mai-pulse-anim {
          0%, 100% { opacity: 1; }
          50% { opacity: .55; }
        }
        .mai-shimmer {
          background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important;
          background-size: 200% 100% !important;
          animation: mai-shimmer-anim 1.4s linear infinite;
        }
        .mai-pulse { animation: mai-pulse-anim 1.8s ease-in-out infinite; }
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
        .mai-card-row, .mai-card-grid {
          outline: none;
          transition: transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm), box-shadow var(--dur-sm);
        }
        .mai-card-row:hover {
          transform: translateY(-1px);
          border-color: hsl(var(--c-session) / .35);
          box-shadow: 0 6px 18px hsl(var(--c-session) / .14);
        }
        .mai-card-grid:hover {
          transform: translateY(-2px);
          border-color: hsl(var(--c-session) / .35);
          box-shadow: 0 8px 22px hsl(var(--c-session) / .16);
        }
        .mai-card-row:focus-visible, .mai-card-grid:focus-visible {
          outline: 2px solid hsl(var(--c-session));
          outline-offset: 2px;
        }
        button:focus-visible, a:focus-visible, input:focus-visible {
          outline: 2px solid hsl(var(--c-session));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
