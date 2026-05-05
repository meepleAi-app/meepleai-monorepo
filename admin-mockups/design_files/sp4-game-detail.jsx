/* MeepleAI SP4 wave 1 — Schermata #2 / 5
   Route: /games/[id]
   File: admin-mockups/design_files/sp4-game-detail.{html,jsx}
   Pattern desktop: Hero + body con tabs (5: Info / Sessions / Chat / Stats / Documents)

   Hero 240px (180 mobile) cover gradient + overlay 0→60%, titolo Quicksand 4xl,
   meta riga, azioni a destra variant-aware.
   ConnectionBar 1:1 prod (PR #549/#552) sticky sotto hero.
   Tabs animated underline (riuso v2 da SP3).
   Varianti: own (CTA Gioca ora) · community (CTA Aggiungi a libreria, tabs locked).

   v2 nuovi:
   - GameHero            → apps/web/src/components/ui/v2/game-hero/
   - GameTabs            → apps/web/src/components/ui/v2/game-tabs/  (animated underline)
   - GameSpecsCard       → apps/web/src/components/ui/v2/game-specs-card/
   - GameStatsKpi        → apps/web/src/components/ui/v2/game-stats-kpi/
   - GameLeaderboard     → apps/web/src/components/ui/v2/game-leaderboard/
   - HouseRulesList      → apps/web/src/components/ui/v2/house-rules-list/

   Riusi pixel-perfect:
   - ConnectionBar (PR #549/#552)
   - MeepleCard variants list (session, kb)
   - SettingsRow pattern (custom rules)
*/

const { useState, useEffect, useMemo, useRef } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.game;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

const GAME = DS.games.find(g => g.id === 'g-wingspan');
const RELATED_AGENTS = DS.agents.filter(a => a.gameId === GAME.id);
const RELATED_KBS = DS.kbs.filter(k => k.gameId === GAME.id);
const RELATED_SESSIONS = DS.sessions.filter(s => s.gameId === GAME.id);
const RELATED_CHATS = DS.chats.filter(c => c.gameId === GAME.id);

// Mock add'l sessions for richer tab
const MORE_SESSIONS = [
  { id:'s-w-101', title:'Wingspan · Sabato', subtitle:'Conclusa · 4 giocatori', date:'12 Mar 2026',
    duration:'1h 28m', winner:'Marco R.', score: 92, playerIds:['p-marco','p-sara','p-luca','p-giulia'] },
  { id:'s-w-102', title:'Wingspan · Asia mode', subtitle:'Conclusa · 3 giocatori', date:'5 Mar 2026',
    duration:'1h 12m', winner:'Sara T.', score: 89, playerIds:['p-sara','p-marco','p-andrea'] },
  { id:'s-w-103', title:'Wingspan · Solo', subtitle:'Conclusa · solitario', date:'2 Mar 2026',
    duration:'42m', winner:'Marco R.', score: 78, playerIds:['p-marco'] },
  { id:'s-w-042', title:'Wingspan · Domenica', subtitle:'Conclusa · 5 giocatori', date:'24 Feb 2026',
    duration:'1h 42m', winner:'Sara T.', score: 95, playerIds:['p-sara','p-marco','p-giulia','p-luca','p-andrea'] },
];

const HOUSE_RULES = [
  { id:'hr1', label:'Bonus carte solo a fine partita', enabled: true },
  { id:'hr2', label:'Penalità -3 punti se cibo avanza', enabled: false },
  { id:'hr3', label:'Mani di 8 invece di 5', enabled: true },
];

const LEADERBOARD = [
  { ...DS.players.find(p => p.id === 'p-sara'), wins: 8, plays: 14, avgScore: 87 },
  { ...DS.players.find(p => p.id === 'p-marco'), wins: 5, plays: 12, avgScore: 81 },
  { ...DS.players.find(p => p.id === 'p-luca'), wins: 2, plays: 9, avgScore: 72 },
  { ...DS.players.find(p => p.id === 'p-giulia'), wins: 1, plays: 6, avgScore: 65 },
];

const MOCK_CHAT = [
  { role:'user', text:'Posso usare l\'azione "guadagna cibo" sull\'habitat foresta anche quando il bosco è pieno?' },
  { role:'agent', text:'Sì. L\'azione "guadagna cibo" si attiva sempre quando un uccello con quel potere viene attivato. Se il bosco è pieno, il bonus si applica solo per gli uccelli già piazzati.', cite:'wingspan-rules.pdf · pag 14' },
  { role:'user', text:'E se il dado pesca un cibo non disponibile?' },
];

