/* MeepleAI SP4 · Power Grid — SESSION SUMMARY  (App)
   /sessions/[id]/summary — riepilogo post-partita di Power Grid.

   Hero: banner vincitore con città alimentate + spareggio Elektro.
   Tab: Scoreboard (breakdown per giocatore · primary/tiebreaker/info) ·
        Network (snapshot finale rete) · Step (timeline dei 3 step) ·
        Stats (bid medio per fascia · spesa per giocatore · meta).

   Riusa: OutcomeBadge · ComputationBadge · NetworkMap · MiniAvatar · atomi PG.
   Carica gli stessi deps del live + QUESTO file.
   DEMO-NAV-HINTS: sp4-session-power-grid-live.html
*/

const M = window.MAI;
const R = window.SkeletonRenderers;
const PG = window.PG;
const F = window.PGFlavor;
const P = window.PGParts;
const eHsl = M.entityHsl;
const { useState, useEffect } = React;
const { mono, disp } = F;
const SUM = PG.summary;

function ThemeToggle() {
  const [dark, setDark] = useState(true);
  useEffect(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

// ─── Hero — banner vincitore ──────────────────────────────────────────────
const SummaryHero = ({ compact }) => {
  const w = SUM.standings.find(s => s.winner);
  const runnerUp = PG.byId(SUM.tiebreak.runnerUpId);
  return (
    <div style={{ position: 'relative', borderRadius: 'var(--r-xl)', overflow: 'hidden', border: `1px solid ${eHsl('toolkit', 0.35)}`, background: `linear-gradient(135deg, ${eHsl('toolkit', 0.12)}, ${eHsl('session', 0.08)})`, boxShadow: 'var(--shadow-md)' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: compact ? 12 : 18, padding: compact ? 14 : 20, flexWrap: 'wrap' }}>
        <div style={{ width: compact ? 54 : 68, height: compact ? 66 : 84, borderRadius: 'var(--r-md)', background: PG.game.cover, flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: compact ? 26 : 34, boxShadow: 'var(--shadow-sm)' }} aria-hidden="true">{PG.game.emoji}</div>
        <div style={{ flex: 1, minWidth: 180 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
            <M.OutcomeBadge status="win" size="sm" />
            <span style={{ ...mono(9.5, 700, 'var(--text-muted)') }}>{PG.game.title} · {SUM.rounds} round · Step {SUM.step}</span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
            <F.MiniAvatar p={w} size={compact ? 32 : 40} ring />
            <h2 style={{ ...disp(compact ? 22 : 30, 800, 'var(--text)'), margin: 0 }}>{w.name} vince <span aria-hidden="true">🏆</span></h2>
          </div>
          <div style={{ ...mono(11, 700, 'var(--text-sec)'), marginTop: 6, lineHeight: 1.5 }}>{SUM.tiebreak.note}</div>
        </div>
        {/* primary metric: cities powered */}
        <div style={{ display: 'flex', gap: 10, flexShrink: 0 }}>
          <div style={{ textAlign: 'center', padding: '10px 16px', borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: `1px solid ${eHsl('game', 0.3)}` }}>
            <div style={{ ...disp(compact ? 30 : 40, 800, eHsl('game')), fontVariantNumeric: 'tabular-nums', lineHeight: 1 }}>{w.powered}</div>
            <div style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em', marginTop: 3 }}>città alimentate</div>
          </div>
          <div style={{ textAlign: 'center', padding: '10px 14px', borderRadius: 'var(--r-lg)', background: 'var(--bg-card)', border: '1px solid var(--border)' }}>
            <div style={{ ...disp(compact ? 22 : 28, 800, 'var(--text-sec)'), fontVariantNumeric: 'tabular-nums', lineHeight: 1, marginTop: compact ? 4 : 8 }}>€{w.elektro}</div>
            <div style={{ ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em', marginTop: 3 }}>Elektro · spareggio</div>
          </div>
        </div>
      </div>
      <div style={{ ...mono(9, 700, 'var(--text-muted)'), padding: '6px 16px', borderTop: '1px solid var(--border-light)', background: 'var(--glass-bg)' }}>
        Fine partita: <strong style={{ color: 'var(--text-sec)' }}>{SUM.endTrigger}</strong> → ultima Burocrazia · runner-up {runnerUp.name}
      </div>
    </div>
  );
};

// ─── Scoreboard — breakdown per giocatore ─────────────────────────────────
const COLS = [
  { id: 'powered', lb: 'Città alimentate', sub: 'primary', comp: 'Count', big: true, e: 'game' },
  { id: 'elektro', lb: 'Elektro', sub: 'spareggio 1', comp: 'Sum', e: 'session', prefix: '€' },
  { id: 'plantsCap', lb: 'Cap. centrali', sub: 'spareggio 2', comp: 'Custom', e: 'toolkit' },
  { id: 'cities', lb: 'Città possedute', sub: 'info', e: 'player' },
  { id: 'plants', lb: 'N° centrali', sub: 'info', e: 'kb' },
  { id: 'maxCap', lb: 'Cap. max singola', sub: 'info', e: 'kb' },
];
const ScoreboardTable = ({ state = 'default' }) => (
  <R.StateScaffold state={state} sseWhere="punteggio"
    empty={{ icon: '🏆', title: 'Nessun punteggio finale', body: 'La classifica comparirà a partita conclusa.' }}
    error={{ title: 'Classifica non disponibile', body: 'Impossibile caricare il punteggio finale.' }}
    loading={<div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 8 }}>{Array.from({ length: 5 }).map((_, i) => <M.Shimmer key={i} h={48} />)}</div>}>
    <div style={{ padding: 12, overflowX: 'auto' }} className="mai-cb-scroll">
      <table style={{ width: '100%', borderCollapse: 'separate', borderSpacing: 0, minWidth: 640 }}>
        <thead>
          <tr>
            <th style={{ textAlign: 'left', padding: '6px 8px', ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>#</th>
            <th style={{ textAlign: 'left', padding: '6px 8px', ...mono(8.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.06em' }}>Giocatore</th>
            {COLS.map(c => (
              <th key={c.id} style={{ textAlign: 'center', padding: '6px 8px', whiteSpace: 'nowrap' }}>
                <div style={{ display: 'inline-flex', alignItems: 'center', gap: 5, justifyContent: 'center' }}>
                  {c.comp && <R.ComputationBadge c={c.comp} size={15} />}
                  <div style={{ textAlign: 'left', lineHeight: 1.1 }}>
                    <div style={{ ...mono(9, 800, c.sub === 'primary' ? eHsl('game') : 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.04em' }}>{c.lb}</div>
                    <div style={{ ...mono(7.5, 700, c.sub === 'info' ? 'var(--text-muted)' : eHsl(c.e)), textTransform: 'uppercase', letterSpacing: '.05em' }}>{c.sub}</div>
                  </div>
                </div>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {SUM.standings.map(p => (
            <tr key={p.id} style={{ background: p.winner ? eHsl('toolkit', 0.08) : 'transparent' }}>
              <td style={{ padding: '9px 8px', borderTop: '1px solid var(--border-light)' }}>
                <span style={{ width: 22, height: 22, borderRadius: 'var(--r-pill)', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', background: p.winner ? eHsl('toolkit') : eHsl('session', 0.12), color: p.winner ? '#fff' : eHsl('session'), ...mono(11, 800) }}>{p.rank}</span>
              </td>
              <td style={{ padding: '9px 8px', borderTop: '1px solid var(--border-light)' }}>
                <div style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                  <F.MiniAvatar p={p} size={26} ring={p.winner} />
                  <span style={{ ...disp(13.5, 800, 'var(--text)') }}>{p.name}</span>
                  {p.winner && <span aria-hidden="true">🏆</span>}
                </div>
              </td>
              {COLS.map(c => (
                <td key={c.id} style={{ padding: '9px 8px', borderTop: '1px solid var(--border-light)', textAlign: 'center' }}>
                  <span style={{ ...disp(c.big ? 20 : 14, 800, c.big ? eHsl('game') : c.sub === 'info' ? 'var(--text-muted)' : 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{c.prefix || ''}{p[c.id]}</span>
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      <div style={{ ...mono(9, 700, 'var(--text-muted)'), marginTop: 10, lineHeight: 1.5, display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
        <R.ComputationBadge c="Count" size={14} /> primary · <R.ComputationBadge c="Sum" size={14} /> spareggio 1 · <R.ComputationBadge c="Custom" size={14} /> spareggio 2 · le altre colonne sono informative.
      </div>
    </div>
  </R.StateScaffold>
);

// ─── Step timeline ────────────────────────────────────────────────────────
const StepTimeline = () => (
  <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 0 }}>
    {SUM.steps.map((s, i) => (
      <div key={s.step} style={{ display: 'flex', gap: 12 }}>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flexShrink: 0 }}>
          <span style={{ width: 36, height: 36, borderRadius: '50%', background: eHsl('game'), color: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', ...disp(18, 800, '#fff'), boxShadow: `0 3px 10px ${eHsl('game', 0.4)}` }}>{s.step}</span>
          {i < SUM.steps.length - 1 && <span style={{ width: 2, flex: 1, minHeight: 28, background: eHsl('game', 0.3), margin: '4px 0' }} />}
        </div>
        <div style={{ flex: 1, paddingBottom: 18 }}>
          <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, flexWrap: 'wrap' }}>
            <span style={{ ...disp(15, 800, 'var(--text)') }}>Step {s.step}</span>
            <span style={{ ...mono(9, 800, eHsl('game')), padding: '1px 7px', borderRadius: 'var(--r-pill)', background: eHsl('game', 0.12), border: `1px solid ${eHsl('game', 0.3)}` }}>Round {s.round}</span>
            <span style={{ ...mono(9.5, 700, 'var(--text-sec)') }}>{s.trigger}</span>
          </div>
          <div style={{ ...mono(10, 600, 'var(--text-muted)'), marginTop: 5, lineHeight: 1.5 }}>{s.detail}</div>
        </div>
      </div>
    ))}
  </div>
);

// ─── Stats panel ──────────────────────────────────────────────────────────
const Bar = ({ value, max, e, label, suffix }) => (
  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
    <span style={{ ...mono(9.5, 700, 'var(--text-sec)'), width: 96, flexShrink: 0, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{label}</span>
    <div style={{ flex: 1, height: 16, borderRadius: 'var(--r-pill)', background: 'var(--bg-sunken)', overflow: 'hidden' }}>
      <div style={{ width: `${(value / max) * 100}%`, height: '100%', borderRadius: 'var(--r-pill)', background: eHsl(e), minWidth: 2 }} />
    </div>
    <span style={{ ...mono(10, 800, 'var(--text)'), width: 44, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>{suffix || ''}{value}</span>
  </div>
);
const StatsPanel = () => {
  const st = SUM.stats;
  const maxBid = Math.max(...st.avgBidByTier.map(t => t.avg));
  const maxSpend = Math.max(...st.spendByPlayer.map(p => p.plants + p.resources + p.connections));
  const SEG = [{ k: 'plants', lb: 'centrali', e: 'kb' }, { k: 'resources', lb: 'risorse', e: 'game' }, { k: 'connections', lb: 'connessioni', e: 'session' }];
  return (
    <div style={{ padding: 14, display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(120px, 1fr))', gap: 8 }}>
        {st.facts.map(f => (
          <div key={f.lb} style={{ padding: '9px 11px', borderRadius: 'var(--r-md)', background: 'var(--bg-muted)', border: '1px solid var(--border-light)' }}>
            <div style={{ ...disp(20, 800, 'var(--text)'), fontVariantNumeric: 'tabular-nums' }}>{f.v}</div>
            <div style={{ ...mono(8.5, 700, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.05em' }}>{f.lb}</div>
          </div>
        ))}
      </div>
      <div>
        <div style={{ ...mono(9.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 9 }}>Bid medio per fascia di centrale</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 7 }}>
          {st.avgBidByTier.map(t => <Bar key={t.tier} label={t.tier} value={t.avg} max={maxBid} e="session" suffix="€" />)}
        </div>
      </div>
      <div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 9, flexWrap: 'wrap' }}>
          <span style={{ ...mono(9.5, 800, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.07em' }}>Spesa Elektro per giocatore</span>
          <div style={{ display: 'flex', gap: 8 }}>
            {SEG.map(s => <span key={s.k} style={{ display: 'inline-flex', alignItems: 'center', gap: 4, ...mono(8.5, 700, 'var(--text-sec)') }}><span style={{ width: 9, height: 9, borderRadius: 2, background: eHsl(s.e) }} />{s.lb}</span>)}
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          {st.spendByPlayer.map(p => {
            const total = p.plants + p.resources + p.connections;
            return (
              <div key={p.id} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <F.MiniAvatar p={p} size={22} />
                <span style={{ ...disp(11.5, 800, 'var(--text)'), width: 52, flexShrink: 0 }}>{p.name}</span>
                <div style={{ flex: 1, height: 18, borderRadius: 'var(--r-pill)', overflow: 'hidden', display: 'flex', background: 'var(--bg-sunken)' }} aria-label={`${p.name} spesa totale €${total}`}>
                  {SEG.map(s => <div key={s.k} title={`${s.lb}: €${p[s.k]}`} style={{ width: `${(p[s.k] / maxSpend) * 100}%`, height: '100%', background: eHsl(s.e) }} />)}
                </div>
                <span style={{ ...mono(10, 800, 'var(--text)'), width: 44, textAlign: 'right', fontVariantNumeric: 'tabular-nums' }}>€{total}</span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};

// ─── Summary body (tabs) ──────────────────────────────────────────────────
const SUM_TABS = [
  { id: 'scoreboard', icon: '🏆', label: 'Scoreboard', entity: 'toolkit', render: (st) => <ScoreboardTable state={st} /> },
  { id: 'network',    icon: '🗺', label: 'Network',    entity: 'session', render: (st) => <P.NetworkMap state={st} h={340} /> },
  { id: 'steps',      icon: '③', label: 'Step',        entity: 'game',    render: () => <StepTimeline /> },
  { id: 'stats',      icon: '📊', label: 'Stats',      entity: 'player',  render: () => <StatsPanel /> },
];
const SummaryBody = ({ initial = 'scoreboard', states, embedded }) => {
  const [tab, setTab] = useState(initial);
  const [st, setSt] = useState('default');
  const active = SUM_TABS.find(t => t.id === tab) || SUM_TABS[0];
  const stateful = active.id === 'scoreboard' || active.id === 'network';
  return (
    <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, background: 'var(--bg-card)' }}>
      <div role="tablist" aria-label="Sezioni riepilogo" className="mai-cb-scroll" style={{ display: 'flex', borderBottom: '1px solid var(--border-light)', flexShrink: 0, overflowX: 'auto', background: 'var(--bg-card)' }}>
        {SUM_TABS.map(t => {
          const on = tab === t.id;
          return (
            <button key={t.id} type="button" role="tab" aria-selected={on} onClick={() => { setTab(t.id); setSt('default'); }} style={{ flex: '1 0 auto', padding: '11px 14px', background: on ? eHsl(t.entity, 0.06) : 'transparent', border: 'none', borderBottom: on ? `2px solid ${eHsl(t.entity)}` : '2px solid transparent', color: on ? eHsl(t.entity) : 'var(--text-sec)', ...disp(12.5, 800), cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6, whiteSpace: 'nowrap' }}>
              <span aria-hidden="true" style={{ fontSize: 14 }}>{t.icon}</span>{t.label}
            </button>
          );
        })}
      </div>
      {states && stateful && <window.SkeletonParts.StateSwitch value={st} onChange={setSt} />}
      <div role="tabpanel" className="mai-cb-scroll" style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>{active.render(st)}</div>
    </div>
  );
};

// ─── App ──────────────────────────────────────────────────────────────────
const StatesRow = ({ title, sub, entity, render }) => (
  <div style={{ marginBottom: 26 }}>
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
      <span style={{ ...disp(15, 800, eHsl(entity)) }}>{title}</span>
      {sub && <span style={{ ...mono(10, 700, 'var(--text-muted)') }}>{sub}</span>}
    </div>
    <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', paddingBottom: 10 }}>
      {P.STATES.map(s => <P.PanelFrame key={s.id} label={s.lb} entity={entity} dark={s.id === 'sse'} w={340}>{render(s.id)}</P.PanelFrame>)}
    </div>
  </div>
);

function App() {
  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">
        <div style={{ ...mono(11, 600, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Power Grid · Summary 🏁 · estensione skeleton</div>
        <h1>Session summary — Power Grid</h1>
        <p className="lead">
          Riepilogo post-partita per <code>/sessions/[id]/summary</code>. Hero con il vincitore e la metrica primaria
          (<strong>città alimentate</strong>, spareggio a Elektro), poi quattro tab:
          <strong> Scoreboard</strong> (breakdown primary/spareggio/info per giocatore),
          <strong> Network</strong> (snapshot finale della rete), <strong>Step</strong> (timeline dei 3 step di gioco) e
          <strong> Stats</strong> (bid medio per fascia, spesa Elektro per giocatore, meta-partita).
        </p>

        <div className="section-label">Hero · vincitore + metrica primaria</div>
        <SummaryHero />

        <div className="section-label">Interattivo · Desktop 1280 — tab Scoreboard / Network / Step / Stats</div>
        <window.SkeletonParts.DesktopFrame ds={PG.ds} dark label="Desktop · summary · Power Grid" url="meepleai.app/sessions/pg-7/summary" height={560}
          desc="Tab Scoreboard: tabella con città alimentate (primary), Elektro e capacità centrali (spareggi), più colonne informative. Tab Network: rete finale color-coded. Tab Step: i 3 step e quando sono scattati. Tab Stats: bid medio e spesa Elektro per categoria.">
          <window.SkeletonParts.TopBar ds={{ ...PG.ds, session: { elapsed: '2h 41min' } }} connection="offline" />
          <SummaryBody states />
        </window.SkeletonParts.DesktopFrame>

        <div className="section-label">Mobile 375 — hero + tab in colonna</div>
        <div className="phones-grid">
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
            <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>01 · Scoreboard</div>
            <div className="phone">
              <div className="phone-sbar" style={{ color: 'var(--text)' }}><span style={{ fontFamily: 'var(--f-mono)' }}>17:48</span><div className="ind"><span>●●●●</span><span>100%</span></div></div>
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', overflow: 'hidden', minHeight: 0 }}>
                <div style={{ padding: 10 }}><SummaryHero compact /></div>
                <SummaryBody initial="scoreboard" />
              </div>
            </div>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }} data-theme="dark">
            <div style={{ ...mono(11, 700, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.08em' }}>02 · Stats · dark</div>
            <div className="phone" data-theme="dark">
              <div className="phone-sbar" style={{ color: 'var(--text)' }}><span style={{ fontFamily: 'var(--f-mono)' }}>17:49</span><div className="ind"><span>●●●●</span><span>100%</span></div></div>
              <div style={{ flex: 1, display: 'flex', flexDirection: 'column', background: 'var(--bg)', overflow: 'hidden', minHeight: 0 }}>
                <div style={{ padding: 10 }}><SummaryHero compact /></div>
                <SummaryBody initial="stats" />
              </div>
            </div>
          </div>
        </div>

        <div className="section-label">Gallery stati · Scoreboard × 5 stati canonici</div>
        <StatesRow title="ScoreboardTable" sub="default · empty · loading · error · sse-disconnect" entity="toolkit"
          render={(st) => <div style={{ flex: 1, overflowY: 'auto' }}><ScoreboardTable state={st} /></div>} />
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; width: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; font-family: var(--f-mono); }
        @media (prefers-reduced-motion: reduce) { .mai-pulse-dot, .mai-shimmer { animation: none !important; } }
        .phone > div::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
