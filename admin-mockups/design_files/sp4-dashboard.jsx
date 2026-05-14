/* MeepleAI Pre-Stage-3 (#1149) — Dashboard (authenticated, forward-design)
   Route: /dashboard (authenticated)
   File: admin-mockups/design_files/sp4-dashboard.{html,jsx}

   Pattern: forward-design — diverge dall'implementazione corrente DashboardClient.tsx
   (PR #309 — Gaming Hub). Layout: Greeting hero + 5 entity sections (Games/Players/
   Agents/Sessions/Events). Mobile stacked, desktop 2×2 grid + Events full-width.

   FEATURES KEPT da DashboardClient.tsx PR #309:
   - ✅ Greeting personalizzato → hero "Bentornato, {name}"
   - ✅ Live session indicator → integrato nel Sessions section header

   FEATURES DROPPED:
   - ❌ Chat recent cards → ora coperto da /chat + ChatSlideOverPanel (separation of concerns)
   - ❌ Friends row standalone → merged into Players section

   FEATURES ADDED vs current:
   - ➕ Games section (primary entity overview, mai stata sulla dashboard)
   - ➕ Agents section overview (currently nessuna presenza dashboard)
   - ➕ Events upcoming (currently nessuna presenza dashboard)

   IMPLICATION: Stage 3 cluster `dashboard` (#1026) sarà re-implementation completa
   (NON conformity micro-fix). DashboardClient.tsx PR #309 viene rimpiazzato.

   Componenti v2 nuovi flaggati:
   - DashboardHero        → apps/web/src/components/ui/v2/dashboard-hero/
   - DashboardSection     → apps/web/src/components/ui/v2/dashboard-section/ (generico per le 5 sezioni)
   - GameCarouselCard     → variante `carousel` di MeepleCard per Games section
   - PlayerAvatarList     → list orizzontale di avatar con count
   - AgentGridCard        → variante `compact` di MeepleCard per Agents 2×2
   - SessionTimelineItem  → row con live indicator + duration + meta
   - EventListItem        → row con data display + participant count

   USE CASE:
     Primary actor: utente autenticato con ≥1 entity in qualsiasi sezione
     Goal: visualizzare a colpo d'occhio lo stato di tutte le entità (Games, Players, Agents, Sessions, Events) e accedervi rapidamente.
     Success: ogni sezione mostra empty-state se 0 items, top-N con CTA "Vedi tutto" se >0.
     Non-goal: chat threading (vedi /chat), profilo utente (vedi /me).
*/

const { useState, useEffect, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.game;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── DATASET DASHBOARD SLICES ──────────────────
// Top-N per section (mockup mostra il populated state)
const TOP_GAMES = DS.games.slice(0, 3);
const TOP_PLAYERS = DS.players.slice(0, 5);
const TOP_AGENTS = DS.agents.slice(0, 4);
const TOP_SESSIONS = DS.sessions.filter(s => s.status !== 'archived').slice(0, 3);
const TOP_EVENTS = DS.events.filter(e => e.status !== 'completed').slice(0, 3);

const USER_NAME = 'Marco';
const KPI = {
  games: 24,
  sessions: 89,
  hoursPlayed: 142,
  winRate: 0.528,
};

// ═══════════════════════════════════════════════════════
// ─── DASHBOARD HERO ──────────────────────────────
// ═══════════════════════════════════════════════════════
const DashboardHero = ({ compact, name = USER_NAME }) => {
  const now = new Date();
  const hour = now.getHours();
  const salute = hour < 12 ? 'Buongiorno' : hour < 18 ? 'Buon pomeriggio' : 'Buonasera';
  return (
    <header style={{
      padding: compact ? '20px 16px 16px' : '32px 32px 22px',
      background:`linear-gradient(135deg, ${entityHsl('game', 0.06)} 0%, ${entityHsl('event', 0.05)} 50%, ${entityHsl('agent', 0.06)} 100%)`,
      borderBottom:'1px solid var(--border-light)',
    }}>
      <div style={{ display:'flex', alignItems:'center', gap: 6, marginBottom: 10 }}>
        <span style={{
          display:'inline-flex', alignItems:'center', gap: 5,
          padding:'3px 9px', borderRadius:'var(--r-pill)',
          background:'var(--bg-muted)',
          color:'var(--text-sec)',
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.08em',
        }}><span aria-hidden="true">⌂</span>Dashboard · /dashboard</span>
      </div>
      <h1 style={{
        fontFamily:'var(--f-display)', fontWeight: 800,
        fontSize: compact ? 24 : 34, letterSpacing:'-.02em', lineHeight: 1.05,
        color:'var(--text)', margin:'0 0 4px',
      }}>{salute}, {name} <span aria-hidden="true">👋</span></h1>
      <p style={{
        color:'var(--text-sec)', fontSize: compact ? 13 : 14, lineHeight: 1.55,
        margin:'0 0 16px', maxWidth: 620,
      }}>
        Ecco una panoramica di tutto: giochi in libreria, gruppo di gioco, agenti AI, sessioni e prossimi eventi.
      </p>
      <div style={{
        display:'grid',
        gridTemplateColumns: compact ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)',
        gap: compact ? 8 : 14,
        maxWidth: 720,
      }}>
        <KpiCard label="Giochi" value={KPI.games} icon="🎲" entity="game" compact={compact}/>
        <KpiCard label="Sessioni" value={KPI.sessions} icon="🎯" entity="session" compact={compact}/>
        <KpiCard label="Ore giocate" value={KPI.hoursPlayed} unit="h" icon="⏱️" entity="toolkit" compact={compact}/>
        <KpiCard label="Win rate" value={(KPI.winRate * 100).toFixed(0)} unit="%" icon="🏆" entity="agent" compact={compact}/>
      </div>
    </header>
  );
};

