/* ===================================================================
   SP7 Game Night — Transition Dialog (sp7-game-night-transition.jsx)
   Opened from: /game-nights/[id]/live  (mockup K, toolbar "Transition ➡️")
   Persona: Marco (host) tra game e game, decisione 30–90 sec.
   Coverage: US-31 G31.14 / G31.15 — Sprint N+1 P1
   Pattern desktop: Modal centered 600×500, 2-column split (recap last | preview next)
   Pattern mobile : fullscreen vertical stack (recap stack-top, preview stack-bottom)

   Stati (6 totali)
   - state-01-transition-default      Default 2-col split (recap Brass + preview Spirit popolati)
   - state-02-rules-loading-skeleton  KB quick-glance ancora in loading (skeleton 3 righe)
   - state-03-rules-load-error        KB fetch failed — fallback "Rules non disponibili offline"
   - state-04-skip-confirm            Submodal nested "Saltare Spirit Island?"
   - state-05-end-night-confirm       Submodal nested "Terminare serata? → Summary"
   - state-06-mobile-fullscreen       Mobile vertical stack invece di 2-col

   Continuità dati con K
   - Ultimo gioco completato: Brass: Birmingham
     winner Davide 178pt · durata 2h 45m · top-3 (Davide 178 · Marco 142 · Giulia 128)
   - Prossimo gioco: Spirit Island · est 90m
   - 3 rules bullet: Phase order Growth→Fast→Slow · Fear deck 9 carte · Power growth 1 minor + 1 major
   - Setup checklist: tabellone · 4 spirit panels · invader deck shuffled · fear pool 9

   Entity color mapping
   - winner banner          = entityHsl('event')   rose
   - score top-3            = entityHsl('player')  violet
   - KB rules quick-glance  = entityHsl('kb')      teal
   - CTA Avvia              = entityHsl('session') indigo
   - Skip submodal          = var(--c-warning)
   - End night submodal     = var(--c-danger)

   Componenti emersi (paths canonical Stage 2 PR #1025)
   - GameTransitionDialog       → apps/web/src/components/features/game-nights/game-transition-dialog/
   - TransitionRecapPanel       → features/game-nights/transition-recap-panel/    (left col)
   - TransitionPreviewPanel     → features/game-nights/transition-preview-panel/  (right col)
   - KBRulesQuickGlance         → components/ui/kb-rules-quick-glance/            (primitive riusabile · 3 bullet · skeleton · offline fallback)
   - SetupChecklist             → components/ui/setup-checklist/                  (primitive riusabile · icone + check)
   - TopThreeScoreList          → features/game-nights/top-three-score-list/      (rank + avatar + score, specifico session/night-recap)
   - TransitionConfirmSubmodal  → features/game-nights/transition-confirm-submodal/

   Riusi pattern
   - PauseOverlay / EndgameDialog (sp4) — backdrop + bottom-sheet mobile
   - ConfirmModal                  — pattern SP6-B
   - MeepleCard variant compact     — game cover preview
   =================================================================== */

