/* MeepleAI SP4 · Codenames — LIVE
   /sessions/[id]/live  — Codenames extension of the universal session skeleton.

   Codenames is the lone team-deduction / asymmetric-info archetype of the
   premium set: the Spymaster knows the key card, Operatives don't. The 5×5 word
   grid is the always-visible centerpiece; the mandatory chat/log accordion sits
   below it (one panel open at a time); the right column is the polymorphic tab
   stack (Scoring=Ranking · Board=key card · Indizi · Squadre · Chat).

   Below the live frames: full states gallery — every game-specific renderer
   (WordGrid · TeamPanel · ClueHistory) and the reused polymorphic renderers
   (ScoringPanel→Ranking · TurnIndicator→Sequential) × the 5 canonical states.

   Loads: sp4-parts-common.jsx · sp4-session-codenames-data.jsx ·
          sp4-session-skeleton-renderers.jsx · sp4-session-skeleton-parts.jsx ·
          sp4-session-codenames-flavor.jsx · sp4-session-codenames-parts.jsx ·
          sp4-session-codenames-bodies.jsx
   DEMO-NAV-HINTS: sp4-session-codenames-summary.html */

const S = window.SkeletonParts;
const R = window.SkeletonRenderers;
const M = window.MAI;
const CN = window.CN;
const P = window.CNParts;
const F = window.CNFlavor;
const eHsl = M.entityHsl;
const STATES = window.SkeletonData.STATES;
const { mono, disp } = F;

const GalleryRow = ({ title, sub, entity, children }) => (
  <div style={{ marginBottom: 26 }}>
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
      <span style={{ ...disp(15, 800, eHsl(entity)) }}>{title}</span>
      {sub && <span style={{ ...mono(10, 700, 'var(--text-muted)') }}>{sub}</span>}
    </div>
    <div className="mai-cb-scroll" style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 10 }}>{children}</div>
  </div>
);

const StatesOf = ({ entity, Renderer, render, h = 480 }) =>
  STATES.map((s) => (
    <P.PanelFrame key={s.id} label={s.lb} entity={entity} h={h}>
      {render ? render(s.id) : <Renderer data={CN.ds} state={s.id} />}
    </P.PanelFrame>
  ));

function Intro() {
  const cards = [
    { e: 'session', h: 'Archetipo unico', b: 'Unica deduzione a squadre con info asimmetrica del set premium: lo Spymaster vede la key card, gli Operatives no.' },
    { e: 'event',   h: 'Griglia 5×5 al centro', b: '25 carte parola sempre visibili. Toggle prospettiva Operativo / Spymaster + key card nascondibile per lo screen-share.' },
    { e: 'agent',   h: 'Chat / log / indizi in accordion', b: 'Colonna principale: WordGrid fissa + accordion a pannello singolo (Chat · Registro · Storico indizi), identico allo skeleton.' },
    { e: 'toolkit', h: '5 stati canonici', b: 'default · empty · loading · error · sse-disconnect su ogni renderer, riusando gli atomi MAI.' },
  ];
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12, margin: '4px 0 8px' }}>
      {cards.map((c) => (
        <div key={c.h} style={{ padding: 12, borderRadius: 'var(--r-lg)', background: eHsl(c.e, 0.06), border: `1px solid ${eHsl(c.e, 0.25)}` }}>
          <div style={{ ...disp(13, 800, eHsl(c.e)), marginBottom: 4 }}>{c.h}</div>
          <div style={{ fontFamily: 'var(--f-body)', fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5 }}>{c.b}</div>
        </div>
      ))}
    </div>
  );
}

