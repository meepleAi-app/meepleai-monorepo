/* MeepleAI SP4 · Puerto Rico — SESSION LIVE  (App)
   /sessions/[id]/live  — Puerto Rico extension of the universal session skeleton.

   PREMIUM mockup #? of pipeline B19. The skeleton (mockup #1) supplies the
   polymorphic renderers + universal chrome; THIS layer adds the Puerto Rico
   board: per-player mats, the 8-card role-selection board, galleons/trading
   house/colonist ship shared state, and a 5-tab right column
   (Scoring · Roles · Trade · Ship · Chat).

   Reused verbatim from the skeleton: TopBar · ChatAgentPanel · ActionLog ·
   ScoringPanelRenderer (Points) · TurnIndicatorRenderer (Sequential) ·
   ToolkitRenderer. Dark = primary mode. Default variant = 4 players, Round 4,
   current phase = Captain.

   Loads: sp4-parts-common · skeleton-renderers · skeleton-data(shim) ·
          skeleton-parts · puerto-rico-data · puerto-rico-flavor · puerto-rico-parts
   DEMO-NAV-HINTS: sp4-session-puerto-rico-summary.html
*/

const M = window.MAI;
const R = window.SkeletonRenderers;
const S = window.SkeletonParts;
const F = window.PRFlavor;
const P = window.PRParts;
const PR = window.PR;
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
      { e: 'session', h: 'Estende lo skeleton', b: 'Riusa TopBar · ChatAgent · ActionLog e i 3 renderer polimorfici. Aggiunge sopra le plance e il tavolo di Puerto Rico.' },
      { e: 'game',    h: 'Selezione ruoli', b: '8 carte ruolo (Prospector 2 solo a 5 giocatori). Il ruolo della fase corrente è evidenziato; i ruoli non scelti accumulano dobloni.' },
      { e: 'player',  h: 'Plance per giocatore', b: 'Plancia del giocatore attivo sempre visibile; le altre in drawer. Dobloni · coloni · PV · 5 magazzini · piantagioni · edifici.' },
      { e: 'toolkit', h: '5 stati canonici', b: 'default · empty · loading · error · sse-disconnect per ogni pannello (galeoni, casa commerciale, scoring, ruoli).' },
    ].map(c => (
      <div key={c.h} style={{ padding: 12, borderRadius: 'var(--r-lg)', background: eHsl(c.e, 0.06), border: `1px solid ${eHsl(c.e, 0.25)}` }}>
        <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: eHsl(c.e), marginBottom: 4 }}>{c.h}</div>
        <div style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5 }}>{c.b}</div>
      </div>
    ))}
  </div>
);