const { useState, useEffect, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.event;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ═══════════════════════════════════════════════════════
// FIXTURE — continuità SP7-K (Sabato Padovani · 17 mag)
// ═══════════════════════════════════════════════════════

const LAST_GAME = {
  id:'gs-brass-1',
  title:'Brass: Birmingham',
  publisher:'Roxley',
  emoji:'🏭',
  cover:['hsl(220 35% 28%)','hsl(28 60% 38%)'],
  duration:'2h 45m',
  endedAt:'22:55',
  winner: { id:'p-davide', initials:'DC', name:'Davide',  color: 200, score: 178 },
  topThree: [
    { id:'p-davide', initials:'DC', name:'Davide', color: 200, score: 178, delta: '+50 industria' },
    { id:'p-marco',  initials:'MR', name:'Marco',  color: 262, score: 142, delta: '+12 network' },
    { id:'p-giulia', initials:'GM', name:'Giulia', color: 10,  score: 128, delta: '+8 carbone' },
  ],
};

const NEXT_GAME = {
  id:'gs-spirit-1',
  title:'Spirit Island',
  publisher:'GMT · co-op',
  emoji:'🌋',
  cover:['hsl(210 50% 30%)','hsl(150 50% 38%)'],
  estimated:'90m',
  weight: 4.08,
  rules: [
    { icon:'🔁', text:'Phase order · Growth → Fast → Slow', src:'§ 4.2' },
    { icon:'😱', text:'Fear deck · 9 carte iniziali (level 1/2/3)', src:'§ 5.1' },
    { icon:'⚡', text:'Power growth · 1 minor + 1 major per round', src:'§ 6.3' },
  ],
  setup: [
    { icon:'🗺️', text:'Tabellone · 4 isole', done: true },
    { icon:'🧝', text:'Spirit panels (4 spiriti scelti)', done: true },
    { icon:'🃏', text:'Invader deck shuffled · stage I', done: false },
    { icon:'💀', text:'Fear pool · 9 token', done: false },
  ],
};

// ═══════════════════════════════════════════════════════
// PRIMITIVES
// ═══════════════════════════════════════════════════════

const Avatar = ({ player, size = 22, ring }) => (
  <span style={{
    width: size, height: size, borderRadius:'50%',
    background:`hsl(${player.color}, 60%, 55%)`, color:'#fff',
    fontFamily:'var(--f-display)', fontWeight: 800,
    fontSize: size <= 18 ? 8 : size <= 24 ? 9 : 11,
    display:'inline-flex', alignItems:'center', justifyContent:'center',
    border:`2px solid ${ring || 'var(--bg-card)'}`,
    flexShrink: 0,
  }}>{player.initials}</span>
);

const MonoKicker = ({ children, color = 'var(--text-muted)', size = 10 }) => (
  <span style={{
    fontFamily:'var(--f-mono)', fontSize: size, fontWeight: 800,
    color, textTransform:'uppercase', letterSpacing:'.08em',
  }}>{children}</span>
);

const PulsingDot = ({ color, size = 8 }) => (
  <span aria-hidden="true" style={{
    width: size, height: size, borderRadius:'50%',
    background: color, flexShrink: 0,
    animation:'sp7Pulse 1.6s var(--ease-out) infinite',
  }}/>
);

// ═══════════════════════════════════════════════════════
// LEFT COL — TransitionRecapPanel (Ultimo gioco)
// /* v2: TransitionRecapPanel */
// ═══════════════════════════════════════════════════════

const RecapPanel = ({ game = LAST_GAME, mobile }) => (
  <div style={{
    display:'flex', flexDirection:'column',
    background: entityHsl('event', 0.04),
    borderRight: mobile ? 'none' : `1px solid var(--border)`,
    borderBottom: mobile ? `1px solid var(--border)` : 'none',
    padding: mobile ? '16px 18px 14px' : '18px 18px 16px',
    gap: 12, minWidth: 0,
  }}>
    {/* Section header */}
    <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
      <span aria-hidden="true" style={{ fontSize: 14 }}>⏪</span>
      <MonoKicker color={entityHsl('event')}>Ultimo gioco</MonoKicker>
    </div>

    {/* Cover + title */}
    <div style={{
      display:'flex', gap: 12, alignItems:'center',
    }}>
      <div style={{
        width: 56, height: 56, borderRadius:'var(--r-md)',
        background:`linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 26, flexShrink: 0,
        boxShadow:'var(--shadow-sm)',
      }} aria-hidden="true">{game.emoji}</div>
      <div style={{ minWidth: 0, flex: 1 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
          color:'var(--text)', lineHeight: 1.2,
          whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
        }}>{game.title}</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color:'var(--text-muted)', marginTop: 2,
        }}>{game.publisher} · finito {game.endedAt}</div>
      </div>
    </div>

    {/* Winner banner — entityHsl('event') */}
    <div style={{
      padding:'10px 12px', borderRadius:'var(--r-md)',
      background: entityHsl('event', 0.1),
      border:`1px solid ${entityHsl('event', 0.32)}`,
      display:'flex', alignItems:'center', gap: 10,
      boxShadow:`0 0 0 3px ${entityHsl('event', 0.06)}`,
    }}>
      <span aria-hidden="true" style={{ fontSize: 22 }}>🏆</span>
      <div style={{ flex: 1, minWidth: 0 }}>
        <MonoKicker size={9} color={entityHsl('event')}>Winner · {game.duration}</MonoKicker>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          color:'var(--text)', marginTop: 1,
        }}>{game.winner.name}{' '}
          <span style={{
            color: entityHsl('event'),
            fontVariantNumeric:'tabular-nums',
          }}>{game.winner.score}pt</span>
        </div>
      </div>
      <Avatar player={game.winner} size={32} ring={entityHsl('event', 0.4)}/>
    </div>

    {/* Top-3 score list */}
    <div style={{ display:'flex', flexDirection:'column', gap: 4 }}>
      <MonoKicker size={9.5}>Classifica · Top 3</MonoKicker>
      <div style={{ display:'flex', flexDirection:'column', gap: 4 }}>
        {game.topThree.map((p, i) => {
          const isWinner = i === 0;
          return (
            <div key={p.id} style={{
              display:'flex', alignItems:'center', gap: 9,
              padding:'7px 10px',
              borderRadius:'var(--r-sm)',
              background: isWinner ? entityHsl('event', 0.08) : 'var(--bg-card)',
              border: isWinner
                ? `1px solid ${entityHsl('event', 0.28)}`
                : '1px solid var(--border)',
            }}>
              <span style={{
                width: 18, height: 18, borderRadius:'50%',
                background: isWinner
                  ? entityHsl('event')
                  : 'var(--bg-muted)',
                color: isWinner ? '#fff' : 'var(--text-sec)',
                display:'inline-flex', alignItems:'center', justifyContent:'center',
                fontFamily:'var(--f-display)', fontSize: 10, fontWeight: 800,
                flexShrink: 0,
              }}>{i + 1}</span>
              <Avatar player={p} size={22}/>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{
                  fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
                  color:'var(--text)',
                }}>{p.name}</div>
                <div style={{
                  fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
                  color:'var(--text-muted)',
                  whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
                }}>{p.delta}</div>
              </div>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 14, fontWeight: 800,
                color: isWinner ? entityHsl('event') : 'var(--text)',
                fontVariantNumeric:'tabular-nums',
              }}>{p.score}</span>
            </div>
          );
        })}
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// RIGHT COL — TransitionPreviewPanel (Prossimo gioco)
// /* v2: TransitionPreviewPanel + KBRulesQuickGlance + SetupChecklist */
// ═══════════════════════════════════════════════════════

const RulesQuickGlance = ({ game = NEXT_GAME, status = 'ok' }) => {
  if (status === 'loading') {
    return (
      <div style={{ display:'flex', flexDirection:'column', gap: 4 }}>
        <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
          <span aria-hidden="true" style={{ fontSize: 13 }}>📄</span>
          <MonoKicker color={entityHsl('kb')}>Rules quick-glance · loading…</MonoKicker>
        </div>
        <div style={{ display:'flex', flexDirection:'column', gap: 5 }}>
          {[0,1,2].map(i => (
            <div key={i} className="sp7-shimmer" style={{
              height: 28, borderRadius:'var(--r-sm)',
              background:'var(--bg-muted)',
            }}/>
          ))}
        </div>
        <MonoKicker size={9}>Fetching from KB · spirit-island-rules-v2</MonoKicker>
      </div>
    );
  }
  if (status === 'error') {
    return (
      <div style={{ display:'flex', flexDirection:'column', gap: 6 }}>
        <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
          <span aria-hidden="true" style={{ fontSize: 13 }}>📄</span>
          <MonoKicker color={entityHsl('kb')}>Rules quick-glance</MonoKicker>
        </div>
        <div role="alert" style={{
          padding:'10px 12px',
          borderRadius:'var(--r-md)',
          background:'hsl(var(--c-danger) / 0.06)',
          border:'1px dashed hsl(var(--c-danger) / 0.35)',
          display:'flex', gap: 9, alignItems:'flex-start',
        }}>
          <span aria-hidden="true" style={{
            fontSize: 16, color:'hsl(var(--c-danger))', marginTop: 1,
          }}>⚠</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
              color:'var(--text)',
            }}>Rules non disponibili offline</div>
            <div style={{
              fontSize: 11, color:'var(--text-sec)', lineHeight: 1.45, marginTop: 2,
            }}>Tu sei offline o la KB non è ancora indicizzata. Procedi senza quick-glance, oppure riprova.</div>
            <div style={{ display:'flex', gap: 6, marginTop: 6 }}>
              <button type="button" style={{
                padding:'3px 9px', borderRadius:'var(--r-pill)',
                background:'hsl(var(--c-danger))', color:'#fff', border:'none',
                fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
                cursor:'pointer', textTransform:'uppercase', letterSpacing:'.06em',
              }}>↻ Riprova</button>
              <button type="button" style={{
                padding:'3px 9px', borderRadius:'var(--r-pill)',
                background:'transparent', border:'1px solid var(--border)',
                color:'var(--text-sec)',
                fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 700,
                cursor:'pointer', textTransform:'uppercase', letterSpacing:'.06em',
              }}>Procedi senza</button>
            </div>
          </div>
        </div>
      </div>
    );
  }
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 5 }}>
      <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
        <span aria-hidden="true" style={{ fontSize: 13 }}>📄</span>
        <MonoKicker color={entityHsl('kb')}>Rules quick-glance · 3 bullet</MonoKicker>
      </div>
      <ul style={{ margin: 0, padding: 0, listStyle:'none', display:'flex', flexDirection:'column', gap: 5 }}>
        {game.rules.map((r, i) => (
          <li key={i} style={{
            display:'flex', alignItems:'center', gap: 8,
            padding:'7px 10px',
            borderRadius:'var(--r-sm)',
            background: entityHsl('kb', 0.06),
            border:`1px solid ${entityHsl('kb', 0.22)}`,
          }}>
            <span aria-hidden="true" style={{ fontSize: 13, flexShrink: 0 }}>{r.icon}</span>
            <span style={{
              flex: 1, minWidth: 0,
              fontFamily:'var(--f-body)', fontSize: 12, fontWeight: 600,
              color:'var(--text)', lineHeight: 1.35,
            }}>{r.text}</span>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              color: entityHsl('kb'), letterSpacing:'.05em',
              padding:'1px 6px', borderRadius:'var(--r-pill)',
              background: entityHsl('kb', 0.12),
              flexShrink: 0,
            }}>{r.src}</span>
          </li>
        ))}
      </ul>
    </div>
  );
};

const SetupChecklist = ({ items = NEXT_GAME.setup }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 5 }}>
    <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
      <span aria-hidden="true" style={{ fontSize: 13 }}>📋</span>
      <MonoKicker>Setup checklist · {items.filter(i => i.done).length}/{items.length}</MonoKicker>
    </div>
    <ul style={{ margin: 0, padding: 0, listStyle:'none', display:'flex', flexDirection:'column', gap: 4 }}>
      {items.map((it, i) => (
        <li key={i} style={{
          display:'flex', alignItems:'center', gap: 9,
          padding:'5px 10px',
          borderRadius:'var(--r-sm)',
          background: it.done ? entityHsl('toolkit', 0.06) : 'var(--bg-muted)',
          border: it.done
            ? `1px solid ${entityHsl('toolkit', 0.25)}`
            : '1px solid var(--border)',
          opacity: it.done ? 1 : 0.92,
        }}>
          <span aria-hidden="true" style={{
            width: 18, height: 18, borderRadius:'var(--r-sm)',
            background: it.done ? entityHsl('toolkit') : 'transparent',
            border: it.done ? 'none' : '1.5px solid var(--border-strong)',
            color:'#fff',
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            fontSize: 11, fontWeight: 800, flexShrink: 0,
          }}>{it.done ? '✓' : ''}</span>
          <span aria-hidden="true" style={{ fontSize: 13 }}>{it.icon}</span>
          <span style={{
            flex: 1, minWidth: 0,
            fontFamily:'var(--f-body)', fontSize: 11.5, fontWeight: 600,
            color: it.done ? 'var(--text)' : 'var(--text-sec)',
            textDecoration: it.done ? 'line-through' : 'none',
            textDecorationColor: 'var(--text-muted)',
            textDecorationThickness: '1px',
          }}>{it.text}</span>
        </li>
      ))}
    </ul>
  </div>
);

const PreviewPanel = ({ game = NEXT_GAME, rulesStatus = 'ok', mobile }) => (
  <div style={{
    flex: 1, display:'flex', flexDirection:'column',
    padding: mobile ? '16px 18px 14px' : '18px 18px 16px',
    gap: 12, minWidth: 0,
    background:'var(--bg-card)',
  }}>
    {/* Section header */}
    <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
      <span aria-hidden="true" style={{ fontSize: 14 }}>⏩</span>
      <MonoKicker color={entityHsl('session')}>Prossimo gioco</MonoKicker>
    </div>

    {/* Cover + title */}
    <div style={{ display:'flex', gap: 12, alignItems:'center' }}>
      <div style={{
        width: 56, height: 56, borderRadius:'var(--r-md)',
        background:`linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 26, flexShrink: 0,
        boxShadow:'var(--shadow-sm)',
      }} aria-hidden="true">{game.emoji}</div>
      <div style={{ minWidth: 0, flex: 1 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
          color:'var(--text)', lineHeight: 1.2,
          whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
        }}>{game.title}</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color:'var(--text-muted)', marginTop: 2,
        }}>{game.publisher} · weight {game.weight.toFixed(2)}</div>
      </div>
      <div style={{
        padding:'4px 9px', borderRadius:'var(--r-pill)',
        background: entityHsl('session', 0.12),
        border:`1px solid ${entityHsl('session', 0.3)}`,
        color: entityHsl('session'),
        fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
        fontVariantNumeric:'tabular-nums',
        display:'inline-flex', alignItems:'center', gap: 4,
        flexShrink: 0,
      }}>
        <span aria-hidden="true">⏱</span>
        ~{game.estimated}
      </div>
    </div>

    <RulesQuickGlance game={game} status={rulesStatus}/>
    <SetupChecklist items={game.setup}/>
  </div>
);

