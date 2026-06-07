/* ════════════════════════════════════════════════════════════════════════
   sp4-add-game-drawer.jsx
   AddGameDrawer — Right-side Sheet (sm:max-w-xl) per aggiungere un gioco.

   Riscrittura sp4-* di apps/web AddGameDrawer.tsx. Token canonici (tokens.css),
   UI 100% italiana, light + dark, animazione apertura/chiusura 300ms,
   deep-link ?action=add.

   Vincolo legale ADR-059: Wikidata primary · BGG SOLO admin-side.
   ⇒ Nessun path "Importa BGG" / search BGG lato utente. Solo:
      (a) Manuale custom   (b) Catalogo condiviso (seed admin Wikidata+BGG-whitelist).

   Fix spec-panel review:
     C1 i18n completa · M1 empty-catalogo + CTA manuale · M2 already-in-library
     alert · M3 copy choice-cards · M4 a11y (role=dialog, focus trap, ESC,
     aria-labelledby) · N1 toast success · N2 skeleton loading.
   ════════════════════════════════════════════════════════════════════════ */

const { useState, useEffect, useRef, useCallback, useMemo, useId } = React;

// ─── i18n — single source of truth per ogni stringa visibile (fix C1) ──────────
const T = {
  titleChoice:  'Aggiungi un gioco',
  titleManual:  'Aggiungi manualmente',
  titleCatalog: 'Aggiungi dal catalogo',
  choiceLead:   'Come vuoi aggiungere il tuo gioco?',
  manualTitle:  'Aggiungi manualmente',
  manualDesc:   'Crea da zero. Ideale per giochi indie o auto-prodotti.',
  catalogTitle: 'Da catalogo condiviso',
  catalogDesc:  'Cerca tra ~159 giochi della community. Aggiunta in 1 click.',
  back:         'Indietro',
  close:        'Chiudi',
  searchPlaceholder: 'Cerca nel catalogo…',
  resultsCount: (n) => `${n} ${n === 1 ? 'gioco trovato' : 'giochi trovati'}`,
  noImage:      'Nessuna immagine',
  inLibrary:    'Già nella libreria',
  select:       'Aggiungi',
  selecting:    'Aggiungo…',
  emptyTitle:   'Nessun gioco trovato nel catalogo',
  emptyBody:    'Il catalogo condiviso non contiene un gioco con questo nome. Puoi crearlo manualmente in pochi secondi.',
  emptyCta:     'Aggiungi manualmente',
  blockedMsg:   (name) => `Hai già «${name}» in libreria.`,
  blockedCta:   'Vai alla scheda',
  toastAdded:   (name) => `${name} aggiunto alla libreria`,
  wizardLabel:  'Wizard manuale',
  wizardNote:   'UserWizardClient · compactMode',
  wizardHint:   'Inserisci i dettagli del gioco. Potrai caricare il regolamento e configurare l’agente AI più tardi, dalla scheda del gioco.',
  pagePrev:     'Pagina precedente',
  pageNext:     'Pagina successiva',
  reopen:       'Riapri il drawer',
  reopenHint:   'Il drawer si apre con il deep-link',
  redirectNote: (id) => `→ redirect /library/${id}`,
};

// ─── Mock catalog (facts-only, fonte Wikidata CC0 · titolo + anno) ─────────────
//     Cover demo = gradient + glyph (placeholder; le immagini reali arrivano dal seed).
//     `noImg` dimostra lo stato "Nessuna immagine". `inLibrary` lo stato disabled.
const CATALOG = [
  { id: 'g-wingspan',  title: 'Wingspan',            year: 2019, glyph: '🐦', cover: 'linear-gradient(135deg, hsl(174 55% 42%), hsl(142 60% 40%))' },
  { id: 'g-catan',     title: 'I Coloni di Catan',   year: 1995, glyph: '🌋', cover: 'linear-gradient(135deg, hsl(25 85% 52%), hsl(38 88% 50%))', inLibrary: true },
  { id: 'g-azul',      title: 'Azul',                year: 2017, glyph: '🔷', cover: 'linear-gradient(135deg, hsl(220 75% 55%), hsl(262 70% 60%))' },
  { id: 'g-brass',     title: 'Brass: Birmingham',   year: 2018, noImg: true },
  { id: 'g-pandemic',  title: 'Pandemic',            year: 2008, glyph: '🧫', cover: 'linear-gradient(135deg, hsl(350 75% 55%), hsl(25 80% 52%))' },
  { id: 'g-tmars',     title: 'Terraforming Mars',   year: 2016, glyph: '🪐', cover: 'linear-gradient(135deg, hsl(15 80% 50%), hsl(350 70% 52%))', inLibrary: true },
  { id: 'g-everdell',  title: 'Everdell',            year: 2018, glyph: '🌲', cover: 'linear-gradient(135deg, hsl(142 55% 42%), hsl(174 55% 44%))' },
  { id: 'g-root',      title: 'Root',                year: 2018, noImg: true },
  { id: 'g-carcas',    title: 'Carcassonne',         year: 2000, glyph: '🏰', cover: 'linear-gradient(135deg, hsl(38 80% 52%), hsl(25 82% 50%))' },
];
const TOTAL_RESULTS = 159;
const PAGE_SIZE = 9;
const TOTAL_PAGES = Math.ceil(TOTAL_RESULTS / PAGE_SIZE); // 18

