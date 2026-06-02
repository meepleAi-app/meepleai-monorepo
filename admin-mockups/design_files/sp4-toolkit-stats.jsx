/* MeepleAI SP4 wave 4 — S · #1490 · 1/4 — sp4-toolkit-stats
   Route: /toolkit/stats — KPI dashboard analytics sessioni cross-game con monthly activity + top games + score trends
   File: admin-mockups/design_files/sp4-toolkit-stats.{html,jsx}
   Pattern: Hero + body verticale stacked (no split, no sidebar). Palette --c-toolkit (verde, dashboard toolkit cross-game).
   Sezioni: Header sticky · KPI grid (3) · Most Played Games (top-N) · Monthly Activity (bar chart) · Recent Score Trends (top-N).

   Source restyle (NO ridisegnare logica): apps/web/src/app/(authenticated)/toolkit/stats/client.tsx
   API: api.sessionStatistics.getStatistics(12) → { totalSessions, totalGamesPlayed, averageSessionDuration,
        mostPlayedGames[], monthlyActivity[], recentScoreTrends[] }

   8 stati (state picker continuity con cluster #1489, persistito localStorage `ts-state`):
   default · empty-no-sessions · empty-partial-data · loading · error · range-3m · range-all · mobile-stack

   Deviazioni flaggate: nessuna palette nuova — solo tint di entity esistenti (toolkit/game/success/warning/danger/info).
*/

const { useState, useEffect, useMemo, useRef } = React;
const DS = window.DS;

const eHsl = (type, a) => {
  const c = DS.EC[type] || DS.EC.toolkit;
  return a !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${a})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};
// semantic via CSS var (theme-aware)
const sem = (name, a) => a !== undefined ? `hsl(var(--c-${name}) / ${a})` : `hsl(var(--c-${name}))`;

// ═══════════════════════════════════════════════════════
// ─── FIXTURE (dati realistici, cross-ref data.js) ────
// ═══════════════════════════════════════════════════════
const MONTHS_SHORT = ['Gen','Feb','Mar','Apr','Mag','Giu','Lug','Ago','Set','Ott','Nov','Dic'];
const MONTHS_FULL = ['Gennaio','Febbraio','Marzo','Aprile','Maggio','Giugno','Luglio','Agosto','Settembre','Ottobre','Novembre','Dicembre'];

// 24 mesi reali Lug 2024 → Giu 2026 (recenti 12 = vista default; 24 = "Tutto")
const COUNTS_24 = [1,0,2,1,3,2,4,2,3,1,2,3,  3,1,4,5,3,6,5,4,7,4,3,2];
function buildMonthly() {
  const out = [];
  // start: Lug 2024 (month index 6, year 2024) → 24 entries ending Giu 2026 (index 5, 2026)
  let mi = 6, yr = 2024;
  for (let i = 0; i < 24; i++) {
    const sessions = COUNTS_24[i];
    const minutes = sessions ? Math.round(sessions * 82 + (i % 3) * 11) : 0;
    out.push({
      key: `${yr}-${String(mi + 1).padStart(2, '0')}`,
      short: MONTHS_SHORT[mi], full: MONTHS_FULL[mi], year: yr,
      sessions, minutes,
    });
    mi++; if (mi > 11) { mi = 0; yr++; }
  }
  return out;
}
const MONTHLY_24 = buildMonthly();
// current month = Giu 2026 (ultimo): in corso
const CURRENT_KEY = '2026-06';

const MOST_PLAYED_FULL = [
  { gameId: 'g-catan',    plays: 12 },
  { gameId: 'g-wingspan', plays: 9 },
  { gameId: 'g-azul',     plays: 8 },
  { gameId: 'g-7wonders', plays: 7 },
  { gameId: 'g-brass',    plays: 5 },
  { gameId: 'g-arknova',  plays: 3 },
  { gameId: 'g-spirit',   plays: 2 },
];

// variance = score − avgScore del gioco (data.js)
const SCORE_TRENDS_FULL = [
  { gameId: 'g-wingspan', date: '1 giu 2026',  full: '1 giugno 2026 · 21:14',  score: 92,  variance: 3 },
  { gameId: 'g-catan',    date: '30 mag 2026', full: '30 maggio 2026 · 20:02', score: 11,  variance: 2 },
  { gameId: 'g-azul',     date: '28 mag 2026', full: '28 maggio 2026 · 22:40', score: 87,  variance: 15 },
  { gameId: 'g-7wonders', date: '26 mag 2026', full: '26 maggio 2026 · 19:30', score: 58,  variance: -6 },
  { gameId: 'g-brass',    date: '24 mag 2026', full: '24 maggio 2026 · 23:05', score: 132, variance: 0 },
  { gameId: 'g-wingspan', date: '21 mag 2026', full: '21 maggio 2026 · 18:50', score: 76,  variance: -13 },
  { gameId: 'g-azul',     date: '18 mag 2026', full: '18 maggio 2026 · 21:20', score: 81,  variance: 9 },
  { gameId: 'g-catan',    date: '15 mag 2026', full: '15 maggio 2026 · 20:45', score: 8,   variance: -1 },
  { gameId: 'g-arknova',  date: '12 mag 2026', full: '12 maggio 2026 · 22:10', score: 124, variance: 6 },
  { gameId: 'g-7wonders', date: '9 mag 2026',  full: '9 maggio 2026 · 19:00',  score: 71,  variance: 7 },
];

