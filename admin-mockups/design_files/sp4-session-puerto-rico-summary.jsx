/* MeepleAI SP4 · Puerto Rico — SESSION SUMMARY  (App)
   /sessions/[id]/summary — post-game recap for a finished Puerto Rico session.

   Tabs:
     Hero (always-on)  · final VP totals + winner banner + stacked breakdown bar
     Scoreboard        · analytic per-player breakdown:
                         buildings VP (per building) · shipped goods (per type) ·
                         large building bonuses (per building, custom computation)
     Final Board       · snapshot of every player's mat (plantations+buildings+coloni)
     Round Recap       · who picked which role each round (governor rotation)
     Stats             · per-player averages / maxima (VP/min, top role, totals)

   Reuses: window.PRFlavor (goods/avatars) · window.PRParts (PlayerMat ·
   SectionCard · RoleChip) · window.PR (dataset + PR.summary). Dark = primary.

   Loads: sp4-parts-common · skeleton-renderers · skeleton-data(shim) ·
          skeleton-parts · puerto-rico-data · puerto-rico-flavor · puerto-rico-parts
   DEMO-NAV-HINTS: sp4-session-puerto-rico-live.html
*/

const M = window.MAI;
const F = window.PRFlavor;
const P = window.PRParts;
const PR = window.PR;
const eHsl = M.entityHsl;
const { useState, useEffect } = React;
const mono = F.mono, disp = F.disp;
const sum = window.PR.summary;
const byId = (id) => PR.roster.find(p => p.id === id);

// final ranking (desc by total)
const RANKED = [...PR.roster].map(p => ({ ...p, fin: sum.finalScore[p.id] }))
  .sort((a, b) => b.fin.total - a.fin.total);
const WINNER = RANKED[0];
const MAXTOTAL = WINNER.fin.total;

// breakdown segment palette (consistent everywhere)
const SEG = [
  { key: 'building', e: 'kb',      lb: 'Edifici' },
  { key: 'shipped',  e: 'session', lb: 'Spedizioni' },
  { key: 'large',    e: 'player',  lb: 'Bonus grandi' },
];

