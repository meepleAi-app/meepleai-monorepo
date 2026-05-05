/* MeepleAI SP4 wave 2 — Schermata #2 / 3 — main app
   Imports parts from sp4-session-live-parts.jsx (window.LiveSessionParts1)
   This file: tools rail / chat / notes / overlays / state harness / frames
*/

const { useState: useState2, useEffect: useEffect2, useRef: useRef2, useMemo: useMemo2 } = React;
const P = window.LiveSessionParts1;
const DS2 = window.DS;
const eHsl = P.entityHsl;

// ═══════════════════════════════════════════════════════
// ─── SESSION TOOLS RAIL ──────────────────────────────
// /* v2: SessionToolsRail */
// ═══════════════════════════════════════════════════════
const ToolCard = ({ icon, name, desc, color = 'tool', custom }) => (
  <button type="button" style={{
    padding:'12px 10px', borderRadius:'var(--r-md)',
    background: custom ? eHsl('toolkit', 0.06) : 'var(--bg-card)',
    border: custom ? `1px solid ${eHsl('toolkit', 0.3)}` : '1px solid var(--border)',
    cursor:'pointer', textAlign:'left',
    display:'flex', flexDirection:'column', gap: 4,
    minHeight: 78,
  }}>
    <div style={{
      display:'flex', alignItems:'center', gap: 6,
    }}>
      <div style={{
        width: 28, height: 28, borderRadius:'var(--r-sm)',
        background: eHsl(color, 0.14),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 14,
      }} aria-hidden="true">{icon}</div>
      {custom && (
        <span style={{
          padding:'1px 5px', borderRadius:'var(--r-pill)',
          background: eHsl('toolkit', 0.14),
          color: eHsl('toolkit'),
          fontFamily:'var(--f-mono)', fontSize: 8, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.05em',
        }}>Custom</span>
      )}
    </div>
    <div style={{
      fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
      color:'var(--text)',
    }}>{name}</div>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)',
      fontWeight: 600,
    }}>{desc}</div>
  </button>
);

const SessionToolsRail = () => (
  <div style={{ padding: 12, display:'flex', flexDirection:'column', gap: 10 }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
      color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
    }}>Quick tools</div>
    <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap: 7 }}>
      <ToolCard icon="🎲" name="Dadi" desc="d4 d6 d8 d10 d20"/>
      <ToolCard icon="⏱" name="Timer" desc="Countdown · audio"/>
      <ToolCard icon="🔢" name="Contatore" desc="Multi · history"/>
      <ToolCard icon="🪙" name="Moneta" desc="Flip · history"/>
    </div>
    {/* Last roll preview */}
    <div style={{
      padding:'9px 11px', borderRadius:'var(--r-md)',
      background: eHsl('tool', 0.06),
      border:`1px dashed ${eHsl('tool', 0.3)}`,
    }}>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        color: eHsl('tool'), textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 4,
      }}>Ultimo tiro · 14:41</div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 16, fontWeight: 800,
        color:'var(--text)', fontVariantNumeric:'tabular-nums',
      }}>2d6 → 5 + 3 = <strong style={{ color: eHsl('tool') }}>8</strong></div>
    </div>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
      color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
      marginTop: 4,
    }}>Custom · Wingspan</div>
    <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap: 7 }}>
      <ToolCard icon="🦜" name="Pesca uccello" desc="Random bird card" color="toolkit" custom/>
      <ToolCard icon="🌳" name="Habitat picker" desc="Forest · Grassland" color="toolkit" custom/>
    </div>
    <button type="button" style={{
      padding:'10px',
      borderRadius:'var(--r-md)',
      background:'transparent', color: eHsl('tool'),
      border:`1px dashed ${eHsl('tool', 0.4)}`,
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
      cursor:'pointer',
    }}>+ Aggiungi tool</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── LIVE AGENT CHAT ─────────────────────────────────
