/* ===================================================================
   SP7 Game Night — Night Summary (sp7-game-night-summary.jsx)
   Route: /game-nights/[id]/summary  (post-end final state)
   Persona: tutti i partecipanti, post-serata. Mobile primario per share,
            desktop per archiviazione/review.
   Coverage: US-31 G31.16 / G31.17 / G31.18 — Sprint N+1 P1
   Pattern: Full-screen single column scrollable · hero + sections stack

   Stati (6 totali)
   - state-01-summary-full           3 games completed · 28 diary events · 6 foto
   - state-02-summary-no-photos      Stesso recap · gallery empty con placeholder CTA "Aggiungi foto"
   - state-03-summary-single-game    Serata 1 game (no transition · no per-game multipli · stats ridotte)
   - state-04-share-success-toast    Post-share toast "Link copiato" entityHsl('toolkit', 0.1)
   - state-05-archived               Post-archive banner muted + CTA "Torna alla lista"
   - state-06-mobile-single-col      Mobile vertical stack (390 fullscreen · padding ridotto)

   Continuità dati con K + L
   - Serata: "Sabato boardgame con i Padovani" · sab 17 mag 21:00 · Casa Marco
   - 3 games completati:
       Brass Birmingham   · winner Davide 178pt   · 2h 45m · top-3 (Davide/Marco/Giulia)
       Spirit Island      · coop · winner team   · 1h 50m · round 5/6
       Wingspan           · winner Giulia 92pt    · 1h 25m · top-3 (Giulia/Sara/Aaron)
   - Total: 28 diary events · 6h 15m totale · 6 foto placeholder
   - MVP serata: Davide (1 win + most events generated)
   - 6 player: Marco (host), Giulia, Davide, Luca, Sara, Aaron

   Entity color mapping
   - hero MVP banner              = entityHsl('event')   rose
   - KPI grid (totale sessioni)   = entityHsl('session') indigo
   - KPI grid (durata)            = entityHsl('event')   rose
   - KPI grid (diary events)      = entityHsl('chat')    blue
   - KPI grid (winner per gioco)  = entityHsl('player')  violet
   - per-game recap CTA           = entityHsl('session') (link a /sessions/[id])
   - foto gallery placeholder     = entityHsl('event', 0.1) tint
   - share success toast          = entityHsl('toolkit') green
   - archived banner              = var(--text-muted)

   Componenti v2 emersi
   - NightSummaryHero            → apps/web/src/components/ui/v2/night-summary-hero/
   - MVPBanner                   → ui/v2/mvp-banner/
   - KPIStatGrid + KPIStatCard   → ui/v2/kpi-stat-grid/
   - PerGameRecapRow             → ui/v2/per-game-recap-row/
   - CollapsedDiaryByGame        → ui/v2/collapsed-diary-by-game/ (CrossGame collapsed mode)
   - NightPhotoGallery           → ui/v2/night-photo-gallery/
   - ShareSuccessToast           → ui/v2/share-success-toast/ (varianti per archive/copy/x)
   - ArchivedBanner              → ui/v2/archived-banner/

   Riusi pattern
   - CrossGameDiaryTimeline (sp7-k) — qui in mode collapsed-by-game
   - MeepleCard variant compact     — per-game recap rows
   - StatusBadge lifecycle           — "Completata" badge da SP7-B
   - AutoSaveToast pattern (sp7-k) — share toast riuso bottom-right
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
// FIXTURE — Sabato Padovani · serata completata
// ═══════════════════════════════════════════════════════

const NIGHT = {
  id: 'gn-padovani-may17',
  title: 'Sabato boardgame con i Padovani',
  dateLine: 'sabato 17 maggio 2026',
  startedAt: '21:00',
  endedAt: '03:15',
  duration: '6h 15m',
  location: 'Casa Marco · Padova',
  hostId: 'p-marco',
};

const PLAYERS = [
  { id:'p-marco',   initials:'MR', name:'Marco',   color: 262, role:'host' },
  { id:'p-giulia',  initials:'GM', name:'Giulia',  color: 10  },
  { id:'p-davide',  initials:'DC', name:'Davide',  color: 200 },
  { id:'p-luca',    initials:'LB', name:'Luca',    color: 180 },
  { id:'p-sara',    initials:'ST', name:'Sara',    color: 320 },
  { id:'p-aaron',   initials:'AK', name:'Aaron',   color: 140 },
];
const byPid = Object.fromEntries(PLAYERS.map(p => [p.id, p]));

const GAMES = [
  { id:'gs-brass-1',  gameId:'g-brass',    title:'Brass: Birmingham', publisher:'Roxley',
    emoji:'🏭', cover:['hsl(220 35% 28%)','hsl(28 60% 38%)'],
    duration:'2h 45m', startedAt:'21:00', endedAt:'22:45',
    coopMode: false, winnerId:'p-davide',
    topThree: [
      { id:'p-davide', score: 178 },
      { id:'p-marco',  score: 142 },
      { id:'p-giulia', score: 128 },
    ],
    sessionId: 's-brass-may17', eventsCount: 11, order: 1,
  },
  { id:'gs-spirit-1', gameId:'g-spirit',   title:'Spirit Island', publisher:'GMT',
    emoji:'🌋', cover:['hsl(210 50% 30%)','hsl(150 50% 38%)'],
    duration:'1h 50m', startedAt:'23:05', endedAt:'00:55',
    coopMode: true, winnerId:null,
    coopOutcome: 'Team coop ha vinto · round 5 di 6 · Adversary England 1',
    sessionId: 's-spirit-may17', eventsCount: 8, order: 2,
  },
  { id:'gs-wing-1',   gameId:'g-wingspan', title:'Wingspan', publisher:'Stonemaier',
    emoji:'🦜', cover:['hsl(85 40% 45%)','hsl(35 60% 50%)'],
    duration:'1h 25m', startedAt:'01:30', endedAt:'02:55',
    coopMode: false, winnerId:'p-giulia',
    topThree: [
      { id:'p-giulia', score: 92 },
      { id:'p-sara',   score: 78 },
      { id:'p-aaron',  score: 71 },
    ],
    sessionId: 's-wing-may17', eventsCount: 9, order: 3,
  },
];

const MVP = byPid['p-davide'];

// Diary events condensed (per game). Collapsed mode mostra solo count + breakdown.
// L'expanded di Spirit Island mostra 3 eventi-chiave (highlights, non full list).
const DIARY_HIGHLIGHTS = {
  'gs-brass-1': [
    { t:'22:48', kind:'score',  icon:'📊', actors:['p-marco'],  text:'Marco completa rete Birmingham (+12 link points)' },
    { t:'22:55', kind:'end',    icon:'🏆', actors:['p-davide'], text:'🏆 Davide vince Brass Birmingham 178–142' },
  ],
  'gs-spirit-1': [
    { t:'23:30', kind:'custom', icon:'💡', actors:['p-aaron'],  text:'Aaron usa innate Lightning Strike (2 danno)' },
    { t:'00:42', kind:'score',  icon:'📊', actors:[],           text:'Fear deck stage III · 3 Earthquake' },
    { t:'00:55', kind:'end',    icon:'🏆', actors:[],           text:'🏆 Team coop wins · England 1 sconfitto al round 5/6' },
  ],
  'gs-wing-1': [
    { t:'02:38', kind:'score',  icon:'📊', actors:['p-giulia'], text:'Giulia +15 (Eggs bonus end-game)' },
    { t:'02:55', kind:'end',    icon:'🏆', actors:['p-giulia'], text:'🏆 Giulia vince Wingspan 92–78' },
  ],
};

const KIND_COLOR = {
  turn:   'session',
  score:  'session',
  custom: 'tool',
  chat:   'chat',
  end:    'event',
  system: 'toolkit',
};

const PHOTOS = [
  { id:'ph-1', label:'Setup Brass',       g:'linear-gradient(135deg, hsl(220 35% 30%), hsl(28 60% 40%))' },
  { id:'ph-2', label:'Davide vince',      g:'linear-gradient(135deg, hsl(350 70% 55%), hsl(28 60% 50%))' },
  { id:'ph-3', label:'Tabellone Spirit',  g:'linear-gradient(135deg, hsl(210 50% 30%), hsl(150 50% 40%))' },
  { id:'ph-4', label:'Foto di gruppo',    g:'linear-gradient(135deg, hsl(262 50% 50%), hsl(320 60% 55%))' },
  { id:'ph-5', label:'Tableau Wingspan',  g:'linear-gradient(135deg, hsl(85 50% 50%), hsl(35 60% 55%))' },
  { id:'ph-6', label:'Brindisi finale',   g:'linear-gradient(135deg, hsl(38 80% 55%), hsl(10 70% 50%))' },
];

// ═══════════════════════════════════════════════════════
// PRIMITIVES
// ═══════════════════════════════════════════════════════

const Avatar = ({ player, size = 22, ring }) => (
  <span style={{
    width: size, height: size, borderRadius:'50%',
    background:`hsl(${player.color}, 60%, 55%)`, color:'#fff',
    fontFamily:'var(--f-display)', fontWeight: 800,
    fontSize: size <= 18 ? 8 : size <= 24 ? 9 : size <= 36 ? 12 : 16,
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

// ═══════════════════════════════════════════════════════
// HERO — NightSummaryHero
// /* v2: NightSummaryHero + MVPBanner */
// ═══════════════════════════════════════════════════════