function App() {
  return (
    <div className="stage">
      <S.ThemeToggle />
      <div className="stage-wrap">
        <div style={{ ...mono(11, 600, 'var(--text-muted)'), textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Codenames · Session live 🕵️</div>
        <h1>Codenames — session live (deduzione a squadre)</h1>
        <p className="lead">
          Estensione game-specific dello <strong>skeleton universale</strong> per <code>/sessions/[id]/live</code>.
          La struttura (scoring <strong>Ranking</strong>, turni <strong>Sequential</strong> a 4 fasi alternate, widget) è letta dal
          Toolkit JSON; sopra, i widget specifici di Codenames — <strong>griglia 5×5</strong>, <strong>key card Spymaster</strong>,
          tracker squadre, storico indizi. Dark = modalità primaria.
        </p>
        <Intro />

        {/* SIDE-BY-SIDE — Operative vs Spymaster perspective */}
        <div className="section-label">Side-by-side · Desktop 1280 — selettore scenario (mid-game · match point · assassino) · prospettiva Operativo vs Spymaster</div>
        <div className="sk-sxs">
          <S.DesktopFrame ds={CN.ds} label="Vista Operativo · mid-game" url="meepleai.app/sessions/cdn-7/live?view=operative" height={800}
            desc="Cambia scenario dal selettore in alto. Operativi: solo tessere rivelate colorate. Griglia 5×5 al centro, accordion Chat aperto, colonna destra su Scoring (Ranking · squadre).">
            <S.TopBar ds={CN.ds} connection="connected" />
            <P.DesktopBody initialTab="scoring" initialState="default" initialPerspective="operative" initialScenario="mid" />
          </S.DesktopFrame>
          <S.DesktopFrame ds={CN.ds} label="Vista Spymaster · match point Blu" url="meepleai.app/sessions/cdn-7/live?view=spymaster" height={800} dark
            desc="Scenario «match point»: al Blu manca 1 agente. Lo Spymaster vede i colori reali (tint + key card) e può nasconderli per lo screen-share. Passa a «Sconfitta · Assassino» per il banner di fine partita.">
            <S.TopBar ds={CN.ds} connection="connected" />
            <P.DesktopBody initialTab="board" initialState="default" initialPerspective="spymaster" initialScenario="match" />
          </S.DesktopFrame>
        </div>

        {/* MOBILE */}
        <div className="section-label">Mobile 375 — selettore scenario · griglia compatta · accordion · tab strip → bottom-sheet</div>
        <div className="phones-grid">
          <P.PhoneShell label="Operativo · mid-game" initialPerspective="operative" initialScenario="mid" desc="PhaseBanner sticky, indizio attuale, griglia 5×5 compatta, tracker squadre, accordion Chat/Registro/Indizi." />
          <P.PhoneShell label="Sconfitta Assassino · dark" dark initialPerspective="operative" initialScenario="assassin" desc="Scenario di fine partita: banner di sconfitta, tessera assassino rivelata sulla griglia, CTA verso il recap." />
        </div>

        {/* CHROME showcase — phase banner + current clue */}
        <div className="section-label">Chrome di turno — PhaseBanner (4 fasi alternate · aria-current) + indizio attuale</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12, marginBottom: 12 }}>
          <div style={{ borderRadius: 'var(--r-lg)', border: '1px solid var(--border)', overflow: 'hidden' }}><P.PhaseBanner /></div>
          <P.CurrentCluePanel />
        </div>

        {/* STATES GALLERY — game-specific renderers */}
        <div className="section-label">Gallery stati · WordGrid — 5×5 carte parola (operativo) × 5 stati canonici</div>
        <GalleryRow title="WordGrid" sub="griglia · covered/red/blue/neutral/assassin" entity="session">
          <StatesOf entity="session" h={520} render={(st) => <P.WordGrid state={st} compact perspective="operative" />} />
        </GalleryRow>

        <div className="section-label">Gallery stati · TeamPanel — tracker Rosso + Blu × 5 stati</div>
        <GalleryRow title="TeamPanel" sub="agenti rimasti · roster · turno corrente" entity="player">
          <StatesOf entity="player" h={420} render={(st) => <div style={{ padding: 12, overflowY: 'auto' }}><P.TeamPanel state={st} /></div>} />
        </GalleryRow>

        <div className="section-label">Gallery stati · ClueHistoryTimeline — indizi dati · success rate × 5 stati</div>
        <GalleryRow title="ClueHistoryTimeline" sub="storico · pieno/parziale/mancato · migliore/peggiore" entity="agent">
          <StatesOf entity="agent" h={420} render={(st) => <div style={{ padding: 12, overflowY: 'auto' }}><P.ClueHistoryTimeline state={st} /></div>} />
        </GalleryRow>

        {/* STATES GALLERY — reused polymorphic renderers */}
        <div className="section-label">Gallery stati · ScoringPanelRenderer (riuso) — scoreType <strong>Ranking</strong> (race condition) × 5 stati</div>
        <GalleryRow title="ScoringPanelRenderer · Ranking" sub="classifica squadre · agenti trovati + assassino" entity="session">
          <StatesOf entity="session" h={420} Renderer={R.ScoringPanelRenderer} />
        </GalleryRow>

        <div className="section-label">Gallery stati · TurnIndicatorRenderer (riuso) — turnOrderType <strong>Sequential</strong> (4 fasi alternate) × 5 stati</div>
        <GalleryRow title="TurnIndicatorRenderer · Sequential" sub="phase stepper a squadra" entity="player">
          <StatesOf entity="player" h={420} Renderer={R.TurnIndicatorRenderer} />
        </GalleryRow>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; width: 7px; }
        .mai-cb-scroll::-webkit-scrollbar-thumb { background: var(--border-strong); border-radius: 999px; }
        .sk-sxs { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 22px; align-items: start; }
        @media (max-width: 1100px) { .sk-sxs { grid-template-columns: 1fr; } }
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
