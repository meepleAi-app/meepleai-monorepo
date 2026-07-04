/* MeepleAI SP4 — Schermata /play-records · INDEX
   Route: /play-records
   File: admin-mockups/design_files/sp4-play-records-index.{html,jsx}
   Modello: sp4-sessions-index — Hero leggero + filtri sticky + list/grid.
   Entity dominante: session 🎯 (240 60% 55%).

   ── Stati canonici (G7 SessionStateRenderer, PR 2357) ──────────────
   Export per-stato (anchor #state-NN-* nell'index HTML):
     State01_Default  → state-01-default   (hub completo, lista 9 record)
     State02_Empty    → state-02-empty     (nessun record · CTA prima partita)
     State03_Loading  → state-03-loading   (skeleton primitives · aria-busy)
     State04_Error    → state-04-error     (banner alert · retry · dismiss)
   state-05-sse → SKIPPED: questo hub NON è SSE-driven (vedi App, in fondo).

   FREEZE: zero hex/hsl hardcoded per gli entity color → solo token --c-*
   via entityHsl(). Nessun asset di hosting esterno (ban #2123).
*/
const { useState, useEffect, useMemo } = React;
const DS = window.DS;

// entityHsl(entity, alpha?) — risolve SEMPRE sui token CSS (--c-*), così il
// colore segue automaticamente light/dark ([data-theme]) ed è FREEZE-clean
// (nessun valore hsl numerico hardcoded nel sorgente del mockup).
const entityHsl = (entity, alpha) =>
  alpha === undefined
    ? `hsl(var(--c-${entity}))`
    : `hsl(var(--c-${entity}) / ${alpha})`;

const RECORDS = DS.playRecords;

