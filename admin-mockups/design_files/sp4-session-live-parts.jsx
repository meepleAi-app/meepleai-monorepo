/* MeepleAI SP4 wave 2 — Schermata #2 / 3
   Route: /sessions/[id]/live
   Pattern: Split 3-col desktop · vertical stack mobile w/ tab switcher
   Entity: session (palette indaco)

   v2 nuovi:
   - LiveTopBar              → ui/v2/live-top-bar/
   - TurnIndicator           → ui/v2/turn-indicator/
   - PlayerRosterLive        → ui/v2/player-roster-live/
   - LiveScoringPanel        → ui/v2/live-scoring-panel/
   - ActionLogTimeline       → ui/v2/action-log-timeline/
   - SessionToolsRail        → ui/v2/session-tools-rail/
   - LiveAgentChat           → ui/v2/live-agent-chat/
   - LiveSessionNotes        → ui/v2/live-session-notes/
   - LiveBottomTabs          → ui/v2/live-bottom-tabs/  (mobile only)
   - PauseOverlay            → ui/v2/pause-overlay/
   - EndgameDialog           → ui/v2/endgame-dialog/
   - ConnectionLostBanner    → ui/v2/connection-lost-banner/
*/

const { useState, useEffect, useRef, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.session;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── Wingspan players (live game roster) ─────────────
const ROSTER = [
  { id:'p-marco', name:'Marco', emoji:'👤', color: 262, current: true,
    score: 87, lastDelta: +12, position: 1,
    breakdown: { eggs: 24, birds: 31, habitat: 18, bonus: 14 } },
  { id:'p-anna',  name:'Anna',  emoji:'👤', color: 320, current: false,
    score: 79, lastDelta: +8,  position: 2,
    breakdown: { eggs: 21, birds: 28, habitat: 22, bonus: 8 } },
  { id:'p-luca',  name:'Luca',  emoji:'👤', color: 180, current: false,
    score: 64, lastDelta: +5,  position: 3,
    breakdown: { eggs: 18, birds: 22, habitat: 16, bonus: 8 } },
  { id:'p-sara',  name:'Sara',  emoji:'👤', color: 100, current: false,
    score: 58, lastDelta: +3,  position: 4,
    breakdown: { eggs: 15, birds: 20, habitat: 14, bonus: 9 } },
];

// ─── Action log events ───────────────────────────────
const LOG = [
  { id:'e1', t:'14:32', kind:'pause', icon:'⏸', text:'Pausa 14:32–14:38 (6m)', actors:[] },
  { id:'e2', t:'14:38', kind:'resume', icon:'▶', text:'Sessione ripresa', actors:[] },
  { id:'e3', t:'14:41', kind:'tool', icon:'🎲', text:'Marco ha tirato 5 + 3 (somma 8)', actors:['p-marco'] },
  { id:'e4', t:'14:43', kind:'score', icon:'🏆', text:'Anna ha guadagnato +8 punti (Habitat)', actors:['p-anna'] },
  { id:'e5', t:'14:48', kind:'score', icon:'🥚', text:'Luca ha deposto 2 uova (+2)', actors:['p-luca'] },
  { id:'e6', t:'14:51', kind:'agent', icon:'🤖', text:'Agente: "Considera la wetland — Marco potrebbe completare il bonus Forest se prendi la prossima carta foresta."', actors:[] },
  { id:'e7', t:'14:54', kind:'photo', icon:'📷', text:'Sara ha aggiunto una foto (Tableau finale)', actors:['p-sara'] },
  { id:'e8', t:'14:58', kind:'chat', icon:'💬', text:'Marco: "Quanto valgono i bird con uova multiple?"', actors:['p-marco'] },
  { id:'e9', t:'15:02', kind:'agent', icon:'🤖', text:'Agente: "I bird con simbolo uovo nell\'angolo valgono 1 punto bonus per uovo deposto."', actors:[] },
  { id:'e10', t:'15:05', kind:'score', icon:'🐦', text:'Marco ha giocato Bald Eagle (Forest +12)', actors:['p-marco'] },
];

const LOG_FILTERS = [
  { id:'all',   label:'Tutti' },
  { id:'score', label:'Score' },
  { id:'event', label:'Eventi' },
  { id:'chat',  label:'Chat' },
  { id:'tool',  label:'Tools' },
];

// ─── Chat thread (agent live) ────────────────────────
const CHAT = [
  { id:'c1', from:'agent', t:'14:25', text:'Ciao Marco, sono qui se hai domande sulle regole. Buona partita!' },
  { id:'c2', from:'user',  t:'14:58', text:'Quanto valgono i bird con uova multiple?' },
  { id:'c3', from:'agent', t:'15:02', text:'I bird con simbolo uovo nell\'angolo valgono 1 punto bonus per ogni uovo deposto su di loro a fine partita. Massimo 6 uova per bird.', citations: ['Wingspan Rulebook §4.2', 'FAQ ufficiale #12'] },
  { id:'c4', from:'user',  t:'15:08', text:'Posso giocare un bird sopra il limite di habitat?' },
];

const CHAT_SUGGESTIONS = [
  'Quanto valgono i bird?',
  'Posso giocare due carte?',
  'Regole bonus end-game',
  'Cosa fa la wetland?',
];

// ═══════════════════════════════════════════════════════
// ─── ENTITY CHIP ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const EntityChip = ({ type, label, link }) => (
  <span style={{
    display:'inline-flex', alignItems:'center', gap: 4,
    padding:'2px 7px', borderRadius:'var(--r-pill)',
    background: entityHsl(type, 0.12),
    border: `1px solid ${entityHsl(type, 0.28)}`,
    color: entityHsl(type),
    fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
    textTransform:'uppercase', letterSpacing:'.05em',
    cursor: link ? 'pointer' : 'default',
  }}>
    <span aria-hidden="true">{DS.EC[type].em}</span>
    {label}
  </span>
);