// ─── STARS ──────────────────────────────────────────
const Stars = ({ value = 0, size = 11 }) => {
  const full = Math.floor(value);
  const half = value - full >= 0.5;
  return (
    <span style={{ display:'inline-flex', gap: 1, color:'hsl(var(--c-agent))', fontSize: size }}>
      {[0,1,2,3,4].map(i => (
        <span key={i} aria-hidden="true">{i < full ? '★' : (i === full && half ? '⯨' : '☆')}</span>
      ))}
    </span>
  );
};

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
              transition:'transform var(--dur-sm) var(--ease-out), box-shadow var(--dur-sm)',
            }}
            onMouseEnter={e => {
              e.currentTarget.style.transform = 'scale(1.03)';
              e.currentTarget.style.boxShadow = 'var(--shadow-md)';
            }}
            onMouseLeave={e => {
              e.currentTarget.style.transform = '';
              e.currentTarget.style.boxShadow = '';
            }}
          >
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
// ─── GAME HERO ──────────────────────────────────────
// /* v2: GameHero → apps/web/src/components/ui/v2/game-hero/ */
// ═══════════════════════════════════════════════════════
const GameHero = ({ game, variant, compact, loading }) => {
  if (loading) {
    return (
      <div style={{ position:'relative' }}>
        <div className="mai-shimmer" style={{ height: compact ? 180 : 240, background:'var(--bg-muted)' }}/>
        <div style={{ padding: compact ? '14px 16px' : '20px 32px', display:'flex', flexDirection:'column', gap: 8 }}>
          <div className="mai-shimmer" style={{ height: 28, width:'52%', background:'var(--bg-muted)', borderRadius: 6 }}/>
          <div className="mai-shimmer" style={{ height: 14, width:'72%', background:'var(--bg-muted)', borderRadius: 4 }}/>
        </div>
      </div>
    );
  }
  return (
    <div style={{ position:'relative', background:'var(--bg)' }}>
      {/* Cover */}
      <div style={{
        height: compact ? 180 : 240,
        background: game.cover,
        position:'relative', overflow:'hidden',
        display:'flex', alignItems:'center', justifyContent:'center',
      }}>
        <span aria-hidden="true" style={{
          fontSize: compact ? 90 : 130,
          filter:'drop-shadow(0 6px 18px rgba(0,0,0,.4))', opacity: .92,
        }}>{game.coverEmoji}</span>
        {/* gradient overlay 0→60% */}
        <div style={{
          position:'absolute', inset: 0,
          background:'linear-gradient(180deg, transparent 0%, rgba(0,0,0,.6) 100%)',
        }}/>
        {/* Title overlay */}
        <div style={{
          position:'absolute', left: 0, right: 0, bottom: 0,
          padding: compact ? '14px 16px' : '20px 32px',
          color:'#fff',
        }}>
          <div style={{
            display:'flex', alignItems:'center', gap: 6, marginBottom: 8,
          }}>
            <span style={{
              display:'inline-flex', alignItems:'center', gap: 5,
              padding:'3px 9px', borderRadius:'var(--r-pill)',
              background:'rgba(255,255,255,.18)', backdropFilter:'blur(8px)',
              color:'#fff',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.08em',
            }}><span aria-hidden="true">🎲</span>Game</span>
            <span style={{
              padding:'3px 9px', borderRadius:'var(--r-pill)',
              background:'rgba(255,255,255,.18)', backdropFilter:'blur(8px)',
              fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform:'uppercase', letterSpacing:'.08em',
            }}>{variant === 'own' ? '✓ In libreria' : '🌐 Community'}</span>
          </div>
          <h1 style={{
            fontFamily:'var(--f-display)', fontWeight: 800,
            fontSize: compact ? 28 : 40, letterSpacing:'-.02em', lineHeight: 1.05,
            color:'#fff', margin:'0 0 6px',
            textShadow:'0 2px 12px rgba(0,0,0,.3)',
          }}>{game.title}</h1>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: compact ? 11 : 12,
            color:'rgba(255,255,255,.92)', fontWeight: 600,
            display:'flex', flexWrap:'wrap', gap:'4px 10px',
          }}>
            <span>{game.author}</span><span aria-hidden="true">·</span>
            <span>{game.year}</span><span aria-hidden="true">·</span>
            <span>{game.players} pl</span><span aria-hidden="true">·</span>
            <span>{game.duration}</span><span aria-hidden="true">·</span>
            <span>complexity {game.weight?.toFixed(1)}/5</span>
            <span aria-hidden="true">·</span>
            <span style={{ display:'inline-flex', gap: 4, alignItems:'center' }}>
              <Stars value={game.stars} size={11}/> {game.rating}
            </span>
          </div>
        </div>
      </div>
      {/* Action bar */}
      <div style={{
        display:'flex', alignItems:'center', gap: 8,
        padding: compact ? '12px 16px' : '14px 32px',
        background:'var(--bg)',
        borderBottom:'1px solid var(--border-light)',
        flexWrap: compact ? 'wrap' : 'nowrap',
      }}>
        {variant === 'own' ? (
          <>
            <button type="button" style={{
              padding: compact ? '9px 14px' : '10px 18px', borderRadius:'var(--r-md)',
              background: entityHsl('session'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              cursor:'pointer',
              boxShadow:`0 4px 14px ${entityHsl('session', 0.4)}`,
              display:'inline-flex', alignItems:'center', gap: 6,
            }}><span aria-hidden="true">🎯</span>Gioca ora</button>
            <button type="button" style={{
              padding: compact ? '9px 12px' : '10px 14px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor:'pointer',
            }}>✎ Modifica</button>
            <button type="button" aria-label="Condividi" style={{
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
              background: entityHsl('game'), color:'#fff', border:'none',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
              cursor:'pointer',
              boxShadow:`0 4px 14px ${entityHsl('game', 0.4)}`,
              display:'inline-flex', alignItems:'center', gap: 6,
            }}>+ Aggiungi a libreria</button>
            <button type="button" style={{
              padding: compact ? '9px 12px' : '10px 14px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor:'pointer',
            }}>👀 Vedi simili</button>
            <button type="button" aria-label="Condividi" style={{
              padding: compact ? '9px 12px' : '10px 14px', borderRadius:'var(--r-md)',
              background:'transparent', color:'var(--text)',
              border:'1px solid var(--border-strong)',
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 700,
              cursor:'pointer',
            }}>↗</button>
          </>
        )}
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── GAME TABS (animated underline v2) ──────────────
// /* v2: GameTabs → apps/web/src/components/ui/v2/game-tabs/ */
// ═══════════════════════════════════════════════════════
const GameTabs = ({ tabs, active, onChange, compact, locked }) => {
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
    }} role="tablist">
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
            disabled={isLocked}
            style={{
              display:'inline-flex', alignItems:'center', gap: 7,
              padding:'12px 14px', background:'transparent', border:'none',
              color: isLocked ? 'var(--text-muted)' : (isActive ? entityHsl('game') : 'var(--text-sec)'),
              fontFamily:'var(--f-display)', fontSize: 13,
              fontWeight: isActive ? 800 : 700,
              cursor: isLocked ? 'not-allowed' : 'pointer',
              whiteSpace:'nowrap', flexShrink: 0,
              transition:'color var(--dur-sm) var(--ease-out)',
              opacity: isLocked ? 0.55 : 1,
            }}
          >
            <span aria-hidden="true">{t.icon}</span>
            <span>{t.label}</span>
            {t.count !== undefined && (
              <span style={{
                padding:'1px 7px', borderRadius:'var(--r-pill)',
                background: isActive ? entityHsl('game') : 'var(--bg-muted)',
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
        height: 2, background: entityHsl('game'),
        transition:'left .3s cubic-bezier(.4,0,.2,1), width .3s cubic-bezier(.4,0,.2,1)',
        borderRadius:'2px 2px 0 0',
      }}/>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── TAB: INFO ──────────────────────────────────────
// ═══════════════════════════════════════════════════════
// /* v2: GameSpecsCard */
const SpecsCard = ({ game }) => {
  const specs = [
    { label:'Giocatori', value: game.players },
    { label:'Durata', value: game.duration },
    { label:'Età', value: '14+' },
    { label:'Complessità', value: `${game.weight.toFixed(1)} / 5` },
    { label:'Anno', value: game.year },
    { label:'Designer', value: game.author },
    { label:'Editore', value: game.publisher },
    { label:'Rating BGG', value: game.rating },
  ];
  return (
    <div style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', padding: 18,
    }}>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
        color:'var(--text)', margin:'0 0 14px',
      }}>Specifiche</h3>
      <div style={{
        display:'grid', gridTemplateColumns:'repeat(auto-fill, minmax(140px, 1fr))', gap: 12,
      }}>
        {specs.map(s => (
          <div key={s.label}>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
              textTransform:'uppercase', letterSpacing:'.08em', fontWeight: 700,
              marginBottom: 3,
            }}>{s.label}</div>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
              color:'var(--text)',
            }}>{s.value}</div>
          </div>
        ))}
      </div>
    </div>
  );
};

