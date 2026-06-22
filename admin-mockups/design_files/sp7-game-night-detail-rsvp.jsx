/* ===================================================================
   SP7 Game Night — Detail + RSVP (sp7-game-night-detail-rsvp.jsx)
   Route: /game-nights/[id]
   Persona: misto host (Marco) + invitato (Davide)
   Coverage: US-31 Game Nights P1 — Sprint N+1
   Pattern mobile : hero summary + 3 tab (Dettagli / Voting / Chat) +
                    bottom-sheet RSVP CTA sticky se invitato pending
   Pattern desktop: split-view 380px summary sidebar + main area tab content

   Stati (10 mobile + 2 desktop = 12):
   - state-01-host-view-pending     Host, 3/8 RSVP, voting attivo
   - state-02-host-view-ready       Host, 8/8 RSVP, CTA "Avvia sessione"
   - state-03-invitee-pending       Davide pending, RSVP CTA grandi
   - state-04-invitee-confirmed     Davide post-tap, celebration micro
   - state-05-voting-active         Tab voting · 7 voti distribuiti
   - state-06-voting-tied           Tie 3-3 · host scegli (-1h evento)
   - state-07-cancelled             Banner danger + reason + secondaria CTA
   - state-08-in-progress           Session indigo pulsing · "Apri sessione"
   - state-09-completed             Completata · "Registra play" success
   - state-10-mobile-tab-chat       Mobile fullscreen Tab Chat + input
   - state-11-desktop-split-detail  Desktop sidebar 380 + Tab Dettagli
   - state-12-desktop-split-voting  Desktop sidebar 380 + Tab Voting

   Entity color mapping:
   - hero status badge   = entityHsl('event')   rose pulsing
   - RSVP confirmed      = entityHsl('player')  violet
   - voting bar candid.  = entityHsl('game')    orange
   - "in corso" status   = entityHsl('session') indigo pulsing
   - cancelled banner    = var(--c-danger)
   - completed CTA play  = var(--c-success)
   - chat tab messages   = entityHsl('chat')    blue
   - rsvp ✅ confirmed   = var(--c-success) green
   - rsvp ❓ maybe       = var(--c-warning) amber
   - rsvp ❌ no          = var(--text-muted)
   - rsvp ⏳ pending     = dashed border + dotted

   Componenti v2 nuovi:
   - GameNightDetailHero       → apps/web/src/components/ui/v2/game-night-detail-hero/
   - GameNightTabs             → ui/v2/game-night-tabs/
   - RSVPRow                   → ui/v2/rsvp-row/
   - RSVPCTABottomSheet        → ui/v2/rsvp-cta-bottom-sheet/
   - GameVoteCard              → ui/v2/game-vote-card/
   - VotingTiedResolver        → ui/v2/voting-tied-resolver/
   - GameNightChatStream       → ui/v2/game-night-chat-stream/

   Riusi pattern:
   - ConnectionBar (wave 1)        — header pip riga
   - Tabs animated underline       — wave 1
   - Drawer bottom-sheet (SP6-D)   — RSVP CTA sticky
   - chat-stream primitives        — bubble pattern existing
   =================================================================== */

const { useState, useEffect, useMemo, useRef } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.event;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── FIXTURE ───────────────────────────────────────────
const PARTY = [
  { id:'p-marco',   initials:'MR', name:'Marco R.',    color: 262, role:'host' },
  { id:'p-davide',  initials:'DC', name:'Davide C.',   color: 200, role:'invitee' },
  { id:'p-giulia',  initials:'GM', name:'Giulia M.',   color: 10,  role:'invitee' },
  { id:'p-luca',    initials:'LB', name:'Luca B.',     color: 180, role:'invitee' },
  { id:'p-sara',    initials:'ST', name:'Sara T.',     color: 320, role:'invitee' },
  { id:'p-aaron',   initials:'AK', name:'Aaron K.',    color: 140, role:'invitee' },
  { id:'p-federica',initials:'FN', name:'Federica N.', color: 290, role:'invitee' },
  { id:'p-matteo',  initials:'MZ', name:'Matteo Z.',   color: 60,  role:'invitee' },
];
const byPid = Object.fromEntries(PARTY.map(p => [p.id, p]));

const CANDIDATES = [
  { id:'g-twilight', title:'Twilight Imperium 4', publisher:'Fantasy Flight', emoji:'🌌', cover:['hsl(262 45% 22%)','hsl(220 70% 38%)'], players:'3–6', duration:'4–8h',     weight: 4.31 },
  { id:'g-brass',    title:'Brass: Birmingham',   publisher:'Roxley',         emoji:'🏭', cover:['hsl(220 35% 28%)','hsl(28 60% 38%)'],  players:'2–4', duration:'120–180m', weight: 3.91 },
  { id:'g-spirit',   title:'Spirit Island',       publisher:'GMT',            emoji:'🌋', cover:['hsl(210 50% 30%)','hsl(150 50% 38%)'], players:'1–4', duration:'90–120m',  weight: 4.08 },
];

// ═══════════════════════════════════════════════════════
// PRIMITIVES
// ═══════════════════════════════════════════════════════

const Avatar = ({ player, size = 32, ring }) => (
  <span style={{
    width: size, height: size, borderRadius:'50%',
    background: `hsl(${player.color}, 60%, 55%)`,
    color:'#fff', fontFamily:'var(--f-display)',
    fontWeight: 800, fontSize: size <= 22 ? 9 : size <= 32 ? 11 : 14,
    display:'inline-flex', alignItems:'center', justifyContent:'center',
    border: `2px solid ${ring || 'var(--bg-card)'}`,
    flexShrink: 0,
  }}>{player.initials}</span>
);

const Label = ({ children }) => (
  <label style={{
    fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
    color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
  }}>{children}</label>
);

const PulsingDot = ({ color, size = 8 }) => (
  <span aria-hidden="true" style={{
    width: size, height: size, borderRadius:'50%',
    background: color, flexShrink: 0,
    boxShadow: `0 0 0 4px ${color.replace(')', ' / .25)').replace('hsl(', 'hsla(')}`,
    animation:'sp7Pulse 1.6s var(--ease-out) infinite',
  }}/>
);

// ─── Status badge (event lifecycle) ────────────────────
const STATUS_CONFIG = {
  pending:    { label:'Pianificata', dot: true, color:'event',    icon:'📅' },
  ready:      { label:'Pronta',      dot: true, color:'toolkit',  icon:'✓' },
  inprogress: { label:'In corso',    dot: true, color:'session',  icon:'🎯' },
  completed:  { label:'Completata',  dot: false,color:null,       icon:'✓' },
  cancelled:  { label:'Cancellata',  dot: false,color:null,       icon:'✕' },
};

const StatusBadge = ({ status }) => {
  const cfg = STATUS_CONFIG[status];
  const isCancelled = status === 'cancelled';
  const isCompleted = status === 'completed';
  const fg = isCancelled ? 'hsl(var(--c-danger))'
           : isCompleted ? 'var(--text-muted)'
           : entityHsl(cfg.color);
  const bg = isCancelled ? 'hsl(var(--c-danger) / 0.12)'
           : isCompleted ? 'var(--bg-muted)'
           : entityHsl(cfg.color, 0.14);
  const bd = isCancelled ? 'hsl(var(--c-danger) / 0.32)'
           : isCompleted ? 'var(--border)'
           : entityHsl(cfg.color, 0.28);
  return (
    <span style={{
      display:'inline-flex', alignItems:'center', gap: 6,
      padding:'4px 10px', borderRadius:'var(--r-pill)',
      background: bg, color: fg, border: `1px solid ${bd}`,
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
      textTransform:'uppercase', letterSpacing:'.08em',
    }}>
      {cfg.dot && <PulsingDot color={fg} size={7}/>}
      {!cfg.dot && <span aria-hidden="true">{cfg.icon}</span>}
      {cfg.label}
    </span>
  );
};