const RANGE_LABEL = { '3': 'Ultimi 3 mesi', '6': 'Ultimi 6 mesi', '12': 'Ultimi 12 mesi', 'all': 'Tutto' };
const RANGE_OPTS = [
  { id: '3', label: '3 mesi' }, { id: '6', label: '6 mesi' },
  { id: '12', label: '12 mesi' }, { id: 'all', label: 'Tutto' },
];

function fmtDuration(min) {
  const h = Math.floor(min / 60), m = min % 60;
  if (h && m) return `${h}h ${m}min`;
  if (h) return `${h}h`;
  return `${m}min`;
}

// ═══════════════════════════════════════════════════════
// ─── PRIMITIVES ─────────────────────────────────────
// ═══════════════════════════════════════════════════════
const EntityChip = ({ gameId, compact, truncate }) => {
  const g = DS.byId[gameId];
  if (!g) return null;
  return (
    <button type="button" title={`Apri ${g.title} in libreria`}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 5,
        maxWidth: '100%', overflow: 'hidden',
        padding: compact ? '2px 8px' : '3px 9px', borderRadius: 'var(--r-pill)',
        background: eHsl('game', 0.12), color: eHsl('game'),
        border: `1px solid ${eHsl('game', 0.22)}`,
        fontFamily: 'var(--f-display)', fontSize: compact ? 11 : 12, fontWeight: 800,
        cursor: 'pointer', whiteSpace: 'nowrap',
      }}>
      <span aria-hidden="true">🎲</span>
      <span style={truncate ? { overflow: 'hidden', textOverflow: 'ellipsis', minWidth: 0 } : undefined}>{g.title}</span>
    </button>
  );
};

const TrendChip = ({ dir, children }) => {
  const cfg = dir === 'up'
    ? { bg: sem('success', 0.14), fg: sem('success'), icon: '📈' }
    : dir === 'down'
      ? { bg: sem('danger', 0.14), fg: sem('danger'), icon: '📉' }
      : { bg: 'var(--bg-muted)', fg: 'var(--text-muted)', icon: '▶' };
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      padding: '3px 8px', borderRadius: 'var(--r-pill)',
      background: cfg.bg, color: cfg.fg,
      fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
      fontVariantNumeric: 'tabular-nums', whiteSpace: 'nowrap',
    }}>
      <span aria-hidden="true">{cfg.icon}</span>{children}
    </span>
  );
};

const SectionHead = ({ title, link, onLink, accent = 'toolkit', children }) => (
  <div style={{
    display: 'flex', alignItems: 'center', justifyContent: 'space-between',
    gap: 12, marginBottom: 14, flexWrap: 'wrap',
  }}>
    <h2 style={{
      fontFamily: 'var(--f-display)', fontSize: 17, fontWeight: 800,
      color: 'var(--text)', margin: 0,
    }}>{title}</h2>
    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
      {children}
      {link && (
        <button type="button" onClick={onLink} style={{
          background: 'transparent', border: 'none', cursor: 'pointer',
          color: eHsl(accent), fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800,
          padding: '2px 2px',
        }}>{link} →</button>
      )}
    </div>
  </div>
);

const Card = ({ children, pad = 20, style }) => (
  <div style={{
    background: 'var(--bg-card)', border: '1px solid var(--border-light)',
    borderRadius: 'var(--r-lg)', padding: pad, boxShadow: 'var(--shadow-sm)',
    ...style,
  }}>{children}</div>
);

