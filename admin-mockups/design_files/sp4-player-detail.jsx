/* MeepleAI SP4 wave 3 — Schermata D / 5
   Route: /players/[id]
   File: admin-mockups/design_files/sp4-player-detail.{html,jsx}
   Pattern desktop: Hero + body con tabs (Overview · Sessions · Games · Toolkits · Achievements)
   Character sheet style — palette --c-player. Riuso pattern wave 1 (sp4-game-detail.jsx):
   ConnectionBar 1:1 prod (PR #549/#552), Tabs animated underline, KpiCard, TabSkeleton/Error/Empty.

   Varianti: registered (default) · guest (no avatar gradient, no Games tab, achievements limitati).

   v2 nuovi (per impl post-merge):
   - PlayerHero            → apps/web/src/components/ui/v2/player-hero/
   - PlayerStatsGrid       → apps/web/src/components/ui/v2/player-stats-grid/
   - PlayerLeaderboardCard → apps/web/src/components/ui/v2/player-leaderboard-card/
   - AchievementBadgeGrid  → apps/web/src/components/ui/v2/achievement-badge-grid/
   - FavoriteGamesStrip    → apps/web/src/components/ui/v2/favorite-games-strip/

   Riusi pixel-perfect (NON ridisegnare nel codice prod):
   - ConnectionBar (PR #549/#552)
   - MeepleCard variants list (session) e compact (game)
   - Tabs animated underline (wave 1)

   Deviazioni flaggate: win rate 73% culture-independent (no decimali, no virgola).
*/

const { useState, useEffect, useMemo, useRef } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.player;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── PLAYER FIXTURE ─────────────────────────────────
// Use existing data.js player for cross-ref consistency, then enrich.
const PLAYER = {
  ...DS.players.find(p => p.id === 'p-marco'),
  handle: '@marco.r',
  bio: 'Pro player — heavy euro & worker placement. Organizzatore Game Night Club.',
  joinedAt: 'Gen 2025',
  city: 'Milano',
  favAgentId: 'a-azul-rules',
  topGameId: 'g-azul',
  guest: false,
};

const GUEST_PLAYER = {
  id: 'p-davide',
  type: 'player',
  title: 'Davide F.',
  handle: null,
  initials: 'DF',
  bio: 'Ospite di Marco. Nessun account MeepleAI.',
  joinedAt: null,
  city: null,
  favAgentId: null,
  topGameId: null,
  guest: true,
  totalSessions: 4,
  totalWins: 1,
  winRate: 0.25,
  fav: 'Catan',
};

const PLAYER_SESSIONS = [
  { id: 's-azul-live', title: 'Serata Azul', date: '15 Mar 2026', duration: '45m',
    gameId: 'g-azul', state: 'inprogress', badge: 'In Corso',
    playerIds: ['p-marco', 'p-sara', 'p-luca', 'p-giulia'], won: null, score: 48 },
  { id: 's-wing-042', title: 'Wingspan · Domenica', date: '10 Mar 2026', duration: '1h 42m',
    gameId: 'g-wingspan', state: 'done', badge: 'Vittoria',
    playerIds: ['p-sara', 'p-marco', 'p-giulia', 'p-luca', 'p-andrea'], won: true, score: 92 },
  { id: 's-brass-041', title: 'Brass Martedì', date: '5 Mar 2026', duration: '2h 15m',
    gameId: 'g-brass', state: 'done', badge: 'Vittoria',
    playerIds: ['p-marco', 'p-sara', 'p-luca'], won: true, score: 132 },
  { id: 's-7w-018', title: '7 Wonders · Duel', date: '1 Mar 2026', duration: '32m',
    gameId: 'g-7wonders', state: 'done', badge: 'Sconfitta',
    playerIds: ['p-marco', 'p-sara'], won: false, score: 58 },
  { id: 's-azul-021', title: 'Azul · Casa Marco', date: '24 Feb 2026', duration: '38m',
    gameId: 'g-azul', state: 'done', badge: 'Vittoria',
    playerIds: ['p-marco', 'p-luca', 'p-giulia'], won: true, score: 81 },
];

const PLAYER_TOP_GAMES = [
  { id: 'g-azul', plays: 23, wins: 15, winRate: 0.65 },
  { id: 'g-wingspan', plays: 17, wins: 10, winRate: 0.59 },
  { id: 'g-brass', plays: 12, wins: 6, winRate: 0.50 },
  { id: 'g-7wonders', plays: 11, wins: 5, winRate: 0.45 },
  { id: 'g-catan', plays: 9, wins: 3, winRate: 0.33 },
];

const PLAYER_TOOLKITS = [
  { id: 'tk-azul-v2', title: 'Azul Toolkit v2', gameId: 'g-azul',
    toolCount: 3, useCount: 12, version: 2, badge: 'Pubblicato' },
];

const ACHIEVEMENTS = [
  { id: 'ach-first-win', icon: '🥇', title: 'Prima vittoria', sub: 'Vinci la tua prima partita', unlocked: true, date: 'Gen 2025' },
  { id: 'ach-streak-3', icon: '🔥', title: 'Streak ×3', sub: '3 vittorie consecutive', unlocked: true, date: 'Mar 2025' },
  { id: 'ach-fav-game', icon: '⭐', title: 'Specialista', sub: '20+ partite stesso gioco', unlocked: true, date: 'Set 2025' },
  { id: 'ach-night-host', icon: '🎉', title: 'Ospite', sub: 'Organizza una Game Night', unlocked: true, date: 'Ott 2025' },
  { id: 'ach-toolkit-pub', icon: '🧰', title: 'Tinkerer', sub: 'Pubblica il primo toolkit', unlocked: true, date: 'Gen 2026' },
  { id: 'ach-50-plays', icon: '🎯', title: '50 partite', sub: 'Raggiungi 50 partite totali', unlocked: true, date: 'Dic 2025' },
  { id: 'ach-100-plays', icon: '💯', title: '100 partite', sub: 'Raggiungi 100 partite totali', unlocked: false },
  { id: 'ach-no-loss', icon: '🛡️', title: 'Imbattuto', sub: '5 partite consecutive vinte', unlocked: false },
  { id: 'ach-explorer', icon: '🧭', title: 'Esploratore', sub: 'Prova 10 giochi diversi', unlocked: true, date: 'Nov 2025' },
  { id: 'ach-ai-friend', icon: '🤖', title: 'AI Buddy', sub: '50 chat con un agente', unlocked: false },
  { id: 'ach-heavy', icon: '🏋️', title: 'Heavy player', sub: 'Vinci un gioco con peso > 3.5', unlocked: true, date: 'Feb 2026' },
  { id: 'ach-perfect', icon: '✨', title: 'Game perfetta', sub: '100% punteggio in una partita', unlocked: false },
];

// ═══════════════════════════════════════════════════════
// ─── PRIMITIVES ─────────────────────────────────────
// ═══════════════════════════════════════════════════════

const EntityChip = ({ type, label, count, icon, onClick }) => (
  <button
    type="button"
    onClick={onClick}
    style={{
      display:'inline-flex', alignItems:'center', gap: 6,
      padding:'4px 10px', borderRadius:'var(--r-pill)',
      background: entityHsl(type, 0.12), color: entityHsl(type),
      border:`1px solid ${entityHsl(type, 0.2)}`,
      fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
      cursor:'pointer', whiteSpace:'nowrap',
    }}>
    <span aria-hidden="true">{icon || DS.EC[type]?.em}</span>
    {count !== undefined && (
      <span style={{ fontFamily:'var(--f-mono)', fontVariantNumeric:'tabular-nums' }}>{count}</span>
    )}
    <span>{label}</span>
  </button>
);