// /* v2: HouseRulesList → apps/web/src/components/ui/v2/house-rules-list/ */
const HouseRulesCard = () => {
  const [rules, setRules] = useState(HOUSE_RULES);
  return (
    <div style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', padding: 18,
    }}>
      <div style={{
        display:'flex', alignItems:'baseline', justifyContent:'space-between', marginBottom: 12,
      }}>
        <h3 style={{
          fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800, color:'var(--text)', margin: 0,
        }}>House rules</h3>
        <button type="button" style={{
          padding:'4px 10px', borderRadius:'var(--r-sm)',
          background:'transparent', border:'1px solid var(--border)',
          color:'var(--text-sec)', fontSize: 11, fontWeight: 700,
          fontFamily:'var(--f-display)', cursor:'pointer',
        }}>+ Aggiungi</button>
      </div>
      <div style={{ display:'flex', flexDirection:'column', gap: 0 }}>
        {rules.map((r, i) => (
          <label key={r.id} style={{
            display:'flex', alignItems:'center', gap: 12,
            padding:'10px 0',
            borderBottom: i < rules.length - 1 ? '1px solid var(--border-light)' : 'none',
            cursor:'pointer',
          }}>
            <span style={{
              width: 36, height: 20, borderRadius: 999,
              background: r.enabled ? entityHsl('game') : 'var(--bg-muted)',
              position:'relative', transition:'background var(--dur-sm)',
              flexShrink: 0,
            }}>
              <span style={{
                position:'absolute', top: 2, left: r.enabled ? 18 : 2,
                width: 16, height: 16, borderRadius: '50%',
                background:'#fff', boxShadow:'0 1px 3px rgba(0,0,0,.2)',
                transition:'left var(--dur-sm) var(--ease-spring)',
              }}/>
            </span>
            <span style={{
              flex: 1, fontSize: 13, color: r.enabled ? 'var(--text)' : 'var(--text-sec)',
              fontWeight: 600,
            }}>{r.label}</span>
            <input type="checkbox" checked={r.enabled} style={{ display:'none' }}
              onChange={() => setRules(rs => rs.map(x => x.id === r.id ? { ...x, enabled: !x.enabled } : x))}/>
          </label>
        ))}
      </div>
    </div>
  );
};