// ═══════════════════════════════════════════════════════
// ─── HEADER ─────────────────────────────────────────
// ═══════════════════════════════════════════════════════
const Header = ({ compact, range, onRange }) => {
  const [open, setOpen] = useState(false);
  const ref = useRef(null);
  useEffect(() => {
    const onDoc = (e) => { if (ref.current && !ref.current.contains(e.target)) setOpen(false); };
    const onEsc = (e) => { if (e.key === 'Escape') setOpen(false); };
    document.addEventListener('mousedown', onDoc);
    document.addEventListener('keydown', onEsc);
    return () => { document.removeEventListener('mousedown', onDoc); document.removeEventListener('keydown', onEsc); };
  }, []);
  return (
    <div style={{
      position: 'sticky', top: 0, zIndex: 10,
      background: 'var(--glass-bg)', backdropFilter: 'blur(14px)',
      borderBottom: '1px solid var(--border)',
      padding: compact ? '14px 16px' : '22px 28px',
    }}>
      <div style={{
        display: 'flex', alignItems: 'flex-end', justifyContent: 'space-between',
        gap: 16, flexWrap: 'wrap',
      }}>
        <div style={{ minWidth: 0 }}>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
            letterSpacing: '.04em', marginBottom: 6,
          }}>
            <span>Toolkit</span>
            <span aria-hidden="true" style={{ margin: '0 6px' }}>›</span>
            <strong style={{ color: 'var(--text-sec)' }}>Statistiche</strong>
          </div>
          <div style={{
            display: 'flex', alignItems: compact ? 'flex-start' : 'center', gap: 9, marginBottom: 4,
          }}>
            <span aria-hidden="true" style={{
              width: compact ? 30 : 36, height: compact ? 30 : 36, flexShrink: 0,
              borderRadius: 'var(--r-md)', background: eHsl('toolkit', 0.16), color: eHsl('toolkit'),
              display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
              fontSize: compact ? 17 : 20,
            }}>🧰</span>
            <h1 style={{
              fontFamily: 'var(--f-display)', fontWeight: 800,
              fontSize: compact ? 21 : 32, letterSpacing: '-.02em',
              color: 'var(--text)', margin: 0,
              whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
            }}>
              Analytics sessioni
            </h1>
          </div>
          <p style={{
            color: 'var(--text-sec)', fontSize: compact ? 13 : 15, margin: 0,
            display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap',
          }}>
            Attività di gioco degli ultimi 12 mesi
            <span aria-hidden="true" style={{ color: 'var(--text-muted)' }}>·</span>
            <span style={{
              display: 'inline-flex', alignItems: 'center', gap: 5,
              fontFamily: 'var(--f-mono)', fontSize: 11, color: eHsl('player'),
            }}><span aria-hidden="true">👤</span>Marco R.</span>
          </p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
          {/* Time-range pill + dropdown */}
          <div ref={ref} style={{ position: 'relative' }}>
            <button type="button" role="combobox" aria-expanded={open} aria-haspopup="listbox"
              aria-label={`Intervallo temporale: ${RANGE_LABEL[range]}`}
              onClick={() => setOpen(o => !o)}
              style={{
                display: 'inline-flex', alignItems: 'center', gap: 8,
                padding: '8px 12px', borderRadius: 'var(--r-pill)',
                background: eHsl('toolkit', 0.14), border: `1px solid ${eHsl('toolkit', 0.35)}`,
                color: eHsl('toolkit'), fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800,
                cursor: 'pointer', whiteSpace: 'nowrap',
              }}>
              <span aria-hidden="true">🗓</span>{RANGE_LABEL[range]}
              <span aria-hidden="true" style={{ fontSize: 9, opacity: .8 }}>▼</span>
            </button>
            {open && (
              <div role="listbox" style={{
                position: 'absolute', top: 'calc(100% + 6px)', right: 0, zIndex: 20,
                background: 'var(--bg-card)', border: '1px solid var(--border)',
                borderRadius: 'var(--r-md)', boxShadow: 'var(--shadow-lg)',
                padding: 5, minWidth: 150,
              }}>
                {RANGE_OPTS.map(o => (
                  <button key={o.id} type="button" role="option" aria-selected={range === o.id}
                    onClick={() => { onRange(o.id); setOpen(false); }}
                    style={{
                      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                      width: '100%', padding: '8px 10px', borderRadius: 'var(--r-sm)',
                      background: range === o.id ? eHsl('toolkit', 0.12) : 'transparent',
                      border: 'none', cursor: 'pointer',
                      color: range === o.id ? eHsl('toolkit') : 'var(--text-sec)',
                      fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700, textAlign: 'left',
                    }}>
                    {o.label}{range === o.id && <span aria-hidden="true">✓</span>}
                  </button>
                ))}
              </div>
            )}
          </div>
          {/* Export CSV */}
          <button type="button" style={{
            display: 'inline-flex', alignItems: 'center', gap: 6,
            padding: '8px 14px', borderRadius: 'var(--r-md)',
            background: 'var(--bg-card)', border: '1px solid var(--border-strong)',
            color: 'var(--text)', fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800,
            cursor: 'pointer', whiteSpace: 'nowrap',
          }}><span aria-hidden="true">📊</span>{compact ? 'CSV' : 'Esporta CSV'}</button>
        </div>
      </div>
    </div>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SECTION 1 · KPI CARDS ──────────────────────────
// ═══════════════════════════════════════════════════════
const KpiCard = ({ icon, accent, label, value, mono, trend, sub, labelId }) => (
  <figure role="figure" aria-labelledby={labelId} className="ts-kpi" style={{
    margin: 0, padding: 20, borderRadius: 'var(--r-lg)',
    background: 'var(--glass-bg)', backdropFilter: 'blur(8px)',
    border: '1px solid var(--border-light)', boxShadow: 'var(--shadow-sm)',
    display: 'flex', flexDirection: 'column', gap: 12,
  }}>
    <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
      <span aria-hidden="true" style={{
        width: 26, height: 26, borderRadius: 'var(--r-sm)',
        background: eHsl(accent, 0.16), color: eHsl(accent),
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 14,
      }}>{icon}</span>
      <figcaption id={labelId} style={{
        fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
        textTransform: 'uppercase', letterSpacing: '.07em', fontWeight: 700,
      }}>{label}</figcaption>
    </div>
    <div style={{
      fontFamily: mono ? 'var(--f-mono)' : 'var(--f-display)',
      fontSize: mono ? 30 : 40, fontWeight: 800, color: 'var(--text)',
      fontVariantNumeric: 'tabular-nums', lineHeight: 1,
    }}>{value}</div>
    <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', minHeight: 22 }}>
      {trend && <TrendChip dir={trend.dir}>{trend.text}</TrendChip>}
      {sub && <span style={{
        fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', fontWeight: 600,
      }}>{sub}</span>}
    </div>
  </figure>
);