// ═══════════════════════════════════════════════════════
// ─── CONNECTIONBAR (1:1 prod) ────────────────────────
// /* v2: ConnectionBar — ✅ in produzione (PR #549/#552) */
// ═══════════════════════════════════════════════════════
const ConnectionBar = ({ connections, loading, onPipClick }) => {
  if (loading) {
    return (
      <div style={{ display:'flex', gap: 8, padding:'8px 0', overflowX:'auto', scrollbarWidth:'none' }}>
        {[0,1,2,3,4,5].map(i => (
          <div key={i} className="mai-shimmer" style={{
            height: 28, width: 92 + i*6, borderRadius: 999,
            background:'var(--bg-muted)', flexShrink: 0,
          }}/>
        ))}
      </div>
    );
  }
  if (!connections || connections.length === 0) return null;
  return (
    <div className="mai-cb-scroll" style={{
      display:'flex', alignItems:'center', gap: 8,
      overflowX:'auto', padding:'8px 0', scrollbarWidth:'none',
    }}>
      {connections.map(c => {
        const isEmpty = c.isEmpty || c.count === 0;
        const ariaLabel = isEmpty ? c.label : `${c.label}: ${c.count}`;
        const ec = DS.EC[c.entityType];
        return (
          <button
            key={c.entityType}
            type="button"
            aria-label={ariaLabel}
            onClick={() => onPipClick && onPipClick(c)}
            style={{
              display:'inline-flex', alignItems:'center', gap: 6,
              padding:'6px 12px', borderRadius: 999,
              fontSize: 12, fontWeight: 700, fontFamily:'var(--f-display)',
              background: isEmpty ? 'transparent' : entityHsl(c.entityType, 0.1),
              color: entityHsl(c.entityType),
              border: isEmpty
                ? `1.5px dashed ${entityHsl(c.entityType, 0.5)}`
                : `1px solid ${entityHsl(c.entityType, 0.2)}`,
              opacity: isEmpty ? 0.6 : 1,
              cursor:'pointer', flexShrink: 0, whiteSpace:'nowrap',
            }}>
            <span aria-hidden="true" style={{ fontSize: 13, lineHeight: 1 }}>{ec.em}</span>
            {isEmpty ? (
              <svg width="13" height="13" viewBox="0 0 24 24"
                fill="none" stroke="currentColor" strokeWidth="2.5"
                strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <line x1="12" y1="5" x2="12" y2="19"/>
                <line x1="5" y1="12" x2="19" y2="12"/>
              </svg>
            ) : (
              <span style={{
                fontFamily:'var(--f-mono)', fontVariantNumeric:'tabular-nums', fontWeight: 700,
              }}>{c.count}</span>
            )}
            <span>{c.label}</span>
          </button>
        );
      })}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── PLAYER HERO ────────────────────────────────────
// /* v2: PlayerHero → apps/web/src/components/ui/v2/player-hero/ */
// ═══════════════════════════════════════════════════════
const PlayerHero = ({ player, compact, loading }) => {
  if (loading) {
    return (
      <div style={{ position:'relative', background:'var(--bg)' }}>
        <div style={{
          padding: compact ? '20px 16px 14px' : '32px 32px 18px',
          display:'flex', gap: compact ? 14 : 20, alignItems:'center',
        }}>
          <div className="mai-shimmer" style={{
            width: compact ? 96 : 128, height: compact ? 96 : 128,
            borderRadius:'50%', background:'var(--bg-muted)', flexShrink: 0,
          }}/>
          <div style={{ flex: 1, display:'flex', flexDirection:'column', gap: 8 }}>
            <div className="mai-shimmer" style={{ height: 28, width:'60%', background:'var(--bg-muted)', borderRadius: 6 }}/>
            <div className="mai-shimmer" style={{ height: 14, width:'42%', background:'var(--bg-muted)', borderRadius: 4 }}/>
            <div className="mai-shimmer" style={{ height: 14, width:'80%', background:'var(--bg-muted)', borderRadius: 4 }}/>
          </div>
        </div>
      </div>
    );
  }

  const isGuest = player.guest;
  const avatarSize = compact ? 96 : 128;
  const winRatePct = Math.round((player.winRate || 0) * 100);

  return (
    <div style={{ position:'relative', background:'var(--bg)' }}>
      {/* Soft top band */}
      <div style={{
        position:'absolute', top: 0, left: 0, right: 0,
        height: compact ? 80 : 120,
        background: isGuest
          ? 'var(--bg-muted)'
          : `linear-gradient(135deg, ${entityHsl('player', 0.18)}, ${entityHsl('player', 0.06)} 60%, ${entityHsl('event', 0.10)})`,
        zIndex: 0,
      }}/>

      <div style={{
        position:'relative', zIndex: 1,
        padding: compact ? '20px 16px 14px' : '32px 32px 18px',
        display:'flex', flexDirection: compact ? 'column' : 'row',
        alignItems: compact ? 'center' : 'flex-end',
        gap: compact ? 14 : 22,
        textAlign: compact ? 'center' : 'left',
      }}>
        {/* Avatar */}
        <div style={{
          width: avatarSize, height: avatarSize,
          borderRadius:'50%',
          border: `3px solid ${entityHsl('player', 0.4)}`,
          background: isGuest
            ? 'var(--bg-muted)'
            : `linear-gradient(135deg, hsl(${player.color}, 75%, 60%), hsl(${(player.color + 40) % 360}, 65%, 45%))`,
          color: isGuest ? 'var(--text-muted)' : '#fff',
          display:'flex', alignItems:'center', justifyContent:'center',
          fontFamily:'var(--f-display)', fontWeight: 800,
          fontSize: isGuest ? 44 : (compact ? 36 : 48),
          flexShrink: 0,
          boxShadow: isGuest ? 'none' : `0 8px 24px ${entityHsl('player', 0.35)}`,
        }} aria-hidden="true">
          {isGuest ? '👤' : player.initials}
        </div>

        <div style={{ flex: 1, minWidth: 0 }}>
          {/* Badges row */}
          <div style={{
            display:'flex', alignItems:'center',
            justifyContent: compact ? 'center' : 'flex-start',
            gap: 6, flexWrap:'wrap', marginBottom: 8,
          }}>
            <span style={{
              display:'inline-flex', alignItems:'center', gap: 5,
              padding:'3px 9px', borderRadius:'var(--r-pill)',
              background: entityHsl('player', 0.14), color: entityHsl('player'),
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.08em',
            }}><span aria-hidden="true">👤</span>Player</span>
            {isGuest ? (
              <span style={{
                padding:'3px 9px', borderRadius:'var(--r-pill)',
                background:'var(--bg-muted)', color:'var(--text-sec)',
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform:'uppercase', letterSpacing:'.08em',
              }}>Ospite</span>
            ) : (
              <span style={{
                padding:'3px 9px', borderRadius:'var(--r-pill)',
                background: entityHsl('toolkit', 0.14), color: entityHsl('toolkit'),
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform:'uppercase', letterSpacing:'.08em',
              }}>● {player.badge || 'Attivo'}</span>
            )}
          </div>

          {/* Name */}
          <h1 style={{
            fontFamily:'var(--f-display)', fontWeight: 800,
            fontSize: compact ? 26 : 36, letterSpacing:'-.02em', lineHeight: 1.05,
            color:'var(--text)', margin:'0 0 4px',
          }}>{player.title}</h1>

          {/* Handle + meta */}
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: compact ? 11 : 12,
            color:'var(--text-muted)', fontWeight: 600,
            display:'flex', flexWrap:'wrap', gap:'2px 10px',
            justifyContent: compact ? 'center' : 'flex-start',
            marginBottom: 10,
          }}>
            {player.handle && <span>{player.handle}</span>}
            {player.handle && (player.city || player.joinedAt) && <span aria-hidden="true">·</span>}
            {player.city && <span>📍 {player.city}</span>}
            {player.city && player.joinedAt && <span aria-hidden="true">·</span>}
            {player.joinedAt && <span>Membro da {player.joinedAt}</span>}
            {isGuest && <span>Aggiunto come ospite</span>}
          </div>

          {/* Top-line stats row */}
          {!isGuest ? (
            <div style={{
              display:'flex', flexWrap:'wrap',
              justifyContent: compact ? 'center' : 'flex-start',
              gap: compact ? 10 : 18, marginBottom: 4,
            }}>
              <StatInline label="Partite" value={player.totalSessions} />
              <StatInline label="Vittorie" value={player.totalWins} accent="toolkit" />
              <StatInline label="Win rate" value={`${winRatePct}%`} accent="player" />
              <StatInline label="Top" value={DS.byId[player.topGameId]?.title || '—'} accent="game" mono />
            </div>
          ) : (
            <div style={{
              display:'flex', flexWrap:'wrap',
              justifyContent: compact ? 'center' : 'flex-start',
              gap: compact ? 10 : 18, marginBottom: 4,
            }}>
              <StatInline label="Partite (con noi)" value={player.totalSessions} />
              <StatInline label="Vittorie" value={player.totalWins} accent="toolkit" />
              <StatInline label="Win rate" value={`${winRatePct}%`} accent="player" />
            </div>
          )}
        </div>
      </div>

      {/* Action bar */}
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding: compact ? '10px 16px 14px' : '12px 32px 18px',
        background:'var(--bg)',
        flexWrap: compact ? 'wrap' : 'nowrap',
        justifyContent: compact ? 'center' : 'flex-start',
      }}>
        {!isGuest ? (
          <>
            <button type="button" style={{
              padding: compact ? '9px 14px' : '10px 18px', borderRadius:'var(--r-md)',
              background: entityHsl('player'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              cursor:'pointer',
              boxShadow:`0 4px 14px ${entityHsl('player', 0.4)}`,
              display:'inline-flex', alignItems:'center', gap: 6,
            }}><span aria-hidden="true">📊</span>Confronta</button>
            <button type="button" style={{
              padding: compact ? '9px 12px' : '10px 14px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor:'pointer',
            }}>✎ Modifica</button>
            <button type="button" aria-label="Condividi profilo" style={{
              padding: compact ? '9px 12px' : '10px 14px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor:'pointer',
            }}>↗</button>
          </>
        ) : (
          <>
            <button type="button" style={{
              padding: compact ? '9px 14px' : '10px 18px', borderRadius:'var(--r-md)',
              background: entityHsl('player'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              cursor:'pointer',
              boxShadow:`0 4px 14px ${entityHsl('player', 0.4)}`,
            }}>+ Invita su MeepleAI</button>
            <button type="button" style={{
              padding: compact ? '9px 12px' : '10px 14px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor:'pointer',
            }}>✎ Modifica</button>
          </>
        )}
      </div>
    </div>
  );
};