const NightSummaryHero = ({ mobile, mvp = MVP, gamesCount = 3 }) => (
  <header style={{
    position:'relative',
    padding: mobile ? '20px 18px 22px' : '32px 32px 28px',
    background: `linear-gradient(160deg, ${entityHsl('event', 0.18)} 0%, ${entityHsl('event', 0.03)} 100%)`,
    borderBottom: `1px solid ${entityHsl('event', 0.2)}`,
    overflow:'hidden',
  }}>
    {/* Decorative confetti shapes */}
    <div aria-hidden="true" style={{
      position:'absolute', top:-40, right:-40,
      width: 220, height: 220, borderRadius:'50%',
      background: `radial-gradient(circle at 30% 30%, ${entityHsl('event', 0.4)}, transparent 70%)`,
      opacity: .4, pointerEvents:'none',
    }}/>
    <div aria-hidden="true" style={{
      position:'absolute', bottom:-30, left:-30,
      width: 160, height: 160, borderRadius:'50%',
      background: `radial-gradient(circle at 40% 40%, ${entityHsl('toolkit', 0.4)}, transparent 70%)`,
      opacity: .25, pointerEvents:'none',
    }}/>

    {/* Status badge */}
    <div style={{
      display:'flex', alignItems:'center', gap: 8, marginBottom: 12,
      position:'relative',
    }}>
      <span style={{
        display:'inline-flex', alignItems:'center', gap: 6,
        padding:'4px 10px', borderRadius:'var(--r-pill)',
        background: 'var(--bg-card)',
        border:'1px solid var(--border-strong)',
        color:'var(--text-muted)',
        fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.08em',
      }}>
        <span aria-hidden="true">✓</span>
        Serata completata
      </span>
      <MonoKicker size={10}>#GN-042 · 17 mag 2026</MonoKicker>
    </div>

    {/* Title */}
    <h1 style={{
      position:'relative',
      fontFamily:'var(--f-display)', fontSize: mobile ? 26 : 36, fontWeight: 800,
      color:'var(--text)', letterSpacing:'-.02em',
      margin:'0 0 6px', lineHeight: 1.1,
    }}>{NIGHT.title}</h1>

    {/* Date + location */}
    <div style={{
      position:'relative',
      display:'flex', flexWrap:'wrap', gap:'4px 14px', alignItems:'center',
      fontFamily:'var(--f-mono)', fontSize: 12, fontWeight: 700,
      color:'var(--text-sec)', marginBottom: mobile ? 16 : 22,
    }}>
      <span style={{ display:'inline-flex', alignItems:'center', gap: 5 }}>
        <span aria-hidden="true">📅</span>{NIGHT.dateLine}
      </span>
      <span style={{ color:'var(--text-muted)' }}>·</span>
      <span style={{ display:'inline-flex', alignItems:'center', gap: 5 }}>
        <span aria-hidden="true">⏱</span>
        {NIGHT.startedAt} → {NIGHT.endedAt} · <strong style={{ color:'var(--text)' }}>{NIGHT.duration}</strong>
      </span>
      <span style={{ color:'var(--text-muted)' }}>·</span>
      <span style={{ display:'inline-flex', alignItems:'center', gap: 5 }}>
        <span aria-hidden="true">📍</span>{NIGHT.location}
      </span>
    </div>

    {/* MVP banner */}
    {mvp && (
      <div style={{
        position:'relative',
        display:'flex', alignItems:'center', gap: mobile ? 12 : 16,
        padding: mobile ? '12px 14px' : '16px 20px',
        borderRadius:'var(--r-xl)',
        background: entityHsl('event', 0.12),
        border:`1.5px solid ${entityHsl('event', 0.4)}`,
        boxShadow: `0 0 0 4px ${entityHsl('event', 0.08)}, var(--shadow-md)`,
        maxWidth: mobile ? '100%' : 540,
      }}>
        <div style={{ position:'relative', flexShrink: 0 }}>
          <Avatar player={mvp} size={mobile ? 48 : 60}
            ring={`${entityHsl('event', 0.5)}`}/>
          <span aria-hidden="true" style={{
            position:'absolute', top: -6, right: -8,
            fontSize: mobile ? 20 : 26,
            filter:'drop-shadow(0 2px 4px rgba(0,0,0,.25))',
          }}>🏆</span>
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <MonoKicker size={mobile ? 9 : 10} color={entityHsl('event')}>
            MVP della serata
          </MonoKicker>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: mobile ? 19 : 24, fontWeight: 800,
            color:'var(--text)', letterSpacing:'-.01em', lineHeight: 1.15,
            marginTop: 2,
          }}>{mvp.name}</div>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 700,
            color:'var(--text-sec)', marginTop: 3,
          }}>1 vittoria · 11 eventi diary · top scorer Brass</div>
        </div>
        <span aria-hidden="true" style={{
          fontSize: mobile ? 28 : 36, opacity: .35, color: entityHsl('event'),
          flexShrink: 0, marginRight: -4,
        }}>✨</span>
      </div>
    )}
  </header>
);

