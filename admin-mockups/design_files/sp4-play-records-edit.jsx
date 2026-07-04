/* MeepleAI SP4 — /play-records/[id]/edit · GALLERY (stati canonici)
   Route: /play-records/[id]/edit — Modifica una partita giocata.
   File: admin-mockups/design_files/sp4-play-records-edit.{html,jsx}
   Modello: sp4-play-records-new (Mockup 2) — gallery scaffold IDENTICO (nav sticky +
   state-section + frame chrome phone 375 / desktop 1440 + ThemeToggle + SSE skip ghost).
   Unica differenza vs -new: mode='edit' + titoli/copy ("Modifica partita").

   ── Stati canonici (G7 SessionStateRenderer, PR 2357) — mode='edit' ─────────
   Export gallery wrapper per-stato; ciascuno monta il form condiviso via
   window.PRForm.render(rootId, { mode: 'edit', state }) — la logica vive in
   pr-form-core.jsx (GIÀ ESTESO dal Mockup 2, riusato as-is, NON modificato).

     State01_Default  → state-01-default  (record esistente precompilato Wingspan · CTA Salva modifiche · Elimina)
     State02_Empty    → state-02-empty    (record salvato minimale · campi parziali + banner info, role=status)
     State03_Loading  → state-03-loading  (autosave dopo modifica field · toolbar + body offuscato + skeleton, aria-busy)
     State04_Error    → state-04-error    (submit modifiche fallito · banner alert + form preservato + retry)
   state-05-sse → SKIPPED: il form è transactional (single-shot save), NON SSE-driven.

   FREEZE: zero hex/hsl hardcoded per gli entity color → solo token --c-* via
   entityHsl() (in pr-form-core.jsx). Esente: color:'#fff' su background entity.
*/
const { useState, useEffect, useRef } = React;

// ─── Mount: ogni stato monta il form condiviso in una sub-root dedicata ──
// Honoring API: window.PRForm.render(rootId, { mode: 'edit', state }).
const PRStateMount = ({ state }) => {
  const ref = useRef(null);
  useEffect(() => {
    if (ref.current) window.PRForm.render(ref.current.id, { mode: 'edit', state });
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
    sub:'Modifica di un record esistente: form precompilato dalla partita salvata (Wingspan, punteggi Marco/Anna/Luca/Sara, note). CTA primary "✓ Salva modifiche", azione secondaria critica "Elimina partita" (entity=event). Stato base, invariato dall\'as-shipped originale.' },
  { id:'state-02-empty', num:'02', title:'Empty', Comp: State02_Empty,
    sub:'Il record era minimale quando salvato (no foto, no note, alcuni score ancora da inserire): il form precompila solo i campi parziali. Banner info top (role="status", aria-live="polite") invita a completare i dati mancanti per arricchire la cronologia.' },
  { id:'state-03-loading', num:'03', title:'Loading', Comp: State03_Loading,
    sub:'Autosave in corso dopo la modifica di un field (es. cambio di uno score). Toolbar sticky "Salvataggio modifiche in corso…" con spinner, body offuscato (0.65) ma inputabile, progress upload se nuova foto, skeleton sull\'anteprima record (desktop). aria-busy + screen-reader announce; pulse 2s, snap con reduced-motion.' },
  { id:'state-04-error', num:'04', title:'Error', Comp: State04_Error,
    sub:'Errore al submit finale (Step 3 → "Salva modifiche"). Banner full-width (role="alert") + "Riprova salvataggio", form preservato con tutto l\'input modificato, link "Annulla modifiche" (entity=session) sotto il banner per revert allo stato originale.' },
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
      <div className="gallery-nav-brand"><span aria-hidden="true">🎯</span> SP4 · /play-records/[id]/edit</div>
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
          <div className="kicker">SP4 · /play-records/[id]/edit 🎯 — canonical states</div>
          <h1>Modifica partita — Stati canonici</h1>
          <p className="lead">
            Wizard 3-step (Gioco · Quando · Punteggi) per modificare una partita registrata, allineato al pattern <strong>G7 SessionStateRenderer</strong> (PR 2357).
            4 stati canonici × viewport mobile&nbsp;375 (wizard bottom-nav) / desktop&nbsp;1440 (split-form 8-col + anteprima live 4-col), × tema light/dark via toggle.
            Form condiviso con <code>/play-records/new</code> via <code>{"window.PRForm.render(rootId, { mode: 'edit', state })"}</code>; entity dominante <strong>session 🎯</strong>, colori esclusivamente da token <code>--c-*</code> via <code>entityHsl()</code>.
            Lo stato <code>state-05-sse</code> è intenzionalmente <strong>saltato</strong> (form transactional, non SSE-driven).
          </p>
        </header>

        {STATES.map(s => <StateSection key={s.id} {...s}/>)}
      </div>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
