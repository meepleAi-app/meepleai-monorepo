/* MeepleAI SP4 — /play-records/new · GALLERY (stati canonici)
   Route: /play-records/new — Registra una partita giocata.
   File: admin-mockups/design_files/sp4-play-records-new.{html,jsx}
   Modello: sp4-play-records-index — gallery scaffold (nav sticky + state-section
   + frame chrome phone 375 / desktop 1440 + ThemeToggle + SSE skip ghost).

   ── Stati canonici (G7 SessionStateRenderer, PR 2357) — mode='new' ──────────
   Export gallery wrapper per-stato; ciascuno monta il form condiviso via
   window.PRForm.render(rootId, { mode: 'new', state }) — la logica vive in
   pr-form-core.jsx (riusato anche da -edit, Mockup 4).

     State01_Default  → state-01-default  (deep-link serata · tutto precompilato Wingspan)
     State02_Empty    → state-02-empty    (standalone · field vuoti + banner info, role=status)
     State03_Loading  → state-03-loading  (autosave post Step 1 · toolbar + body offuscato + skeleton, aria-busy)
     State04_Error    → state-04-error    (submit fallito · banner alert + form preservato + retry)
   state-05-sse → SKIPPED: il form è transactional (single-shot save), NON SSE-driven.

   FREEZE: zero hex/hsl hardcoded per gli entity color → solo token --c-* via
   entityHsl() (in pr-form-core.jsx). Esente: color:'#fff' su background entity.
*/
const { useState, useEffect, useRef } = React;

// ─── Mount: ogni stato monta il form condiviso in una sub-root dedicata ──
// Honoring API: window.PRForm.render(rootId, { mode: 'new', state }).
const PRStateMount = ({ state }) => {
  const ref = useRef(null);
  useEffect(() => {
    if (ref.current) window.PRForm.render(ref.current.id, { mode: 'new', state });
  }, [state]);
  return <div id={`pr-form-mount-${state}`} ref={ref} />;
};

// ─── Gallery wrapper exports (anchor #state-NN-*) ──────
const State01_Default = () => <PRStateMount state="default" />;
const State02_Empty   = () => <PRStateMount state="empty" />;
const State03_Loading = () => <PRStateMount state="loading" />;
const State04_Error   = () => <PRStateMount state="error" />;

const STATES = [
  { id:'state-01-default', num:'01', title:'Default', Comp: State01_Default,
    sub:'Form arrivato da deep-link serata completata (gameNightId in URL). Step 1 gioco preselezionato (Wingspan), Step 2 data/ora dalla serata, Step 3 roster dai partecipanti. Stato base, invariato dall\'as-shipped.' },
  { id:'state-02-empty', num:'02', title:'Empty', Comp: State02_Empty,
    sub:'Form standalone (nessun gameNightId): tutti i field vuoti. Banner info entry-point (role="status", aria-live="polite"), picker gioco con placeholder, roster vuoto con CTA "Aggiungi giocatore".' },
  { id:'state-03-loading', num:'03', title:'Loading', Comp: State03_Loading,
    sub:'Autosave in corso dopo la compilazione di Step 1. Toolbar sticky "Salvataggio in corso…" con spinner, body offuscato (0.65) ma inputabile, progress upload foto, skeleton sull\'anteprima record (desktop). aria-busy + screen-reader announce; pulse 2s, snap con reduced-motion.' },
  { id:'state-04-error', num:'04', title:'Error', Comp: State04_Error,
    sub:'Errore al submit finale (Step 3 → "Salva"). Banner full-width (role="alert") + "Riprova salvataggio", form preservato con tutto l\'input, link "Salva come bozza locale" (entity=session) sotto il banner.' },
];

const NAV = [
  { id:'state-01-default', label:'01 · Default' },
  { id:'state-02-empty',   label:'02 · Empty' },
  { id:'state-03-loading', label:'03 · Loading' },
  { id:'state-04-error',   label:'04 · Error' },
];

function ThemeToggle() {
  const initial = (() => { try { return localStorage.getItem('sp4-pr-theme') === 'dark'; } catch (e) { return false; } })();
  const [dark, setDark] = useState(initial);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
    try { localStorage.setItem('sp4-pr-theme', dark ? 'dark' : 'light'); } catch (e) {}
  }, [dark]);
  return (
    <button type="button" className="theme-toggle" onClick={() => setDark(d => !d)} aria-pressed={dark}
      aria-label={dark ? 'Passa a tema chiaro' : 'Passa a tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span><span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

function GalleryNav() {
  return (
    <nav className="gallery-nav" aria-label="Stati canonici">
      <div className="gallery-nav-brand"><span aria-hidden="true">🎯</span> SP4 · /play-records/new</div>
      <div className="gallery-nav-links">
        {NAV.map(n => <a key={n.id} href={`#${n.id}`}>{n.label}</a>)}
      </div>
      <a className="gallery-nav-ghost" href="#state-05-sse-skipped" aria-disabled="true" title="state-05-sse: skipped — form transactional, non SSE-driven">05 · SSE · skip</a>
      <ThemeToggle/>
    </nav>
  );
}

function StateSection({ id, num, title, sub, Comp }) {
  return (
    <section id={id} className="state-section" data-screen-label={id}>
      <header className="state-head">
        <div className="state-num">{num}</div>
        <div className="state-head-text">
          <h2>{title}</h2>
          <p>{sub}</p>
        </div>
        <code className="state-anchor">#{id}</code>
      </header>
      <Comp/>
    </section>
  );
}

function App() {
  return (
    <div className="gallery">
      <GalleryNav/>
      <div className="gallery-body">
        <header className="gallery-intro">
          <div className="kicker">SP4 · /play-records/new 🎯 — canonical states</div>
          <h1>Registra partita — Stati canonici</h1>
          <p className="lead">
            Wizard 3-step (Gioco · Quando · Punteggi) per registrare una partita, allineato al pattern <strong>G7 SessionStateRenderer</strong> (PR 2357).
            4 stati canonici × viewport mobile&nbsp;375 (wizard bottom-nav) / desktop&nbsp;1440 (split-form 8-col + anteprima live 4-col), × tema light/dark via toggle.
            Entity dominante <strong>session 🎯</strong>; colori esclusivamente da token <code>--c-*</code> via <code>entityHsl()</code>.
            Lo stato <code>state-05-sse</code> è intenzionalmente <strong>saltato</strong> (form transactional, non SSE-driven).
          </p>
        </header>

        {STATES.map(s => <StateSection key={s.id} {...s}/>)}
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