// ═══════════════════════════════════════════════════════
// KPI STAT GRID — KPIStatGrid + KPIStatCard
// /* v2: KPIStatGrid */
// ═══════════════════════════════════════════════════════

const KPIStatCard = ({ label, value, sub, tone = 'session', icon }) => (
  <div style={{
    padding:'14px 16px',
    borderRadius:'var(--r-lg)',
    background:'var(--bg-card)',
    border:'1px solid var(--border)',
    borderTop: `3px solid ${entityHsl(tone)}`,
    display:'flex', flexDirection:'column', gap: 4, minWidth: 0,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
      <span aria-hidden="true" style={{ fontSize: 13 }}>{icon}</span>
      <MonoKicker size={9.5}>{label}</MonoKicker>
    </div>
    <div style={{
      fontFamily:'var(--f-display)', fontSize: 28, fontWeight: 800,
      color: entityHsl(tone), fontVariantNumeric:'tabular-nums',
      letterSpacing:'-.02em', lineHeight: 1,
    }}>{value}</div>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
      color:'var(--text-muted)',
    }}>{sub}</div>
  </div>
);

const KPIStatGrid = ({ mobile, games = GAMES, eventsCount = 28 }) => {
  const winners = games.filter(g => g.coopMode || g.winnerId).length;
  return (
    <div style={{
      padding: mobile ? '18px 14px 4px' : '24px 32px 8px',
      display:'grid',
      gridTemplateColumns: mobile ? 'repeat(2, 1fr)' : 'repeat(4, 1fr)',
      gap: mobile ? 10 : 14,
    }}>
      <KPIStatCard icon="🎯" tone="session" label="Sessioni totali"
        value={games.length} sub={games.length === 1 ? '1 game · serata breve' : `${games.length} game completati`}/>
      <KPIStatCard icon="⏱" tone="event" label="Durata totale"
        value={NIGHT.duration} sub={`${NIGHT.startedAt} → ${NIGHT.endedAt}`}/>
      <KPIStatCard icon="📜" tone="chat" label="Eventi diary"
        value={eventsCount} sub={`${(eventsCount / Math.max(games.length, 1)).toFixed(1)} avg per session`}/>
      <KPIStatCard icon="🏆" tone="player" label="Winner per gioco"
        value={`${winners}/${games.length}`} sub={games.some(g => g.coopMode) ? '1 coop · 2 competitive' : 'tutti competitive'}/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// PER-GAME RECAP ROW
// /* v2: PerGameRecapRow */
// ═══════════════════════════════════════════════════════

const PerGameRecapRow = ({ game, mobile }) => {
  const winner = game.winnerId ? byPid[game.winnerId] : null;
  const isCoop = game.coopMode;
  return (
    <div style={{
      display:'grid',
      gridTemplateColumns: mobile ? '1fr' : '90px 1fr auto',
      gap: mobile ? 10 : 16,
      alignItems:'center',
      padding: mobile ? 12 : '14px 16px',
      background:'var(--bg-card)',
      border:'1px solid var(--border)',
      borderLeft: `4px solid ${entityHsl(isCoop ? 'toolkit' : 'event')}`,
      borderRadius:'var(--r-lg)',
    }}>
      {/* Cover */}
      <div style={{
        height: mobile ? 110 : 70,
        borderRadius:'var(--r-md)',
        background:`linear-gradient(135deg, ${game.cover[0]}, ${game.cover[1]})`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: mobile ? 44 : 30, position:'relative',
      }} aria-hidden="true">
        <span style={{
          position:'absolute', top: 6, left: 6,
          padding:'2px 7px', borderRadius:'var(--r-pill)',
          background:'rgba(0,0,0,0.55)', color:'#fff',
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          letterSpacing:'.06em',
        }}>#{game.order}</span>
        <span style={{ filter:'drop-shadow(0 3px 8px rgba(0,0,0,.4))' }}>{game.emoji}</span>
      </div>

      {/* Info */}
      <div style={{ minWidth: 0, display:'flex', flexDirection:'column', gap: 6 }}>
        <div style={{ display:'flex', alignItems:'center', gap: 8, flexWrap:'wrap' }}>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
            color:'var(--text)', letterSpacing:'-.01em',
          }}>{game.title}</div>
          {isCoop && (
            <span style={{
              padding:'2px 7px', borderRadius:'var(--r-pill)',
              background: entityHsl('toolkit', 0.12),
              border:`1px solid ${entityHsl('toolkit', 0.3)}`,
              color: entityHsl('toolkit'),
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.06em',
            }}>🤝 Co-op</span>
          )}
          <MonoKicker size={9.5}>
            ⏱ {game.duration} · {game.eventsCount} eventi
          </MonoKicker>
        </div>

        {/* Winner line */}
        <div style={{
          display:'flex', alignItems:'center', gap: 8, flexWrap:'wrap',
        }}>
          {isCoop ? (
            <>
              <span style={{
                display:'inline-flex', alignItems:'center', gap: 5,
                padding:'3px 9px', borderRadius:'var(--r-pill)',
                background: entityHsl('toolkit', 0.12),
                border:`1px solid ${entityHsl('toolkit', 0.3)}`,
                color: entityHsl('toolkit'),
                fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800,
              }}>
                <span aria-hidden="true">🏆</span>
                Team coop ha vinto
              </span>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 10.5, fontWeight: 700,
                color:'var(--text-sec)',
              }}>{game.coopOutcome}</span>
            </>
          ) : winner && (
            <>
              <Avatar player={winner} size={22} ring={entityHsl('event', 0.4)}/>
              <span style={{
                fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
                color:'var(--text)',
              }}>🏆 {winner.name}</span>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
                color: entityHsl('event'), fontVariantNumeric:'tabular-nums',
              }}>{game.topThree[0].score}pt</span>
              {/* Mini score chips */}
              <div style={{ display:'flex', gap: 4, marginLeft: 4 }}>
                {game.topThree.slice(1).map((t, i) => {
                  const p = byPid[t.id];
                  return (
                    <span key={t.id} style={{
                      display:'inline-flex', alignItems:'center', gap: 4,
                      padding:'2px 6px 2px 2px',
                      borderRadius:'var(--r-pill)',
                      background:'var(--bg-muted)',
                      border:'1px solid var(--border)',
                      fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 700,
                      color:'var(--text-sec)',
                      fontVariantNumeric:'tabular-nums',
                    }}>
                      <Avatar player={p} size={16}/>
                      {p.name} {t.score}
                    </span>
                  );
                })}
              </div>
            </>
          )}
        </div>
      </div>

      {/* CTA */}
      <a href={`/sessions/${game.sessionId}`} style={{
        padding:'10px 14px', borderRadius:'var(--r-md)',
        background: entityHsl('session', 0.1),
        border:`1px solid ${entityHsl('session', 0.3)}`,
        color: entityHsl('session'),
        textDecoration:'none',
        fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
        display:'inline-flex', alignItems:'center', gap: 6,
        flexShrink: 0,
        whiteSpace:'nowrap',
        justifyContent: mobile ? 'center' : 'flex-start',
      }}>Vai a session <span aria-hidden="true">→</span></a>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// COLLAPSED CROSS-GAME DIARY TIMELINE
