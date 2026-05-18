/* ===================================================================
   SP7 Game Night — Live Hub (sp7-game-night-live.jsx)
   Route: /game-nights/[id]/live
   Persona: Marco (host) durante la serata, desktop al tavolo / tablet.
            Mobile per quick-jump tra session.
   Coverage: US-31 G31.11 / G31.12 / G31.13 — Sprint N+1 P1 backbone
   Pattern desktop: 3-pane (sidebar planned 280px | center current 1fr | sidebar diary 320px)
   Pattern mobile : fullscreen swipeable tabs (Current / Planned / Diary)

   Stati (anchor id state-NN-…)
   - state-01-game-1-in-progress     Game 1 live, diary 5 eventi, 2 planned upcoming
   - state-02-mid-night-game-2       Game 1 completed (winner), Game 2 in-progress, diary 18 eventi
   - state-03-transition-pending     Tra Game 1 e Game 2, current pane "In attesa transition"
   - state-04-paused                 Serata in pausa (overlay entityHsl('agent', 0.1) warning)
   - state-05-diary-empty            Inizio serata · diary "Nessun evento ancora"
   - state-06-diary-widget-embedded  Diary widget 320×400 isolato (riuso standalone)
   - state-07-mobile-tab-current     Mobile tab "Current" attivo
   - state-08-mobile-tab-planned     Mobile tab "Planned" attivo
   - state-09-mobile-tab-diary       Mobile tab "Diary" attivo
   - state-10-auto-save-toast        Toast bottom-right entityHsl('toolkit', 0.1) ogni 60s
                                     (acceptance criterion issue #487)

   Entity color mapping
   - serata event accent      = entityHsl('event')   rose
   - session live current     = entityHsl('session') indigo pulsing
   - games planned cards      = entityHsl('game')    orange
   - players confermati       = entityHsl('player')  violet
   - diary chat events        = entityHsl('chat')    blue
   - auto-save toast bg       = entityHsl('toolkit', 0.1) green tint
   - pause overlay accent     = var(--c-warning)     amber

   Componenti v2 emersi (per impl post-merge)
   - GameNightLiveHub          → apps/web/src/components/ui/v2/game-night-live-hub/
   - LiveHubTopBar             → ui/v2/live-hub-top-bar/
   - LiveHubToolbar            → ui/v2/live-hub-toolbar/      (Pausa · Transition · End)
   - PlannedGamesPane          → ui/v2/planned-games-pane/
   - PlannedGameRow            → ui/v2/planned-game-row/      (status ✅ / 🔴 / ⏳)
   - CurrentGameCard           → ui/v2/current-game-card/
   - CrossGameDiaryTimeline    → ui/v2/cross-game-diary-timeline/  (aggregato multi-session)
   - DiaryInlineWidget         → ui/v2/diary-inline-widget/         (320×400 export riusabile)
   - AutoSaveToast             → ui/v2/auto-save-toast/             (60s tick, bottom-right)
   - LiveHubMobileTabs         → ui/v2/live-hub-mobile-tabs/
   - PauseOverlay              → ui/v2/pause-overlay/               (riuso da sp4-session-live)
   - TransitionPendingPane     → ui/v2/transition-pending-pane/

   Riusi pattern
   - ConnectionBar / ConnectionPip   — wave 1 (rendering 1:1 PR #568)
   - SessionDiaryTimeline             — sp4-session-live-parts.jsx (CrossGame deriva da qui)
   - StatusBadge lifecycle             — sp7-game-night-detail-rsvp.jsx
   - Tabs animated underline           — wave 1
   =================================================================== */

const { useState, useEffect, useMemo, useRef } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.event;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ═══════════════════════════════════════════════════════
// FIXTURE — Sabato boardgame con i Padovani · 17 mag 2026
// ═══════════════════════════════════════════════════════

const NIGHT = {
  id: 'gn-padovani-may17',
  title: 'Sabato boardgame con i Padovani',
  dateLine: 'sab 17 maggio · 21:00',
  startedAt: '21:00',
  host: 'p-marco',
  location: 'Casa Marco',
};

const PLAYERS = [
  { id:'p-marco',   initials:'MR', name:'Marco',   color: 262, role:'host' },
  { id:'p-giulia',  initials:'GM', name:'Giulia',  color: 10,  role:'invitee' },
  { id:'p-davide',  initials:'DC', name:'Davide',  color: 200, role:'invitee' },
  { id:'p-luca',    initials:'LB', name:'Luca',    color: 180, role:'invitee' },
  { id:'p-sara',    initials:'ST', name:'Sara',    color: 320, role:'invitee' },
  { id:'p-aaron',   initials:'AK', name:'Aaron',   color: 140, role:'invitee' },
];
const byPid = Object.fromEntries(PLAYERS.map(p => [p.id, p]));

const GAMES = [
  { id:'gs-brass-1',   gameId:'g-brass',    title:'Brass: Birmingham', publisher:'Roxley',
    emoji:'🏭', cover:['hsl(220 35% 28%)','hsl(28 60% 38%)'],
    estimated:'120m', actual:'113m', winnerId:'p-marco', score:'142–128',
    sessionId:'s-brass-may17', status:'completed', order: 1, startedAt:'21:02', endedAt:'22:55' },
  { id:'gs-spirit-1',  gameId:'g-spirit',   title:'Spirit Island', publisher:'GMT',
    emoji:'🌋', cover:['hsl(210 50% 30%)','hsl(150 50% 38%)'],
    estimated:'90m',  actual:'35m elapsed', winnerId:null, score:'round 2 · in corso',
    sessionId:'s-spirit-may17', status:'inprogress', order: 2, startedAt:'23:00', endedAt:null },
  { id:'gs-wing-1',    gameId:'g-wingspan', title:'Wingspan', publisher:'Stonemaier',
    emoji:'🦜', cover:['hsl(85 40% 45%)','hsl(35 60% 50%)'],
    estimated:'60m',  actual:null, winnerId:null, score:'in attesa',
    sessionId:'s-wing-may17', status:'upcoming', order: 3, startedAt:null, endedAt:null },
];

// 18 eventi cross-game (Brass completed → transition → Spirit in-progress)
const DIARY = [
  // ── Brass Birmingham ─────────────────────────────────
  { id:'d01', t:'21:02', game:'gs-brass-1',  kind:'turn',   icon:'🎯', actors:['p-marco'],
    text:'Marco apre il Canal Era — porto a Stoke-on-Trent' },
  { id:'d02', t:'21:15', game:'gs-brass-1',  kind:'score',  icon:'📊', actors:['p-giulia'],
    text:'Giulia: +5 punti (Industria carbone)' },
  { id:'d03', t:'21:34', game:'gs-brass-1',  kind:'turn',   icon:'🎯', actors:['p-davide'],
    text:'Davide costruisce ferrovia Birmingham → Coventry' },
  { id:'d04', t:'21:52', game:'gs-brass-1',  kind:'custom', icon:'💡', actors:['p-sara'],
    text:'Sara passa il turno · scoop +10£' },
  { id:'d05', t:'22:08', game:'gs-brass-1',  kind:'score',  icon:'📊', actors:['p-luca'],
    text:'Luca: −2 income (debito loan)' },
  { id:'d06', t:'22:20', game:'gs-brass-1',  kind:'turn',   icon:'🎯', actors:[],
    text:'Inizia l\'era ferroviaria · scarto canali' },
  { id:'d07', t:'22:36', game:'gs-brass-1',  kind:'custom', icon:'🎲', actors:['p-aaron'],
    text:'Aaron pesca 4 carte (mano era 2)' },
  { id:'d08', t:'22:48', game:'gs-brass-1',  kind:'score',  icon:'📊', actors:['p-marco'],
    text:'Marco completa rete Birmingham (+12 link points)' },
  { id:'d09', t:'22:55', game:'gs-brass-1',  kind:'end',    icon:'🏆', actors:['p-marco'],
    text:'🏆 Marco vince Brass Birmingham 142–128' },
  // ── Transition ───────────────────────────────────────
  { id:'d10', t:'23:00', game:null,           kind:'system', icon:'↻', actors:[],
    text:'Setup Spirit Island · 4 spiriti scelti random' },
  // ── Spirit Island ────────────────────────────────────
  { id:'d11', t:'23:08', game:'gs-spirit-1', kind:'turn',   icon:'🎯', actors:[],
    text:'Round 1 · Esplora — invaders Mountain' },
  { id:'d12', t:'23:14', game:'gs-spirit-1', kind:'score',  icon:'📊', actors:['p-davide'],
    text:'Spirito "River Surges" +3 Fear · livello 1' },
  { id:'d13', t:'23:19', game:'gs-spirit-1', kind:'custom', icon:'💡', actors:['p-aaron'],
    text:'Aaron usa innate Lightning Strike (2 danno)' },
  { id:'d14', t:'23:23', game:'gs-spirit-1', kind:'score',  icon:'📊', actors:['p-davide'],
    text:'Blight su Powerwala — Davide perde 1 presenza' },
  { id:'d15', t:'23:27', game:'gs-spirit-1', kind:'turn',   icon:'🎯', actors:[],
    text:'Round 2 · Crescita · pesca 6 carte minor' },
  { id:'d16', t:'23:30', game:'gs-spirit-1', kind:'chat',   icon:'💬', actors:['p-luca'],
    text:'Luca: "Posso supportare il tuo spirito questo turno?"' },
  { id:'d17', t:'23:33', game:'gs-spirit-1', kind:'score',  icon:'📊', actors:['p-giulia'],
    text:'Giulia raccoglie 3 carte minor (Sacred Site)' },
  { id:'d18', t:'23:35', game:'gs-spirit-1', kind:'custom', icon:'⏳', actors:['p-aaron'],
    text:'Aaron sta decidendo le sue azioni…' },
];