// ═══════════════════════════════════════════════════════
// ─── TURN INDICATOR ──────────────────────────────────
// /* v2: TurnIndicator */
// ═══════════════════════════════════════════════════════
const TurnIndicator = ({ current, total, player, compact }) => (
  <div style={{
    display:'flex', flexDirection:'column', gap: compact ? 2 : 4,
    alignItems:'center', minWidth: 0, flex: 1, maxWidth: compact ? '100%' : 480,
  }}>
    <div style={{
      display:'flex', alignItems:'center', gap: 8, justifyContent:'center',
      flexWrap:'nowrap', minWidth: 0,
    }}>
      <span style={{
        fontFamily:'var(--f-mono)', fontSize: compact ? 10 : 11, fontWeight: 800,
        color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
        whiteSpace:'nowrap',
      }}>Turno</span>
      <span style={{
        fontFamily:'var(--f-display)', fontSize: compact ? 16 : 20, fontWeight: 800,
        color:'var(--text)', fontVariantNumeric:'tabular-nums', lineHeight: 1,
      }}>{current}<span style={{ color:'var(--text-muted)' }}>/{total}</span></span>
      <span style={{
        width: 1, height: 16, background:'var(--border)', flexShrink: 0,
      }}/>
      <div style={{
        display:'flex', alignItems:'center', gap: 5, minWidth: 0,
      }}>
        <span aria-hidden="true" className="mai-pulse-dot" style={{
          width: 7, height: 7, borderRadius:'50%',
          background: entityHsl('session'),
          boxShadow: `0 0 8px ${entityHsl('session', 0.6)}`,
          flexShrink: 0,
        }}/>
        <div style={{
          width: compact ? 22 : 26, height: compact ? 22 : 26, borderRadius:'50%',
          background:`hsl(${player.color}, 60%, 60%)`, color:'#fff',
          display:'flex', alignItems:'center', justifyContent:'center',
          fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
          flexShrink: 0,
        }}>{player.name[0]}</div>
        <span style={{
          fontFamily:'var(--f-display)', fontSize: compact ? 12.5 : 13.5, fontWeight: 800,
          color:'var(--text)',
        }}>{player.name}</span>
        <span style={{
          fontFamily:'var(--f-body)', fontSize: compact ? 11 : 12.5, fontWeight: 600,
          color:'var(--text-muted)',
        }}>sta giocando</span>
      </div>
    </div>
    {/* Progress bar */}
    <div role="progressbar" aria-valuenow={current} aria-valuemin={1} aria-valuemax={total}
      style={{
        width:'100%', height: 3, borderRadius: 999,
        background:'var(--bg-muted)', overflow:'hidden',
      }}>
      <div style={{
        width: `${(current/total)*100}%`, height:'100%',
        background:`linear-gradient(90deg, ${entityHsl('session', 0.7)} 0%, ${entityHsl('session')} 100%)`,
        borderRadius: 999,
        boxShadow:`0 0 6px ${entityHsl('session', 0.6)}`,
      }}/>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── LIVE TOP BAR ────────────────────────────────────
// /* v2: LiveTopBar */
// ═══════════════════════════════════════════════════════
const LiveTopBar = ({ compact, onPause, paused }) => {
  const [now, setNow] = useState(0);
  useEffect(() => {
    if (paused) return;
    const t = setInterval(() => setNow(n => n + 1), 1000);
    return () => clearInterval(t);
  }, [paused]);
  const baseSec = 5076 + now;
  const hh = Math.floor(baseSec / 3600).toString().padStart(2,'0');
  const mm = Math.floor((baseSec % 3600) / 60).toString().padStart(2,'0');
  const ss = (baseSec % 60).toString().padStart(2,'0');

  return (
    <header style={{
      display:'flex', flexDirection:'column',
      background:'var(--glass-bg)', backdropFilter:'blur(14px)',
      borderBottom:`1px solid ${entityHsl('session', 0.25)}`,
      position:'sticky', top: 0, zIndex: 30,
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: compact ? 8 : 14,
        padding: compact ? '8px 12px' : '12px 24px',
        minHeight: compact ? 56 : 68,
      }}>
        {/* Left */}
        <div style={{
          display:'flex', alignItems:'center', gap: 8, flexShrink: 0,
          minWidth: 0,
        }}>
          <button type="button" aria-label="Indietro" style={{
            width: compact ? 32 : 36, height: compact ? 32 : 36,
            borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text-sec)', cursor:'pointer',
            fontSize: 16, fontWeight: 800, flexShrink: 0,
          }}>←</button>
          {!compact && (
            <div style={{ display:'flex', flexDirection:'column', gap: 2, minWidth: 0 }}>
              <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
                <EntityChip type="game" label="Wingspan"/>
              </div>
              <div style={{
                fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
                color:'var(--text)', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
              }}>Serata Wingspan #3</div>
            </div>
          )}
          {compact && (
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              color:'var(--text)', whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
              minWidth: 0,
            }}>Wingspan #3</div>
          )}
        </div>

        {/* Center turn indicator (desktop only fully expanded) */}
        {!compact && <TurnIndicator current={12} total={18} player={ROSTER[0]} compact={false}/>}

        {/* Right: timer + controls */}
        <div style={{
          display:'flex', alignItems:'center', gap: compact ? 4 : 6,
          flexShrink: 0, marginLeft:'auto',
        }}>
          <div style={{
            display:'flex', alignItems:'center', gap: 5,
            padding: compact ? '5px 8px' : '6px 11px',
            borderRadius:'var(--r-md)',
            background: entityHsl('session', 0.1),
            border:`1px solid ${entityHsl('session', 0.25)}`,
          }}>
            <span aria-hidden="true" style={{ fontSize: 11 }}>⏱</span>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: compact ? 12 : 13, fontWeight: 800,
              color: entityHsl('session'), fontVariantNumeric:'tabular-nums',
              letterSpacing:'.04em',
            }}>{hh}:{mm}:{ss}</span>
          </div>
          <button type="button" onClick={onPause} aria-label="Pausa" style={{
            padding: compact ? '6px 9px' : '7px 12px',
            borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text-sec)', cursor:'pointer',
            fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
            display:'inline-flex', alignItems:'center', gap: 4,
          }}><span aria-hidden="true">⏸</span>{!compact && 'Pausa'}</button>
          <button type="button" aria-label="Menu sessione" style={{
            width: compact ? 32 : 36, height: compact ? 32 : 36,
            borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text-sec)', cursor:'pointer',
            fontSize: 16, fontWeight: 800, flexShrink: 0,
          }}>⋯</button>
        </div>
      </div>
      {/* Mobile compact turn indicator */}
      {compact && (
        <div style={{
          padding:'8px 12px 10px',
          borderTop:'1px solid var(--border-light)',
          background:'var(--bg-card)',
        }}>
          <TurnIndicator current={12} total={18} player={ROSTER[0]} compact/>
        </div>
      )}
      {/* Live banner */}
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding: compact ? '6px 12px' : '7px 24px',
        background: entityHsl('session', 0.06),
        borderTop:`1px solid ${entityHsl('session', 0.18)}`,
        fontFamily:'var(--f-mono)', fontSize: 10.5,
      }}>
        <span className="mai-pulse-dot" aria-hidden="true" style={{
          width: 7, height: 7, borderRadius:'50%',
          background:'hsl(140, 60%, 45%)',
          boxShadow:'0 0 8px hsl(140, 60%, 45% / .7)',
        }}/>
        <span style={{
          color:'hsl(140, 50%, 35%)', fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.08em',
        }}>Live</span>
        <span style={{ color:'var(--text-muted)', fontWeight: 700 }}>·</span>
        <span style={{ color:'var(--text-sec)', fontWeight: 700 }}>Auto-save attivo · ultimo: 12s fa</span>
      </div>
    </header>
  );
};