// states-gallery helper
const StatesRow = ({ title, sub, entity, render }) => (
  <div style={{ marginBottom: 26 }}>
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
      <span style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHsl(entity) }}>{title}</span>
      {sub && <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 700 }}>{sub}</span>}
    </div>
    <div className="mai-cb-scroll" style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 10 }}>
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
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Puerto Rico · Live ⚡ · estensione skeleton</div>
        <h1>Session live — Puerto Rico</h1>
        <p className="lead">
          Estensione game-specific dello <strong>skeleton universale</strong> applicata a <strong>Puerto Rico</strong>
          (euro a selezione ruoli, 3-5 giocatori, ~120 min). Lo skeleton fornisce i renderer polimorfici e la chrome;
          questo layer aggiunge <strong>plance per giocatore</strong>, il <strong>tavolo di selezione ruoli</strong>,
          lo stato condiviso (galeoni · casa commerciale · nave coloni) e una colonna destra a 5 tab
          <strong> Scoring · Roles · Trade · Ship · Chat</strong>. Dark = modalità primaria.
        </p>
        <Intro />

        {/* INTERACTIVE — desktop */}
        <div className="section-label">Interattivo · Desktop 1280 — plancia attiva · tavolo ruoli + stato condiviso · colonna tab</div>
        <S.DesktopFrame ds={PR.ds} dark label="Desktop · live · Puerto Rico" url="meepleai.app/sessions/pr-7/live?tab=ship" height={760}
          desc="3 regioni + tab: SINISTRA plancia del giocatore attivo (Marco) + plance altrui in drawer · CENTRO selezione ruoli (Capitano evidenziato) + galeoni/rifornimenti/coloni/edifici + registro · DESTRA tab Scoring/Roles/Trade/Ship/Chat con selettore stato.">
          <S.TopBar ds={PR.ds} connection="connected" />
          <P.DesktopBody initialTab="ship" initialState="default" />
        </S.DesktopFrame>

        {/* INTERACTIVE — mobile */}
        <div className="section-label">Mobile 375 — tavolo full-width · plance e sezioni in bottom-sheet</div>
        <div className="phones-grid">
          <P.PhoneShell label="01 · base · sheet chiuso" desc="Tavolo sempre visibile (ruoli + galeoni + rifornimenti + log). In basso: barra plance + strip tab." />
          <P.PhoneShell label="02 · sheet Ship" initialTab="ship" desc="Tap su una tab apre il bottom-sheet: galeoni con obbligo di spedizione e selettore stato." />
          <P.PhoneShell label="03 · sheet Scoring · dark" dark initialTab="scoring" desc="ScoringPanelRenderer riusato: classifica live + breakdown 3 categorie con badge computation." />
        </div>

        {/* STATES — reused polymorphic renderers */}
        <div className="section-label">Gallery stati · renderer polimorfici riusati — alimentati dal toolkit Puerto Rico</div>
        <StatesRow title="ScoringPanelRenderer" sub="scoreType · Points — 3 categorie dal JSON (Sum · Count · Custom)" entity="session"
          render={(st) => <R.ScoringPanelRenderer data={PR.ds} state={st} />} />
        <StatesRow title="TurnIndicatorRenderer" sub="turnOrderType · Sequential — phase stepper sui ruoli del round" entity="player"
          render={(st) => <R.TurnIndicatorRenderer data={PR.ds} state={st} />} />
        <StatesRow title="ToolkitRenderer" sub="dispatch widgets[] — CounterTools del JSON (dadi/timer esclusi)" entity="toolkit"
          render={(st) => <R.ToolkitRenderer widgets={PR.counterWidgets} players={PR.roster} state={st} />} />

        {/* STATES — PR-specific panels */}
        <div className="section-label">Gallery stati · pannelli specifici Puerto Rico × 5 stati canonici</div>
        <StatesRow title="GalleonsShipping" sub="fase Capitano · galeoni mono-merce + obbligo" entity="toolkit"
          render={(st) => <P.GalleonsShipping state={st} />} />
        <StatesRow title="TradingHouseSlots" sub="fase Mercante · 4 slot + prezzi" entity="chat"
          render={(st) => <P.TradingHouseSlots state={st} />} />
        <StatesRow title="RolesTab" sub="board compatto + sequenza fasi (TurnIndicator)" entity="player"
          render={(st) => <P.RolesTab state={st} />} />

        {/* PR component showcase (default state, light + dark) */}
        <div className="section-label">Componenti Puerto Rico · plancia giocatore · board ruoli</div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: 18, alignItems: 'start' }}>
          <div>
            <div style={{ ...F.mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>PlayerMat · attivo</div>
            <P.SectionCard title="Plancia · Marco" entity="session" accent>
              <P.PlayerMat player={PR.roster[0]} active tileSize={28} bw={80} />
            </P.SectionCard>
          </div>
          <div data-theme="dark">
            <div style={{ ...F.mono(10, 800, 'var(--text-sec)'), textTransform: 'uppercase', letterSpacing: '.07em', marginBottom: 8 }}>PlayerMat · dark</div>
            <div style={{ background: 'var(--bg)', padding: 12, borderRadius: 'var(--r-lg)' }}>
              <P.SectionCard title="Plancia · Anna" entity="session" accent>
                <P.PlayerMat player={PR.roster[1]} tileSize={28} bw={80} />
              </P.SectionCard>
            </div>
          </div>
        </div>
        <div style={{ marginTop: 18 }}>
          <P.SectionCard title="Tavolo · selezione ruoli (8 carte · 4 giocatori)" entity="session" accent>
            <P.RoleSelectionBoard />
          </P.SectionCard>
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot, .mai-pulse-dot-host::before { }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; }
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