// Mapping diary kind → entity color
const DIARY_COLOR = {
  turn:   'session',
  score:  'session',
  custom: 'tool',
  chat:   'chat',
  end:    'event',
  system: 'toolkit',
};

// ═══════════════════════════════════════════════════════
// PRIMITIVES
// ═══════════════════════════════════════════════════════

const Avatar = ({ player, size = 22, ring }) => (
  <span style={{
    width: size, height: size, borderRadius:'50%',
    background:`hsl(${player.color}, 60%, 55%)`, color:'#fff',
    fontFamily:'var(--f-display)',
    fontWeight: 800, fontSize: size <= 18 ? 8 : size <= 24 ? 9 : 11,
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

// Pulsing dot — sp7Pulse animation defined in HTML
const PulsingDot = ({ color, size = 8 }) => (
  <span aria-hidden="true" style={{
    width: size, height: size, borderRadius:'50%',
    background: color, flexShrink: 0,
    animation:'sp7Pulse 1.6s var(--ease-out) infinite',
  }}/>
);

// /* v2: ConnectionPip (riuso da wave 1, rendering 1:1 PR #568) */
const ConnectionPip = ({ type, count, label }) => {
  const empty = !count || count === 0;
  return (
    <span aria-label={empty ? label : `${label}: ${count}`}
      style={{
        display:'inline-flex', alignItems:'center', gap: 5,
        padding:'3px 9px 3px 6px',
        borderRadius: 999,
        background: empty ? 'transparent' : entityHsl(type, 0.1),
        border: empty
          ? `1px dashed ${entityHsl(type, 0.3)}`
          : `1px solid ${entityHsl(type, 0.28)}`,
        opacity: empty ? 0.6 : 1,
        color: entityHsl(type),
        fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
        fontVariantNumeric:'tabular-nums',
      }}>
      <span aria-hidden="true" style={{
        width: 14, height: 14, borderRadius:'50%',
        background: entityHsl(type, empty ? 0.18 : 0.3),
        color: entityHsl(type),
        display:'inline-flex', alignItems:'center', justifyContent:'center',
        fontSize: 9, fontWeight: 800, flexShrink: 0,
      }}>{empty ? '+' : DS.EC[type].em}</span>
      <span style={{ color: empty ? 'var(--text-muted)' : 'inherit' }}>{label}</span>
      {!empty && <strong style={{ marginLeft: 2 }}>{count}</strong>}
    </span>
  );
};

// /* v2: ConnectionBar — riga di pip sotto hero */
const ConnectionBar = ({ pips }) => (
  <div role="navigation" aria-label="Entità collegate" style={{
    display:'flex', gap: 6, flexWrap:'wrap', alignItems:'center',
    padding:'8px 16px',
    background:'var(--bg-card)',
    borderBottom:'1px solid var(--border)',
  }}>
    <MonoKicker size={9}>Collegate ·</MonoKicker>
    {pips.map((p, i) => <ConnectionPip key={i} {...p}/>)}
  </div>
);

// ═══════════════════════════════════════════════════════
// LIVE TOP BAR (title + status + counter + toolbar)
// /* v2: LiveHubTopBar */
// ═══════════════════════════════════════════════════════

const LiveHubTopBar = ({ paused, compact, current = 2, total = 3, elapsed = '23m', confirmed = 6, nightStatus = 'live' }) => {
  const accent = paused ? 'hsl(var(--c-warning))' : entityHsl('session');
  const statusLabel = paused ? 'In pausa'
                    : nightStatus === 'transition' ? 'Transition'
                    : 'Live';
  return (
    <header style={{
      display:'flex', flexDirection:'column',
      background:'var(--glass-bg)', backdropFilter:'blur(14px)',
      borderBottom:`1px solid ${entityHsl('event', 0.25)}`,
      position:'sticky', top: 0, zIndex: 20,
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: compact ? 8 : 12,
        padding: compact ? '10px 12px' : '12px 18px',
        minHeight: compact ? 56 : 64,
      }}>
        {/* Left: back + title */}
        <button type="button" aria-label="Indietro" style={{
          width: compact ? 32 : 34, height: compact ? 32 : 34,
          borderRadius:'var(--r-md)',
          background:'var(--bg-card)', border:'1px solid var(--border)',
          color:'var(--text-sec)', cursor:'pointer',
          fontSize: 16, fontWeight: 800, flexShrink: 0,
        }}>←</button>

        <div style={{ display:'flex', flexDirection:'column', gap: 2, minWidth: 0, flex: 1 }}>
          <div style={{
            display:'flex', alignItems:'center', gap: 7, flexWrap:'wrap',
          }}>
            {/* Status pip */}
            <span style={{
              display:'inline-flex', alignItems:'center', gap: 5,
              padding:'2px 8px 2px 6px',
              borderRadius:'var(--r-pill)',
              background: paused
                ? 'hsl(var(--c-warning) / .14)'
                : nightStatus === 'transition' ? entityHsl('event', 0.14)
                : entityHsl('session', 0.14),
              border: `1px solid ${paused ? 'hsl(var(--c-warning) / .3)' : entityHsl(nightStatus === 'transition' ? 'event' : 'session', 0.3)}`,
              color: accent,
              fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.08em',
            }}>
              {!paused && <PulsingDot color={accent} size={6}/>}
              {paused && <span aria-hidden="true">⏸</span>}
              {statusLabel}
            </span>
            <MonoKicker size={9.5}>#GN-042</MonoKicker>
          </div>
          <div style={{
            display:'flex', alignItems:'center', gap: 8,
            fontFamily:'var(--f-display)', fontSize: compact ? 13 : 14.5, fontWeight: 800,
            color:'var(--text)',
            whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
            minWidth: 0,
          }}>
            <span aria-hidden="true">🎉</span>
            <span style={{ overflow:'hidden', textOverflow:'ellipsis' }}>
              {compact ? 'Padovani · 17 mag' : NIGHT.title}
            </span>
          </div>
        </div>

        {/* Center counter: Game X/Y · elapsed (desktop only) */}
        {!compact && (
          <div style={{
            display:'flex', alignItems:'center', gap: 12,
            padding:'6px 14px', borderRadius:'var(--r-md)',
            background: entityHsl('event', 0.08),
            border:`1px solid ${entityHsl('event', 0.22)}`,
            flexShrink: 0,
          }}>
            <div style={{ textAlign:'center', minWidth: 0 }}>
              <MonoKicker size={9}>Game</MonoKicker>
              <div style={{
                fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
                color: entityHsl('event'), fontVariantNumeric:'tabular-nums', lineHeight: 1,
              }}>{current}<span style={{ color:'var(--text-muted)' }}>/{total}</span></div>
            </div>
            <span style={{ width: 1, height: 22, background:'var(--border)' }}/>
            <div style={{ textAlign:'center', minWidth: 0 }}>
              <MonoKicker size={9}>Elapsed</MonoKicker>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 13.5, fontWeight: 800,
                color:'var(--text)', fontVariantNumeric:'tabular-nums', lineHeight: 1.1,
              }}>{elapsed}</div>
            </div>
            <span style={{ width: 1, height: 22, background:'var(--border)' }}/>
            <div style={{ textAlign:'center', minWidth: 0 }}>
              <MonoKicker size={9}>Player</MonoKicker>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 13.5, fontWeight: 800,
                color: entityHsl('player'), fontVariantNumeric:'tabular-nums', lineHeight: 1.1,
              }}>{confirmed}/6</div>
            </div>
          </div>
        )}

        {/* Right: toolbar */}
        <div style={{
          display:'flex', alignItems:'center', gap: 6,
          flexShrink: 0, marginLeft:'auto',
        }}>
          <ToolbarBtn icon={paused ? '▶' : '⏸'} label={compact ? null : (paused ? 'Riprendi' : 'Pausa')}
            tone={paused ? 'session' : null}/>
          <ToolbarBtn icon="➡️" label={compact ? null : 'Transition'} tone="event"/>
          <ToolbarBtn icon="🛑" label={compact ? null : 'End'} tone="danger"/>
        </div>
      </div>

      {/* Compact counter row mobile */}
      {compact && (
        <div style={{
          display:'flex', gap: 10, padding:'6px 12px 8px',
          borderTop:'1px solid var(--border-light)',
          background:'var(--bg-card)',
          fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 700,
          color:'var(--text-sec)', alignItems:'center',
        }}>
          <span><strong style={{
            color: entityHsl('event'), fontVariantNumeric:'tabular-nums',
          }}>Game {current}/{total}</strong></span>
          <span style={{ color:'var(--text-muted)' }}>·</span>
          <span>Elapsed <strong style={{
            color:'var(--text)', fontVariantNumeric:'tabular-nums',
          }}>{elapsed}</strong></span>
          <span style={{ color:'var(--text-muted)' }}>·</span>
          <span><strong style={{
            color: entityHsl('player'), fontVariantNumeric:'tabular-nums',
          }}>{confirmed}/6</strong> player</span>
        </div>
      )}
    </header>
  );
};