// ═══════════════════════════════════════════════════════
// ─── PLAYER ROSTER LIVE ──────────────────────────────
// /* v2: PlayerRosterLive */
// ═══════════════════════════════════════════════════════
const PlayerCard = ({ p, expanded, onToggle }) => {
  const isCurrent = p.current;
  return (
    <div style={{
      position:'relative',
      padding:'10px 12px',
      background: isCurrent ? entityHsl('session', 0.08) : 'var(--bg-card)',
      border: isCurrent ? `1px solid ${entityHsl('session', 0.35)}` : '1px solid var(--border)',
      borderLeft: isCurrent ? `4px solid ${entityHsl('session')}` : `4px solid hsl(${p.color}, 60%, 60%)`,
      borderRadius:'var(--r-md)',
    }}>
      {/* Top row */}
      <div style={{ display:'flex', alignItems:'center', gap: 10 }}>
        <div style={{ position:'relative', flexShrink: 0 }}>
          <div style={{
            width: 44, height: 44, borderRadius:'50%',
            background:`linear-gradient(135deg, hsl(${p.color}, 70%, 65%), hsl(${p.color}, 60%, 45%))`,
            color:'#fff',
            display:'flex', alignItems:'center', justifyContent:'center',
            fontFamily:'var(--f-display)', fontSize: 17, fontWeight: 800,
          }}>{p.name[0]}</div>
          {isCurrent && (
            <span className="mai-pulse-dot" aria-hidden="true" style={{
              position:'absolute', bottom: -1, right: -1,
              width: 12, height: 12, borderRadius:'50%',
              background: entityHsl('session'),
              border:'2px solid var(--bg-card)',
              boxShadow:`0 0 8px ${entityHsl('session', 0.6)}`,
            }}/>
          )}
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display:'flex', alignItems:'center', gap: 5, marginBottom: 2,
          }}>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800,
              color:'var(--text)',
            }}>{p.name}</div>
            {isCurrent && (
              <span style={{
                padding:'1px 5px', borderRadius:'var(--r-pill)',
                background: entityHsl('session'),
                color:'#fff',
                fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800,
                textTransform:'uppercase', letterSpacing:'.06em',
              }}>turno</span>
            )}
          </div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)',
            fontWeight: 700, textTransform:'uppercase', letterSpacing:'.04em',
          }}>
            {p.position === 1 ? '🏆 1°' : `${p.position}°`} di 4
          </div>
        </div>
        <div style={{ textAlign:'right', flexShrink: 0 }}>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
            color: isCurrent ? entityHsl('session') : 'var(--text)',
            fontVariantNumeric:'tabular-nums', lineHeight: 1,
          }}>{p.score}</div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
            color: p.lastDelta > 0 ? 'hsl(140, 50%, 40%)' : 'var(--text-muted)',
            marginTop: 2,
          }}>{p.lastDelta > 0 ? `+${p.lastDelta}` : p.lastDelta}</div>
        </div>
      </div>
      {/* Breakdown */}
      <button type="button" onClick={onToggle} style={{
        marginTop: 8, width:'100%',
        display:'flex', alignItems:'center', justifyContent:'space-between',
        padding:'5px 7px', borderRadius:'var(--r-sm)',
        background:'transparent', border:'1px dashed var(--border)',
        color:'var(--text-muted)', cursor:'pointer',
        fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      }}>
        <span>Breakdown</span>
        <span aria-hidden="true">{expanded ? '▴' : '▾'}</span>
      </button>
      {expanded && (
        <div style={{
          marginTop: 6, display:'grid',
          gridTemplateColumns:'repeat(4, 1fr)', gap: 4,
        }}>
          {[
            { ic:'🥚', v: p.breakdown.eggs, lb:'Eggs' },
            { ic:'🐦', v: p.breakdown.birds, lb:'Birds' },
            { ic:'🍃', v: p.breakdown.habitat, lb:'Habitat' },
            { ic:'🎯', v: p.breakdown.bonus, lb:'Bonus' },
          ].map(b => (
            <div key={b.lb} style={{
              padding:'4px 5px', borderRadius:'var(--r-sm)',
              background:'var(--bg-muted)',
              textAlign:'center',
            }}>
              <div style={{ fontSize: 11 }} aria-hidden="true">{b.ic}</div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
                color:'var(--text)', fontVariantNumeric:'tabular-nums',
              }}>{b.v}</div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 8, fontWeight: 700,
                color:'var(--text-muted)', textTransform:'uppercase',
              }}>{b.lb}</div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

const PlayerRosterLive = ({ compact }) => {
  const [expandedId, setExpandedId] = useState('p-marco');
  return (
    <aside style={{
      width: compact ? '100%' : 300,
      background:'var(--bg-card)',
      borderRight: compact ? 'none' : '1px solid var(--border)',
      display:'flex', flexDirection:'column',
      flexShrink: 0,
    }}>
      <div style={{
        padding:'12px 14px',
        borderBottom:'1px solid var(--border-light)',
        display:'flex', alignItems:'center', justifyContent:'space-between',
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 800,
          color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
          display:'inline-flex', alignItems:'center', gap: 5,
        }}>
          <span aria-hidden="true">👥</span>
          Giocatori (4)
        </div>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color: entityHsl('session'),
        }}>● Live</span>
      </div>
      <div style={{
        flex: 1, overflowY:'auto',
        padding: 10, display:'flex', flexDirection:'column', gap: 8,
      }}>
        {ROSTER.map(p => (
          <PlayerCard key={p.id} p={p}
            expanded={expandedId === p.id}
            onToggle={() => setExpandedId(expandedId === p.id ? null : p.id)}/>
        ))}
      </div>
      <div style={{
        padding:'10px 12px', borderTop:'1px solid var(--border-light)',
      }}>
        <button type="button" disabled title="Disponibile solo a inizio partita"
          style={{
            width:'100%', padding:'8px',
            borderRadius:'var(--r-md)',
            background:'transparent', color:'var(--text-muted)',
            border:'1px dashed var(--border-strong)',
            fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
            cursor:'not-allowed', opacity: 0.7,
          }}>+ Aggiungi giocatore</button>
      </div>
    </aside>
  );
};