const KpiCard = ({ label, value, unit = '', icon, entity, compact }) => (
  <div style={{
    padding: compact ? '10px 12px' : '14px 16px',
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-md)',
    display:'flex', alignItems:'center', gap: compact ? 10 : 12,
  }}>
    <div style={{
      width: compact ? 32 : 40, height: compact ? 32 : 40,
      borderRadius:'var(--r-sm)',
      background: entityHsl(entity, 0.12),
      color: entityHsl(entity),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: compact ? 16 : 20,
      flexShrink: 0,
    }} aria-hidden="true">{icon}</div>
    <div style={{ minWidth: 0 }}>
      <div style={{
        color:'var(--text-muted)', fontSize: 9, fontWeight: 700,
        fontFamily:'var(--f-mono)', textTransform:'uppercase', letterSpacing:'.08em',
        marginBottom: 1,
      }}>{label}</div>
      <div style={{
        color:'var(--text)', fontSize: compact ? 18 : 22, fontWeight: 800,
        fontFamily:'var(--f-display)', fontVariantNumeric:'tabular-nums',
        lineHeight: 1,
      }}>
        {value}<span style={{ fontSize: 11, color:'var(--text-muted)', fontWeight: 600, marginLeft: 2 }}>{unit}</span>
      </div>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── DASHBOARD SECTION wrapper ──────────────────────
// ═══════════════════════════════════════════════════════
const DashboardSection = ({ entity, icon, title, count, viewAllHref, viewAllLabel, children, compact, fullWidth }) => (
  <section style={{
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-xl)',
    padding: compact ? 14 : 18,
    display:'flex', flexDirection:'column', gap: compact ? 10 : 12,
    gridColumn: fullWidth ? '1 / -1' : 'auto',
    minHeight: compact ? 'auto' : 180,
  }}>
    <header style={{
      display:'flex', alignItems:'center', gap: compact ? 8 : 10,
    }}>
      <div style={{
        width: compact ? 26 : 32, height: compact ? 26 : 32,
        borderRadius:'var(--r-sm)',
        background: entityHsl(entity, 0.12),
        color: entityHsl(entity),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: compact ? 14 : 17,
        flexShrink: 0,
      }} aria-hidden="true">{icon}</div>
      <h2 style={{
        fontFamily:'var(--f-display)', fontWeight: 800,
        fontSize: compact ? 14 : 17, lineHeight: 1.15,
        color:'var(--text)', margin: 0,
      }}>{title}</h2>
      {count > 0 && (
        <span style={{
          padding:'2px 8px', borderRadius:'var(--r-pill)',
          background:'var(--bg-muted)',
          color:'var(--text-muted)',
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
          fontVariantNumeric:'tabular-nums',
        }}>{count}</span>
      )}
      <div style={{ flex: 1 }}/>
      {viewAllHref && (
        <a href={viewAllHref} onClick={e=>e.preventDefault()} style={{
          padding:'4px 8px', borderRadius:'var(--r-sm)',
          color: entityHsl(entity),
          fontFamily:'var(--f-display)', fontSize: compact ? 11 : 12, fontWeight: 700,
          textDecoration:'none',
          display:'inline-flex', alignItems:'center', gap: 4,
        }}>{viewAllLabel || 'Vedi tutto'} <span aria-hidden="true">→</span></a>
      )}
    </header>
    {children}
  </section>
);