const ToolbarBtn = ({ icon, label, tone }) => {
  const isDanger = tone === 'danger';
  const fg = isDanger ? 'hsl(var(--c-danger))'
           : tone ? entityHsl(tone) : 'var(--text-sec)';
  const bg = tone
    ? (isDanger ? 'hsl(var(--c-danger) / .1)' : entityHsl(tone, 0.1))
    : 'var(--bg-card)';
  const bd = tone
    ? (isDanger ? 'hsl(var(--c-danger) / .28)' : entityHsl(tone, 0.28))
    : 'var(--border)';
  return (
    <button type="button" aria-label={label || icon} style={{
      padding: label ? '7px 12px' : '7px 9px',
      borderRadius:'var(--r-md)',
      background: bg, color: fg, border:`1px solid ${bd}`,
      cursor:'pointer',
      fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
      display:'inline-flex', alignItems:'center', gap: 5,
    }}>
      <span aria-hidden="true">{icon}</span>{label}
    </button>
  );
};

// ═══════════════════════════════════════════════════════
// PLANNED GAMES PANE (left sidebar 280)
// /* v2: PlannedGamesPane + PlannedGameRow */
// ═══════════════════════════════════════════════════════

const PlannedGameRow = ({ game, current }) => {
  const isCompleted = game.status === 'completed';
  const isInProgress = game.status === 'inprogress';
  const isUpcoming = game.status === 'upcoming';
  const accent = isCompleted ? entityHsl('toolkit')
               : isInProgress ? entityHsl('session')
               : entityHsl('game');
  const winner = isCompleted && game.winnerId ? byPid[game.winnerId] : null;
  return (
    <div style={{
      position:'relative',
      padding:'10px 12px',
      borderRadius:'var(--r-md)',
      background: isInProgress ? entityHsl('session', 0.08) : 'var(--bg-card)',
      border: isInProgress
        ? `1px solid ${entityHsl('session', 0.35)}`
        : '1px solid var(--border)',
      borderLeft: `4px solid ${accent}`,
      opacity: isCompleted ? 0.82 : 1,
    }}>
      <div style={{ display:'flex', alignItems:'center', gap: 9, marginBottom: 7 }}>
        <span style={{
          width: 30, height: 30, borderRadius:'var(--r-sm)',
          background:`linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})`,
          display:'inline-flex', alignItems:'center', justifyContent:'center',
          fontSize: 16, flexShrink: 0,
        }} aria-hidden="true">{game.emoji}</span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
            color:'var(--text)',
            whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
            textDecoration: isCompleted ? 'none' : 'none',
          }}>{game.title}</div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 700,
            color:'var(--text-muted)',
          }}>#{game.order} · {game.publisher}</div>
        </div>
        <span aria-hidden="true" style={{
          width: 22, height: 22, borderRadius:'50%',
          background: accent,
          color:'#fff', flexShrink: 0,
          display:'inline-flex', alignItems:'center', justifyContent:'center',
          fontSize: 11, fontWeight: 800, fontFamily:'var(--f-display)',
          position:'relative',
        }}>
          {isCompleted && '✓'}
          {isInProgress && (
            <>
              <span style={{ fontSize: 10 }}>●</span>
              <span style={{
                position:'absolute', inset:-3, borderRadius:'50%',
                border:`2px solid ${accent}`, opacity: .4,
                animation:'sp7PulseRing 1.6s var(--ease-out) infinite',
              }}/>
            </>
          )}
          {isUpcoming && <span style={{ fontSize: 10 }}>⏳</span>}
        </span>
      </div>

      {/* Status line */}
      <div style={{
        display:'flex', alignItems:'center', gap: 6,
        fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
        color: accent,
      }}>
        {isCompleted && (
          <>
            <span>FINITO</span>
            <span style={{ color:'var(--text-muted)' }}>·</span>
            <span style={{ color:'var(--text-sec)' }}>{game.actual}</span>
          </>
        )}
        {isInProgress && (
          <>
            <PulsingDot color={accent} size={6}/>
            <span>LIVE</span>
            <span style={{ color:'var(--text-muted)' }}>·</span>
            <span style={{ color:'var(--text-sec)' }}>{game.actual}</span>
          </>
        )}
        {isUpcoming && (
          <>
            <span>UPCOMING</span>
            <span style={{ color:'var(--text-muted)' }}>·</span>
            <span style={{ color:'var(--text-sec)' }}>est. {game.estimated}</span>
          </>
        )}
      </div>

      {/* Winner banner */}
      {winner && (
        <div style={{
          marginTop: 7,
          padding:'5px 8px', borderRadius:'var(--r-sm)',
          background: entityHsl('event', 0.1),
          border:`1px solid ${entityHsl('event', 0.25)}`,
          display:'flex', alignItems:'center', gap: 6,
        }}>
          <Avatar player={winner} size={20}/>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
              color: entityHsl('event'),
              whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
            }}>🏆 {winner.name} ha vinto</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
              color:'var(--text-muted)', fontVariantNumeric:'tabular-nums',
            }}>{game.score}</div>
          </div>
        </div>
      )}
    </div>
  );
};

const PlannedGamesPane = ({ compact, games = GAMES }) => (
  <aside style={{
    width: compact ? '100%' : 280,
    background:'var(--bg-card)',
    borderRight: compact ? 'none' : '1px solid var(--border)',
    display:'flex', flexDirection:'column', flexShrink: 0, minWidth: 0,
  }}>
    <div style={{
      padding:'11px 14px', borderBottom:'1px solid var(--border-light)',
      display:'flex', alignItems:'center', justifyContent:'space-between',
    }}>
      <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
        <span aria-hidden="true">🎲</span>
        <MonoKicker size={10.5}>Planned ({games.length})</MonoKicker>
      </div>
      <span title="Riordino disabilitato in live mode" style={{
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
        color:'var(--text-muted)',
        padding:'2px 6px', borderRadius:'var(--r-pill)',
        background:'var(--bg-muted)',
        border:'1px solid var(--border)',
      }}>🔒 PlayOrder</span>
    </div>
    <div style={{
      flex: 1, overflowY:'auto', minHeight: 0,
      padding: 10, display:'flex', flexDirection:'column', gap: 8,
    }}>
      {games.map(g => <PlannedGameRow key={g.id} game={g}/>)}
    </div>
    <div style={{
      padding:'10px 12px', borderTop:'1px solid var(--border-light)',
    }}>
      <button type="button" disabled title="Aggiungi disponibile fuori live"
        style={{
          width:'100%', padding:'8px',
          borderRadius:'var(--r-md)',
          background:'transparent', color:'var(--text-muted)',
          border:'1px dashed var(--border-strong)',
          fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 700,
          cursor:'not-allowed', opacity: 0.7,
        }}>+ Aggiungi gioco</button>
    </div>
  </aside>
);

