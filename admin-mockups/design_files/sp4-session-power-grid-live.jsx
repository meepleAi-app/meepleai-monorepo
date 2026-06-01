/* MeepleAI SP4 · Power Grid — SESSION LIVE  (App)
   /sessions/[id]/live — estensione game-specific dello skeleton universale
   applicata a Power Grid (euro auction + network building, 2-6 giocatori, ~120 min).

   PREMIUM mockup. Lo skeleton (mockup #1) fornisce i renderer polimorfici +
   la chrome universale; QUESTO layer aggiunge la board di Power Grid:
   PhaseIndicator (5 fasi + Step), TurnOrderStrip reverse-aware, mercato 8
   centrali, mercato risorse a scarsità, AuctionOverlay, NetworkMap e una
   colonna destra a 5 tab (Scoring · Market · Network · Plants · Chat).

   MECCANICA CHIAVE: l'ordine si RICALCOLA per città e si RIBALTA nelle fasi
   2-Asta / 3-Risorse (leader gioca ULTIMO).

   Riusa verbatim: TopBar · ChatAgentPanel · ActionLog · ScoringPanelRenderer
   (Points) · TurnIndicatorRenderer (Sequential) · ToolkitRenderer. Dark = primaria.

   Carica: sp4-parts-common · power-grid-data(shim SkeletonData) · skeleton-renderers
           · skeleton-parts · power-grid-flavor · power-grid-parts · QUESTO file.
   DEMO-NAV-HINTS: sp4-session-power-grid-summary.html
*/

const M = window.MAI;
const R = window.SkeletonRenderers;
const S = window.SkeletonParts;
const PG = window.PG;
const F = window.PGFlavor;
const P = window.PGParts;
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
      { e: 'session', h: 'Estende lo skeleton', b: 'Riusa TopBar · ChatAgent · ActionLog e i 3 renderer polimorfici. Aggiunge sopra la board di Power Grid: fasi, mercati, rete.' },
      { e: 'event',   h: 'Ordine ribaltato', b: 'L\u2019ordine si ricalcola per città e si RIBALTA nelle fasi Asta e Risorse: il leader gioca per ultimo. Indicatore ⇄ sempre visibile.' },
      { e: 'game',    h: 'Mercati & Step', b: '8 centrali (4 attuali + 4 future), mercato risorse a scarsità con prossimo prezzo, e i 3 Step di gioco scanditi dal conteggio città.' },
      { e: 'toolkit', h: '5 stati canonici', b: 'default · empty · loading · error · sse-disconnect per ogni pannello (mercato centrali, risorse, rete, scoring, centrali).' },
    ].map(c => (
      <div key={c.h} style={{ padding: 12, borderRadius: 'var(--r-lg)', background: eHsl(c.e, 0.06), border: `1px solid ${eHsl(c.e, 0.25)}` }}>
        <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: eHsl(c.e), marginBottom: 4 }}>{c.h}</div>
        <div style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5 }}>{c.b}</div>
      </div>
    ))}
  </div>
);

const StatesRow = ({ title, sub, entity, render }) => (
  <div style={{ marginBottom: 26 }}>
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
      <span style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHsl(entity) }}>{title}</span>
      {sub && <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 700 }}>{sub}</span>}
    </div>
    <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', paddingBottom: 10 }}>
      {P.STATES.map(s => (
        <P.PanelFrame key={s.id} label={s.lb} entity={entity} dark={s.id === 'sse'}>
          {render(s.id)}
        </P.PanelFrame>
      ))}
    </div>
  </div>
);