// ═══════════════════════════════════════════════════════
// ─── EMPTY SECTION STATE ────────────────────────
// ═══════════════════════════════════════════════════════
const EmptySection = ({ entity, icon, message, cta, ctaHref, compact }) => (
  <div style={{
    display:'flex', flexDirection:'column', alignItems:'center', textAlign:'center',
    padding: compact ? '20px 12px' : '28px 16px',
    background:'var(--bg)',
    border:`1px dashed ${entityHsl(entity, 0.3)}`,
    borderRadius:'var(--r-md)',
    flex: 1,
  }}>
    <div style={{
      width: 48, height: 48, borderRadius:'50%',
      background: entityHsl(entity, 0.12),
      color: entityHsl(entity),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 22, marginBottom: 8,
    }} aria-hidden="true">{icon}</div>
    <p style={{
      fontSize: 12, color:'var(--text-muted)', maxWidth: 240,
      margin:'0 0 10px', lineHeight: 1.4,
    }}>{message}</p>
    {cta && (
      <a href={ctaHref || '#'} onClick={e=>e.preventDefault()}
        style={{
          padding:'6px 12px', borderRadius:'var(--r-sm)',
          background: entityHsl(entity), color:'#fff',
          textDecoration:'none',
          fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
        }}>{cta}</a>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── GAMES SECTION CONTENT ──────────────────────
// ═══════════════════════════════════════════════════════
const GamesCarousel = ({ items, compact }) => {
  if (items.length === 0) {
    return (
      <EmptySection entity="game" icon="🎲"
        message="Nessun gioco nella libreria. Aggiungine uno per iniziare a tracciare partite."
        cta="+ Aggiungi gioco" ctaHref="/games/new" compact={compact}/>
    );
  }
  return (
    <div style={{
      display:'grid',
      gridTemplateColumns: compact ? '1fr 1fr 1fr' : 'repeat(3, 1fr)',
      gap: compact ? 8 : 10,
    }}>
      {items.map(g => (
        <a key={g.id} href={`/games/${g.id}`} onClick={e=>e.preventDefault()}
          style={{
            display:'flex', flexDirection:'column', gap: 6,
            border:'1px solid var(--border)',
            borderRadius:'var(--r-md)',
            overflow:'hidden',
            background:'var(--bg)',
            textDecoration:'none', color:'inherit',
            transition:'transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm)',
          }}>
          <div style={{
            position:'relative',
            aspectRatio:'5 / 3',
            background: g.cover,
            display:'flex', alignItems:'center', justifyContent:'center',
          }}>
            <span aria-hidden="true" style={{
              fontSize: 36,
              filter:'drop-shadow(0 2px 8px rgba(0,0,0,.35))',
            }}>{g.coverEmoji}</span>
          </div>
          <div style={{ padding:'6px 8px 8px' }}>
            <div style={{
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 12,
              color:'var(--text)',
              display:'-webkit-box', WebkitLineClamp: 1, WebkitBoxOrient:'vertical', overflow:'hidden',
            }}>{g.title}</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
              fontWeight: 600,
            }}>{g.totalPlays || 0} partite</div>
          </div>
        </a>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── PLAYERS SECTION CONTENT ────────────────────
// ═══════════════════════════════════════════════════════
const PlayersAvatarList = ({ items, compact }) => {
  if (items.length === 0) {
    return (
      <EmptySection entity="player" icon="👤"
        message="Nessun gruppo di gioco. Invita amici per iniziare a giocare insieme."
        cta="+ Invita amici" ctaHref="/players/invite" compact={compact}/>
    );
  }
  return (
    <div style={{ display:'flex', alignItems:'center', gap: compact ? 8 : 12, flexWrap:'wrap' }}>
      {items.map(p => (
        <a key={p.id} href={`/players/${p.id}`} onClick={e=>e.preventDefault()}
          title={p.title}
          style={{
            display:'flex', flexDirection:'column', alignItems:'center', gap: 4,
            textDecoration:'none', color:'inherit',
          }}>
          <div style={{
            width: compact ? 44 : 52, height: compact ? 44 : 52,
            borderRadius:'50%',
            background: p.cover,
            color:'#fff',
            display:'flex', alignItems:'center', justifyContent:'center',
            fontFamily:'var(--f-display)', fontWeight: 800,
            fontSize: compact ? 14 : 16,
            boxShadow:`0 2px 8px ${entityHsl('player', 0.25)}`,
            border:`2px solid var(--bg-card)`,
          }}>{p.initials}</div>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 10, fontWeight: 700,
            color:'var(--text)',
            maxWidth: compact ? 50 : 60,
            overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
          }}>{p.title.split(' ')[0]}</div>
        </a>
      ))}
      <div style={{
        display:'flex', alignItems:'center',
        marginLeft:'auto',
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 700, textTransform:'uppercase', letterSpacing:'.06em',
      }}>{items.length} di {items.length + 3}</div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── AGENTS SECTION CONTENT ─────────────────────
// ═══════════════════════════════════════════════════════
const AgentsGrid = ({ items, compact }) => {
  if (items.length === 0) {
    return (
      <EmptySection entity="agent" icon="🤖"
        message="Nessun agente AI attivo. Esplora il catalogo community o crea il tuo."
        cta="Esplora /hub/agents" ctaHref="/hub/agents" compact={compact}/>
    );
  }
  return (
    <div style={{
      display:'grid',
      gridTemplateColumns:'repeat(2, 1fr)',
      gap: 8,
    }}>
      {items.slice(0, 4).map(a => (
        <a key={a.id} href={`/agents/${a.id}`} onClick={e=>e.preventDefault()}
          style={{
            padding:'8px 10px',
            border:'1px solid var(--border)',
            borderRadius:'var(--r-md)',
            background:'var(--bg)',
            display:'flex', alignItems:'center', gap: 8,
            textDecoration:'none', color:'inherit',
          }}>
          <div style={{
            width: 32, height: 32, borderRadius:'var(--r-sm)',
            background: a.cover,
            display:'flex', alignItems:'center', justifyContent:'center',
            fontSize: 16, flexShrink: 0,
          }}>{a.coverEmoji}</div>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 11,
              color:'var(--text)',
              overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
            }}>{a.title}</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
              fontWeight: 600,
            }}>{a.model} · {a.invocations} call</div>
          </div>
          <span style={{
            width: 6, height: 6, borderRadius:'50%',
            background: a.status === 'active' ? entityHsl('toolkit') : 'var(--text-muted)',
            flexShrink: 0,
          }} aria-label={a.status === 'active' ? 'Attivo' : 'Idle'}/>
        </a>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SESSIONS SECTION CONTENT ───────────────────
// ═══════════════════════════════════════════════════════
const SessionsTimeline = ({ items, compact }) => {
  if (items.length === 0) {
    return (
      <EmptySection entity="session" icon="🎯"
        message="Nessuna sessione attiva o recente. Crea una sessione per iniziare a giocare."
        cta="+ Crea sessione" ctaHref="/sessions/new" compact={compact}/>
    );
  }
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
      {items.map(s => {
        const isLive = s.state === 'live';
        return (
          <a key={s.id} href={`/sessions/${s.id}`} onClick={e=>e.preventDefault()}
            style={{
              padding:'8px 10px',
              border:`1px solid ${isLive ? entityHsl('session', 0.4) : 'var(--border)'}`,
              borderRadius:'var(--r-md)',
              background: isLive ? entityHsl('session', 0.06) : 'var(--bg)',
              display:'flex', alignItems:'center', gap: 10,
              textDecoration:'none', color:'inherit',
            }}>
            <div style={{
              width: 32, height: 32, borderRadius:'var(--r-sm)',
              background: s.cover,
              display:'flex', alignItems:'center', justifyContent:'center',
              fontSize: 16, flexShrink: 0,
            }}>{s.coverEmoji}</div>
            <div style={{ minWidth: 0, flex: 1 }}>
              <div style={{
                fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 12,
                color:'var(--text)', display:'flex', alignItems:'center', gap: 6,
              }}>
                {s.title}
                {isLive && (
                  <span style={{
                    display:'inline-flex', alignItems:'center', gap: 3,
                    padding:'1px 6px', borderRadius:'var(--r-pill)',
                    background: entityHsl('session', 0.15),
                    color: entityHsl('session'),
                    fontFamily:'var(--f-mono)', fontSize: 8, fontWeight: 800,
                    textTransform:'uppercase', letterSpacing:'.06em',
                  }}>
                    <span style={{
                      width: 5, height: 5, borderRadius:'50%',
                      background: entityHsl('session'),
                      animation:'pulse 1.4s ease-in-out infinite',
                    }}/> LIVE
                  </span>
                )}
              </div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
                fontWeight: 600,
              }}>Turno {s.turn} · {s.duration} · {s.playerIds.length} giocatori</div>
            </div>
          </a>
        );
      })}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── EVENTS SECTION CONTENT (full-width) ────────
// ═══════════════════════════════════════════════════════
const EventsList = ({ items, compact }) => {
  if (items.length === 0) {
    return (
      <EmptySection entity="event" icon="📅"
        message="Nessuna serata gioco pianificata. Crea un evento per organizzare il prossimo game night."
        cta="+ Pianifica serata" ctaHref="/game-nights/new" compact={compact}/>
    );
  }
  return (
    <div style={{
      display:'grid',
      gridTemplateColumns: compact ? '1fr' : 'repeat(3, 1fr)',
      gap: 10,
    }}>
      {items.map(e => (
        <a key={e.id} href={`/game-nights/${e.id}`} onClick={ev=>ev.preventDefault()}
          style={{
            padding: 12,
            border:'1px solid var(--border)',
            borderRadius:'var(--r-md)',
            background:'var(--bg)',
            display:'flex', alignItems:'center', gap: 12,
            textDecoration:'none', color:'inherit',
          }}>
          <div style={{
            width: 52, height: 52, borderRadius:'var(--r-md)',
            background: entityHsl('event', 0.1),
            color: entityHsl('event'),
            display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center',
            flexShrink: 0,
            border:`1px solid ${entityHsl('event', 0.25)}`,
          }}>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', lineHeight: 1,
            }}>{e.date.split(' ')[1]}</div>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 20, fontWeight: 800,
              lineHeight: 1,
            }}>{e.date.split(' ')[0]}</div>
          </div>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 13,
              color:'var(--text)', marginBottom: 3,
              overflow:'hidden', textOverflow:'ellipsis', whiteSpace:'nowrap',
            }}>{e.title}</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
              fontWeight: 600, marginBottom: 4,
            }}>{e.time} · {e.location}</div>
            <div style={{
              display:'inline-flex', alignItems:'center', gap: 4,
              padding:'2px 6px', borderRadius:'var(--r-pill)',
              background: entityHsl('player', 0.12),
              color: entityHsl('player'),
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
            }}>
              <span aria-hidden="true">👤</span>{e.confirmed}/{e.confirmed + e.pending}
            </div>
          </div>
        </a>
      ))}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── ERROR / SKELETON ─────────────────────────