// ═══════════════════════════════════════════════════════
// CURRENT GAME CARD (center 1fr)
// /* v2: CurrentGameCard */
// ═══════════════════════════════════════════════════════

const CurrentGameCard = ({ game, compact }) => {
  if (!game) return null;
  return (
    <div style={{
      flex: 1, padding: compact ? '14px' : '20px 24px',
      display:'flex', flexDirection:'column', gap: 16,
      minHeight: 0, overflowY:'auto', minWidth: 0,
    }}>
      <div style={{
        display:'flex', alignItems:'center', gap: 8, flexWrap:'wrap',
      }}>
        <MonoKicker size={10}>Current game</MonoKicker>
        <span style={{
          display:'inline-flex', alignItems:'center', gap: 5,
          padding:'2px 8px', borderRadius:'var(--r-pill)',
          background: entityHsl('session', 0.12),
          border:`1px solid ${entityHsl('session', 0.28)}`,
          color: entityHsl('session'),
          fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.08em',
        }}>
          <PulsingDot color={entityHsl('session')} size={6}/>
          {game.score}
        </span>
      </div>

      {/* Cover hero */}
      <div style={{
        borderRadius:'var(--r-xl)',
        border:`1px solid ${entityHsl('session', 0.25)}`,
        overflow:'hidden',
        boxShadow: `var(--shadow-md), 0 0 0 4px ${entityHsl('session', 0.08)}`,
      }}>
        <div style={{
          height: compact ? 140 : 200,
          background:`linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})`,
          position:'relative', display:'flex',
          alignItems:'flex-end', padding: 16,
        }} aria-hidden="true">
          <div style={{
            position:'absolute', top: 12, right: 12,
            padding:'3px 8px', borderRadius:'var(--r-pill)',
            background:'rgba(0,0,0,0.55)', color:'#fff',
            fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.08em',
            display:'inline-flex', alignItems:'center', gap: 5,
          }}>
            <span style={{
              width: 6, height: 6, borderRadius:'50%',
              background:'#fff',
              animation:'sp7Pulse 1.6s var(--ease-out) infinite',
            }}/>
            Live · session
          </div>
          <div style={{
            position:'absolute', inset: 0,
            background:'linear-gradient(180deg, transparent 50%, rgba(0,0,0,0.55) 100%)',
            pointerEvents:'none',
          }}/>
          <div style={{ position:'relative', display:'flex', alignItems:'flex-end', gap: 14, color:'#fff' }}>
            <span style={{
              fontSize: compact ? 56 : 72, lineHeight: 1,
              filter:'drop-shadow(0 4px 14px rgba(0,0,0,.5))',
            }}>{game.emoji}</span>
            <div>
              <div style={{
                fontFamily:'var(--f-display)', fontSize: compact ? 22 : 30,
                fontWeight: 800, letterSpacing:'-.01em', lineHeight: 1.1,
                textShadow:'0 2px 8px rgba(0,0,0,.4)',
              }}>{game.title}</div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 700,
                opacity: .85, marginTop: 4,
              }}>#{game.order} · {game.publisher} · iniziato {game.startedAt}</div>
            </div>
          </div>
        </div>

        {/* Stats row */}
        <div style={{
          display:'grid',
          gridTemplateColumns: compact ? '1fr 1fr' : 'repeat(4, 1fr)',
          background:'var(--bg-card)',
        }}>
          {[
            { lb:'Round', vl:'2', sub:'di stima 4', tone: 'session' },
            { lb:'Elapsed', vl:'35m', sub:'su 90m est', tone:'event' },
            { lb:'Player attivi', vl:'4', sub:'co-op', tone:'player' },
            { lb:'Fear / Blight', vl:'+3/1', sub:'livello 1', tone:'tool' },
          ].map((s, i) => (
            <div key={i} style={{
              padding:'12px 14px',
              borderRight: i < 3 ? '1px solid var(--border-light)' : 'none',
              borderTop:'1px solid var(--border-light)',
            }}>
              <MonoKicker size={9}>{s.lb}</MonoKicker>
              <div style={{
                fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
                color: entityHsl(s.tone), fontVariantNumeric:'tabular-nums',
                lineHeight: 1.1, marginTop: 2,
              }}>{s.vl}</div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 700,
                color:'var(--text-muted)', marginTop: 1,
              }}>{s.sub}</div>
            </div>
          ))}
        </div>
      </div>

      {/* Player roster strip */}
      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        <div style={{
          display:'flex', alignItems:'center', justifyContent:'space-between',
        }}>
          <MonoKicker>👥 Player · 6 confermati</MonoKicker>
          <button type="button" style={{
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            color: entityHsl('player'), background:'transparent', border:'none',
            cursor:'pointer', padding: 0,
          }}>Apri roster →</button>
        </div>
        <div style={{ display:'flex', gap: 6, flexWrap:'wrap' }}>
          {PLAYERS.map(p => (
            <div key={p.id} style={{
              display:'inline-flex', alignItems:'center', gap: 6,
              padding:'3px 8px 3px 3px', borderRadius:'var(--r-pill)',
              background: entityHsl('player', 0.1),
              border:`1px solid ${entityHsl('player', 0.25)}`,
            }}>
              <Avatar player={p} size={20} ring={entityHsl('player', 0.2)}/>
              <span style={{
                fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
                color: entityHsl('player'),
              }}>{p.name}</span>
              {p.id === game.winnerId && <span aria-hidden="true">🏆</span>}
              {p.role === 'host' && (
                <span style={{
                  fontFamily:'var(--f-mono)', fontSize: 8, fontWeight: 800,
                  textTransform:'uppercase', letterSpacing:'.06em',
                  background: entityHsl('player', 0.18),
                  padding:'1px 4px', borderRadius:'var(--r-sm)',
                }}>HOST</span>
              )}
            </div>
          ))}
        </div>
      </div>

      {/* CTA */}
      <a href="#" style={{
        display:'flex', alignItems:'center', justifyContent:'space-between',
        padding:'14px 18px', borderRadius:'var(--r-lg)',
        background:`linear-gradient(135deg, ${entityHsl('session')}, ${entityHsl('chat')})`,
        color:'#fff', textDecoration:'none',
        boxShadow:`0 8px 24px ${entityHsl('session', 0.4)}`,
      }}>
        <div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.1em', opacity:.85,
          }}>Quick jump</div>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, marginTop: 2,
          }}>Apri sessione live → /sessions/{game.sessionId}</div>
        </div>
        <span aria-hidden="true" style={{ fontSize: 22, fontWeight: 800 }}>↗</span>
      </a>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// TRANSITION PENDING PANE (state-03)
// /* v2: TransitionPendingPane */
// ═══════════════════════════════════════════════════════