const InfoTab = ({ game }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 14 }}>
    <div style={{
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)', padding: 18,
    }}>
      <h3 style={{
        fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
        color:'var(--text)', margin:'0 0 10px',
      }}>Descrizione</h3>
      <p style={{
        fontSize: 13.5, color:'var(--text-sec)', lineHeight: 1.65, margin: 0,
      }}>
        In <strong>{game.title}</strong> sei un appassionato ornitologo che cerca di scoprire
        e attirare i migliori uccelli nella tua riserva. Ogni uccello attiva una catena di
        poteri progressivi nei tre habitat (foresta, prateria, zone umide). Combo, motori e
        decisioni di tempistica caratterizzano una partita di {game.duration}.
        Vince chi accumula più punti tra uccelli, uova, cibo e bonus di carta finale.
      </p>
    </div>
    <SpecsCard game={game}/>
    <HouseRulesCard/>
  </div>
);

const InfoTabLocked = () => (
  <CommunityGate
    icon="📘"
    title="Aggiungi alla libreria per personalizzare"
    sub="Le house rules e gli specs personalizzati sono disponibili solo per i giochi nella tua libreria."
  />
);

// ═══════════════════════════════════════════════════════
// ─── TAB: SESSIONS ──────────────────────────────────
// ═══════════════════════════════════════════════════════
const SessionListItem = ({ s }) => (
  <article tabIndex={0} className="mai-card-row" style={{
    display:'flex', alignItems:'center', gap: 14, padding: 14,
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)', cursor:'pointer',
  }}>
    <div style={{
      width: 44, height: 44, borderRadius:'var(--r-md)',
      background: entityHsl('session', 0.12), color: entityHsl('session'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 22, flexShrink: 0,
    }} aria-hidden="true">🎯</div>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        display:'flex', alignItems:'baseline', gap: 8, flexWrap:'wrap',
        marginBottom: 3,
      }}>
        <h4 style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800,
          color:'var(--text)', margin: 0,
        }}>{s.title}</h4>
        <span style={{
          padding:'1px 7px', borderRadius:'var(--r-pill)',
          background: entityHsl('session', 0.14), color: entityHsl('session'),
          fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
          textTransform:'uppercase', letterSpacing:'.06em',
        }}>{s.subtitle?.split('·')[0] || 'Conclusa'}</span>
      </div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
        fontWeight: 600,
      }}>
        {s.date || s.subtitle} · {s.duration} · {s.playerIds?.length || 0} giocatori
        {s.winner && <> · 🏆 <strong style={{ color: entityHsl('player') }}>{s.winner}</strong></>}
        {s.score && <> · {s.score} pt</>}
      </div>
    </div>
    {/* avatar stack */}
    <div style={{ display:'flex', alignItems:'center' }}>
      {(s.playerIds || []).slice(0, 4).map((pid, i) => {
        const p = DS.byId[pid];
        return (
          <div key={pid} style={{
            width: 28, height: 28, borderRadius:'50%',
            background: `hsl(${p?.color || 200}, 60%, 55%)`,
            color:'#fff',
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 10,
            border:'2px solid var(--bg-card)', marginLeft: i === 0 ? 0 : -8,
            zIndex: 4 - i, flexShrink: 0,
          }}>{p?.initials || '?'}</div>
        );
      })}
    </div>
  </article>
);

const SessionsTab = ({ compact }) => {
  const all = [...RELATED_SESSIONS, ...MORE_SESSIONS];
  return (
    <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
      {!compact && (
        <div style={{
          display:'flex', alignItems:'center', justifyContent:'space-between', gap: 8,
        }}>
          <div style={{
            fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
            fontWeight: 700, letterSpacing:'.04em',
          }}>{all.length} partite registrate</div>
          <button type="button" style={{
            padding:'8px 14px', borderRadius:'var(--r-md)',
            background: entityHsl('session'), color:'#fff', border:'none',
            fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
            cursor:'pointer',
            boxShadow:`0 3px 10px ${entityHsl('session', 0.35)}`,
          }}>+ Nuova partita</button>
        </div>
      )}
      {all.map(s => <SessionListItem key={s.id} s={s}/>)}
    </div>
  );
};

const CommunityGate = ({ icon, title, sub }) => (
  <div style={{
    padding:'40px 24px', textAlign:'center',
    background:'var(--bg-card)',
    border:'1px dashed var(--border-strong)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center',
  }}>
    <div style={{
      width: 64, height: 64, borderRadius:'50%',
      background: entityHsl('game', 0.12), color: entityHsl('game'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 28, marginBottom: 14,
    }} aria-hidden="true">{icon}</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
      color:'var(--text)', margin:'0 0 6px',
    }}>{title}</h3>
    <p style={{
      fontSize: 12.5, color:'var(--text-muted)', maxWidth: 360,
      margin:'0 0 16px', lineHeight: 1.55,
    }}>{sub}</p>
    <button type="button" style={{
      padding:'10px 18px', borderRadius:'var(--r-md)',
      background: entityHsl('game'), color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800,
      cursor:'pointer',
      boxShadow:`0 4px 14px ${entityHsl('game', 0.35)}`,
    }}>+ Aggiungi a libreria</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: CHAT ──────────────────────────────────────
