/* MeepleAI SP4 — Schermata /play-records · INDEX
   Route: /play-records
   File: admin-mockups/design_files/sp4-play-records-index.{html,jsx}
   Modello: sp4-sessions-index — Hero leggero + filtri sticky + list/grid.
   Entity dominante: session 🎯 (240 60% 55%).

   Lista partite registrate: search + filtri (gioco / stato / data) + sort.
   Card partita: gioco, data, n° giocatori, esito (completata/in corso/pianificata), vincitore.
*/
const { useState, useEffect, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.session;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

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
    won:  { label:'🏆 Vinta', bg:'hsl(142 60% 45% / .14)', fg:'hsl(142 50% 32%)', bd:'hsl(142 60% 45% / .3)' },
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
const StatusChip = ({ active, label, onClick, count }) => (
  <button type="button" onClick={onClick} aria-pressed={active} style={{
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
    background:'var(--bg-card)', border:'1px solid var(--border)', color:'var(--text-sec)',
    fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
    cursor:'pointer', whiteSpace:'nowrap', flexShrink: 0,
  }}>
    <span style={{ fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 800 }}>{label}</span>
    <span>{value}</span>
    <span aria-hidden="true" style={{ fontSize: 10, opacity: .6 }}>▾</span>
  </button>
);

const RecordFilters = ({ statusFilter, onStatusChange, view, onViewChange, search, onSearchChange, compact, counts }) => (
  <div style={{
    padding: compact ? '10px 16px' : '12px 32px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border-light)',
    display:'flex', flexDirection:'column', gap: 8,
  }}>
    <div style={{ position:'relative' }}>
      <span aria-hidden="true" style={{ position:'absolute', left: 12, top:'50%', transform:'translateY(-50%)', color:'var(--text-muted)', fontSize: 14, pointerEvents:'none' }}>⌕</span>
      <input type="search" placeholder="Cerca partita o gioco..." value={search} onChange={e => onSearchChange(e.target.value)}
        style={{
          width:'100%', padding:'8px 12px 8px 34px', borderRadius:'var(--r-md)',
          border:'1px solid var(--border)', background:'var(--bg-card)', color:'var(--text)',
          fontFamily:'var(--f-body)', fontSize: 13,
        }}/>
    </div>

    <div className="mai-cb-scroll" style={{ display:'flex', alignItems:'center', gap: 6, overflowX:'auto' }}>
      <StatusChip active={statusFilter==='all'} onClick={() => onStatusChange('all')} label="Tutte" count={counts.all}/>
      <StatusChip active={statusFilter==='inprogress'} onClick={() => onStatusChange('inprogress')} label="● In corso" count={counts.inprogress}/>
      <StatusChip active={statusFilter==='completed'} onClick={() => onStatusChange('completed')} label="✓ Completate" count={counts.completed}/>
      <StatusChip active={statusFilter==='planned'} onClick={() => onStatusChange('planned')} label="📅 Pianificate" count={counts.planned}/>
    </div>

    <div className="mai-cb-scroll" style={{ display:'flex', alignItems:'center', gap: 6, overflowX:'auto' }}>
      <Dropdown label="GIOCO" value="Tutti"/>
      <Dropdown label="DATA" value="Sempre"/>
      <Dropdown label="ESITO" value="Tutti"/>
      <Dropdown label="SORT" value="Data ↓"/>
      <div style={{ flex: 1 }}/>
      <div role="radiogroup" aria-label="Vista" style={{
        display:'inline-flex', borderRadius:'var(--r-md)', border:'1px solid var(--border)',
        background:'var(--bg-card)', overflow:'hidden', flexShrink: 0,
      }}>
        {[{ id:'list', icon:'☰', label:'List · default' }, { id:'grid', icon:'▦', label:'Grid' }].map(v => {
          const active = view === v.id;
          return (
            <button key={v.id} type="button" role="radio" aria-checked={active} onClick={() => onViewChange(v.id)} aria-label={v.label}
              style={{
                padding:'6px 10px',
                background: active ? entityHsl('session', 0.14) : 'transparent',
                color: active ? entityHsl('session') : 'var(--text-muted)',
                border:'none', cursor:'pointer', fontSize: 13, fontWeight: 700,
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
          padding:'2px 7px', borderRadius:'var(--r-pill)', background:'rgba(255,255,255,.85)', backdropFilter:'blur(6px)',
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
// ─── EMPTY / LOADING / ERROR ───────────────────────────
// ═══════════════════════════════════════════════════════
const EmptyState = ({ kind }) => {
  if (kind === 'first-run') {
    return (
      <div style={{
        padding:'48px 24px', textAlign:'center', background:'var(--bg-card)',
        border:'1px dashed var(--border-strong)', borderRadius:'var(--r-xl)',
        display:'flex', flexDirection:'column', alignItems:'center',
      }}>
        <div style={{
          width: 92, height: 92, borderRadius:'50%',
          background:`radial-gradient(circle, ${entityHsl('session', 0.18)} 0%, transparent 70%)`,
          display:'flex', alignItems:'center', justifyContent:'center', fontSize: 42, marginBottom: 14,
        }} aria-hidden="true">🎯</div>
        <h2 style={{ fontFamily:'var(--f-display)', fontSize: 19, fontWeight: 800, color:'var(--text)', margin:'0 0 6px' }}>Nessuna partita registrata</h2>
        <p style={{ fontSize: 13, color:'var(--text-sec)', margin:'0 0 18px', maxWidth: 360, lineHeight: 1.5 }}>Registra la tua prima partita per tracciare punteggi, esiti e classifiche del gruppo.</p>
        <a href="sp4-play-records-new.html" style={{
          padding:'9px 16px', borderRadius:'var(--r-md)', background: entityHsl('session'), color:'#fff',
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, boxShadow:`0 4px 14px ${entityHsl('session', 0.4)}`,
        }}>+ Registra prima partita</a>
      </div>
    );
  }
  return (
    <div style={{
      padding:'40px 24px', textAlign:'center', background:'var(--bg-card)',
      border:'1px dashed var(--border-strong)', borderRadius:'var(--r-xl)',
      display:'flex', flexDirection:'column', alignItems:'center',
    }}>
      <div style={{
        width: 56, height: 56, borderRadius:'50%', background: entityHsl('session', 0.12), color: entityHsl('session'),
        display:'flex', alignItems:'center', justifyContent:'center', fontSize: 24, marginBottom: 12,
      }} aria-hidden="true">⌕</div>
      <h3 style={{ fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)', margin:'0 0 4px' }}>Nessuna partita per questi filtri</h3>
      <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 320 }}>Prova a rimuovere alcuni vincoli o cambiare periodo.</p>
      <button type="button" style={{
        padding:'7px 14px', borderRadius:'var(--r-md)', background:'transparent', color: entityHsl('session'),
        border:`1px solid ${entityHsl('session', 0.4)}`, fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
      }}>↻ Reset filtri</button>
    </div>
  );
};

const LoadingList = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
    {[0,1,2,3,4].map(i => <div key={i} className="mai-shimmer" style={{ height: 92, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>)}
  </div>
);

const ErrorState = () => (
  <div style={{
    padding:'40px 24px', textAlign:'center', background:'hsl(var(--c-danger) / .06)',
    border:'1px solid hsl(var(--c-danger) / .25)', borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center',
  }}>
    <div style={{
      width: 56, height: 56, borderRadius:'50%', background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))',
      display:'flex', alignItems:'center', justifyContent:'center', fontSize: 26, marginBottom: 12,
    }} aria-hidden="true">⚠</div>
    <h3 style={{ fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)', margin:'0 0 4px' }}>Errore caricamento</h3>
    <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 12px', maxWidth: 320 }}>Impossibile recuperare le partite. Verifica la connessione.</p>
    <button type="button" style={{
      padding:'7px 14px', borderRadius:'var(--r-md)', background:'hsl(var(--c-danger))', color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
    }}>↻ Riprova</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── BODY ──────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const RecordsBody = ({ stateOverride, initialView='list', initialFilter='all', compact }) => {
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
    if (stateOverride === 'loading') return <LoadingList/>;
    if (stateOverride === 'error') return <ErrorState/>;
    if (stateOverride === 'empty-first-run') return <EmptyState kind="first-run"/>;
    if (stateOverride === 'empty-filter') return <EmptyState kind="no-results"/>;

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
// ─── FRAMES ────────────────────────────────────────────
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
        background:'linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-event)))',
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

const PhoneScreen = (props) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflow:'hidden', display:'flex', flexDirection:'column', position:'relative', background:'var(--bg)' }}>
      <TopNav compact/>
      <RecordsBody {...props} compact/>
    </div>
  </>
);

const DesktopFrameInner = (props) => (
  <div style={{ display:'flex', flexDirection:'column', minHeight: 720, background:'var(--bg)' }}>
    <TopNav/>
    <RecordsBody {...props}/>
  </div>
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
      <div className="desktop-bar">
        <span className="traffic"/><span className="traffic"/><span className="traffic"/>
        <span className="url">meepleai.app/play-records</span>
      </div>
      {children}
    </div>
    {desc && <div style={{ fontSize: 11, color:'var(--text-muted)', maxWidth: 720, textAlign:'center', lineHeight: 1.55 }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id:'m1', view:'list', filter:'all', label:'01 · List · default', desc:'Hero compatto + filter chips. Card list con cover gioco + scoring inline + outcome badge + vincitore + ChipStrip.' },
  { id:'m2', view:'list', filter:'inprogress', label:'02 · In corso', desc:'Solo partite live. Badge "In corso" pulsante + turno + CTA Riprendi.' },
  { id:'m3', view:'grid', filter:'all', label:'03 · Grid view', desc:'Cover prominente, outcome top-right, scoring boxed, ChipStrip footer.' },
  { id:'m4', view:'list', filter:'planned', label:'04 · Pianificate', desc:'Card opacity ridotta + badge "Pianificata" entity-event + CTA "Avvia ora".' },
  { id:'m5', view:'list', state:'empty-first-run', label:'05 · Empty first-run', desc:'Illustrazione + CTA "Registra prima partita".' },
  { id:'m6', view:'list', state:'empty-filter', label:'06 · Empty filtri', desc:'Nessun risultato per i filtri attivi + Reset.' },
  { id:'m7', view:'list', state:'loading', label:'07 · Loading', desc:'Hero + filtri + 5 skeleton shimmer 92px.' },
  { id:'m8', view:'list', state:'error', label:'08 · Error', desc:'Errore con CTA Riprova in danger color.' },
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
        <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8 }}>SP4 · /play-records · Index 🎯</div>
        <h1>Play records — /play-records</h1>
        <p className="lead">
          Lista partite registrate. Hero stats inline · status chips (Tutte / In corso / Completate / Pianificate) + dropdown
          <strong> gioco · data · esito · sort</strong> + search · view toggle List default / Grid. Card con cover, data, n° giocatori,
          esito (Vinta/Persa/Pareggio/In corso/Pianificata) e <strong>vincitore</strong> evidenziato.
        </p>

        <div className="section-label">Mobile · 375 — 8 stati</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <PhoneScreen stateOverride={s.state || null} initialView={s.view} initialFilter={s.filter || 'all'}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1280 — 4 stati chiave</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="09 · Desktop · List · default" desc="Container 1280. Card row complete: cover 76 + titolo+data+esito+vincitore + meta + scoring inline + ChipStrip.">
            <DesktopFrameInner stateOverride={null} initialView="list" initialFilter="all"/>
          </DesktopFrame>
          <DesktopFrame label="10 · Desktop · Grid · 3-col" desc="Vista alternativa cover-first.">
            <DesktopFrameInner stateOverride={null} initialView="grid" initialFilter="all"/>
          </DesktopFrame>
          <DesktopFrame label="11 · Desktop · Dark mode" dark desc="Verifica contrasti e accent entity-session in dark.">
            <DesktopFrameInner stateOverride={null} initialView="list" initialFilter="all"/>
          </DesktopFrame>
          <DesktopFrame label="12 · Desktop · Empty first-run" desc="Hero + filtri + illustrazione 92 entity-session + CTA Registra.">
            <DesktopFrameInner stateOverride="empty-first-run" initialView="list" initialFilter="all"/>
          </DesktopFrame>
        </div>
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