// ─── HERO SUMMARY ─────────────────────────────────────
// /* v2: GameNightDetailHero */
const Hero = ({ status = 'pending', mobile, compact }) => {
  const dateLine = status === 'inprogress' ? 'Iniziata 12 minuti fa · 21:12'
                 : status === 'completed'  ? 'Conclusa sabato scorso · 23:48'
                 : status === 'cancelled'  ? 'Era prevista per sab 17 mag · 21:00'
                 : 'Tra 3 giorni · sab 17 maggio · 21:00';
  const accent = status === 'cancelled' ? 'hsl(var(--c-danger))'
               : status === 'inprogress' ? entityHsl('session')
               : status === 'completed' ? entityHsl('toolkit')
               : entityHsl('event');

  return (
    <header style={{
      padding: compact ? '14px 18px' : (mobile ? '16px 16px 18px' : '24px 24px 22px'),
      background: status === 'cancelled'
        ? `linear-gradient(160deg, hsl(var(--c-danger) / .14), hsl(var(--c-danger) / .04))`
        : status === 'inprogress'
        ? `linear-gradient(160deg, ${entityHsl('session', 0.18)}, ${entityHsl('event', 0.05)})`
        : status === 'completed'
        ? 'var(--bg-muted)'
        : `linear-gradient(160deg, ${entityHsl('event', 0.16)}, ${entityHsl('event', 0.02)})`,
      borderBottom:'1px solid var(--border)',
      position:'relative',
    }}>
      {/* Decorative shape */}
      <div aria-hidden="true" style={{
        position:'absolute', top:-24, right:-24,
        width: 110, height: 110, borderRadius:'50%',
        background: `radial-gradient(circle at 30% 30%, ${accent}, transparent 70%)`,
        opacity: .25, pointerEvents:'none',
      }}/>

      {/* Status row */}
      <div style={{
        display:'flex', alignItems:'center', gap: 8, marginBottom: 10,
        position:'relative',
      }}>
        <StatusBadge status={status}/>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 700,
        }}>#GN-{status === 'inprogress' ? '042' : '041'}</span>
      </div>

      {/* Title */}
      <h1 style={{
        fontFamily:'var(--f-display)', fontSize: compact ? 18 : (mobile ? 22 : 28),
        fontWeight: 800, color:'var(--text)', lineHeight: 1.15,
        margin:'0 0 8px', letterSpacing:'-.01em',
        textDecoration: status === 'cancelled' ? 'line-through' : 'none',
        textDecorationThickness: status === 'cancelled' ? '2px' : 'auto',
        textDecorationColor: status === 'cancelled' ? 'hsl(var(--c-danger) / 0.6)' : 'auto',
      }}>Sabato boardgame con i Padovani</h1>

      {/* Date + location */}
      <div style={{
        display:'flex', flexDirection:'column', gap: 6,
      }}>
        <div style={{
          display:'flex', alignItems:'center', gap: 7,
          fontFamily:'var(--f-mono)', fontSize: compact ? 11 : 12, color:'var(--text-sec)',
          fontWeight: 700,
        }}>
          <span aria-hidden="true" style={{ color: accent, fontSize: 13 }}>📅</span>
          <span>{dateLine}</span>
        </div>
        <button type="button" style={{
          display:'inline-flex', alignItems:'center', gap: 7,
          padding:'5px 0', background:'transparent', border:'none',
          color:'var(--text-sec)', fontFamily:'var(--f-mono)', fontSize: compact ? 11 : 12,
          fontWeight: 700, cursor:'pointer', textAlign:'left',
        }}>
          <span aria-hidden="true" style={{ color: accent, fontSize: 13 }}>📍</span>
          <span style={{ borderBottom:'1px dashed currentColor' }}>
            Casa Marco · Via C. Battisti 12, Padova
          </span>
          <span style={{
            fontSize: 10, color: accent, fontWeight: 800,
          }}>↗</span>
        </button>
      </div>

      {/* Host + party count */}
      <div style={{
        display:'flex', alignItems:'center', gap: 10, marginTop: 12,
        paddingTop: 12, borderTop:'1px solid var(--border-light)',
      }}>
        <Avatar player={byPid['p-marco']} size={28}/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
            color:'var(--text)',
          }}>Organizzato da Marco R.</div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
            fontWeight: 700,
          }}>3h stimate · 8 invitati</div>
        </div>
      </div>
    </header>
  );
};

// ─── TABS animated underline ─────────────────────────
const Tabs = ({ tabs, active, onChange }) => (
  <div role="tablist" style={{
    display:'flex', alignItems:'stretch',
    background:'var(--bg)',
    borderBottom:'1px solid var(--border)',
    position:'sticky', top: 0, zIndex: 6,
  }}>
    {tabs.map(t => {
      const isActive = t.id === active;
      return (
        <button key={t.id} role="tab" aria-selected={isActive}
          onClick={() => onChange && onChange(t.id)}
          style={{
            flex: 1, padding:'12px 6px',
            background:'transparent', border:'none',
            color: isActive ? 'var(--text)' : 'var(--text-muted)',
            fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
            cursor:'pointer', position:'relative',
            display:'flex', alignItems:'center', justifyContent:'center', gap: 6,
          }}>
          <span aria-hidden="true">{t.icon}</span>
          <span>{t.label}</span>
          {t.badge !== undefined && (
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              color: isActive ? entityHsl('event') : 'var(--text-muted)',
              background: isActive ? entityHsl('event', 0.14) : 'var(--bg-muted)',
              padding:'1px 5px', borderRadius:'var(--r-sm)',
              border:`1px solid ${isActive ? entityHsl('event', 0.22) : 'var(--border)'}`,
            }}>{t.badge}</span>
          )}
          {isActive && (
            <span aria-hidden="true" style={{
              position:'absolute', bottom: 0, left: '15%', right: '15%',
              height: 2, borderRadius: 1,
              background: entityHsl('event'),
            }}/>
          )}
        </button>
      );
    })}
  </div>
);

// ═══════════════════════════════════════════════════════
// TAB 1 — DETTAGLI · RSVP list
// /* v2: RSVPRow */
// ═══════════════════════════════════════════════════════

const RSVP_ICON = {
  yes:     { ic:'✓', color:'var(--c-success)',  label:'Confermato' },
  maybe:   { ic:'?', color:'var(--c-warning)',  label:'Forse' },
  no:      { ic:'×', color: null,                label:'Non posso' },
  pending: { ic:'⏳', color: null,                label:'In attesa' },
};