// /* v2: LiveAgentChat */
// ═══════════════════════════════════════════════════════
const LiveAgentChat = () => (
  <div style={{ display:'flex', flexDirection:'column', flex: 1, minHeight: 0 }}>
    {/* Agent header */}
    <div style={{
      padding:'10px 12px',
      background: eHsl('agent', 0.06),
      borderBottom:`1px solid ${eHsl('agent', 0.2)}`,
      display:'flex', alignItems:'center', gap: 10,
    }}>
      <div style={{
        width: 36, height: 36, borderRadius:'50%',
        background:`linear-gradient(135deg, ${eHsl('agent')}, ${eHsl('agent', 0.6)})`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 16, color:'#fff', position:'relative',
      }} aria-hidden="true">🦜
        <span className="mai-pulse-dot" style={{
          position:'absolute', bottom: -1, right: -1,
          width: 11, height: 11, borderRadius:'50%',
          background:'hsl(140, 60%, 45%)',
          border:'2px solid var(--bg-card)',
        }}/>
      </div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
          color:'var(--text)',
        }}>Wingspan Coach</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 9.5, color:'hsl(140, 50%, 35%)',
          fontWeight: 700,
        }}>● Online · Risponde in 1-2s</div>
      </div>
    </div>
    {/* Messages */}
    <div style={{
      flex: 1, overflowY:'auto', padding: 12,
      display:'flex', flexDirection:'column', gap: 10,
    }}>
      {P.CHAT.map(m => {
        const isAgent = m.from === 'agent';
        return (
          <div key={m.id} style={{
            alignSelf: isAgent ? 'flex-start' : 'flex-end',
            maxWidth:'88%',
            display:'flex', flexDirection:'column',
            gap: 3,
          }}>
            <div style={{
              padding:'7px 11px', borderRadius:'var(--r-lg)',
              background: isAgent ? eHsl('agent', 0.1) : eHsl('chat', 0.12),
              border: `1px solid ${isAgent ? eHsl('agent', 0.25) : eHsl('chat', 0.25)}`,
              borderTopLeftRadius: isAgent ? 4 : 'var(--r-lg)',
              borderTopRightRadius: isAgent ? 'var(--r-lg)' : 4,
              fontFamily:'var(--f-body)', fontSize: 12.5, fontWeight: 500,
              color:'var(--text)', lineHeight: 1.45,
            }}>{m.text}</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
              fontWeight: 700,
              alignSelf: isAgent ? 'flex-start' : 'flex-end',
              padding:'0 4px',
            }}>{m.t}</div>
            {m.citations && (
              <div style={{
                display:'flex', gap: 3, flexWrap:'wrap', marginTop: 2,
              }}>
                {m.citations.map((c, i) => (
                  <span key={i} style={{
                    display:'inline-flex', alignItems:'center', gap: 3,
                    padding:'2px 6px', borderRadius:'var(--r-pill)',
                    background: eHsl('kb', 0.1),
                    border:`1px solid ${eHsl('kb', 0.25)}`,
                    color: eHsl('kb'),
                    fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
                  }}>
                    <span aria-hidden="true">📄</span>{c}
                  </span>
                ))}
              </div>
            )}
          </div>
        );
      })}
    </div>
    {/* Suggestions */}
    <div style={{
      padding:'8px 12px', borderTop:'1px solid var(--border-light)',
      display:'flex', gap: 5, flexWrap:'wrap',
    }}>
      {P.CHAT_SUGGESTIONS.map(s => (
        <button key={s} type="button" style={{
          padding:'5px 10px', borderRadius:'var(--r-pill)',
          background: eHsl('chat', 0.08),
          border:`1px solid ${eHsl('chat', 0.25)}`,
          color: eHsl('chat'),
          fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
          cursor:'pointer',
        }}>{s}</button>
      ))}
    </div>
    {/* Input */}
    <div style={{
      padding:'10px 12px', borderTop:'1px solid var(--border)',
      display:'flex', gap: 6, alignItems:'flex-end',
    }}>
      <textarea placeholder="Chiedi all'agente..." rows={1}
        style={{
          flex: 1, padding:'8px 11px',
          borderRadius:'var(--r-md)', border:'1px solid var(--border)',
          background:'var(--bg)', color:'var(--text)',
          fontFamily:'var(--f-body)', fontSize: 12.5, resize:'none',
          minHeight: 34,
        }}/>
      <button type="button" aria-label="Voice" style={{
        width: 36, height: 36, borderRadius:'var(--r-md)',
        background:'var(--bg-card)', border:'1px solid var(--border)',
        color:'var(--text-sec)', fontSize: 14, cursor:'pointer', flexShrink: 0,
      }}>🎙</button>
      <button type="button" aria-label="Invia" style={{
        width: 36, height: 36, borderRadius:'var(--r-md)',
        background: eHsl('chat'), color:'#fff', border:'none',
        fontSize: 14, cursor:'pointer', flexShrink: 0,
        boxShadow:`0 3px 10px ${eHsl('chat', 0.4)}`,
      }}>↑</button>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── LIVE SESSION NOTES ──────────────────────────────