const KpiGrid = ({ compact, data }) => (
  <div style={{
    display: 'grid', gap: 14,
    gridTemplateColumns: compact ? '1fr' : 'repeat(3, 1fr)',
  }}>
    <KpiCard labelId="kpi-sessions" icon="🎮" accent="toolkit" label="Sessioni totali"
      value={data.totalSessions}
      trend={{ dir: 'up', text: `+${data.yoy.delta} vs anno scorso · +${data.yoy.pct}%` }} />
    <KpiCard labelId="kpi-games" icon="🏆" accent="game" label="Giochi giocati"
      value={data.totalGamesPlayed}
      sub={`Mediamente ${Math.round(data.totalSessions / Math.max(1, data.totalGamesPlayed))} sessioni / gioco`} />
    <KpiCard labelId="kpi-duration" icon="⏱" accent="warning" label="Durata media" mono
      value={data.avgDuration}
      sub={`Più lunga: ${data.longestSession}`} />
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── SECTION 2 · MOST PLAYED ────────────────────────
// ═══════════════════════════════════════════════════════
const RANK_COLOR = ['#d9a441', '#9aa0a6', '#b06a3c']; // amber · silver · bronze
const MostPlayedRow = ({ row, idx, max }) => {
  const g = DS.byId[row.gameId];
  if (!g) return null;
  const pct = Math.round((row.plays / max) * 100);
  return (
    <div tabIndex={0} role="listitem" className="ts-row" title={`Apri ${g.title}`} style={{
      display: 'flex', flexDirection: 'column', gap: 9,
      padding: '12px 12px', borderRadius: 'var(--r-md)', cursor: 'pointer',
    }}>
      {/* top line: rank · cover · name (full) */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, minWidth: 0 }}>
        <span style={{
          fontFamily: 'var(--f-mono)', fontSize: 13, fontWeight: 800, width: 24, textAlign: 'center', flexShrink: 0,
          color: idx < 3 ? RANK_COLOR[idx] : 'var(--text-muted)',
        }}>#{idx + 1}</span>
        <span aria-hidden="true" style={{
          width: 28, height: 34, borderRadius: 'var(--r-sm)', background: g.cover,
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 15,
          color: 'rgba(255,255,255,.92)', flexShrink: 0,
        }}>{g.coverEmoji}</span>
        <span style={{ minWidth: 0 }}><EntityChip gameId={row.gameId} /></span>
      </div>
      {/* bottom line: progress bar + play count */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, paddingLeft: 34 }}>
        <div style={{ flex: 1, height: 6, borderRadius: 'var(--r-pill)', background: 'var(--bg-muted)', overflow: 'hidden' }}>
          <div style={{
            height: '100%', width: `${pct}%`, borderRadius: 'var(--r-pill)',
            background: `linear-gradient(90deg, ${eHsl('game', 0.7)}, ${eHsl('game')})`,
          }} />
        </div>
        <span style={{
          display: 'inline-flex', alignItems: 'center', gap: 5, flexShrink: 0,
          padding: '4px 9px', borderRadius: 'var(--r-pill)',
          background: eHsl('game', 0.12), color: eHsl('game'),
          fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700, whiteSpace: 'nowrap',
        }}><span aria-hidden="true">📊</span>{row.plays} plays</span>
      </div>
    </div>
  );
};