// ═══════════════════════════════════════════════════════
// ─── LIVE SCORING PANEL ──────────────────────────────
// /* v2: LiveScoringPanel */
// ═══════════════════════════════════════════════════════
const ScoringTab = ({ active, onClick, label, value }) => (
  <button type="button" onClick={onClick} aria-pressed={active}
    style={{
      flex: 1, padding:'7px 6px',
      background: active ? entityHsl('session', 0.1) : 'transparent',
      border:'none',
      borderBottom: active ? `2px solid ${entityHsl('session')}` : '2px solid transparent',
      color: active ? entityHsl('session') : 'var(--text-sec)',
      fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 800,
      cursor:'pointer',
      display:'flex', flexDirection:'column', alignItems:'center', gap: 2,
    }}>
    <span style={{ textTransform:'uppercase', letterSpacing:'.04em' }}>{label}</span>
    <span style={{
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      color: active ? entityHsl('session') : 'var(--text-muted)',
    }}>{value}</span>
  </button>
);

const LiveScoringPanel = ({ emptyTurn, compact }) => {
  const [tab, setTab] = useState('eggs');
  const [val, setVal] = useState(emptyTurn ? 0 : 12);
  const tabs = [
    { id:'eggs',    label:'Eggs',    value: 24 },
    { id:'birds',   label:'Birds',   value: 31 },
    { id:'habitat', label:'Habitat', value: 18 },
    { id:'bonus',   label:'Bonus',   value: 14 },
  ];
  const totalThis = val;
  const totalRunning = 87;

  return (
    <div style={{
      background:'var(--bg-card)',
      border:`1px solid ${entityHsl('session', 0.3)}`,
      borderRadius:'var(--r-lg)',
      overflow:'hidden',
      boxShadow:`0 4px 16px ${entityHsl('session', 0.1)}`,
    }}>
      <div style={{
        padding:'10px 14px',
        background: entityHsl('session', 0.06),
        borderBottom:`1px solid ${entityHsl('session', 0.2)}`,
        display:'flex', alignItems:'center', gap: 8, flexWrap:'wrap',
      }}>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
          color: entityHsl('session'), textTransform:'uppercase', letterSpacing:'.08em',
        }}>Turno corrente</span>
        <span style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          color:'var(--text)',
        }}>Marco</span>
        <div style={{ flex: 1 }}/>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 10.5, color:'var(--text-muted)',
          fontWeight: 700,
        }}>Totale: <strong style={{ color:'var(--text)' }}>{totalRunning + (emptyTurn ? 0 : 0)}</strong> pt</span>
      </div>

      {/* Tabs */}
      <div style={{ display:'flex', borderBottom:'1px solid var(--border-light)' }}>
        {tabs.map(t => (
          <ScoringTab key={t.id} active={tab===t.id} onClick={()=>setTab(t.id)} label={t.label} value={t.value}/>
        ))}
      </div>

      {/* Input */}
      <div style={{ padding: compact ? '14px 14px' : '18px 20px' }}>
        <div style={{
          display:'flex', alignItems:'center', gap: 12, justifyContent:'center',
          marginBottom: 12,
        }}>
          <button type="button" aria-label="Diminuisci"
            onClick={() => setVal(v => Math.max(0, v - 1))}
            style={{
              width: 44, height: 44, borderRadius:'var(--r-md)',
              background:'var(--bg-muted)', border:'1px solid var(--border)',
              color:'var(--text)', fontSize: 22, fontWeight: 800, cursor:'pointer',
            }}>−</button>
          <div style={{
            minWidth: 120, textAlign:'center',
            fontFamily:'var(--f-display)', fontSize: compact ? 40 : 54, fontWeight: 800,
            color: entityHsl('session'),
            fontVariantNumeric:'tabular-nums', lineHeight: 1,
            letterSpacing:'-.02em',
          }}>{val}</div>
          <button type="button" aria-label="Aumenta"
            onClick={() => setVal(v => v + 1)}
            style={{
              width: 44, height: 44, borderRadius:'var(--r-md)',
              background: entityHsl('session', 0.12),
              border:`1px solid ${entityHsl('session', 0.3)}`,
              color: entityHsl('session'), fontSize: 22, fontWeight: 800, cursor:'pointer',
            }}>+</button>
        </div>
        <div style={{
          display:'flex', gap: 5, justifyContent:'center', flexWrap:'wrap',
          marginBottom: 14,
        }}>
          {[1, 5, 10, 20].map(p => (
            <button key={p} type="button" onClick={() => setVal(v => v + p)}
              style={{
                padding:'5px 10px', borderRadius:'var(--r-pill)',
                background:'var(--bg-muted)', border:'1px solid var(--border)',
                color:'var(--text-sec)',
                fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
                cursor:'pointer',
              }}>+{p}</button>
          ))}
        </div>
        <div style={{
          padding:'8px 10px', borderRadius:'var(--r-sm)',
          background:'var(--bg-muted)',
          fontFamily:'var(--f-mono)', fontSize: 10.5, color:'var(--text-muted)',
          fontWeight: 700, textAlign:'center', marginBottom: 14,
          textTransform:'uppercase', letterSpacing:'.04em',
        }}>
          Categoria <strong style={{ color:'var(--text)' }}>{tab}</strong> +{val} ·
          Running <strong style={{ color: entityHsl('session') }}>{totalRunning + val}</strong>
        </div>
        <div style={{ display:'flex', gap: 6 }}>
          <button type="button" style={{
            flex: 2, padding:'9px',
            borderRadius:'var(--r-md)',
            background: entityHsl('session'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            cursor:'pointer',
            boxShadow:`0 4px 12px ${entityHsl('session', 0.4)}`,
          }}>✓ Conferma turno</button>
          <button type="button" style={{
            flex: 1, padding:'9px',
            borderRadius:'var(--r-md)',
            background:'transparent', color:'var(--text-sec)',
            border:'1px solid var(--border-strong)',
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700, cursor:'pointer',
          }}>Annulla</button>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── ACTION LOG TIMELINE ─────────────────────────────
// /* v2: ActionLogTimeline */
// ═══════════════════════════════════════════════════════
const eventColor = (kind) => {
  const m = {
    score: 'session', tool: 'tool', agent: 'agent',
    chat: 'chat', photo: 'kb', pause: 'event', resume: 'toolkit',
  };
  return m[kind] || 'session';
};

const ActionLogTimeline = ({ compact }) => {
  const [filter, setFilter] = useState('all');
  const filtered = useMemo(() => {
    if (filter === 'all') return LOG;
    if (filter === 'event') return LOG.filter(e => ['pause','resume','photo'].includes(e.kind));
    return LOG.filter(e => e.kind === filter);
  }, [filter]);

  return (
    <div style={{
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
      display:'flex', flexDirection:'column',
      flex: 1, minHeight: 0, overflow:'hidden',
    }}>
      {/* Header */}
      <div style={{
        padding:'10px 14px',
        borderBottom:'1px solid var(--border-light)',
        display:'flex', alignItems:'center', gap: 8, flexWrap:'wrap',
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 800,
          color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
          display:'inline-flex', alignItems:'center', gap: 5,
        }}>
          <span aria-hidden="true">📜</span>Action log
        </div>
        <div style={{ flex: 1 }}/>
        <button type="button" style={{
          padding:'3px 9px', borderRadius:'var(--r-pill)',
          background: entityHsl('session', 0.1),
          border:`1px solid ${entityHsl('session', 0.3)}`,
          color: entityHsl('session'), cursor:'pointer',
          fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.06em',
          display:'inline-flex', alignItems:'center', gap: 3,
        }}>↓ Live</button>
      </div>
      {/* Filters */}
      <div style={{
        padding:'7px 12px', display:'flex', gap: 4, flexWrap:'wrap',
        borderBottom:'1px solid var(--border-light)',
      }}>
        {LOG_FILTERS.map(f => (
          <button key={f.id} type="button" onClick={() => setFilter(f.id)}
            aria-pressed={filter === f.id}
            style={{
              padding:'3px 9px', borderRadius:'var(--r-pill)',
              background: filter===f.id ? entityHsl('session', 0.14) : 'transparent',
              border: filter===f.id ? `1px solid ${entityHsl('session', 0.4)}` : '1px solid var(--border)',
              color: filter===f.id ? entityHsl('session') : 'var(--text-sec)',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
              cursor:'pointer',
            }}>{f.label}</button>
        ))}
      </div>
      {/* Timeline */}
      <div style={{
        flex: 1, overflowY:'auto', padding:'10px 14px', position:'relative',
      }}>
        <div aria-hidden="true" style={{
          position:'absolute', left: 24, top: 0, bottom: 0,
          width: 2,
          background:`linear-gradient(180deg, ${entityHsl('session', 0.3)}, ${entityHsl('session', 0.05)})`,
        }}/>
        <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
          {filtered.map(e => {
            const ec = eventColor(e.kind);
            return (
              <div key={e.id} style={{
                position:'relative', display:'flex', gap: 10, paddingLeft: 4,
              }}>
                <div style={{
                  width: 22, height: 22, borderRadius:'50%',
                  background: entityHsl(ec, 0.14),
                  border: `2px solid ${entityHsl(ec, 0.4)}`,
                  display:'flex', alignItems:'center', justifyContent:'center',
                  fontSize: 10, flexShrink: 0,
                  zIndex: 1,
                }}>
                  <span aria-hidden="true">{e.icon}</span>
                </div>
                <div style={{
                  flex: 1, minWidth: 0,
                  padding:'5px 10px',
                  background: e.kind === 'agent' ? entityHsl('agent', 0.06) : 'var(--bg-muted)',
                  borderRadius:'var(--r-sm)',
                  borderLeft: e.kind === 'agent' ? `2px solid ${entityHsl('agent')}` : '2px solid transparent',
                }}>
                  <div style={{
                    display:'flex', alignItems:'center', gap: 6, marginBottom: 1,
                  }}>
                    <span style={{
                      fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
                      color: entityHsl(ec), textTransform:'uppercase', letterSpacing:'.06em',
                    }}>{e.t}</span>
                    {e.actors.length > 0 && (
                      <div style={{ display:'flex', gap: 2 }}>
                        {e.actors.map(a => {
                          const p = ROSTER.find(r => r.id === a);
                          if (!p) return null;
                          return (
                            <div key={a} title={p.name} style={{
                              width: 14, height: 14, borderRadius:'50%',
                              background:`hsl(${p.color}, 60%, 60%)`, color:'#fff',
                              display:'flex', alignItems:'center', justifyContent:'center',
                              fontSize: 8, fontWeight: 800,
                              border:'1px solid var(--bg-card)',
                            }}>{p.name[0]}</div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                  <div style={{
                    fontFamily:'var(--f-body)', fontSize: 12, fontWeight: 600,
                    color:'var(--text)', lineHeight: 1.4,
                  }}>{e.text}</div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

window.LiveSessionParts1 = {
  EntityChip, TurnIndicator, LiveTopBar,
  PlayerRosterLive, LiveScoringPanel, ActionLogTimeline,
  ROSTER, CHAT, CHAT_SUGGESTIONS, entityHsl,
};
