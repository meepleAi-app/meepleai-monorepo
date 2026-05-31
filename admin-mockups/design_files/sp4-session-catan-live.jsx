/* MeepleAI SP4 · Coloni di Catan — SESSION LIVE  (App)
   /sessions/[id]/live — Catan extension of the universal session skeleton.

   The skeleton (mockup #1) supplies the polymorphic renderers + universal chrome
   (TopBar · ChatAgentPanel · ActionLog · ScoringPanelRenderer · TurnIndicator ·
   ToolkitRenderer). THIS layer adds the Catan board: the 19-hex flat-top map with
   color-coded settlements / cities / roads, the robber, ports, the 2D6 dice + roll
   history, per-player state cards, the resource bank / dev deck, and a 5-tab right
   column (Punti · Scambi · Sviluppo · Costruisci · Agente).

   Reused verbatim from the skeleton: TopBar · ChatAgentPanel · ActionLogTimeline ·
   ScoringPanelRenderer (Points) · TurnIndicatorRenderer (RoundRobin clockwise) ·
   ToolkitRenderer (CounterTools dal JSON). Dark = primary mode.

   Loads: sp4-parts-common · skeleton-renderers · catan-data(+SkeletonData shim) ·
          skeleton-parts · catan-flavor · catan-parts · catan-live
   DEMO-NAV-HINTS: sp4-session-catan-summary.html
*/

const M = window.MAI;
const R = window.SkeletonRenderers;
const S = window.SkeletonParts;
const C = window.CATAN;
const F = window.CatanFlavor;
const P = window.CatanParts;
const eHsl = M.entityHsl;
const { useState, useEffect } = React;

function ThemeToggle() {
  const [dark, setDark] = useState(true);
  useEffect(() => { document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light'); }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle" aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

const Intro = () => (
  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12, margin: '4px 0 8px' }}>
    {[
      { e: 'session', h: 'Estende lo skeleton', b: 'Riusa TopBar · ChatAgent · ActionLog e i 3 renderer polimorfici. Aggiunge sopra il tabellone esagonale e lo stato dei giocatori.' },
      { e: 'game',    h: 'Tabellone a 19 esagomi', b: '4 legno · 4 lana · 4 grano · 3 argilla · 3 minerale · 1 deserto. Token di produzione 2-12, ladro, insediamenti/città/strade color-coded, porti.' },
      { e: 'player',  h: 'Stato per giocatore', b: 'Mano (visibile per il giocatore attivo, segreta per gli altri) · pezzi rimasti · carte sviluppo · PV pubblici · badge Strada+ / Armata+.' },
      { e: 'toolkit', h: '5 stati canonici', b: 'default · empty · loading · error · sse-disconnect per ogni pannello (punti, scambi, sviluppo, costruisci).' },
    ].map(c => (
      <div key={c.h} style={{ padding: 12, borderRadius: 'var(--r-lg)', background: eHsl(c.e, 0.06), border: `1px solid ${eHsl(c.e, 0.25)}` }}>
        <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: eHsl(c.e), marginBottom: 4 }}>{c.h}</div>
        <div style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5 }}>{c.b}</div>
      </div>
    ))}
  </div>
);

// states-gallery helper
const StatesRow = ({ title, sub, entity, render, h = 480 }) => (
  <div style={{ marginBottom: 26 }}>
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
      <span style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHsl(entity) }}>{title}</span>
      {sub && <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 700 }}>{sub}</span>}
    </div>
    <div className="mai-cb-scroll" style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 10 }}>
      {P.STATES.map(s => (
        <S.PanelFrame key={s.id} label={s.lb} entity={entity} dark={s.id === 'sse'} h={h}>
          {render(s.id)}
        </S.PanelFrame>
      ))}
    </div>
  </div>
);