// /* v2: CollapsedDiaryByGame */
// ═══════════════════════════════════════════════════════

const DiaryHighlightRow = ({ e }) => {
  const ec = KIND_COLOR[e.kind] || 'session';
  return (
    <div style={{ display:'flex', gap: 9, alignItems:'flex-start', paddingLeft: 4 }}>
      <div style={{
        width: 22, height: 22, borderRadius:'50%',
        background: entityHsl(ec, 0.14),
        border:`2px solid ${entityHsl(ec, 0.4)}`,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 10, flexShrink: 0,
      }}><span aria-hidden="true">{e.icon}</span></div>
      <div style={{
        flex: 1, minWidth: 0,
        padding:'5px 9px',
        background: e.kind === 'end' ? entityHsl('event', 0.08) : 'var(--bg-muted)',
        borderRadius:'var(--r-sm)',
        borderLeft: e.kind === 'end' ? `2px solid ${entityHsl('event')}` : '2px solid transparent',
      }}>
        <div style={{ display:'flex', alignItems:'center', gap: 6, marginBottom: 1, flexWrap:'wrap' }}>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 9.5, fontWeight: 800,
            color: entityHsl(ec), letterSpacing:'.06em',
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
  );
};

const CollapsedDiaryByGame = ({ mobile, games = GAMES, initialExpanded = ['gs-spirit-1'] }) => {
  const [expanded, setExpanded] = useState(initialExpanded);
  const toggle = (id) => setExpanded(prev =>
    prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
  );
  const total = games.reduce((s, g) => s + g.eventsCount, 0);
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
      <div style={{
        display:'flex', alignItems:'center', justifyContent:'space-between',
        flexWrap:'wrap', gap: 8,
      }}>
        <div>
          <MonoKicker color={entityHsl('event')}>Diary cross-game · collapsed</MonoKicker>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800,
            color:'var(--text)', letterSpacing:'-.01em', marginTop: 2,
          }}>{total} eventi su {games.length} session</div>
        </div>
        <button type="button" style={{
          padding:'7px 12px', borderRadius:'var(--r-md)',
          background:'transparent', color: entityHsl('event'),
          border:`1px solid ${entityHsl('event', 0.32)}`,
          fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
          display:'inline-flex', alignItems:'center', gap: 5,
        }}>
          <span aria-hidden="true">📜</span>Apri timeline completa →
        </button>
      </div>

      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        {games.map(g => {
          const isOpen = expanded.includes(g.id);
          const highlights = DIARY_HIGHLIGHTS[g.id] || [];
          return (
            <div key={g.id} style={{
              background:'var(--bg-card)',
              border:`1px solid var(--border)`,
              borderRadius:'var(--r-lg)',
              overflow:'hidden',
            }}>
              <button type="button" onClick={() => toggle(g.id)}
                aria-expanded={isOpen}
                style={{
                  width:'100%',
                  display:'flex', alignItems:'center', gap: 12,
                  padding:'10px 14px',
                  background: isOpen ? entityHsl('event', 0.05) : 'transparent',
                  border:'none', cursor:'pointer',
                  textAlign:'left', minWidth: 0,
                }}>
                <span style={{
                  width: 36, height: 36, borderRadius:'var(--r-sm)',
                  background:`linear-gradient(135deg, ${g.cover[0]}, ${g.cover[1]})`,
                  display:'inline-flex', alignItems:'center', justifyContent:'center',
                  fontSize: 18, flexShrink: 0,
                }} aria-hidden="true">{g.emoji}</span>
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{
                    fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
                    color:'var(--text)',
                  }}>{g.title}</div>
                  <MonoKicker size={9.5}>
                    {g.startedAt} → {g.endedAt} · {g.duration} · {g.eventsCount} eventi
                  </MonoKicker>
                </div>
                <span style={{
                  padding:'3px 8px', borderRadius:'var(--r-pill)',
                  background: entityHsl('event', 0.1),
                  border:`1px solid ${entityHsl('event', 0.28)}`,
                  color: entityHsl('event'),
                  fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
                  textTransform:'uppercase', letterSpacing:'.06em',
                  flexShrink: 0,
                  fontVariantNumeric:'tabular-nums',
                }}>{g.eventsCount}</span>
                <span aria-hidden="true" style={{
                  fontSize: 14, color:'var(--text-muted)', flexShrink: 0,
                  transform: isOpen ? 'rotate(180deg)' : 'none',
                  transition:'transform var(--dur-sm) var(--ease-out)',
                }}>▾</span>
              </button>
              {isOpen && (
                <div style={{
                  padding:'4px 14px 14px',
                  borderTop:'1px solid var(--border-light)',
                  display:'flex', flexDirection:'column', gap: 8,
                  position:'relative',
                }}>
                  <MonoKicker size={9} color={entityHsl('event')}>Highlights · {highlights.length}</MonoKicker>
                  <div style={{ position:'relative' }}>
                    <div aria-hidden="true" style={{
                      position:'absolute', left: 14, top: 0, bottom: 0, width: 2,
                      background:`linear-gradient(180deg, ${entityHsl('event', 0.3)}, ${entityHsl('event', 0.05)})`,
                    }}/>
                    <div style={{ display:'flex', flexDirection:'column', gap: 7 }}>
                      {highlights.map((e, i) => <DiaryHighlightRow key={i} e={e}/>)}
                    </div>
                  </div>
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// NIGHT PHOTO GALLERY
// /* v2: NightPhotoGallery */
// ═══════════════════════════════════════════════════════

const NightPhotoGallery = ({ photos = PHOTOS, empty, mobile }) => {
  const cols = mobile ? 3 : 4;
  if (empty) {
    return (
      <div style={{
        display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center',
        padding: mobile ? '24px 16px' : '36px 24px', gap: 10, textAlign:'center',
        background: entityHsl('event', 0.05),
        border:`1.5px dashed ${entityHsl('event', 0.35)}`,
        borderRadius:'var(--r-xl)',
      }}>
        <div style={{
          width: 60, height: 60, borderRadius:'50%',
          background: entityHsl('event', 0.12),
          color: entityHsl('event'),
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize: 26,
        }} aria-hidden="true">📷</div>
        <h3 style={{
          margin: 0, fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
          color:'var(--text)', letterSpacing:'-.01em',
        }}>Nessuna foto ancora</h3>
        <p style={{
          margin: 0, fontSize: 12.5, color:'var(--text-sec)', lineHeight: 1.55,
          maxWidth: 380,
        }}>Aggiungi una foto della serata: setup tavolo, foto di gruppo, tableau finali — resteranno legate al play record.</p>
        <button type="button" style={{
          marginTop: 4,
          padding:'10px 18px', borderRadius:'var(--r-md)',
          background: entityHsl('event'), color:'#fff', border:'none',
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
          cursor:'pointer',
          boxShadow:`0 4px 14px ${entityHsl('event', 0.35)}`,
          display:'inline-flex', alignItems:'center', gap: 6,
        }}>+ Aggiungi prima foto</button>
      </div>
    );
  }
  return (
    <div style={{
      display:'grid',
      gridTemplateColumns: `repeat(${cols}, 1fr)`,
      gap: mobile ? 6 : 10,
    }}>
      {photos.map(p => (
        <div key={p.id} style={{
          aspectRatio:'1', borderRadius:'var(--r-md)',
          background: p.g,
          display:'flex', alignItems:'flex-end',
          padding: 8, position:'relative', overflow:'hidden',
          cursor:'pointer',
        }}>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            color:'#fff', background:'rgba(0,0,0,.5)',
            padding:'2px 6px', borderRadius:'var(--r-pill)',
            whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
            maxWidth:'100%',
          }}>{p.label}</span>
        </div>
      ))}
      {/* Add CTA tile */}
      <button type="button" style={{
        aspectRatio:'1', borderRadius:'var(--r-md)',
        background: entityHsl('event', 0.08),
        border:`1.5px dashed ${entityHsl('event', 0.4)}`,
        color: entityHsl('event'),
        cursor:'pointer',
        display:'flex', flexDirection:'column', alignItems:'center', justifyContent:'center',
        gap: 4,
      }}>
        <span aria-hidden="true" style={{ fontSize: 24 }}>+</span>
        <span style={{
          fontFamily:'var(--f-display)', fontSize: 10.5, fontWeight: 800,
        }}>Aggiungi</span>
      </button>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// FOOTER CTA + SHARE TOAST + ARCHIVED BANNER
// ═══════════════════════════════════════════════════════

const FooterCTAs = ({ mobile }) => (
  <div style={{
    display:'flex', gap: 10, flexDirection: mobile ? 'column' : 'row',
    padding: mobile ? '20px 14px 24px' : '28px 32px 36px',
    borderTop:'1px solid var(--border)',
    background:'var(--bg-muted)',
  }}>
    <button type="button" style={{
      flex: mobile ? 'unset' : 1,
      padding:'13px 20px', borderRadius:'var(--r-md)',
      background:`linear-gradient(135deg, ${entityHsl('toolkit')}, hsl(142 60% 38%))`,
      color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
      cursor:'pointer',
      boxShadow:`0 6px 18px ${entityHsl('toolkit', 0.4)}`,
      display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
    }}>
      <span aria-hidden="true" style={{ fontSize: 16 }}>📤</span>
      Condividi riepilogo
    </button>
    <button type="button" style={{
      flex: mobile ? 'unset' : 1,
      padding:'13px 20px', borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border-strong)',
      color:'var(--text)',
      fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
      cursor:'pointer',
      display:'inline-flex', alignItems:'center', justifyContent:'center', gap: 8,
    }}>
      <span aria-hidden="true">✓</span>
      Archivia serata
    </button>
  </div>
);

const ShareSuccessToast = () => (
  <div role="status" aria-live="polite" style={{
    position:'absolute', bottom: 20, right: 20, zIndex: 40,
    display:'flex', alignItems:'center', gap: 10,
    padding:'10px 14px 10px 12px',
    borderRadius:'var(--r-pill)',
    background: entityHsl('toolkit', 0.1),
    border:`1px solid ${entityHsl('toolkit', 0.32)}`,
    backdropFilter:'blur(8px)',
    boxShadow:`var(--shadow-lg), 0 0 0 4px ${entityHsl('toolkit', 0.06)}`,
    animation:'sp7ToastIn .35s var(--ease-spring)',
    maxWidth: 280,
  }}>
    <span aria-hidden="true" style={{
      width: 26, height: 26, borderRadius:'50%',
      background: entityHsl('toolkit'), color:'#fff',
      display:'inline-flex', alignItems:'center', justifyContent:'center',
      fontSize: 13, fontWeight: 800, flexShrink: 0,
    }}>🔗</span>
    <div style={{ display:'flex', flexDirection:'column', gap: 1, minWidth: 0 }}>
      <div style={{
        fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
        color: entityHsl('toolkit'),
      }}>Link copiato</div>
      <MonoKicker size={9}>meepleai.app/s/gn-042 · sparisce in 3s</MonoKicker>
    </div>
  </div>
);

const ArchivedBanner = ({ mobile }) => (
  <div role="status" style={{
    margin: mobile ? '0 14px' : '0 32px',
    padding:'14px 16px',
    background:'var(--bg-muted)',
    border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
    display:'flex', alignItems:'center', gap: 12,
    flexWrap:'wrap',
  }}>
    <span aria-hidden="true" style={{
      width: 36, height: 36, borderRadius:'50%',
      background:'var(--bg-card)',
      border:'1px solid var(--border-strong)',
      color:'var(--text-muted)',
      display:'inline-flex', alignItems:'center', justifyContent:'center',
      fontSize: 16, flexShrink: 0,
    }}>📦</span>
    <div style={{ flex: 1, minWidth: 0 }}>
      <MonoKicker>Serata archiviata</MonoKicker>
      <div style={{
        fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
        color:'var(--text)', marginTop: 1,
      }}>Riepilogo salvato · accessibile da /game-nights/archived</div>
    </div>
    <a href="/game-nights" style={{
      padding:'8px 14px', borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border-strong)',
      color:'var(--text)', textDecoration:'none',
      fontFamily:'var(--f-display)', fontSize: 12.5, fontWeight: 800,
      display:'inline-flex', alignItems:'center', gap: 5, whiteSpace:'nowrap',
    }}>← Torna alla lista</a>
  </div>
);

// ═══════════════════════════════════════════════════════
// MAIN SUMMARY PAGE
// /* v2: NightSummaryPage */
// ═══════════════════════════════════════════════════════

const NightSummaryPage = ({ mobile, games = GAMES, photos = PHOTOS, eventsCount = 28, archived, photosEmpty }) => (
  <div style={{
    background:'var(--bg)', minWidth: 0,
    display:'flex', flexDirection:'column',
  }}>
    <NightSummaryHero mobile={mobile} mvp={MVP} gamesCount={games.length}/>
    {archived && <ArchivedBanner mobile={mobile}/>}
    <KPIStatGrid mobile={mobile} games={games} eventsCount={eventsCount}/>
    <section style={{
      padding: mobile ? '16px 14px' : '20px 32px',
      display:'flex', flexDirection:'column', gap: 12,
    }}>
      <div>
        <MonoKicker color={entityHsl('event')}>Per-game recap</MonoKicker>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800,
          color:'var(--text)', letterSpacing:'-.01em', marginTop: 2,
        }}>{games.length === 1 ? '1 gioco completato' : `${games.length} giochi completati · in ordine cronologico`}</div>
      </div>
      <div style={{ display:'flex', flexDirection:'column', gap: 8 }}>
        {games.map(g => <PerGameRecapRow key={g.id} game={g} mobile={mobile}/>)}
      </div>
    </section>
    <section style={{
      padding: mobile ? '8px 14px 16px' : '12px 32px 20px',
    }}>
      <CollapsedDiaryByGame mobile={mobile} games={games}/>
    </section>
    <section style={{
      padding: mobile ? '8px 14px 16px' : '12px 32px 20px',
      display:'flex', flexDirection:'column', gap: 10,
    }}>
      <div style={{
        display:'flex', alignItems:'center', justifyContent:'space-between',
        flexWrap:'wrap', gap: 6,
      }}>
        <div>
          <MonoKicker color={entityHsl('event')}>Foto della serata</MonoKicker>
          <div style={{
            fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800,
            color:'var(--text)', letterSpacing:'-.01em', marginTop: 2,
          }}>{photosEmpty ? 'Gallery vuota' : `${photos.length} foto caricate`}</div>
        </div>
        {!photosEmpty && (
          <button type="button" style={{
            padding:'6px 11px', borderRadius:'var(--r-pill)',
            background: entityHsl('event', 0.1),
            border:`1px solid ${entityHsl('event', 0.3)}`,
            color: entityHsl('event'),
            fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 800,
            cursor:'pointer', textTransform:'uppercase', letterSpacing:'.06em',
          }}>+ Aggiungi foto</button>
        )}
      </div>
      <NightPhotoGallery photos={photos} empty={photosEmpty} mobile={mobile}/>
    </section>
    <FooterCTAs mobile={mobile}/>
  </div>
);

// ═══════════════════════════════════════════════════════
// FRAMES + HARNESS
// ═══════════════════════════════════════════════════════

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>03:18</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">92%</span></div>
  </div>
);