function ThemeToggle() {
  const [dark, setDark] = useState(true);
  useEffect(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

// stacked breakdown bar (building / shipped / large)
const StackBar = ({ fin, max, h = 12 }) => (
  <div style={{ display: 'flex', height: h, borderRadius: 'var(--r-pill)', overflow: 'hidden', background: 'var(--bg-sunken)', width: '100%' }} aria-hidden="true">
    {SEG.map(s => (
      <div key={s.key} title={`${s.lb}: ${fin[s.key]}`} style={{ width: `${(fin[s.key] / max) * 100}%`, background: eHsl(s.e), height: '100%' }} />
    ))}
  </div>
);

// ═══ HERO ═════════════════════════════════════════════════════════════════
const WinnerHero = () => (
  <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0,1.1fr) minmax(0,1fr)', gap: 18, alignItems: 'stretch' }} className="pr-hero-grid">
    {/* winner banner */}
    <div style={{ position: 'relative', overflow: 'hidden', borderRadius: 'var(--r-2xl)', border: `1px solid ${eHsl('toolkit', 0.4)}`, background: `linear-gradient(135deg, ${eHsl('toolkit', 0.16)}, ${eHsl('session', 0.12)})`, padding: 22, display: 'flex', flexDirection: 'column', justifyContent: 'space-between', minHeight: 230 }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <span style={{ ...mono(10, 800, eHsl('toolkit')), padding: '3px 10px', borderRadius: 'var(--r-pill)', background: eHsl('toolkit', 0.16), border: `1px solid ${eHsl('toolkit', 0.4)}`, textTransform: 'uppercase', letterSpacing: '.06em' }}>🏆 Vincitore</span>
        <span style={{ ...mono(10, 700, 'var(--text-sec)') }}>Partita conclusa · {sum.durationMin} min</span>
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
        <F.MiniAvatar p={WINNER} size={72} ring />
        <div>
          <div style={{ ...disp(30, 800, 'var(--text)'), lineHeight: 1 }}>{WINNER.name}</div>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginTop: 4 }}>
            <span style={{ ...disp(42, 800, eHsl('toolkit')), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{WINNER.fin.total}</span>
            <span style={{ ...mono(12, 800, 'var(--text-sec)') }}>PV totali</span>
          </div>
        </div>
      </div>
      <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
        {SEG.map(s => (
          <span key={s.key} style={{ display: 'inline-flex', alignItems: 'center', gap: 5, ...mono(10, 800, eHsl(s.e)), padding: '3px 9px', borderRadius: 'var(--r-pill)', background: eHsl(s.e, 0.12), border: `1px solid ${eHsl(s.e, 0.3)}` }}>
            <span style={{ width: 8, height: 8, borderRadius: 2, background: eHsl(s.e) }} aria-hidden="true" />{s.lb} {WINNER.fin[s.key]}
          </span>
        ))}
      </div>
    </div>
    {/* podium / standings */}
    <div style={{ borderRadius: 'var(--r-2xl)', border: '1px solid var(--border)', background: 'var(--bg-card)', padding: 16, display: 'flex', flexDirection: 'column', gap: 9 }}>
      <div style={{ ...mono(10, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Classifica finale</div>
      {RANKED.map((p, i) => (
        <div key={p.id} style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '7px 9px', borderRadius: 'var(--r-md)', background: i === 0 ? eHsl('toolkit', 0.08) : 'var(--bg-muted)', border: `1px solid ${i === 0 ? eHsl('toolkit', 0.3) : 'var(--border-light)'}` }}>
          <span style={{ ...mono(12, 800, i === 0 ? eHsl('toolkit') : 'var(--text-muted)'), width: 18 }}>{i + 1}°</span>
          <F.MiniAvatar p={p} size={28} ring={i === 0} />
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ ...disp(13.5, 800, 'var(--text)') }}>{p.name}</div>
            <div style={{ marginTop: 4 }}><StackBar fin={p.fin} max={MAXTOTAL} h={8} /></div>
          </div>
          <span style={{ ...disp(20, 800, i === 0 ? eHsl('toolkit') : 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{p.fin.total}</span>
        </div>
      ))}
      <div style={{ ...mono(9.5, 700, 'var(--text-muted)'), marginTop: 2, lineHeight: 1.4 }}>Fine partita: {sum.endTrigger}</div>
    </div>
  </div>
);

// ═══ SCOREBOARD TAB ═══════════════════════════════════════════════════════
const ScoreboardTab = () => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
    {/* totals matrix */}
    <P.SectionCard title="Matrice punteggi" entity="session" accent>
      <div style={{ overflowX: 'auto' }} className="mai-cb-scroll">
        <table style={{ width: '100%', borderCollapse: 'collapse', minWidth: 460 }}>
          <thead>
            <tr>
              <th style={thStyle('left')}>Giocatore</th>
              {SEG.map(s => <th key={s.key} style={thStyle('right', eHsl(s.e))}>{s.lb}</th>)}
              <th style={thStyle('right')}>Totale</th>
            </tr>
          </thead>
          <tbody>
            {RANKED.map((p, i) => (
              <tr key={p.id} style={{ background: i === 0 ? eHsl('toolkit', 0.06) : 'transparent' }}>
                <td style={tdStyle('left')}>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: 7 }}>
                    <F.MiniAvatar p={p} size={22} ring={i === 0} />
                    <span style={{ ...disp(12.5, 800, 'var(--text)') }}>{p.name}</span>
                  </span>
                </td>
                {SEG.map(s => <td key={s.key} style={tdStyle('right')}><span style={{ ...mono(12, 800, eHsl(s.e)) }}>{p.fin[s.key]}</span></td>)}
                <td style={tdStyle('right')}><span style={{ ...disp(16, 800, i === 0 ? eHsl('toolkit') : 'var(--text)') }}>{p.fin.total}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </P.SectionCard>

    {/* per-player breakdown cards */}
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(290px, 1fr))', gap: 14 }}>
      {RANKED.map((p, i) => (
        <P.SectionCard key={p.id} title={`${p.name} · ${p.fin.total} PV`} entity={i === 0 ? 'toolkit' : 'session'} accent={i === 0}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            {/* buildings VP */}
            <div>
              <BreakHead e="kb" lb="Edifici · PV stampati" total={p.fin.building} />
              <div style={{ display: 'flex', flexDirection: 'column', gap: 3, marginTop: 6 }}>
                {sum.buildingVP[p.id].map((b, k) => (
                  <div key={k} style={rowStyle}>
                    <span style={{ ...disp(11.5, 700, 'var(--text)'), flex: 1 }}>{b[0]}</span>
                    <span style={{ width: 16, height: 16, borderRadius: 'var(--r-xs)', background: PR.SUPPLY.vp.color, color: '#fff', ...mono(9, 800), display: 'inline-flex', alignItems: 'center', justifyContent: 'center' }}>{b[1]}</span>
                  </div>
                ))}
              </div>
            </div>
            {/* shipped goods */}
            <div>
              <BreakHead e="session" lb="Merci spedite · 1 PV/merce" total={p.fin.shipped} />
              <div style={{ display: 'flex', gap: 5, flexWrap: 'wrap', marginTop: 6 }}>
                {PR.GOODS.map(g => <F.GoodCount key={g.id} id={g.id} n={sum.shipped[p.id][g.id] || 0} dim />)}
              </div>
            </div>
            {/* large bonuses */}
            <div>
              <BreakHead e="player" lb="Bonus edifici grandi" total={p.fin.large} />
              <div style={{ display: 'flex', flexDirection: 'column', gap: 5, marginTop: 6 }}>
                {sum.largeBonus[p.id].map((b, k) => (
                  <div key={k} style={{ padding: '7px 9px', borderRadius: 'var(--r-sm)', background: eHsl('player', 0.08), border: `1px solid ${eHsl('player', 0.28)}` }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <span style={{ ...disp(12, 800, eHsl('player')), flex: 1 }}>{b.n}</span>
                      <span style={{ ...mono(11, 800, eHsl('player')) }}>+{b.value}</span>
                    </div>
                    <div style={{ ...mono(8.5, 600, 'var(--text-muted)'), marginTop: 2, lineHeight: 1.35 }}>{b.rule}</div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </P.SectionCard>
      ))}
    </div>
  </div>
);
const BreakHead = ({ e, lb, total }) => (
  <div style={{ display: 'flex', alignItems: 'center', gap: 7, paddingBottom: 5, borderBottom: `1px solid ${eHsl(e, 0.25)}` }}>
    <span style={{ width: 8, height: 8, borderRadius: 2, background: eHsl(e) }} aria-hidden="true" />
    <span style={{ ...mono(9.5, 800, eHsl(e)), textTransform: 'uppercase', letterSpacing: '.05em', flex: 1 }}>{lb}</span>
    <span style={{ ...disp(14, 800, 'var(--text)') }}>{total}</span>
  </div>
);
const rowStyle = { display: 'flex', alignItems: 'center', gap: 8, padding: '4px 8px', borderRadius: 'var(--r-sm)', background: 'var(--bg-muted)' };
const thStyle = (align, color) => ({ textAlign: align, padding: '6px 10px', ...mono(9, 800, color || 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em', borderBottom: '1px solid var(--border)', whiteSpace: 'nowrap' });
const tdStyle = (align) => ({ textAlign: align, padding: '8px 10px', borderBottom: '1px solid var(--border-light)' });

// ═══ FINAL BOARD TAB ══════════════════════════════════════════════════════
const FinalBoardTab = () => (
  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(330px, 1fr))', gap: 14 }}>
    {RANKED.map((p, i) => (
      <P.SectionCard key={p.id} title={`Plancia finale · ${p.name}`} entity={i === 0 ? 'toolkit' : 'session'} accent={i === 0}
        right={<span style={{ ...disp(14, 800, eHsl('toolkit')) }}>{p.fin.total} PV</span>}>
        <P.PlayerMat player={p} active={false} tileSize={26} bw={72} compact />
      </P.SectionCard>
    ))}
  </div>
);

// ═══ ROUND RECAP TAB ══════════════════════════════════════════════════════
const RoundRecapTab = () => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
    {sum.recap.map(r => (
      <P.SectionCard key={r.round} title={`Round ${r.round}`} entity="player"
        right={<span style={{ display: 'inline-flex', alignItems: 'center', gap: 5 }}><span style={{ ...mono(9, 700, 'var(--text-muted)') }}>governatore</span><F.MiniAvatar p={byId(r.governor)} size={18} /></span>}>
        <div className="mai-cb-scroll" style={{ display: 'flex', gap: 10, overflowX: 'auto', paddingBottom: 4, alignItems: 'stretch' }}>
          {r.picks.map((pk, i) => {
            const pl = byId(pk[0]);
            return (
              <React.Fragment key={i}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 6, minWidth: 130, flexShrink: 0, padding: '9px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <F.MiniAvatar p={pl} size={20} />
                    <span style={{ ...disp(12, 800, 'var(--text)') }}>{pl.name}</span>
                  </div>
                  <P.RoleChip roleId={pk[1]} />
                </div>
                {i < r.picks.length - 1 && <span aria-hidden="true" style={{ alignSelf: 'center', color: 'var(--text-muted)', ...mono(13, 800) }}>→</span>}
              </React.Fragment>
            );
          })}
        </div>
      </P.SectionCard>
    ))}
  </div>
);

// ═══ STATS TAB ════════════════════════════════════════════════════════════
const StatTile = ({ lb, value, e = 'session', sub }) => (
  <div style={{ padding: '10px 12px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)', flex: 1, minWidth: 0 }}>
    <div style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>{lb}</div>
    <div style={{ ...disp(18, 800, eHsl(e)), fontVariantNumeric: 'tabular-nums', marginTop: 2 }}>{value}</div>
    {sub && <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), marginTop: 1 }}>{sub}</div>}
  </div>
);
const StatsTab = () => {
  const maxShip = Math.max(...PR.roster.map(p => sum.stats[p.id].shippedTot));
  const maxVpm = Math.max(...PR.roster.map(p => sum.stats[p.id].vpPerMin));
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 14 }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px,1fr))', gap: 12 }}>
        <P.SectionCard title="Durata" entity="session"><div style={{ ...disp(24, 800, 'var(--text)') }}>{sum.durationMin}<span style={{ ...mono(11, 700, 'var(--text-muted)'), marginLeft: 4 }}>min</span></div></P.SectionCard>
        <P.SectionCard title="Round giocati" entity="player"><div style={{ ...disp(24, 800, 'var(--text)') }}>{sum.recap.length}</div></P.SectionCard>
        <P.SectionCard title="Merci spedite (tot)" entity="toolkit"><div style={{ ...disp(24, 800, 'var(--text)') }}>{PR.roster.reduce((s, p) => s + sum.stats[p.id].shippedTot, 0)}</div></P.SectionCard>
        <P.SectionCard title="Edifici costruiti" entity="kb"><div style={{ ...disp(24, 800, 'var(--text)') }}>{PR.roster.reduce((s, p) => s + sum.stats[p.id].builtTot, 0)}</div></P.SectionCard>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px,1fr))', gap: 14 }}>
        {RANKED.map((p, i) => {
          const st = sum.stats[p.id];
          return (
            <P.SectionCard key={p.id} title={p.name} entity={i === 0 ? 'toolkit' : 'session'} accent={i === 0}
              right={st.shippedTot === maxShip ? <span style={{ ...mono(8.5, 800, eHsl('event')), padding: '1px 6px', borderRadius: 'var(--r-pill)', background: eHsl('event', 0.12) }}>top spedizioni</span> : null}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: 9 }}>
                <div style={{ display: 'flex', gap: 8 }}>
                  <StatTile lb="PV / min" value={st.vpPerMin.toFixed(2)} e={st.vpPerMin === maxVpm ? 'toolkit' : 'session'} sub={st.vpPerMin === maxVpm ? 'più efficiente' : null} />
                  <StatTile lb="Coloni piazzati" value={st.colonists} e="game" />
                </div>
                <div style={{ display: 'flex', gap: 8 }}>
                  <StatTile lb="Merci spedite" value={st.shippedTot} e="session" />
                  <StatTile lb="Edifici" value={st.builtTot} e="kb" />
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 10px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
                  <span style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>Ruolo più scelto</span>
                  <div style={{ flex: 1 }} />
                  <P.RoleChip roleId={st.topRole} />
                  <span style={{ ...mono(10, 800, 'var(--text)') }}>×{st.topRoleCount}</span>
                </div>
              </div>
            </P.SectionCard>
          );
        })}
      </div>
    </div>
  );
};