const RSVPRow = ({ player, status, isMe, isHost }) => {
  const cfg = RSVP_ICON[status];
  const isPending = status === 'pending';
  const isNo = status === 'no';

  return (
    <div style={{
      display:'flex', alignItems:'center', gap: 10,
      padding:'10px 14px',
      background: isMe ? entityHsl('player', 0.06) : 'var(--bg-card)',
      border: isPending
        ? `1px dashed ${entityHsl('player', 0.4)}`
        : isMe ? `1px solid ${entityHsl('player', 0.32)}`
        : '1px solid var(--border)',
      borderRadius:'var(--r-md)',
      opacity: isNo ? 0.7 : 1,
    }}>
      <Avatar player={player} size={32} ring={isMe ? entityHsl('player', 0.4) : undefined}/>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display:'flex', alignItems:'center', gap: 6,
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
          color:'var(--text)',
          textDecoration: isNo ? 'line-through' : 'none',
        }}>
          <span>{player.name}</span>
          {isMe && (
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 8, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.06em',
              background: entityHsl('player', 0.18),
              color: entityHsl('player'),
              padding:'1px 5px', borderRadius:'var(--r-sm)',
            }}>TU</span>
          )}
          {isHost && (
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 8, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.06em',
              background: entityHsl('event', 0.16),
              color: entityHsl('event'),
              padding:'1px 5px', borderRadius:'var(--r-sm)',
            }}>HOST</span>
          )}
        </div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 700, marginTop: 1,
        }}>
          {status === 'yes'     && 'Risposto 2 giorni fa'}
          {status === 'maybe'   && 'Risposto 1 giorno fa · "vediamo se torno in tempo"'}
          {status === 'no'      && 'Risposto 4 giorni fa · "cena famiglia"'}
          {status === 'pending' && 'Notifica inviata · in attesa'}
        </div>
      </div>
      <div style={{
        display:'flex', alignItems:'center', gap: 5,
        padding:'4px 9px', borderRadius:'var(--r-pill)',
        background: cfg.color
          ? `hsl(${cfg.color.replace('var(', '').replace(')', '')} / 0.14)`
          : 'var(--bg-muted)',
        color: cfg.color ? `hsl(${cfg.color.replace('var(', '').replace(')', '')})` : 'var(--text-muted)',
        border: `1px solid ${cfg.color
          ? `hsl(${cfg.color.replace('var(', '').replace(')', '')} / 0.28)`
          : 'var(--border)'}`,
        fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
      }}>
        <span aria-hidden="true">{cfg.ic}</span>
        <span>{cfg.label}</span>
      </div>
    </div>
  );
};

const TabDettagli = ({ rsvpMap, mobile, viewer = 'host', viewerRSVP }) => {
  const counts = Object.values(rsvpMap).reduce((acc, s) => { acc[s] = (acc[s] || 0) + 1; return acc; }, {});
  const yes = counts.yes || 0;
  const total = Object.keys(rsvpMap).length;

  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 14, padding:'14px' }}>
      {/* Counter kicker */}
      <div style={{
        display:'flex', alignItems:'center', justifyContent:'space-between',
        padding:'10px 14px',
        background: entityHsl('player', 0.06),
        border:`1px solid ${entityHsl('player', 0.18)}`,
        borderRadius:'var(--r-md)',
      }}>
        <div>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800,
            color: entityHsl('player'),
          }}>{yes}<span style={{ fontSize: 13, color: entityHsl('player', 0.6) }}>/{total}</span></div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
            marginTop: 1,
          }}>confermati</div>
        </div>
        <div style={{ display:'flex', alignItems:'center', gap: 4 }}>
          {PARTY.filter(p => rsvpMap[p.id] === 'yes').slice(0, 6).map((p, i) => (
            <span key={p.id} style={{ marginLeft: i === 0 ? 0 : -8 }}>
              <Avatar player={p} size={26}/>
            </span>
          ))}
        </div>
      </div>

      {/* RSVP list */}
      <div style={{ display:'flex', flexDirection:'column', gap: 6 }}>
        <Label>Risposte</Label>
        {PARTY.map(p => (
          <RSVPRow key={p.id}
            player={p}
            status={rsvpMap[p.id]}
            isMe={viewer === 'invitee' && p.id === 'p-davide'}
            isHost={p.id === 'p-marco'}/>
        ))}
      </div>

      {/* Host CTAs */}
      {viewer === 'host' && (
        <div style={{
          display:'grid', gridTemplateColumns:'1fr 1fr', gap: 8,
        }}>
          <button type="button" style={{
            padding:'12px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:`1px solid ${entityHsl('event', 0.4)}`,
            color: entityHsl('event'),
            fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
            cursor:'pointer',
          }}>✏️ Modifica serata</button>
          <button type="button" style={{
            padding:'12px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid hsl(var(--c-danger) / 0.35)',
            color:'hsl(var(--c-danger))',
            fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
            cursor:'pointer',
          }}>✕ Cancella</button>
        </div>
      )}

      {/* Invitee already-replied row */}
      {viewer === 'invitee' && viewerRSVP && (
        <div style={{
          display:'flex', alignItems:'center', justifyContent:'space-between',
          padding:'12px 14px',
          background: viewerRSVP === 'yes'
            ? 'hsl(var(--c-success) / 0.1)'
            : entityHsl('player', 0.08),
          border: `1px solid ${viewerRSVP === 'yes'
            ? 'hsl(var(--c-success) / 0.32)'
            : entityHsl('player', 0.25)}`,
          borderRadius:'var(--r-md)',
        }}>
          <div style={{ display:'flex', alignItems:'center', gap: 10 }}>
            <span style={{
              width: 32, height: 32, borderRadius:'50%',
              background: viewerRSVP === 'yes' ? 'hsl(var(--c-success))' : entityHsl('player'),
              color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
              fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
            }}>{viewerRSVP === 'yes' ? '✓' : '?'}</span>
            <div>
              <div style={{
                fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
                color:'var(--text)',
              }}>{viewerRSVP === 'yes' ? 'Hai confermato' : 'Hai risposto "Forse"'}</div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
                fontWeight: 700,
              }}>Risposto poco fa</div>
            </div>
          </div>
          <button type="button" style={{
            padding:'6px 12px', borderRadius:'var(--r-sm)',
            background:'transparent', border:'1px solid var(--border-strong)',
            color:'var(--text-sec)', fontFamily:'var(--f-display)',
            fontSize: 11, fontWeight: 800, cursor:'pointer',
          }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-game-nights-index.html'; }, 0); /* DEMO-NAV */ }}>Cambia</button>
        </div>
      )}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// TAB 2 — VOTING
// /* v2: GameVoteCard */
// ═══════════════════════════════════════════════════════

