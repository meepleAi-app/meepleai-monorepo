/* MeepleAI SP4 — AddGameDrawer · BGG Search Step
   Issue: #911
   File: admin-mockups/design_files/sp4-add-game-bgg-step.{html,jsx}

   Surface: AddGameDrawer → tab "From BGG"
   Entity color: --c-game (orange)

   6 screen states:
   1. Empty state       — hint + placeholder + rate-limit note
   2. Loading state     — skeleton cards + "BGG può essere lento" banner + cancel
   3. Results state     — 6 BGG result cards with add-to-library CTA
   4. BGG throttled     — HTTP 202 yellow warning + countdown + retry
   5. Auto-redirect     — game already in SharedGameCatalog dialog + 2 CTAs
   6. Tier-quota lock   — free-tier 3-game limit + upgrade CTA

   Mobile 375 + Desktop 1280, light + dark.
*/

const { useState, useEffect } = React;
const DS = window.DS;

// ─── Entity HSL helper ───────────────────────────────
const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.game;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── BGG Sample data (spec) ──────────────────────────
const BGG_RESULTS = [
  { id: 13,     title: 'Catan',            year: 1995, players: '3–4', weight: 2.3, thumb: '#c4a574' },
  { id: 174430, title: 'Gloomhaven',       year: 2017, players: '1–4', weight: 3.9, thumb: '#5c3a21' },
  { id: 266192, title: 'Wingspan',         year: 2019, players: '1–5', weight: 2.4, thumb: '#7da87a' },
  { id: 167791, title: 'Terraforming Mars',year: 2016, players: '1–5', weight: 3.2, thumb: '#a85c43' },
  { id: 230802, title: 'Azul',             year: 2017, players: '2–4', weight: 1.8, thumb: '#3d6da8' },
  { id: 30549,  title: 'Pandemic',         year: 2008, players: '2–4', weight: 2.4, thumb: '#5b8a96' },
];