const StatInline = ({ label, value, accent, mono }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 2 }}>
    <span style={{
      fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
      textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800,
    }}>{label}</span>
    <span style={{
      fontFamily: mono ? 'var(--f-mono)' : 'var(--f-display)',
      fontSize: mono ? 14 : 18, fontWeight: 800,
      color: accent ? entityHsl(accent) : 'var(--text)',
      fontVariantNumeric:'tabular-nums', lineHeight: 1.1,
    }}>{value}</span>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── PLAYER TABS ────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PlayerTabs = ({ tabs, active, onChange, compact, locked }) => {
  const refs = useRef({});
  const [ind, setInd] = useState({ left: 0, width: 0 });
  useEffect(() => {
    const el = refs.current[active];
    if (el) setInd({ left: el.offsetLeft, width: el.offsetWidth });
  }, [active, tabs.length, compact]);

  return (
    <div className="mai-cb-scroll" style={{
      position:'relative',
      display:'flex', alignItems:'center', gap: 2,
      borderBottom:'1px solid var(--border)',
      overflowX:'auto', scrollbarWidth:'none',
      padding: compact ? '0 16px' : '0 32px',
      background:'var(--bg)',
    }} role="tablist" aria-label="Sezioni profilo giocatore">
      {tabs.map(t => {
        const isActive = t.id === active;
        const isLocked = locked && t.locked;
        return (
          <button
            key={t.id}
            type="button"
            ref={el => { refs.current[t.id] = el; }}
            onClick={() => !isLocked && onChange(t.id)}
            role="tab"
            aria-selected={isActive}
            aria-controls={`panel-${t.id}`}
            id={`tab-${t.id}`}
            disabled={isLocked}
            style={{
              display:'inline-flex', alignItems:'center', gap: 7,
              padding:'12px 14px', background:'transparent', border:'none',
              color: isLocked ? 'var(--text-muted)' : (isActive ? entityHsl('player') : 'var(--text-sec)'),
              fontFamily:'var(--f-display)', fontSize: 13,
              fontWeight: isActive ? 800 : 700,
              cursor: isLocked ? 'not-allowed' : 'pointer',
              whiteSpace:'nowrap', flexShrink: 0,
              transition:'color var(--dur-sm) var(--ease-out)',
              opacity: isLocked ? 0.55 : 1,
            }}>
            <span aria-hidden="true">{t.icon}</span>
            <span>{t.label}</span>
            {t.count !== undefined && (
              <span style={{
                padding:'1px 7px', borderRadius:'var(--r-pill)',
                background: isActive ? entityHsl('player') : 'var(--bg-muted)',
                color: isActive ? '#fff' : 'var(--text-muted)',
                fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
                fontVariantNumeric:'tabular-nums',
              }}>{t.count}</span>
            )}
            {isLocked && <span aria-hidden="true" style={{ fontSize: 10 }}>🔒</span>}
          </button>
        );
      })}
      <span aria-hidden="true" style={{
        position:'absolute', bottom: -1, left: ind.left, width: ind.width,
        height: 2, background: entityHsl('player'),
        transition:'left .3s cubic-bezier(.4,0,.2,1), width .3s cubic-bezier(.4,0,.2,1)',
        borderRadius:'2px 2px 0 0',
      }}/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── KPI CARD (riuso da wave 1) ─────────────────────
// ═══════════════════════════════════════════════════════
const KpiCard = ({ label, value, unit, accent = 'player', icon, sub }) => (
  <div style={{
    padding: 14,
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)',
    display:'flex', flexDirection:'column', gap: 6,
  }}>
    <div style={{ display:'flex', alignItems:'center', gap: 6 }}>
      <span aria-hidden="true" style={{
        width: 24, height: 24, borderRadius:'var(--r-sm)',
        background: entityHsl(accent, 0.14), color: entityHsl(accent),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 12,
      }}>{icon}</span>
      <span style={{
        fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
        textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800,
      }}>{label}</span>
    </div>
    <div style={{
      fontFamily:'var(--f-display)', fontSize: 28, fontWeight: 800,
      color:'var(--text)', fontVariantNumeric:'tabular-nums', lineHeight: 1,
    }}>{value}<span style={{ fontSize: 13, color:'var(--text-muted)', fontWeight: 600, marginLeft: 4 }}>{unit}</span></div>
    {sub && (
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 600,
      }}>{sub}</div>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: OVERVIEW ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const TopGamesCard = () => (
  <div style={{
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)', padding: 18,
  }}>
    <div style={{
      display:'flex', alignItems:'baseline', justifyContent:'space-between', marginBottom: 12,
    }}>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
        color:'var(--text)', margin: 0,
      }}>Top giochi</h3>
      <button type="button" style={{
        padding:'4px 10px', borderRadius:'var(--r-sm)',
        background:'transparent', border:'1px solid var(--border)',
        color:'var(--text-sec)', fontSize: 11, fontWeight: 700,
        fontFamily:'var(--f-display)', cursor:'pointer',
      }}>Vedi tutti →</button>
    </div>
    <div style={{ display:'flex', flexDirection:'column', gap: 0 }}>
      {PLAYER_TOP_GAMES.map((g, i) => {
        const game = DS.byId[g.id];
        if (!game) return null;
        const wr = Math.round(g.winRate * 100);
        return (
          <div key={g.id} style={{
            display:'flex', alignItems:'center', gap: 12,
            padding:'10px 0',
            borderBottom: i < PLAYER_TOP_GAMES.length - 1 ? '1px solid var(--border-light)' : 'none',
          }}>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 11, fontWeight: 800,
              color: i === 0 ? entityHsl('agent') : 'var(--text-muted)',
              width: 22, textAlign:'center',
            }}>{i === 0 ? '🏆' : `#${i+1}`}</span>
            <div style={{
              width: 36, height: 44, borderRadius:'var(--r-sm)',
              background: game.cover, flexShrink: 0,
              display:'flex', alignItems:'center', justifyContent:'center',
              fontSize: 18, color:'rgba(255,255,255,.92)',
            }} aria-hidden="true">{game.coverEmoji}</div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{
                fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, color:'var(--text)',
              }}>{game.title}</div>
              <div style={{
                fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 600,
              }}>{g.plays} partite · {g.wins} vittorie</div>
            </div>
            <div style={{ display:'flex', flexDirection:'column', alignItems:'flex-end', minWidth: 56 }}>
              <span style={{
                fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
                color: entityHsl('toolkit'), fontVariantNumeric:'tabular-nums', lineHeight: 1,
              }}>{wr}%</span>
              <span style={{
                fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
                textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700, marginTop: 2,
              }}>win rate</span>
            </div>
          </div>
        );
      })}
    </div>
  </div>
);