// /* v2: LiveSessionNotes */
// ═══════════════════════════════════════════════════════
const LiveSessionNotes = () => (
  <div style={{ padding: 12, display:'flex', flexDirection:'column', gap: 12, flex: 1, overflowY:'auto' }}>
    <div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
        color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
        marginBottom: 6,
      }}>Quick notes</div>
      <textarea
        defaultValue={"Marco apertura aggressiva sul forest.\nWetland con 3 uccelli a turno 8.\n→ Considerare strategia bonus card 'Eggs in nest' per fine partita."}
        rows={5}
        style={{
          width:'100%', padding:'10px 12px',
          borderRadius:'var(--r-md)', border:'1px solid var(--border)',
          background:'var(--bg-card)', color:'var(--text)',
          fontFamily:'var(--f-mono)', fontSize: 12, lineHeight: 1.55,
          resize:'vertical', minHeight: 100,
        }}/>
    </div>
    <div>
      <div style={{
        display:'flex', alignItems:'center', justifyContent:'space-between',
        marginBottom: 6,
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
          color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
        }}>Foto · 2</div>
        <button type="button" style={{
          padding:'3px 8px', borderRadius:'var(--r-pill)',
          background: eHsl('kb', 0.1), color: eHsl('kb'),
          border:`1px solid ${eHsl('kb', 0.3)}`,
          fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
          cursor:'pointer', textTransform:'uppercase', letterSpacing:'.04em',
        }}>+ Foto</button>
      </div>
      <div style={{ display:'grid', gridTemplateColumns:'repeat(3, 1fr)', gap: 6 }}>
        {[
          { lb:'Tableau T8', g: 'linear-gradient(135deg, hsl(85,60%,55%), hsl(180,50%,40%))' },
          { lb:'Wetland', g: 'linear-gradient(135deg, hsl(200,60%,55%), hsl(230,50%,40%))' },
        ].map((p, i) => (
          <div key={i} style={{
            aspectRatio:'1', borderRadius:'var(--r-sm)',
            background: p.g,
            display:'flex', alignItems:'flex-end',
            padding: 4, position:'relative', overflow:'hidden',
          }}>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              color:'#fff', background:'rgba(0,0,0,.4)',
              padding:'1px 5px', borderRadius:'var(--r-pill)',
            }}>{p.lb}</span>
          </div>
        ))}
        <button type="button" style={{
          aspectRatio:'1', borderRadius:'var(--r-sm)',
          background:'transparent', border:'1px dashed var(--border-strong)',
          color:'var(--text-muted)', fontSize: 20, cursor:'pointer',
          display:'flex', alignItems:'center', justifyContent:'center',
        }} aria-label="Aggiungi foto">+</button>
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── RIGHT COLUMN ────────────────────────────────────
// ═══════════════════════════════════════════════════════
const RightColumnTabs = ({ initial = 'tools' }) => {
  const [tab, setTab] = useState2(initial);
  const tabs = [
    { id:'tools', icon:'🔧', label:'Tools', entity:'tool' },
    { id:'chat',  icon:'💬', label:'Chat',  entity:'chat' },
    { id:'notes', icon:'📋', label:'Note',  entity:'kb' },
  ];
  return (
    <aside style={{
      width: 380, flexShrink: 0,
      background:'var(--bg-card)',
      borderLeft:'1px solid var(--border)',
      display:'flex', flexDirection:'column',
    }}>
      <div style={{
        display:'flex', borderBottom:'1px solid var(--border-light)',
      }}>
        {tabs.map(t => {
          const active = tab === t.id;
          return (
            <button key={t.id} type="button" onClick={() => setTab(t.id)}
              aria-pressed={active}
              style={{
                flex: 1, padding:'10px 6px',
                background: active ? eHsl(t.entity, 0.06) : 'transparent',
                border:'none',
                borderBottom: active ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent',
                color: active ? eHsl(t.entity) : 'var(--text-sec)',
                fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
                cursor:'pointer',
                display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 4,
              }}>
              <span aria-hidden="true">{t.icon}</span>{t.label}
            </button>
          );
        })}
      </div>
      <div style={{ flex: 1, minHeight: 0, display:'flex', flexDirection:'column' }}>
        {tab === 'tools' && <SessionToolsRail/>}
        {tab === 'chat' && <LiveAgentChat/>}
        {tab === 'notes' && <LiveSessionNotes/>}
      </div>
    </aside>
  );
};