// ═══════════════════════════════════════════════════════
const ChatTab = ({ compact }) => (
  <div style={{ display:'flex', flexDirection:'column', gap: 12 }}>
    {/* Agent header */}
    <div style={{
      display:'flex', alignItems:'center', gap: 12,
      padding: 14,
      background:'var(--bg-card)', border:'1px solid var(--border)',
      borderRadius:'var(--r-lg)',
    }}>
      <div style={{
        width: 40, height: 40, borderRadius:'var(--r-md)',
        background: entityHsl('agent', 0.14), color: entityHsl('agent'),
        display:'flex', alignItems:'center', justifyContent:'center',
        fontSize: 22, flexShrink: 0,
      }} aria-hidden="true">🤖</div>
      <div style={{ flex: 1 }}>
        <div style={{
          fontFamily:'var(--f-display)', fontSize: 14, fontWeight: 800, color:'var(--text)',
        }}>Wingspan Rules</div>
        <div style={{
          fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
          fontWeight: 600,
        }}>Claude Sonnet · 2 KB · 230 invocazioni</div>
      </div>
      <span style={{
        padding:'3px 9px', borderRadius:'var(--r-pill)',
        background: entityHsl('agent', 0.14), color: entityHsl('agent'),
        fontFamily:'var(--f-mono)', fontSize: 9, fontWeight: 800,
        textTransform:'uppercase', letterSpacing:'.06em',
      }}>● Attivo</span>
    </div>

    {/* Messages */}
    <div style={{ display:'flex', flexDirection:'column', gap: 10, padding:'4px 0' }}>
      {MOCK_CHAT.map((m, i) => m.role === 'user' ? (
        <div key={i} style={{ display:'flex', justifyContent:'flex-end' }}>
          <div style={{
            maxWidth:'78%', padding:'10px 14px',
            background: entityHsl('chat'), color:'#fff',
            borderRadius:'var(--r-lg) var(--r-lg) 4px var(--r-lg)',
            fontSize: 13, lineHeight: 1.5,
            boxShadow: `0 2px 8px ${entityHsl('chat', 0.25)}`,
          }}>{m.text}</div>
        </div>
      ) : (
        <div key={i} style={{ display:'flex', justifyContent:'flex-start', gap: 8 }}>
          <div style={{
            width: 28, height: 28, borderRadius:'var(--r-md)',
            background: entityHsl('agent', 0.14), color: entityHsl('agent'),
            display:'flex', alignItems:'center', justifyContent:'center',
            fontSize: 14, flexShrink: 0,
          }} aria-hidden="true">🤖</div>
          <div style={{ maxWidth:'78%' }}>
            <div style={{
              padding:'10px 14px',
              background:'var(--bg-card)', border:'1px solid var(--border)',
              borderRadius:'4px var(--r-lg) var(--r-lg) var(--r-lg)',
              fontSize: 13, lineHeight: 1.55, color:'var(--text)',
            }}>{m.text}</div>
            {m.cite && (
              <div style={{
                marginTop: 4, padding:'4px 8px',
                display:'inline-flex', alignItems:'center', gap: 5,
                background: entityHsl('kb', 0.12), color: entityHsl('kb'),
                borderRadius:'var(--r-sm)',
                fontFamily:'var(--f-mono)', fontSize: 10, fontWeight: 700,
              }}><span aria-hidden="true">📄</span>{m.cite}</div>
            )}
          </div>
        </div>
      ))}
    </div>

    {/* Input */}
    <div style={{
      display:'flex', gap: 8, alignItems:'center',
      padding:'10px 12px',
      background:'var(--bg-card)', border:'1px solid var(--border-strong)',
      borderRadius:'var(--r-lg)',
      position:'sticky', bottom: 0,
    }}>
      <input type="text" placeholder="Chiedi all'agente Wingspan…"
        style={{
          flex: 1, border:'none', outline:'none', background:'transparent',
          color:'var(--text)', fontFamily:'var(--f-body)', fontSize: 13,
          padding:'4px 0',
        }}/>
      <button type="button" style={{
        padding:'7px 12px', borderRadius:'var(--r-md)',
        background: entityHsl('chat'), color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
        cursor:'pointer',
      }}>↑ Invia</button>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: STATS ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
// /* v2: GameStatsKpi */
const KpiCard = ({ label, value, unit, accent = 'game', icon }) => (
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
  </div>
);

// /* v2: GameLeaderboard */
const Leaderboard = () => (
  <div style={{
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)', padding: 18,
  }}>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
      color:'var(--text)', margin:'0 0 14px',
    }}>Classifica giocatori</h3>
    <div style={{ display:'flex', flexDirection:'column', gap: 0 }}>
      {LEADERBOARD.map((p, i) => (
        <div key={p.id} style={{
          display:'flex', alignItems:'center', gap: 12,
          padding:'10px 0',
          borderBottom: i < LEADERBOARD.length - 1 ? '1px solid var(--border-light)' : 'none',
        }}>
          <span style={{
            fontFamily:'var(--f-mono)', fontSize: 13, fontWeight: 800,
            color: i === 0 ? entityHsl('agent') : 'var(--text-muted)',
            width: 22, textAlign:'center',
          }}>{i === 0 ? '🏆' : `#${i+1}`}</span>
          <div style={{
            width: 32, height: 32, borderRadius:'50%',
            background: `hsl(${p.color}, 60%, 55%)`,
            color:'#fff',
            display:'inline-flex', alignItems:'center', justifyContent:'center',
            fontFamily:'var(--f-display)', fontWeight: 800, fontSize: 11,
            flexShrink: 0,
          }}>{p.initials}</div>
          <div style={{ flex: 1 }}>
            <div style={{
              fontFamily:'var(--f-display)', fontSize: 13, fontWeight: 800, color:'var(--text)',
            }}>{p.title}</div>
            <div style={{
              fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)', fontWeight: 600,
            }}>{p.plays} partite · avg {p.avgScore} pt</div>
          </div>
          <div style={{
            display:'flex', flexDirection:'column', alignItems:'flex-end',
          }}>
            <span style={{
              fontFamily:'var(--f-display)', fontSize: 16, fontWeight: 800,
              color: entityHsl('player'), fontVariantNumeric:'tabular-nums', lineHeight: 1,
            }}>{p.wins}</span>
            <span style={{
              fontFamily:'var(--f-mono)', fontSize: 9, color:'var(--text-muted)',
              textTransform:'uppercase', letterSpacing:'.06em', fontWeight: 700, marginTop: 2,
            }}>vittorie</span>
          </div>
        </div>
      ))}
    </div>
  </div>
);