const GameVoteCard = ({ game, votes, totalVoters, voters, viewerVoted, leading }) => {
  const pct = Math.round((votes / Math.max(1, totalVoters)) * 100);
  return (
    <div style={{
      display:'flex', flexDirection:'column',
      borderRadius:'var(--r-lg)',
      background:'var(--bg-card)',
      border: leading
        ? `1.5px solid ${entityHsl('game', 0.5)}`
        : '1px solid var(--border)',
      boxShadow: leading ? `0 0 0 3px ${entityHsl('game', 0.1)}` : 'none',
      overflow:'hidden',
      position:'relative',
    }}>
      {leading && (
        <span style={{
          position:'absolute', top: 8, right: 8, zIndex: 2,
          padding:'3px 8px', borderRadius:'var(--r-pill)',
          background: entityHsl('game'),
          color:'#fff', fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.08em',
          boxShadow:'0 2px 6px rgba(0,0,0,.2)',
        }}>👑 In testa</span>
      )}
      <div style={{
        display:'flex', alignItems:'stretch', gap: 0,
      }}>
        <div style={{
          width: 76, flexShrink: 0,
          background:`linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})`,
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 30,
        }} aria-hidden="true">
          <span style={{ filter:'drop-shadow(0 2px 8px rgba(0,0,0,.4))' }}>{game.emoji}</span>
        </div>
        <div style={{ flex: 1, padding:'10px 12px', display:'flex', flexDirection:'column', gap: 6, minWidth: 0 }}>
          <div>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              color:'var(--text)', lineHeight: 1.2,
            }}>{game.title}</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
              fontWeight: 600, marginTop: 2,
            }}>👥 {game.players} · ⏱ {game.duration} · ⚖ {game.weight}</div>
          </div>
          {/* Voting bar */}
          <div style={{
            position:'relative', height: 8, borderRadius: 4,
            background:'var(--bg-muted)', overflow:'hidden',
          }}>
            <span style={{
              position:'absolute', inset:'0 auto 0 0',
              width: `${pct}%`,
              background:`linear-gradient(90deg, ${entityHsl('game', 0.6)}, ${entityHsl('game')})`,
              borderRadius: 4,
              transition:'width var(--dur-md) var(--ease-out)',
            }}/>
          </div>
          {/* Voters row */}
          <div style={{ display:'flex', alignItems:'center', justifyContent:'space-between', gap: 8 }}>
            <div style={{ display:'flex', alignItems:'center', gap: 4 }}>
              {voters.slice(0, 4).map((vid, i) => (
                <span key={vid} style={{ marginLeft: i === 0 ? 0 : -6 }}>
                  <Avatar player={byPid[vid]} size={20}/>
                </span>
              ))}
              {voters.length > 4 && (
                <span style={{
                  marginLeft: -4,
                  fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
                  color:'var(--text-muted)',
                }}>+{voters.length - 4}</span>
              )}
              <span style={{
                marginLeft: 6,
                fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
                color: entityHsl('game'),
              }}>{votes}<span style={{ fontSize: 10, color:'var(--text-muted)', fontWeight: 700 }}> voti</span></span>
            </div>
            <button type="button" style={{
              padding:'5px 11px', borderRadius:'var(--r-pill)',
              background: viewerVoted ? entityHsl('game') : 'transparent',
              border: viewerVoted ? `1px solid ${entityHsl('game')}` : `1px solid ${entityHsl('game', 0.4)}`,
              color: viewerVoted ? '#fff' : entityHsl('game'),
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
              cursor:'pointer',
              display:'inline-flex', alignItems:'center', gap: 4,
            }}>
              <span aria-hidden="true">{viewerVoted ? '✓' : '+'}</span>
              <span>{viewerVoted ? 'Votato' : 'Vota'}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

const TabVoting = ({ tied = false, mobile }) => {
  const votingState = tied
    ? {
        'g-twilight': { votes: 3, voters: ['p-marco','p-davide','p-sara'], leading: true },
        'g-brass':    { votes: 3, voters: ['p-giulia','p-luca','p-aaron'], leading: true },
        'g-spirit':   { votes: 1, voters: ['p-matteo'],                     leading: false },
      }
    : {
        'g-twilight': { votes: 4, voters: ['p-marco','p-davide','p-sara','p-aaron'], leading: true },
        'g-brass':    { votes: 2, voters: ['p-giulia','p-luca'],                      leading: false },
        'g-spirit':   { votes: 1, voters: ['p-matteo'],                                leading: false },
      };
  const total = Object.values(votingState).reduce((s, v) => s + v.votes, 0);

  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 14, padding:'14px' }}>
      {tied && (
        <div role="alert" style={{
          padding:'12px 14px',
          background:'hsl(var(--c-warning) / .14)',
          border:'1px solid hsl(var(--c-warning) / .35)',
          borderRadius:'var(--r-lg)',
          display:'flex', gap: 10,
        }}>
          <span aria-hidden="true" style={{ fontSize: 22, flexShrink: 0 }}>⚖️</span>
          <div style={{ flex: 1 }}>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              color:'var(--text)',
            }}>Pareggio · 3 a 3</div>
            <div style={{
              fontSize: 12, color:'var(--text-sec)', marginTop: 3, lineHeight: 1.45,
            }}>
              I voti chiudono <strong>tra 47 minuti</strong>. Come host puoi
              risolvere tu il pareggio scegliendo direttamente.
            </div>
          </div>
        </div>
      )}

      <div style={{
        display:'flex', alignItems:'center', justifyContent:'space-between',
        padding:'8px 12px',
        background: entityHsl('game', 0.08),
        border:`1px solid ${entityHsl('game', 0.18)}`,
        borderRadius:'var(--r-md)',
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
          color: entityHsl('game'), textTransform:'uppercase', letterSpacing:'.06em',
        }}>{total} voti totali · {tied ? 'Pareggio' : 'Twilight in testa'}</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color:'var(--text-muted)',
        }}>chiude {tied ? '47 min' : '23h 12m'}</div>
      </div>

      <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
        {CANDIDATES.map(g => (
          <GameVoteCard key={g.id}
            game={g}
            votes={votingState[g.id].votes}
            totalVoters={total}
            voters={votingState[g.id].voters}
            leading={votingState[g.id].leading}
            viewerVoted={votingState[g.id].voters.includes('p-marco')}/>
        ))}
      </div>

      {tied && (
        <div style={{
          display:'flex', flexDirection:'column', gap: 8,
          padding:'12px 14px',
          background:'var(--bg-card)',
          border:`1.5px dashed ${entityHsl('event', 0.4)}`,
          borderRadius:'var(--r-lg)',
        }}>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            color:'var(--text)',
          }}>Scegli tu come host</div>
          <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap: 8 }}>
            <button type="button" style={{
              padding:'10px', borderRadius:'var(--r-md)',
              background: entityHsl('game'),
              border:'none', color:'#fff',
              fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
              cursor:'pointer',
              boxShadow:`0 4px 12px ${entityHsl('game', 0.3)}`,
            }}>🌌 Scegli Twilight</button>
            <button type="button" style={{
              padding:'10px', borderRadius:'var(--r-md)',
              background:'var(--bg-card)',
              border:`1.5px solid ${entityHsl('game', 0.4)}`,
              color: entityHsl('game'),
              fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
              cursor:'pointer',
            }}>🏭 Scegli Brass</button>
          </div>
        </div>
      )}

      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 600, textAlign:'center', padding:'4px 12px', lineHeight: 1.5,
      }}>
        💡 Voti pubblici · ognuno può votare più candidati · si chiudono 1h prima dell'evento
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// TAB 3 — CHAT
// /* v2: GameNightChatStream */
// ═══════════════════════════════════════════════════════

const SystemMessage = ({ icon, text }) => (
  <div style={{
    display:'flex', alignItems:'center', justifyContent:'center', gap: 8,
    padding:'4px 0',
  }}>
    <span style={{
      display:'inline-flex', alignItems:'center', gap: 6,
      padding:'4px 10px', borderRadius:'var(--r-pill)',
      background:'var(--bg-muted)',
      color:'var(--text-muted)',
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      fontStyle:'italic',
      border:'1px solid var(--border-light)',
    }}>
      <span aria-hidden="true">{icon}</span>
      <span>{text}</span>
    </span>
  </div>
);

