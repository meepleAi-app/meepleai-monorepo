/* MeepleAI SP4 · Paleo — LIVE  (premium, game-specific)
   /sessions/[id]/live?game=paleo

   Estensione game-specific dello skeleton universale applicata a Paleo
   (co-op preistorico · SIMULTANEOUS · 1–4 · ~45–60 min). Riusa i renderer
   polimorfici dello skeleton (Scoring BinaryWin · Turn Simultaneous ·
   ChatAgent · ActionLog) e monta sopra i pannelli Paleo (Tribù · Carte ·
   Mani · Rivelazione) con lo STESSO pattern accordion del power-grid.

   Loads: sp4-parts-common.jsx · sp4-session-paleo-data.jsx ·
          sp4-session-skeleton-renderers.jsx · sp4-session-skeleton-parts.jsx ·
          sp4-session-paleo-flavor.jsx · sp4-session-paleo-parts.jsx
   DEMO-NAV-HINTS: sp4-session-paleo-summary.html */

const S = window.SkeletonParts;
const R = window.SkeletonRenderers;
const PP = window.PaleoParts;
const PD = window.PaleoData;
const M = window.MAI;
const eHsl = M.entityHsl;
const STATES = PD.STATES;

const GalleryRow = ({ title, sub, entity, children }) => (
  <div style={{ marginBottom: 26 }}>
    <div style={{ display: 'flex', alignItems: 'baseline', gap: 8, marginBottom: 10, flexWrap: 'wrap' }}>
      <span style={{ fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800, color: eHsl(entity) }}>{title}</span>
      {sub && <span style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 700 }}>{sub}</span>}
    </div>
    <div className="mai-cb-scroll" style={{ display: 'flex', gap: 16, overflowX: 'auto', paddingBottom: 10 }}>{children}</div>
  </div>
);