function App() {
  return (
    <div className="stage">
      <ThemeToggle />
      <div className="stage-wrap">
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Power Grid · Live ⚡ · estensione skeleton</div>
        <h1>Session live — Power Grid</h1>
        <p className="lead">
          Estensione game-specific dello <strong>skeleton universale</strong> applicata a <strong>Power Grid</strong>
          (euro ad asta + costruzione rete, 2-6 giocatori, ~120 min). Lo skeleton fornisce i renderer polimorfici e
          la chrome; questo layer aggiunge l\u2019<strong>indicatore di fase</strong> (5 fasi + Step), la
          <strong> turn-order strip reverse-aware</strong>, il <strong>mercato delle 8 centrali</strong>, il
          <strong> mercato risorse a scarsità</strong>, l\u2019<strong>overlay d\u2019asta</strong> e la
          <strong> rete della Germania</strong>, più una colonna destra a 5 tab. Dark = modalità primaria.
        </p>
        <Intro />

        {/* INTERACTIVE — desktop */}
        <div className="section-label">Interattivo · Desktop 1280 — fasi + ordine ⇄ · rail giocatore · mercati + asta · colonna tab</div>
        <S.DesktopFrame ds={PG.ds} dark label="Desktop · live · Power Grid" url="meepleai.app/sessions/pg-7/live?tab=market" height={820}
          desc="Top: PhaseIndicator (fase 2 Asta attiva, Step 2, Round 5) + TurnOrderStrip ribaltata (Marco leader → ultimo). SINISTRA plancia del giocatore attivo (Elektro · risorse · centrali · città; clic sugli altri per cambiarlo). CENTRO overlay asta + mercato 8 centrali + mercato risorse + log. DESTRA tab Scoring/Market/Network/Plants/Chat con selettore stato.">
          <S.TopBar ds={PG.ds} connection="connected" />
          <P.DesktopBody initialTab="market" initialState="default" />
        </S.DesktopFrame>

        {/* INTERACTIVE — mobile */}
        <div className="section-label">Mobile 375 — phase indicator sticky · board scroll · tab → bottom-sheet</div>
        <div className="phones-grid">
          <P.PhoneShell label="01 · base · sheet chiuso" desc="PhaseIndicator sticky in alto, ordine ribaltato, board scrollabile (asta + mercati in scroll orizzontale). In basso strip tab." />
          <P.PhoneShell label="02 · sheet Market" initialTab="market" desc="Tap su una tab apre il bottom-sheet: mercato centrali + mercato risorse + Step, con selettore stato." />
          <P.PhoneShell label="03 · sheet Network · dark" dark initialTab="network" desc="NetworkMap nella tab dedicata: rete Germania con città color-coded per owner e costi connessione." />
        </div>

        {/* STATES — reused polymorphic renderers */}
        <div className="section-label">Gallery stati · renderer polimorfici riusati — alimentati dal toolkit Power Grid</div>
        <StatesRow title="ScoringPanelRenderer" sub="scoreType · Points — città alimentate + 3 categorie dal JSON (Count · Sum · Custom)" entity="session"
          render={(st) => <R.ScoringPanelRenderer data={PG.ds} state={st} />} />
        <StatesRow title="TurnIndicatorRenderer" sub="turnOrderType · Sequential — phase stepper sulle 5 fasi del round" entity="player"
          render={(st) => <R.TurnIndicatorRenderer data={PG.ds} state={st} />} />
        <StatesRow title="ToolkitRenderer" sub="dispatch widgets[] — CounterTools del JSON (dadi/timer esclusi)" entity="toolkit"
          render={(st) => <R.ToolkitRenderer widgets={PG.counterWidgets} players={PG.ds.players} state={st} />} />

        {/* STATES — PG-specific panels */}
        <div className="section-label">Gallery stati · pannelli specifici Power Grid × 5 stati canonici</div>
        <StatesRow title="PowerPlantMarket" sub="8 carte · Current + Future · 2 righe" entity="game"
          render={(st) => <P.PowerPlantMarket state={st} compact withStep />} />
        <StatesRow title="ResourceMarket" sub="4 colonne con scarsità + prossimo prezzo" entity="kb"
          render={(st) => <P.ResourceMarket state={st} compact />} />
        <StatesRow title="NetworkMap" sub="rete Germania · owner coloring + highlight raggiungibili" entity="session"
          render={(st) => <div style={{ flex: 1, overflowY: 'auto' }}><P.NetworkMap state={st} h={250} showLegend={false} /></div>} />
        <StatesRow title="PlantsTab" sub="dettaglio delle 3 centrali del giocatore" entity="kb"
          render={(st) => <P.PlantsTab state={st} />} />

        {/* PG component showcase */}
        <div className="section-label">Componenti Power Grid · indicatore fase · ordine ribaltato · overlay asta · carta centrale</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <P.SectionCard title="PhaseIndicator + TurnOrderStrip" entity="session" accent flush>
            <P.PhaseIndicator />
            <P.TurnOrderStrip />
          </P.SectionCard>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 16, alignItems: 'start' }}>
            <div>
              <div style={{ ...F.mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>AuctionOverlay · fase 2</div>
              <P.AuctionOverlay />
            </div>
            <div data-theme="dark">
              <div style={{ ...F.mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>AuctionOverlay · dark</div>
              <div style={{ background: 'var(--bg)', padding: 12, borderRadius: 'var(--r-lg)' }}>
                <P.AuctionOverlay compact />
              </div>
            </div>
          </div>

          <div>
            <div style={{ ...F.mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>PlantCard · tipi combustibile (eco · ibrida · carbone · uranio · posseduta)</div>
            <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
              <F.PlantCard plant={PG.PLANTS.p13} />
              <F.PlantCard plant={PG.PLANTS.p21} buyable />
              <F.PlantCard plant={PG.PLANTS.p20} />
              <F.PlantCard plant={PG.PLANTS.p23} />
              <F.PlantCard plant={PG.roster[0].plants[0]} loaded={PG.roster[0].plants[0].loaded} />
              <F.PlantCard plant={PG.PLANTS.p27} future />
            </div>
          </div>

          <P.SectionCard title="NetworkMap · rete Germania" entity="session" accent flush>
            <P.NetworkMap />
          </P.SectionCard>
        </div>
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
        button:focus-visible, a:focus-visible, input:focus-visible, textarea:focus-visible { outline: 2px solid hsl(var(--c-session)); outline-offset: 2px; }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