const StatsTab = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 14 }}>
    <div style={{
      display:'grid', gridTemplateColumns:'repeat(auto-fit, minmax(160px, 1fr))', gap: 10,
    }}>
      <KpiCard label="Win rate" value="59" unit="%" accent="player" icon="🏆"/>
      <KpiCard label="Avg score" value="89" unit="pt" accent="game" icon="⭐"/>
      <KpiCard label="Partite" value="17" unit="" accent="session" icon="🎯"/>
      <KpiCard label="Ultima" value="3" unit="g fa" accent="event" icon="📅"/>
    </div>
    <Leaderboard/>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── TAB: DOCUMENTS ─────────────────────────────────
// ═══════════════════════════════════════════════════════
const DocItem = ({ kb }) => (
  <article tabIndex={0} className="mai-card-row" style={{
    display:'flex', alignItems:'center', gap: 14, padding: 12,
    background:'var(--bg-card)', border:'1px solid var(--border)',
    borderRadius:'var(--r-lg)', cursor:'pointer',
  }}>
    <div style={{
      width: 42, height: 42, borderRadius:'var(--r-md)',
      background: entityHsl('kb', 0.14), color: entityHsl('kb'),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 20, flexShrink: 0,
    }} aria-hidden="true">📄</div>
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 13, fontWeight: 700, color:'var(--text)',
        marginBottom: 3, whiteSpace:'nowrap', overflow:'hidden', textOverflow:'ellipsis',
      }}>{kb.title}</div>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 10, color:'var(--text-muted)',
        fontWeight: 600,
      }}>
        <span style={{
          padding:'1px 5px', borderRadius: 3,
          background: entityHsl('kb', 0.14), color: entityHsl('kb'),
          fontWeight: 800, marginRight: 6,
        }}>{kb.status === 'indexed' ? '✓ INDEXED' : kb.status.toUpperCase()}</span>
        {kb.size} · {kb.pages} pag · {kb.chunks} chunks
      </div>
    </div>
    <button type="button" style={{
      padding:'6px 12px', borderRadius:'var(--r-md)',
      background:'transparent', border:'1px solid var(--border)',
      color:'var(--text-sec)', fontFamily:'var(--f-display)',
      fontSize: 11, fontWeight: 800, cursor:'pointer',
    }}>Apri ↗</button>
  </article>
);

const DocsTab = () => (
  <div style={{ display:'flex', flexDirection:'column', gap: 10 }}>
    <div style={{
      display:'flex', alignItems:'center', justifyContent:'space-between',
    }}>
      <div style={{
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
        fontWeight: 700, letterSpacing:'.04em',
      }}>{RELATED_KBS.length} documenti indicizzati</div>
      <button type="button" style={{
        padding:'8px 14px', borderRadius:'var(--r-md)',
        background: entityHsl('kb'), color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800,
        cursor:'pointer',
        boxShadow:`0 3px 10px ${entityHsl('kb', 0.35)}`,
      }}>↑ Carica documento</button>
    </div>
    {RELATED_KBS.map(kb => <DocItem key={kb.id} kb={kb}/>)}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ERROR / LOADING STATES per tab ─────────────────
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
    }}>Errore caricamento</h3>
    <p style={{ fontSize: 12, color:'var(--text-muted)', margin:'0 0 12px', maxWidth: 320 }}>
      Impossibile recuperare i dati. Verifica la connessione.
    </p>
    <button type="button" onClick={onRetry}
      style={{
        padding:'7px 14px', borderRadius:'var(--r-md)',
        background:'hsl(var(--c-danger))', color:'#fff', border:'none',
        fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
      }}>↻ Riprova</button>
  </div>
);