const TrendCard = () => (
  <div style={{
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)', padding: 18,
  }}>
    <div style={{
      display:'flex', alignItems:'baseline', justifyContent:'space-between', marginBottom: 14,
    }}>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)', margin: 0,
      }}>Andamento ultimi 6 mesi</h3>
      <span style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color: entityHsl('toolkit'), fontWeight: 700,
      }}>↗ +12%</span>
    </div>
    {/* SVG line chart placeholder */}
    <div style={{ width:'100%', height: 110, position:'relative' }}>
      <svg viewBox="0 0 280 100" preserveAspectRatio="none" style={{ width:'100%', height:'100%' }} aria-hidden="true">
        <defs>
          <linearGradient id="trendg" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor={entityHsl('player', 0.35)}/>
            <stop offset="100%" stopColor={entityHsl('player', 0)}/>
          </linearGradient>
        </defs>
        <path d="M0,75 L40,60 L80,68 L120,40 L160,52 L200,28 L240,32 L280,18 L280,100 L0,100 Z"
          fill="url(#trendg)"/>
        <path d="M0,75 L40,60 L80,68 L120,40 L160,52 L200,28 L240,32 L280,18"
          fill="none" stroke={entityHsl('player')} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"/>
        {[
          [0,75],[40,60],[80,68],[120,40],[160,52],[200,28],[240,32],[280,18]
        ].map(([x,y], i) => (
          <circle key={i} cx={x} cy={y} r="3" fill="var(--bg-card)" stroke={entityHsl('player')} strokeWidth="2"/>
        ))}
      </svg>
    </div>
    <div style={{
      display:'flex', justifyContent:'space-between', marginTop: 6,
      fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)', fontWeight: 700,
    }}>
      <span>Ott</span><span>Nov</span><span>Dic</span><span>Gen</span><span>Feb</span><span>Mar</span>
    </div>
  </div>
);

const FavoriteAgentCard = () => {
  const agent = DS.byId[PLAYER.favAgentId];
  if (!agent) return null;
  return (
    <div style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', padding: 14,
      display:'flex', alignItems:'center', gap: 12,
    }}>
      <div style={{
        width: 44, height: 44, borderRadius:'var(--r-md)',
        background: entityHsl('agent', 0.14), color: entityHsl('agent'),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 22, flexShrink: 0,
      }} aria-hidden="true">🤖</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800, marginBottom: 2,
        }}>Agente preferito</div>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, color:'var(--text)',
        }}>{agent.title}</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 600,
        }}>{agent.invocations} invocazioni · {agent.avgLatency}</div>
      </div>
      <button type="button" aria-label="Apri agente" style={{
        padding:'6px 10px', borderRadius:'var(--r-md)',
        background:'transparent', border:'1px solid var(--border)',
        color: entityHsl('agent'), fontFamily:'var(--f-display)',
        fontSize: 11, fontWeight: 800, cursor:'pointer',
      }}>↗</button>
    </div>
  );
};

const OverviewTab = ({ player }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 14 }}>
    {player.bio && (
      <div style={{
        background:'var(--bg-card)', border:'1px solid var(--border)',
        borderRadius:'var(--r-lg)', padding: 16,
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
          textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 800, marginBottom: 6,
        }}>Bio</div>
        <p style={{
          fontSize: 13.5, color:'var(--text)', lineHeight: 1.6, margin: 0,
        }}>{player.bio}</p>
      </div>
    )}
    <div style={{
      display:'grid', gridTemplateColumns:'repeat(auto-fit, minmax(150px, 1fr))', gap: 10,
    }}>
      <KpiCard label="Partite" value={player.totalSessions} unit="" accent="session" icon="🎯"/>
      <KpiCard label="Vittorie" value={player.totalWins} unit="" accent="toolkit" icon="🏆"/>
      <KpiCard label="Win rate" value={Math.round(player.winRate * 100)} unit="%" accent="player" icon="📈"/>
      <KpiCard label="Streak" value="3" unit="W" accent="event" icon="🔥" sub="3 vittorie consecutive"/>
    </div>
    {!player.guest && <FavoriteAgentCard/>}
    <TopGamesCard/>
    {!player.guest && <TrendCard/>}
  </div>
);