const TransitionPendingPane = ({ from = GAMES[0], to = GAMES[1] }) => (
  <div style={{
    flex: 1, padding: 24, overflowY:'auto',
    display:'flex', flexDirection:'column', gap: 16, minWidth: 0,
  }}>
    <div style={{
      display:'flex', alignItems:'center', gap: 8,
    }}>
      <PulsingDot color={entityHsl('event')} size={9}/>
      <MonoKicker color={entityHsl('event')}>In attesa transition</MonoKicker>
    </div>
    <h2 style={{
      fontFamily:'var(--f-display)', fontSize: 26, fontWeight: 800,
      color:'var(--text)', letterSpacing:'-.01em', margin: 0,
    }}>Pronti per il prossimo gioco?</h2>
    <p style={{
      fontSize: 13.5, color:'var(--text-sec)', lineHeight: 1.6, margin: 0, maxWidth: 540,
    }}>
      <strong>{from.title}</strong> è finito. Conferma per avviare la session di <strong>{to.title}</strong>.
      Tutti i giocatori (6) ricevono notifica e il timer parte automaticamente.
    </p>

    <div style={{
      display:'grid', gridTemplateColumns:'1fr auto 1fr', gap: 16, alignItems:'stretch',
    }}>
      <TransitionMiniCard game={from} variant="completed"/>
      <div style={{
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 28, color: entityHsl('event'),
      }} aria-hidden="true">→</div>
      <TransitionMiniCard game={to} variant="upcoming"/>
    </div>

    <div style={{ display:'flex', gap: 10, marginTop: 4 }}>
      <button type="button" style={{
        flex: 1, padding:'13px',
        borderRadius:'var(--r-md)',
        background:`linear-gradient(135deg, ${entityHsl('session')}, ${entityHsl('chat')})`,
        color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
        cursor:'pointer',
        boxShadow:`0 6px 20px ${entityHsl('session', 0.35)}`,
      }}>▶ Avvia Spirit Island</button>
      <button type="button" style={{
        padding:'13px 18px', borderRadius:'var(--r-md)',
        background:'transparent', border:'1px solid var(--border-strong)',
        color:'var(--text-sec)',
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700, cursor:'pointer',
      }}>Salta gioco</button>
      <button type="button" style={{
        padding:'13px 18px', borderRadius:'var(--r-md)',
        background:'transparent', border:'1px solid hsl(var(--c-danger) / 0.4)',
        color:'hsl(var(--c-danger))',
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700, cursor:'pointer',
      }}>Termina serata</button>
    </div>
  </div>
);