const ChatBubble = ({ player, text, isMe, time }) => (
  <div style={{
    display:'flex', gap: 8, alignItems:'flex-end',
    flexDirection: isMe ? 'row-reverse' : 'row',
    margin:'4px 0',
  }}>
    {!isMe && <Avatar player={player} size={26}/>}
    <div style={{
      maxWidth:'74%',
      display:'flex', flexDirection:'column',
      alignItems: isMe ? 'flex-end' : 'flex-start',
      gap: 2,
    }}>
      {!isMe && (
        <span style={{
          fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
          color: entityHsl('player'),
          paddingLeft: 4,
        }}>{player.name}</span>
      )}
      <div style={{
        padding:'8px 12px',
        borderRadius: isMe
          ? 'var(--r-lg) var(--r-lg) var(--r-xs) var(--r-lg)'
          : 'var(--r-lg) var(--r-lg) var(--r-lg) var(--r-xs)',
        background: isMe
          ? `linear-gradient(135deg, ${entityHsl('chat')}, hsl(${DS.EC.chat.h - 15}, ${DS.EC.chat.s}%, ${DS.EC.chat.l - 8}%))`
          : 'var(--bg-card)',
        color: isMe ? '#fff' : 'var(--text)',
        border: isMe ? 'none' : '1px solid var(--border)',
        fontSize: 13, lineHeight: 1.45,
      }}>{text}</div>
      <span style={{
        fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
        fontWeight: 700, padding:'0 4px',
      }}>{time}</span>
    </div>
  </div>
);

const TabChat = ({ mobile }) => (
  <div style={{
    display:'flex', flexDirection:'column',
    height: mobile ? 'auto' : '100%',
    minHeight: 320,
    background: `linear-gradient(180deg, var(--bg) 0%, ${entityHsl('chat', 0.04)} 100%)`,
  }}>
    <div style={{
      flex: 1, padding:'14px', display:'flex', flexDirection:'column', gap: 4,
    }}>
      <SystemMessage icon="🎉" text="Marco ha creato la serata · 5 giorni fa"/>
      <ChatBubble player={byPid['p-marco']} isMe={false}
        text="Ragazzi, sabato si gioca! Ho aperto il voting su 3 candidati 🎲"
        time="lun 21:14"/>
      <SystemMessage icon="🎮" text="Davide ha confermato"/>
      <SystemMessage icon="🎮" text="Giulia ha votato Forse"/>
      <ChatBubble player={byPid['p-davide']} isMe={false}
        text="Ci sono! Porto io il dolce 🍰"
        time="mar 18:42"/>
      <SystemMessage icon="🎲" text="Davide ha votato Twilight Imperium 4"/>
      <SystemMessage icon="🎮" text="Sara ha confermato"/>
      <ChatBubble player={byPid['p-sara']} isMe={false}
        text="Twilight ce la facciamo in 4h? Sennò proporrei Brass come piano B"
        time="mar 21:03"/>
      <ChatBubble player={byPid['p-marco']} isMe={true}
        text="Iniziamo presto, alle 20 setup. Compro le bibite io 🥤"
        time="mer 09:18"/>
      <SystemMessage icon="🎲" text="Aaron ha votato Twilight Imperium 4"/>
      <ChatBubble player={byPid['p-luca']} isMe={false}
        text="Io porto la spianata. @Federica ce la fai sabato?"
        time="mer 12:48"/>
    </div>
    {/* Input */}
    <div style={{
      display:'flex', alignItems:'center', gap: 8,
      padding:'10px 12px',
      background:'var(--glass-bg)',
      backdropFilter:'blur(12px)',
      borderTop:'1px solid var(--border)',
      position: mobile ? 'sticky' : 'static',
      bottom: 0,
    }}>
      <button type="button" aria-label="Allega" style={{
        width: 34, height: 34, borderRadius:'var(--r-md)',
        background:'var(--bg-muted)', border:'1px solid var(--border)',
        color:'var(--text-sec)', fontSize: 16, cursor:'pointer',
        flexShrink: 0,
      }}>＋</button>
      <input type="text" placeholder="Scrivi al gruppo…" readOnly style={{
        flex: 1, padding:'10px 14px', borderRadius:'var(--r-pill)',
        background:'var(--bg-card)', border:'1px solid var(--border)',
        color:'var(--text)', fontFamily:'var(--f-body)', fontSize: 13,
        outline:'none',
      }}/>
      <button type="button" aria-label="Invia" style={{
        width: 38, height: 38, borderRadius:'50%',
        background: entityHsl('chat'),
        border:'none', color:'#fff', fontSize: 15, cursor:'pointer',
        flexShrink: 0,
        boxShadow:`0 2px 8px ${entityHsl('chat', 0.4)}`,
      }}>↑</button>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// RSVP CTA bottom-sheet (sticky if invitee pending)
// /* v2: RSVPCTABottomSheet */
// ═══════════════════════════════════════════════════════

const RSVPBottomSheet = ({ confirmed = false }) => (
  <div style={{
    position:'sticky', bottom: 0,
    padding:'12px 14px 14px',
    background:'var(--glass-bg)',
    backdropFilter:'blur(16px)',
    borderTop:`1.5px solid ${entityHsl('event', 0.3)}`,
    boxShadow:'var(--shadow-drawer)',
    zIndex: 8,
  }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
      color: confirmed ? 'hsl(var(--c-success))' : entityHsl('event'),
      textTransform:'uppercase', letterSpacing:'.08em', marginBottom: 8,
      display:'flex', alignItems:'center', gap: 6,
    }}>
      {!confirmed && <PulsingDot color={entityHsl('event')} size={6}/>}
      {confirmed
        ? '✓ Hai confermato la tua partecipazione'
        : 'Conferma la tua partecipazione'}
    </div>
    {!confirmed ? (
      <div style={{ display:'grid', gridTemplateColumns:'1.4fr 1fr 1fr', gap: 6 }}>
        <button type="button" style={{
          padding:'14px 8px', borderRadius:'var(--r-md)',
          background:`linear-gradient(135deg, hsl(var(--c-success)), hsl(142 70% 36%))`,
          border:'none', color:'#fff',
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
          cursor:'pointer',
          boxShadow:'0 4px 14px hsl(var(--c-success) / 0.35)',
          display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 6,
        }}>
          <span aria-hidden="true">✓</span><span>Ci sarò</span>
        </button>
        <button type="button" style={{
          padding:'14px 6px', borderRadius:'var(--r-md)',
          background:'var(--bg-card)',
          border:'1.5px solid hsl(var(--c-warning) / 0.4)',
          color:'hsl(var(--c-warning))',
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
          cursor:'pointer',
          display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 5,
        }}>
          <span aria-hidden="true">?</span><span>Forse</span>
        </button>
        <button type="button" style={{
          padding:'14px 6px', borderRadius:'var(--r-md)',
          background:'var(--bg-card)',
          border:'1.5px solid var(--border-strong)',
          color:'var(--text-sec)',
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
          cursor:'pointer',
          display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 5,
        }}>
          <span aria-hidden="true">×</span><span>Non posso</span>
        </button>
      </div>
    ) : (
      <div style={{
        padding:'14px',
        background:'hsl(var(--c-success) / 0.1)',
        border:'1px solid hsl(var(--c-success) / 0.3)',
        borderRadius:'var(--r-md)',
        display:'flex', alignItems:'center', gap: 12,
        position:'relative', overflow:'hidden',
        animation:'sp7Celebrate var(--dur-lg) var(--ease-spring)',
      }}>
        {/* sparkles */}
        {[0,1,2,3,4,5].map(i => (
          <span key={i} aria-hidden="true" style={{
            position:'absolute',
            top: `${10 + (i % 3) * 30}%`, left: `${15 + i * 14}%`,
            fontSize: 12, opacity: 0.85,
            animation: `sp7Sparkle 2s ${i * 0.15}s var(--ease-out) infinite`,
          }}>{['✦','✧','⋆','✦','✧','⋆'][i]}</span>
        ))}
        <span style={{
          width: 38, height: 38, borderRadius:'50%',
          background:'hsl(var(--c-success))',
          color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 18, fontWeight: 800,
          flexShrink: 0,
          boxShadow:'0 2px 8px hsl(var(--c-success) / 0.5)',
        }}>✓</span>
        <div style={{ flex: 1, position:'relative' }}>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
            color:'var(--text)',
          }}>Ci vediamo sabato! 🎲</div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-sec)',
            fontWeight: 700, marginTop: 1,
          }}>Aggiunto al calendario · ricorderemo 1h prima</div>
        </div>
        <button type="button" style={{
          padding:'6px 10px', borderRadius:'var(--r-sm)',
          background:'transparent', border:'1px solid hsl(var(--c-success) / 0.4)',
          color:'hsl(var(--c-success))',
          fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800, cursor:'pointer',
          flexShrink: 0,
        }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-game-nights-index.html'; }, 0); /* DEMO-NAV */ }}>Cambia</button>
      </div>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// Status banners (cancelled / in-progress / completed CTA)
// ═══════════════════════════════════════════════════════

const CancelledBanner = () => (
  <div role="alert" style={{
    margin:'14px',
    padding:'14px',
    background:'hsl(var(--c-danger) / 0.1)',
    border:'1px solid hsl(var(--c-danger) / 0.32)',
    borderRadius:'var(--r-lg)',
    display:'flex', flexDirection:'column', gap: 10,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 10 }}>
      <span style={{
        width: 36, height: 36, borderRadius:'50%',
        background:'hsl(var(--c-danger))',
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 18, fontWeight: 800, flexShrink: 0,
      }}>✕</span>
      <div style={{ flex: 1 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          color:'var(--text)',
        }}>Serata cancellata</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 700, marginTop: 2,
        }}>Cancellata da Marco · 2 giorni fa</div>
      </div>
    </div>
    <div style={{
      padding:'10px 12px',
      background:'var(--bg-card)',
      borderRadius:'var(--r-md)',
      border:'1px solid var(--border)',
      fontSize: 12.5, color:'var(--text-sec)', lineHeight: 1.5,
    }}>
      <strong style={{ color:'var(--text)' }}>Motivo:</strong> "Mia figlia ha la
      febbre, riprogrammiamo la prossima settimana. Scusate ragazzi 😔"
    </div>
    <button type="button" style={{
      padding:'12px', borderRadius:'var(--r-md)',
      background:'var(--bg-card)',
      border:`1.5px solid ${entityHsl('event', 0.4)}`,
      color: entityHsl('event'),
      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
    }}>📅 Crea nuova serata</button>
  </div>
);