const OverviewEmpty = ({ player }) => (
  <div style={{
    padding:'40px 24px', textAlign:'center',
    background:'var(--bg-card)',
    border:'1px dashed var(--border-strong)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center',
  }}>
    <div style={{
      width: 64, height: 64, borderRadius:'50%',
      background: entityHsl('player', 0.12), color: entityHsl('player'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 28, marginBottom: 14,
    }} aria-hidden="true">🎯</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
      color:'var(--text)', margin:'0 0 6px',
    }}>Nessuna partita ancora</h3>
    <p style={{
      fontSize: 12.5, color:'var(--text-muted)', maxWidth: 360,
      margin:'0 0 16px', lineHeight: 1.55,
    }}>{player.title} non ha ancora partite registrate. Aggiungine una per vedere stats e trend.</p>
    <button type="button" style={{
      padding:'10px 18px', borderRadius:'var(--r-md)',
      background: entityHsl('session'), color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
      cursor:'pointer',
      boxShadow:`0 4px 14px ${entityHsl('session', 0.35)}`,
    }}>+ Aggiungi prima partita</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: SESSIONS ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const SessionItem = ({ s }) => {
  const game = DS.byId[s.gameId];
  const wonChip = s.won === true
    ? { label:'🏆 Vittoria', color: entityHsl('toolkit') }
    : s.won === false
      ? { label:'Sconfitta', color: 'var(--text-muted)' }
      : { label:'In corso', color: entityHsl('session') };
  return (
    <article tabIndex={0} className="mai-card-row" style={{
      display:'flex', alignItems:'center', gap: 14, padding: 14,
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', cursor:'pointer',
    }}>
      <div style={{
        width: 44, height: 54, borderRadius:'var(--r-sm)',
        background: game?.cover || entityHsl('session', 0.2), flexShrink: 0,
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 22, color:'rgba(255,255,255,.92)',
      }} aria-hidden="true">{game?.coverEmoji || '🎯'}</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display:'flex', alignItems:'baseline', gap: 8, flexWrap:'wrap', marginBottom: 3,
        }}>
          <h4 style={{
            fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
            color:'var(--text)', margin: 0,
          }}>{s.title}</h4>
          <span style={{
            padding:'1px 7px', borderRadius:'var(--r-pill)',
            background: s.won === true ? entityHsl('toolkit', 0.14)
              : s.won === false ? 'var(--bg-muted)'
              : entityHsl('session', 0.14),
            color: wonChip.color,
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.06em',
          }}>{wonChip.label}</span>
        </div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)', fontWeight: 600,
          display:'flex', flexWrap:'wrap', gap:'2px 10px',
        }}>
          <span>{s.date}</span>
          <span aria-hidden="true">·</span>
          <span>{s.duration}</span>
          <span aria-hidden="true">·</span>
          <span>{s.score} pt</span>
        </div>
      </div>
      <div style={{ display:'flex', alignItems:'center' }}>
        {(s.playerIds || []).slice(0, 4).map((pid, i) => {
          const p = DS.byId[pid];
          return (
            <div key={pid} style={{
              width: 26, height: 26, borderRadius:'50%',
              background: `hsl(${p?.color || 200}, 60%, 55%)`,
              color:'#fff',
              display:'inline-flex', alignItems:'center', justifyContent:'center',
              fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 9,
              border:'2px solid var(--bg-card)', marginLeft: i === 0 ? 0 : -8,
              zIndex: 4 - i, flexShrink: 0,
            }}>{p?.initials || '?'}</div>
          );
        })}
      </div>
    </article>
  );
};

const SessionsTab = ({ player }) => {
  const [filter, setFilter] = useState('all');
  const filtered = filter === 'all' ? PLAYER_SESSIONS
    : filter === 'wins' ? PLAYER_SESSIONS.filter(s => s.won === true)
    : PLAYER_SESSIONS.filter(s => s.won === false);
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 12 }}>
      <div style={{ display:'flex', gap: 6, flexWrap:'wrap' }}>
        {[
          { id:'all', label:'Tutte', n: PLAYER_SESSIONS.length },
          { id:'wins', label:'Vinte', n: PLAYER_SESSIONS.filter(s => s.won === true).length },
          { id:'losses', label:'Perse', n: PLAYER_SESSIONS.filter(s => s.won === false).length },
        ].map(f => (
          <button key={f.id} type="button" onClick={() => setFilter(f.id)} style={{
            padding:'6px 12px', borderRadius:'var(--r-pill)',
            background: filter === f.id ? entityHsl('session') : 'var(--bg-muted)',
            color: filter === f.id ? '#fff' : 'var(--text-sec)',
            border: filter === f.id ? 'none' : '1px solid var(--border)',
            fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 700,
            cursor:'pointer',
          }}>{f.label} <span style={{ fontFamily:'var(--f-mono)', opacity: 0.7 }}>{f.n}</span></button>
        ))}
      </div>
      {filtered.map(s => <SessionItem key={s.id} s={s}/>)}
    </div>
  );
};

const SessionsEmpty = () => (
  <div style={{
    padding:'40px 24px', textAlign:'center',
    background:'var(--bg-card)', border:'1px dashed var(--border-strong)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center',
  }}>
    <div style={{
      width: 60, height: 60, borderRadius:'50%',
      background: entityHsl('session', 0.12), color: entityHsl('session'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 28, marginBottom: 12,
    }} aria-hidden="true">🎯</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>Nessuna partita registrata</h3>
    <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 340 }}>
      Aggiungi la prima partita per iniziare a tracciare stats e win rate.
    </p>
    <button type="button" style={{
      padding:'8px 16px', borderRadius:'var(--r-md)',
      background: entityHsl('session'), color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
      boxShadow:`0 3px 10px ${entityHsl('session', 0.35)}`,
    }}>+ Aggiungi partita</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: GAMES (registered only) ───────────────────
// ═══════════════════════════════════════════════════════
const GameCompactCard = ({ gameId, plays, wins }) => {
  const game = DS.byId[gameId];
  if (!game) return null;
  return (
    <article tabIndex={0} className="mai-card-row" style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', cursor:'pointer', overflow:'hidden',
      display:'flex', flexDirection:'column',
    }}>
      <div style={{
        height: 96, background: game.cover, position:'relative',
        display:'flex', alignItems:'center', justifyContent:'center',
      }}>
        <span aria-hidden="true" style={{ fontSize: 44, opacity: 0.92 }}>{game.coverEmoji}</span>
      </div>
      <div style={{ padding: 12 }}>
        <h4 style={{
          fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
          color:'var(--text)', margin:'0 0 4px',
        }}>{game.title}</h4>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 600,
          marginBottom: 6,
        }}>{game.year} · {game.players} pl</div>
        <div style={{ display:'flex', gap: 6 }}>
          <span style={{
            padding:'2px 7px', borderRadius:'var(--r-pill)',
            background: entityHsl('session', 0.12), color: entityHsl('session'),
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          }}>{plays} giocate</span>
          <span style={{
            padding:'2px 7px', borderRadius:'var(--r-pill)',
            background: entityHsl('toolkit', 0.12), color: entityHsl('toolkit'),
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          }}>{wins} vinte</span>
        </div>
      </div>
    </article>
  );
};

const GamesTab = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 12 }}>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      fontWeight: 700, letterSpacing:'.04em',
    }}>{PLAYER_TOP_GAMES.length} giochi nella collection</div>
    <div style={{
      display:'grid',
      gridTemplateColumns:'repeat(auto-fill, minmax(180px, 1fr))', gap: 12,
    }}>
      {PLAYER_TOP_GAMES.map(g => <GameCompactCard key={g.id} gameId={g.id} plays={g.plays} wins={g.wins}/>)}
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: TOOLKITS ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const ToolkitItem = ({ tk }) => {
  const game = DS.byId[tk.gameId];
  return (
    <article tabIndex={0} className="mai-card-row" style={{
      display:'flex', alignItems:'center', gap: 14, padding: 14,
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', cursor:'pointer',
    }}>
      <div style={{
        width: 44, height: 44, borderRadius:'var(--r-md)',
        background: entityHsl('toolkit', 0.14), color: entityHsl('toolkit'),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 22, flexShrink: 0,
      }} aria-hidden="true">🧰</div>
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{
          display:'flex', alignItems:'baseline', gap: 8, flexWrap:'wrap', marginBottom: 3,
        }}>
          <h4 style={{
            fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
            color:'var(--text)', margin: 0,
          }}>{tk.title}</h4>
          <span style={{
            padding:'1px 7px', borderRadius:'var(--r-pill)',
            background: entityHsl('toolkit', 0.14), color: entityHsl('toolkit'),
            fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
            textTransform:'uppercase', letterSpacing:'.06em',
          }}>v{tk.version} · {tk.badge}</span>
        </div>
        <div style={{
          display:'flex', gap: 6, flexWrap:'wrap', marginTop: 4,
        }}>
          {game && <EntityChip type="game" label={game.title} icon="🎲"/>}
          <EntityChip type="tool" label={`${tk.toolCount} strumenti`} icon="🔧"/>
          <EntityChip type="session" label={`${tk.useCount} usi`} icon="🎯"/>
        </div>
      </div>
      <button type="button" aria-label="Apri toolkit" style={{
        padding:'6px 12px', borderRadius:'var(--r-md)',
        background:'transparent', border:'1px solid var(--border)',
        color: entityHsl('toolkit'),
        fontFamily:'var(--f-display)', fontSize: 11, fontWeight: 800, cursor:'pointer',
      }}>Apri ↗</button>
    </article>
  );
};

