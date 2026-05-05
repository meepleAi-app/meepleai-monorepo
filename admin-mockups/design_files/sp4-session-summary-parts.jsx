/* MeepleAI SP4 wave 2 — Schermata #3/3 FINALE — parts
   Hero podio + ConnectionBar + KPI + Scoring breakdown
*/
const { useState: uS, useEffect: uE, useMemo: uM } = React;
const DSx = window.DS;
const eH = (t, a) => {
  const c = DSx.EC[t] || DSx.EC.session;
  return a !== undefined ? `hsla(${c.h},${c.s}%,${c.l}%,${a})` : `hsl(${c.h},${c.s}%,${c.l}%)`;
};

// Final scores (Wingspan partita di Marco)
const FINAL = [
  { id:'p-marco', name:'Marco', color: 262, score: 89, position: 1,
    breakdown: { eggs: 24, birds: 31, habitat: 18, bonus: 16 } },
  { id:'p-anna',  name:'Anna',  color: 320, score: 79, position: 2,
    breakdown: { eggs: 21, birds: 28, habitat: 22, bonus: 8 } },
  { id:'p-luca',  name:'Luca',  color: 180, score: 64, position: 3,
    breakdown: { eggs: 18, birds: 22, habitat: 16, bonus: 8 } },
  { id:'p-sara',  name:'Sara',  color: 100, score: 58, position: 4,
    breakdown: { eggs: 15, birds: 20, habitat: 14, bonus: 9 } },
];