// ─── focus-trap hook (fix M4) ──────────────────────────────────────────────────
//   active:boolean, ref:dialog node. Cicla Tab tra i focusable, focus iniziale.
function useFocusTrap(ref, active) {
  useEffect(() => {
    if (!active || !ref.current) return;
    const node = ref.current;
    const sel = 'a[href],button:not([disabled]),input,select,textarea,[tabindex]:not([tabindex="-1"])';
    const first = node.querySelector(sel);
    if (first) setTimeout(() => first.focus(), 60);
    const onKey = (e) => {
      if (e.key !== 'Tab') return;
      const items = Array.from(node.querySelectorAll(sel)).filter((el) => el.offsetParent !== null);
      if (!items.length) return;
      const a = items[0], b = items[items.length - 1];
      if (e.shiftKey && document.activeElement === a) { e.preventDefault(); b.focus(); }
      else if (!e.shiftKey && document.activeElement === b) { e.preventDefault(); a.focus(); }
    };
    node.addEventListener('keydown', onKey);
    return () => node.removeEventListener('keydown', onKey);
  }, [active, ref]);
}

// ════════════════════════════════════════════════════════════════════════════
//  ChoiceMethodCard — una card del passo 0
//  props: { accent:'game'|'kb', glyph:string, title, desc, onClick }
// ════════════════════════════════════════════════════════════════════════════
function ChoiceMethodCard({ accent, glyph, title, desc, onClick }) {
  const [hover, setHover] = useState(false);
  const a = `var(--c-${accent})`;
  return (
    <button
      type="button"
      onClick={onClick}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      style={{
        width: '100%', textAlign: 'left', cursor: 'pointer',
        display: 'flex', alignItems: 'flex-start', gap: 'var(--s-4)',
        padding: 'var(--s-5)', borderRadius: 'var(--r-xl)',
        background: hover ? `hsl(${a} / .05)` : 'var(--bg-card)',
        border: `1.5px solid ${hover ? `hsl(${a} / .55)` : 'var(--border)'}`,
        boxShadow: hover ? 'var(--shadow-sm)' : 'var(--shadow-xs)',
        transform: hover ? 'translateY(-2px)' : 'none',
        transition: 'all var(--dur-sm) var(--ease-out)',
        font: 'inherit',
      }}
    >
      <span aria-hidden="true" style={{
        flexShrink: 0, width: 46, height: 46, borderRadius: 'var(--r-lg)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 22,
        background: `hsl(${a} / .14)`,
        boxShadow: hover ? `0 0 0 3px hsl(${a} / .12)` : 'none',
        transition: 'box-shadow var(--dur-sm) var(--ease-out)',
      }}>{glyph}</span>
      <span style={{ flex: 1, minWidth: 0 }}>
        <span style={{
          display: 'block', fontFamily: 'var(--f-display)', fontWeight: 800,
          fontSize: 'var(--fs-lg)', color: 'var(--text)', lineHeight: 1.2,
        }}>{title}</span>
        <span style={{
          display: 'block', marginTop: 5, fontFamily: 'var(--f-body)',
          fontSize: 'var(--fs-base)', color: 'var(--text-sec)', lineHeight: 1.45,
        }}>{desc}</span>
      </span>
      <span aria-hidden="true" style={{
        flexShrink: 0, alignSelf: 'center', fontSize: 18, fontWeight: 800,
        color: hover ? `hsl(${a})` : 'var(--text-muted)',
        transform: hover ? 'translateX(2px)' : 'none',
        transition: 'all var(--dur-sm) var(--ease-out)',
      }}>›</span>
    </button>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  ChoiceStep — Step 0 (fix M3 copy)
//  props: { onPick:(step)=>void }
// ════════════════════════════════════════════════════════════════════════════
function ChoiceStep({ onPick }) {
  return (
    <div style={{ padding: 'var(--s-6)', display: 'flex', flexDirection: 'column', gap: 'var(--s-4)' }}>
      <p style={{
        margin: 0, fontSize: 'var(--fs-md)', color: 'var(--text-sec)', fontWeight: 600,
      }}>{T.choiceLead}</p>
      <ChoiceMethodCard accent="game" glyph="✍️" title={T.manualTitle}  desc={T.manualDesc}  onClick={() => onPick('manual')} />
      <ChoiceMethodCard accent="kb"   glyph="📚" title={T.catalogTitle} desc={T.catalogDesc} onClick={() => onPick('catalog')} />
    </div>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  ManualStep — Step 1a (placeholder embed UserWizardClient compactMode)
//  props: { onBack:()=>void }
// ════════════════════════════════════════════════════════════════════════════
function ManualStep({ onBack }) {
  return (
    <div style={{ padding: 'var(--s-5) var(--s-6) var(--s-6)' }}>
      <BackRow onBack={onBack} />
      <div style={{
        marginTop: 'var(--s-4)',
        border: '1.5px dashed var(--border-strong)', borderRadius: 'var(--r-xl)',
        background: 'repeating-linear-gradient(135deg, var(--bg-muted), var(--bg-muted) 10px, transparent 10px, transparent 20px)',
        padding: 'var(--s-9) var(--s-6)', textAlign: 'center',
        display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 'var(--s-3)',
      }}>
        <span aria-hidden="true" style={{
          width: 56, height: 56, borderRadius: 'var(--r-lg)',
          background: 'hsl(var(--c-game) / .14)', color: 'hsl(var(--c-game))',
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 26,
        }}>✍️</span>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 'var(--fs-xl)', color: 'var(--text)',
        }}>{T.wizardLabel}</div>
        <code style={{
          fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', color: 'var(--text-muted)',
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          padding: '3px 9px', borderRadius: 'var(--r-sm)', fontWeight: 700,
        }}>{T.wizardNote}</code>
        <p style={{
          margin: '4px 0 0', maxWidth: 360, fontSize: 'var(--fs-base)',
          color: 'var(--text-sec)', lineHeight: 1.5,
        }}>{T.wizardHint}</p>
      </div>
    </div>
  );
}

// ─── BackRow — riga "Indietro" condivisa (Step 1a/1b) ──────────────────────────
function BackRow({ onBack, children }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--s-2)' }}>
      <button
        type="button" onClick={onBack} aria-label={T.back}
        style={{
          display: 'inline-flex', alignItems: 'center', gap: 6,
          padding: '7px 12px 7px 9px', borderRadius: 'var(--r-md)',
          background: 'var(--bg-card)', border: '1px solid var(--border)',
          color: 'var(--text-sec)', fontFamily: 'var(--f-display)',
          fontWeight: 700, fontSize: 'var(--fs-sm)', cursor: 'pointer', flexShrink: 0,
        }}
      >
        <span aria-hidden="true" style={{ fontSize: 15 }}>←</span>{T.back}
      </button>
      {children}
    </div>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  CardSkeleton — placeholder shimmer (fix N2)
// ════════════════════════════════════════════════════════════════════════════
function CardSkeleton() {
  return (
    <div style={{
      display: 'flex', flexDirection: 'column',
      borderRadius: 'var(--r-lg)', border: '1px solid var(--border)', overflow: 'hidden',
    }}>
      <div className="shimmer" style={{ aspectRatio: '16 / 10' }} />
      <div style={{ padding: 'var(--s-3)', display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div className="shimmer" style={{ height: 12, borderRadius: 'var(--r-pill)', width: '78%' }} />
        <div className="shimmer" style={{ height: 9, borderRadius: 'var(--r-pill)', width: '34%' }} />
        <div className="shimmer" style={{ height: 30, borderRadius: 'var(--r-md)', marginTop: 4 }} />
      </div>
    </div>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  SelectableGameCard — card catalogo selezionabile
//  props: { game, isSelecting:boolean, onSelect, onBlocked }
//  - inLibrary → opacity ridotta, badge "Già nella libreria" (aria-label), btn disabled
//  - click su card disabled → onBlocked(game)   (fix M2)
//  - noImg → placeholder "Nessuna immagine"     (fix C1)
// ════════════════════════════════════════════════════════════════════════════
function SelectableGameCard({ game, isSelecting, onSelect, onBlocked }) {
  const inLib = !!game.inLibrary;
  return (
    <article
      onClick={inLib ? () => onBlocked(game) : undefined}
      style={{
        position: 'relative', display: 'flex', flexDirection: 'column',
        borderRadius: 'var(--r-lg)', overflow: 'hidden',
        border: '1px solid var(--border)', background: 'var(--bg-card)',
        opacity: inLib ? 0.62 : 1, cursor: inLib ? 'pointer' : 'default',
        transition: 'box-shadow var(--dur-sm) var(--ease-out), border-color var(--dur-sm) var(--ease-out)',
      }}
      onMouseEnter={(e) => { if (!inLib) { e.currentTarget.style.boxShadow = 'var(--shadow-md)'; e.currentTarget.style.borderColor = 'hsl(var(--c-game) / .4)'; } }}
      onMouseLeave={(e) => { e.currentTarget.style.boxShadow = 'none'; e.currentTarget.style.borderColor = 'var(--border)'; }}
    >
      {/* Cover */}
      <div style={{ position: 'relative', aspectRatio: '16 / 10', overflow: 'hidden' }}>
        {game.noImg ? (
          <div role="img" aria-label={T.noImage} style={{
            width: '100%', height: '100%',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            background: 'repeating-linear-gradient(135deg, var(--bg-muted), var(--bg-muted) 9px, var(--bg-sunken) 9px, var(--bg-sunken) 18px)',
            color: 'var(--text-muted)', fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)',
            fontWeight: 700, letterSpacing: '.04em',
          }}>{T.noImage}</div>
        ) : (
          <div aria-hidden="true" style={{
            width: '100%', height: '100%', background: game.cover,
            display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 40,
          }}>
            <span style={{ filter: 'drop-shadow(0 2px 6px rgba(0,0,0,.3))' }}>{game.glyph}</span>
          </div>
        )}
        {inLib && (
          <span
            aria-label={T.inLibrary}
            style={{
              position: 'absolute', top: 7, right: 7,
              display: 'inline-flex', alignItems: 'center', gap: 4,
              padding: '2px 8px', borderRadius: 'var(--r-pill)',
              background: 'hsl(var(--c-success))', color: '#fff',
              fontFamily: 'var(--f-display)', fontSize: 10, fontWeight: 800,
            }}
          >
            <span aria-hidden="true">✓</span>{T.inLibrary}
          </span>
        )}
      </div>

      {/* Info */}
      <div style={{ padding: 'var(--s-3)', display: 'flex', flexDirection: 'column', gap: 6, flex: 1 }}>
        <p title={game.title} style={{
          margin: 0, fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 'var(--fs-base)',
          color: 'var(--text)', lineHeight: 1.2,
          display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden',
        }}>{game.title}</p>
        <p style={{
          margin: 0, fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)',
          color: 'var(--text-muted)', fontWeight: 700,
        }}>{game.year}</p>

        <button
          type="button"
          disabled={inLib || isSelecting}
          onClick={(e) => { e.stopPropagation(); if (!inLib) onSelect(game); }}
          style={{
            marginTop: 'auto', width: '100%', padding: '8px 12px',
            borderRadius: 'var(--r-md)', fontFamily: 'var(--f-display)',
            fontWeight: 800, fontSize: 'var(--fs-sm)',
            cursor: inLib ? 'not-allowed' : 'pointer',
            border: inLib ? '1px solid var(--border)' : 'none',
            background: inLib ? 'transparent' : 'hsl(var(--c-game))',
            color: inLib ? 'var(--text-muted)' : '#fff',
            opacity: isSelecting ? 0.7 : 1,
            transition: 'filter var(--dur-sm) var(--ease-out)',
          }}
          onMouseEnter={(e) => { if (!inLib && !isSelecting) e.currentTarget.style.filter = 'brightness(1.06)'; }}
          onMouseLeave={(e) => { e.currentTarget.style.filter = 'none'; }}
        >
          {inLib ? T.inLibrary : (isSelecting ? T.selecting : T.select)}
        </button>
      </div>
    </article>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  CatalogEmpty — empty state catalogo (fix M1)
//  props: { onAddManually:()=>void }
// ════════════════════════════════════════════════════════════════════════════
function CatalogEmpty({ onAddManually }) {
  return (
    <div style={{
      gridColumn: '1 / -1', padding: 'var(--s-9) var(--s-5)', textAlign: 'center',
      display: 'flex', flexDirection: 'column', alignItems: 'center',
      animation: 'pv-pop var(--dur-md) var(--ease-out)',
    }}>
      <div aria-hidden="true" style={{
        position: 'relative', width: 92, height: 92, borderRadius: 'var(--r-2xl)',
        background: 'hsl(var(--c-game) / .10)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 42,
        marginBottom: 'var(--s-5)',
      }}>
        🔍
        <span style={{
          position: 'absolute', inset: -7, borderRadius: 'var(--r-2xl)',
          border: '2px dashed hsl(var(--c-game) / .28)',
        }} />
      </div>
      <h3 style={{
        margin: '0 0 var(--s-2)', fontFamily: 'var(--f-display)', fontWeight: 800,
        fontSize: 'var(--fs-xl)', color: 'var(--text)',
      }}>{T.emptyTitle}</h3>
      <p style={{
        margin: '0 0 var(--s-5)', maxWidth: 340, fontSize: 'var(--fs-base)',
        color: 'var(--text-sec)', lineHeight: 1.55,
      }}>{T.emptyBody}</p>
      <button
        type="button" onClick={onAddManually}
        style={{
          display: 'inline-flex', alignItems: 'center', gap: 7,
          padding: '10px 18px', borderRadius: 'var(--r-md)',
          background: 'hsl(var(--c-game))', color: '#fff', border: 'none',
          fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 'var(--fs-base)',
          cursor: 'pointer', boxShadow: '0 4px 14px hsl(var(--c-game) / .4)',
        }}
      >
        <span aria-hidden="true">✍️</span>{T.emptyCta}
      </button>
    </div>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  CatalogPagination
//  props: { page, totalPages, onChange }
// ════════════════════════════════════════════════════════════════════════════
function CatalogPagination({ page, totalPages, onChange }) {
  const pages = useMemo(() => {
    const out = [];
    const add = (p) => out.push(p);
    add(1);
    if (page > 3) out.push('…');
    for (let p = Math.max(2, page - 1); p <= Math.min(totalPages - 1, page + 1); p++) add(p);
    if (page < totalPages - 2) out.push('…');
    if (totalPages > 1) add(totalPages);
    return out;
  }, [page, totalPages]);

  const arrow = (dir, disabled, label) => (
    <button
      type="button" aria-label={label} disabled={disabled}
      onClick={() => onChange(page + dir)}
      style={{
        width: 32, height: 32, borderRadius: 'var(--r-md)',
        border: '1px solid var(--border)', background: 'var(--bg-card)',
        color: disabled ? 'var(--text-muted)' : 'var(--text)',
        cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.45 : 1,
        fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 14,
      }}
    >{dir < 0 ? '‹' : '›'}</button>
  );

  return (
    <div style={{
      gridColumn: '1 / -1', display: 'flex', alignItems: 'center', justifyContent: 'center',
      gap: 5, paddingTop: 'var(--s-3)',
    }}>
      {arrow(-1, page <= 1, T.pagePrev)}
      {pages.map((p, i) =>
        p === '…' ? (
          <span key={`e${i}`} aria-hidden="true" style={{
            width: 22, textAlign: 'center', color: 'var(--text-muted)',
            fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-sm)',
          }}>…</span>
        ) : (
          <button
            key={p} type="button"
            aria-label={`Pagina ${p}`} aria-current={p === page ? 'page' : undefined}
            onClick={() => onChange(p)}
            style={{
              minWidth: 32, height: 32, padding: '0 6px', borderRadius: 'var(--r-md)',
              border: p === page ? '1px solid hsl(var(--c-game) / .5)' : '1px solid var(--border)',
              background: p === page ? 'hsl(var(--c-game) / .12)' : 'var(--bg-card)',
              color: p === page ? 'hsl(var(--c-game))' : 'var(--text-sec)',
              fontFamily: 'var(--f-mono)', fontWeight: 800, fontSize: 'var(--fs-sm)',
              cursor: 'pointer', fontVariantNumeric: 'tabular-nums',
            }}
          >{p}</button>
        )
      )}
      {arrow(1, page >= totalPages, T.pageNext)}
    </div>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  CatalogStep — Step 1b
//  props: { view:'loading'|'empty'|'results', onBack, onAddSuccess, onGoToManual }
// ════════════════════════════════════════════════════════════════════════════
function CatalogStep({ view, onBack, onAddSuccess, onGoToManual }) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [selectingId, setSelectingId] = useState(null);
  const [blocked, setBlocked] = useState(null); // {id,title} per alert M2
  const blockTimer = useRef(null);

  // filtro client sui risultati visibili (demo)
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? CATALOG.filter((g) => g.title.toLowerCase().includes(q)) : CATALOG;
  }, [search]);

  const isLoading = view === 'loading';
  // empty se forzato dal preview, oppure se la ricerca non trova nulla
  const isEmpty = view === 'empty' || (view === 'results' && filtered.length === 0);

  const handleSelect = useCallback((game) => {
    setSelectingId(game.id);
    setTimeout(() => {
      setSelectingId(null);
      onAddSuccess(game); // toast + redirect gestiti dal parent (fix N1)
    }, 750);
  }, [onAddSuccess]);

  const handleBlocked = useCallback((game) => {
    setBlocked({ id: game.id, title: game.title });
    if (blockTimer.current) clearTimeout(blockTimer.current);
    blockTimer.current = setTimeout(() => setBlocked(null), 4500);
  }, []);
  useEffect(() => () => { if (blockTimer.current) clearTimeout(blockTimer.current); }, []);

  const count = view === 'empty' ? 0 : (search.trim() ? filtered.length : TOTAL_RESULTS);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--s-4)', padding: 'var(--s-5) var(--s-6) var(--s-6)' }}>
      {/* Back + search */}
      <BackRow onBack={onBack}>
        <div style={{ position: 'relative', flex: 1 }}>
          <span aria-hidden="true" style={{
            position: 'absolute', left: 11, top: '50%', transform: 'translateY(-50%)',
            color: 'var(--text-muted)', fontSize: 15, pointerEvents: 'none',
          }}>⌕</span>
          <input
            type="search" value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            placeholder={T.searchPlaceholder} aria-label={T.searchPlaceholder}
            style={{
              width: '100%', padding: '9px 12px 9px 34px',
              borderRadius: 'var(--r-md)', border: '1px solid var(--border)',
              background: 'var(--bg-card)', color: 'var(--text)',
              fontFamily: 'var(--f-body)', fontSize: 'var(--fs-base)', outline: 'none',
            }}
            onFocus={(e) => { e.target.style.borderColor = 'hsl(var(--c-game) / .6)'; e.target.style.boxShadow = '0 0 0 3px hsl(var(--c-game) / .14)'; }}
            onBlur={(e) => { e.target.style.borderColor = 'var(--border)'; e.target.style.boxShadow = 'none'; }}
          />
        </div>
      </BackRow>

      {/* Alert "già in libreria" (fix M2) — non intrusivo, inline, auto-dismiss */}
      {blocked && (
        <div role="status" style={{
          display: 'flex', alignItems: 'center', gap: 'var(--s-3)',
          padding: '10px 12px', borderRadius: 'var(--r-md)',
          background: 'hsl(var(--c-success) / .10)',
          border: '1px solid hsl(var(--c-success) / .35)',
          animation: 'pv-slide-up var(--dur-md) var(--ease-spring)',
        }}>
          <span aria-hidden="true" style={{
            width: 26, height: 26, flexShrink: 0, borderRadius: 'var(--r-sm)',
            background: 'hsl(var(--c-success) / .18)', color: 'hsl(var(--c-success))',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 13, fontWeight: 800,
          }}>✓</span>
          <span style={{ flex: 1, fontSize: 'var(--fs-sm)', color: 'var(--text)', fontWeight: 600 }}>
            {T.blockedMsg(blocked.title)}
          </span>
          <button
            type="button"
            onClick={() => onAddSuccess({ id: blocked.id, title: blocked.title, _goto: true })}
            style={{
              flexShrink: 0, padding: '5px 10px', borderRadius: 'var(--r-sm)',
              background: 'hsl(var(--c-success))', color: '#fff', border: 'none',
              fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 'var(--fs-xs)', cursor: 'pointer',
            }}
          >{T.blockedCta} →</button>
          <button
            type="button" onClick={() => setBlocked(null)} aria-label={T.close}
            style={{
              flexShrink: 0, width: 24, height: 24, borderRadius: 'var(--r-sm)',
              background: 'transparent', border: 'none', color: 'var(--text-muted)',
              fontSize: 13, cursor: 'pointer',
            }}
          >✕</button>
        </div>
      )}

      {/* Results count */}
      {!isLoading && !isEmpty && (
        <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', fontFamily: 'var(--f-mono)', fontWeight: 700 }}>
          {T.resultsCount(count)}
        </p>
      )}

      {/* Grid 3x3 */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 'var(--s-3)' }}>
        {isLoading
          ? Array.from({ length: PAGE_SIZE }).map((_, i) => <CardSkeleton key={i} />)
          : isEmpty
            ? <CatalogEmpty onAddManually={onGoToManual} />
            : filtered.map((g) => (
                <SelectableGameCard
                  key={g.id} game={g}
                  isSelecting={selectingId === g.id}
                  onSelect={handleSelect}
                  onBlocked={handleBlocked}
                />
              ))}

        {/* Pagination — solo in results senza ricerca attiva */}
        {!isLoading && !isEmpty && !search.trim() && (
          <CatalogPagination page={page} totalPages={TOTAL_PAGES} onChange={setPage} />
        )}
      </div>
    </div>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  Toast — success post-add (fix N1)
//  props: { toast:{name}|null }
// ════════════════════════════════════════════════════════════════════════════
function Toast({ toast }) {
  if (!toast) return null;
  return (
    <div role="status" aria-live="polite" style={{
      position: 'absolute', bottom: 22, left: '50%', transform: 'translateX(-50%)',
      zIndex: 80, display: 'inline-flex', alignItems: 'center', gap: 8,
      padding: '11px 18px', borderRadius: 'var(--r-pill)',
      background: 'var(--text)', color: 'var(--bg)',
      fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 'var(--fs-sm)',
      boxShadow: 'var(--shadow-lg)', whiteSpace: 'nowrap', maxWidth: 'calc(100% - 48px)',
      animation: 'pv-slide-up var(--dur-md) var(--ease-spring)',
    }}>
      <span aria-hidden="true" style={{ color: 'hsl(var(--c-success))', fontWeight: 800 }}>✓</span>
      <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{T.toastAdded(toast.name)}</span>
    </div>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  AddGameDrawer — Sheet (role=dialog) con animazione 300ms (fix M4)
//  props: { open, step, view, onClose, onStep, onAddSuccess }
// ════════════════════════════════════════════════════════════════════════════
const ANIM_MS = 300;

function AddGameDrawer({ open, step, view, onClose, onStep, onAddSuccess }) {
  const panelRef = useRef(null);
  const titleId = useId();
  useFocusTrap(panelRef, open);

  // ESC chiude (fix M4)
  useEffect(() => {
    if (!open) return;
    const onKey = (e) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  const title = step === 'manual' ? T.titleManual : step === 'catalog' ? T.titleCatalog : T.titleChoice;

  return (
    <>
      {/* Backdrop */}
      <div
        onClick={onClose} aria-hidden="true"
        style={{
          position: 'absolute', inset: 0, zIndex: 50,
          background: 'rgba(20, 12, 4, 0.46)', backdropFilter: 'blur(2px)',
          opacity: open ? 1 : 0,
          pointerEvents: open ? 'auto' : 'none',
          transition: `opacity ${ANIM_MS}ms var(--ease-out)`,
        }}
      />

      {/* Panel — right side, sm:max-w-xl (576px) */}
      <aside
        ref={panelRef}
        role="dialog" aria-modal="true" aria-labelledby={titleId}
        style={{
          position: 'absolute', top: 0, right: 0, bottom: 0, zIndex: 51,
          width: '100%', maxWidth: 576,
          display: 'flex', flexDirection: 'column',
          background: 'var(--bg)', borderLeft: '1px solid var(--border)',
          boxShadow: 'var(--shadow-lg)',
          transform: open ? 'translateX(0)' : 'translateX(100%)',
          transition: `transform ${ANIM_MS}ms var(--ease-out)`,
        }}
      >
        {/* Header */}
        <div style={{
          flexShrink: 0, display: 'flex', alignItems: 'center', gap: 'var(--s-3)',
          padding: 'var(--s-4) var(--s-5)', borderBottom: '1px solid var(--border)',
        }}>
          <span aria-hidden="true" style={{
            width: 34, height: 34, borderRadius: 'var(--r-md)', flexShrink: 0,
            background: 'hsl(var(--c-game) / .14)', color: 'hsl(var(--c-game))',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 18, fontWeight: 800,
          }}>+</span>
          <h2 id={titleId} style={{
            flex: 1, margin: 0, fontFamily: 'var(--f-display)', fontWeight: 800,
            fontSize: 'var(--fs-xl)', color: 'var(--text)',
          }}>{title}</h2>
          <button
            type="button" onClick={onClose} aria-label={T.close}
            style={{
              width: 34, height: 34, borderRadius: 'var(--r-md)', flexShrink: 0,
              background: 'var(--bg-muted)', border: 'none', color: 'var(--text)',
              fontSize: 15, cursor: 'pointer',
            }}
          >✕</button>
        </div>

        {/* Body */}
        <div className="dw-scroll" style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>
          {step === 'choice'  && <ChoiceStep onPick={onStep} />}
          {step === 'manual'  && <ManualStep onBack={() => onStep('choice')} />}
          {step === 'catalog' && (
            <CatalogStep
              view={view}
              onBack={() => onStep('choice')}
              onAddSuccess={onAddSuccess}
              onGoToManual={() => onStep('manual')}
            />
          )}
        </div>
      </aside>
    </>
  );
}

// ════════════════════════════════════════════════════════════════════════════
//  PreviewApp — guscio demo: pilota open/step/view via control-bar dell'HTML
// ════════════════════════════════════════════════════════════════════════════
function PreviewApp() {
  const [open, setOpen] = useState(true);
  const [step, setStep] = useState('choice');
  const [view, setView] = useState('results');     // stato catalogo (loading/empty/results)
  const [toast, setToast] = useState(null);
  const [redirect, setRedirect] = useState(null);  // mono note → /library/{id}
  const reopenTimer = useRef(null);
  const toastTimer = useRef(null);

  // ── sync con la control-bar HTML ──
  useEffect(() => {
    const stepSeg = document.getElementById('stepSeg');
    const stateSeg = document.getElementById('stateSeg');
    const urlChip = document.getElementById('urlChip');

    const setSegOn = (seg, attr, val) => {
      seg.querySelectorAll('button').forEach((b) => b.classList.toggle('on', b.dataset[attr] === val));
    };
    const onStepClick = (e) => {
      const b = e.target.closest('button'); if (!b) return;
      const s = b.dataset.step;
      setStep(s); setOpen(true); setRedirect(null);
      setSegOn(stepSeg, 'step', s);
    };
    const onStateClick = (e) => {
      const b = e.target.closest('button'); if (!b) return;
      const v = b.dataset.state;
      setView(v); setSegOn(stateSeg, 'state', v);
    };
    stepSeg.addEventListener('click', onStepClick);
    stateSeg.addEventListener('click', onStateClick);

    // espone setter per il sync inverso
    PreviewApp._sync = { stepSeg, stateSeg, urlChip, setSegOn };
    return () => {
      stepSeg.removeEventListener('click', onStepClick);
      stateSeg.removeEventListener('click', onStateClick);
    };
  }, []);

  // abilita/disabilita il segmented "stato catalogo" e aggiorna URL chip
  useEffect(() => {
    const s = PreviewApp._sync; if (!s) return;
    s.stateSeg.classList.toggle('disabled', step !== 'catalog');
    s.urlChip.textContent = open ? '/library?action=add' : '/library';
  }, [step, open]);

  const handleClose = useCallback(() => {
    setOpen(false);
    if (reopenTimer.current) clearTimeout(reopenTimer.current);
    // reset a 'choice' dopo l'animazione (parità con il 300ms del componente reale)
    reopenTimer.current = setTimeout(() => {
      setStep('choice');
      const s = PreviewApp._sync;
      if (s) s.setSegOn(s.stepSeg, 'step', 'choice');
    }, ANIM_MS);
  }, []);

  const handleStep = useCallback((s) => {
    setStep(s); setRedirect(null);
    const sy = PreviewApp._sync;
    if (sy) sy.setSegOn(sy.stepSeg, 'step', s);
  }, []);

  // add success → toast (N1) poi "redirect" /library/{id}
  const handleAddSuccess = useCallback((game) => {
    if (!game._goto) {
      setToast({ name: game.title });
      if (toastTimer.current) clearTimeout(toastTimer.current);
      toastTimer.current = setTimeout(() => setToast(null), 2400);
    }
    setRedirect(game.id);
    setOpen(false);
    if (reopenTimer.current) clearTimeout(reopenTimer.current);
    reopenTimer.current = setTimeout(() => {
      setStep('choice');
      const s = PreviewApp._sync;
      if (s) s.setSegOn(s.stepSeg, 'step', 'choice');
    }, ANIM_MS);
  }, []);

  useEffect(() => () => {
    if (reopenTimer.current) clearTimeout(reopenTimer.current);
    if (toastTimer.current) clearTimeout(toastTimer.current);
  }, []);

  const reopen = () => {
    setRedirect(null); setOpen(true); setStep('choice');
    const s = PreviewApp._sync;
    if (s) s.setSegOn(s.stepSeg, 'step', 'choice');
  };

  return (
    <>
      {/* Reopen affordance quando il drawer è chiuso (dimostra deep-link) */}
      {!open && (
        <div style={{
          position: 'absolute', inset: 0, display: 'flex', alignItems: 'center', justifyContent: 'center',
          pointerEvents: 'none',
        }}>
          <div style={{
            pointerEvents: 'auto', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 'var(--s-3)',
            padding: 'var(--s-6) var(--s-7)', borderRadius: 'var(--r-xl)',
            background: 'var(--glass-bg)', backdropFilter: 'blur(12px)',
            border: '1px solid var(--border)', boxShadow: 'var(--shadow-md)',
          }}>
            {redirect && (
              <code style={{
                fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', fontWeight: 700,
                color: 'hsl(var(--c-success))', background: 'hsl(var(--c-success) / .1)',
                padding: '3px 9px', borderRadius: 'var(--r-sm)',
              }}>{T.redirectNote(redirect)}</code>
            )}
            <button
              type="button" onClick={reopen}
              style={{
                display: 'inline-flex', alignItems: 'center', gap: 7,
                padding: '10px 18px', borderRadius: 'var(--r-md)',
                background: 'hsl(var(--c-game))', color: '#fff', border: 'none',
                fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 'var(--fs-base)',
                cursor: 'pointer', boxShadow: '0 4px 14px hsl(var(--c-game) / .4)',
              }}
            >
              <span aria-hidden="true">+</span>{T.reopen}
            </button>
            <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
              {T.reopenHint} <b style={{ color: 'var(--text-sec)' }}>?action=add</b>
            </span>
          </div>
        </div>
      )}

      <AddGameDrawer
        open={open} step={step} view={view}
        onClose={handleClose} onStep={handleStep} onAddSuccess={handleAddSuccess}
      />

      <Toast toast={toast} />
    </>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<PreviewApp />);