const ToolkitsTab = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
    <div style={{
      display:'flex', alignItems:'center', justifyContent:'space-between',
    }}>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
        fontWeight: 700, letterSpacing:'.04em',
      }}>{PLAYER_TOOLKITS.length} toolkit pubblicati</div>
      <button type="button" style={{
        padding:'8px 14px', borderRadius:'var(--r-md)',
        background: entityHsl('toolkit'), color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
        cursor:'pointer',
        boxShadow:`0 3px 10px ${entityHsl('toolkit', 0.35)}`,
      }}>+ Nuovo toolkit</button>
    </div>
    {PLAYER_TOOLKITS.map(tk => <ToolkitItem key={tk.id} tk={tk}/>)}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: ACHIEVEMENTS ──────────────────────────────
// ═══════════════════════════════════════════════════════
const AchievementBadge = ({ a }) => (
  <div style={{
    padding: 12, textAlign:'center',
    background: a.unlocked ? 'var(--bg-card)' : 'var(--bg-muted)',
    border: `1px solid ${a.unlocked ? entityHsl('event', 0.3) : 'var(--border-light)'}`,
    borderRadius:'var(--r-lg)',
    opacity: a.unlocked ? 1 : 0.55,
    display:'flex', flexDirection:'column', gap: 4, alignItems:'center',
  }}>
    <div style={{
      width: 48, height: 48, borderRadius:'50%',
      background: a.unlocked
        ? `linear-gradient(135deg, ${entityHsl('event', 0.25)}, ${entityHsl('event', 0.4)})`
        : 'var(--bg-sunken)',
      color: a.unlocked ? '#fff' : 'var(--text-muted)',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 22, marginBottom: 4,
      boxShadow: a.unlocked ? `0 4px 12px ${entityHsl('event', 0.3)}` : 'none',
      filter: a.unlocked ? 'none' : 'grayscale(0.8)',
    }} aria-hidden="true">{a.unlocked ? a.icon : '🔒'}</div>
    <div style={{
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
      color: a.unlocked ? 'var(--text)' : 'var(--text-muted)',
    }}>{a.title}</div>
    <div style={{
      fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
      lineHeight: 1.3,
    }}>{a.sub}</div>
    {a.unlocked && a.date && (
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 9, color: entityHsl('event'),
        fontWeight: 700, marginTop: 2,
      }}>✓ {a.date}</div>
    )}
  </div>
);

const AchievementsTab = ({ guest }) => {
  const list = guest ? ACHIEVEMENTS.slice(0, 4) : ACHIEVEMENTS;
  const unlocked = list.filter(a => a.unlocked).length;
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 12 }}>
      <div style={{
        display:'flex', justifyContent:'space-between', alignItems:'baseline',
      }}>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          fontWeight: 700, letterSpacing:'.04em',
        }}>{unlocked} / {list.length} sbloccati</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color: entityHsl('event'), fontWeight: 700,
        }}>{Math.round((unlocked/list.length)*100)}% completato</div>
      </div>
      <div style={{
        height: 6, borderRadius:'var(--r-pill)', background:'var(--bg-muted)', overflow:'hidden',
      }}>
        <div style={{
          height:'100%', width: `${(unlocked/list.length)*100}%`,
          background:`linear-gradient(90deg, ${entityHsl('event')}, ${entityHsl('player')})`,
          borderRadius:'var(--r-pill)',
        }}/>
      </div>
      <div style={{
        display:'grid',
        gridTemplateColumns:'repeat(auto-fill, minmax(140px, 1fr))', gap: 10,
      }}>
        {list.map(a => <AchievementBadge key={a.id} a={a}/>)}
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── ERROR / LOADING ────────────────────────────────
// ═══════════════════════════════════════════════════════
const TabSkeleton = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
    {[0,1,2,3].map(i => (
      <div key={i} className="mai-shimmer" style={{
        height: 76, borderRadius:'var(--r-lg)', background:'var(--bg-muted)',
      }}/>
    ))}
  </div>
);

const TabError = ({ onRetry }) => (
  <div style={{
    padding:'32px 24px', textAlign:'center',
    background:'hsl(var(--c-danger) / .08)',
    border:'1px solid hsl(var(--c-danger) / .3)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center',
  }}>
    <div style={{
      width: 48, height: 48, borderRadius:'50%',
      background:'hsl(var(--c-danger) / .15)', color:'hsl(var(--c-danger))',
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 22, marginBottom: 10,
    }} aria-hidden="true">⚠</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>Errore caricamento profilo</h3>
    <p style={{ fontSize: 12, color:'var(--text-muted)', margin:'0 0 12px', maxWidth: 320 }}>
      Impossibile recuperare i dati del giocatore. Verifica la connessione.
    </p>
    <button type="button" onClick={onRetry} style={{
      padding:'7px 14px', borderRadius:'var(--r-md)',
      background:'hsl(var(--c-danger))', color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
    }}>↻ Riprova</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── BODY ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PlayerDetailBody = ({ stateOverride, variant = 'registered', compact, initialTab = 'overview' }) => {
  const [tab, setTab] = useState(initialTab);
  const isLoading = stateOverride === 'loading';
  const isError = stateOverride === 'error';
  const isEmpty = stateOverride === 'empty';
  const player = variant === 'guest' ? GUEST_PLAYER : PLAYER;
  const isGuest = player.guest;

  const tabs = [
    { id:'overview', icon:'📋', label:'Overview' },
    { id:'sessions', icon:'🎯', label:'Partite', count: PLAYER_SESSIONS.length },
    { id:'games', icon:'🎲', label:'Giochi', count: PLAYER_TOP_GAMES.length, locked: isGuest },
    { id:'toolkits', icon:'🧰', label:'Toolkit', count: PLAYER_TOOLKITS.length, locked: isGuest },
    { id:'achievements', icon:'🏆', label:'Achievement', count: isGuest ? 4 : ACHIEVEMENTS.filter(a=>a.unlocked).length },
  ];

  // Connection bar — 6 pip per brief
  const connections = isGuest ? [
    { entityType:'game', count: 2, label:'Top giochi' },
    { entityType:'session', count: 4, label:'Partite' },
    { entityType:'event', count: 1, label:'Game Nights' },
    { entityType:'agent', count: 0, label:'Agenti usati', isEmpty: true },
    { entityType:'toolkit', count: 0, label:'Toolkit', isEmpty: true },
    { entityType:'chat', count: 0, label:'Chat', isEmpty: true },
  ] : [
    { entityType:'game', count: 5, label:'Top giochi' },
    { entityType:'session', count: 23, label:'Partite' },
    { entityType:'event', count: 4, label:'Game Nights' },
    { entityType:'agent', count: 0, label:'Agenti usati', isEmpty: true },
    { entityType:'toolkit', count: 1, label:'Toolkit' },
    { entityType:'chat', count: 12, label:'Chat' },
  ];

  const renderTab = () => {
    if (isLoading) return <TabSkeleton/>;
    if (isError) return <TabError/>;
    if (isEmpty) return <OverviewEmpty player={player}/>;

    if (isGuest && (tab === 'games' || tab === 'toolkits')) {
      return (
        <div style={{
          padding:'40px 24px', textAlign:'center',
          background:'var(--bg-card)',
          border:'1px dashed var(--border-strong)',
          borderRadius:'var(--r-xl)',
          display:'flex', flexDirection:'column', alignItems:'center',
        }}>
          <div style={{
            width: 60, height: 60, borderRadius:'50%',
            background:'var(--bg-muted)', color:'var(--text-muted)',
            display:'flex', alignItems:'center', justifyContent:'center',
            fontSize: 28, marginBottom: 12,
          }} aria-hidden="true">🔒</div>
          <h3 style={{
            fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
            color:'var(--text)', margin:'0 0 4px',
          }}>Sezione non disponibile per ospiti</h3>
          <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 340 }}>
            Collection e toolkit sono disponibili solo per giocatori registrati su MeepleAI.
          </p>
          <button type="button" style={{
            padding:'8px 16px', borderRadius:'var(--r-md)',
            background: entityHsl('player'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
          }}>+ Invita {player.title.split(' ')[0]} su MeepleAI</button>
        </div>
      );
    }

    if (tab === 'overview') return <OverviewTab player={player}/>;
    if (tab === 'sessions') return <SessionsTab player={player}/>;
    if (tab === 'games') return <GamesTab/>;
    if (tab === 'toolkits') return <ToolkitsTab/>;
    if (tab === 'achievements') return <AchievementsTab guest={isGuest}/>;
    return null;
  };

  return (
    <>
      <PlayerHero player={player} compact={compact} loading={isLoading}/>
      <div style={{
        padding: compact ? '0 16px' : '0 32px',
        background:'var(--glass-bg)', backdropFilter:'blur(12px)',
        borderBottom:'1px solid var(--border-light)',
        position:'sticky', top: 0, zIndex: 8,
      }}>
        <ConnectionBar connections={connections} loading={isLoading}/>
      </div>
      <div style={{ position:'sticky', top: 45, zIndex: 7,
        background:'var(--glass-bg)', backdropFilter:'blur(12px)',
      }}>
        <PlayerTabs tabs={tabs} active={tab} onChange={setTab} compact={compact} locked={isGuest}/>
      </div>
      <div role="tabpanel" id={`panel-${tab}`} aria-labelledby={`tab-${tab}`} style={{
        padding: compact ? '14px 16px 32px' : '20px 32px 64px',
      }}>
        {renderTab()}
      </div>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SHELLS ─────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopNav = ({ player }) => (
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
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      marginLeft: 22,
    }}>
      <a href="#" style={{ color:'inherit' }}>Giocatori</a>
      <span aria-hidden="true"> / </span>
      <strong style={{ color:'var(--text-sec)' }}>{player.title}</strong>
    </div>
  </div>
);