function Intro() {
  const cards = [
    { e: 'session', h: 'Estensione dello skeleton', b: 'Stesso template universale: i renderer polimorfici restano invariati, i pannelli Paleo si montano sopra leggendo paleo.json.' },
    { e: 'toolkit', h: 'Co-op simultaneo', b: 'Nessun turno individuale — tutti agiscono insieme. Vittoria collettiva (BinaryWin): pittura rupestre vs teschi.' },
    { e: 'event', h: 'Stato critico in evidenza', b: 'Teschi (aria-live) e impronte rupestri (aria-valuetext) sempre leggibili: a 5 teschi la tribù si estingue.' },
    { e: 'agent', h: 'Pattern accordion', b: 'Colonna principale single-expanded: Tribù · Carte · ChatAgent · Registro. Un solo pannello aperto, come lo skeleton.' },
  ];
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 12, margin: '4px 0 8px' }}>
      {cards.map(c => (
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
        <div style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '.08em', marginBottom: 8, display: 'flex', gap: 12, flexWrap: 'wrap' }}>
          <span>SP4 · Paleo · Session live 🦣</span>
          <a href="sp4-session-paleo-summary.html" style={{ color: eHsl('toolkit'), borderBottom: `1px solid ${eHsl('toolkit', 0.4)}` }}>→ Riepilogo post-partita</a>
        </div>
        <h1>Paleo — sessione live (co-op simultaneo)</h1>
        <p className="lead">
          Mockup <strong>premium game-specific</strong> per <code>/sessions/[id]/live</code>. Lo
          <strong> skeleton universale</strong> resta invariato (Scoring · Turn · ChatAgent · Registro);
          sopra si montano i pannelli Paleo — <strong>Tribù</strong>, <strong>Carte & mazzi</strong>,
          <strong> mani segrete</strong> e <strong>rivelazione simultanea</strong> — con lo stesso
          pattern accordion. Esito collettivo <strong>BinaryWin</strong>: completa la pittura rupestre
          (5 impronte) prima di accumulare 5 teschi. Dark = modalità primaria.
        </p>
        <Intro />

        {/* DESKTOP — full interactive session */}
        <div className="section-label">Desktop 1280 — sessione completa · accordion (Tribù · Carte · Chat · Registro) · tab Scoring/Tribù/Carte/Abilità/Chat · «Rivela azioni» apre l'overlay</div>
        <S.DesktopFrame ds={PD.ds} label="Paleo — BinaryWin · Simultaneous" url="meepleai.app/sessions/paleo-1/live?tab=scoring" height={740}
          desc="Banner fasi Mattina/Giorno/Notte (Giorno 3/6) · fascia stato tribù condiviso · colonna principale accordion single-expanded · colonna destra polimorfica. La fascia mani mostra la mano di Marco scoperta e le altre coperte; «Rivela azioni» apre la risoluzione simultanea.">
          <S.TopBar ds={PD.ds} connection="connected" />
          <PP.DesktopBody initialTab="scoring" initialState="default" />
        </S.DesktopFrame>

        {/* MOBILE — three states */}
        <div className="section-label">Mobile 375 — main full-width · accordion · fascia mani · tab → bottom-sheet drawer</div>
        <div className="phones-grid">
          <PP.PhoneShell label="Paleo · Tribù" desc="Banner fasi sticky, fascia stato tribù, accordion con Tribù aperta: pittura rupestre + teschi in evidenza, membri, risorse, missioni." />
          <PP.PhoneShell label="Paleo · sheet Scoring" initialTab="scoring" desc="Tap su una tab apre il bottom-sheet: lo ScoringPanelRenderer rende il ramo BinaryWin (esito collettivo, niente punti)." />
          <PP.PhoneShell label="Paleo · sheet Tribù · dark" dark initialTab="tribe" desc="Stesso drawer in dark: la tab Tribù riusa TurnIndicatorRenderer (Simultaneous) + overview tribù." />
        </div>

        {/* STATES — Paleo panels */}
        <div className="section-label">Gallery stati · pannelli Paleo — TribePanel · CardsDeckPanel × 5 stati canonici</div>
        <GalleryRow title="TribePanel" sub="meeple · pittura · teschi · risorse · missioni" entity="session">
          {STATES.map(s => (
            <PP.PanelFrame key={s.id} label={s.lb} entity="session" h={520}>
              <div style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}><PP.TribePanel state={s.id} compact /></div>
            </PP.PanelFrame>
          ))}
        </GalleryRow>
        <GalleryRow title="CardsDeckPanel" sub="mazzo azioni · missioni · incontri · scarti" entity="game">
          {STATES.map(s => (
            <PP.PanelFrame key={s.id} label={s.lb} entity="game" h={470}>
              <div style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}><PP.CardsDeckPanel state={s.id} /></div>
            </PP.PanelFrame>
          ))}
        </GalleryRow>

        {/* STATES — reused skeleton renderers */}
        <div className="section-label">Gallery stati · renderer skeleton riusati — ScoringPanelRenderer (BinaryWin) · TurnIndicatorRenderer (Simultaneous) × 5 stati</div>
        <GalleryRow title="ScoringPanelRenderer · BinaryWin" sub="esito collettivo · pittura vs teschi · 3 condizioni" entity="session">
          {STATES.map(s => (
            <PP.PanelFrame key={s.id} label={s.lb} entity="session" h={470}>
              <R.ScoringPanelRenderer data={PD.ds} state={s.id} />
            </PP.PanelFrame>
          ))}
        </GalleryRow>
        <GalleryRow title="TurnIndicatorRenderer · Simultaneous" sub="tutti giocano insieme · fasi del giorno" entity="player">
          {STATES.map(s => (
            <PP.PanelFrame key={s.id} label={s.lb} entity="player" h={470}>
              <R.TurnIndicatorRenderer data={PD.ds} state={s.id} />
            </PP.PanelFrame>
          ))}
        </GalleryRow>
      </div>

      <style>{`
        @keyframes mai-shimmer-anim { 0% { background-position: -200% 0; } 100% { background-position: 200% 0; } }
        @keyframes mai-pulse-dot-anim { 0%,100% { transform: scale(1); opacity: 1; } 50% { transform: scale(1.4); opacity: .6; } }
        .mai-shimmer { background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important; background-size: 200% 100% !important; animation: mai-shimmer-anim 1.4s linear infinite; }
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