// ─── Confetti (CSS-only static + first-load anim) ────
const Confetti = () => (
  <div aria-hidden="true" className="mai-confetti" style={{
    position:'absolute', inset: 0, pointerEvents:'none', overflow:'hidden',
    zIndex: 1,
  }}>
    {Array.from({ length: 24 }).map((_, i) => {
      const colors = ['session','toolkit','game','agent','chat','event'];
      const c = colors[i % colors.length];
      const left = (i * 4.17) % 100;
      const delay = (i * 0.13) % 2.4;
      const top = (i * 7.3) % 50;
      const rot = (i * 31) % 360;
      return (
        <span key={i} className="mai-confetti-piece" style={{
          position:'absolute', left:`${left}%`, top:`${top}%`,
          width: 8, height: 12,
          background: eH(c, 0.85),
          borderRadius: 2,
          transform: `rotate(${rot}deg)`,
          animationDelay: `${delay}s`,
        }}/>
      );
    })}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── HERO PODIO ──────────────────────────────────────
// /* v2: SummaryHeroPodium */
// ═══════════════════════════════════════════════════════
const PodiumPlace = ({ p, place, compact, tied }) => {
  const sizes = {
    1: { avatar: compact ? 64 : 92, height: compact ? 92 : 130, score: compact ? 26 : 40, crown: '🏆', medal: '' },
    2: { avatar: compact ? 52 : 72, height: compact ? 64 : 92,  score: compact ? 19 : 28, medal: '🥈' },
    3: { avatar: compact ? 52 : 72, height: compact ? 50 : 72,  score: compact ? 19 : 28, medal: '🥉' },
  };
  const s = sizes[place];
  const isWinner = place === 1;
  return (
    <div style={{
      display:'flex', flexDirection:'column', alignItems:'center', gap: 6,
      flex: 1, minWidth: 0, position:'relative', zIndex: 2,
    }}>
      {/* Crown over avatar */}
      {isWinner && (
        <div style={{
          fontSize: compact ? 22 : 32, marginBottom: -4, lineHeight: 1,
          filter:`drop-shadow(0 0 12px ${eH('toolkit', 0.6)})`,
        }} aria-hidden="true">{s.crown}</div>
      )}
      <div style={{
        width: s.avatar, height: s.avatar, borderRadius:'50%',
        background:`linear-gradient(135deg, hsl(${p.color},70%,65%), hsl(${p.color},60%,42%))`,
        color:'#fff',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontFamily:'var(--f-display)', fontSize: compact ? 22 : 32, fontWeight: 800,
        border: isWinner ? `3px solid ${eH('toolkit')}` : '2px solid var(--bg-card)',
        boxShadow: isWinner
          ? `0 0 30px ${eH('toolkit', 0.4)}, 0 4px 14px ${eH('toolkit', 0.3)}`
          : `0 4px 12px rgba(0,0,0,.18)`,
        position:'relative',
      }}>
        {p.name[0]}
        {!isWinner && (
          <div style={{
            position:'absolute', bottom: -3, right: -3,
            fontSize: compact ? 16 : 22, lineHeight: 1,
          }} aria-hidden="true">{s.medal}</div>
        )}
        {tied && isWinner && (
          <div style={{
            position:'absolute', top: -3, right: -3,
            fontSize: compact ? 13 : 18,
            background: eH('toolkit'), color:'#fff',
            width: compact ? 18 : 24, height: compact ? 18 : 24, borderRadius:'50%',
            display:'flex', alignItems:'center', justifyContent:'center',
            border:'2px solid var(--bg-card)',
            fontFamily:'var(--f-display)', fontWeight: 800,
            fontSize: compact ? 9 : 11,
          }}>T</div>
        )}
      </div>
      <div style={{
        fontFamily:'var(--f-display)', fontSize: compact ? 12 : 14, fontWeight: 800,
        color:'var(--text)', textAlign:'center',
      }}>{p.name}</div>
      <div style={{
        fontFamily:'var(--f-display)',
        fontSize: s.score, fontWeight: 800,
        color: isWinner ? eH('toolkit') : 'var(--text)',
        fontVariantNumeric:'tabular-nums', lineHeight: 1,
        letterSpacing:'-.02em',
        textShadow: isWinner ? `0 0 16px ${eH('toolkit', 0.4)}` : 'none',
      }}>{p.score}</div>
      {/* Pedestal */}
      <div style={{
        marginTop: 4, width:'88%',
        height: s.height,
        background: isWinner
          ? `linear-gradient(180deg, ${eH('toolkit', 0.18)}, ${eH('toolkit', 0.08)})`
          : 'linear-gradient(180deg, var(--bg-muted), var(--bg-hover))',
        borderTop: isWinner ? `3px solid ${eH('toolkit')}` : '1px solid var(--border)',
        borderLeft:'1px solid var(--border)',
        borderRight:'1px solid var(--border)',
        borderRadius:'var(--r-md) var(--r-md) 0 0',
        display:'flex', alignItems:'center', justifyContent:'center',
        fontFamily:'var(--f-display)', fontSize: compact ? 18 : 28, fontWeight: 800,
        color: isWinner ? eH('toolkit') : 'var(--text-muted)',
      }}>{place}°</div>
    </div>
  );
};

const SummaryHeroPodium = ({ variant = 'default', compact }) => {
  // variant: 'default' | 'tied' | 'abandoned' | 'solo'
  if (variant === 'abandoned') {
    return (
      <div style={{
        position:'relative',
        padding: compact ? '20px 16px 22px' : '32px 32px 36px',
        background:`radial-gradient(circle at 50% 0%, ${eH('event', 0.18)} 0%, transparent 60%)`,
        borderBottom:`1px solid ${eH('event', 0.25)}`,
        textAlign:'center',
      }}>
        <div style={{
          display:'inline-flex', alignItems:'center', gap: 5,
          padding:'3px 10px', borderRadius:'var(--r-pill)',
          background: eH('event', 0.14),
          border:`1px solid ${eH('event', 0.3)}`,
          color: eH('event'),
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.1em',
          marginBottom: 12,
        }}>
          <span aria-hidden="true">⏸</span>Abbandonata
        </div>
        <h1 style={{
          fontFamily:'var(--f-display)', fontSize: compact ? 22 : 32, fontWeight: 800,
          color:'var(--text)', margin:'0 0 6px', letterSpacing:'-.02em',
        }}>Partita interrotta al turno 12/18</h1>
        <p style={{
          fontFamily:'var(--f-body)', fontSize: 13, color:'var(--text-sec)',
          margin:'0 0 16px', maxWidth: 480, marginLeft:'auto', marginRight:'auto',
        }}>La sessione è stata abbandonata. Puoi riprendere se è ancora possibile, oppure archiviarla come incompleta.</p>
        <div style={{ display:'inline-flex', gap: 7 }}>
          <button type="button" style={{
            padding:'9px 16px', borderRadius:'var(--r-md)',
            background: eH('session'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
            cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 5,
          }}><span aria-hidden="true">▶</span>Riprendi se possibile</button>
          <button type="button" style={{
            padding:'9px 14px', borderRadius:'var(--r-md)',
            background:'transparent', color:'var(--text-sec)',
            border:'1px solid var(--border-strong)',
            fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700,
            cursor:'pointer',
          }}>Archivia incompleta</button>
        </div>
      </div>
    );
  }

  const order = variant === 'tied'
    ? [FINAL[0], FINAL[1], FINAL[2]] // 1° tied = Marco + Anna both at top
    : [FINAL[1], FINAL[0], FINAL[2]];

  const soloOnly = variant === 'solo';

  return (
    <div style={{
      position:'relative', overflow:'hidden',
      padding: compact ? '14px 12px 18px' : '24px 32px 30px',
      background: `radial-gradient(ellipse at 50% 0%, ${eH('session', 0.2)} 0%, transparent 60%), linear-gradient(180deg, ${eH('toolkit', 0.04)} 0%, transparent 100%)`,
      borderBottom:`1px solid ${eH('session', 0.2)}`,
    }}>
      <Confetti/>
      {/* Action bar top right */}
      <div style={{
        position:'absolute', top: compact ? 10 : 16, right: compact ? 10 : 16,
        display:'flex', gap: 5, zIndex: 3,
      }}>
        <button type="button" style={{
          padding: compact ? '6px 9px' : '7px 12px',
          borderRadius:'var(--r-md)',
          background: eH('session'), color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 800,
          cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 4,
          boxShadow:`0 3px 10px ${eH('session', 0.35)}`,
        }}><span aria-hidden="true">📤</span>{!compact && 'Condividi'}</button>
        <button type="button" style={{
          padding: compact ? '6px 9px' : '7px 12px',
          borderRadius:'var(--r-md)',
          background:'var(--bg-card)', color:'var(--text-sec)',
          border:'1px solid var(--border)',
          fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
          cursor:'pointer', display:'inline-flex', alignItems:'center', gap: 4,
        }}><span aria-hidden="true">📷</span>{!compact && 'Foto'}</button>
        <button type="button" aria-label="Menu" style={{
          width: compact ? 30 : 34, height: compact ? 30 : 34,
          borderRadius:'var(--r-md)',
          background:'var(--bg-card)', border:'1px solid var(--border)',
          color:'var(--text-sec)', cursor:'pointer',
          fontSize: 14, fontWeight: 800,
        }}>⋯</button>
      </div>

      {/* Title */}
      <div style={{
        textAlign:'center', position:'relative', zIndex: 2,
        marginTop: compact ? 30 : 0,
      }}>
        <div style={{
          display:'inline-flex', alignItems:'center', gap: 6,
          padding:'3px 10px', borderRadius:'var(--r-pill)',
          background: eH('toolkit', 0.14),
          border:`1px solid ${eH('toolkit', 0.3)}`,
          color: eH('toolkit'),
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.12em',
          marginBottom: 6,
        }}>
          {variant === 'tied'
            ? <><span aria-hidden="true">🤝</span>Pareggio</>
            : variant === 'solo'
              ? <><span aria-hidden="true">🎯</span>Solo run</>
              : <><span aria-hidden="true">🏆</span>{FINAL[0].name} è il vincitore</>}
        </div>
      </div>

      {/* Podio */}
      {!soloOnly && (
        <div style={{
          position:'relative', zIndex: 2,
          display:'flex', alignItems:'flex-end', justifyContent:'center',
          gap: compact ? 4 : 12,
          maxWidth: compact ? '100%' : 600, margin:'10px auto 0',
        }}>
          {variant === 'tied' ? (
            <>
              <PodiumPlace p={FINAL[0]} place={1} compact tied/>
              <PodiumPlace p={FINAL[1]} place={1} compact tied/>
              <PodiumPlace p={FINAL[2]} place={3} compact={compact}/>
            </>
          ) : (
            <>
              <PodiumPlace p={order[0]} place={2} compact={compact}/>
              <PodiumPlace p={order[1]} place={1} compact={compact}/>
              <PodiumPlace p={order[2]} place={3} compact={compact}/>
            </>
          )}
        </div>
      )}

      {/* Solo */}
      {soloOnly && (
        <div style={{
          position:'relative', zIndex: 2, marginTop: 14,
          display:'flex', flexDirection:'column', alignItems:'center', gap: 4,
        }}>
          <div style={{ fontSize: compact ? 22 : 32, marginBottom: -2, filter:`drop-shadow(0 0 12px ${eH('toolkit', 0.6)})` }} aria-hidden="true">🏆</div>
          <div style={{
            width: compact ? 88 : 120, height: compact ? 88 : 120, borderRadius:'50%',
            background:`linear-gradient(135deg, hsl(262,70%,65%), hsl(262,60%,42%))`,
            color:'#fff',
            display:'flex', alignItems:'center', justifyContent:'center',
            fontFamily:'var(--f-display)', fontSize: compact ? 30 : 42, fontWeight: 800,
            border:`3px solid ${eH('toolkit')}`,
            boxShadow:`0 0 36px ${eH('toolkit', 0.45)}`,
          }}>M</div>
          <div style={{ fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800, color:'var(--text)' }}>Marco</div>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: compact ? 38 : 56, fontWeight: 800,
            color: eH('toolkit'), fontVariantNumeric:'tabular-nums', lineHeight: 1,
            textShadow:`0 0 18px ${eH('toolkit', 0.4)}`, letterSpacing:'-.02em',
          }}>89</div>
        </div>
      )}

      {/* 4° giocatore se presente (solo default) */}
      {variant === 'default' && FINAL[3] && (
        <div style={{
          position:'relative', zIndex: 2,
          display:'flex', alignItems:'center', justifyContent:'center', gap: 8,
          marginTop: 10, padding:'7px 12px',
          background:'var(--bg-card)', border:'1px solid var(--border)',
          borderRadius:'var(--r-pill)', maxWidth: 340, margin:'10px auto 0',
        }}>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.06em',
          }}>4°</span>
          <div style={{
            width: 22, height: 22, borderRadius:'50%',
            background:`hsl(${FINAL[3].color},60%,60%)`, color:'#fff',
            display:'flex', alignItems:'center', justifyContent:'center',
            fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
          }}>{FINAL[3].name[0]}</div>
          <span style={{ fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700, color:'var(--text-sec)' }}>{FINAL[3].name}</span>
          <span style={{ fontFamily:'var(--f-mono)', fontSize: 12, fontWeight: 800, color:'var(--text)', fontVariantNumeric:'tabular-nums' }}>{FINAL[3].score}</span>
        </div>
      )}

      {/* Meta row */}
      <div style={{
        position:'relative', zIndex: 2,
        display:'flex', alignItems:'center', justifyContent:'center', gap: 14,
        flexWrap:'wrap', marginTop: 14,
        fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 700,
        color:'var(--text-sec)',
      }}>
        <span style={{ display:'inline-flex', alignItems:'center', gap: 4 }}><span aria-hidden="true">🎯</span>Wingspan</span>
        <span style={{ color:'var(--text-muted)' }}>·</span>
        <span style={{ display:'inline-flex', alignItems:'center', gap: 4 }}><span aria-hidden="true">📅</span>23 apr 2026</span>
        <span style={{ color:'var(--text-muted)' }}>·</span>
        <span style={{ display:'inline-flex', alignItems:'center', gap: 4 }}><span aria-hidden="true">⏱</span>1h 24min</span>
        <span style={{ color:'var(--text-muted)' }}>·</span>
        <span style={{ display:'inline-flex', alignItems:'center', gap: 4 }}><span aria-hidden="true">🔄</span>18 turni</span>
      </div>

      {/* Winner banner */}
      {variant !== 'solo' && variant !== 'abandoned' && (
        <div style={{
          position:'relative', zIndex: 2, marginTop: 10,
          display:'flex', alignItems:'center', justifyContent:'center', gap: 6,
          padding:'7px 12px', borderRadius:'var(--r-pill)',
          background: eH('toolkit', 0.12),
          border:`1px solid ${eH('toolkit', 0.3)}`,
          color: eH('toolkit'),
          fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.08em',
          maxWidth: 340, margin:'10px auto 0',
        }}>
          {variant === 'tied'
            ? <><span aria-hidden="true">🤝</span>Pareggio tra Marco e Anna</>
            : <><span aria-hidden="true">🏆</span>Marco è il vincitore</>}
        </div>
      )}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── CONNECTION BAR (prod 1:1) ───────────────────────