// ═══════════════════════════════════════════════════════
const SectionSkeleton = ({ compact, height = 140 }) => (
  <div style={{
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-xl)',
    padding: compact ? 14 : 18,
    display:'flex', flexDirection:'column', gap: 10,
    minHeight: height,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 8 }}>
      <div className="mai-shimmer" style={{ width: 28, height: 28, borderRadius:'var(--r-sm)', background:'var(--bg-muted)' }}/>
      <div className="mai-shimmer" style={{ height: 14, width:'40%', borderRadius: 4, background:'var(--bg-muted)' }}/>
      <div style={{ flex: 1 }}/>
      <div className="mai-shimmer" style={{ height: 10, width: 60, borderRadius: 4, background:'var(--bg-muted)' }}/>
    </div>
    <div className="mai-shimmer" style={{ flex: 1, minHeight: 80, borderRadius:'var(--r-md)', background:'var(--bg-muted)' }}/>
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
    }}>Impossibile caricare la dashboard</h3>
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

// ═══════════════════════════════════════════════════════
// ─── DASHBOARD BODY ──────────────────────────────
// ═══════════════════════════════════════════════════════
const DashboardBody = ({ stateOverride, compact }) => {
  const isLoading = stateOverride === 'loading';
  const isError = stateOverride === 'error';
  const isEmpty = stateOverride === 'empty';

  if (isError) {
    return (
      <div style={{ padding: compact ? '16px' : '24px 32px 64px' }}>
        <DashboardHero compact={compact}/>
        <div style={{ padding: compact ? '16px 0' : '24px 0' }}>
          <ErrorState compact={compact}/>
        </div>
      </div>
    );
  }

  const games = isEmpty ? [] : TOP_GAMES;
  const players = isEmpty ? [] : TOP_PLAYERS;
  const agents = isEmpty ? [] : TOP_AGENTS;
  const sessions = isEmpty ? [] : TOP_SESSIONS;
  const events = isEmpty ? [] : TOP_EVENTS;

  return (
    <>
      <DashboardHero compact={compact}/>
      <div style={{
        padding: compact ? '14px 16px 24px' : '24px 32px 64px',
        display:'grid',
        gridTemplateColumns: compact ? '1fr' : 'repeat(2, 1fr)',
        gap: compact ? 12 : 16,
      }}>
        {isLoading ? (
          <>
            {[1,2,3,4].map(i => <SectionSkeleton key={i} compact={compact} height={compact ? 160 : 200}/>)}
            <div style={{ gridColumn:'1 / -1' }}>
              <SectionSkeleton compact={compact} height={compact ? 120 : 160}/>
            </div>
          </>
        ) : (
          <>
            <DashboardSection entity="game" icon="🎲" title="Giochi" count={KPI.games}
              viewAllHref="/library" viewAllLabel="Vedi libreria" compact={compact}>
              <GamesCarousel items={games} compact={compact}/>
            </DashboardSection>

            <DashboardSection entity="player" icon="👤" title="Gruppo di gioco" count={players.length + (isEmpty ? 0 : 3)}
              viewAllHref="/players" viewAllLabel="Tutti i giocatori" compact={compact}>
              <PlayersAvatarList items={players} compact={compact}/>
            </DashboardSection>

            <DashboardSection entity="agent" icon="🤖" title="Agenti AI" count={agents.length}
              viewAllHref="/agents" viewAllLabel="Tutti" compact={compact}>
              <AgentsGrid items={agents} compact={compact}/>
            </DashboardSection>

            <DashboardSection entity="session" icon="🎯" title="Sessioni" count={sessions.length}
              viewAllHref="/sessions" viewAllLabel="Tutte" compact={compact}>
              <SessionsTimeline items={sessions} compact={compact}/>
            </DashboardSection>

            <DashboardSection entity="event" icon="📅" title="Prossime serate" count={events.length}
              viewAllHref="/game-nights" viewAllLabel="Calendario" compact={compact} fullWidth>
              <EventsList items={events} compact={compact}/>
            </DashboardSection>
          </>
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
    { id:'dashboard', label:'Dashboard', active: true },
    { id:'library', label:'Libreria' },
    { id:'hub', label:'Hub' },
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
              color: it.active ? entityHsl('game') : 'var(--text-sec)',
              background: it.active ? entityHsl('game', 0.1) : 'transparent',
              textDecoration:'none',
            }}>{it.label}</a>
        ))}
      </div>
      <div style={{ flex: 1 }}/>
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding:'5px 10px', borderRadius:'var(--r-pill)',
        background:'var(--bg-muted)',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700,
      }}>
        <span style={{
          width: 24, height: 24, borderRadius:'50%',
          background: entityHsl('player'), color:'#fff',
          display:'flex', alignItems:'center', justifyContent:'center',
          fontWeight: 800, fontSize: 11,
        }}>M</span>
        <span>{USER_NAME}</span>
      </div>
    </div>
  );
};