// ─── Weight pill ─────────────────────────────────────
const WeightPill = ({ w }) => {
  const color = w >= 3.5 ? 'hsl(350,89%,60%)' : w >= 2.5 ? 'hsl(38,92%,50%)' : 'hsl(142,70%,45%)';
  return (
    <span style={{
      padding: '1px 6px', borderRadius: 'var(--r-pill)',
      background: w >= 3.5 ? 'hsla(350,89%,60%,0.12)' : w >= 2.5 ? 'hsla(38,92%,50%,0.12)' : 'hsla(142,70%,45%,0.12)',
      color, fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
    }}>W {w.toFixed(1)}</span>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SHARED: Drawer chrome / shell ───────────────────
// Represents the AddGameDrawer wrapper: header + tab strip
// ═══════════════════════════════════════════════════════
const DrawerShell = ({ children, compact }) => (
  <div style={{
    background: 'var(--bg-card)',
    border: '1px solid var(--border)',
    borderRadius: compact ? 'var(--r-xl)' : 'var(--r-xl)',
    boxShadow: 'var(--shadow-drawer)',
    overflow: 'hidden',
    display: 'flex',
    flexDirection: 'column',
    width: compact ? 360 : 520,
    maxHeight: compact ? 640 : 680,
    minHeight: compact ? 520 : 560,
  }}>
    {/* Drawer handle (mobile) */}
    {compact && (
      <div style={{ display: 'flex', justifyContent: 'center', padding: '10px 0 4px' }}>
        <div style={{
          width: 36, height: 4, borderRadius: 'var(--r-pill)',
          background: 'var(--border-strong)',
        }}/>
      </div>
    )}
    {/* Header */}
    <div style={{
      padding: compact ? '8px 16px 0' : '14px 20px 0',
      borderBottom: '1px solid var(--border)',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span aria-hidden="true" style={{ fontSize: compact ? 18 : 22 }}>🎲</span>
          <span style={{
            fontFamily: 'var(--f-display)', fontWeight: 700,
            fontSize: compact ? 15 : 18, color: 'var(--text)',
          }}>Aggiungi gioco</span>
        </div>
        <button aria-label="Chiudi" style={{
          width: 28, height: 28, borderRadius: 'var(--r-sm)',
          background: 'var(--bg-muted)', border: 'none',
          color: 'var(--text-muted)', fontSize: 14, cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}>✕</button>
      </div>
      {/* Tab strip */}
      <div style={{ display: 'flex', gap: 2 }}>
        {['Manuale', 'Da BGG', 'Catalogo'].map((tab, i) => (
          <button key={tab} style={{
            padding: compact ? '5px 10px' : '6px 14px',
            borderRadius: 'var(--r-sm) var(--r-sm) 0 0',
            border: 'none',
            background: i === 1 ? 'hsl(var(--c-game) / 0.10)' : 'transparent',
            borderBottom: i === 1 ? `2px solid hsl(var(--c-game))` : '2px solid transparent',
            color: i === 1 ? 'hsl(var(--c-game))' : 'var(--text-muted)',
            fontFamily: 'var(--f-display)', fontSize: compact ? 11 : 12.5, fontWeight: 700,
            cursor: 'pointer',
          }}>{tab}</button>
        ))}
      </div>
    </div>
    {/* Body */}
    <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column' }}>
      {children}
    </div>
  </div>
);

// ─── Search input bar ────────────────────────────────
const SearchBar = ({ placeholder = 'Es. Catan, Wingspan, Brass Birmingham…', compact }) => (
  <div style={{
    padding: compact ? '10px 12px' : '14px 16px',
    borderBottom: '1px solid var(--border-light)',
  }}>
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8,
      background: 'var(--bg-sunken)',
      border: '1px solid var(--border)',
      borderRadius: 'var(--r-md)',
      padding: compact ? '7px 10px' : '9px 12px',
    }}>
      <span aria-hidden="true" style={{ color: 'var(--text-muted)', fontSize: 14 }}>🔍</span>
      <span style={{
        fontFamily: 'var(--f-body)', fontSize: compact ? 12 : 13,
        color: 'var(--text-muted)', flex: 1,
      }}>{placeholder}</span>
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700,
        color: 'var(--text-muted)', padding: '1px 5px',
        border: '1px solid var(--border)', borderRadius: 'var(--r-xs)',
      }}>⏎</span>
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATE 1 · Empty ─────────────────────────────────
// ═══════════════════════════════════════════════════════
const BggStepEmpty = ({ compact }) => (
  <DrawerShell compact={compact}>
    <SearchBar compact={compact}/>
    <div style={{
      flex: 1, display: 'flex', flexDirection: 'column',
      alignItems: 'center', justifyContent: 'center',
      padding: compact ? '20px 16px' : '32px 24px',
      gap: 16,
    }}>
      {/* Illustration */}
      <div style={{
        width: compact ? 72 : 88,
        height: compact ? 72 : 88,
        borderRadius: '50%',
        background: 'hsl(var(--c-game) / 0.08)',
        border: `2px dashed hsl(var(--c-game) / 0.25)`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: compact ? 32 : 40,
      }}>🎲</div>
      <div style={{ textAlign: 'center' }}>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 700,
          fontSize: compact ? 15 : 17, color: 'var(--text)',
          marginBottom: 6,
        }}>Cerca su BoardGameGeek</div>
        <div style={{
          fontFamily: 'var(--f-body)', fontSize: compact ? 11.5 : 12.5,
          color: 'var(--text-sec)', lineHeight: 1.55, maxWidth: 280,
        }}>
          Digita il titolo, parte del titolo o il codice BGG del gioco
          che vuoi aggiungere alla tua libreria.
        </div>
      </div>
      {/* Example queries */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6, justifyContent: 'center' }}>
        {['Catan', 'Wingspan', '174430', 'Brass Birm…'].map(q => (
          <span key={q} style={{
            padding: '3px 9px', borderRadius: 'var(--r-pill)',
            background: 'hsl(var(--c-game) / 0.08)',
            border: '1px solid hsl(var(--c-game) / 0.18)',
            color: 'hsl(var(--c-game))',
            fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
            cursor: 'pointer',
          }}>{q}</span>
        ))}
      </div>
      {/* Rate-limit note */}
      <div style={{
        display: 'flex', alignItems: 'flex-start', gap: 7,
        padding: '8px 12px', borderRadius: 'var(--r-md)',
        background: 'var(--bg-sunken)',
        border: '1px solid var(--border)',
        maxWidth: 300,
      }}>
        <span aria-hidden="true" style={{ fontSize: 12, flexShrink: 0, marginTop: 1 }}>⏱</span>
        <span style={{
          fontFamily: 'var(--f-body)', fontSize: compact ? 10.5 : 11,
          color: 'var(--text-muted)', lineHeight: 1.5,
        }}>
          BGG free tier: <strong style={{ color: 'var(--text-sec)' }}>60 richieste/ora per utente</strong>.
          Ricerche frequenti potrebbero essere temporaneamente sospese.
        </span>
      </div>
    </div>
  </DrawerShell>
);