// ═══════════════════════════════════════════════════════
const ConnectionBar = ({ compact }) => {
  const pips = [
    { entity:'game',    count: 1, label:'Gioco',     em:'🎲', empty: false },
    { entity:'player',  count: 4, label:'Giocatori', em:'👤', empty: false },
    { entity:'agent',   count: 1, label:'Agente',    em:'🤖', empty: false },
    { entity:'chat',    count: 8, label:'Chat',      em:'💬', empty: false },
    { entity:'kb',      count: 3, label:'Foto',      em:'📄', empty: false },
    { entity:'event',   count: 0, label:'Serata',    em:'📅', empty: true  },
  ];
  return (
    <div className="mai-cb-scroll" style={{
      display:'flex', gap: 6,
      padding: compact ? '8px 12px' : '10px 32px',
      background:'var(--glass-bg)', backdropFilter:'blur(12px)',
      borderBottom:'1px solid var(--border)',
      overflowX:'auto', scrollbarWidth:'none',
      position:'sticky', top: 0, zIndex: 12,
    }}>
      {pips.map(p => (
        <a key={p.entity} href={`#section-${p.entity}`}
          style={{
            display:'inline-flex', alignItems:'center', gap: 5,
            padding:'5px 10px', borderRadius:'var(--r-pill)',
            background: p.empty ? 'transparent' : eH(p.entity, 0.1),
            border: p.empty
              ? `1px dashed ${eH(p.entity, 0.4)}`
              : `1px solid ${eH(p.entity, 0.3)}`,
            color: eH(p.entity),
            fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.04em',
            opacity: p.empty ? 0.6 : 1,
            textDecoration:'none',
            whiteSpace:'nowrap', flexShrink: 0,
            cursor:'pointer',
          }}>
          <span aria-hidden="true">{p.em}</span>
          {!p.empty && (
            <span style={{
              padding:'0 5px', borderRadius:'var(--r-pill)',
              background: eH(p.entity, 0.2),
              fontWeight: 800,
            }}>{p.count}</span>
          )}
          <span>{p.label}</span>
        </a>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── KPI GRID ────────────────────────────────────────
// /* v2: SummaryKpiGrid */
// ═══════════════════════════════════════════════════════
const KpiGrid = ({ compact }) => {
  const kpis = [
    { entity:'session', em:'⏱', value:'1h 24min', label:'Durata' },
    { entity:'session', em:'🔄', value:'18', label:'Turni' },
    { entity:'toolkit', em:'🏆', value:'Marco', label:'MVP' },
    { entity:'session', em:'📊', value:'71.75', label:'Score medio' },
    { entity:'game', em:'🥚', value:'89', label:'Eggs totali' },
    { entity:'game', em:'🐦', value:'74', label:'Birds totali' },
    { entity:'game', em:'🍃', value:'67', label:'Habitat' },
    { entity:'game', em:'🎯', value:'28', label:'Bonus' },
  ];
  return (
    <div style={{
      display:'grid',
      gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)',
      gap: compact ? 7 : 10,
    }}>
      {kpis.map((k, i) => (
        <div key={i} style={{
          padding: compact ? '10px 12px' : '14px 16px',
          borderRadius:'var(--r-md)',
          background:'var(--bg-card)',
          border:`1px solid ${eH(k.entity, 0.22)}`,
          borderLeft:`3px solid ${eH(k.entity)}`,
          display:'flex', flexDirection:'column', gap: 3,
        }}>
          <div style={{
            display:'flex', alignItems:'center', gap: 5,
            fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
            color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.06em',
          }}>
            <span aria-hidden="true">{k.em}</span>
            {k.label}
          </div>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: compact ? 20 : 24, fontWeight: 800,
            color: eH(k.entity), fontVariantNumeric:'tabular-nums',
            lineHeight: 1.05, letterSpacing:'-.01em',
          }}>{k.value}</div>
        </div>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SCORING BREAKDOWN TABLE ─────────────────────────
// /* v2: ScoringBreakdownTable */
// ═══════════════════════════════════════════════════════
const ScoringBreakdownTable = ({ compact }) => {
  const [showCharts, setShowCharts] = uS(false);
  const max = Math.max(...FINAL.map(p => p.score));
  return (
    <div style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', overflow:'hidden',
    }}>
      {/* Header row */}
      <div style={{
        padding:'10px 14px',
        borderBottom:'1px solid var(--border-light)',
        display:'flex', alignItems:'center', justifyContent:'space-between',
        flexWrap:'wrap', gap: 6,
      }}>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14.5, fontWeight: 800,
          color:'var(--text)', margin: 0, display:'inline-flex', alignItems:'center', gap: 6,
        }}>
          <span aria-hidden="true" style={{
            width: 22, height: 22, borderRadius:'var(--r-sm)',
            background: eH('session', 0.14), color: eH('session'),
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            fontSize: 11,
          }}>📊</span>
          Scoring breakdown
        </h3>
        <button type="button" onClick={() => setShowCharts(s => !s)}
          style={{
            padding:'5px 10px', borderRadius:'var(--r-pill)',
            background: showCharts ? eH('session', 0.14) : 'var(--bg-muted)',
            border: showCharts ? `1px solid ${eH('session', 0.4)}` : '1px solid var(--border)',
            color: showCharts ? eH('session') : 'var(--text-sec)',
            fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 800,
            cursor:'pointer', textTransform:'uppercase', letterSpacing:'.06em',
          }}>📊 {showCharts ? 'Tabella' : 'Grafici'}</button>
      </div>
      {!showCharts ? (
        <div style={{ overflowX:'auto' }}>
          <table style={{ width:'100%', borderCollapse:'collapse', minWidth: compact ? 480 : 0 }}>
            <thead>
              <tr style={{ background:'var(--bg-muted)' }}>
                {['Giocatore','🥚 Eggs','🐦 Birds','🍃 Habitat','🎯 Bonus','Totale'].map((h, i) => (
                  <th key={i} style={{
                    padding:'8px 12px',
                    fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
                    color:'var(--text-muted)', textAlign: i === 0 ? 'left' : 'center',
                    textTransform:'uppercase', letterSpacing:'.05em',
                    borderBottom:'1px solid var(--border-light)',
                    whiteSpace:'nowrap',
                  }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {FINAL.map(p => {
                const isWinner = p.position === 1;
                return (
                  <tr key={p.id} style={{
                    background: isWinner ? eH('toolkit', 0.06) : 'transparent',
                    borderBottom:'1px solid var(--border-light)',
                  }}>
                    <td style={{ padding:'10px 12px', whiteSpace:'nowrap' }}>
                      <div style={{ display:'flex', alignItems:'center', gap: 7 }}>
                        {isWinner && <span aria-hidden="true">🏆</span>}
                        <div style={{
                          width: 22, height: 22, borderRadius:'50%',
                          background:`hsl(${p.color},60%,60%)`, color:'#fff',
                          display:'flex', alignItems:'center', justifyContent:'center',
                          fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
                        }}>{p.name[0]}</div>
                        <span style={{
                          fontFamily:'var(--f-display)', fontSize: 12.5,
                          fontWeight: isWinner ? 800 : 700,
                          color:'var(--text)',
                        }}>{p.name}</span>
                      </div>
                    </td>
                    {['eggs','birds','habitat','bonus'].map(c => (
                      <td key={c} style={{
                        padding:'10px 12px', textAlign:'center',
                        fontFamily:'var(--f-mono)', fontSize: 12, fontWeight: 700,
                        color:'var(--text-sec)', fontVariantNumeric:'tabular-nums',
                      }}>{p.breakdown[c]}</td>
                    ))}
                    <td style={{
                      padding:'10px 12px', textAlign:'center',
                      fontFamily:'var(--f-display)', fontSize: 16,
                      fontWeight: 800,
                      color: isWinner ? eH('toolkit') : 'var(--text)',
                      fontVariantNumeric:'tabular-nums',
                    }}>{p.score}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <div style={{ padding:'16px 14px', display:'flex', flexDirection:'column', gap: 10 }}>
          {FINAL.map(p => {
            const isWinner = p.position === 1;
            return (
              <div key={p.id} style={{ display:'flex', alignItems:'center', gap: 10 }}>
                <div style={{
                  width: 70, fontFamily:'var(--f-display)', fontSize: 12,
                  fontWeight: isWinner ? 800 : 700, color:'var(--text)',
                  display:'inline-flex', alignItems:'center', gap: 4,
                }}>{isWinner && '🏆'} {p.name}</div>
                <div style={{ flex: 1, height: 22, position:'relative',
                  background:'var(--bg-muted)', borderRadius:'var(--r-sm)', overflow:'hidden' }}>
                  <div style={{
                    width:`${(p.score/max)*100}%`, height:'100%',
                    background: isWinner
                      ? `linear-gradient(90deg, ${eH('toolkit', 0.7)}, ${eH('toolkit')})`
                      : `linear-gradient(90deg, ${eH('session', 0.5)}, ${eH('session')})`,
                    borderRadius:'var(--r-sm)',
                  }}/>
                  <div style={{
                    position:'absolute', inset: 0,
                    display:'flex', alignItems:'center', padding:'0 9px',
                    fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
                    color:'#fff', textShadow:'0 1px 2px rgba(0,0,0,.3)',
                  }}>{p.score}</div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};

window.SummaryParts = {
  SummaryHeroPodium, ConnectionBar, KpiGrid, ScoringBreakdownTable,
  FINAL, eH,
};