const DesktopScreen = ({ stateOverride }) => (
  <div style={{ minHeight: 720, background:'var(--bg)' }}>
    <DesktopAuthNav/>
    <DashboardBody stateOverride={stateOverride}/>
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
    <button aria-label="Menu" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>☰</button>
    <div style={{ flex: 1, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 700, textAlign:'center' }}>
      Dashboard
    </div>
    <button aria-label="Notifiche" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 16, cursor:'pointer',
    }}>🔔</button>
  </div>
);
const MobileBottomBar = () => {
  const tabs = [
    { id:'dashboard', icon:'⌂', label:'Home', active: true },
    { id:'library', icon:'📚', label:'Libreria' },
    { id:'hub', icon:'🌐', label:'Hub' },
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
          color: t.active ? entityHsl('game') : 'var(--text-muted)',
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
        <DashboardBody stateOverride={stateOverride} compact/>
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
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/dashboard</span>
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
  { id:'default', label:'01 · Default (mixed)', desc:'5 sezioni stacked (Games carousel · Players avatars · Agents 2×2 · Sessions live · Events). Hero greeting + 4 KPI grid 2×2.' },
  { id:'empty', label:'02 · Empty (first-run)', desc:'Tutte le sezioni mostrano empty-state con CTA entity-tinted: "Aggiungi gioco", "Invita amici", "Esplora hub", "Crea sessione", "Pianifica serata".' },
  { id:'loading', label:'03 · Loading', desc:'4 section skeleton + 1 full-width skeleton (Events). Hero NON in skeleton (KPI server-rendered).' },
  { id:'error', label:'04 · Error', desc:'Banner danger inline + retry. Hero rimane visibile.' },
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
        }}>Pre-Stage-3 · #1149 · Dashboard (forward-design)</div>
        <h1>Dashboard — /dashboard</h1>
        <p className="lead">
          Forward-design: 5 entity sections (Games / Players / Agents / Sessions / Events) +
          hero greeting con 4 KPI. Mobile stacked, desktop 2×2 + Events full-width.
          <strong> Diverge da DashboardClient.tsx PR #309</strong> — Stage 3 cluster sarà
          REFACTOR-FORWARD. 4 stati mobile e 3 desktop.
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
          <DesktopFrame label="05 · Desktop · Default (mixed)"
            desc="Hero gradient 3-color + 4 KPI grid 4-col. Body 2×2 grid (Games / Players / Agents / Sessions) + Events full-width row. Live session indicator pulse-animated nel Sessions section.">
            <DesktopScreen stateOverride="default"/>
          </DesktopFrame>
          <DesktopFrame label="06 · Desktop · Empty (first-run)"
            desc="Tutte le 5 sezioni renderano empty-state entity-tinted con CTA. Caso 'utente nuovo': dashboard ancora vuota, ogni sezione orienta verso la prossima azione.">
            <DesktopScreen stateOverride="empty"/>
          </DesktopFrame>
          <DesktopFrame label="07 · Desktop · Loading"
            desc="4 section skeleton (2×2) + 1 full-width skeleton (Events). Hero renderato con KPI placeholder.">
            <DesktopScreen stateOverride="loading"/>
          </DesktopFrame>
        </div>

        <div className="section-label">Stage 3 implications</div>
        <div style={{
          background:'var(--bg-card)',
          border:'1px solid var(--border)',
          borderRadius:'var(--r-md)',
          padding: '16px 20px',
          fontSize: 13, lineHeight: 1.55, color:'var(--text-sec)',
        }}>
          <strong style={{ color:'var(--text)' }}>Diff vs DashboardClient.tsx (PR #309):</strong>
          <ul style={{ margin:'8px 0 0', paddingLeft: 20 }}>
            <li><strong>Kept</strong>: Greeting personalizzato (→ hero "Bentornato, {USER_NAME}"), Live session indicator (→ integrato nel Sessions section header)</li>
            <li><strong>Dropped</strong>: Chat recent cards (→ ora in /chat + ChatSlideOverPanel), Friends row standalone (→ merged in Players section)</li>
            <li><strong>Added</strong>: Games section (primary entity overview), Agents section (catalog access), Events upcoming (game-nights planning)</li>
          </ul>
          <p style={{ marginTop: 10, marginBottom: 0 }}>
            Stage 3 cluster `dashboard` sarà <strong>re-implementation completa</strong>: lo scope si amplia da
            "conformity micro-fix" a "REFACTOR-FORWARD" — il parent spec §3 row riflette già la nuova etichetta.
          </p>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.4; }
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
        select:focus-visible, input:focus-visible, button:focus-visible, a:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