// ═══════════════════════════════════════════════════════
// ─── STATE 2 · Loading ───────────────────────────────
// ═══════════════════════════════════════════════════════
const SkeletonCard = ({ compact }) => (
  <div style={{
    display: 'flex', gap: compact ? 10 : 12, alignItems: 'flex-start',
    padding: compact ? '10px 12px' : '12px 16px',
    borderBottom: '1px solid var(--border-light)',
  }}>
    {/* Thumb skeleton */}
    <div style={{
      width: compact ? 44 : 56, height: compact ? 44 : 56,
      borderRadius: 'var(--r-md)', flexShrink: 0,
      background: 'var(--bg-muted)',
      animation: 'shimmer 1.6s ease-in-out infinite',
    }}/>
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 6 }}>
      <div style={{
        height: compact ? 12 : 14, borderRadius: 'var(--r-xs)',
        width: '70%', background: 'var(--bg-muted)',
        animation: 'shimmer 1.6s ease-in-out infinite',
      }}/>
      <div style={{
        height: 10, borderRadius: 'var(--r-xs)',
        width: '45%', background: 'var(--bg-muted)',
        animation: 'shimmer 1.6s ease-in-out 0.2s infinite',
      }}/>
      <div style={{
        height: 10, borderRadius: 'var(--r-xs)',
        width: '30%', background: 'var(--bg-muted)',
        animation: 'shimmer 1.6s ease-in-out 0.4s infinite',
      }}/>
    </div>
    <div style={{
      width: compact ? 70 : 80, height: compact ? 26 : 30,
      borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', flexShrink: 0,
      animation: 'shimmer 1.6s ease-in-out 0.1s infinite',
    }}/>
  </div>
);

const BggStepLoading = ({ compact }) => (
  <DrawerShell compact={compact}>
    <SearchBar placeholder="tra pomeriggio…" compact={compact}/>
    {/* BGG slow warning banner */}
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8,
      padding: compact ? '7px 12px' : '8px 16px',
      background: 'hsl(var(--c-agent) / 0.08)',
      borderBottom: '1px solid hsl(var(--c-agent) / 0.18)',
    }}>
      <span aria-hidden="true" style={{ fontSize: 13, flexShrink: 0 }}>⏳</span>
      <span style={{
        flex: 1,
        fontFamily: 'var(--f-body)', fontSize: compact ? 11 : 12,
        color: 'var(--text-sec)', lineHeight: 1.4,
      }}>
        <strong>BGG può essere lento</strong>, attendere il completamento della ricerca…
      </span>
      <button style={{
        padding: '3px 9px', borderRadius: 'var(--r-sm)',
        background: 'transparent',
        border: '1px solid var(--border)',
        color: 'var(--text-muted)',
        fontFamily: 'var(--f-display)', fontSize: 10, fontWeight: 700,
        cursor: 'pointer', flexShrink: 0,
      }}>Annulla</button>
    </div>
    {/* Skeleton list */}
    <div style={{ flex: 1, overflowY: 'auto' }}>
      <style>{`
        @keyframes shimmer {
          0%   { opacity: 1; }
          50%  { opacity: 0.45; }
          100% { opacity: 1; }
        }
      `}</style>
      {[0, 1, 2, 3, 4].map(i => (
        <SkeletonCard key={i} compact={compact}/>
      ))}
    </div>
  </DrawerShell>
);

// ═══════════════════════════════════════════════════════
// ─── STATE 3 · Results ───────────────────────────────
// ═══════════════════════════════════════════════════════
const BggResultCard = ({ game, compact }) => (
  <div style={{
    display: 'flex', alignItems: 'center', gap: compact ? 10 : 12,
    padding: compact ? '9px 12px' : '11px 16px',
    borderBottom: '1px solid var(--border-light)',
    transition: 'background var(--dur-xs)',
  }}
  onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
  onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
  >
    {/* Thumbnail */}
    <div style={{
      width: compact ? 44 : 56, height: compact ? 44 : 56,
      borderRadius: 'var(--r-md)', flexShrink: 0,
      background: game.thumb,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      border: '1px solid rgba(0,0,0,0.12)',
    }}>
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: compact ? 8 : 9,
        color: 'rgba(255,255,255,0.8)', fontWeight: 700,
        textAlign: 'center', lineHeight: 1.2, padding: '0 2px',
      }}>{game.id}</span>
    </div>
    {/* Info */}
    <div style={{ flex: 1, minWidth: 0 }}>
      <div style={{
        fontFamily: 'var(--f-display)', fontWeight: 700,
        fontSize: compact ? 13 : 15, color: 'var(--text)',
        whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
        marginBottom: 3,
      }}>{game.title}</div>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap',
      }}>
        <span style={{
          fontFamily: 'var(--f-body)', fontSize: compact ? 10.5 : 11.5,
          color: 'var(--text-muted)',
        }}>{game.year}</span>
        <span style={{ color: 'var(--border-strong)', fontSize: 10 }}>·</span>
        <span style={{
          fontFamily: 'var(--f-body)', fontSize: compact ? 10.5 : 11.5,
          color: 'var(--text-muted)',
        }}>👥 {game.players}</span>
        <span style={{ color: 'var(--border-strong)', fontSize: 10 }}>·</span>
        <WeightPill w={game.weight}/>
        <span style={{
          fontFamily: 'var(--f-mono)', fontSize: compact ? 9 : 10,
          color: 'hsl(var(--c-kb))', fontWeight: 600,
        }}>BGG#{game.id}</span>
      </div>
    </div>
    {/* Add button */}
    <button style={{
      padding: compact ? '5px 9px' : '6px 12px',
      borderRadius: 'var(--r-md)',
      background: 'hsl(var(--c-game))',
      border: 'none',
      color: '#fff',
      fontFamily: 'var(--f-display)', fontSize: compact ? 10 : 11, fontWeight: 700,
      cursor: 'pointer', flexShrink: 0,
      display: 'flex', alignItems: 'center', gap: 4,
    }}>
      <span aria-hidden="true">＋</span>
      {compact ? 'Aggiungi' : 'Aggiungi alla libreria'}
    </button>
  </div>
);