// ═══════════════════════════════════════════════════════
// ─── DESKTOP LAYOUT ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopBody = ({ emptyTurn }) => (
  <div style={{ display:'flex', flex: 1, minHeight: 0 }}>
    <P.PlayerRosterLive/>
    <main style={{
      flex: 1, padding: 16, overflowY:'auto',
      display:'flex', flexDirection:'column', gap: 12, minWidth: 0,
    }}>
      <P.LiveScoringPanel emptyTurn={emptyTurn}/>
      <P.ActionLogTimeline/>
    </main>
    <RightColumnTabs/>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── MOBILE LAYOUT ───────────────────────────────────
// ═══════════════════════════════════════════════════════
const MobileBody = ({ initialTab = 'score' }) => {
  const [tab, setTab] = useState2(initialTab);
  const tabs = [
    { id:'score',  icon:'🎯', label:'Score',  entity:'session' },
    { id:'player', icon:'👥', label:'Player', entity:'player' },
    { id:'tools',  icon:'🔧', label:'Tools',  entity:'tool' },
    { id:'chat',   icon:'💬', label:'Chat',   entity:'chat' },
  ];
  return (
    <>
      <div style={{
        flex: 1, overflowY:'auto', minHeight: 0,
        background:'var(--bg)',
      }}>
        {tab === 'score' && (
          <div style={{ padding: 12, display:'flex', flexDirection:'column', gap: 12 }}>
            <P.LiveScoringPanel compact/>
            <P.ActionLogTimeline compact/>
          </div>
        )}
        {tab === 'player' && (
          <div style={{ padding: 0 }}>
            <P.PlayerRosterLive compact/>
          </div>
        )}
        {tab === 'tools' && <SessionToolsRail/>}
        {tab === 'chat' && (
          <div style={{ display:'flex', flexDirection:'column', height:'100%' }}>
            <LiveAgentChat/>
          </div>
        )}
      </div>
      {/* Bottom tab switcher */}
      <nav style={{
        display:'flex', flexShrink: 0,
        background:'var(--glass-bg)', backdropFilter:'blur(14px)',
        borderTop:'1px solid var(--border)',
      }}>
        {tabs.map(t => {
          const active = tab === t.id;
          return (
            <button key={t.id} type="button" onClick={() => setTab(t.id)}
              aria-pressed={active}
              style={{
                flex: 1, padding:'8px 4px',
                background: active ? eHsl(t.entity, 0.08) : 'transparent',
                border:'none',
                borderTop: active ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent',
                color: active ? eHsl(t.entity) : 'var(--text-muted)',
                cursor:'pointer',
                display:'flex', flexDirection:'column', alignItems:'center', gap: 1,
                fontFamily:'var(--f-display)', fontSize: 10.5, fontWeight: 800,
              }}>
              <span aria-hidden="true" style={{ fontSize: 16 }}>{t.icon}</span>
              {t.label}
            </button>
          );
        })}
      </nav>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── OVERLAYS ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PauseOverlay = () => (
  <div style={{
    position:'absolute', inset: 0, zIndex: 50,
    background:'rgba(15,12,30,.78)',
    backdropFilter:'blur(8px)',
    display:'flex', alignItems:'center', justifyContent:'center',
    flexDirection:'column', gap: 22, padding: 24,
    boxShadow:`inset 0 0 200px ${eHsl('session', 0.3)}`,
  }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 12, fontWeight: 800,
      color: eHsl('session'), textTransform:'uppercase', letterSpacing:'.16em',
      display:'inline-flex', alignItems:'center', gap: 8,
    }}>
      <span aria-hidden="true" style={{
        width: 10, height: 10, borderRadius:'50%',
        background: eHsl('session'),
        boxShadow:`0 0 14px ${eHsl('session', 0.8)}`,
      }}/>
      Sessione in pausa
    </div>
    <div style={{ textAlign:'center' }}>
      <div style={{
        fontFamily:'var(--f-display)', fontSize: 56, fontWeight: 800,
        color:'#fff', fontVariantNumeric:'tabular-nums',
        textShadow:`0 0 30px ${eHsl('session', 0.6)}`,
        letterSpacing:'-.02em', lineHeight: 1,
      }}>03:42</div>
      <div style={{
        fontFamily:'var(--f-body)', fontSize: 13, color:'rgba(255,255,255,.7)',
        marginTop: 8,
      }}>Tempo di pausa · auto-save attivo</div>
    </div>
    <div style={{ display:'flex', flexDirection:'column', gap: 8, width:'100%', maxWidth: 280 }}>
      <button type="button" style={{
        padding:'12px 20px', borderRadius:'var(--r-md)',
        background: eHsl('session'), color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
        cursor:'pointer',
        boxShadow:`0 6px 22px ${eHsl('session', 0.5)}`,
        display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 6,
      }}><span aria-hidden="true">▶</span>Riprendi partita</button>
      <button type="button" style={{
        padding:'10px 16px', borderRadius:'var(--r-md)',
        background:'transparent', color:'#fff',
        border:'1px solid rgba(255,255,255,.3)',
        fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700,
        cursor:'pointer',
      }}>Pausa lunga · esci salvando</button>
    </div>
  </div>
);

const EndgameDialog = () => (
  <div style={{
    position:'absolute', inset: 0, zIndex: 50,
    background:'rgba(15,12,30,.5)',
    backdropFilter:'blur(4px)',
    display:'flex', alignItems:'flex-end',
  }}>
    <div style={{
      width:'100%', background:'var(--bg-card)',
      borderTopLeftRadius:'var(--r-xl)', borderTopRightRadius:'var(--r-xl)',
      borderTop:`3px solid ${eHsl('session')}`,
      padding:'18px 16px 20px',
      maxHeight:'85%', overflowY:'auto',
      boxShadow:`0 -10px 40px rgba(0,0,0,.3)`,
    }}>
      <div style={{
        width: 36, height: 4, borderRadius: 999,
        background:'var(--border-strong)', margin:'0 auto 14px',
      }}/>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 800,
        color: eHsl('session'), textTransform:'uppercase', letterSpacing:'.1em',
        marginBottom: 4,
      }}>Termina partita?</div>
      <h2 style={{
        fontFamily:'var(--f-display)', fontSize: 19, fontWeight: 800,
        color:'var(--text)', margin:'0 0 4px',
      }}>Riepilogo finale</h2>
      <p style={{
        fontFamily:'var(--f-body)', fontSize: 12.5, color:'var(--text-sec)',
        margin:'0 0 14px',
      }}>Conferma per chiudere la sessione e generare il summary.</p>
      <div style={{ display:'flex', flexDirection:'column', gap: 6, marginBottom: 14 }}>
        {P.ROSTER.map((p, i) => {
          const isWinner = i === 0;
          return (
            <div key={p.id} style={{
              display:'flex', alignItems:'center', gap: 10,
              padding:'10px 12px', borderRadius:'var(--r-md)',
              background: isWinner ? eHsl('session', 0.1) : 'var(--bg-muted)',
              border: isWinner ? `2px solid ${eHsl('session')}` : '1px solid var(--border)',
            }}>
              <div style={{
                width: 28, height: 28, borderRadius:'50%',
                background:`hsl(${p.color}, 60%, 60%)`, color:'#fff',
                display:'flex', alignItems:'center', justifyContent:'center',
                fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              }}>{p.name[0]}</div>
              <div style={{ flex: 1 }}>
                <div style={{
                  fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800,
                  color:'var(--text)',
                }}>{p.name}{isWinner && <span style={{ color: eHsl('session'), marginLeft: 5 }}>🏆</span>}</div>
                <div style={{
                  fontFamily:'var(--f-mono)', fontSize: 9.5, color:'var(--text-muted)',
                  fontWeight: 700,
                }}>{i+1}° posto</div>
              </div>
              <div style={{
                fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
                color: isWinner ? eHsl('session') : 'var(--text)',
                fontVariantNumeric:'tabular-nums',
              }}>{p.score}</div>
            </div>
          );
        })}
      </div>
      <div style={{ display:'flex', gap: 6 }}>
        <button type="button" style={{
          flex: 2, padding:'11px',
          borderRadius:'var(--r-md)',
          background: eHsl('session'), color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
          cursor:'pointer',
          boxShadow:`0 4px 14px ${eHsl('session', 0.4)}`,
        }}>✓ Conferma e vai a riepilogo</button>
        <button type="button" style={{
          flex: 1, padding:'11px',
          borderRadius:'var(--r-md)',
          background:'transparent', color:'var(--text-sec)',
          border:'1px solid var(--border-strong)',
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700, cursor:'pointer',
        }}>Annulla</button>
      </div>
    </div>
  </div>
);