const InProgressCTA = () => (
  <div style={{
    margin:'14px',
    padding:'14px',
    borderRadius:'var(--r-lg)',
    background:`linear-gradient(135deg, ${entityHsl('session', 0.18)}, ${entityHsl('session', 0.06)})`,
    border:`1.5px solid ${entityHsl('session', 0.4)}`,
    display:'flex', flexDirection:'column', gap: 10,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 10 }}>
      <PulsingDot color={entityHsl('session')} size={10}/>
      <div style={{ flex: 1 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          color:'var(--text)',
        }}>Sessione live in corso</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color: entityHsl('session'),
          fontWeight: 800, marginTop: 1, textTransform:'uppercase', letterSpacing:'.06em',
        }}>Twilight Imperium · turno 3 · 1h 12m</div>
      </div>
    </div>
    <button type="button" style={{
      padding:'12px', borderRadius:'var(--r-md)',
      background:`linear-gradient(135deg, ${entityHsl('session')}, hsl(${DS.EC.session.h - 15}, ${DS.EC.session.s}%, ${DS.EC.session.l - 8}%))`,
      border:'none', color:'#fff',
      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
      boxShadow:`0 4px 14px ${entityHsl('session', 0.4)}`,
      display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
    }}>🎯 Apri sessione live</button>
  </div>
);

const CompletedCTA = () => (
  <div style={{
    margin:'14px',
    padding:'14px',
    borderRadius:'var(--r-lg)',
    background:'hsl(var(--c-success) / 0.1)',
    border:'1px solid hsl(var(--c-success) / 0.3)',
    display:'flex', flexDirection:'column', gap: 10,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 10 }}>
      <span style={{
        width: 36, height: 36, borderRadius:'50%',
        background:'hsl(var(--c-success))',
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 18, fontWeight: 800, flexShrink: 0,
      }}>✓</span>
      <div style={{ flex: 1 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          color:'var(--text)',
        }}>Serata completata · 4h 32m</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 700, marginTop: 2,
        }}>Vincitore: Sara T. · 142 punti</div>
      </div>
    </div>
    <button type="button" style={{
      padding:'12px', borderRadius:'var(--r-md)',
      background:`linear-gradient(135deg, hsl(var(--c-success)), hsl(142 70% 36%))`,
      border:'none', color:'#fff',
      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, cursor:'pointer',
      boxShadow:'0 4px 14px hsl(var(--c-success) / 0.35)',
      display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
    }} onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-game-onboarding.html'; }, 0); /* DEMO-NAV */ }}>📊 Registra play record</button>
    <button type="button" style={{
      padding:'10px', borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border-strong)',
      color:'var(--text-sec)',
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 700, cursor:'pointer',
    }}>📅 Crea il rematch</button>
  </div>
);

const HostReadyCTA = () => (
  <div style={{
    margin:'14px',
    padding:'14px',
    borderRadius:'var(--r-lg)',
    background:`linear-gradient(135deg, ${entityHsl('toolkit', 0.16)}, ${entityHsl('toolkit', 0.04)})`,
    border:`1.5px solid ${entityHsl('toolkit', 0.4)}`,
    display:'flex', flexDirection:'column', gap: 10,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 10 }}>
      <span style={{
        width: 36, height: 36, borderRadius:'50%',
        background: entityHsl('toolkit'),
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 18, fontWeight: 800, flexShrink: 0,
      }}>✓</span>
      <div style={{ flex: 1 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          color:'var(--text)',
        }}>Tutti gli 8 hanno confermato 🎉</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
          fontWeight: 700, marginTop: 2,
        }}>Quando sei pronto, avvia la sessione live</div>
      </div>
    </div>
    <button type="button" style={{
      padding:'14px', borderRadius:'var(--r-md)',
      background:`linear-gradient(135deg, ${entityHsl('session')}, hsl(${DS.EC.session.h - 15}, ${DS.EC.session.s}%, ${DS.EC.session.l - 8}%))`,
      border:'none', color:'#fff',
      fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, cursor:'pointer',
      boxShadow:`0 4px 14px ${entityHsl('session', 0.4)}`,
      display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
    }}>🎯 Avvia sessione live</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// SHELLS
// ═══════════════════════════════════════════════════════

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>21:42</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const PhoneTopNav = ({ title = 'Serata' }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', background:'var(--glass-bg)',
    backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border-light)',
    position:'sticky', top: 0, zIndex: 7,
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 16, cursor:'pointer',
    }}>←</button>
    <div style={{
      flex: 1, fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
      color:'var(--text)', textAlign:'center',
    }}>{title}</div>
    <button aria-label="Condividi" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text-sec)', fontSize: 14, cursor:'pointer',
    }}>↗</button>
  </div>
);