const DesktopFrame = ({ id, label, desc, gherkin, children, height = 1180, showToast }) => (
  <section id={id} data-screen-label={`SP7-M · ${label}`} className="desktop-shell">
    <div className="state-caption">
      <span className="state-id">{id.replace('state-', '#')}</span>
      <span className="state-label">🖥️ {label}</span>
      {gherkin && <span className="gherkin">{gherkin}</span>}
    </div>
    <div className="desktop-frame">
      <div className="desktop-bar">
        <span className="traffic"/><span className="traffic"/><span className="traffic"/>
        <span className="url">meepleai.app/game-nights/gn-padovani-may17/summary</span>
      </div>
      <div style={{
        background:'var(--bg)', position:'relative', overflow:'hidden',
        height,
      }}>
        <div style={{ height:'100%', overflowY:'auto' }}>
          {children}
        </div>
        {showToast && <ShareSuccessToast/>}
      </div>
    </div>
    {desc && <p className="state-desc">{desc}</p>}
  </section>
);

const PhoneShell = ({ id, label, desc, gherkin, children }) => (
  <section id={id} data-screen-label={`SP7-M · ${label}`} className="phone-shell">
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
        <div style={{ flex: 1, overflowY:'auto', minHeight: 0 }}>
          {children}
        </div>
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
            background:`linear-gradient(135deg, ${entityHsl('event')}, ${entityHsl('toolkit')})`,
            color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
            fontWeight: 800, fontFamily:'var(--f-display)', fontSize: 18,
          }}>M</div>
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
              }}>SP7 · Mockup M · Wave 1+</span>
              <MonoKicker size={10}>US-31.M · Issue #487 · P1</MonoKicker>
            </div>
            <div style={{
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 22,
              color:'var(--text)', letterSpacing:'-.01em',
            }}>Game Night · Summary</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
              fontWeight: 600, marginTop: 2,
            }}>
              <code>/game-nights/[id]/summary</code> · full-screen scrollable · hero MVP + 4 KPI + per-game recap + diary collapsed + foto gallery + share/archive
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
        {/* Desktop section — 5 states */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('event') }}/>
            <h2 className="section-title">Desktop · 1280 full-screen scrollable — 5 stati</h2>
            <span className="section-meta">hero + sections stack</span>
          </div>
          <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
            <DesktopFrame
              id="state-01-summary-full"
              label="01 · Summary full · 3 games · 28 eventi · 6 foto"
              gherkin="G31.16"
              desc="Canonical post-end: hero rosa con MVP banner Davide 1 win + 11 eventi, 4 KPI stat card (3 session · 6h15m · 28 eventi · 3/3 winner), per-game recap 3 row (Brass winner Davide 178, Spirit coop team won, Wingspan winner Giulia 92), diary collapsed con Spirit expanded di default (3 highlights), foto gallery 6 + add CTA, footer toolkit share + archive.">
              <NightSummaryPage/>
            </DesktopFrame>

            <DesktopFrame
              id="state-02-summary-no-photos"
              label="02 · Gallery empty · placeholder CTA 'Aggiungi prima foto'"
              gherkin="G31.16 (no photos)"
              desc="Identico a state-01 ma section foto mostra empty state: card dashed rose con icon 📷 + heading + body + CTA primary 'Aggiungi prima foto'. Tutto il resto invariato.">
              <NightSummaryPage photosEmpty/>
            </DesktopFrame>

            <DesktopFrame
              id="state-03-summary-single-game"
              label="03 · Single game · serata 1 game · stats ridotte"
              gherkin="G31.16 (single)"
              desc="Edge case: serata con 1 solo game (Brass). KPI mostra '1 game · serata breve', '1/1 winner', no per-game multipli, diary collapsed con 1 sola row, recap conciso."
              height={1050}>
              <NightSummaryPage games={[GAMES[0]]} eventsCount={11}/>
            </DesktopFrame>

            <DesktopFrame
              id="state-04-share-success-toast"
              label="04 · Post-share · toast 'Link copiato' bottom-right · toolkit success"
              gherkin="G31.17"
              desc="Toast pill bottom-right entityHsl('toolkit', 0.1) con icon 🔗 + label 'Link copiato' + sublabel 'meepleai.app/s/gn-042 · sparisce in 3s'. Stessa scene di state-01 con toast visibile."
              showToast>
              <NightSummaryPage/>
            </DesktopFrame>

            <DesktopFrame
              id="state-05-archived"
              label="05 · Archiviata · banner muted + 'Torna alla lista'"
              gherkin="G31.18"
              desc="Banner archived inserito subito sotto l'hero (sopra le KPI). Icon 📦 in card muted · MonoKicker 'Serata archiviata' · sublabel '/game-nights/archived' · CTA right 'Torna alla lista' che porta a /game-nights. Footer CTA share/archive restano (archivia è no-op se già archiviata).">
              <NightSummaryPage archived/>
            </DesktopFrame>
          </div>
        </div>

        {/* Mobile section — state-06 */}
        <div>
          <div className="section-header">
            <span className="section-marker" style={{ background: entityHsl('session') }}/>
            <h2 className="section-title">Mobile · 390 single-col — 1 stato</h2>
            <span className="section-meta">padding ridotto · stack vertical</span>
          </div>
          <div className="mobile-grid">
            <PhoneShell
              id="state-06-mobile-single-col"
              label="06 · Mobile single col · stessa pagina · padding ridotto"
              gherkin="G31.16 (mobile)"
              desc="Stessa pagina di state-01 ma con mobile=true: hero font ridotto, KPI 2x2 invece di 4x1, per-game recap stack 1-col con cover 110px height, photo gallery 3 col, footer CTA stack column. Scroll-y interno al phone frame.">
              <NightSummaryPage mobile/>
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
            <li>Full-screen single column scrollable: hero rose tint → archived banner (opzionale) → KPI grid 4-col → per-game recap stack → diary collapsed-by-game → foto gallery → footer CTA. Tutto verticale, no split.</li>
            <li>MVPBanner globale nell'hero entityHsl('event'): avatar 60 + 🏆 emoji floating + nome Quicksand 24 + sublabel mono "1 vittoria · 11 eventi · top scorer Brass". Box-shadow + ring entity-event per dare presenza.</li>
            <li>KPIStatCard pattern: border-top 3px entity color, value Quicksand 28 tabular-nums, label mono kicker, sub mono micro. Border-top tone-aware (session/event/chat/player) anziché bg pieno per minor visual weight.</li>
            <li>PerGameRecapRow grid 90 / 1fr / auto: cover 90×70 con #order badge top-left + emoji centered, info 1fr con winner banner inline (avatar + score + top-2 chip), CTA right "Vai a session →" link a /sessions/[id]. Co-op variant con badge 🤝 toolkit invece di winner avatar.</li>
            <li>CollapsedDiaryByGame: 3 row click-to-expand. Spirit di default expanded (showcase). Highlights = 2-3 eventi chiave per game (non full list di 28 — quella si apre con "Apri timeline completa →" che porta al CrossGameDiaryTimeline full di K).</li>
            <li>NightPhotoGallery grid 4-col desktop / 3-col mobile + add-tile dashed rose. Empty state full card con CTA primary "+ Aggiungi prima foto". Match brief "add-photo CTA entityHsl('event', 0.1) placeholder card".</li>
            <li>Share toast riusa pattern AutoSaveToast di K (bottom-right + spring-in) ma con tone toolkit + icon 🔗 + sublabel timestamp/sparizione. Match brief "Foto gallery (placeholder)" + acceptance G31.17.</li>
            <li>ArchivedBanner: muted neutral · NO entity color (brief "banner muted var(--text-muted)"). Posizionato sopra le KPI (subito dopo hero) come notice non-destructive.</li>
            <li>Light + dark via <code>[data-theme="dark"]</code> + MutationObserver (pattern K/L). Solo <code>entityHsl()</code> + <code>var(--c-*)</code> semantici. Game cover gradients restano fixture decorativi.</li>
            <li>Anchor id <code>state-NN-...</code> standard SP7 + <code>data-screen-label</code>.</li>
          </ul>
        </footer>
      </main>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
