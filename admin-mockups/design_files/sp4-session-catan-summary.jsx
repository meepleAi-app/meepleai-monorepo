/* MeepleAI SP4 · Coloni di Catan — SESSION SUMMARY  (App)
   /sessions/[id]/summary — post-game recap for a finished Catan session.

   Reuses the universal chrome + atoms; adds the Catan post-game surfaces:
     Hero (winner banner + final VP) · Scoreboard (per-player VP breakdown by the
     5 catan.json categories) · Final Board (board snapshot) · Stats (turns, 2D6
     distribution chart, trades per player, longest production run).

   Reused: OutcomeBadge (window.MAI) · HexBoard / VPBadge / badges (CatanFlavor) ·
   SectionCard / PlayerDot (CatanParts). Dark = primary mode.

   Loads: sp4-parts-common · skeleton-renderers · catan-data(+SkeletonData shim) ·
          skeleton-parts · catan-flavor · catan-parts · catan-summary
   DEMO-NAV-HINTS: sp4-session-catan-live.html
*/

const M = window.MAI;
const R = window.SkeletonRenderers;
const S = window.SkeletonParts;
const C = window.CATAN;
const F = window.CatanFlavor;
const P = window.CatanParts;
const eHsl = M.entityHsl;
const { useState, useEffect } = React;
const { mono, disp } = F;