const TransitionMiniCard = ({ game, variant }) => {
  const isCompleted = variant === 'completed';
  const accent = isCompleted ? entityHsl('toolkit') : entityHsl('event');
  return (
    <div style={{
      padding: 12,
      borderRadius:'var(--r-lg)',
      background:'var(--bg-card)',
      border:`1px solid ${accent}`,
      boxShadow: `0 0 0 3px ${isCompleted ? entityHsl('toolkit', 0.08) : entityHsl('event', 0.08)}`,
      display:'flex', flexDirection:'column', gap: 8,
    }}>
      <div style={{
        height: 90, borderRadius:'var(--r-md)',
        background:`linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 36,
      }} aria-hidden="true">{game.emoji}</div>
      <MonoKicker size={9} color={accent}>{isCompleted ? '✓ Appena finito' : '⏳ Prossimo'}</MonoKicker>
      <div style={{
        fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
        color:'var(--text)', lineHeight: 1.2,
      }}>{game.title}</div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 700,
        color:'var(--text-muted)',
      }}>
        {isCompleted ? `${game.actual} · ${game.score}` : `Durata est. ${game.estimated} · co-op`}
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// CROSS-GAME DIARY TIMELINE  (right sidebar 320)
// + DIARY INLINE WIDGET (320×400 export riusabile)
// /* v2: CrossGameDiaryTimeline + DiaryInlineWidget */
// ═══════════════════════════════════════════════════════

const DIARY_FILTERS = [
  { id:'all',    label:'Tutti' },
  { id:'turn',   label:'Turn' },
  { id:'score',  label:'Score' },
  { id:'custom', label:'Custom' },
];

const DiaryEvent = ({ e, prevGame, compact }) => {
  const ec = DIARY_COLOR[e.kind] || 'session';
  const game = e.game ? GAMES.find(g => g.id === e.game) : null;
  const gameChanged = game && prevGame && prevGame.id !== game.id;
  return (
    <>
      {/* Cross-game divider — quando cambio session */}
      {(gameChanged || (game && !prevGame)) && (
        <div aria-hidden="true" style={{
          display:'flex', alignItems:'center', gap: 6,
          padding: compact ? '4px 0 2px' : '6px 0 2px',
        }}>
          <span style={{
            flex: 1, height: 1,
            background: `linear-gradient(90deg, transparent, ${entityHsl('event', 0.4)}, transparent)`,
          }}/>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            color: entityHsl('event'), textTransform:'uppercase', letterSpacing:'.1em',
            display:'inline-flex', alignItems:'center', gap: 4,
          }}>
            <span aria-hidden="true">{game.emoji}</span>
            {game.title}
          </span>
          <span style={{
            flex: 1, height: 1,
            background: `linear-gradient(90deg, transparent, ${entityHsl('event', 0.4)}, transparent)`,
          }}/>
        </div>
      )}
      <div style={{
        position:'relative', display:'flex', gap: 9, paddingLeft: 4,
      }}>
        <div style={{
          width: 22, height: 22, borderRadius:'50%',
          background: entityHsl(ec, 0.14),
          border:`2px solid ${entityHsl(ec, 0.4)}`,
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 10, flexShrink: 0, zIndex: 1,
        }}>
          <span aria-hidden="true">{e.icon}</span>
        </div>
        <div style={{
          flex: 1, minWidth: 0,
          padding:'5px 9px',
          background: e.kind === 'end' ? entityHsl('event', 0.08)
                    : e.kind === 'system' ? entityHsl('toolkit', 0.06)
                    : 'var(--bg-muted)',
          borderRadius:'var(--r-sm)',
          borderLeft: e.kind === 'end' ? `2px solid ${entityHsl('event')}`
                    : e.kind === 'system' ? `2px solid ${entityHsl('toolkit')}`
                    : '2px solid transparent',
        }}>
          <div style={{
            display:'flex', alignItems:'center', gap: 6, marginBottom: 1, flexWrap:'wrap',
          }}>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
              color: entityHsl(ec), textTransform:'uppercase', letterSpacing:'.06em',
              fontVariantNumeric:'tabular-nums',
            }}>{e.t}</span>
            {e.actors.length > 0 && (
              <div style={{ display:'flex', gap: 1 }}>
                {e.actors.map(a => {
                  const p = byPid[a];
                  if (!p) return null;
                  return <Avatar key={a} player={p} size={14} ring="var(--bg-muted)"/>;
                })}
              </div>
            )}
          </div>
          <div style={{
            fontFamily:'var(--f-body)', fontSize: 11.5, fontWeight: 600,
            color:'var(--text)', lineHeight: 1.4,
          }}>{e.text}</div>
        </div>
      </div>
    </>
  );
};

// /* v2: DiaryInlineWidget — 320×400 export riusabile (state-06)
//    Usato anche dentro /game-nights/[id] detail sidebar e /sessions/[id]/live */
const DiaryInlineWidget = ({ events = DIARY, width = 320, height = 400, embedded }) => {
  const visible = events.slice(-7);
  return (
    <div style={{
      width: embedded ? width : '100%',
      height: embedded ? height : '100%',
      maxWidth: width,
      display:'flex', flexDirection:'column',
      background:'var(--bg-card)',
      border:`1px solid ${entityHsl('event', 0.3)}`,
      borderRadius:'var(--r-lg)',
      boxShadow: embedded ? `var(--shadow-md), 0 0 0 3px ${entityHsl('event', 0.06)}` : 'none',
      overflow:'hidden',
    }}>
      <div style={{
        padding:'9px 12px',
        background: entityHsl('event', 0.06),
        borderBottom:`1px solid ${entityHsl('event', 0.2)}`,
        display:'flex', alignItems:'center', gap: 7,
      }}>
        <span aria-hidden="true" style={{ fontSize: 13 }}>📜</span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <MonoKicker size={9.5} color={entityHsl('event')}>Diary cross-game</MonoKicker>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 700,
            color:'var(--text-muted)', marginTop: 1,
          }}>{events.length} eventi · 2 session</div>
        </div>
        <span style={{
          padding:'2px 6px', borderRadius:'var(--r-pill)',
          background: entityHsl('session', 0.12),
          color: entityHsl('session'),
          fontFamily:'var(--f-mono)', fontSize: 8.5, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.06em',
          display:'inline-flex', alignItems:'center', gap: 3,
        }}>
          <PulsingDot color={entityHsl('session')} size={5}/>
          Live
        </span>
      </div>
      <div style={{
        flex: 1, overflowY:'auto', padding:'10px 12px',
        display:'flex', flexDirection:'column', gap: 7, position:'relative',
      }}>
        <div aria-hidden="true" style={{
          position:'absolute', left: 17, top: 10, bottom: 10, width: 2,
          background: `linear-gradient(180deg, ${entityHsl('event', 0.3)}, ${entityHsl('event', 0.05)})`,
        }}/>
        {visible.length === 0 && <DiaryEmptyState compact/>}
        {visible.map((e, i) => (
          <DiaryEvent key={e.id} e={e} prevGame={i > 0 ? GAMES.find(g => g.id === visible[i-1].game) : null} compact/>
        ))}
      </div>
      <div style={{
        padding:'7px 10px',
        borderTop:'1px solid var(--border-light)',
        display:'flex', alignItems:'center', justifyContent:'space-between',
      }}>
        <button type="button" style={{
          padding:'3px 8px', borderRadius:'var(--r-pill)',
          background: entityHsl('event', 0.1),
          border:`1px solid ${entityHsl('event', 0.3)}`,
          color: entityHsl('event'),
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          cursor:'pointer', textTransform:'uppercase', letterSpacing:'.06em',
        }}>+ Annota</button>
        <span style={{
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 700,
          color:'var(--text-muted)',
        }}>Apri timeline ↗</span>
      </div>
    </div>
  );
};

const CrossGameDiaryTimeline = ({ events = DIARY, compact }) => {
  const [filter, setFilter] = useState('all');
  const filtered = useMemo(() => {
    if (filter === 'all') return events;
    return events.filter(e => e.kind === filter);
  }, [filter, events]);

  return (
    <aside style={{
      width: compact ? '100%' : 320,
      background:'var(--bg-card)',
      borderLeft: compact ? 'none' : '1px solid var(--border)',
      display:'flex', flexDirection:'column', flexShrink: 0, minWidth: 0,
    }}>
      <div style={{
        padding:'10px 14px', borderBottom:'1px solid var(--border-light)',
        display:'flex', alignItems:'center', gap: 8, flexWrap:'wrap',
      }}>
        <span aria-hidden="true">📜</span>
        <MonoKicker size={10.5}>Diary cross-game</MonoKicker>
        <div style={{ flex: 1 }}/>
        <span style={{
          padding:'2px 7px', borderRadius:'var(--r-pill)',
          background: entityHsl('event', 0.12),
          color: entityHsl('event'),
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.06em',
          fontVariantNumeric:'tabular-nums',
        }}>{events.length} eventi</span>
      </div>

      {/* Filters */}
      <div style={{
        padding:'7px 12px', display:'flex', gap: 4, flexWrap:'wrap',
        borderBottom:'1px solid var(--border-light)',
      }}>
        {DIARY_FILTERS.map(f => (
          <button key={f.id} type="button" onClick={() => setFilter(f.id)}
            aria-pressed={filter === f.id}
            style={{
              padding:'3px 9px', borderRadius:'var(--r-pill)',
              background: filter===f.id ? entityHsl('event', 0.14) : 'transparent',
              border: filter===f.id ? `1px solid ${entityHsl('event', 0.4)}` : '1px solid var(--border)',
              color: filter===f.id ? entityHsl('event') : 'var(--text-sec)',
              fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
              cursor:'pointer',
            }}>{f.label}</button>
        ))}
      </div>

      <div style={{
        flex: 1, overflowY:'auto', minHeight: 0,
        padding:'10px 14px', position:'relative',
      }}>
        <div aria-hidden="true" style={{
          position:'absolute', left: 24, top: 0, bottom: 0, width: 2,
          background:`linear-gradient(180deg, ${entityHsl('event', 0.3)}, ${entityHsl('event', 0.05)})`,
        }}/>
        <div style={{ display:'flex', flexDirection:'column', gap: 9 }}>
          {filtered.map((e, i) => (
            <DiaryEvent key={e.id} e={e}
              prevGame={i > 0 ? GAMES.find(g => g.id === filtered[i-1].game) : null}/>
          ))}
        </div>
      </div>
      <div style={{
        padding:'9px 12px', borderTop:'1px solid var(--border-light)',
        display:'flex', gap: 6,
      }}>
        <button type="button" style={{
          flex: 1, padding:'7px 10px', borderRadius:'var(--r-md)',
          background: entityHsl('event', 0.1),
          border:`1px solid ${entityHsl('event', 0.3)}`,
          color: entityHsl('event'),
          fontFamily:'var(--f-display)', fontSize: 11.5, fontWeight: 800,
          cursor:'pointer',
        }}>+ Annota evento</button>
        <button type="button" aria-label="Esporta" style={{
          width: 34, height: 34, borderRadius:'var(--r-md)',
          background:'var(--bg-card)', border:'1px solid var(--border)',
          color:'var(--text-sec)', fontSize: 13, cursor:'pointer',
        }}>↗</button>
      </div>
    </aside>
  );
};

const DiaryEmptyState = ({ compact }) => (
  <div style={{
    flex: 1, padding: compact ? 12 : 24,
    display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center',
    gap: 8, textAlign:'center',
  }}>
    <div style={{
      width: compact ? 36 : 48, height: compact ? 36 : 48, borderRadius:'50%',
      background: entityHsl('event', 0.1),
      color: entityHsl('event'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: compact ? 18 : 22,
    }} aria-hidden="true">📜</div>
    <div style={{
      fontFamily:'var(--f-display)', fontSize: compact ? 12 : 14, fontWeight: 800,
      color:'var(--text)',
    }}>Nessun evento ancora</div>
    <p style={{
      margin: 0, fontSize: compact ? 10.5 : 12, color:'var(--text-sec)',
      maxWidth: 220, lineHeight: 1.5,
    }}>
      Inizia a giocare! Score, turn change, e annotazioni appariranno qui in tempo reale.
    </p>
  </div>
);

// ═══════════════════════════════════════════════════════
// AUTO-SAVE TOAST (bottom-right · 60s tick)
// /* v2: AutoSaveToast — acceptance criterion issue #487 */
// ═══════════════════════════════════════════════════════

const AutoSaveToast = ({ visible }) => {
  if (!visible) return null;
  return (
    <div role="status" aria-live="polite" style={{
      position:'absolute', bottom: 18, right: 18, zIndex: 40,
      display:'flex', alignItems:'center', gap: 10,
      padding:'10px 14px 10px 12px',
      borderRadius:'var(--r-pill)',
      background: entityHsl('toolkit', 0.1),
      border:`1px solid ${entityHsl('toolkit', 0.32)}`,
      backdropFilter:'blur(8px)',
      boxShadow:`var(--shadow-lg), 0 0 0 4px ${entityHsl('toolkit', 0.06)}`,
      animation:'sp7ToastIn .35s var(--ease-spring)',
    }}>
      <span aria-hidden="true" style={{
        width: 22, height: 22, borderRadius:'50%',
        background: entityHsl('toolkit'), color:'#fff',
        display:'inline-flex', alignItems:'center', justifyContent:'center',
        fontSize: 12, fontWeight: 800,
      }}>✓</span>
      <div style={{ display:'flex', flexDirection:'column', gap: 1, minWidth: 0 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
          color: entityHsl('toolkit'),
        }}>Auto-salvato</div>
        <MonoKicker size={9}>23:35:42 · prossimo tra 60s</MonoKicker>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// PAUSE OVERLAY (state-04)
// ═══════════════════════════════════════════════════════

const PauseOverlay = () => (
  <div style={{
    position:'absolute', inset: 0, zIndex: 30,
    background:'hsl(var(--c-warning) / 0.1)',
    backdropFilter:'blur(8px)',
    display:'flex', alignItems:'center', justifyContent:'center',
    flexDirection:'column', gap: 22, padding: 24,
  }}>
    <div style={{
      padding:'10px 20px', borderRadius:'var(--r-pill)',
      background:'hsl(var(--c-warning) / 0.18)',
      border:'1px solid hsl(var(--c-warning) / 0.4)',
      color:'hsl(var(--c-warning))',
      fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
      textTransform:'uppercase', letterSpacing:'.16em',
      display:'inline-flex', alignItems:'center', gap: 10,
    }}>
      <span aria-hidden="true" style={{ fontSize: 14 }}>⏸</span>
      Serata in pausa
    </div>
    <div style={{ textAlign:'center' }}>
      <div style={{
        fontFamily:'var(--f-display)', fontSize: 56, fontWeight: 800,
        color:'var(--text)', fontVariantNumeric:'tabular-nums',
        letterSpacing:'-.02em', lineHeight: 1,
      }}>04:18</div>
      <div style={{
        fontFamily:'var(--f-body)', fontSize: 13, color:'var(--text-sec)', marginTop: 8,
      }}>Tempo di pausa · auto-save attivo · diary congelato</div>
    </div>
    <div style={{
      display:'flex', flexDirection:'column', gap: 8, width:'100%', maxWidth: 280,
    }}>
      <button type="button" style={{
        padding:'12px 20px', borderRadius:'var(--r-md)',
        background:'hsl(var(--c-warning))', color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, cursor:'pointer',
        boxShadow:`0 6px 22px hsl(var(--c-warning) / 0.4)`,
        display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 6,
      }}><span aria-hidden="true">▶</span>Riprendi serata</button>
      <button type="button" style={{
        padding:'10px 16px', borderRadius:'var(--r-md)',
        background:'transparent', color:'var(--text-sec)',
        border:'1px solid var(--border-strong)',
        fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 700, cursor:'pointer',
      }}>Pausa lunga · salva e chiudi</button>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// DESKTOP LAYOUT (3-pane: 280 / 1fr / 320)
// ═══════════════════════════════════════════════════════

const DesktopBody = ({ centerVariant = 'current', currentGame = GAMES[1], plannedGames = GAMES, diaryEvents = DIARY }) => (
  <div style={{ display:'flex', flex: 1, minHeight: 0 }}>
    <PlannedGamesPane games={plannedGames}/>
    {centerVariant === 'transition'
      ? <TransitionPendingPane from={GAMES[0]} to={GAMES[1]}/>
      : <CurrentGameCard game={currentGame}/>
    }
    <CrossGameDiaryTimeline events={diaryEvents}/>
  </div>
);

// ═══════════════════════════════════════════════════════
// MOBILE LAYOUT (tabs swipeable)
// ═══════════════════════════════════════════════════════

const MobileBody = ({ initialTab = 'current', currentGame = GAMES[1], diaryEvents = DIARY }) => {
  const [tab, setTab] = useState(initialTab);
  const tabs = [
    { id:'current', icon:'🎯', label:'Current', entity:'session' },
    { id:'planned', icon:'🎲', label:'Planned', entity:'game' },
    { id:'diary',   icon:'📜', label:'Diary',   entity:'event' },
  ];
  return (
    <>
      <div style={{
        flex: 1, overflowY:'auto', minHeight: 0,
        background:'var(--bg)',
      }}>
        {tab === 'current' && <CurrentGameCard game={currentGame} compact/>}
        {tab === 'planned' && <PlannedGamesPane games={GAMES} compact/>}
        {tab === 'diary'   && <CrossGameDiaryTimeline events={diaryEvents} compact/>}
      </div>
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
                flex: 1, padding:'9px 4px 10px',
                background: active ? entityHsl(t.entity, 0.08) : 'transparent',
                border:'none',
                borderTop: active ? `2px solid ${entityHsl(t.entity)}` : '2px solid transparent',
                color: active ? entityHsl(t.entity) : 'var(--text-muted)',
                cursor:'pointer',
                display:'flex', flexDirection:'column', alignItems:'center', gap: 1,
                fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
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
// FRAMES + HARNESS
// ═══════════════════════════════════════════════════════

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>23:35</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

const DesktopFrame = ({ id, label, desc, gherkin, children }) => (
  <section id={id} data-screen-label={`SP7-K · ${label}`} className="desktop-shell">
    <div className="state-caption">
      <span className="state-id">{id.replace('state-', '#')}</span>
      <span className="state-label">🖥️ {label}</span>
      {gherkin && <span className="gherkin">{gherkin}</span>}
    </div>
    <div className="desktop-frame">
      <div className="desktop-bar">
        <span className="traffic"/><span className="traffic"/><span className="traffic"/>
        <span className="url">meepleai.app/game-nights/gn-padovani-may17/live</span>
      </div>
      <div style={{ background:'var(--bg)', position:'relative', overflow:'hidden', minHeight: 660 }}>
        {children}
      </div>
    </div>
    {desc && <p className="state-desc">{desc}</p>}
  </section>
);

const PhoneShell = ({ id, label, desc, gherkin, children }) => (
  <section id={id} data-screen-label={`SP7-K · ${label}`} className="phone-shell">
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

const WidgetShell = ({ id, label, desc, gherkin }) => (
  <section id={id} data-screen-label={`SP7-K · ${label}`} className="widget-shell">
    <div className="state-caption">
      <span className="state-id">{id.replace('state-', '#')}</span>
      <span className="state-label">🧩 {label}</span>
      {gherkin && <span className="gherkin">{gherkin}</span>}
    </div>
    <div style={{ display:'flex', alignItems:'flex-start', gap: 14 }}>
      <DiaryInlineWidget embedded width={320} height={400}/>
      <div className="widget-callout">
        <MonoKicker color={entityHsl('event')}>DiaryInlineWidget · export</MonoKicker>
        <ul>
          <li><strong>Riusabile</strong> in <code>/game-nights/[id]</code> detail sidebar</li>
          <li><strong>Embedded</strong> in <code>/sessions/[id]/live</code> right rail</li>
          <li>320×400 · scroll interno · cross-game divider visibile</li>
          <li>Stesso pattern di <code>SessionDiaryTimeline</code> (sp4) ma multi-session</li>
        </ul>
      </div>
    </div>
    {desc && <p className="state-desc" style={{ maxWidth: 720 }}>{desc}</p>}
  </section>
);

const DesktopScene = ({ stateVariant, paused, currentGame = GAMES[1], plannedGames = GAMES, diaryEvents = DIARY, showToast, ...topBarProps }) => (
  <div style={{
    display:'flex', flexDirection:'column',
    minHeight: 660, height: 660,
    background:'var(--bg)', position:'relative', overflow:'hidden',
  }}>
    <LiveHubTopBar paused={paused} nightStatus={stateVariant === 'transition' ? 'transition' : 'live'} {...topBarProps}/>
    <ConnectionBar pips={[
      { type:'event',   count: 1, label:'event' },
      { type:'game',    count: 3, label:'game' },
      { type:'session', count: currentGame ? 1 : 0, label: currentGame ? currentGame.sessionId.replace('s-','') : 'session' },
      { type:'player',  count: 6, label:'player' },
    ]}/>
    <DesktopBody centerVariant={stateVariant === 'transition' ? 'transition' : 'current'}
      currentGame={currentGame} plannedGames={plannedGames} diaryEvents={diaryEvents}/>
    {paused && <PauseOverlay/>}
    <AutoSaveToast visible={showToast}/>
  </div>
);

const MobileScene = ({ initialTab, currentGame = GAMES[1], diaryEvents = DIARY }) => (
  <>
    <LiveHubTopBar compact currentGame={currentGame}/>
    <MobileBody initialTab={initialTab} currentGame={currentGame} diaryEvents={diaryEvents}/>
  </>
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
      {/* Header */}
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
          }}>K</div>
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
              }}>SP7 · Mockup K · Wave 1+</span>
              <MonoKicker size={10}>US-31.K · Issue #487 · P1</MonoKicker>
            </div>
            <div style={{
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 22,
              color:'var(--text)', letterSpacing:'-.01em',
            }}>Game Night · Live Hub</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
              fontWeight: 600, marginTop: 2,
            }}>
              <code>/game-nights/[id]/live</code> · 3-pane desktop (280 / 1fr / 320) · mobile tabs swipeable · diary cross-game multi-session
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
        {/* Desktop section */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('event') }}/>
            <h2 className="section-title">Desktop · 1280 — 3-pane (280 / 1fr / 320) — 5 stati</h2>
            <span className="section-meta">Marco al tavolo · tablet/laptop</span>
          </div>
          <div style={{ display:'flex', flexDirection:'column', gap: 32 }}>
            <DesktopFrame
              id="state-01-game-1-in-progress"
              label="01 · Game 1 in progress · diary 5 eventi · 2 planned upcoming"
              gherkin="G31.11 / G31.12"
              desc="Inizio serata: Brass Birmingham in corso (session indigo pulsing nel pane sx), Spirit Island + Wingspan upcoming. Diary mostra i primi 5 eventi del Brass run. Current pane focus su Brass cover + 4 KPI stats + roster 6 player + quick-jump CTA.">
              <DesktopScene
                currentGame={{ ...GAMES[0], status:'inprogress', score:'round 1 · in corso', actual:'13m elapsed' }}
                plannedGames={[
                  { ...GAMES[0], status:'inprogress', actual:'13m elapsed', score:'round 1 · in corso' },
                  { ...GAMES[1], status:'upcoming' },
                  { ...GAMES[2], status:'upcoming' },
                ]}
                diaryEvents={DIARY.slice(0, 5)}
                current={1} total={3} elapsed="13m"/>
            </DesktopFrame>

            <DesktopFrame
              id="state-02-mid-night-game-2"
              label="02 · Mid-night · Game 1 completed + winner banner · Game 2 in-progress · diary 18 eventi"
              gherkin="G31.11 / G31.12"
              desc="Stato canonical: Brass finito (winner Marco banner nel pane sx) · Spirit Island in corso al centro con 4 KPI · diary aggregato cross-game con divider 'Spirit Island' a separazione visiva delle 2 session.">
              <DesktopScene
                current={2} total={3} elapsed="2h 35m"/>
            </DesktopFrame>

            <DesktopFrame
              id="state-03-transition-pending"
              label="03 · Transition pending tra Game 1 e Game 2 · Avvia / Salta / Termina"
              gherkin="G31.12"
              desc="Center pane sostituito da TransitionPendingPane: recap Brass (completed toolkit) → preview Spirit Island (upcoming event) · 3 CTA Avvia / Salta / Termina. Sidebar sx mostra entrambi i game con stati distinti, diary già con event 'Marco vince' + transition.">
              <DesktopScene stateVariant="transition"
                plannedGames={[
                  GAMES[0],
                  { ...GAMES[1], status:'upcoming', actual: null },
                  GAMES[2],
                ]}
                diaryEvents={DIARY.slice(0, 10)}
                current={2} total={3} elapsed="1h 53m"/>
            </DesktopFrame>

            <DesktopFrame
              id="state-04-paused"
              label="04 · Serata in pausa · overlay amber + timer pausa + auto-save attivo"
              gherkin="G31.13 (acceptance #487)"
              desc="Overlay full-area entityHsl('agent' alias warning) · timer pausa 56 Quicksand · 2 CTA Riprendi / Pausa lunga. Top bar pulsing dot diventa ⏸ amber. Auto-save continua sotto, diary congelato.">
              <DesktopScene paused current={2} total={3} elapsed="2h 35m"/>
            </DesktopFrame>

            <DesktopFrame
              id="state-05-diary-empty"
              label="05 · Inizio serata · diary empty + sidebar sx tutti upcoming"
              gherkin="G31.11"
              desc="Pre-game: serata appena iniziata (#GN-042), nessuna session avviata, diary empty con CTA 'Inizia a giocare!' + sidebar sx 3 game tutti upcoming + center pane mostra Game 1 in attesa di start.">
              <DesktopScene
                currentGame={{ ...GAMES[0], status:'upcoming', score:'in attesa di start', actual:'non iniziato', startedAt:'—' }}
                plannedGames={GAMES.map(g => ({ ...g, status:'upcoming', actual:null, winnerId:null, score:'in attesa', startedAt:null }))}
                diaryEvents={[]}
                current={0} total={3} elapsed="0m"/>
            </DesktopFrame>
          </div>
        </div>

        {/* Widget standalone state-06 */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('event') }}/>
            <h2 className="section-title">Widget standalone · 320×400 — riuso embedded</h2>
            <span className="section-meta">DiaryInlineWidget export</span>
          </div>
          <WidgetShell
            id="state-06-diary-widget-embedded"
            label="06 · DiaryInlineWidget · 320×400 standalone preview"
            gherkin="G31.12 (riuso)"
            desc="Versione compatta export-ready del CrossGameDiaryTimeline. 7 eventi più recenti, divider cross-game, header + footer azioni. Si embedda in altri context (game-night detail, session live sidebar) con stesso codice — solo width/height + flag embedded."/>
        </div>

        {/* Mobile section */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('session') }}/>
            <h2 className="section-title">Mobile · 390 — fullscreen swipeable tabs · 3 stati</h2>
            <span className="section-meta">Quick-jump al tavolo · iPhone 15 viewport</span>
          </div>
          <div className="mobile-grid">
            <PhoneShell id="state-07-mobile-tab-current"
              label="07 · Tab Current · Spirit Island in-progress"
              gherkin="G31.11"
              desc="Tab di default. Top bar compatta 56 con counter row sotto (Game 2/3 · 2h35m · 6/6 player). Cover hero Spirit + 4 KPI 2-col + roster strip + quick-jump CTA.">
              <MobileScene initialTab="current"/>
            </PhoneShell>
            <PhoneShell id="state-08-mobile-tab-planned"
              label="08 · Tab Planned · 3 game (✓ + 🔴 live + ⏳)"
              gherkin="G31.11"
              desc="3 PlannedGameRow stack verticale: Brass completed con winner banner Marco, Spirit Island in-progress pulsing, Wingspan upcoming. PlayOrder locked durante live.">
              <MobileScene initialTab="planned"/>
            </PhoneShell>
            <PhoneShell id="state-09-mobile-tab-diary"
              label="09 · Tab Diary · cross-game timeline · 18 eventi"
              gherkin="G31.12"
              desc="Timeline cross-game completa con divider 'Spirit Island' tra le 2 session. Filter pills Tutti/Turn/Score/Custom. Annota event CTA bottom sticky.">
              <MobileScene initialTab="diary"/>
            </PhoneShell>
          </div>
        </div>

        {/* Auto-save toast state-10 */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('toolkit') }}/>
            <h2 className="section-title">Auto-save toast · acceptance criterion #487</h2>
            <span className="section-meta">60s tick · bottom-right · entityHsl('toolkit', 0.1)</span>
          </div>
          <DesktopFrame
            id="state-10-auto-save-toast"
            label="10 · Auto-save toast 'Auto-salvato' bottom-right · ogni 60s"
            gherkin="G31.13 (acceptance #487)"
            desc="Toast pill in bottom-right con bg entityHsl('toolkit', 0.1) + border 0.32 + check icon. Mostra timestamp HH:MM:SS + countdown prossimo tick. Non blocca interazione, sparisce dopo 4s. Stessa scene di state-02 con toast visibile.">
            <DesktopScene showToast current={2} total={3} elapsed="2h 35m"/>
          </DesktopFrame>
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
            <li>3-pane desktop fisso: 280 PlannedGamesPane | 1fr CurrentGameCard (o TransitionPendingPane) | 320 CrossGameDiaryTimeline. Mobile fullscreen + bottom tabs Current/Planned/Diary entity-aware.</li>
            <li>ConnectionBar inline con pip <code>{'[event=1, game=3, session={current_id}, player={confirmed_count}]'}</code>. Rendering 1:1 prod (PR #568): borderRadius pill, bg pieno entityHsl @ 0.1 non-empty, dashed + opacity 0.6 + Plus icon empty, aria-label corretto.</li>
            <li>CrossGameDiaryTimeline è la versione multi-session del SessionDiaryTimeline di sp4: stesso event row pattern (icon medallion + timestamp mono + avatar attori + bubble) ma con divider visibile a separazione cross-session.</li>
            <li>DiaryInlineWidget esportato 320×400: ri-usato dentro game-night detail sidebar e session live rail. Stesso codice, flag embedded + width/height.</li>
            <li>Auto-save toast (#487 acceptance): bg <code>entityHsl('toolkit', 0.1)</code>, border 0.32, position absolute bottom-right 18/18, animation spring in. Ogni 60s con timestamp HH:MM:SS.</li>
            <li>Pause overlay accent <code>var(--c-warning)</code> (alias agent in <code>tokens.css</code>): respect del brief "entityHsl('agent', 0.1) = warning". Diary congelato durante pause, top bar status switch da Live a In pausa.</li>
            <li>Light + dark via <code>[data-theme="dark"]</code>. Solo <code>entityHsl()</code> helper + <code>var(--c-*)</code> semantic token. Zero <code>hsl(num, ...)</code> hardcoded fuori dalle utility.</li>
            <li>Anchor id <code>state-NN-...</code> standard SP6/SP7 su wrapper top-level + <code>data-screen-label</code> per review meeting.</li>
          </ul>
        </footer>
      </main>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