const BggStepResults = ({ compact }) => (
  <DrawerShell compact={compact}>
    <SearchBar placeholder="tra pomeriggio" compact={compact}/>
    {/* Result count */}
    <div style={{
      padding: compact ? '5px 12px' : '6px 16px',
      borderBottom: '1px solid var(--border-light)',
      display: 'flex', alignItems: 'center', gap: 6,
    }}>
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: compact ? 9.5 : 10.5, fontWeight: 700,
        color: 'hsl(var(--c-game))',
      }}>6 risultati</span>
      <span style={{
        fontFamily: 'var(--f-body)', fontSize: compact ? 10 : 11,
        color: 'var(--text-muted)',
      }}>da BoardGameGeek</span>
    </div>
    {/* Results list */}
    <div style={{ flex: 1, overflowY: 'auto' }}>
      {BGG_RESULTS.map(g => (
        <BggResultCard key={g.id} game={g} compact={compact}/>
      ))}
    </div>
    {/* Footer note */}
    <div style={{
      padding: compact ? '6px 12px' : '8px 16px',
      borderTop: '1px solid var(--border-light)',
      fontFamily: 'var(--f-body)', fontSize: compact ? 9.5 : 10.5,
      color: 'var(--text-muted)', textAlign: 'center', lineHeight: 1.4,
    }}>
      Risultati BGG · max 6 per ricerca · <span style={{ fontFamily: 'var(--f-mono)', fontSize: 9 }}>🔗 boardgamegeek.com</span>
    </div>
  </DrawerShell>
);

// ═══════════════════════════════════════════════════════
// ─── STATE 4 · BGG Throttled (HTTP 202) ──────────────
// ═══════════════════════════════════════════════════════
const BggStepThrottled = ({ compact }) => (
  <DrawerShell compact={compact}>
    <SearchBar placeholder="tra pomeriggio" compact={compact}/>
    {/* Throttle warning banner — prominent */}
    <div style={{
      margin: compact ? 10 : 14,
      padding: compact ? '12px 14px' : '16px 18px',
      borderRadius: 'var(--r-lg)',
      background: 'hsl(var(--c-event) / 0.08)',
      border: `1.5px solid hsl(var(--c-event) / 0.35)`,
      display: 'flex', flexDirection: 'column', gap: 12,
    }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
        <span aria-hidden="true" style={{ fontSize: compact ? 20 : 24, flexShrink: 0 }}>⚠️</span>
        <div style={{ flex: 1 }}>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700,
            fontSize: compact ? 13 : 14.5, color: 'hsl(var(--c-event))',
            marginBottom: 4,
          }}>BGG rate-limited</div>
          <div style={{
            fontFamily: 'var(--f-body)', fontSize: compact ? 11 : 12,
            color: 'var(--text-sec)', lineHeight: 1.5,
          }}>
            BoardGameGeek ha temporaneamente sospeso le tue richieste.
            Riprova automaticamente tra:
          </div>
        </div>
      </div>
      {/* Countdown */}
      <div style={{
        display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 12,
        padding: compact ? '10px 0' : '12px 0',
      }}>
        <div style={{
          display: 'flex', flexDirection: 'column', alignItems: 'center',
          padding: compact ? '8px 18px' : '10px 22px',
          borderRadius: 'var(--r-md)',
          background: 'hsl(var(--c-event) / 0.12)',
          border: '1px solid hsl(var(--c-event) / 0.22)',
        }}>
          <span style={{
            fontFamily: 'var(--f-mono)', fontWeight: 800,
            fontSize: compact ? 28 : 36,
            color: 'hsl(var(--c-event))',
            fontVariantNumeric: 'tabular-nums',
            lineHeight: 1,
          }}>23</span>
          <span style={{
            fontFamily: 'var(--f-display)', fontSize: compact ? 9 : 10,
            color: 'hsl(var(--c-event) / 0.7)', fontWeight: 600,
            marginTop: 3, textTransform: 'uppercase', letterSpacing: '.06em',
          }}>secondi</span>
        </div>
      </div>
      {/* Actions */}
      <div style={{ display: 'flex', gap: 8 }}>
        <button style={{
          flex: 1,
          padding: compact ? '7px 0' : '9px 0',
          borderRadius: 'var(--r-md)',
          background: 'hsl(var(--c-event))',
          border: 'none',
          color: '#fff',
          fontFamily: 'var(--f-display)', fontSize: compact ? 11 : 12.5, fontWeight: 700,
          cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 6,
        }}>
          <span aria-hidden="true">🔄</span> Riprova ora
        </button>
        <button style={{
          padding: compact ? '7px 12px' : '9px 14px',
          borderRadius: 'var(--r-md)',
          background: 'transparent',
          border: '1px solid var(--border)',
          color: 'var(--text-muted)',
          fontFamily: 'var(--f-display)', fontSize: compact ? 11 : 12.5, fontWeight: 700,
          cursor: 'pointer',
        }}>Annulla</button>
      </div>
    </div>
    {/* Stale previous results dimmed */}
    <div style={{ flex: 1, overflowY: 'auto', opacity: 0.35, pointerEvents: 'none' }}>
      {BGG_RESULTS.slice(0, 3).map(g => (
        <BggResultCard key={g.id} game={g} compact={compact}/>
      ))}
    </div>
    <div style={{
      padding: compact ? '6px 12px' : '7px 16px',
      borderTop: '1px solid var(--border-light)',
      fontFamily: 'var(--f-body)', fontSize: compact ? 9.5 : 10.5,
      color: 'var(--text-muted)', textAlign: 'center',
    }}>Risultati precedenti in sola lettura durante il cooldown</div>
  </DrawerShell>
);