const TabEmpty = ({ icon, title, sub, ctaLabel, ctaColor='game' }) => (
  <div style={{
    padding:'40px 24px', textAlign:'center',
    background:'var(--bg-card)',
    border:'1px dashed var(--border-strong)',
    borderRadius:'var(--r-xl)',
    display:'flex', flexDirection:'column', alignItems:'center',
  }}>
    <div style={{
      width: 60, height: 60, borderRadius:'50%',
      background: entityHsl(ctaColor, 0.12), color: entityHsl(ctaColor),
      display:'flex', alignItems:'center', justifyContent:'center',
      fontSize: 28, marginBottom: 12,
    }} aria-hidden="true">{icon}</div>
    <h3 style={{
      fontFamily:'var(--f-display)', fontSize: 15, fontWeight: 800,
      color:'var(--text)', margin:'0 0 4px',
    }}>{title}</h3>
    <p style={{ fontSize: 12.5, color:'var(--text-muted)', margin:'0 0 14px', maxWidth: 340 }}>
      {sub}
    </p>
    <button type="button" style={{
      padding:'8px 16px', borderRadius:'var(--r-md)',
      background: entityHsl(ctaColor), color:'#fff', border:'none',
      fontFamily:'var(--f-display)', fontSize: 12, fontWeight: 800, cursor:'pointer',
      boxShadow: `0 3px 10px ${entityHsl(ctaColor, 0.35)}`,
    }}>{ctaLabel}</button>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── BODY ───────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const GameDetailBody = ({ stateOverride, variant = 'own', compact, initialTab = 'info' }) => {
  const [tab, setTab] = useState(initialTab);
  const isLoading = stateOverride === 'loading';
  const isError = stateOverride === 'error';

  const tabs = [
    { id:'info', icon:'📘', label:'Info' },
    { id:'sessions', icon:'🎯', label:'Partite', count: variant === 'own' ? RELATED_SESSIONS.length + MORE_SESSIONS.length : 0, locked: variant !== 'own' },
    { id:'chat', icon:'💬', label:'Chat', count: variant === 'own' ? RELATED_CHATS.length : 0, locked: variant !== 'own' },
    { id:'stats', icon:'📊', label:'Stats', locked: variant !== 'own' },
    { id:'docs', icon:'📄', label:'Documenti', count: RELATED_KBS.length },
  ];

  const connections = [
    { entityType:'agent', count: 2, label:'Agenti', isEmpty: false },
    { entityType:'kb', count: 5, label:'Documenti', isEmpty: false },
    { entityType:'toolkit', count: 1, label:'Toolkit', isEmpty: false },
    { entityType:'session', count: 12, label:'Partite', isEmpty: false },
    { entityType:'player', count: 4, label:'Giocatori', isEmpty: false },
    { entityType:'chat', count: 0, label:'Chat', isEmpty: true },
  ];

  const renderTab = () => {
    if (isLoading) return <TabSkeleton/>;
    if (isError) return <TabError/>;

    if (variant !== 'own' && (tab === 'sessions' || tab === 'chat' || tab === 'stats')) {
      const cfg = {
        sessions: { icon:'🎯', t:'Aggiungi alla libreria per registrare partite',
          sub:'Le partite sono tracciate solo per i giochi della tua collezione.' },
        chat: { icon:'💬', t:'Aggiungi alla libreria per chattare',
          sub:'Sblocca chat con l\'agente esperto del gioco una volta in libreria.' },
        stats: { icon:'📊', t:'Aggiungi alla libreria per vedere le stats',
          sub:'Win rate, leaderboard e trend sono disponibili per i tuoi giochi.' },
      }[tab];
      return <TabEmpty icon={cfg.icon} title={cfg.t} sub={cfg.sub} ctaLabel="+ Aggiungi a libreria"/>;
    }

    if (tab === 'info') return <InfoTab game={GAME}/>;
    if (tab === 'sessions') return <SessionsTab compact={compact}/>;
    if (tab === 'chat') return <ChatTab compact={compact}/>;
    if (tab === 'stats') return <StatsTab/>;
    if (tab === 'docs') return <DocsTab/>;
    return null;
  };

  return (
    <>
      <GameHero game={GAME} variant={variant} compact={compact} loading={isLoading}/>
      {/* ConnectionBar sticky */}
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
        <GameTabs tabs={tabs} active={tab} onChange={setTab} compact={compact} locked={variant !== 'own'}/>
      </div>
      <div style={{
        padding: compact ? '14px 16px 32px' : '20px 32px 64px',
      }}>
        {renderTab()}
      </div>
    </>
  );
};

// ═══════════════════════════════════════════════════════
// ─── DESKTOP / MOBILE SHELLS ─────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopNav = () => (
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
      <a href="#" style={{ color:'inherit' }}>Giochi</a>
      <span aria-hidden="true"> / </span>
      <strong style={{ color:'var(--text-sec)' }}>{GAME.title}</strong>
    </div>
  </div>
);