function ThemeToggle() {
  const [dark, setDark] = useState(true);
  useEffect(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

// ─── final standings (game over — Marco reaches 10 VP) ──────────────────────
// counts per category; VP computed from C.SCORING category weights.
const FINAL = {
  winner: 'marco',
  longestRoadHolder: 'marco',
  largestArmyHolder: 'anna',
  rows: [
    { id: 'marco', counts: { settlements: 2, cities: 2, 'longest-road': 1, 'largest-army': 0, 'vp-cards': 2 }, knights: 1, roadLen: 7 },
    { id: 'anna',  counts: { settlements: 2, cities: 2, 'longest-road': 0, 'largest-army': 1, 'vp-cards': 0 }, knights: 4, roadLen: 5 },
    { id: 'luca',  counts: { settlements: 3, cities: 1, 'longest-road': 0, 'largest-army': 0, 'vp-cards': 1 }, knights: 2, roadLen: 5 },
    { id: 'sara',  counts: { settlements: 4, cities: 0, 'longest-road': 0, 'largest-army': 0, 'vp-cards': 1 }, knights: 0, roadLen: 3 },
  ],
};
const CAT = C.SCORING.categories;
const catVP = (catId, count) => {
  const cat = CAT.find(c => c.id === catId);
  return count * cat.weight;
};
const rowTotal = (row) => CAT.reduce((s, c) => s + catVP(c.id, row.counts[c.id]), 0);
FINAL.rows.forEach(r => { r.total = rowTotal(r); r.player = C.PLAYERS.find(p => p.id === r.id); });
FINAL.standings = [...FINAL.rows].sort((a, b) => b.total - a.total);

// ─── Hero ────────────────────────────────────────────────────────────────────
const Hero = () => {
  const win = FINAL.standings[0];
  return (
    <div style={{ display: 'flex', gap: 16, alignItems: 'stretch', flexWrap: 'wrap', padding: 16, borderBottom: '1px solid var(--border)', background: `linear-gradient(135deg, ${eHsl('toolkit', 0.1)}, ${eHsl('session', 0.06)})` }}>
      <div style={{ flex: '1 1 320px', minWidth: 0, display: 'flex', gap: 14, alignItems: 'center' }}>
        <div style={{ position: 'relative', flexShrink: 0 }}>
          <div aria-hidden="true" style={{ width: 64, height: 64, borderRadius: '50%', background: `linear-gradient(135deg, hsl(${win.player.hue},70%,64%), hsl(${win.player.hue},58%,44%))`, color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', ...disp(28, 800, '#fff'), border: `3px solid ${eHsl('toolkit')}`, boxShadow: `0 4px 18px ${eHsl('toolkit', 0.35)}` }}>{win.player.name[0]}</div>
          <span aria-hidden="true" style={{ position: 'absolute', top: -10, right: -6, fontSize: 26, transform: 'rotate(18deg)' }}>👑</span>
        </div>
        <div style={{ minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 3 }}>
            <M.OutcomeBadge status="win" />
            <span style={{ ...mono(9.5, 700, 'var(--text-muted)') }}>{C.GAME.title} · {C.STATS.duration} · {C.STATS.totalTurns} turni</span>
          </div>
          <h2 style={{ fontFamily: 'var(--f-display)', fontSize: 26, fontWeight: 800, color: 'var(--text)', margin: 0 }}>{win.player.name} vince!</h2>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginTop: 5 }}>
            <F.VPBadge vp={win.total} leader size="lg" />
            <span style={{ ...mono(10, 700, 'var(--text-sec)') }}>· primo a {C.SCORING.target} PV</span>
          </div>
        </div>
      </div>
      {/* final standings strip */}
      <div style={{ flex: '1 1 360px', display: 'flex', flexDirection: 'column', gap: 6, justifyContent: 'center' }}>
        {FINAL.standings.map((r, i) => (
          <div key={r.id} style={{ display: 'flex', alignItems: 'center', gap: 9, padding: '6px 10px', borderRadius: 'var(--r-md)', background: i === 0 ? eHsl('toolkit', 0.1) : 'var(--bg-card)', border: `1px solid ${i === 0 ? eHsl('toolkit', 0.3) : 'var(--border)'}` }}>
            <span style={{ ...mono(11, 800, i === 0 ? eHsl('toolkit') : 'var(--text-muted)'), width: 16 }}>{i + 1}°</span>
            <F.PlayerDot p={r.player} size={12} />
            <span style={{ flex: 1, ...disp(13, 800, 'var(--text)') }}>{r.player.name}</span>
            {FINAL.longestRoadHolder === r.id && <F.LongestRoadBadge on len={r.roadLen} />}
            {FINAL.largestArmyHolder === r.id && <F.LargestArmyBadge on n={r.knights} />}
            <div style={{ height: 7, width: 90, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
              <div style={{ width: `${(r.total / C.SCORING.target) * 100}%`, height: '100%', background: i === 0 ? eHsl('toolkit') : `hsl(${r.player.hue},58%,55%)` }} />
            </div>
            <span style={{ ...disp(18, 800, i === 0 ? eHsl('toolkit') : 'var(--text)'), fontVariantNumeric: 'tabular-nums', minWidth: 22, textAlign: 'right' }}>{r.total}</span>
          </div>
        ))}
      </div>
    </div>
  );
};

// ─── Scoreboard tab ──────────────────────────────────────────────────────────
const ScoreboardTab = () => (
  <div style={{ padding: 16, overflowX: 'auto' }}>
    <div style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 10 }}>Punteggio finale · breakdown per categoria (catan.json scoringTemplate)</div>
    <table style={{ width: '100%', minWidth: 640, borderCollapse: 'separate', borderSpacing: 0 }}>
      <thead>
        <tr>
          <th style={{ textAlign: 'left', padding: '8px 10px', ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em', borderBottom: '1px solid var(--border)' }}>Giocatore</th>
          {CAT.map(c => (
            <th key={c.id} title={c.description} style={{ textAlign: 'center', padding: '8px 6px', ...mono(9, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.03em', borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap' }}>
              {c.label}<div style={{ ...mono(8, 700, 'var(--text-muted)'), opacity: 0.7 }}>×{c.weight} · {c.computation}</div>
            </th>
          ))}
          <th style={{ textAlign: 'right', padding: '8px 10px', ...mono(9, 800, eHsl('toolkit')), textTransform: 'uppercase', letterSpacing: '.05em', borderBottom: '1px solid var(--border)' }}>Totale</th>
        </tr>
      </thead>
      <tbody>
        {FINAL.standings.map((r, i) => (
          <tr key={r.id} style={{ background: i === 0 ? eHsl('toolkit', 0.06) : 'transparent' }}>
            <td style={{ padding: '9px 10px', borderBottom: '1px solid var(--border-light)' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <span style={{ ...mono(10, 800, i === 0 ? eHsl('toolkit') : 'var(--text-muted)') }}>{i + 1}°</span>
                <F.PlayerDot p={r.player} size={11} />
                <span style={{ ...disp(13, 800, 'var(--text)') }}>{r.player.name}</span>
                {i === 0 && <span aria-hidden="true">👑</span>}
              </div>
            </td>
            {CAT.map(c => {
              const cnt = r.counts[c.id];
              const vp = catVP(c.id, cnt);
              const isBonus = c.id === 'longest-road' || c.id === 'largest-army';
              const held = isBonus && cnt > 0;
              return (
                <td key={c.id} style={{ textAlign: 'center', padding: '9px 6px', borderBottom: '1px solid var(--border-light)' }}>
                  {isBonus
                    ? (held
                        ? <span style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', gap: 1 }}><span aria-hidden="true">{c.id === 'longest-road' ? '🛤️' : '⚔️'}</span><span style={{ ...mono(10, 800, eHsl('toolkit')) }}>+{vp}</span></span>
                        : <span style={{ ...mono(11, 700, 'var(--text-muted)') }}>—</span>)
                    : <span style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center', lineHeight: 1.1 }}>
                        <span style={{ ...disp(14, 800, vp ? 'var(--text)' : 'var(--text-muted)'), fontVariantNumeric: 'tabular-nums' }}>{vp}</span>
                        <span style={{ ...mono(8, 700, 'var(--text-muted)') }}>{c.id === 'vp-cards' ? `${cnt} carte` : `${cnt}×`}</span>
                      </span>}
                </td>
              );
            })}
            <td style={{ textAlign: 'right', padding: '9px 10px', borderBottom: '1px solid var(--border-light)' }}>
              <span style={{ ...disp(20, 800, i === 0 ? eHsl('toolkit') : 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{r.total}</span>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
    <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap', marginTop: 14 }}>
      {CAT.map(c => (
        <div key={c.id} style={{ display: 'flex', alignItems: 'center', gap: 6, ...mono(9.5, 700, 'var(--text-muted)') }}>
          <R.ComputationBadge c={c.computation} size={16} />{c.label} — {c.description}
        </div>
      ))}
    </div>
  </div>
);

// ─── Final Board tab ─────────────────────────────────────────────────────────
const FinalBoardTab = () => (
  <div style={{ padding: 16, display: 'flex', gap: 18, flexWrap: 'wrap', alignItems: 'flex-start', justifyContent: 'center' }}>
    <F.HexBoard R={46} highlightNumber={null} robberHex={C.ROBBER_HEX} />
    <div style={{ flex: '0 1 280px', display: 'flex', flexDirection: 'column', gap: 12 }}>
      <div style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Snapshot finale del tavolo</div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        {FINAL.standings.map(r => {
          const pc = C.PLAYERS.find(p => p.id === r.id);
          return (
            <div key={r.id} style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-card)', border: '1px solid var(--border)' }}>
              <F.PlayerDot p={pc} size={14} />
              <span style={{ flex: 1, ...disp(12.5, 800, 'var(--text)') }}>{pc.name}</span>
              <span style={{ ...mono(9.5, 700, 'var(--text-sec)') }}>{pc.pieces.settlements}🏠 · {pc.pieces.cities}🏛️ · {pc.pieces.roads}🛤️</span>
            </div>
          );
        })}
      </div>
      <div style={{ padding: '10px 12px', borderRadius: 'var(--r-md)', background: eHsl('event', 0.07), border: `1px solid ${eHsl('event', 0.28)}`, display: 'flex', alignItems: 'center', gap: 9 }}>
        <span aria-hidden="true" style={{ fontSize: 18 }}>🦹</span>
        <div style={{ ...mono(9.5, 700, 'var(--text-sec)') }}>Ladro fermo su <strong>{C.RES[C.HEXES.find(h => h.id === C.ROBBER_HEX).t].terrain}-{C.HEXES.find(h => h.id === C.ROBBER_HEX).n}</strong> a fine partita · {C.STATS.robberMoves} spostamenti totali</div>
      </div>
    </div>
  </div>
);

// ─── Stats tab ───────────────────────────────────────────────────────────────
const DiceChart = () => {
  const counts = C.STATS.diceCounts;
  const max = Math.max(...Object.values(counts));
  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'flex-end', gap: 6, height: 150, padding: '0 4px' }}>
        {Object.keys(counts).map(k => {
          const n = +k, v = counts[k];
          const hot = n === 6 || n === 8, seven = n === 7;
          return (
            <div key={k} style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, height: '100%', justifyContent: 'flex-end' }}>
              <span style={{ ...mono(9.5, 800, seven ? eHsl('event') : hot ? '#d9694f' : 'var(--text-sec)'), fontVariantNumeric: 'tabular-nums' }}>{v}</span>
              <div title={`${n}: ${v} volte`} style={{ width: '100%', maxWidth: 30, height: `${(v / max) * 100}%`, borderRadius: '4px 4px 0 0', background: seven ? eHsl('event') : hot ? 'linear-gradient(180deg,#e07a5f,#c0584a)' : `hsl(${210},45%,55%)`, minHeight: 4, transition: 'height var(--dur-md) var(--ease-out)' }} />
              <span style={{ ...mono(10, 800, seven ? eHsl('event') : 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{n}</span>
            </div>
          );
        })}
      </div>
      <div style={{ ...mono(9, 700, 'var(--text-muted)'), marginTop: 8, textAlign: 'center' }}>{C.STATS.diceTotal} lanci totali · 7 (ladro) il più frequente · 6 e 8 i numeri di produzione più caldi</div>
    </div>
  );
};
const TradeBars = () => {
  const t = C.STATS.trades;
  const max = Math.max(...Object.values(t));
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      {C.PLAYERS.map(p => (
        <div key={p.id} style={{ display: 'flex', alignItems: 'center', gap: 9 }}>
          <F.PlayerDot p={p} size={11} />
          <span style={{ width: 48, ...disp(12, 700, 'var(--text)') }}>{p.name}</span>
          <div style={{ flex: 1, height: 14, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
            <div style={{ width: `${(t[p.id] / max) * 100}%`, height: '100%', borderRadius: 'var(--r-pill)', background: `hsl(${p.hue},58%,55%)` }} />
          </div>
          <span style={{ ...mono(11, 800, 'var(--text)'), minWidth: 16, textAlign: 'right' }}>{t[p.id]}</span>
        </div>
      ))}
    </div>
  );
};
const StatTile = ({ icon, value, label, e = 'session' }) => (
  <div style={{ flex: 1, minWidth: 120, padding: '12px 14px', borderRadius: 'var(--r-lg)', background: eHsl(e, 0.06), border: `1px solid ${eHsl(e, 0.25)}` }}>
    <div style={{ fontSize: 18, marginBottom: 3 }} aria-hidden="true">{icon}</div>
    <div style={{ ...disp(22, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{value}</div>
    <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em', marginTop: 3 }}>{label}</div>
  </div>
);
const StatsTab = () => (
  <div style={{ padding: 16, display: 'flex', flexDirection: 'column', gap: 16 }}>
    <div style={{ display: 'flex', gap: 10, flexWrap: 'wrap' }}>
      <StatTile icon="🔄" value={C.STATS.totalTurns} label="turni totali" e="player" />
      <StatTile icon="🗓️" value={C.STATS.totalRounds} label="round" e="session" />
      <StatTile icon="⏱️" value={C.STATS.duration} label="durata" e="game" />
      <StatTile icon="🤝" value={Object.values(C.STATS.trades).reduce((a, b) => a + b, 0)} label="scambi" e="chat" />
      <StatTile icon="🦹" value={C.STATS.robberMoves} label="mosse del ladro" e="event" />
    </div>
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 14 }}>
      <P.SectionCard title="Distribuzione dei 2D6" entity="tool" accent icon="🎲"><DiceChart /></P.SectionCard>
      <P.SectionCard title="Scambi per giocatore" entity="chat" accent icon="🤝"><TradeBars /></P.SectionCard>
    </div>
    <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
      <div style={{ flex: '1 1 280px', display: 'flex', alignItems: 'center', gap: 12, padding: 14, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: `1px solid ${eHsl('toolkit', 0.3)}` }}>
        <span aria-hidden="true" style={{ fontSize: 26 }}>🌾</span>
        <div><div style={{ ...disp(14, 800, 'var(--text)') }}>Serie di produzione più lunga</div><div style={{ ...mono(10, 700, 'var(--text-sec)') }}>{C.STATS.longestProductionRun.hint} (numero {C.STATS.longestProductionRun.number})</div></div>
        <div style={{ flex: 1 }} /><span style={{ ...disp(28, 800, eHsl('toolkit')) }}>×{C.STATS.longestProductionRun.count}</span>
      </div>
      <div style={{ flex: '1 1 280px', display: 'flex', alignItems: 'center', gap: 12, padding: 14, borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: `1px solid ${eHsl('player', 0.3)}` }}>
        <span aria-hidden="true" style={{ fontSize: 26 }}>🃏</span>
        <div><div style={{ ...disp(14, 800, 'var(--text)') }}>Mano più grande</div><div style={{ ...mono(10, 700, 'var(--text-sec)') }}>{C.PLAYERS.find(p => p.id === C.STATS.biggestHand.player).name} · {C.STATS.biggestHand.hint}</div></div>
        <div style={{ flex: 1 }} /><span style={{ ...disp(28, 800, eHsl('player')) }}>{C.STATS.biggestHand.n}</span>
      </div>
    </div>
  </div>
);

// ─── tab shell ───────────────────────────────────────────────────────────────
const SUM_TABS = [
  { id: 'scoreboard', icon: '🏆', label: 'Scoreboard', entity: 'toolkit', render: () => <ScoreboardTab /> },
  { id: 'board',      icon: '🗺️', label: 'Tabellone finale', entity: 'session', render: () => <FinalBoardTab /> },
  { id: 'stats',      icon: '📊', label: 'Statistiche', entity: 'player', render: () => <StatsTab /> },
];
const SummaryBody = ({ initial = 'scoreboard' }) => {
  const [tab, setTab] = useState(initial);
  const active = SUM_TABS.find(t => t.id === tab) || SUM_TABS[0];
  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
      <Hero />
      <div role="tablist" aria-label="Sezioni recap" style={{ display: 'flex', borderBottom: '1px solid var(--border)', flexShrink: 0, background: 'var(--bg-card)' }}>
        {SUM_TABS.map(t => {
          const on = tab === t.id;
          return (
            <button key={t.id} type="button" role="tab" aria-selected={on} onClick={() => setTab(t.id)} style={{ padding: '11px 16px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(12.5, 800), cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: 6 }}>
              <span aria-hidden="true" style={{ fontSize: 15 }}>{t.icon}</span>{t.label}
            </button>
          );
        })}
      </div>
      <div role="tabpanel" style={{ flex: 1, minHeight: 0, overflowY: 'auto', background: 'var(--bg)' }}>{active.render()}</div>
    </div>
  );
};

// ─── mobile phone ────────────────────────────────────────────────────────────
const PhoneSummary = ({ label, dark, initial }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
    <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>{label}{dark && <span style={{ color: eHsl('session'), marginLeft: 6 }}>· dark</span>}</div>
    <div className="phone" data-theme={dark ? 'dark' : undefined}>
      <div className="phone-sbar" style={{ color: 'var(--text)' }}>
        <span style={{ fontFamily: 'var(--f-mono)' }}>20:11</span>
        <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">78%</span></div>
      </div>
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', minHeight: 0, overflow: 'hidden' }}>
        <SummaryBody initial={initial} />
      </div>
    </div>
  </div>
);

const Intro = () => (
  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12, margin: '4px 0 8px' }}>
    {[
      { e: 'toolkit', h: 'Hero · vincitore', b: 'Banner con OutcomeBadge riusato, PV finali e classifica completa con barre verso il traguardo dei 10 PV.' },
      { e: 'session', h: 'Scoreboard', b: 'Breakdown per le 5 categorie del catan.json: insediamenti ×1, città ×2, Strada+ 2PV, Armata+ 2PV, carte PV ×1.' },
      { e: 'game',    h: 'Tabellone finale', b: 'Snapshot del tavolo a fine partita — esagoni, pezzi color-coded, posizione finale del ladro.' },
      { e: 'player',  h: 'Statistiche', b: 'Turni, distribuzione dei 2D6 (chart), scambi per giocatore, serie di produzione più lunga.' },
    ].map(c => (
      <div key={c.h} style={{ padding: 12, borderRadius: 'var(--r-lg)', background: eHsl(c.e, 0.06), border: `1px solid ${eHsl(c.e, 0.25)}` }}>
        <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: eHsl(c.e), marginBottom: 4 }}>{c.h}</div>
        <div style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5 }}>{c.b}</div>
      </div>
    ))}
  </div>
);

function App() {
  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Coloni di Catan · Summary 🏆 · estensione skeleton</div>
        <h1>Recap partita — Coloni di Catan</h1>
        <p className="lead">
          Schermata post-partita per <code>/sessions/[id]/summary</code>. Hero con il vincitore e i PV finali, poi tre tab:
          <strong> Scoreboard</strong> (breakdown per le 5 categorie del toolkit), <strong>Tabellone finale</strong> (snapshot del tavolo)
          e <strong>Statistiche</strong> (turni · distribuzione dei dadi · scambi). Dark = modalità primaria.
        </p>
        <Intro />

        <div className="section-label">Desktop 1280 — hero + tab Scoreboard / Tabellone finale / Statistiche</div>
        <S.DesktopFrame dark label="Desktop · summary · Coloni di Catan" url="meepleai.app/sessions/catan-7/summary" height={760}
          desc="Hero: Marco vince con 10 PV. Tab Scoreboard: breakdown per categoria con badge computation. Tab Tabellone: snapshot finale. Tab Statistiche: chart dei 2D6, scambi, record di partita.">
          <SummaryBody initial="scoreboard" />
        </S.DesktopFrame>

        <div className="section-label">Mobile 375 — recap a tab impilate</div>
        <div className="phones-grid">
          <PhoneSummary label="01 · Scoreboard" initial="scoreboard" />
          <PhoneSummary label="02 · Tabellone finale" initial="board" />
          <PhoneSummary label="03 · Statistiche · dark" dark initial="stats" />
        </div>
      </div>

      <style>{`
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; font-family: var(--f-mono); }
        @media (prefers-reduced-motion: reduce) { .mai-pulse-dot { animation: none !important; } }
        .phone > div::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