// ═══════════════════════════════════════════════════════
// ─── STATE 5 · Auto-redirect (already in catalog) ────
// ═══════════════════════════════════════════════════════
const BggStepAutoRedirect = ({ compact }) => (
  <DrawerShell compact={compact}>
    <SearchBar placeholder="Wingspan" compact={compact}/>
    {/* Dimmed single result */}
    <div style={{ opacity: 0.3, pointerEvents: 'none' }}>
      <BggResultCard game={BGG_RESULTS[2]} compact={compact}/>
    </div>
    {/* Overlay dialog inline */}
    <div style={{
      margin: compact ? '8px 10px' : '12px 14px',
      padding: compact ? '14px' : '18px 20px',
      borderRadius: 'var(--r-xl)',
      background: 'var(--bg-card)',
      border: `1.5px solid hsl(var(--c-game) / 0.35)`,
      boxShadow: 'var(--shadow-md)',
      display: 'flex', flexDirection: 'column', gap: 12,
    }}>
      {/* Icon + title */}
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 10 }}>
        <div style={{
          width: compact ? 38 : 44, height: compact ? 38 : 44,
          borderRadius: 'var(--r-md)',
          background: BGG_RESULTS[2].thumb,
          flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center',
        }}>
          <span style={{ fontFamily: 'var(--f-mono)', fontSize: 8, color: 'rgba(255,255,255,0.8)', fontWeight: 700 }}>
            {BGG_RESULTS[2].id}
          </span>
        </div>
        <div>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700,
            fontSize: compact ? 13.5 : 15, color: 'var(--text)', marginBottom: 3,
          }}>{BGG_RESULTS[2].title} è già nel catalogo</div>
          <div style={{
            fontFamily: 'var(--f-body)', fontSize: compact ? 11 : 12,
            color: 'var(--text-sec)', lineHeight: 1.5,
          }}>
            Questo gioco è già presente nel <strong>Catalogo community</strong>.
            Puoi usare la scheda condivisa o creare una copia privata.
          </div>
        </div>
      </div>
      {/* Community catalog badge */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 7,
        padding: compact ? '6px 10px' : '7px 12px',
        borderRadius: 'var(--r-md)',
        background: 'hsl(var(--c-game) / 0.06)',
        border: '1px solid hsl(var(--c-game) / 0.18)',
      }}>
        <span aria-hidden="true" style={{ fontSize: 14 }}>🌐</span>
        <div>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700,
            fontSize: compact ? 11 : 12, color: 'hsl(var(--c-game))',
          }}>Scheda community disponibile</div>
          <div style={{
            fontFamily: 'var(--f-body)', fontSize: compact ? 9.5 : 10.5,
            color: 'var(--text-muted)',
          }}>
            KB condivisa · PDF · Agente pre-configurato
          </div>
        </div>
      </div>
      {/* CTAs */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
        <button style={{
          padding: compact ? '8px 0' : '10px 0',
          borderRadius: 'var(--r-md)',
          background: 'hsl(var(--c-game))',
          border: 'none', color: '#fff',
          fontFamily: 'var(--f-display)', fontSize: compact ? 12 : 13.5, fontWeight: 700,
          cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 7,
        }}>
          <span aria-hidden="true">🌐</span>
          Vai al catalogo community
        </button>
        <button style={{
          padding: compact ? '7px 0' : '9px 0',
          borderRadius: 'var(--r-md)',
          background: 'transparent',
          border: '1px solid var(--border-strong)',
          color: 'var(--text-sec)',
          fontFamily: 'var(--f-display)', fontSize: compact ? 11 : 12.5, fontWeight: 600,
          cursor: 'pointer',
          display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 7,
        }}>
          <span aria-hidden="true">📋</span>
          Crea copia privata comunque
        </button>
      </div>
    </div>
    <div style={{ flex: 1 }}/>
  </DrawerShell>
);