const DesktopScreen = ({ stateOverride, variant }) => (
  <div style={{ minHeight: 720, background:'var(--bg)' }}>
    <DesktopNav/>
    <GameDetailBody stateOverride={stateOverride} variant={variant}/>
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
    }}>games / wingspan</div>
    <button aria-label="Più" style={{
      width: 32, height: 32, borderRadius:'var(--r-md)',
      background:'var(--bg-card)', border:'1px solid var(--border)',
      color:'var(--text)', fontSize: 14, cursor:'pointer',
    }}>⋯</button>
  </div>
);
const MobileScreen = ({ stateOverride, variant, initialTab }) => (
  <>
    <PhoneSbar/>
    <div style={{ flex: 1, overflowY:'auto', position:'relative', background:'var(--bg)' }}>
      <PhoneTopNav/>
      <GameDetailBody stateOverride={stateOverride} variant={variant} compact initialTab={initialTab}/>
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
        padding:'9px 14px', background:'var(--bg-muted)',
        borderBottom:'1px solid var(--border)',
        fontFamily:'var(--f-mono)', fontSize: 11, color:'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius:'50%', background:'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign:'center', letterSpacing:'.04em' }}>meepleai.app/games/wingspan</span>
      </div>
      {children}
    </div>
    {desc && <div style={{
      fontSize: 11, color:'var(--text-muted)', maxWidth: 720,
      textAlign:'center', lineHeight: 1.55,
    }}>{desc}</div>}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── ROOT ────────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  { id:'m1', stateOverride:null, variant:'own', tab:'info',     label:'01 · Own · Info', desc:'Hero 180 + ConnectionBar 6 pip (chat empty dashed) + 5 tab. Info: descrizione + specs grid + house rules toggle.' },
  { id:'m2', stateOverride:null, variant:'own', tab:'sessions', label:'02 · Own · Sessions', desc:'Lista partite con avatar stack giocatori, winner pip, score. CTA "+ Nuova partita".' },
  { id:'m3', stateOverride:null, variant:'own', tab:'chat',     label:'03 · Own · Chat', desc:'Agent header + bubble user (entity-chat) + risposta agent con citation pip kb + input sticky.' },
  { id:'m4', stateOverride:null, variant:'own', tab:'stats',    label:'04 · Own · Stats', desc:'4 KPI cards (win rate / avg score / partite / ultima) + leaderboard giocatori.' },
  { id:'m5', stateOverride:null, variant:'community', tab:'sessions', label:'05 · Community · locked', desc:'Variante community: tabs Sessions/Chat/Stats lockate (🔒 + opacity), tab corrente mostra empty + CTA "+ Aggiungi a libreria".' },
  { id:'m6', stateOverride:'loading', variant:'own', tab:'info', label:'06 · Loading', desc:'Hero skeleton + ConnectionBar shimmer + tabs nav visible + tab body 4 row skeleton.' },
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
        }}>SP4 wave 1 · #2/5 · Hero + body con tabs</div>
        <h1>Game detail — /games/[id]</h1>
        <p className="lead">
          Cover hero 240px + <strong>ConnectionBar</strong> sticky 1:1 prod (6 pip, chat empty dashed) +
          <strong> GameTabs</strong> animated underline (5 tabs). Variante <strong>own</strong> sblocca tutto;
          <strong> community</strong> locka Sessions/Chat/Stats con CTA "+ Aggiungi a libreria".
        </p>

        <div className="section-label">Mobile · 375 — 6 stati / tabs</div>
        <div className="phones-grid">
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              <MobileScreen stateOverride={s.stateOverride} variant={s.variant} initialTab={s.tab}/>
            </PhoneShell>
          ))}
        </div>

        <div className="section-label">Desktop · 1440 — 4 varianti chiave</div>
        <div style={{ display:'flex', flexDirection:'column', gap: 36 }}>
          <DesktopFrame label="07 · Desktop · Own · Info"
            desc="Variante own + tab Info: hero 240 + ConnectionBar sticky con 6 pip (chat empty dashed) + tabs animated underline. Body: descrizione + specs grid 8 cells + house rules toggle.">
            <DesktopScreen stateOverride={null} variant="own"/>
          </DesktopFrame>
          <DesktopFrame label="08 · Desktop · Community · locked"
            desc="Gioco non in libreria: action bar primaria '+ Aggiungi a libreria'. Tabs Sessions/Chat/Stats con icon 🔒 + opacity .55 + cursor not-allowed. Tab Info e Documenti accessibili (read-only).">
            <DesktopScreen stateOverride={null} variant="community"/>
          </DesktopFrame>
          <DesktopFrame label="09 · Desktop · Loading"
            desc="Hero skeleton 240 + ConnectionBar shimmer (6 pill) + tabs nav visible + body 4 row 76px shimmer. Action bar nascosta durante load (skeleton hero copre).">
            <DesktopScreen stateOverride="loading" variant="own"/>
          </DesktopFrame>
          <DesktopFrame label="10 · Desktop · Error"
            desc="Hero + ConnectionBar visibili (cached), tab body mostra danger card '⚠ Errore caricamento' + retry CTA. L'errore è scoped al tab content, non all'intera pagina.">
            <DesktopScreen stateOverride="error" variant="own"/>
          </DesktopFrame>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
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
        .mai-cb-scroll::-webkit-scrollbar { display: none; }
        .mai-card-row {
          outline: none;
          transition: transform var(--dur-sm) var(--ease-out), border-color var(--dur-sm), box-shadow var(--dur-sm);
        }
        .mai-card-row:hover {
          transform: translateY(-1px);
          border-color: hsl(var(--c-game) / .35);
          box-shadow: var(--shadow-xs);
        }
        .mai-card-row:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
        button:focus-visible, a:focus-visible, input:focus-visible {
          outline: 2px solid hsl(var(--c-game));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