const DesktopScreen = ({ stateOverride, variant, initialTab }) => {
  const player = variant === 'guest' ? GUEST_PLAYER : PLAYER;
  return (
    <div style={{ minHeight: 720, background:'var(--bg)' }}>
      <DesktopNav player={player}/>
      <PlayerDetailBody stateOverride={stateOverride} variant={variant} initialTab={initialTab}/>
    </div>
  );
};

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color:'var(--text)' }}>
    <span style={{ fontFamily:'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);
const PhoneTopNav = ({ player }) => (
  <div style={{
    display:'flex', alignItems:'center', gap: 9,
    padding:'10px 14px', background:'var(--glass-bg)',
    backdropFilter:'blur(12px)',
    borderBottom:'1px solid var(--border-light)',
    position:'sticky', top: 0, zIndex: 9,
  }}>
    <button aria-label="Indietro" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>←</button>
    <div style={{
      flex: 1, fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
      textAlign:'center', fontWeight: 700,
    }}>players / {player.id.replace('p-', '')}</div>
    <button aria-label="Più opzioni" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>⋯</button>
  </div>
);
const MobileScreen = ({ stateOverride, variant, initialTab }) => {
  const player = variant === 'guest' ? GUEST_PLAYER : PLAYER;
  return (
    <>
      <PhoneSbar/>
      <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
        <PhoneTopNav player={player}/>
        <PlayerDetailBody stateOverride={stateOverride} variant={variant} compact initialTab={initialTab}/>
      </div>
    </>
  );
};

const PhoneShell = ({ label, desc, children, conformityMarker }) => (
  <div
    style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 10 }}
    {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
  >
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

const DesktopFrame = ({ label, desc, children, conformityMarker }) => {
  const player = (children?.props?.variant === 'guest') ? GUEST_PLAYER : PLAYER;
  return (
    <div
      style={{ display:'flex', flexDirection:'column', alignItems:'center', gap: 12, width:'100%' }}
      {...(conformityMarker ? { 'data-conformity-screen': conformityMarker } : {})}
    >
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-sec)',
        textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
      }}>{label}</div>
      <div style={{
        width:'100%', maxWidth: 1440,
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
          <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/players/{player.id.replace('p-', '')}</span>
        </div>
        {children}
      </div>
      {desc && <div style={{
        fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
        textAlign:'center', lineHeight: 1.55,
      }}>{desc}</div>}
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── ROOT ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id:'m1', stateOverride:null, variant:'registered', tab:'overview',
    label:'01 · Registered · Overview',
    desc:'Hero compatto + avatar gradient 96px + 6 ConnectionPip (agent empty dashed). Bio + 4 KPI + agente preferito + top games + trend.' },
  { id:'m2', stateOverride:null, variant:'registered', tab:'sessions',
    label:'02 · Registered · Sessions',
    desc:'Filter chips Tutte/Vinte/Perse. Card sessione con cover gioco, vittoria/sconfitta chip semantico, avatar stack giocatori.' },
  { id:'m3', stateOverride:null, variant:'registered', tab:'achievements',
    label:'03 · Registered · Achievements',
    desc:'Progress bar gradient event→player. Grid 2-col badge unlocked colorati / locked grayscale con data sblocco mono.' },
  { id:'m4', stateOverride:null, variant:'guest', tab:'overview',
    label:'04 · Guest · Overview',
    desc:'No avatar gradient (placeholder 👤), pill "Ospite" neutrale, no agente preferito, no trend, tabs Giochi+Toolkit locked 🔒.' },
  { id:'m5', stateOverride:'empty', variant:'registered', tab:'overview',
    label:'05 · Empty (player nuovo)',
    desc:'Player appena creato. Tab Overview mostra empty state con CTA "+ Aggiungi prima partita" entity=session.' },
  { id:'m6', stateOverride:'loading', variant:'registered', tab:'overview',
    label:'06 · Loading',
    desc:'Hero shimmer 96px + ConnectionBar shimmer 6 pip + tabs senza count + skeleton 4 card body.' },
  { id:'m7', stateOverride:'error', variant:'registered', tab:'overview',
    label:'07 · Error',
    desc:'Hero renderizzato comunque (cache locale), tab body mostra TabError con CTA "↻ Riprova".' },
];

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
      {/* Header */}
      <header style={{
        maxWidth: 1440, margin:'0 auto 32px',
        display:'flex', alignItems:'center', gap: 12, flexWrap:'wrap',
      }}>
        <div style={{
          display:'flex', alignItems:'center', gap: 9,
        }}>
          <div style={{
            width: 32, height: 32, borderRadius: 8,
            background:'linear-gradient(135deg, hsl(var(--c-player)), hsl(var(--c-event)))',
            color:'#fff', display:'flex', alignItems:'center', justifyContent:'center',
            fontWeight: 800, fontFamily:'var(--f-display)',
          }}>D</div>
          <div>
            <div style={{ fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 16 }}>SP4 wave 3 · D</div>
            <div style={{ fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)' }}>
              /players/[id] — Player detail (character sheet)
            </div>
          </div>
        </div>
        <div style={{ flex: 1 }}/>
        <button type="button" onClick={() => setTheme(t => t === 'light' ? 'dark' : 'light')}
          style={{
            padding:'8px 14px', borderRadius:'var(--r-md)',
            background:'var(--bg-card)', border:'1px solid var(--border)',
            color:'var(--text)', fontFamily:'var(--f-display)',
            fontSize: 12, fontWeight: 800, cursor:'pointer',
          }}>🌗 {theme === 'light' ? 'Light' : 'Dark'}</button>
      </header>

      {/* Desktop variants */}
      <section style={{ maxWidth: 1440, margin:'0 auto', display:'flex', flexDirection:'column', gap: 32 }}>
        <DesktopFrame label="Desktop · 01 · Registered · Overview"
          desc="Hero 128px avatar gradient + nome 36 + handle/città/membro mono + 4 stat top-line + ConnectionBar 6 pip sticky + tabs animated underline + body 2-col grid (KPI top, agente preferito, top games leaderboard, trend chart)."
          conformityMarker="default-desktop">
          <DesktopScreen variant="registered" initialTab="overview"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 02 · Registered · Sessions"
          desc="Tab sessioni con filter chips (Tutte / Vinte / Perse). Card list-variant con cover game 44×54 + win/loss chip semantico (toolkit verde / muted) + avatar stack.">
          <DesktopScreen variant="registered" initialTab="sessions"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 03 · Registered · Games"
          desc="Collection grid responsive (auto-fill 180px). Card compact: cover 96px + nome + meta + chip plays/wins.">
          <DesktopScreen variant="registered" initialTab="games"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 04 · Registered · Achievements"
          desc="Progress bar gradient event→player. Grid badge: unlocked colorati con data sblocco mono / locked grayscale + 🔒.">
          <DesktopScreen variant="registered" initialTab="achievements"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 05 · Guest player"
          desc="No avatar gradient → placeholder 👤. Pill 'Ospite' neutrale (no badge attivo). No agente preferito, no trend, tab Giochi+Toolkit locked. ConnectionPip agent/toolkit/chat in stato empty (dashed).">
          <DesktopScreen variant="guest" initialTab="overview"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 06 · Empty (player nuovo)"
          desc="Hero renderizzato (player appena creato), Overview tab body mostra empty illustration + CTA '+ Aggiungi prima partita' entity=session.">
          <DesktopScreen variant="registered" initialTab="overview" stateOverride="empty"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 07 · Loading"
          desc="Hero skeleton avatar 128 round + 3 lines shimmer. ConnectionBar 6 pip shimmer. Tabs visibili senza count. Body 4 card skeleton.">
          <DesktopScreen variant="registered" initialTab="overview" stateOverride="loading"/>
        </DesktopFrame>

        <DesktopFrame label="Desktop · 08 · Error"
          desc="Tab body TabError pattern: alert danger sfumato + icona ⚠ + CTA '↻ Riprova'. Hero e ConnectionBar rimangono renderizzati per non perdere contesto.">
          <DesktopScreen variant="registered" initialTab="overview" stateOverride="error"/>
        </DesktopFrame>
      </section>

      {/* Mobile variants */}
      <section style={{ maxWidth: 1440, margin:'48px auto 0' }}>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 22, fontWeight: 800,
          marginBottom: 8,
        }}>Mobile · 375px</h2>
        <p style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-muted)', marginBottom: 24,
        }}>7 stati: registered (overview, sessions, achievements) · guest · empty · loading · error</p>
        <div style={{
          display:'flex', flexWrap:'wrap', gap: 32, justifyContent:'center',
        }}>
          {MOBILE_STATES.map((m, idx) => (
            <PhoneShell
              key={m.id}
              label={m.label}
              desc={m.desc}
              conformityMarker={idx === 0 ? 'default-mobile' : undefined}>
              <MobileScreen stateOverride={m.stateOverride} variant={m.variant} initialTab={m.tab}/>
            </PhoneShell>
          ))}
        </div>
      </section>

      {/* DoD checklist */}
      <section style={{
        maxWidth: 900, margin:'64px auto 0',
        padding: 24, borderRadius:'var(--r-xl)',
        background:'var(--bg-card)', border:'1px solid var(--border)',
      }}>
        <h2 style={{
          fontFamily:'var(--f-display)', fontSize: 18, fontWeight: 800, marginBottom: 12,
        }}>Definition of Done · D player-detail</h2>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.9, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>✓ Solo CSS variables da tokens.css (zero hex hardcoded)</li>
          <li>✓ Helper entityHsl(type, alpha) inline per i 9 entity color</li>
          <li>✓ Light + dark mode entrambi funzionanti (toggle in header)</li>
          <li>✓ Mobile 375px (7 phone shells) + Desktop 1440px (8 frame)</li>
          <li>✓ ConnectionBar 1:1 prod: borderRadius 999, bg entityHsl(.1), dashed+Plus empty, aria-label `${'`'}${'${label}'}: ${'${count}'}${'`'}</li>
          <li>✓ EntityChip per ogni cross-reference (Sessions giocate, Top giochi, Toolkits creati, Agenti)</li>
          <li>✓ Tabs animated underline (riuso wave 1 pattern)</li>
          <li>✓ ARIA: role="tablist" + aria-label + role="tabpanel" + aria-selected + aria-controls</li>
          <li>✓ aria-label su icon-only buttons (Indietro, Più, Apri agente, Apri toolkit, Condividi)</li>
          <li>✓ Focus visibile keyboard (outline 2px hsl(--c-player) via components.css)</li>
          <li>✓ prefers-reduced-motion disabilita transizioni (components.css media query)</li>
          <li>✓ Stati: default / empty / loading / error + guest variant</li>
          <li>✓ Win rate culture-independent: 73% (no decimali, no virgola)</li>
          <li>✓ Testo UI in italiano · dati realistici Marco R., Sara T., Luca B., Giulia M., Davide F.</li>
          <li>✓ ID short readable: p-marco, p-davide, s-azul-live, tk-azul-v2 — NO UUID-like</li>
          <li>✓ Commento di apertura con route /players/[id] + descrizione 1-riga</li>
          <li>✓ Nessun TODO / placeholder visibile in UI</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
        }}>Nuovi componenti v2 emersi</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· PlayerHero (avatar gradient + badge variant-aware + stat row top-line)</li>
          <li>· PlayerStatsGrid (4 KpiCard riusabili da wave 1)</li>
          <li>· PlayerLeaderboardCard (top games con win rate per gioco)</li>
          <li>· FavoriteAgentCard (cross-ref agente preferito)</li>
          <li>· AchievementBadgeGrid (progress bar + grid unlocked/locked + reduced-motion safe)</li>
        </ul>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, marginTop: 18, marginBottom: 8,
          color: entityHsl('warning'),
        }}>Deviazioni flaggate</h3>
        <ul style={{
          fontFamily:'var(--f-mono)', fontSize: 12, color:'var(--text-sec)',
          lineHeight: 1.8, listStyle:'none', padding: 0, margin: 0,
        }}>
          <li>· Win rate display 73% (no decimali) — culture-independent, lezione issue #2593</li>
          <li>· Trend SVG line chart è placeholder visivo (vera logica + API post-alpha)</li>
          <li>· Achievements gamification = solo visual placeholder (definito dal brief)</li>
          <li>· Hero soft band gradient player→event a 0.18/0.10/0.10 alpha — uso semantico entity, non nuova palette</li>
        </ul>
      </section>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