// toolkit widgets derived from catan.json CounterTools (for the reused ToolkitRenderer)
const COUNTER_WIDGETS = [
  { id: 'w-res', type: 'ResourceManager', isEnabled: true, displayOrder: 0, config: { shared: true, resources: [
    { label: 'Legno', value: C.BANK.wood, max: 19 }, { label: 'Argilla', value: C.BANK.brick, max: 19 },
    { label: 'Lana', value: C.BANK.sheep, max: 19 }, { label: 'Grano', value: C.BANK.wheat, max: 19, danger: C.BANK.wheat <= 9 },
    { label: 'Minerale', value: C.BANK.ore, max: 19 },
  ] } },
  { id: 'w-score', type: 'ScoreTracker', isEnabled: true, displayOrder: 1, config: {} },
  { id: 'w-dice', type: 'RandomGenerator', isEnabled: true, displayOrder: 2, config: { name: 'Dadi di produzione', last: C.DICE.last[0] + C.DICE.last[1], quantity: 2, faces: ['1', '2', '3', '4', '5', '6'] } },
  { id: 'w-turn', type: 'TurnManager', isEnabled: false, displayOrder: 3, config: { phaseBased: false } },
];

function App() {
  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Coloni di Catan · Live ⚡ · estensione skeleton</div>
        <h1>Session live — Coloni di Catan</h1>
        <p className="lead">
          Estensione game-specific dello <strong>skeleton universale</strong> applicata a <strong>Coloni di Catan</strong>
          (euro medium con trading, 3-4 giocatori, ~80 min). Lo skeleton fornisce i renderer polimorfici e la chrome;
          questo layer aggiunge il <strong>tabellone esagonale a 19 caselle</strong>, il <strong>ladro</strong>, i
          <strong> 2D6</strong> con cronologia, lo <strong>stato per giocatore</strong> e una colonna destra a 5 tab
          <strong> Punti · Scambi · Sviluppo · Costruisci · Agente</strong>. Dark = modalità primaria.
        </p>
        <Intro />

        {/* INTERACTIVE — desktop */}
        <div className="section-label">Interattivo · Desktop 1280 — rail giocatori · tabellone + dadi + banca · colonna tab</div>
        <S.DesktopFrame ds={C.ds} dark label="Desktop · live · Coloni di Catan" url="meepleai.app/sessions/catan-7/live?tab=scores" height={780}
          desc="3 regioni + tab: SINISTRA stato dei giocatori (Marco attivo, mano visibile; avversari con mano segreta) · CENTRO turno + 2D6 con cronologia · tabellone 19 esagoni con produzione evidenziata (8) · banca + mazzo sviluppo · registro azioni · DESTRA tab Punti/Scambi/Sviluppo/Costruisci/Agente con selettore stato.">
          <S.TopBar ds={C.ds} connection="connected" />
          <P.DesktopBody ds={C.ds} initialTab="scoring" initialState="default" />
        </S.DesktopFrame>

        {/* INTERACTIVE — mobile */}
        <div className="section-label">Mobile 375 — tabellone scrollabile · rail giocatori collassabile · sezioni in bottom-sheet</div>
        <div className="phones-grid">
          <P.PhoneShell label="01 · base · sheet chiuso" desc="Tabellone + dadi sempre visibili. In basso: rail giocatori collassabile + strip tab scrollabile." />
          <P.PhoneShell label="02 · sheet Scambi" initialTab="trade" desc="Tap su una tab apre il bottom-sheet: offerte player-to-player + banca/porti, con selettore stato." />
          <P.PhoneShell label="03 · sheet Punti · dark" dark initialTab="scoring" desc="ScoringPanelRenderer riusato: classifica live + breakdown 5 categorie (insediamenti/città/strada+/armata+/carte PV)." />
        </div>

        {/* STATES — reused polymorphic renderers fed by catan.json */}
        <div className="section-label">Gallery stati · renderer polimorfici riusati — alimentati dal toolkit Catan</div>
        <StatesRow title="ScoringPanelRenderer" sub="scoreType · Points — 5 categorie dal JSON (Count · Custom · Sum)" entity="session"
          render={(st) => <R.ScoringPanelRenderer data={C.ds} state={st} />} />
        <StatesRow title="TurnIndicatorRenderer" sub="turnOrderType · RoundRobin — giocatore attivo + ordine orario" entity="player"
          render={(st) => <R.TurnIndicatorRenderer data={C.ds} state={st} />} />
        <StatesRow title="ToolkitRenderer" sub="dispatch widgets[] — CounterTools del JSON (risorse · punti · dadi)" entity="toolkit"
          render={(st) => <R.ToolkitRenderer widgets={COUNTER_WIDGETS} players={C.PLAYERS} state={st} />} />

        {/* STATES — Catan-specific panels */}
        <div className="section-label">Gallery stati · pannelli specifici Catan × 5 stati canonici</div>
        <StatesRow title="TradePanel" sub="offerte player-to-player + banca 4:1 / porti 3:1 · 2:1" entity="chat"
          render={(st) => <P.TradePanel state={st} />} />
        <StatesRow title="DevCardsPanel" sub="mano segreta + cavalieri giocati + tipi nel mazzo" entity="toolkit"
          render={(st) => <P.DevCardsPanel state={st} />} />
        <StatesRow title="BuildPanel" sub="costi di costruzione + pezzi rimasti" entity="game"
          render={(st) => <P.BuildPanel state={st} />} />

        {/* COMPONENT SHOWCASE */}
        <div className="section-label">Componenti Catan · tabellone · dadi · ladro · stato giocatore</div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 18, alignItems: 'start' }}>
          <div>
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>HexBoard · produzione 8 evidenziata</div>
            <P.SectionCard title="Tabellone · 19 esagoni" entity="session" accent icon="🗺️" pad={10}>
              <F.HexBoard R={40} highlightNumber={8} />
            </P.SectionCard>
          </div>
          <div data-theme="dark">
            <div style={{ fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800, color: 'var(--text-sec)', textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>RobberBanner · 7 sui dadi · dark</div>
            <div style={{ background: 'var(--bg)', padding: 12, borderRadius: 'var(--r-lg)', display: 'flex', flexDirection: 'column', gap: 12 }}>
              <P.RobberBanner mode="discard" />
              <P.RobberBanner mode="move" />
              <P.SectionCard title="Stato giocatore · attivo" entity="session" accent>
                <P.PlayerStateCard p={C.PLAYERS[0]} active />
              </P.SectionCard>
            </div>
          </div>
        </div>
        <div style={{ marginTop: 18 }}>
          <P.SectionCard title="Stato giocatori · 4 plance (Marco attivo)" entity="session" accent>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(230px, 1fr))', gap: 12 }}>
              {C.PLAYERS.map(p => <P.PlayerStateCard key={p.id} p={p} active={p.current} />)}
            </div>
          </P.SectionCard>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        @keyframes catan-hot-anim { 0%,100% { filter: drop-shadow(0 0 5px hsl(var(--c-event)/.9)) drop-shadow(0 0 11px hsl(var(--c-event)/.55)); } 50% { filter: drop-shadow(0 0 8px hsl(var(--c-event)/1)) drop-shadow(0 0 20px hsl(var(--c-event)/.8)); } }
        .catan-hot { animation: catan-hot-anim 1.7s ease-in-out infinite; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .phones-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(380px, 1fr)); gap: var(--s-7); align-items: start; }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage code { background: var(--bg-muted); padding: 1px 6px; border-radius: var(--r-sm); font-size: .85em; font-family: var(--f-mono); }
        @media (prefers-reduced-motion: reduce) { .mai-pulse-dot, .mai-shimmer, .catan-hot { animation: none !important; } }
        .phone > div::-webkit-scrollbar { display: none; }
        button:focus-visible, a:focus-visible, input:focus-visible, textarea:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