// ─── Scenarios per state ─────────────────────────────────
const RSVP_SCENARIOS = {
  'host-pending': {
    'p-marco':'yes', 'p-davide':'yes', 'p-giulia':'yes',
    'p-luca':'pending', 'p-sara':'maybe', 'p-aaron':'pending',
    'p-federica':'no', 'p-matteo':'pending',
  },
  'host-ready': {
    'p-marco':'yes', 'p-davide':'yes', 'p-giulia':'yes',
    'p-luca':'yes', 'p-sara':'yes', 'p-aaron':'yes',
    'p-federica':'yes', 'p-matteo':'yes',
  },
  'invitee-pending': {
    'p-marco':'yes', 'p-davide':'pending', 'p-giulia':'yes',
    'p-luca':'yes', 'p-sara':'maybe', 'p-aaron':'yes',
    'p-federica':'no', 'p-matteo':'pending',
  },
  'invitee-confirmed': {
    'p-marco':'yes', 'p-davide':'yes', 'p-giulia':'yes',
    'p-luca':'yes', 'p-sara':'maybe', 'p-aaron':'yes',
    'p-federica':'no', 'p-matteo':'pending',
  },
};

// /* Mobile screen wrapper for one state */
const MobileScreen = ({ id, label, gherkin, status = 'pending', tab = 'detail',
  rsvpKey, viewer, viewerRSVP, sticky, banner, voting }) => {
  const TABS = [
    { id:'detail', label:'Dettagli', icon:'📋', badge: rsvpKey ? Object.values(RSVP_SCENARIOS[rsvpKey]).filter(s => s === 'yes').length : null },
    { id:'voting', label:'Voting',   icon:'🗳️', badge: 7 },
    { id:'chat',   label:'Chat',     icon:'💬', badge: 12 },
  ];
  return (
    <section id={id} data-screen-label={`SP7-B · ${label}`} className="phone-shell">
      <div className="state-caption">
        <span className="state-id">{id.replace('state-','#')}</span>
        <span className="state-label">📱 {label}</span>
        {gherkin && <span className="gherkin">{gherkin}</span>}
      </div>
      <div className="phone">
        <PhoneSbar/>
        <div style={{ flex: 1, overflowY:'auto', display:'flex', flexDirection:'column', background:'var(--bg)' }}>
          <PhoneTopNav/>
          {banner === 'cancelled' && <CancelledBanner/>}
          <Hero status={status} mobile/>
          {banner === 'inprogress' && <InProgressCTA/>}
          {banner === 'completed' && <CompletedCTA/>}
          {banner === 'host-ready' && <HostReadyCTA/>}
          <Tabs tabs={TABS} active={tab}/>
          {tab === 'detail' && rsvpKey && (
            <TabDettagli rsvpMap={RSVP_SCENARIOS[rsvpKey]} mobile viewer={viewer} viewerRSVP={viewerRSVP}/>
          )}
          {tab === 'voting' && <TabVoting tied={voting === 'tied'} mobile/>}
          {tab === 'chat' && <TabChat mobile/>}
          {sticky === 'rsvp-pending' && <RSVPBottomSheet/>}
          {sticky === 'rsvp-confirmed' && <RSVPBottomSheet confirmed/>}
        </div>
      </div>
    </section>
  );
};

