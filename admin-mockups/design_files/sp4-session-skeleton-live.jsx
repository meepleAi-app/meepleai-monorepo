/* MeepleAI SP4 — Generic Session Skeleton · LIVE
   /sessions/[id]/live  — game-AGNOSTIC template.

   The SAME skeleton renders any game by reading the Toolkit JSON. To prove it,
   two opposite datasets are rendered SIDE-BY-SIDE, live & interactive:
     A · Wingspan — Points · RoundRobin · 4 round · 6 categorie
     B · Paleo    — BinaryWin · Simultaneous · co-op · no punteggio

   Layout (desktop): LEFT 60% = ChatAgent (sempre visibile) + action log ·
   RIGHT 40% = RightColumnTabs polimorfico (Score/Turni/Widget/Note).
   Mobile 375: main full-width, side → bottom-sheet drawer.

   Below: full states gallery — ogni renderer polimorfico × 5 stati canonici
   (default · empty · loading · error · sse-disconnect).

   Loads: sp4-parts-common.jsx · sp4-session-skeleton-data.jsx ·
          sp4-session-skeleton-renderers.jsx · sp4-session-skeleton-parts.jsx
   DEMO-NAV-HINTS: sp4-session-summary-skeleton.html
*/

const D = window.SkeletonData;
const S = window.SkeletonParts;
const R = window.SkeletonRenderers;
const M = window.MAI;
const eHsl = M.entityHsl;
const WING = D.DATASETS.wingspan;
const PALEO = D.DATASETS.paleo;
const STATES = D.STATES;

// ─── gallery building blocks ───────────────────────────────────────────────
const GalleryRow = ({ title, sub, entity, children }) => (
  <div style={{ marginBottom: 26 }}>
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
      <span style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHsl(entity) }}>{title}</span>
      {sub && <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 700 }}>{sub}</span>}
    </div>
    <div className="mai-cb-scroll" style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 10 }}>{children}</div>
  </div>
);

const StatesOf = ({ entity, data, Renderer, h = 470 }) =>
  STATES.map(s => (
    <S.PanelFrame key={s.id} label={s.lb} entity={entity} h={h}>
      <Renderer data={data} state={s.id} players={data.players} />
    </S.PanelFrame>
  ));

// scoring branches: Points(Wing) · Ranking(fix) · BinaryWin(Paleo) · Objectives(fix)
const SCORING_BRANCHES = [
  { key: 'Points',     title: 'Points',     sub: 'scoreType · lista categorie + computation', data: WING },
  { key: 'Ranking',    title: 'Ranking',    sub: 'scoreType · classifica con rank pill',      data: D.scoringFixtures.Ranking },
  { key: 'BinaryWin',  title: 'BinaryWin',  sub: 'scoreType · esito collettivo (no punti)',   data: PALEO },
  { key: 'Objectives', title: 'Objectives', sub: 'scoreType · checklist obiettivi',           data: D.scoringFixtures.Objectives },
];

// turn branches: all 6
const TURN_BRANCHES = [
  { key: 'RoundRobin',   title: 'RoundRobin',   sub: 'active player + Round X/Y',        data: WING },
  { key: 'Sequential',   title: 'Sequential',   sub: 'phase stepper a squadra',          data: { ...D.turnFixtures.Sequential, players: [] } },
  { key: 'Simultaneous', title: 'Simultaneous', sub: 'tutti evidenziati (co-op)',        data: PALEO },
  { key: 'Realtime',     title: 'Realtime',     sub: 'warning banner · nessun turno',    data: { ...D.turnFixtures.Realtime, players: [] } },
  { key: 'None',         title: 'None',         sub: 'banner gioco libero',              data: { ...D.turnFixtures.None, players: [] } },
  { key: 'Custom',       title: 'Custom / Free', sub: 'phase stepper generico',          data: { ...D.turnFixtures.Custom, players: [] } },
];

// widget dispatcher fixture (all 6 types enabled)
const WIDGET_ALL = { widgets: D.widgetFixtures, players: WING.players };