const ConnectionLostBanner = () => (
  <div style={{
    padding:'8px 16px', display:'flex', alignItems:'center', gap: 8,
    background:'hsl(var(--c-event) / .12)',
    borderBottom:'1px solid hsl(var(--c-event) / .3)',
    fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700,
    color:'hsl(var(--c-event))',
  }}>
    <span aria-hidden="true">⚠</span>
    <span style={{ flex: 1 }}>
      <strong>Connessione persa</strong> · auto-retry tra 5s...
    </span>
    <button type="button" style={{
      padding:'3px 9px', borderRadius:'var(--r-pill)',
      background:'hsl(var(--c-event))', color:'#fff', border:'none',
      fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
      cursor:'pointer', textTransform:'uppercase', letterSpacing:'.06em',
    }}>↻ Riprova</button>
  </div>
);

const LoadingDesktop = () => (
  <div style={{ display:'flex', flex: 1, padding: 14, gap: 12 }}>
    <div className="mai-shimmer" style={{ width: 300, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>
    <div style={{ flex: 1, display:'flex', flexDirection:'column', gap: 12 }}>
      <div className="mai-shimmer" style={{ height: 280, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>
      <div className="mai-shimmer" style={{ flex: 1, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>
    </div>
    <div className="mai-shimmer" style={{ width: 380, borderRadius:'var(--r-lg)', background:'var(--bg-muted)' }}/>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── FRAMES ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PhoneSbar = ({ dark }) => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const PhoneScreen = ({ stateOverride, initialTab = 'score' }) => (
  <>
    <PhoneSbar/>
    <div style={{
      flex: 1, display:'flex', flexDirection:'column',
      background:'var(--bg)', position:'relative', overflow:'hidden', minHeight: 0,
    }}>
      {stateOverride === 'connection' && <ConnectionLostBanner/>}
      <P.LiveTopBar compact paused={stateOverride==='paused'}/>
      <MobileBody initialTab={initialTab}/>
      {stateOverride === 'paused' && <PauseOverlay/>}
      {stateOverride === 'endgame' && <EndgameDialog/>}
    </div>
  </>
);

const DesktopFrameInner = ({ stateOverride, emptyTurn }) => (
  <div style={{
    display:'flex', flexDirection:'column',
    minHeight: 720, height: 720,
    background:'var(--bg)', position:'relative', overflow:'hidden',
  }}>
    {stateOverride === 'connection' && <ConnectionLostBanner/>}
    <P.LiveTopBar paused={stateOverride==='paused'}/>
    {stateOverride === 'loading'
      ? <LoadingDesktop/>
      : <DesktopBody emptyTurn={emptyTurn}/>}
    {stateOverride === 'paused' && <PauseOverlay/>}
    {stateOverride === 'endgame' && <EndgameDialog/>}
  </div>
);

const PhoneShell = ({ label, desc, dark, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div className="phone" data-theme={dark ? 'dark' : undefined}>{children}</div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 340,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

const DesktopFrame = ({ label, desc, dark, children }) => (
  <div style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }} data-theme={dark ? 'dark' : undefined}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
    }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark (preferito)</span>}</div>
    <div style={{
      width:'100%', maxWidth: 1400,
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
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/sessions/wing-3/live</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 800,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id:'m1', tab:'score',  label:'01 · Mobile · Score (default)', desc:'Tab Score: ScoringPanel + ActionLog. Top bar compatta 56 con turn indicator collapsed sotto. Bottom tabs 4 modes con entity-color.' },
  { id:'m2', tab:'player', label:'02 · Mobile · Player roster', desc:'4 player card stack — Marco con border-left 4px entity-session + bg @ 8% + dot pulsante. Score grande + delta. Breakdown collapsible.' },
  { id:'m3', tab:'tools',  label:'03 · Mobile · Tools rail', desc:'Grid 2-col tools quick + custom Wingspan + last roll preview entity-tool. CTA "+ Aggiungi tool".' },
  { id:'m4', tab:'chat',   label:'04 · Mobile · Agent chat', desc:'Header agent online + bubble user entity-chat / agent entity-agent · citation kb pip · suggestion chips · input + voice.' },
  { id:'m5', tab:'score', state:'paused', label:'05 · Mobile · Pausa overlay', desc:'Full-screen scuro entity-session glow + timer pausa Quicksand 56 + 2 CTA Riprendi/Pausa lunga.' },
  { id:'m6', tab:'score', state:'endgame', label:'06 · Mobile · Termina dialog', desc:'Bottom-sheet con 4 player score finale, winner highlighted entity-session 2px border, 2 CTA Conferma/Annulla.' },
  { id:'m7', tab:'score', state:'connection', label:'07 · Mobile · Connection lost', desc:'Banner top entity-event "Connessione persa · auto-retry 5s..." + manual retry persistente.' },
  { id:'m8', tab:'score', dark: true, label:'08 · Mobile · Dark mode', desc:'Modalità primaria per live (bassa luminosità). Tutti gli accent entity-session glow più visibili.' },
];

function ThemeToggle() {
  const [dark, setDark] = useState2(true); // default dark per pagina live
  useEffect2(() => {
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
        }}>SP4 wave 2 · #2/3 · Session live ⚡</div>
        <h1>Session live — /sessions/[id]/live</h1>
        <p className="lead">
          La pagina più densa dell'intero SP4: split <strong>3-col desktop</strong> (roster 300 / scoring+log fluid / tools+chat 380) ·
          stack <strong>mobile con bottom tabs 4 modes</strong>. Top bar sticky con TurnIndicator + timer mono + auto-save banner.
          Roster con player corrente highlighted (border-left 4px entity-session + dot pulsante). LiveScoringPanel a tab categorie con
          input grande +/- + quick presets. ActionLog timeline cronologica filterable. Tools/Chat/Note in tabs entity-aware.
          Dark mode è la modalità primaria (bassa luminosità tavolo gioco).
        </p>

        <div className="section-label">Mobile · 375 — 8 stati (default Score · Pausa · Endgame · Connection · Dark)</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc} dark={s.dark}>
              <PhoneScreen stateOverride={s.state} initialTab={s.tab}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — 5 stati chiave (dark = primario)</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="09 · Desktop · Default mid-game" dark
            desc="Split 3-col completo: roster con Marco al turno (border-left + dot pulsante + breakdown expanded) · ScoringPanel categorie Wingspan attiva Eggs · ActionLog 10 eventi filterable · RightCol tab Tools default.">
            <DesktopFrameInner/>
          </DesktopFrame>
          <DesktopFrame label="10 · Desktop · Light mode"
            desc="Stesso layout in light per validare contrasto/legibilità. Dark resta primario per le partite reali.">
            <DesktopFrameInner/>
          </DesktopFrame>
          <DesktopFrame label="11 · Desktop · Pausa overlay" dark
            desc="Overlay full-screen scuro entity-session glow + timer pausa 56 Quicksand + 2 CTA. Top bar e roster restano blurred sotto, leggibili.">
            <DesktopFrameInner stateOverride="paused"/>
          </DesktopFrame>
          <DesktopFrame label="12 · Desktop · Termina partita dialog" dark
            desc="Bottom-sheet con riepilogo 4 player ordered + Marco winner highlighted entity-session 2px + score grande + 2 CTA Conferma/Annulla.">
            <DesktopFrameInner stateOverride="endgame"/>
          </DesktopFrame>
          <DesktopFrame label="13 · Desktop · Empty turn (turno appena iniziato)" dark
            desc="Variante: ScoringPanel con valore 0 come stato iniziale del turno. Player corrente già visibile in roster.">
            <DesktopFrameInner emptyTurn/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        @keyframes mai-pulse-dot-anim {
          0%, 100% { transform: scale(1); opacity: 1; }
          50% { transform: scale(1.4); opacity: .65; }
        }
        .mai-shimmer {
          background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important;
          background-size: 200% 100% !important;
          animation: mai-shimmer-anim 1.4s linear infinite;
        }
        .mai-pulse-dot {
          animation: mai-pulse-dot-anim 1.5s ease-in-out infinite;
          transform-origin: center;
        }
        @media (prefers-reduced-motion: reduce) {
          .mai-pulse-dot { animation: none !important; }
          .mai-shimmer { animation: none !important; }
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
        button:focus-visible, a:focus-visible, input:focus-visible, textarea:focus-visible {
          outline: 2px solid hsl(var(--c-session));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