const MostPlayed = ({ rows }) => {
  const max = rows.length ? rows[0].plays : 1;
  return (
    <Card pad={16}>
      <SectionHead title="Giochi più giocati" link="Vedi tutti" accent="game" onLink={() => {}} />
      <div role="list" style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        {rows.map((r, i) => <MostPlayedRow key={r.gameId + i} row={r} idx={i} max={max} />)}
      </div>
    </Card>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SECTION 3 · MONTHLY ACTIVITY (bar chart) ───────
// ═══════════════════════════════════════════════════════
const MonthlyChart = ({ months, compact }) => {
  const [metric, setMetric] = useState('sessions');
  const [hover, setHover] = useState(null);
  const chartH = compact ? 140 : 200;
  const valOf = (m) => metric === 'sessions' ? m.sessions : m.minutes;
  const max = Math.max(1, ...months.map(valOf));
  const scroll = months.length > 14;

  return (
    <Card pad={compact ? 16 : 20}>
      <SectionHead title="Attività mensile">
        <div role="group" aria-label="Metrica grafico" style={{
          display: 'flex', gap: 2, padding: 3, borderRadius: 'var(--r-pill)',
          background: 'var(--bg-muted)',
        }}>
          {[{ id: 'sessions', l: 'Sessioni' }, { id: 'duration', l: 'Durata' }].map(o => (
            <button key={o.id} type="button" aria-pressed={metric === o.id}
              onClick={() => setMetric(o.id)} style={{
                padding: '5px 12px', borderRadius: 'var(--r-pill)', border: 'none', cursor: 'pointer',
                background: metric === o.id ? 'var(--bg-card)' : 'transparent',
                boxShadow: metric === o.id ? 'var(--shadow-xs)' : 'none',
                color: metric === o.id ? eHsl('toolkit') : 'var(--text-muted)',
                fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800,
              }}>{o.l}</button>
          ))}
        </div>
      </SectionHead>

      <div className={scroll ? 'ts-scroll' : ''} style={{ overflowX: scroll ? 'auto' : 'visible', paddingTop: 22 }}>
        <div role="img"
          aria-label={`Grafico a barre attività mensile, ${months.length} mesi. Metrica: ${metric === 'sessions' ? 'numero sessioni' : 'durata totale'}.`}
          style={{
            display: 'grid',
            gridTemplateColumns: `repeat(${months.length}, ${scroll ? '34px' : 'minmax(0, 1fr)'})`,
            alignItems: 'end', gap: scroll ? 6 : 8, height: chartH,
            borderBottom: '1px solid var(--border)',
          }}>
          {months.map((m) => {
            const v = valOf(m);
            const h = v ? Math.max(4, Math.round((v / max) * (chartH - 26))) : 0;
            const isCurrent = m.key === CURRENT_KEY;
            const isHover = hover === m.key;
            const showTop = h > 30;
            return (
              <div key={m.key} style={{ position: 'relative', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'flex-end', height: '100%' }}
                onMouseEnter={() => setHover(m.key)} onMouseLeave={() => setHover(null)}>
                {/* tooltip */}
                {isHover && v > 0 && (
                  <div role="tooltip" style={{
                    position: 'absolute', bottom: h + 30, left: '50%', transform: 'translateX(-50%)',
                    zIndex: 30, background: 'var(--bg-card)', border: '1px solid var(--border)',
                    borderRadius: 'var(--r-md)', boxShadow: 'var(--shadow-lg)', padding: '8px 11px',
                    whiteSpace: 'nowrap', pointerEvents: 'none',
                  }}>
                    <div style={{ fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, color: 'var(--text)' }}>{m.full} {m.year}</div>
                    <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>
                      {m.sessions} sessioni · {fmtDuration(m.minutes)} totali
                    </div>
                  </div>
                )}
                {showTop && (
                  <span style={{
                    fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700, marginBottom: 4,
                    color: isHover ? eHsl('toolkit') : 'var(--text-sec)', fontVariantNumeric: 'tabular-nums',
                  }}>{metric === 'sessions' ? v : `${Math.floor(v / 60)}h`}</span>
                )}
                <button type="button" tabIndex={0}
                  aria-label={`${m.full} ${m.year}: ${m.sessions} sessioni, ${fmtDuration(m.minutes)}`}
                  className={`ts-bar ${isCurrent ? 'ts-pulse' : ''}`}
                  style={{
                    width: '100%', maxWidth: scroll ? 34 : 46, height: h || 4, cursor: 'pointer',
                    border: 'none', padding: 0,
                    borderRadius: 'var(--r-sm) var(--r-sm) 0 0',
                    background: v
                      ? `linear-gradient(to bottom, ${eHsl('toolkit')}, ${eHsl('toolkit', 0.55)})`
                      : 'transparent',
                    borderTop: v
                      ? (isHover ? `2px solid ${eHsl('toolkit')}` : 'none')
                      : `1.5px dashed ${eHsl('toolkit', 0.4)}`,
                    boxShadow: isCurrent ? 'none' : (isHover ? `0 2px 10px ${eHsl('toolkit', 0.3)}` : 'none'),
                  }} />
                <span style={{
                  fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: isCurrent ? 800 : 600, marginTop: 7,
                  color: isCurrent ? eHsl('toolkit') : 'var(--text-muted)',
                }}>{m.short}</span>
              </div>
            );
          })}
        </div>
      </div>

      {/* a11y: raw data table */}
      <table className="ts-sr-only">
        <caption>Attività mensile per sessioni e durata</caption>
        <thead><tr><th>Mese</th><th>Sessioni</th><th>Durata totale</th></tr></thead>
        <tbody>
          {months.map(m => (
            <tr key={m.key}><td>{m.full} {m.year}</td><td>{m.sessions}</td><td>{fmtDuration(m.minutes)}</td></tr>
          ))}
        </tbody>
      </table>
    </Card>
  );
};

// ═══════════════════════════════════════════════════════
// ─── SECTION 4 · RECENT SCORE TRENDS ────────────────
// ═══════════════════════════════════════════════════════
const ScoreRow = ({ row, compact }) => {
  const g = DS.byId[row.gameId];
  if (!g) return null;
  const dir = row.variance > 0 ? 'up' : row.variance < 0 ? 'down' : 'neutral';
  const varCfg = dir === 'up'
    ? { icon: '🔺', color: sem('success'), text: `+${row.variance} vs media gioco`, aria: `Positivo, +${row.variance} punti rispetto alla media del gioco` }
    : dir === 'down'
      ? { icon: '🔻', color: sem('danger'), text: `${row.variance} vs media gioco`, aria: `Negativo, ${row.variance} punti rispetto alla media del gioco` }
      : { icon: '▶', color: 'var(--text-muted)', text: 'in media', aria: 'In media rispetto al gioco' };
  return (
    <div role="listitem" tabIndex={0} className="ts-row" style={{
      display: 'flex', alignItems: 'center', gap: 12, padding: '11px 12px',
      borderRadius: 'var(--r-md)', cursor: 'pointer',
    }}>
      <span aria-hidden="true" style={{
        width: 30, height: 30, borderRadius: 'var(--r-sm)', flexShrink: 0,
        background: dir === 'up' ? sem('success', 0.14) : dir === 'down' ? sem('danger', 0.14) : 'var(--bg-muted)',
        color: varCfg.color, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 13,
      }}>{dir === 'up' ? '📈' : dir === 'down' ? '📉' : '▶'}</span>
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 4 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <EntityChip gameId={row.gameId} compact />
          <span title={row.full} style={{
            fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', fontWeight: 600,
          }}>{row.date}</span>
        </div>
        {!compact && (
          <span aria-label={varCfg.aria} style={{
            display: 'inline-flex', alignItems: 'center', gap: 5,
            fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700, color: varCfg.color,
          }}><span aria-hidden="true">{varCfg.icon}</span>{varCfg.text}</span>
        )}
      </div>
      <span style={{
        padding: '6px 12px', borderRadius: 'var(--r-md)',
        background: eHsl('toolkit', 0.14), color: eHsl('toolkit'),
        fontFamily: 'var(--f-mono)', fontSize: 15, fontWeight: 800, fontVariantNumeric: 'tabular-nums',
        whiteSpace: 'nowrap',
      }}>{row.score}</span>
    </div>
  );
};

const ScoreTrends = ({ rows, compact }) => (
  <Card pad={16}>
    <SectionHead title="Punteggi recenti" link="Vedi history" accent="toolkit" onLink={() => {}} />
    <div role="list" style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {rows.map((r, i) => <ScoreRow key={r.gameId + i} row={r} compact={compact} />)}
    </div>
  </Card>
);

// ═══════════════════════════════════════════════════════
// ─── STATE BLOCKS (empty / partial / loading / error) ─
// ═══════════════════════════════════════════════════════
const Banner = ({ tone, icon, children, action }) => {
  const map = {
    info: { bg: sem('info', 0.08), bd: sem('info', 0.3), fg: sem('info') },
    danger: { bg: sem('danger', 0.08), bd: sem('danger', 0.3), fg: sem('danger') },
  }[tone];
  return (
    <div role={tone === 'danger' ? 'alert' : undefined} style={{
      display: 'flex', alignItems: 'center', gap: 12, padding: '14px 16px',
      borderRadius: 'var(--r-lg)', background: map.bg, border: `1px solid ${map.bd}`,
    }}>
      <span aria-hidden="true" style={{ fontSize: 18, color: map.fg }}>{icon}</span>
      <div style={{ flex: 1, fontSize: 13.5, color: 'var(--text)', fontWeight: 600 }}>{children}</div>
      {action}
    </div>
  );
};

const EmptyNoSessions = () => (
  <div style={{
    gridColumn: '1 / -1', padding: '56px 24px', textAlign: 'center',
    background: 'var(--bg-card)', border: '1px dashed var(--border-strong)',
    borderRadius: 'var(--r-xl)', display: 'flex', flexDirection: 'column', alignItems: 'center',
  }}>
    <div aria-hidden="true" style={{
      width: 72, height: 72, borderRadius: '50%', marginBottom: 18,
      background: eHsl('toolkit', 0.12), color: eHsl('toolkit'),
      display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 32,
    }}>📊</div>
    <h2 style={{ fontFamily: 'var(--f-display)', fontSize: 20, fontWeight: 800, color: 'var(--text)', margin: '0 0 8px' }}>
      Nessuna sessione ancora
    </h2>
    <p style={{ fontSize: 14, color: 'var(--text-muted)', maxWidth: 380, margin: '0 0 20px', lineHeight: 1.55 }}>
      Inizia a giocare per vedere le tue statistiche: KPI, giochi più giocati, attività mensile e andamento punteggi.
    </p>
    <button type="button" style={{
      display: 'inline-flex', alignItems: 'center', gap: 7,
      padding: '11px 20px', borderRadius: 'var(--r-md)',
      background: eHsl('toolkit'), color: '#fff', border: 'none',
      fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 800, cursor: 'pointer',
      boxShadow: `0 4px 14px ${eHsl('toolkit', 0.4)}`,
    }}><span aria-hidden="true">🎮</span>Crea nuova sessione</button>
  </div>
);

const Skel = ({ h, w = '100%', r = 'var(--r-md)', style }) => (
  <div className="ts-shimmer" style={{ height: h, width: w, borderRadius: r, ...style }} />
);

const LoadingState = ({ compact }) => (
  <div aria-busy="true" aria-label="Caricamento statistiche…" style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>
    <div style={{ display: 'grid', gap: 14, gridTemplateColumns: compact ? '1fr' : 'repeat(3, 1fr)' }}>
      {[0, 1, 2].map(i => (
        <Card key={i} pad={20}><div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <Skel h={18} w="55%" /><Skel h={36} w="40%" /><Skel h={14} w="70%" />
        </div></Card>
      ))}
    </div>
    <Card pad={16}><Skel h={18} w="40%" style={{ marginBottom: 16 }} />
      {[0, 1, 2, 3, 4].map(i => <Skel key={i} h={20} style={{ marginBottom: 12 }} />)}
    </Card>
    <Card pad={20}><Skel h={18} w="35%" style={{ marginBottom: 18 }} />
      <div style={{ display: 'grid', gridTemplateColumns: `repeat(12, 1fr)`, alignItems: 'end', gap: 8, height: compact ? 140 : 200 }}>
        {Array.from({ length: 12 }).map((_, i) => <Skel key={i} h={`${20 + (i * 13) % 80}%`} r="var(--r-sm) var(--r-sm) 0 0" />)}
      </div>
    </Card>
    <Card pad={16}><Skel h={18} w="40%" style={{ marginBottom: 16 }} />
      {[0, 1, 2, 3, 4].map(i => <Skel key={i} h={22} style={{ marginBottom: 12 }} />)}
    </Card>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── BODY (compose) ─────────────────────────────────
// ═══════════════════════════════════════════════════════
const KPI_DEFAULT = {
  totalSessions: 47, totalGamesPlayed: 7, avgDuration: '1h 24m', longestSession: '4h 12m',
  yoy: { delta: 12, pct: 34 },
};
const KPI_PARTIAL = {
  totalSessions: 2, totalGamesPlayed: 1, avgDuration: '46m', longestSession: '52m',
  yoy: { delta: 2, pct: 100 },
};

const StatsBody = ({ state, range, onRange, compact }) => {
  // derive data slices from range
  const months = useMemo(() => {
    if (state === 'partial') return MONTHLY_24.slice(-12).map((m, i) => ({ ...m, sessions: i >= 10 ? [1, 1][i - 10] : 0, minutes: i >= 10 ? [42, 50][i - 10] : 0 }));
    if (range === 'all') return MONTHLY_24;
    const n = range === '3' ? 3 : range === '6' ? 6 : 12;
    return MONTHLY_24.slice(-n);
  }, [range, state]);

  const mostPlayed = useMemo(() => {
    if (state === 'partial') return MOST_PLAYED_FULL.slice(0, 1).map(r => ({ ...r, plays: 2 }));
    if (range === 'all') return MOST_PLAYED_FULL.slice(0, 7);
    if (range === '3') return MOST_PLAYED_FULL.slice(0, 3);
    return MOST_PLAYED_FULL.slice(0, 5);
  }, [range, state]);

  const trends = useMemo(() => {
    if (state === 'partial') return SCORE_TRENDS_FULL.slice(0, 2);
    if (range === '3') return SCORE_TRENDS_FULL.slice(0, 5);
    return SCORE_TRENDS_FULL.slice(0, 10);
  }, [range, state]);

  const kpi = state === 'partial' ? KPI_PARTIAL : KPI_DEFAULT;

  const body = () => {
    if (state === 'loading') return <LoadingState compact={compact} />;
    if (state === 'empty') {
      return <div style={{ display: 'grid' }}><EmptyNoSessions /></div>;
    }
    return (
      <div style={{ display: 'flex', flexDirection: 'column', gap: compact ? 22 : 28 }}>
        {state === 'partial' && (
          <Banner tone="info" icon="📊">
            Le statistiche diventano più ricche con più dati. Continua a giocare!
          </Banner>
        )}
        {state === 'error' && (
          <Banner tone="danger" icon="⚠" action={
            <button type="button" style={{
              display: 'inline-flex', alignItems: 'center', gap: 6,
              padding: '8px 14px', borderRadius: 'var(--r-md)',
              background: 'transparent', border: `1px solid ${sem('danger', 0.5)}`,
              color: sem('danger'), fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, cursor: 'pointer',
            }}><span aria-hidden="true">🔄</span>Riprova</button>
          }>
            Impossibile caricare le statistiche. Riprova tra qualche istante.
          </Banner>
        )}
        <KpiGrid compact={compact} data={kpi} />
        {state === 'error' ? (
          <Card pad={20} style={{ opacity: .55 }}>
            <Skel h={16} w="38%" style={{ marginBottom: 16 }} />
            {[0, 1, 2].map(i => <Skel key={i} h={20} style={{ marginBottom: 12 }} />)}
          </Card>
        ) : (
          <>
            <MostPlayed rows={mostPlayed} />
            <MonthlyChart months={months} compact={compact} />
            <ScoreTrends rows={trends} compact={compact} />
          </>
        )}
      </div>
    );
  };

  return (
    <main role="main" style={{ background: 'var(--bg)', minHeight: compact ? 'auto' : 600 }}>
      <Header compact={compact} range={range} onRange={onRange} />
      <div style={{ padding: compact ? '20px 16px 36px' : '28px 28px 56px', maxWidth: 1100, margin: '0 auto' }}>
        {body()}
      </div>
    </main>
  );
};

// ═══════════════════════════════════════════════════════
// ─── FRAMES (desktop chrome + phone) ────────────────
// ═══════════════════════════════════════════════════════
const DesktopFrame = ({ children }) => (
  <div className="ed-desk" style={{
    width: '100%', borderRadius: 'var(--r-xl)', border: '1px solid var(--border)',
    background: 'var(--bg)', overflow: 'hidden', boxShadow: 'var(--shadow-lg)',
  }}>
    <div style={{
      display: 'flex', alignItems: 'center', gap: 8, padding: '9px 14px',
      background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)',
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
    }}>
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: sem('danger') }} />
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: sem('warning') }} />
      <span style={{ width: 11, height: 11, borderRadius: '50%', background: sem('toolkit') }} />
      <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>meepleai.app/toolkit/stats</span>
    </div>
    {children}
  </div>
);