// ─── ConnectionChipStrip (footer card, max 3) ──────────
const ChipStrip = ({ chips }) => (
  <div style={{ display:'flex', gap: 5, flexWrap:'wrap' }}>
    {chips.map((c, i) => {
      const isEmpty = c.count === 0 || c.empty;
      return (
        <span key={i} style={{
          display:'inline-flex', alignItems:'center', gap: 3,
          padding:'2px 7px', borderRadius:'var(--r-pill)',
          background: isEmpty ? 'transparent' : entityHsl(c.entity, 0.1),
          border: isEmpty ? `1px dashed ${entityHsl(c.entity, 0.4)}` : `1px solid ${entityHsl(c.entity, 0.2)}`,
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

// ─── Outcome badge ─────────────────────────────────────
const OutcomeBadge = ({ outcome, status }) => {
  if (status === 'inprogress') {
    return (
      <span className="mai-pulse" style={{
        display:'inline-flex', alignItems:'center', gap: 4,
        padding:'2px 7px', borderRadius:'var(--r-pill)',
        background: entityHsl('session', 0.14), color: entityHsl('session'),
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
        border:`1px solid ${entityHsl('session', 0.3)}`,
      }}>
        <span aria-hidden="true" style={{ width: 6, height: 6, borderRadius:'50%', background: entityHsl('session') }}/>
        In corso
      </span>
    );
  }
  if (status === 'planned') {
    return (
      <span style={{
        display:'inline-flex', alignItems:'center', gap: 4,
        padding:'2px 7px', borderRadius:'var(--r-pill)',
        background: entityHsl('event', 0.12), color: entityHsl('event'),
        border:`1px solid ${entityHsl('event', 0.3)}`,
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
      }}>📅 Pianificata</span>
    );
  }
  const map = {
    won:  { label:'🏆 Vinta', bg:'hsl(var(--c-success) / .14)', fg:'hsl(var(--c-success))', bd:'hsl(var(--c-success) / .3)' },
    lost: { label:'△ Persa', bg:'hsl(var(--c-event) / .12)', fg:'hsl(var(--c-event))', bd:'hsl(var(--c-event) / .3)' },
    tie:  { label:'= Pareggio', bg:'var(--bg-muted)', fg:'var(--text-sec)', bd:'var(--border)' },
  };
  const m = map[outcome] || map.tie;
  return (
    <span style={{
      padding:'2px 7px', borderRadius:'var(--r-pill)',
      background: m.bg, color: m.fg, border:`1px solid ${m.bd}`,
      fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
      textTransform:'uppercase', letterSpacing:'.06em',
    }}>{m.label}</span>
  );
};

// ─── Scoring inline ────────────────────────────────────
const ScoringInline = ({ scores, compact }) => (
  <div style={{
    display:'flex', flexWrap:'wrap', gap:'3px 8px',
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
        }}>{s.score === null ? '—' : s.score}</span>
      </span>
    ))}
  </div>
);

const winnerName = (r) => {
  const w = r.scores.find(s => s.winner);
  return w ? w.name : null;
};

// ═══════════════════════════════════════════════════════
// ─── HERO ──────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const RecordsHero = ({ compact }) => {
  const stats = [
    { entity:'session', icon:'🎯', count: 89, label:'partite' },
    { entity:'toolkit', icon:'🏆', count: 47, label:'vittorie' },
    { entity:'game',    icon:'🎲', count: 9,  label:'giochi' },
    { entity:'event',   icon:'⏱', count:'142h', label:'totali' },
  ];
  return (
    <div style={{
      padding: compact ? '14px 16px' : '22px 32px',
      background:`radial-gradient(circle at 0% 0%, ${entityHsl('session', 0.14)} 0%, transparent 60%), var(--bg)`,
      borderBottom:'1px solid var(--border-light)',
      position:'relative', overflow:'hidden',
    }}>
      <div aria-hidden="true" style={{
        position:'absolute', top:-30, right:-30, width: 220, height: 220, borderRadius:'50%',
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
            background: entityHsl('session', 0.12), color: entityHsl('session'),
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 6,
            border:`1px solid ${entityHsl('session', 0.25)}`,
          }}><span aria-hidden="true">🎯</span>Play records</div>
          <h1 style={{
            fontFamily:'var(--f-display)', fontWeight: 800,
            fontSize: compact ? 24 : 34, letterSpacing:'-.02em', lineHeight: 1.05,
            margin:'0 0 3px', color:'var(--text)',
          }}>Le tue partite</h1>
          <p style={{
            fontFamily:'var(--f-body)', fontSize: compact ? 12.5 : 14,
            color:'var(--text-sec)', margin: 0, fontWeight: 500,
          }}>Storico, esiti e classifiche delle partite registrate.</p>
        </div>

        <div style={{ display:'flex', gap: compact ? 6 : 12, flexWrap:'wrap' }}>
          {stats.map(s => (
            <div key={s.label} style={{
              display:'flex', alignItems:'center', gap: 5,
              padding: compact ? '5px 8px' : '6px 10px', borderRadius:'var(--r-md)',
              background:'var(--bg-card)', border:`1px solid ${entityHsl(s.entity, 0.22)}`,
            }}>
              <span aria-hidden="true" style={{ fontSize: compact ? 12 : 13 }}>{s.icon}</span>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: compact ? 12 : 14, fontWeight: 800,
                color: entityHsl(s.entity), fontVariantNumeric:'tabular-nums',
              }}>{s.count}</span>
              <span style={{ fontFamily:'var(--f-display)', fontSize: compact ? 10.5 : 11.5, fontWeight: 700, color:'var(--text-sec)' }}>{s.label}</span>
            </div>
          ))}
        </div>

        <div style={{
          display:'flex', gap: 7, flexShrink: 0,
          width: compact ? '100%' : 'auto',
          justifyContent: compact ? 'flex-start' : 'flex-end',
        }}>
          <a href="sp4-play-records-new.html" style={{
            padding: compact ? '8px 12px' : '9px 16px', borderRadius:'var(--r-md)',
            background: entityHsl('session'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
            cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 5,
            boxShadow:`0 4px 14px ${entityHsl('session', 0.4)}`,
            flex: compact ? 1 : 'none', justifyContent:'center',
          }}><span aria-hidden="true">+</span>Registra partita</a>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FILTERS ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const StatusChip = ({ active, label, onClick, count, disabled }) => (
  <button type="button" onClick={onClick} aria-pressed={active} disabled={disabled} aria-disabled={disabled || undefined} style={{
    display:'inline-flex', alignItems:'center', gap: 5,
    padding:'6px 11px', borderRadius:'var(--r-pill)',
    background: active ? entityHsl('session', 0.14) : 'var(--bg-card)',
    border: active ? `1px solid ${entityHsl('session', 0.4)}` : '1px solid var(--border)',
    color: active ? entityHsl('session') : 'var(--text-sec)',
    fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
    cursor: disabled ? 'not-allowed' : 'pointer', whiteSpace:'nowrap', flexShrink: 0,
    opacity: disabled ? 0.5 : 1,
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

const Dropdown = ({ label, value, disabled }) => (
  <button type="button" disabled={disabled} aria-disabled={disabled || undefined} style={{
    display:'inline-flex', alignItems:'center', gap: 5,
    padding:'6px 10px', borderRadius:'var(--r-md)',
    background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text-sec)',
    fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
    cursor: disabled ? 'not-allowed' : 'pointer', whiteSpace:'nowrap', flexShrink: 0,
    opacity: disabled ? 0.5 : 1,
  }}>
    <span style={{ fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 800 }}>{label}</span>
    <span>{value}</span>
    <span aria-hidden="true" style={{ fontSize: 10, opacity: .6 }}>▾</span>
  </button>
);

const RecordFilters = ({ statusFilter, onStatusChange, view, onViewChange, search, onSearchChange, compact, counts, disabled }) => (
  <div style={{
    padding: compact ? '10px 16px' : '12px 32px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border-light)',
    display:'flex', flexDirection:'column', gap: 8,
  }} aria-disabled={disabled || undefined}>
    <div style={{ position:'relative', opacity: disabled ? 0.55 : 1 }}>
      <span aria-hidden="true" style={{ position:'absolute', left: 12, top:'50%', transform:'translateY(-50%)', color:'var(--text-muted)', fontSize: 14, pointerEvents:'none' }}>⌕</span>
      <input type="search" placeholder="Cerca partita o gioco..." value={search} disabled={disabled} onChange={e => onSearchChange && onSearchChange(e.target.value)}
        style={{
          width:'100%', padding:'8px 12px 8px 34px', borderRadius:'var(--r-md)',
          border:'1px solid var(--border)', background:'var(--bg-card)', color:'var(--text)',
          fontFamily:'var(--f-body)', fontSize: 13, cursor: disabled ? 'not-allowed' : 'text',
        }}/>
    </div>

    <div className="mai-cb-scroll" style={{ display:'flex', alignItems:'center', gap: 6, overflowX:'auto' }}>
      <StatusChip active={statusFilter==='all'} onClick={() => onStatusChange && onStatusChange('all')} label="Tutte" count={counts.all} disabled={disabled}/>
      <StatusChip active={statusFilter==='inprogress'} onClick={() => onStatusChange && onStatusChange('inprogress')} label="● In corso" count={counts.inprogress} disabled={disabled}/>
      <StatusChip active={statusFilter==='completed'} onClick={() => onStatusChange && onStatusChange('completed')} label="✓ Completate" count={counts.completed} disabled={disabled}/>
      <StatusChip active={statusFilter==='planned'} onClick={() => onStatusChange && onStatusChange('planned')} label="📅 Pianificate" count={counts.planned} disabled={disabled}/>
    </div>

    <div className="mai-cb-scroll" style={{ display:'flex', alignItems:'center', gap: 6, overflowX:'auto' }}>
      <Dropdown label="GIOCO" value="Tutti" disabled={disabled}/>
      <Dropdown label="DATA" value="Sempre" disabled={disabled}/>
      <Dropdown label="ESITO" value="Tutti" disabled={disabled}/>
      <Dropdown label="SORT" value="Data ↓" disabled={disabled}/>
      <div style={{ flex: 1 }}/>
      <div role="radiogroup" aria-label="Vista" style={{
        display:'inline-flex', borderRadius:'var(--r-md)', border:'1px solid var(--border)',
        background:'var(--bg-card)', overflow:'hidden', flexShrink: 0, opacity: disabled ? 0.5 : 1,
      }}>
        {[{ id:'list', icon:'☰', label:'List · default' }, { id:'grid', icon:'▦', label:'Grid' }].map(v => {
          const active = view === v.id;
          return (
            <button key={v.id} type="button" role="radio" aria-checked={active} disabled={disabled} onClick={() => onViewChange && onViewChange(v.id)} aria-label={v.label}
              style={{
                padding:'6px 10px',
                background: active ? entityHsl('session', 0.14) : 'transparent',
                color: active ? entityHsl('session') : 'var(--text-muted)',
                border:'none', cursor: disabled ? 'not-allowed' : 'pointer', fontSize: 13, fontWeight: 700,
              }}>{v.icon}</button>
          );
        })}
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── RECORD CARD · LIST ────────────────────────────────
// ═══════════════════════════════════════════════════════
const RecordCardList = ({ record, compact }) => {
  const game = DS.byId[record.game];
  const isInProgress = record.status === 'inprogress';
  const isPlanned = record.status === 'planned';
  const wn = winnerName(record);
  return (
    <a href="sp4-play-records-detail.html" tabIndex={0} className="mai-card-row" style={{
      position:'relative', display:'flex', alignItems:'stretch', gap: 0,
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', overflow:'hidden', cursor:'pointer',
      borderLeft: `3px solid ${entityHsl('session')}`,
      opacity: isPlanned ? 0.82 : 1, color:'inherit', textDecoration:'none',
    }}>
      <div style={{
        width: compact ? 56 : 76, background: game?.cover || entityHsl('session', 0.12),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: compact ? 24 : 32, flexShrink: 0,
      }} aria-hidden="true">
        <span style={{ filter:'drop-shadow(0 2px 4px rgba(0,0,0,.3))' }}>{game?.coverEmoji || '🎯'}</span>
      </div>

      <div style={{
        flex: 1, minWidth: 0, padding: compact ? '10px 12px' : '12px 14px',
        display:'flex', flexDirection: compact ? 'column' : 'row',
        gap: compact ? 8 : 14, alignItems: compact ? 'stretch' : 'center',
      }}>
        <div style={{ flex: compact ? 'none' : 1, minWidth: 0 }}>
          <div style={{ display:'flex', alignItems:'center', gap: 6, flexWrap:'wrap', marginBottom: 3 }}>
            <h3 style={{ fontFamily:'var(--f-display)', fontSize: compact ? 13.5 : 14.5, fontWeight: 800, color:'var(--text)', margin: 0, lineHeight: 1.2 }}>{game?.title || 'Partita'}</h3>
            <span style={{ fontFamily:'var(--f-mono)', fontSize: 10.5, color:'var(--text-muted)', fontWeight: 700 }}>· {record.date}</span>
            <OutcomeBadge outcome={record.outcome} status={record.status}/>
            {isInProgress && record.turn && (
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800, color: entityHsl('session'),
                padding:'1px 6px', borderRadius:'var(--r-pill)', background: entityHsl('session', 0.1),
                textTransform:'uppercase', letterSpacing:'.06em',
              }}>Turno {record.turn}</span>
            )}
          </div>
          <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 600, marginBottom: compact ? 6 : 8 }}>
            ⏱ {record.duration} · 👥 {record.playerCount} giocatori · {record.when}
            {wn && !isInProgress && !isPlanned && <span> · 🏆 <span style={{ color: entityHsl('session'), fontWeight:800 }}>{wn}</span></span>}
          </div>
          {!isPlanned && <ScoringInline scores={record.scores} compact={compact}/>}
          {isPlanned && <div style={{ fontFamily:'var(--f-body)', fontSize: 12, color:'var(--text-sec)', fontWeight: 500 }}>{record.scores.map(s => s.name).join(' · ')}</div>}
        </div>

        <div style={{ display:'flex', flexDirection:'column', gap: 6, alignItems: compact ? 'flex-start' : 'flex-end', flexShrink: 0 }}>
          {isInProgress && (
            <span style={{
              padding:'7px 14px', borderRadius:'var(--r-md)',
              background: entityHsl('session'), color:'#fff',
              fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 800,
              display:'inline-flex', alignItems:'center', gap: 4,
              boxShadow: `0 3px 10px ${entityHsl('session', 0.35)}`, whiteSpace:'nowrap',
            }}><span aria-hidden="true">▶</span>Riprendi</span>
          )}
          {isPlanned && (
            <span style={{
              padding:'6px 12px', borderRadius:'var(--r-md)', background:'transparent', color: entityHsl('event'),
              border:`1px solid ${entityHsl('event', 0.4)}`, fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700, whiteSpace:'nowrap',
            }}>Avvia ora</span>
          )}
          <ChipStrip chips={[
            { entity:'game', label: game?.title?.slice(0, 8) || 'gioco' },
            { entity:'player', count: record.playerCount },
            ...(record.hasChat ? [{ entity:'chat', count: record.chatCount }] : [{ entity:'chat', empty: true, count: 0 }]),
          ]}/>
        </div>
      </div>
    </a>
  );
};

// ═══════════════════════════════════════════════════════
// ─── RECORD CARD · GRID ────────────────────────────────
// ═══════════════════════════════════════════════════════
const RecordCardGrid = ({ record, compact }) => {
  const game = DS.byId[record.game];
  const isInProgress = record.status === 'inprogress';
  const isPlanned = record.status === 'planned';
  return (
    <a href="sp4-play-records-detail.html" tabIndex={0} className="mai-card-grid" style={{
      position:'relative', background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', overflow:'hidden', display:'flex', flexDirection:'column',
      cursor:'pointer', opacity: isPlanned ? 0.82 : 1, color:'inherit', textDecoration:'none',
    }}>
      <div aria-hidden="true" style={{ position:'absolute', top: 0, left: 0, right: 0, height: 3, background: entityHsl('session'), zIndex: 2 }}/>
      <div style={{
        height: compact ? 90 : 110, background: game?.cover || entityHsl('session', 0.12),
        position:'relative', display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: compact ? 36 : 44,
      }} aria-hidden="true">
        <span style={{ filter:'drop-shadow(0 2px 6px rgba(0,0,0,.3))' }}>{game?.coverEmoji || '🎯'}</span>
        <div style={{
          position:'absolute', top: 8, left: 8, display:'inline-flex', alignItems:'center', gap: 4,
          padding:'2px 7px', borderRadius:'var(--r-pill)', background:'var(--glass-bg)', backdropFilter:'blur(6px)',
          fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800, color: entityHsl('session'),
          textTransform:'uppercase', letterSpacing:'.06em',
        }}><span aria-hidden="true">🎯</span>Partita</div>
        <div style={{ position:'absolute', top: 8, right: 8 }}><OutcomeBadge outcome={record.outcome} status={record.status}/></div>
      </div>
      <div style={{ padding: 12, display:'flex', flexDirection:'column', gap: 6, flex: 1 }}>
        <div>
          <h3 style={{ fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, color:'var(--text)', margin: 0, lineHeight: 1.2 }}>{game?.title || 'Partita'}</h3>
          <div style={{ fontFamily:'var(--f-mono)', fontSize: 10.5, color:'var(--text-muted)', fontWeight: 700, marginTop: 2 }}>{record.date} · ⏱ {record.duration} · 👥 {record.playerCount}</div>
        </div>
        <div style={{ padding:'6px 8px', borderRadius:'var(--r-sm)', background:'var(--bg-muted)' }}>
          {!isPlanned ? (
            <>
              <ScoringInline scores={record.scores.slice(0, 3)} compact/>
              {record.scores.length > 3 && <div style={{ fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)', marginTop: 2, fontWeight: 700 }}>+{record.scores.length - 3} altri</div>}
            </>
          ) : (
            <div style={{ fontFamily:'var(--f-mono)', fontSize: 10.5, color:'var(--text-sec)', fontWeight: 700 }}>{record.scores.map(s => s.name).join(' · ')}</div>
          )}
        </div>
        <div style={{ flex: 1 }}/>
        <div style={{ padding:'5px 0 0', borderTop:'1px solid var(--border-light)' }}>
          <ChipStrip chips={[
            { entity:'game', label: game?.title?.slice(0, 10) || 'gioco' },
            { entity:'player', count: record.playerCount },
            ...(record.hasChat ? [{ entity:'chat', count: record.chatCount }] : [{ entity:'chat', empty: true, count: 0 }]),
          ]}/>
        </div>
      </div>
    </a>
  );
};

// ═══════════════════════════════════════════════════════
// ─── BODY (default · lista record) ─────────────────────
// ═══════════════════════════════════════════════════════
const RecordsBody = ({ initialView='list', initialFilter='all', compact }) => {
  const [view, setView] = useState(initialView);
  const [statusFilter, setStatusFilter] = useState(initialFilter);
  const [search, setSearch] = useState('');

  useEffect(() => { setView(initialView); }, [initialView]);
  useEffect(() => { setStatusFilter(initialFilter); }, [initialFilter]);

  const counts = useMemo(() => ({
    all: RECORDS.length,
    inprogress: RECORDS.filter(s => s.status === 'inprogress').length,
    completed: RECORDS.filter(s => s.status === 'completed').length,
    planned: RECORDS.filter(s => s.status === 'planned').length,
  }), []);

  const items = useMemo(() => {
    if (statusFilter === 'all') return RECORDS;
    return RECORDS.filter(s => s.status === statusFilter);
  }, [statusFilter]);

  const renderBody = () => {
    if (view === 'grid') {
      return (
        <div style={{ display:'grid', gridTemplateColumns: compact ? '1fr' : 'repeat(3, minmax(0, 1fr))', gap: 12 }}>
          {items.map(s => <RecordCardGrid key={s.id} record={s} compact={compact}/>)}
        </div>
      );
    }
    return (
      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        {items.map(s => <RecordCardList key={s.id} record={s} compact={compact}/>)}
      </div>
    );
  };

  return (
    <div style={{ flex: 1, display:'flex', flexDirection:'column', position:'relative', background:'var(--bg)', minHeight: 0 }}>
      <RecordsHero compact={compact}/>
      <div style={{ position:'sticky', top: 0, zIndex: 8 }}>
        <RecordFilters statusFilter={statusFilter} onStatusChange={setStatusFilter} view={view} onViewChange={setView} search={search} onSearchChange={setSearch} compact={compact} counts={counts}/>
      </div>
      <div style={{ flex: 1, padding: compact ? '14px 16px 32px' : '20px 32px 64px', overflowY:'auto' }}>
        <div style={{ maxWidth: view === 'list' ? 1280 : 'none', margin: '0 auto' }}>{renderBody()}</div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TOP NAV (in-app, hub chrome) ──────────────────────
// ═══════════════════════════════════════════════════════
const TopNav = ({ compact }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 14,
    padding: compact ? '9px 14px' : '10px 32px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)', borderBottom:'1px solid var(--border)',
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
      <div style={{
        width: 26, height: 26, borderRadius: 7,
        background:`linear-gradient(135deg, ${entityHsl('game')}, ${entityHsl('event')})`,
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center', fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
      }}>M</div>
      {!compact && <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>}
    </div>
    <div style={{ flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', marginLeft: compact ? 0 : 14, fontWeight: 700 }}>
      <strong style={{ color:'var(--text-sec)' }}>Play records</strong>
    </div>
  </div>
);

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── SKELETON PRIMITIVES (loading) ─────────────────────
// ═══════════════════════════════════════════════════════
const SkelRect = ({ w, h, r, style }) => (
  <div aria-hidden="true" className="skel" style={{
    width: w, height: h, borderRadius: r || 'var(--r-sm)',
    background: entityHsl('session', 0.08), flexShrink: 0, ...style,
  }}/>
);

const SkelCard = ({ compact }) => (
  <div aria-hidden="true" style={{
    display:'flex', background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)', overflow:'hidden', borderLeft:`3px solid ${entityHsl('session', 0.3)}`,
  }}>
    <div className="skel" style={{ width: compact ? 56 : 76, background: entityHsl('session', 0.08), flexShrink: 0 }}/>
    <div style={{ flex: 1, padding: compact ? '12px' : '14px', display:'flex', flexDirection:'column', gap: 8, minHeight: compact ? 78 : 86 }}>
      <SkelRect w={compact ? '60%' : 220} h={15}/>
      <SkelRect w="92%" h={11} r="var(--r-xs)"/>
      <SkelRect w="74%" h={11} r="var(--r-xs)"/>
      <div style={{ flex: 1 }}/>
      <SkelRect w={compact ? '52%' : 168} h={18} r="var(--r-pill)"/>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATO 01 · DEFAULT ────────────────────────────────
// state-01-default — hub completo: hero + filtri sticky + lista 9 record.
// ═══════════════════════════════════════════════════════
const State01_Default = ({ compact }) => (
  <div style={{ flex: 1, display:'flex', flexDirection:'column', background:'var(--bg)', minHeight: 0 }}>
    <TopNav compact={compact}/>
    <RecordsBody initialView="list" initialFilter="all" compact={compact}/>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATO 02 · EMPTY ──────────────────────────────────
// state-02-empty — nessun record. Filtri presenti ma disabled.
// role="status" per annuncio screen reader.
// ═══════════════════════════════════════════════════════
const EMPTY_COUNTS = { all: 0, inprogress: 0, completed: 0, planned: 0 };

const State02_Empty = ({ compact }) => (
  <div style={{ flex: 1, display:'flex', flexDirection:'column', background:'var(--bg)', minHeight: 0 }}>
    <TopNav compact={compact}/>
    <div style={{ position:'sticky', top: 0, zIndex: 8 }}>
      <RecordFilters statusFilter="all" view="list" search="" compact={compact} counts={EMPTY_COUNTS} disabled/>
    </div>
    <div style={{ flex: 1, padding: compact ? '24px 16px 32px' : '48px 32px 64px', display:'flex', alignItems:'flex-start', justifyContent:'center', overflowY:'auto' }}>
      <div role="status" aria-live="polite" style={{
        width:'100%', maxWidth: 440, padding: compact ? '36px 22px' : '48px 28px', textAlign:'center',
        background:'var(--bg-card)', border:'1px dashed var(--border-strong)', borderRadius:'var(--r-xl)',
        display:'flex', flexDirection:'column', alignItems:'center',
      }}>
        <div aria-hidden="true" style={{
          width: 96, height: 96, borderRadius:'50%',
          background:`radial-gradient(circle, ${entityHsl('session', 0.18)} 0%, transparent 70%)`,
          display:'flex', alignItems:'center', justifyContent:'center', fontSize: 46, marginBottom: 16,
        }}>🎲</div>
        <h2 style={{ fontFamily:'var(--f-display)', fontSize: compact ? 18 : 20, fontWeight: 800, color:'var(--text)', margin:'0 0 8px' }}>Nessuna partita registrata</h2>
        <p style={{ fontFamily:'var(--f-body)', fontSize: compact ? 13 : 13.5, color:'var(--text-sec)', margin:'0 0 20px', maxWidth: 360, lineHeight: 1.55, fontWeight: 500 }}>
          Inizia a tracciare le tue serate per costruire la cronologia del gruppo
        </p>
        <a href="sp4-play-records-new.html" style={{
          padding:'10px 18px', borderRadius:'var(--r-md)', background: entityHsl('session'), color:'#fff',
          fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800,
          display:'inline-flex', alignItems:'center', gap: 6,
          boxShadow:`0 4px 14px ${entityHsl('session', 0.4)}`,
        }}><span aria-hidden="true">+</span>Registra prima partita</a>
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATO 03 · LOADING ────────────────────────────────
// state-03-loading — skeleton primitives durante fetch iniziale.
// aria-busy sul wrapper, role="status" sulla lista, skeleton aria-hidden.
// Pulse 0.4→0.8→0.4 (2s) via .skel; prefers-reduced-motion → snap.
// ═══════════════════════════════════════════════════════
const State03_Loading = ({ compact }) => (
  <div aria-busy="true" style={{ flex: 1, display:'flex', flexDirection:'column', background:'var(--bg)', minHeight: 0 }}>
    <TopNav compact={compact}/>

    {/* Header skeleton (title placeholder) */}
    <div style={{
      padding: compact ? '14px 16px' : '22px 32px', borderBottom:'1px solid var(--border-light)',
      display:'flex', flexDirection:'column', gap: 10,
      background:`radial-gradient(circle at 0% 0%, ${entityHsl('session', 0.08)} 0%, transparent 60%), var(--bg)`,
    }}>
      <SkelRect w={compact ? 110 : 130} h={16} r="var(--r-pill)"/>
      <SkelRect w={compact ? '64%' : 280} h={compact ? 26 : 34} r="var(--r-md)"/>
      <SkelRect w={compact ? '88%' : 360} h={13}/>
    </div>

    {/* Filter chips skeleton (4 chip rect) */}
    <div style={{
      padding: compact ? '10px 16px' : '12px 32px', borderBottom:'1px solid var(--border-light)',
      background:'var(--glass-bg)', display:'flex', flexDirection:'column', gap: 8,
    }}>
      <SkelRect w="100%" h={34} r="var(--r-md)"/>
      <div style={{ display:'flex', gap: 6 }}>
        {[0,1,2,3].map(i => <SkelRect key={i} w={compact ? 66 : 92} h={30} r="var(--r-pill)"/>)}
      </div>
    </div>

    {/* Lista 6 card skeleton */}
    <div role="status" aria-live="polite" style={{ flex: 1, padding: compact ? '14px 16px 32px' : '20px 32px 64px', overflowY:'auto' }}>
      <span style={{ position:'absolute', width: 1, height: 1, padding: 0, margin: -1, overflow:'hidden', clip:'rect(0 0 0 0)', whiteSpace:'nowrap', border: 0 }}>
        Caricamento partite in corso…
      </span>
      <div style={{ maxWidth: 1280, margin:'0 auto', display:'flex', flexDirection:'column', gap: 8 }}>
        {[0,1,2,3,4,5].map(i => <SkelCard key={i} compact={compact}/>)}
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATO 04 · ERROR ──────────────────────────────────
// state-04-error — banner full-width (role="alert") + retry + hub vuoto + dismiss.
// ═══════════════════════════════════════════════════════
const State04_Error = ({ compact }) => (
  <div style={{ flex: 1, display:'flex', flexDirection:'column', background:'var(--bg)', minHeight: 0 }}>
    <TopNav compact={compact}/>

    {/* Banner errore full-width */}
    <div role="alert" style={{
      display:'flex', alignItems:'center', gap: compact ? 10 : 14,
      padding: compact ? '12px 16px' : '14px 32px',
      background: entityHsl('event', 0.08),
      borderLeft: `4px solid ${entityHsl('event', 0.6)}`,
      borderBottom:'1px solid var(--border-light)',
    }}>
      <span aria-hidden="true" style={{ fontSize: compact ? 18 : 20, lineHeight: 1 }}>⚠️</span>
      <div style={{ flex: 1, minWidth: 0, fontFamily:'var(--f-display)', fontSize: compact ? 13 : 14.5, fontWeight: 800, color:'var(--text)' }}>
        Impossibile caricare le partite <span style={{ color: entityHsl('event'), fontWeight: 700 }}>· Riprova</span>
      </div>
      <button type="button" aria-label="Riprova caricamento partite" style={{
        padding:'7px 14px', borderRadius:'var(--r-md)', background:'transparent',
        color: entityHsl('event'), border:`1px solid ${entityHsl('event', 0.5)}`,
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
        display:'inline-flex', alignItems:'center', gap: 5, whiteSpace:'nowrap', flexShrink: 0,
      }}><span aria-hidden="true">↻</span>Riprova</button>
    </div>

    {/* Hub vuoto (nessuna lista renderizzata) */}
    <div style={{ flex: 1, padding: compact ? '28px 16px' : '56px 32px', display:'flex', alignItems:'flex-start', justifyContent:'center', overflowY:'auto' }}>
      <div style={{
        display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
        color:'var(--text-muted)', maxWidth: 360,
      }}>
        <div aria-hidden="true" style={{
          width: 54, height: 54, borderRadius:'50%', background:'var(--bg-muted)',
          display:'flex', alignItems:'center', justifyContent:'center', fontSize: 24, marginBottom: 12,
        }}>🃏</div>
        <div style={{ fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700, color:'var(--text-sec)', marginBottom: 4 }}>Nessun dato disponibile</div>
        <p style={{ fontFamily:'var(--f-body)', fontSize: 12.5, color:'var(--text-muted)', margin: 0, lineHeight: 1.5 }}>Le partite verranno mostrate qui una volta ripristinata la connessione.</p>
      </div>
    </div>

    {/* Footer dismiss */}
    <div style={{ padding: compact ? '12px 16px' : '14px 32px', borderTop:'1px solid var(--border-light)', display:'flex', justifyContent:'center' }}>
      <button type="button" style={{
        background:'transparent', border:'none', color:'var(--text-muted)',
        fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700, cursor:'pointer',
        textDecoration:'underline', textUnderlineOffset: 3,
      }}>Chiudi</button>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATO 05 · SSE — SKIPPED ──────────────────────────
// Questo hub NON è SSE-driven: la lista /play-records è fetch-once, non
// streaming. Nessun State05_SSE renderizzato (cfr. G7 SessionStateRenderer:
// lo stato `sse` si applica solo agli hub con sottoscrizione eventi live).
// ═══════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════
// ─── GALLERY (index) — frames + nav + sections ─────────
// ═══════════════════════════════════════════════════════
const MobileFrame = ({ children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 8 }}>
    <div className="frame-tag">Mobile · 375</div>
    <div className="phone">
      <PhoneSbar/>
      <div style={{ flex: 1, overflow:'hidden', display:'flex', flexDirection:'column', background:'var(--bg)' }}>{children}</div>
    </div>
  </div>
);

const DesktopFrame = ({ children }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 8, flex: 1, minWidth: 0 }}>
    <div className="frame-tag">Desktop · 1440</div>
    <div className="desktop-frame">
      <div className="desktop-bar">
        <span className="traffic"/><span className="traffic"/><span className="traffic"/>
        <span className="url">meepleai.app/play-records</span>
      </div>
      <div style={{ display:'flex', flexDirection:'column', minHeight: 660, background:'var(--bg)' }}>{children}</div>
    </div>
  </div>
);

// 2 viewport per stato (Mobile 375 + Desktop 1440). Il tema light/dark si applica
// GLOBALMENTE via <html data-theme="light|dark"> (toggle in nav) — i token dark di
// tokens.css sono scoping su :root, quindi NIENTE wrapper annidati. NO file separati.
const StateMatrix = ({ Comp }) => (
  <div className="matrix">
    <div className="matrix-row">
      <MobileFrame><Comp compact/></MobileFrame>
      <DesktopFrame><Comp/></DesktopFrame>
    </div>
  </div>
);

const STATES = [
  { id:'state-01-default', num:'01', title:'Default', sub:'Hub completo: hero stats + filtri sticky + lista 9 record (completate / in corso / pianificate). Stato base, invariato.', Comp: State01_Default },
  { id:'state-02-empty',   num:'02', title:'Empty',   sub:'Nessuna partita registrata. Filtri presenti ma disabled, illustrazione 🎲 + CTA "Registra prima partita". role="status".', Comp: State02_Empty },
  { id:'state-03-loading', num:'03', title:'Loading', sub:'Fetch iniziale: skeleton header + 4 chip + 6 card (pulse 2s, snap con reduced-motion). aria-busy + role="status".', Comp: State03_Loading },
  { id:'state-04-error',   num:'04', title:'Error',   sub:'Banner full-width (role="alert") con retry inline, hub vuoto sotto, link "Chiudi" in footer.', Comp: State04_Error },
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
      <div className="gallery-nav-brand"><span aria-hidden="true">🎯</span> SP4 · /play-records</div>
      <div className="gallery-nav-links">
        {NAV.map(n => <a key={n.id} href={`#${n.id}`}>{n.label}</a>)}
      </div>
      <a className="gallery-nav-ghost" href="#state-05-sse-skipped" aria-disabled="true" title="state-05-sse: skipped — hub non SSE-driven">05 · SSE · skip</a>
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
          <div className="kicker">SP4 · /play-records · Index 🎯 — canonical states</div>
          <h1>Play records — Stati canonici</h1>
          <p className="lead">
            Hub lista partite registrate allineato al pattern <strong>G7 SessionStateRenderer</strong> (PR 2357).
            4 stati canonici × viewport mobile&nbsp;375 / desktop&nbsp;1440 (8 frame), × tema light/dark via toggle = 16 combinazioni.
            Entity dominante <strong>session 🎯</strong>; colori esclusivamente da token <code>--c-*</code> via <code>entityHsl()</code>.
            Lo stato <code>state-05-sse</code> è intenzionalmente <strong>saltato</strong> (hub non SSE-driven).
          </p>
        </header>

        {STATES.map(s => <StateSection key={s.id} {...s}/>)}
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