// ═══ TABS SHELL ═══════════════════════════════════════════════════════════
const TABS = [
  { id: 'scoreboard', icon: '📊', label: 'Scoreboard', entity: 'session', render: () => <ScoreboardTab /> },
  { id: 'board',      icon: '🗺️', label: 'Final Board', entity: 'game',    render: () => <FinalBoardTab /> },
  { id: 'recap',      icon: '🔄', label: 'Round Recap', entity: 'player',  render: () => <RoundRecapTab /> },
  { id: 'stats',      icon: '📈', label: 'Stats',       entity: 'toolkit', render: () => <StatsTab /> },
];

function App() {
  const [tab, setTab] = useState('scoreboard');
  const active = TABS.find(t => t.id === tab);
  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Puerto Rico · Summary 🏁</div>
        <h1>Riepilogo partita — Puerto Rico</h1>
        <p className="lead">
          Recap post-partita per <code>/sessions/[id]/summary</code>. Hero con totali finali e vincitore;
          tab analitici: <strong>Scoreboard</strong> (breakdown edifici · merci spedite · bonus grandi),
          <strong> Final Board</strong> (snapshot di ogni plancia), <strong>Round Recap</strong> (chi ha scelto
          quale ruolo), <strong>Stats</strong> (medie e massimi). Dark = modalità primaria.
        </p>

        <WinnerHero />

        <div role="tablist" aria-label="Sezioni riepilogo" style={{ display: 'flex', gap: 6, flexWrap: 'wrap', margin: '22px 0 16px', borderBottom: '1px solid var(--border)', paddingBottom: 2 }}>
          {TABS.map(t => {
            const on = tab === t.id;
            return (
              <button key={t.id} type="button" role="tab" aria-selected={on} onClick={() => setTab(t.id)} style={{
                display: 'inline-flex', alignItems: 'center', gap: 6, padding: '9px 15px', borderRadius: 'var(--r-md) var(--r-md) 0 0',
                background: on ? eHsl(t.entity, 0.1) : 'transparent', border: 'none',
                borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent',
                color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(13, 800), cursor: 'pointer', whiteSpace: 'nowrap',
              }}>
                <span aria-hidden="true" style={{ fontSize: 15 }}>{t.icon}</span>{t.label}
              </button>
            );
          })}
        </div>

        <div role="tabpanel">{active.render()}</div>
      </div>

      <style>{`
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; font-family: var(--f-mono); }
        @media (max-width: 760px) { .pr-hero-grid { grid-template-columns: 1fr !important; } }
        @media (prefers-reduced-motion: reduce) { .mai-pulse-dot { animation: none !important; } }
        button:focus-visible, a:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