// ═══════════════════════════════════════════════════════
// ─── STATE 6 · Tier-quota lock ───────────────────────
// ═══════════════════════════════════════════════════════
const BggStepTierLock = ({ compact }) => (
  <DrawerShell compact={compact}>
    <SearchBar compact={compact}/>
    {/* Dimmed ghost results */}
    <div style={{ opacity: 0.18, pointerEvents: 'none' }}>
      {BGG_RESULTS.slice(0, 3).map(g => (
        <BggResultCard key={g.id} game={g} compact={compact}/>
      ))}
    </div>
    {/* Lock overlay card */}
    <div style={{
      margin: compact ? '8px 10px' : '12px 14px',
      padding: compact ? '16px' : '20px 22px',
      borderRadius: 'var(--r-xl)',
      background: `linear-gradient(135deg, hsl(var(--c-game) / 0.06), hsl(var(--c-game) / 0.02))`,
      border: `1.5px solid hsl(var(--c-game) / 0.28)`,
      boxShadow: 'var(--shadow-md)',
      display: 'flex', flexDirection: 'column', alignItems: 'center',
      gap: compact ? 12 : 14, textAlign: 'center',
    }}>
      {/* Lock icon */}
      <div style={{
        width: compact ? 52 : 64, height: compact ? 52 : 64,
        borderRadius: '50%',
        background: 'hsl(var(--c-game) / 0.10)',
        border: `2px solid hsl(var(--c-game) / 0.22)`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: compact ? 22 : 28,
      }}>🔒</div>
      {/* Limit message */}
      <div>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 700,
          fontSize: compact ? 14 : 16, color: 'var(--text)', marginBottom: 6,
        }}>Limite free raggiunto</div>
        <div style={{
          fontFamily: 'var(--f-body)', fontSize: compact ? 11.5 : 12.5,
          color: 'var(--text-sec)', lineHeight: 1.6, maxWidth: 260,
        }}>
          Hai aggiunto <strong>3 giochi</strong>, il massimo del piano Free.
          Esegui l'upgrade per sbloccare giochi illimitati, agenti e KB avanzate.
        </div>
      </div>
      {/* Quota indicator */}
      <div style={{
        display: 'flex', gap: compact ? 6 : 8, alignItems: 'center',
        padding: compact ? '6px 14px' : '8px 16px',
        borderRadius: 'var(--r-pill)',
        background: 'var(--bg-sunken)',
        border: '1px solid var(--border)',
      }}>
        {[0, 1, 2].map(i => (
          <div key={i} style={{
            width: compact ? 18 : 22, height: compact ? 18 : 22,
            borderRadius: '50%',
            background: 'hsl(var(--c-game))',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: compact ? 9 : 10,
          }}>🎲</div>
        ))}
        <span style={{
          fontFamily: 'var(--f-mono)', fontSize: compact ? 10 : 11.5,
          color: 'var(--text-muted)', fontWeight: 700, marginLeft: 4,
        }}>3 / 3</span>
      </div>
      {/* Upgrade CTA */}
      <button style={{
        width: '100%',
        padding: compact ? '10px 0' : '12px 0',
        borderRadius: 'var(--r-md)',
        background: 'hsl(var(--c-game))',
        border: 'none', color: '#fff',
        fontFamily: 'var(--f-display)', fontSize: compact ? 12.5 : 14, fontWeight: 800,
        cursor: 'pointer',
        display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
        letterSpacing: '-.01em',
      }}>
        <span aria-hidden="true">🚀</span>
        Upgrade to Pro
      </button>
      <div style={{
        fontFamily: 'var(--f-body)', fontSize: compact ? 9.5 : 10.5,
        color: 'var(--text-muted)', lineHeight: 1.4,
      }}>
        Giochi illimitati · KB avanzata · Multi-agent AI
      </div>
    </div>
    <div style={{ flex: 1 }}/>
  </DrawerShell>
);