// ═══════════════════════════════════════════════════════
// SUBMODAL — Skip / End night confirm
// /* v2: TransitionConfirmSubmodal */
// ═══════════════════════════════════════════════════════

const ConfirmSubmodal = ({ tone = 'warning', icon, title, body, primaryLabel, primaryIcon }) => {
  const fg = tone === 'danger' ? 'hsl(var(--c-danger))' : 'hsl(var(--c-warning))';
  const bg = tone === 'danger' ? 'hsl(var(--c-danger) / 0.12)' : 'hsl(var(--c-warning) / 0.12)';
  const bd = tone === 'danger' ? 'hsl(var(--c-danger) / 0.32)' : 'hsl(var(--c-warning) / 0.32)';
  return (
    <div style={{
      position:'absolute', inset: 0, zIndex: 60,
      display:'flex', alignItems:'center', justifyContent:'center',
      background:'rgba(0,0,0,0.45)',
      backdropFilter:'blur(6px)',
      padding: 16,
    }}>
      <div role="alertdialog" aria-modal="true" style={{
        width:'100%', maxWidth: 380,
        background:'var(--bg-card)',
        border:`1px solid ${bd}`,
        borderRadius:'var(--r-xl)',
        boxShadow:'var(--shadow-lg)',
        padding: 18,
        display:'flex', flexDirection:'column', gap: 12,
      }}>
        <div style={{
          width: 44, height: 44, borderRadius:'50%',
          background: bg, color: fg,
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 22, flexShrink: 0,
        }} aria-hidden="true">{icon}</div>
        <div>
          <h3 style={{
            margin: 0, fontFamily:'var(--f-display)', fontSize: 16,
            fontWeight: 800, color:'var(--text)', letterSpacing:'-.01em',
          }}>{title}</h3>
          <p style={{
            margin:'6px 0 0', fontSize: 12.5, color:'var(--text-sec)', lineHeight: 1.55,
          }}>{body}</p>
        </div>
        <div style={{ display:'flex', gap: 8 }}>
          <button type="button" style={{
            flex: 2, padding:'11px',
            borderRadius:'var(--r-md)',
            background: fg, color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            cursor:'pointer',
            boxShadow:`0 4px 14px ${tone === 'danger' ? 'hsl(var(--c-danger) / 0.35)' : 'hsl(var(--c-warning) / 0.35)'}`,
            display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 6,
          }}><span aria-hidden="true">{primaryIcon}</span>{primaryLabel}</button>
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
};

// ═══════════════════════════════════════════════════════
// MAIN DIALOG — GameTransitionDialog
// /* v2: GameTransitionDialog */
// ═══════════════════════════════════════════════════════

const DialogHeader = ({ mobile, onClose }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 10,
    padding: mobile ? '14px 16px' : '14px 18px',
    borderBottom:'1px solid var(--border)',
    background:'var(--glass-bg)',
    backdropFilter:'blur(10px)',
    flexShrink: 0,
  }}>
    <div style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('session')})`,
      color:'#fff', display:'inline-flex', alignItems:'center', justifyContent:'center',
      fontSize: 16, flexShrink: 0,
    }} aria-hidden="true">➡️</div>
    <div style={{ flex: 1, minWidth: 0 }}>
      <MonoKicker size={9.5} color={entityHsl('event')}>Game transition · #GN-042</MonoKicker>
      <div id="sp7-l-dialog-title" style={{
        fontFamily:'var(--f-display)', fontSize: 14.5, fontWeight: 800,
        color:'var(--text)', letterSpacing:'-.01em',
      }}>Pronti per il prossimo gioco?</div>
    </div>
    <button type="button" aria-label="Chiudi" onClick={onClose} style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text-sec)', fontSize: 16, cursor:'pointer',
      flexShrink: 0,
    }}>×</button>
  </div>
);

const DialogFooter = ({ mobile }) => (
  <div style={{
    display:'flex', gap: 8, alignItems:'center', flexWrap: mobile ? 'wrap' : 'nowrap',
    padding: mobile ? '12px 16px' : '12px 18px',
    borderTop:'1px solid var(--border)',
    background:'var(--bg-muted)',
    flexShrink: 0,
  }}>
    <button type="button" style={{
      padding:'9px 13px', borderRadius:'var(--r-md)',
      background:'transparent', color:'hsl(var(--c-warning))',
      border:'1px solid hsl(var(--c-warning) / 0.4)',
      fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700, cursor:'pointer',
      display:'inline-flex', alignItems:'center', gap: 5,
    }}><span aria-hidden="true">⏭</span>Salta gioco</button>
    <button type="button" style={{
      padding:'9px 13px', borderRadius:'var(--r-md)',
      background:'transparent', color:'hsl(var(--c-danger))',
      border:'1px solid hsl(var(--c-danger) / 0.4)',
      fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700, cursor:'pointer',
      display:'inline-flex', alignItems:'center', gap: 5,
    }}><span aria-hidden="true">🛑</span>Termina serata qui</button>
    <div style={{ flex: 1 }}/>
    <button type="button" style={{
      padding:'11px 16px', borderRadius:'var(--r-md)',
      background:`linear-gradient(135deg, ${entityHsl('session')}, ${entityHsl('chat')})`,
      color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 13.5, fontWeight: 800, cursor:'pointer',
      boxShadow:`0 6px 18px ${entityHsl('session', 0.4)}`,
      display:'inline-flex', alignItems:'center', gap: 6,
      flexShrink: 0,
    }}>▶ Avvia prossima session
      <span aria-hidden="true" style={{ fontSize: 14 }}>→</span>
    </button>
  </div>
);

const GameTransitionDialog = ({ rulesStatus = 'ok', mobile, submodal }) => (
  <div role="dialog" aria-modal="true" aria-labelledby="sp7-l-dialog-title"
    style={{
      width: mobile ? '100%' : 600,
      height: mobile ? '100%' : 500,
      maxHeight: '100%',
      background:'var(--bg-card)',
      borderRadius: mobile ? 0 : 'var(--r-xl)',
      border: mobile ? 'none' : '1px solid var(--border)',
      boxShadow: mobile ? 'none' : 'var(--shadow-lg)',
      overflow:'hidden', position:'relative',
      display:'flex', flexDirection:'column',
    }}>
    <DialogHeader mobile={mobile}/>
    <div style={{
      flex: 1, minHeight: 0, overflowY:'auto',
      display: mobile ? 'flex' : 'grid',
      flexDirection: mobile ? 'column' : undefined,
      gridTemplateColumns: mobile ? undefined : '1fr 1fr',
    }}>
      <RecapPanel mobile={mobile}/>
      <PreviewPanel rulesStatus={rulesStatus} mobile={mobile}/>
    </div>
    <DialogFooter mobile={mobile}/>

    {submodal === 'skip' && (
      <ConfirmSubmodal
        tone="warning"
        icon="⏭"
        title="Saltare Spirit Island?"
        body="Verrà rimosso dai planned games di questa serata. Resterà in libreria ma il diary non avrà entries per questa session."
        primaryLabel="Salta e prossimo"
        primaryIcon="⏭"
      />
    )}
    {submodal === 'end' && (
      <ConfirmSubmodal
        tone="danger"
        icon="🛑"
        title="Terminare la serata qui?"
        body="Andrai al Summary con i 2 game registrati. Gli upcoming games verranno archiviati come 'non giocati'. Il diary sarà finalizzato."
        primaryLabel="Termina · vai a Summary"
        primaryIcon="🛑"
      />
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// BLURRED LIVE HUB BACKDROP (preview only)
// — restituisce un mini live-hub semi-trasparente sotto al dialog
// ═══════════════════════════════════════════════════════

const LiveHubBackdrop = () => (
  <div aria-hidden="true" style={{
    position:'absolute', inset: 0,
    background:'var(--bg)',
    overflow:'hidden',
  }}>
    {/* Mock top bar */}
    <div style={{
      display:'flex', alignItems:'center', gap: 10,
      padding:'10px 16px',
      background:'var(--bg-card)',
      borderBottom:`1px solid ${entityHsl('event', 0.25)}`,
    }}>
      <div style={{
        width: 30, height: 30, borderRadius:'var(--r-md)',
        background:'var(--bg-muted)',
      }}/>
      <div style={{ flex: 1 }}>
        <div style={{
          height: 8, width: 100, borderRadius: 4,
          background: entityHsl('event', 0.2), marginBottom: 5,
        }}/>
        <div style={{
          height: 10, width: 200, borderRadius: 4,
          background:'var(--bg-muted)',
        }}/>
      </div>
      <div style={{ display:'flex', gap: 4 }}>
        {[0,1,2].map(i => (
          <div key={i} style={{
            width: 34, height: 30, borderRadius:'var(--r-md)',
            background:'var(--bg-muted)',
          }}/>
        ))}
      </div>
    </div>
    {/* Mock 3-pane */}
    <div style={{ display:'flex', height:'calc(100% - 50px)' }}>
      <div style={{
        width: 200, padding: 10, borderRight:'1px solid var(--border)',
        display:'flex', flexDirection:'column', gap: 8,
      }}>
        {[0,1,2].map(i => (
          <div key={i} style={{
            height: 56, borderRadius:'var(--r-md)',
            background: i === 1 ? entityHsl('event', 0.06) : 'var(--bg-card)',
            border: '1px solid var(--border)',
            borderLeft: `4px solid ${i===0 ? entityHsl('toolkit') : i===1 ? entityHsl('event') : entityHsl('game')}`,
          }}/>
        ))}
      </div>
      <div style={{ flex: 1, padding: 14 }}>
        <div style={{
          height:'70%', borderRadius:'var(--r-lg)',
          background:`linear-gradient(135deg, ${entityHsl('event', 0.18)}, ${entityHsl('session', 0.1)})`,
          border:`1px solid ${entityHsl('event', 0.3)}`,
        }}/>
      </div>
      <div style={{
        width: 220, padding: 10, borderLeft:'1px solid var(--border)',
      }}>
        <div style={{
          height: 12, width: 100, borderRadius: 4,
          background: entityHsl('event', 0.18), marginBottom: 10,
        }}/>
        {[0,1,2,3].map(i => (
          <div key={i} style={{
            height: 36, marginBottom: 5,
            borderRadius:'var(--r-sm)',
            background:'var(--bg-muted)',
            borderLeft: `2px solid ${entityHsl('session', 0.3)}`,
          }}/>
        ))}
      </div>
    </div>
    {/* Dim overlay above backdrop */}
    <div style={{
      position:'absolute', inset: 0,
      background:'rgba(0,0,0,0.4)',
      backdropFilter:'blur(8px)',
    }}/>
  </div>
);

// ═══════════════════════════════════════════════════════
// FRAMES + HARNESS
// ═══════════════════════════════════════════════════════

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>22:55</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const DesktopFrame = ({ id, label, desc, gherkin, children }) => (
  <section id={id} data-screen-label={`SP7-L · ${label}`} className="desktop-shell">
    <div className="state-caption">
      <span className="state-id">{id.replace('state-', '#')}</span>
      <span className="state-label">🖥️ {label}</span>
      {gherkin && <span className="gherkin">{gherkin}</span>}
    </div>
    <div className="desktop-frame">
      <div className="desktop-bar">
        <span className="traffic"/><span className="traffic"/><span className="traffic"/>
        <span className="url">meepleai.app/game-nights/gn-padovani-may17/live · transition</span>
      </div>
      <div style={{
        background:'var(--bg)', position:'relative', overflow:'hidden',
        height: 600,
      }}>
        <LiveHubBackdrop/>
        <div style={{
          position:'absolute', inset: 0, zIndex: 30,
          display:'flex', alignItems:'center', justifyContent:'center',
          padding: 24,
        }}>
          {children}
        </div>
      </div>
    </div>
    {desc && <p className="state-desc">{desc}</p>}
  </section>
);

const PhoneShell = ({ id, label, desc, gherkin, children }) => (
  <section id={id} data-screen-label={`SP7-L · ${label}`} className="phone-shell">
    <div className="state-caption">
      <span className="state-id">{id.replace('state-', '#')}</span>
      <span className="state-label">📱 {label}</span>
      {gherkin && <span className="gherkin">{gherkin}</span>}
    </div>
    <div className="phone">
      <PhoneSbar/>
      <div style={{
        flex: 1, display:'flex', flexDirection:'column',
        background:'var(--bg)', position:'relative', overflow:'hidden', minHeight: 0,
      }}>
        {children}
      </div>
    </div>
    {desc && <p className="state-desc" style={{ maxWidth: 360 }}>{desc}</p>}
  </section>
);

// ═══════════════════════════════════════════════════════
// APP
// ═══════════════════════════════════════════════════════

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "showStateAnchors": true,
  "theme": "light"
}/*EDITMODE-END*/;

const App = () => {
  const [theme, setTheme] = useState(() => {
    const saved = localStorage.getItem('mai-theme');
    return saved || document.documentElement.getAttribute('data-theme') || 'light';
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
      <header style={{
        maxWidth: 1440, margin:'0 auto 32px',
        display:'flex', alignItems:'flex-start', gap: 16, flexWrap:'wrap',
      }}>
        <div style={{ display:'flex', alignItems:'center', gap: 12, flex: 1, minWidth: 0 }}>
          <div style={{
            width: 40, height: 40, borderRadius: 10,
            background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('session')})`,
            color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
            fontWeight: 800, fontFamily:'var(--f-display)', fontSize: 18,
          }}>L</div>
          <div>
            <div style={{
              display:'flex', alignItems:'center', gap: 8, marginBottom: 2, flexWrap:'wrap',
            }}>
              <span style={{
                padding:'2px 8px', borderRadius:'var(--r-pill)',
                background: entityHsl('event', 0.12),
                color: entityHsl('event'),
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform:'uppercase', letterSpacing:'.08em',
                border:`1px solid ${entityHsl('event', 0.22)}`,
              }}>SP7 · Mockup L · Wave 1+</span>
              <MonoKicker size={10}>US-31.L · Issue #487 · P1</MonoKicker>
            </div>
            <div style={{
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 22,
              color:'var(--text)', letterSpacing:'-.01em',
            }}>Game Transition · Dialog</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
              fontWeight: 600, marginTop: 2,
            }}>
              opened-from <code>/game-nights/[id]/live</code> (toolbar Transition) · modal 600×500 desktop / fullscreen mobile · 2-col split (recap | preview)
            </div>
          </div>
        </div>
        <button type="button" onClick={() => setTheme(t => t === 'light' ? 'dark' : 'light')}
          style={{
            padding:'8px 14px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text)', fontFamily:'var(--f-display)',
            fontSize: 12, fontWeight: 800, cursor:'pointer',
            display:'inline-flex', alignItems:'center', gap: 6,
          }}>
          <span aria-hidden="true">🌗</span>
          {theme === 'light' ? 'Light' : 'Dark'}
        </button>
      </header>

      <main style={{
        maxWidth: 1440, margin:'0 auto',
        display:'flex', flexDirection:'column', gap: 56,
      }}>
        {/* Desktop section · 5 states */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('event') }}/>
            <h2 className="section-title">Desktop · modal 600×500 — 5 stati</h2>
            <span className="section-meta">centered su backdrop live-hub blurred</span>
          </div>
          <div style={{ display:'flex', flexDirection:'column', gap: 32 }}>
            <DesktopFrame
              id="state-01-transition-default"
              label="01 · Default 2-col split · recap Brass + preview Spirit popolati"
              gherkin="G31.14"
              desc="Recap left (winner Davide 178pt rose banner + top-3 score + durata 2h45m) | Preview right (cover Spirit + weight 4.08 + 3 bullet rules teal + setup checklist 2/4 done). Footer 3 CTA (Salta · Termina · Avvia primary).">
              <GameTransitionDialog/>
            </DesktopFrame>

            <DesktopFrame
              id="state-02-rules-loading-skeleton"
              label="02 · KB rules quick-glance in loading · skeleton 3 righe"
              gherkin="G31.15 (loading)"
              desc="Stato transitorio mentre KB index fetcha le rules. 3 shimmer bar al posto dei bullet · subtitle 'Fetching from KB · spirit-island-rules-v2'. Tutto il resto del dialog resta funzionante (setup checklist + CTA).">
              <GameTransitionDialog rulesStatus="loading"/>
            </DesktopFrame>

            <DesktopFrame
              id="state-03-rules-load-error"
              label="03 · KB fetch failed · fallback 'Rules non disponibili offline' + retry"
              gherkin="G31.15 (fallback)"
              desc="Banner dashed danger con icon ⚠ + body + 2 micro-CTA (Riprova · Procedi senza). Setup checklist e CTA primary restano abilitati: si può iniziare anche senza quick-glance.">
              <GameTransitionDialog rulesStatus="error"/>
            </DesktopFrame>

            <DesktopFrame
              id="state-04-skip-confirm"
              label="04 · Submodal nested · 'Saltare Spirit Island?' (warning amber)"
              gherkin="G31.14 (skip)"
              desc="ConfirmModal centered sopra il dialog principale. Icon ⏭ amber · 'Verrà rimosso dai planned · resterà in libreria'. 2 CTA (Salta e prossimo · Annulla).">
              <GameTransitionDialog submodal="skip"/>
            </DesktopFrame>

            <DesktopFrame
              id="state-05-end-night-confirm"
              label="05 · Submodal nested · 'Terminare serata qui?' (danger rosso)"
              gherkin="G31.14 (end)"
              desc="ConfirmModal centered sopra il dialog principale. Icon 🛑 danger · 'Andrai al Summary · gli upcoming verranno archiviati'. 2 CTA (Termina · vai a Summary · Annulla).">
              <GameTransitionDialog submodal="end"/>
            </DesktopFrame>
          </div>
        </div>

        {/* Mobile section · state-06 */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('session') }}/>
            <h2 className="section-title">Mobile · 390 fullscreen — 1 stato</h2>
            <span className="section-meta">vertical stack: recap-top, preview-bottom</span>
          </div>
          <div className="mobile-grid">
            <PhoneShell
              id="state-06-mobile-fullscreen"
              label="06 · Mobile fullscreen · vertical stack invece di 2-col"
              gherkin="G31.14 (mobile)"
              desc="Dialog fullscreen senza border-radius. Recap panel stack-top (con border-bottom invece di border-right), Preview panel stack-bottom. Footer wrap su 2 righe: top [Salta · Termina], bottom CTA Avvia full-width.">
              <GameTransitionDialog mobile/>
            </PhoneShell>
          </div>
        </div>

        {/* Pattern note */}
        <footer style={{
          padding:'24px 0', borderTop:'1px solid var(--border)',
          display:'flex', flexDirection:'column', gap: 10, maxWidth: 880,
        }}>
          <MonoKicker>Pattern note</MonoKicker>
          <ul style={{
            margin: 0, paddingLeft: 18,
            fontSize: 12.5, color:'var(--text-sec)', lineHeight: 1.7,
          }}>
            <li>Modal 600×500 centered desktop con backdrop di anteprima live-hub blurred (continuità visiva con K · mostra che la transition NON sostituisce la pagina, è un overlay).</li>
            <li>Mobile fullscreen: dialog occupa 100% del viewport (no border-radius, no shadow). Layout cambia da grid 2-col a flex column con border-bottom invece di border-right tra recap/preview.</li>
            <li>RulesQuickGlance ha 3 stati: <code>ok</code> (3 bullet teal con § page reference), <code>loading</code> (3 shimmer + subtitle), <code>error</code> (banner dashed danger + 2 micro-CTA). CTA primary "Avvia" SEMPRE abilitata anche con KB error (fallback graceful).</li>
            <li>SetupChecklist: 2/4 done di default (tabellone + spirit panels), restanti 2 (invader deck + fear pool) sono "da fare" — non bloccano la transition ma sono visivamente in muted/dashed.</li>
            <li>Submodal nested (z-index 60) sopra il dialog principale (z-index 30): backdrop dim aggiuntivo. ConfirmModal pattern SP6-B con tone warning (skip) o danger (end). 2 CTA primary+annulla.</li>
            <li>Top-3 score list: winner highlighted entityHsl('event') 8% bg + 28% border + numero rank pieno. Resto in card neutro. Tabular-nums per allineamento score.</li>
            <li>Light + dark via <code>[data-theme="dark"]</code> + MutationObserver pattern (stesso K). Solo <code>entityHsl()</code> + <code>var(--c-*)</code> semantici · game cover gradients sono fixture decorativi (pattern accettato come K).</li>
            <li>Anchor id <code>state-NN-...</code> standard SP7 + <code>data-screen-label</code> per review meeting.</li>
          </ul>
        </footer>
      </main>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