function Intro() {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12, margin: '4px 0 8px' }}>
      {[
        { e: 'session', h: 'Skeleton universale', b: 'Un solo template renderizza qualsiasi gioco del catalogo leggendo la struttura dal Toolkit/Toolbox API. Zero dati hard-coded.' },
        { e: 'toolkit', h: '3 renderer polimorfici', b: 'ScoringPanel (switch scoreType) · TurnIndicator (switch turnOrderType) · ToolkitRenderer (dispatch widgets[]).' },
        { e: 'agent', h: 'ChatAgent come magnete', b: 'La chat con l\u2019agente è sempre visibile nella colonna principale — non un toggle secondario.' },
        { e: 'event', h: '5 stati canonici', b: 'default · empty · loading · error · sse-disconnect per ogni componente principale.' },
      ].map(c => (
        <div key={c.h} style={{ padding: 12, borderRadius: 'var(--r-lg)', background: eHsl(c.e, 0.06), border: `1px solid ${eHsl(c.e, 0.25)}` }}>
          <div style={{ fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800, color: eHsl(c.e), marginBottom: 4 }}>{c.h}</div>
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
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8 }}>SP4 · Generic Session Skeleton · Live ⚡</div>
        <h1>Session live — skeleton generico (qualsiasi gioco)</h1>
        <p className="lead">
          Template universale per <code>/sessions/[id]/live</code>. La struttura della sessione è letta dal
          <strong> Toolkit JSON</strong> (scoring · turn · widgets) — nessun dato specifico del gioco nel codice.
          Sotto, lo <strong>stesso skeleton</strong> è applicato a due dataset opposti, affiancati: <strong>Wingspan</strong> (engine builder)
          e <strong>Paleo</strong> (co-op simultaneo). Dark = modalità primaria.
        </p>
        <Intro />

        {/* SIDE-BY-SIDE — both datasets, interactive */}
        <div className="section-label">Side-by-side · Desktop 1280 — stesso skeleton, 2 giochi · cambia tab e stato dai selettori</div>
        <div className="sk-sxs">
          <S.DesktopFrame ds={WING} label="Dataset A · Wingspan — Points · RoundRobin" url="meepleai.app/sessions/wing-3/live?tab=scores" height={660}
            desc="Engine builder: tab Score → breakdown 6 categorie con icona computation; tab Turni → giocatore attivo + Round 2/4; tab Widget → dadi mangiatoia, risorse, score tracker.">
            <S.TopBar ds={WING} connection="connected" />
            <S.DesktopSessionBody ds={WING} initialTab="scoring" initialState="default" />
          </S.DesktopFrame>
          <S.DesktopFrame ds={PALEO} label="Dataset B · Paleo — BinaryWin · Simultaneous" url="meepleai.app/sessions/paleo-1/live?tab=scores" height={660}
            desc="Co-op simultaneo: tab Score → esito collettivo (pittura vs teschi, niente punti); tab Turni → tutti evidenziati + fasi Mattina/Giorno/Notte; tab Widget → risorse condivise, lavagna.">
            <S.TopBar ds={PALEO} connection="connected" />
            <S.DesktopSessionBody ds={PALEO} initialTab="scoring" initialState="default" />
          </S.DesktopFrame>
        </div>

        {/* MOBILE — both datasets */}
        <div className="section-label">Mobile 375 — main full-width · side → bottom-sheet drawer</div>
        <div className="phones-grid">
          <S.PhoneShell ds={WING} label="A · Wingspan · base" desc="ChatAgent + action log sempre visibili. Strip tab in basso, scrollabile." />
          <S.PhoneShell ds={WING} label="A · Wingspan · sheet Score" initialTab="scoring" desc="Tap su una tab apre il bottom-sheet con handle, header entity-color, selettore stato e renderer." />
          <S.PhoneShell ds={PALEO} label="B · Paleo · sheet Turni · dark" dark initialTab="turn" desc="Stesso drawer, dataset co-op: TurnIndicator rende il ramo Simultaneous." />
        </div>

        {/* STATES GALLERY — SCORING */}
        <div className="section-label">Gallery stati · ScoringPanelRenderer — switch su scoreType × 5 stati canonici</div>
        {SCORING_BRANCHES.map(b => (
          <GalleryRow key={b.key} title={b.title} sub={b.sub} entity="session">
            <StatesOf entity="session" data={b.data} Renderer={R.ScoringPanelRenderer} />
          </GalleryRow>
        ))}

        {/* STATES GALLERY — TURN */}
        <div className="section-label">Gallery stati · TurnIndicatorRenderer — switch su turnOrderType (tutti e 6) × 5 stati</div>
        {TURN_BRANCHES.map(b => (
          <GalleryRow key={b.key} title={b.title} sub={b.sub} entity="player">
            <StatesOf entity="player" data={b.data} Renderer={R.TurnIndicatorRenderer} />
          </GalleryRow>
        ))}

        {/* STATES GALLERY — WIDGET */}
        <div className="section-label">Gallery stati · ToolkitRenderer — dispatch widgets[] × 5 stati</div>
        <GalleryRow title="ToolkitRenderer" sub="dispatcher · 6 widget enabled" entity="toolkit">
          {STATES.map(s => (
            <S.PanelFrame key={s.id} label={s.lb} entity="toolkit" h={470}>
              <R.ToolkitRenderer widgets={WIDGET_ALL.widgets} players={WIDGET_ALL.players} state={s.id} />
            </S.PanelFrame>
          ))}
        </GalleryRow>
        <div className="section-label" style={{ marginTop: 4 }}>I 6 widget types del dispatcher — RandomGenerator · TurnManager · ScoreTracker · ResourceManager · NoteManager · Whiteboard</div>
        <div className="mai-cb-scroll" style={{ display: 'flex', gap: 14, overflowX: 'auto', paddingBottom: 10 }}>
          {D.widgetFixtures.map(w => (
            <div key={w.id} style={{ width: 280, flexShrink: 0 }}>
              <R.WidgetBlock widget={w} players={WING.players} />
            </div>
          ))}
        </div>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
        .mai-pulse-dot { animation: mai-pulse-dot-anim 1.5s ease-in-out infinite; transform-origin: center; display: inline-block; }
        .mai-cb-scroll::-webkit-scrollbar { height: 7px; }
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