// ═══════════════════════════════════════════════════════
// ─── PHONE SHELL ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color: 'var(--text-sec)' }}>
    <span>9:41</span>
    <div className="ind">
      <span>●●●</span><span>WiFi</span><span>🔋</span>
    </div>
  </div>
);

const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
    <div style={{
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)',
      textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700,
    }}>{label}</div>
    <div className="phone">
      <PhoneSbar/>
      {/* Dimmed app chrome behind drawer */}
      <div style={{
        position: 'relative', flex: 1,
        background: 'var(--bg)',
        display: 'flex', flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'flex-end',
        overflow: 'hidden',
      }}>
        {/* Scrim */}
        <div style={{
          position: 'absolute', inset: 0,
          background: 'rgba(0,0,0,0.3)',
          zIndex: 1,
        }}/>
        {/* Bottom-sheet drawer positioned at bottom */}
        <div style={{
          position: 'relative', zIndex: 2,
          width: '100%',
          display: 'flex', justifyContent: 'center',
          paddingBottom: 0,
        }}>
          {children}
        </div>
      </div>
    </div>
    {desc && (
      <div style={{
        fontSize: 11, color: 'var(--text-muted)', maxWidth: 340,
        textAlign: 'center', lineHeight: 1.55,
      }}>{desc}</div>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── DESKTOP FRAME ───────────────────────────────────
// ═══════════════════════════════════════════════════════
const DesktopFrame = ({ label, desc, children }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, width: '100%' }}>
    <div style={{
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)',
      textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width: '100%', maxWidth: 1280,
      borderRadius: 'var(--r-xl)', border: '1px solid var(--border)',
      background: 'var(--bg)', overflow: 'hidden',
      boxShadow: 'var(--shadow-lg)',
      position: 'relative',
    }}>
      {/* Browser chrome */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '9px 14px',
        background: 'var(--bg-muted)',
        borderBottom: '1px solid var(--border)',
        fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>meepleai.app/library</span>
      </div>
      {/* Page body with modal overlay */}
      <div style={{
        minHeight: 520, display: 'flex', alignItems: 'center', justifyContent: 'center',
        padding: '32px 24px',
        background: 'var(--bg)',
        position: 'relative',
      }}>
        {/* Faux page bg */}
        <div style={{
          position: 'absolute', inset: 0,
          opacity: 0.3,
          background: 'repeating-linear-gradient(0deg, var(--border-light) 0, var(--border-light) 1px, transparent 1px, transparent 48px)',
          pointerEvents: 'none',
        }}/>
        {/* Scrim */}
        <div style={{
          position: 'absolute', inset: 0,
          background: 'rgba(0,0,0,0.22)',
          zIndex: 1,
        }}/>
        {/* Centered drawer */}
        <div style={{ position: 'relative', zIndex: 2, display: 'flex', justifyContent: 'center' }}>
          {children}
        </div>
      </div>
    </div>
    {desc && (
      <div style={{
        fontSize: 11, color: 'var(--text-muted)', maxWidth: 720,
        textAlign: 'center', lineHeight: 1.55,
      }}>{desc}</div>
    )}
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── THEME TOGGLE ────────────────────────────────────
// ═══════════════════════════════════════════════════════
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

// ═══════════════════════════════════════════════════════
// ─── MOBILE STATES CONFIG ────────────────────────────
// ═══════════════════════════════════════════════════════
const MOBILE_STATES = [
  {
    id: 'empty',
    label: '01 · Empty',
    desc: 'Stato iniziale: illustrazione, hint query, note rate-limit 60 req/h.',
    component: <BggStepEmpty compact/>,
  },
  {
    id: 'loading',
    label: '02 · Loading',
    desc: '5 skeleton cards + banner "BGG può essere lento" + pulsante Annulla.',
    component: <BggStepLoading compact/>,
  },
  {
    id: 'results',
    label: '03 · Results',
    desc: '6 result cards: thumbnail BGG-ID, titolo, anno, giocatori, peso. CTA "Aggiungi" per ognuno.',
    component: <BggStepResults compact/>,
  },
  {
    id: 'throttled',
    label: '04 · Rate-limited',
    desc: 'HTTP 202: banner giallo ⚠️ + countdown "23s" prominente + bottone Riprova + risultati dimmer.',
    component: <BggStepThrottled compact/>,
  },
  {
    id: 'redirect',
    label: '05 · Già nel catalogo',
    desc: 'Auto-redirect: game già in SharedGameCatalog. 2 CTA: "Vai al catalogo community" (primary) / "Crea copia privata" (ghost).',
    component: <BggStepAutoRedirect compact/>,
  },
  {
    id: 'tier-lock',
    label: '06 · Tier quota',
    desc: 'Lock 🔒: limite 3 giochi piano Free. Quota indicator 3/3 + CTA "Upgrade to Pro".',
    component: <BggStepTierLock compact/>,
  },
];

// ═══════════════════════════════════════════════════════
// ─── ROOT APP ────────────────────────────────────────
// ═══════════════════════════════════════════════════════
function App() {
  return (
    <div className="stage">
      <ThemeToggle/>
      <div className="stage-wrap">
        {/* Title strip */}
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 6,
        }}>Mockup · sp4-add-game-bgg-step · #911</div>
        <h1>AddGameDrawer — BGG Search Step</h1>
        <p className="lead">
          Step "Da BGG" del drawer <strong>AddGameDrawer</strong> (tab 2/3 accanto a Manuale e Catalogo).
          Entità: <span style={{ color: 'hsl(var(--c-game))', fontWeight: 700 }}>🎲 Game</span>.
          6 stati: empty · loading · results · rate-limited (HTTP 202) · auto-redirect · tier-quota lock.
          Light + dark · mobile 375 + desktop 1280.
        </p>

        {/* ─── MOBILE · 6 stati ─── */}
        <div className="section-label">Mobile · 375 — 6 stati</div>
        <div style={{
          display: 'flex', flexWrap: 'wrap', gap: 28,
          justifyContent: 'flex-start', alignItems: 'flex-start',
        }}>
          {MOBILE_STATES.map(s => (
            <PhoneShell key={s.id} label={s.label} desc={s.desc}>
              {s.component}
            </PhoneShell>
          ))}
        </div>

        {/* ─── DESKTOP · 6 stati ─── */}
        <div className="section-label">Desktop · 1280 — 6 stati (modal centered)</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 36 }}>

          <DesktopFrame
            label="07 · Desktop · State 1 — Empty"
            desc="Drawer centrato sopra scrim. Illustrazione dashed-circle, chip query example, nota rate-limit discreta in bg-sunken.">
            <BggStepEmpty/>
          </DesktopFrame>

          <DesktopFrame
            label="08 · Desktop · State 2 — Loading"
            desc="5 skeleton shimmer + banner amber 'BGG può essere lento…' + pulsante Annulla a destra del banner.">
            <BggStepLoading/>
          </DesktopFrame>

          <DesktopFrame
            label="09 · Desktop · State 3 — Results"
            desc="6 card: thumbnail colorata (sfondo hex BGG), titolo Quicksand 15px bold, metadati Nunito 11px, pill peso colorato. CTA 'Aggiungi alla libreria' per ogni riga.">
            <BggStepResults/>
          </DesktopFrame>

          <DesktopFrame
            label="10 · Desktop · State 4 — BGG Rate-limited (HTTP 202)"
            desc="Banner prominente --c-event (rose) ⚠️. Countdown '23s' in pill monospaciata. Bottoni Riprova (primary event) + Annulla (ghost). Risultati precedenti dimmed 35%.">
            <BggStepThrottled/>
          </DesktopFrame>

          <DesktopFrame
            label="11 · Desktop · State 5 — Auto-redirect (già nel catalogo)"
            desc="Card inline con info gioco + badge 'Scheda community'. 2 CTA distinte: 'Vai al catalogo community' (primary --c-game) e 'Crea copia privata comunque' (ghost border-strong).">
            <BggStepAutoRedirect/>
          </DesktopFrame>

          <DesktopFrame
            label="12 · Desktop · State 6 — Tier-quota lock"
            desc="Overlay lock 🔒 con gradient --c-game muted. Quota indicator 3/3 (cerchietti emoji 🎲). CTA 'Upgrade to Pro' full-width --c-game bold. Risultati ghost opacity 18%.">
            <BggStepTierLock/>
          </DesktopFrame>

        </div>

        {/* Footer */}
        <div style={{
          marginTop: 64, paddingTop: 24,
          borderTop: '1px solid var(--border)',
          display: 'flex', alignItems: 'center', justifyContent: 'space-between',
          flexWrap: 'wrap', gap: 12,
        }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 10,
            color: 'var(--text-muted)',
          }}>
            sp4-add-game-bgg-step.html · issue <strong style={{ color: 'hsl(var(--c-game))' }}>#911</strong>
            {' · '}2026-05-09
          </div>
          <a href="00-hub.html" style={{
            padding: '6px 14px', borderRadius: 'var(--r-md)',
            background: 'hsl(var(--c-game) / 0.10)',
            border: '1px solid hsl(var(--c-game) / 0.25)',
            color: 'hsl(var(--c-game))',
            fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700,
            textDecoration: 'none',
            display: 'inline-flex', alignItems: 'center', gap: 6,
          }}>
            <span aria-hidden="true">←</span> Torna a 00-hub.html
          </a>
        </div>

      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