const PhoneFrame = ({ children }) => (
  <div className="phone" style={{ width: 375, height: 760 }}>
    <div className="phone-sbar" style={{ color: 'var(--text)' }}>
      <span style={{ fontFamily: 'var(--f-mono)' }}>14:32</span>
      <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
    </div>
    <div style={{ flex: 1, overflowY: 'auto', position: 'relative', background: 'var(--bg)' }}>
      {children}
    </div>
  </div>
);

// ═══════════════════════════════════════════════════════
// ─── STATE PICKER + ROOT ────────────────────────────
// ═══════════════════════════════════════════════════════
const STATES = [
  { id: 'default',  label: 'Default',            state: 'default', range: '12', view: 'both',   desc: 'Tutti i dati popolati: 47 sessioni · top-5 giochi · 12 barre mensili · 10 punteggi recenti.' },
  { id: 'empty',    label: 'Empty · no sessions', state: 'empty',  range: '12', view: 'desktop', desc: 'Utente nuovo: empty state centrato con CTA "Crea nuova sessione".' },
  { id: 'partial',  label: 'Empty · partial',     state: 'partial', range: '12', view: 'desktop', desc: 'Utente con 1–2 sessioni: KPI bassi, 1 gioco, poche barre, help banner info.' },
  { id: 'loading',  label: 'Loading',            state: 'loading', range: '12', view: 'desktop', desc: 'Skeleton shimmer: header + 3 KPI + most played + bar chart + score trends.' },
  { id: 'error',    label: 'Error',              state: 'error',   range: '12', view: 'desktop', desc: 'Banner danger + Riprova; KPI renderizzati, body con placeholder.' },
  { id: 'range3',   label: 'Range · 3 mesi',      state: 'default', range: '3',  view: 'desktop', desc: 'Time range 3 mesi: most played top-3, 3 barre mensili, 5 punteggi.' },
  { id: 'rangeall', label: 'Range · Tutto',       state: 'default', range: 'all', view: 'desktop', desc: 'Time range "Tutto": most played top-7, 24 barre scrollabili orizzontale.' },
  { id: 'mobile',   label: 'Mobile · 375',        state: 'default', range: '12', view: 'mobile',  desc: 'Vista 375px: KPI 1-col, bar chart 140px, score rows compatte (solo score).' },
];
const SKEY = 'ts-state';