// ─── DESKTOP SPLIT VIEW ─────────────────────────────────
const DesktopNav = () => (
  <div style={{
    display:'flex', alignItems:'center', gap: 14,
    padding:'10px 24px',
    background:'var(--glass-bg)', backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border)',
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 9 }}>
      <div style={{
        width: 26, height: 26, borderRadius: 7,
        background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('session')})`,
        color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
        fontWeight: 800, fontSize: 13, fontFamily:'var(--f-display)',
      }}>M</div>
      <span style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 14 }}>MeepleAI</span>
    </div>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      marginLeft: 22,
    }}>
      <span>Serate</span>
      <span aria-hidden="true"> / </span>
      <strong style={{ color:'var(--text-sec)' }}>Sabato boardgame con i Padovani</strong>
    </div>
    <Avatar player={byPid['p-marco']} size={28}/>
  </div>
);

const DesktopShell = ({ id, label, tab, status = 'pending', rsvpKey = 'host-pending', voting }) => {
  const TABS = [
    { id:'detail', label:'Dettagli', icon:'📋', badge: 6 },
    { id:'voting', label:'Voting',   icon:'🗳️', badge: 7 },
    { id:'chat',   label:'Chat',     icon:'💬', badge: 12 },
  ];
  return (
    <section id={id} data-screen-label={`SP7-B · ${label}`} className="desktop-shell">
      <div className="state-caption">
        <span className="state-id">{id.replace('state-','#')}</span>
        <span className="state-label">🖥️ {label}</span>
      </div>
      <div className="desktop-frame">
        <div className="desktop-bar">
          <span className="traffic"/><span className="traffic"/><span className="traffic"/>
          <span className="url">meepleai.app/game-nights/gn-042</span>
        </div>
        <div style={{ background:'var(--bg)' }}>
          <DesktopNav/>
          <div style={{
            display:'grid',
            gridTemplateColumns:'380px minmax(0, 1fr)',
            minHeight: 720,
          }}>
            {/* Sidebar */}
            <aside style={{
              borderRight:'1px solid var(--border)',
              display:'flex', flexDirection:'column',
              overflow:'hidden',
            }}>
              <Hero status={status} mobile={false}/>
              <div style={{ padding:'14px', display:'flex', flexDirection:'column', gap: 10 }}>
                <Label>Party · 6/8 confermati</Label>
                <div style={{ display:'flex', flexWrap:'wrap', gap: 6 }}>
                  {PARTY.map(p => {
                    const s = RSVP_SCENARIOS[rsvpKey][p.id];
                    return (
                      <div key={p.id} style={{
                        display:'inline-flex', alignItems:'center', gap: 6,
                        padding:'4px 8px 4px 4px',
                        borderRadius:'var(--r-pill)',
                        background: s === 'yes'   ? 'hsl(var(--c-success) / 0.1)'
                                  : s === 'maybe' ? 'hsl(var(--c-warning) / 0.1)'
                                  : s === 'no'    ? 'var(--bg-muted)'
                                  : 'var(--bg-card)',
                        border: s === 'yes'   ? '1px solid hsl(var(--c-success) / 0.3)'
                              : s === 'maybe' ? '1px solid hsl(var(--c-warning) / 0.3)'
                              : s === 'no'    ? '1px solid var(--border)'
                              : `1px dashed ${entityHsl('player', 0.4)}`,
                        opacity: s === 'no' ? 0.6 : 1,
                      }}>
                        <Avatar player={p} size={20}/>
                        <span style={{
                          fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
                          color: s === 'yes'   ? 'hsl(var(--c-success))'
                               : s === 'maybe' ? 'hsl(var(--c-warning))'
                               : s === 'no'    ? 'var(--text-muted)'
                               : 'var(--text-sec)',
                        }}>{p.initials}</span>
                      </div>
                    );
                  })}
                </div>
                <button type="button" style={{
                  marginTop: 8,
                  padding:'10px', borderRadius:'var(--r-md)',
                  background:'var(--bg-card)',
                  border:`1px solid ${entityHsl('event', 0.35)}`,
                  color: entityHsl('event'),
                  fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
                  cursor:'pointer',
                }}>✏️ Modifica serata</button>
              </div>
            </aside>

            {/* Main */}
            <main style={{ display:'flex', flexDirection:'column', minWidth: 0 }}>
              <Tabs tabs={TABS} active={tab}/>
              <div style={{ flex: 1, overflow:'auto' }}>
                {tab === 'detail' && (
                  <TabDettagli rsvpMap={RSVP_SCENARIOS[rsvpKey]} mobile={false} viewer="host"/>
                )}
                {tab === 'voting' && <TabVoting tied={voting === 'tied'} mobile={false}/>}
                {tab === 'chat'   && <TabChat mobile={false}/>}
              </div>
            </main>
          </div>
        </div>
      </div>
    </section>
  );
};

// ═══════════════════════════════════════════════════════
// ROOT
// ═══════════════════════════════════════════════════════

const STATES = [
  { id:'state-01-host-view-pending',   label:'01 · Host · 3/8 confermati · voting attivo',
    status:'pending',     tab:'detail',  rsvpKey:'host-pending',     viewer:'host', gherkin:'US-31.B.host' },
  { id:'state-02-host-view-ready',     label:'02 · Host · 8/8 confermati · "Avvia sessione live"',
    status:'ready',       tab:'detail',  rsvpKey:'host-ready',       viewer:'host', banner:'host-ready', gherkin:'US-31.B.ready' },
  { id:'state-03-invitee-pending',     label:'03 · Davide invitee · pending · RSVP CTA bottom-sheet',
    status:'pending',     tab:'detail',  rsvpKey:'invitee-pending',  viewer:'invitee', sticky:'rsvp-pending', gherkin:'US-31.B.rsvp' },
  { id:'state-04-invitee-confirmed',   label:'04 · Davide post-tap "Ci sarò" · celebration micro',
    status:'pending',     tab:'detail',  rsvpKey:'invitee-confirmed',viewer:'invitee', viewerRSVP:'yes', sticky:'rsvp-confirmed', gherkin:'US-31.B.rsvp.ok' },
  { id:'state-05-voting-active',       label:'05 · Tab Voting · 7 voti · Twilight in testa',
    status:'pending',     tab:'voting',  rsvpKey:'host-pending',     viewer:'host', gherkin:'US-31.B.vote' },
  { id:'state-06-voting-tied',         label:'06 · Tab Voting · pareggio 3-3 · host scegli (-1h)',
    status:'pending',     tab:'voting',  rsvpKey:'host-pending',     viewer:'host', voting:'tied', gherkin:'US-31.B.vote.tie' },
  { id:'state-07-cancelled',           label:'07 · Cancellata · banner danger + crea nuova',
    status:'cancelled',   tab:'detail',  rsvpKey:'host-pending',     viewer:'host', banner:'cancelled', gherkin:'US-31.B.cancel' },
  { id:'state-08-in-progress',         label:'08 · In corso · session indigo pulsing · "Apri sessione"',
    status:'inprogress',  tab:'detail',  rsvpKey:'host-ready',       viewer:'host', banner:'inprogress', gherkin:'US-31.B.live' },
  { id:'state-09-completed',           label:'09 · Completata · "Registra play record" success',
    status:'completed',   tab:'detail',  rsvpKey:'host-ready',       viewer:'host', banner:'completed', gherkin:'US-31.B.done' },
  { id:'state-10-mobile-tab-chat',     label:'10 · Tab Chat fullscreen · system + free messages + input sticky',
    status:'pending',     tab:'chat',    rsvpKey:'host-pending',     viewer:'host', gherkin:'US-31.B.chat' },
];

const TWEAK_DEFAULTS = /*EDITMODE-BEGIN*/{
  "showStateAnchors": true,
  "theme": "light"
}/*EDITMODE-END*/;

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
      {/* Page header */}
      <header style={{
        maxWidth: 1440, margin:'0 auto 32px',
        display:'flex', alignItems:'flex-start', gap: 16, flexWrap:'wrap',
      }}>
        <div style={{ display:'flex', alignItems:'center', gap: 12, flex: 1, minWidth: 0 }}>
          <div style={{
            width: 40, height: 40, borderRadius: 10,
            background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('chat')})`,
            color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
            fontWeight: 800, fontFamily:'var(--f-display)', fontSize: 18,
          }}>B</div>
          <div>
            <div style={{
              display:'flex', alignItems:'center', gap: 8, marginBottom: 2,
            }}>
              <span style={{
                padding:'2px 8px', borderRadius:'var(--r-pill)',
                background: entityHsl('event', 0.12),
                color: entityHsl('event'),
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform:'uppercase', letterSpacing:'.08em',
                border:`1px solid ${entityHsl('event', 0.22)}`,
              }}>SP7 · Mockup B</span>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
                fontWeight: 700,
              }}>US-31.B · Sprint N+1 · P1</span>
            </div>
            <div style={{
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 22,
              color:'var(--text)', letterSpacing:'-.01em',
            }}>Game Night · Detail + RSVP</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
              fontWeight: 600, marginTop: 2,
            }}>
              <code>/game-nights/[id]</code> · hero + 3 tab + RSVP one-tap mobile · split-view desktop
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
        {/* Mobile section */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('event') }}/>
            <h2 className="section-title">Mobile · 390 — Hero + 3 tab + RSVP one-tap</h2>
            <span className="section-meta">10 stati · iPhone 15 viewport</span>
          </div>
          <div className="mobile-grid">
            {STATES.map(s => <MobileScreen key={s.id} {...s}/>)}
          </div>
        </div>

        {/* Desktop section */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('session') }}/>
            <h2 className="section-title">Desktop · 1280 — Split-view 380px sidebar + main tab</h2>
            <span className="section-meta">2 varianti tab</span>
          </div>
          <div style={{ display:'flex', flexDirection:'column', gap: 32 }}>
            <DesktopShell
              id="state-11-desktop-split-detail"
              label="11 · Desktop · sidebar 380 + Tab Dettagli (RSVP list 8 player)"
              tab="detail" status="pending" rsvpKey="host-pending"/>
            <DesktopShell
              id="state-12-desktop-split-voting"
              label="12 · Desktop · sidebar 380 + Tab Voting (3 candidates, 7 voti)"
              tab="voting" status="pending" rsvpKey="host-pending"/>
          </div>
        </div>

        {/* Footnote */}
        <footer style={{
          padding:'24px 0', borderTop:'1px solid var(--border)',
          display:'flex', flexDirection:'column', gap: 10, maxWidth: 880, margin:'0 auto',
        }}>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            color:'var(--text-muted)', textTransform:'uppercase', letterSpacing:'.08em',
          }}>Pattern note</div>
          <ul style={{
            margin: 0, paddingLeft: 18,
            fontSize: 12.5, color:'var(--text-sec)', lineHeight: 1.7,
          }}>
            <li>Hero status badge: 4 lifecycle (pending=event rose pulsing · ready=toolkit · inprogress=session indigo pulsing · completed=muted ✓ · cancelled=danger).</li>
            <li>3 tab animated underline (event accent) — Dettagli (default) / Voting / Chat. Sticky tab-bar mobile + desktop.</li>
            <li>RSVP CTA bottom-sheet sticky: 3 bottoni grandi (Ci sarò=success / Forse=warning / Non posso=muted). Glass-bg + blur. Confirmed state celebration micro (sparkles 2s, no full confetti).</li>
            <li>Voting card: bar progress entityHsl('game'), avatar voters, "+1/-1 voto" toggle pill. Tied state-06 → host resolver con 2 cards + "scegli tu" CTA event accent.</li>
            <li>Chat: bubble entityHsl('chat') gradient per messaggi miei, system messages italic muted con icon. Input sticky bottom + send round button.</li>
            <li>Solo helper <code>entityHsl()</code> e token semantic <code>var(--c-*)</code>. Zero <code>hsl(num,...)</code> hardcoded eccetto game cover gradients (decorativi non-token).</li>
            <li>Anchor id <code>state-NN-...</code> standard SP6/SP7-A; data-screen-label per ogni state.</li>
          </ul>
        </footer>
      </main>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