const App = () => {
  const [theme, setTheme] = useState(() => localStorage.getItem('mai-theme') || document.documentElement.getAttribute('data-theme') || 'light');
  const [active, setActive] = useState(() => {
    const s = localStorage.getItem(SKEY);
    return STATES.some(x => x.id === s) ? s : 'default';
  });
  // per-frame interactive range override (header dropdown)
  const [rangeOv, setRangeOv] = useState(null);

  useEffect(() => { document.documentElement.setAttribute('data-theme', theme); localStorage.setItem('mai-theme', theme); }, [theme]);
  useEffect(() => { localStorage.setItem(SKEY, active); setRangeOv(null); }, [active]);

  const cur = STATES.find(s => s.id === active) || STATES[0];
  const range = rangeOv || cur.range;

  return (
    <div style={{ minHeight: '100vh', background: 'var(--bg)', color: 'var(--text)', padding: '20px 20px 80px' }}>
      {/* ─── State picker bar ─── */}
      <header style={{
        position: 'sticky', top: 12, zIndex: 50, maxWidth: 1180, margin: '0 auto 28px',
        background: 'var(--glass-bg)', backdropFilter: 'blur(16px)',
        border: '1px solid var(--border)', borderRadius: 'var(--r-xl)',
        boxShadow: 'var(--shadow-md)', padding: '12px 16px',
        display: 'flex', alignItems: 'center', gap: 14, flexWrap: 'wrap',
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <div style={{
            width: 30, height: 30, borderRadius: 8, flexShrink: 0,
            background: `linear-gradient(135deg, ${eHsl('toolkit')}, ${eHsl('game')})`,
            color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontWeight: 800, fontFamily: 'var(--f-display)', fontSize: 14,
          }}>S</div>
          <div>
            <div style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 14, lineHeight: 1.1 }}>Toolkit Stats</div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)' }}>#1490 · 1/4 · /toolkit/stats</div>
          </div>
        </div>

        <div role="tablist" aria-label="Stati schermata" style={{
          display: 'flex', gap: 6, flexWrap: 'wrap', flex: 1, minWidth: 0,
        }}>
          {STATES.map(s => {
            const on = s.id === active;
            return (
              <button key={s.id} type="button" role="tab" aria-selected={on}
                onClick={() => setActive(s.id)} style={{
                  padding: '7px 12px', borderRadius: 'var(--r-pill)', cursor: 'pointer',
                  background: on ? eHsl('toolkit') : 'var(--bg-muted)',
                  border: on ? 'none' : '1px solid var(--border)',
                  color: on ? '#fff' : 'var(--text-sec)',
                  fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, whiteSpace: 'nowrap',
                  boxShadow: on ? `0 3px 10px ${eHsl('toolkit', 0.35)}` : 'none',
                }}>{s.label}</button>
            );
          })}
        </div>

        <button type="button" onClick={() => setTheme(t => t === 'light' ? 'dark' : 'light')} style={{
          padding: '8px 14px', borderRadius: 'var(--r-md)', flexShrink: 0,
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          color: 'var(--text)', fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800, cursor: 'pointer',
        }}>🌗 {theme === 'light' ? 'Light' : 'Dark'}</button>
      </header>

      {/* ─── Active state description ─── */}
      <div style={{
        maxWidth: 1180, margin: '0 auto 18px', padding: '0 4px',
        fontFamily: 'var(--f-mono)', fontSize: 12, color: 'var(--text-muted)', lineHeight: 1.5,
      }}>
        <strong style={{ color: eHsl('toolkit') }}>{cur.label}</strong> — {cur.desc}
      </div>

      {/* ─── Render area ─── */}
      <div style={{ maxWidth: 1180, margin: '0 auto', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 36 }}>
        {(cur.view === 'both' || cur.view === 'desktop') && (
          <div style={{ width: '100%', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700 }}>Desktop · 1440</div>
            <DesktopFrame>
              <StatsBody state={cur.state} range={range} onRange={setRangeOv} compact={false} />
            </DesktopFrame>
          </div>
        )}
        {(cur.view === 'both' || cur.view === 'mobile') && (
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700 }}>Mobile · 375</div>
            <PhoneFrame>
              <StatsBody state={cur.state} range={range} onRange={setRangeOv} compact={true} />
            </PhoneFrame>
          </div>
        )}
      </div>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
