/* SP6 D — Translation Viewer Fullscreen
   Brief SP6 §D (lines 492-566). 7 stati + theme/device variants.

   Anchor id su ogni screen wrapper (state-NN-…) + Gherkin tag (G4.4 / G4.7).
   Reading-body: 26px Nunito assoluto, line-height 1.6, max-width 60ch — DEVIAZIONE brief L519.
   prefers-reduced-motion guard ereditato (mai-shimmer / mai-spinner / mai-pulse).

   ─── Task 4 · Sezione 1d update (2026-05-23) — Multi-language source + Manual input ───
   Nuovi componenti aggiunti (parity con sezione K/L/M in librogame-runthrough-translate-viewer.html):
     · LangDetectionBadge — header pill post OCR, 3 variants confidence-driven
         · high   (>0.8)   → badge informativo (no friction)
         · medium (0.5-0.8)→ tap-to-confirm prima di translate (blocking)
         · low    (<0.5)   → cross-link state-H source-lang-unknown (error-states.html)
     · LangOverrideModal — modal radio group 5 lingue v1 (EN·FR·DE·ES·IT)
         · CTA "Conferma e ritraduci" → re-trigger AI translation (skip OCR cache hit)
         · OCR result preservato (smoldocling cache key `(photo_hash, page)` invariante)
     · ManualInputMode — textarea fallback senza foto (?mode=manual query param UI-only)
         · max 2000 char + counter visibile + lang dropdown pre-filled + book ref chip
         · Skip step 2 OCR, va a step 3 (AI translate) loading sequence Task 2

   Lingue v1: 🇬🇧 EN · 🇫🇷 FR · 🇩🇪 DE · 🇪🇸 ES · 🇮🇹 IT — copre ~95% libri-game europei.
   Target lingua: IT fisso v1 (da setting profilo). Target dropdown NON visibile.

   Triple-entry discoverability per manual-input-mode (3 punti di ingresso):
     1. CTA "Digita manualmente" in state-F `photo-blur-pre-ocr`
        (librogame-runthrough-error-states.html sez 1a Task 1)
     2. Kebab menu ⋮ del camera step
        (sp6-libro-game-photo-upload.html, top-right + voce "Digita manualmente")
     3. Empty state card "Libro non a portata? Digita il paragrafo"
        (librogame-runthrough-translate-viewer.html state-M, sez 1d Task 4)

   State tracking nello session model (commento backend):
     · source_method: "ocr" | "manual"
     · source_lang: "en" | "fr" | "de" | "es" | "it" (locked dopo conferma utente)
     · lang_detection_confidence: 0.0-1.0 (solo per source_method=ocr)

   Cross-ref:
     · state-H source-lang-unknown → librogame-runthrough-error-states.html (sez 1a Task 1)
     · state-F photo-blur-pre-ocr → librogame-runthrough-error-states.html
     · state-G loading 4-step → librogame-runthrough-translate-viewer.html (sez 1b Task 2)
     · kebab "Digita manualmente" → sp6-libro-game-photo-upload.html
*/

const { useState } = React;

/* ────────────────────────── DATA ────────────────────────── */

// Tainted Grail §147 — italian narrative w/ glossary terms (Niamh, Spada di Avalon, Pietra Spettrale)
const PARAGRAPH_IT = [
  { type: 'p', frags: [
    { t: 'La nebbia si solleva lentamente sulle paludi di Cuanacht. ' },
    { t: 'Niamh', glossary: 'Niamh' },
    { t: ' ti aspetta sulla riva, le mani strette attorno all’elsa della ' },
    { t: 'Spada di Avalon', glossary: 'Spada di Avalon' },
    { t: ', il fiato visibile nell’aria gelida.' },
  ]},
  { type: 'd', text: '« Hai trovato la Pietra? » chiede, senza voltarsi.' },
  { type: 'p', frags: [
    { t: 'Esiti. La ' },
    { t: 'Pietra Spettrale', glossary: 'Pietra Spettrale' },
    { t: ' pulsa appena dentro la tua sacca, fredda come acqua di sorgente. Annuisci, anche se lei non può vederti.' },
  ]},
  { type: 'd', text: '« Bene. Allora è il momento di salire al Tor. »' },
  { type: 'p', frags: [
    { t: 'Se decidi di seguire Niamh verso il Tor, vai al §148. Se preferisci tornare al villaggio prima del tramonto, vai al §156.' },
  ]},
];

// English original (no glossary, no italic dialogues necessary — kept sober)
const PARAGRAPH_EN = [
  { type: 'p', text: 'The mist lifts slowly over the marshes of Cuanacht. Niamh waits for you on the shore, hands clenched around the hilt of the Sword of Avalon, her breath visible in the freezing air.' },
  { type: 'd', text: '“Have you found the Stone?” she asks, without turning.' },
  { type: 'p', text: 'You hesitate. The Wraith Stone pulses faintly inside your satchel, cold as spring water. You nod, even though she cannot see you.' },
  { type: 'd', text: '“Good. Then it is time to climb to the Tor.”' },
  { type: 'p', text: 'If you decide to follow Niamh up to the Tor, go to §148. If you prefer to return to the village before sunset, go to §156.' },
];

const GLOSSARY = {
  'Niamh': 'NPC ricorrente · alleata di Avalon. Glossary entry §G-12.',
  'Spada di Avalon': 'Artefatto leggendario. Glossary entry §G-04.',
  'Pietra Spettrale': 'Wraith Stone — artefatto chiave atto II. Glossary entry §G-19.',
};

/* ────────────────────────── PRIMITIVES ────────────────────────── */

function StatusBar() {
  return (
    <div className="phone-sbar-sp6">
      <span>21:47</span>
      <span className="ind"><span>●●●●</span><span>5G</span><span>87%</span></span>
    </div>
  );
}

function DesktopChrome({ url }) {
  return (
    <div className="desktop-chrome">
      <span className="dot" /><span className="dot" /><span className="dot" />
      <span className="url">{url}</span>
    </div>
  );
}

function FrameMeta({ anchor, gherkin, label, sub }) {
  return (
    <div className="frame-meta">
      <strong>{label}</strong>
      {anchor && <span className="anchor">#{anchor}</span>}
      {gherkin && <span className="gherkin">{gherkin}</span>}
      {sub && <span>{sub}</span>}
    </div>
  );
}

/* ────────────────────────── TOPBAR ────────────────────────── */

function TopBar({ pnum = '§147', menuOpen, onToggleMenu }) {
  return (
    <div className="tv-topbar">
      <button className="icon-btn" aria-label="Indietro">←</button>
      <div className="center">{pnum}</div>
      <button className="icon-btn" aria-label="Altre azioni" onClick={onToggleMenu}>⋯</button>
      {menuOpen && <ThreeDotsDrawer />}
    </div>
  );
}

function ThreeDotsDrawer() {
  return (
    <div className="tv-menu" role="menu">
      <button role="menuitem"><span>🌐</span><span>Mostra originale (EN)</span></button>
      <button role="menuitem"><span>📋</span><span>Copia traduzione</span></button>
      <button role="menuitem" className="disabled" aria-disabled="true">
        <span>🎙️</span><span>Leggi tu (TTS)</span><span className="v2">v2</span>
      </button>
      <button role="menuitem"><span>🐛</span><span>Segnala traduzione errata</span></button>
    </div>
  );
}

/* ────────────────────────── BODY VARIANTS ────────────────────────── */

function ReadingBodyIT({ fs = 'md', lh = 'default', kicker = null, children }) {
  return (
    <div className="reading-body" data-fs={fs} data-lh={lh}>
      {kicker && <div className="kicker">{kicker}</div>}
      {(children !== undefined) ? children : (
        PARAGRAPH_IT.map((blk, i) => {
          if (blk.type === 'd') return <p key={i} className="dialogue">{blk.text}</p>;
          return (
            <p key={i}>
              {blk.frags.map((f, j) =>
                f.glossary
                  ? <span key={j} className="glossary-term" title={GLOSSARY[f.glossary]}>{f.t}</span>
                  : <React.Fragment key={j}>{f.t}</React.Fragment>
              )}
            </p>
          );
        })
      )}
    </div>
  );
}

function ReadingBodyEN({ fs = 'md', lh = 'default', kicker = 'Originale', children }) {
  return (
    <div className="reading-body" data-fs={fs} data-lh={lh}>
      {kicker && <div className="kicker">{kicker}</div>}
      {(children !== undefined) ? children : (
        PARAGRAPH_EN.map((blk, i) => (
          blk.type === 'd'
            ? <p key={i} className="dialogue">{blk.text}</p>
            : <p key={i}>{blk.text}</p>
        ))
      )}
    </div>
  );
}

function BodyHead({ chapter = 'Capitolo 4 — Le Paludi di Cuanacht', pnum = '§147' }) {
  return (
    <div className="head">
      <div className="chapter">{chapter}</div>
      <div className="pnum">{pnum}</div>
    </div>
  );
}

function SkeletonBody() {
  // 5 righe shimmer (brief)
  return (
    <div className="reading-body" aria-busy="true" aria-live="polite" style={{ width: '100%' }}>
      <div className="kicker mai-pulse">Sto traducendo…</div>
      <div className="sk-line mai-shimmer" />
      <div className="sk-line mai-shimmer" />
      <div className="sk-line mai-shimmer" />
      <div className="sk-line mai-shimmer" />
      <div className="sk-line mai-shimmer last" />
    </div>
  );
}

function PartialBody() {
  // mid-translation lost (G4.7): solo prime frasi, poi inline retry + CTA EN
  return (
    <div className="reading-body" data-fs="md" data-lh="default">
      <p>
        La nebbia si solleva lentamente sulle paludi di Cuanacht.{' '}
        <span className="glossary-term" title={GLOSSARY['Niamh']}>Niamh</span>{' '}
        ti aspetta sulla riva, le mani strette attorno all’elsa della{' '}
        <span className="glossary-term" title={GLOSSARY['Spada di Avalon']}>Spada di Avalon</span>…
      </p>
      <p style={{
        display: 'flex', alignItems: 'center', gap: '10px',
        fontSize: '15px', fontFamily: 'var(--f-body)',
        color: 'var(--text-muted)', fontStyle: 'italic',
        padding: '12px 14px',
        background: 'hsl(var(--c-warning) / 0.10)',
        borderLeft: '3px solid hsl(var(--c-warning))',
        borderRadius: 'var(--r-sm)',
      }}>
        <span className="mai-spinner" style={{
          width: 14, height: 14, border: '2px solid currentColor',
          borderRightColor: 'transparent', borderRadius: '50%', display: 'inline-block',
        }}/>
        <span>Sto riprovando… connessione instabile</span>
      </p>
      <button style={{
        marginTop: 0,
        background: 'transparent',
        border: '1px solid hsl(var(--c-kb) / 0.5)',
        color: 'hsl(var(--c-kb))',
        padding: '10px 14px',
        borderRadius: 'var(--r-md)',
        fontFamily: 'var(--f-display)',
        fontWeight: 700,
        fontSize: 13,
        cursor: 'pointer',
      }} onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }}>
        Mostra EN intanto →
      </button>
    </div>
  );
}

function NotFoundBody({ pnum = '§299' }) {
  return (
    <div className="empty" id={undefined}>
      <div className="glyph">📕</div>
      <h2>{pnum} non trovato</h2>
      <p>Questo paragrafo non esiste in <em>Tainted Grail</em>. Probabilmente è un riferimento errato o un typo.</p>
      <div className="actions">
        <button className="nav-btn primary" onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-game-onboarding.html'; }, 0); /* DEMO-NAV */ }}>← Torna al play</button>
        <button className="nav-btn">📷 Scansiona il libro</button>
        <button className="nav-btn ghost" onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }}>🐛 Segnala link errato</button>
      </div>
    </div>
  );
}

/* ────────────────────────── BOTTOM CONTROLS ────────────────────────── */

function BottomControls({
  mode = 'IT', onMode = () => {},
  fs = 'md', onFs = () => {},
  lh = 'default', onLh = () => {},
  theme = 'light', onTheme = () => {},
  navPrev = '§146', navNext = '§148',
}) {
  return (
    <div className="tv-bottom">
      {/* Row 1 — navigazione paragrafi */}
      <div className="row">
        <button className="nav-btn ghost" onClick={() => { setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }}>← {navPrev}</button>
        <button className="nav-btn primary">Letto, vai a {navNext} →</button>
      </div>

      {/* Toggle modes IT / EN / IT+EN */}
      <div className="row center">
        <div className="seg" role="tablist" aria-label="Modalità lingua">
          <button className={mode==='IT'?'active':''} onClick={(e) => { (() => onMode('IT'))(e); setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }}>IT</button>
          <button className={mode==='EN'?'active':''} onClick={() => onMode('EN')}>EN</button>
          <button className={mode==='SBS'?'active':''} onClick={(e) => { (() => onMode('SBS'))(e); setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }}>IT + EN</button>
        </div>
      </div>

      {/* Row 2 — reading aids: font · line · theme */}
      <div className="row aids">
        <div className="seg" aria-label="Dimensione testo">
          <button className={fs==='sm'?'active':''} onClick={(e) => { (()=>onFs('sm'))(e); setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }} title="A− 24px / 18pt">A−</button>
          <button className={fs==='md'?'active':''} onClick={()=>onFs('md')} title="A 26px / 20pt default">A</button>
          <button className={fs==='lg'?'active':''} onClick={(e) => { (()=>onFs('lg'))(e); setTimeout(() => { window.location.href = 'librogame-runthrough-game-detail.html'; }, 0); /* DEMO-NAV */ }} title="A+ 32px / 24pt lettura distante">A+</button>
        </div>
        <div className="seg" aria-label="Interlinea">
          <button className={lh==='tight'?'active':''} onClick={()=>onLh('tight')} title="1.4">≡<sub style={{fontSize:9}}>1.4</sub></button>
          <button className={lh==='default'?'active':''} onClick={()=>onLh('default')} title="1.6">≡<sub style={{fontSize:9}}>1.6</sub></button>
          <button className={lh==='loose'?'active':''} onClick={()=>onLh('loose')} title="1.8">≡<sub style={{fontSize:9}}>1.8</sub></button>
        </div>
        <div className="seg" aria-label="Tema">
          <button className={theme==='light'?'active':''} onClick={()=>onTheme('light')} title="Light">☀</button>
          <button className={theme==='dark'?'active':''} onClick={()=>onTheme('dark')} title="Dark">🌙</button>
          <button className={theme==='sepia'?'active':''} onClick={()=>onTheme('sepia')} title="Sepia (= light)">📜</button>
        </div>
      </div>

      {/* Row 3 — TTS placeholder disabled */}
      <div className="row center">
        <span className="tts-disabled" aria-disabled="true" title="Disponibile in v2">
          🎙️ Leggi tu <span className="v2">v2</span>
        </span>
      </div>
    </div>
  );
}

/* ────────────────────────── TASK 4 sez 1d — LANG DETECTION + MANUAL INPUT ────────────────────────── */

/* Languages v1: 5 supportate · target IT fisso (NO target dropdown UI) */
const SUPPORTED_LANGS = [
  { code: 'EN', flag: '🇬🇧', label: 'English',  sub: 'EN · uk · us' },
  { code: 'FR', flag: '🇫🇷', label: 'Français', sub: 'FR · fr · be · ca' },
  { code: 'DE', flag: '🇩🇪', label: 'Deutsch',  sub: 'DE · de · at · ch' },
  { code: 'ES', flag: '🇪🇸', label: 'Español',  sub: 'ES · es · mx · ar' },
  { code: 'IT', flag: '🇮🇹', label: 'Italiano', sub: 'IT · it · ch · sm' },
];

/**
 * LangDetectionBadge — Header pill post OCR (state K).
 *
 * 3 variants confidence-driven:
 *   - high   (>0.8)   → informativo, no friction (success color)
 *   - medium (0.5-0.8)→ tap-to-confirm BLOCKING prima di translate (warning color)
 *   - low    (<0.5)   → cross-link state-H source-lang-unknown (danger color, animated pulse)
 *
 * onClick → apre <LangOverrideModal/> (state L).
 * Cross-ref: state-H in librogame-runthrough-error-states.html (sez 1a Task 1).
 */
function LangDetectionBadge({ langCode = 'EN', confidence = 0.92, onClick }) {
  const lang = SUPPORTED_LANGS.find(l => l.code === langCode) || SUPPORTED_LANGS[0];
  let variant = 'high';
  if (confidence < 0.5) variant = 'low';
  else if (confidence < 0.8) variant = 'medium';

  const ariaLabel = variant === 'high'
    ? `Sorgente rilevata: ${lang.label} (alta affidabilità ${confidence.toFixed(2)}) — tap per cambiare`
    : variant === 'medium'
      ? `Sorgente probabile: ${lang.label} (confidence ${confidence.toFixed(2)}) — conferma o cambia prima di tradurre`
      : `Lingua non riconosciuta (confidence ${confidence.toFixed(2)}) — scegli manualmente`;

  return (
    <button
      type="button"
      className={`lang-detect-badge ${variant}`}
      aria-label={ariaLabel}
      onClick={onClick}
    >
      <span className="flag">{variant === 'low' ? '⚠️' : lang.flag}</span>
      <span>{variant === 'low' ? '?' : lang.code}</span>
      <span className="conf">{confidence.toFixed(2)}</span>
    </button>
  );
}

/**
 * LangOverrideModal — Modal radio group (state L).
 *
 * Tap sul badge K → modal con 5 lingue v1.
 * CTA "Conferma e ritraduci" → re-trigger AI translation step 3 (skip step 2 OCR cache hit).
 * Pre-selected: lingua auto-detected dal badge.
 *
 * Behavior:
 *   - OCR result preservato (smoldocling cache key (photo_hash, page) invariante)
 *   - Re-translate solo step 3 della loading sequence Task 2
 *   - Cost: solo DeepSeek (~0,01 €), no smoldocling recompute
 */
function LangOverrideModal({
  selectedCode = 'FR',
  autoDetectedCode = 'FR',
  onConfirm = () => {},
  onCancel = () => {},
}) {
  return (
    <div className="lang-modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="lang-modal-title">
      <div className="lang-modal">
        <div className="lang-modal-head">
          <h3 id="lang-modal-title">Lingua sorgente del libro</h3>
          <p>5 lingue v1 supportate · target IT fisso</p>
        </div>
        <div className="lang-modal-body">
          <ul className="lang-radio-list">
            {SUPPORTED_LANGS.map(l => {
              const isSelected = l.code === selectedCode;
              const isAuto = l.code === autoDetectedCode;
              return (
                <li
                  key={l.code}
                  className={`lang-radio-item ${isSelected ? 'selected' : ''}`}
                  role="button"
                  tabIndex={0}
                  aria-current={isSelected ? 'true' : undefined}
                >
                  <span className="flag">{l.flag}</span>
                  <span className="l">
                    <span className="v">{l.label}</span>
                    <span className="s">{l.sub}</span>
                  </span>
                  {isAuto && <span className="auto-tag">Auto</span>}
                  <span className="radio" aria-hidden="true" />
                </li>
              );
            })}
          </ul>
        </div>
        <div className="lang-modal-foot">
          <button className="cta-primary" type="button" autoFocus onClick={onConfirm}>
            Conferma e ritraduci
          </button>
          <button className="cta-secondary" type="button" onClick={onCancel}>
            Annulla
          </button>
          <p className="cache-hint">
            <strong>✓ OCR preservato</strong> · re-translate solo step 3 (skip OCR cache hit)
          </p>
        </div>
      </div>
    </div>
  );
}

/**
 * ManualInputMode — Layout fallback senza foto (state M).
 *
 * Trigger via query param `?mode=manual` (UI-only, NO sub-route backend).
 * Triple-entry discoverability:
 *   1. CTA "Digita manualmente" in state-F photo-blur-pre-ocr (error-states.html)
 *   2. Kebab menu ⋮ del camera step (sp6-libro-game-photo-upload.html)
 *   3. Empty state card "Libro non a portata?" (qui · default route /translate)
 *
 * Layout: header sticky (title + back + lang dropdown + book ref chip)
 *       + textarea multiline (max 2000 char + counter visibile bottom-right)
 *       + CTA "Traduci" → skip step 2 OCR, va a step 3 AI translation (loading state-G)
 *
 * State: source_method = "manual", source_lang locked dopo conferma, lang_detection_confidence = null.
 * Parity: stessi step 3-4 della loading sequence Task 2. Diff = step 2 OCR sostituito da textarea content.
 */
function ManualInputMode({
  initialText = '',
  initialLang = 'EN',
  bookRef = 'Runa di Ardenel',
  maxChars = 2000,
}) {
  const [text, setText] = useState(initialText);
  const [lang, setLang] = useState(initialLang);
  const langInfo = SUPPORTED_LANGS.find(l => l.code === lang) || SUPPORTED_LANGS[0];
  const charCount = text.length;
  const counterClass =
    charCount > maxChars ? 'over' :
    charCount > maxChars * 0.9 ? 'warn' : '';
  const disabled = charCount === 0 || charCount > maxChars;

  return (
    <div className="manual-input-shell">
      {/* Header sticky */}
      <div className="manual-input-head">
        <div className="title-row">
          <button className="back" aria-label="Indietro">←</button>
          <h2>Inserimento manuale</h2>
          <span className="badge-manual">Manual</span>
        </div>
        <div className="meta-row">
          {/* Lang dropdown pre-filled last-used */}
          <button className="manual-lang-dropdown" type="button" aria-label={`Lingua sorgente: ${langInfo.label} — cambia`}>
            <span className="flag">{langInfo.flag}</span>
            <span>{langInfo.code}</span>
            <span className="chevron">▾</span>
          </button>
          {/* Book ref chip pre-filled current campaign book */}
          <span className="book-ref-chip" aria-label={`Libro corrente — ${bookRef}`}>
            <span className="lock">🔒</span>📕 {bookRef}
          </span>
        </div>
      </div>

      {/* Body: textarea + counter + hint */}
      <div className="manual-input-body">
        <div className="manual-textarea-shell">
          <textarea
            className="manual-textarea"
            placeholder="Incolla o digita il paragrafo del libro…"
            aria-label="Testo paragrafo da tradurre"
            maxLength={maxChars}
            value={text}
            onChange={e => setText(e.target.value)}
          />
          <span className={`manual-char-counter ${counterClass}`} aria-live="polite" aria-label={`${charCount} caratteri su ${maxChars} massimi`}>
            {charCount} <span className="max">/ {maxChars}</span>
          </span>
        </div>
        <div className="manual-hint">
          <strong>💡 Suggerimento:</strong> Incolla esattamente il paragrafo dal libro. Salta step 2 OCR e va direttamente alla traduzione. Glossario applicato in step 4.
        </div>
      </div>

      {/* Footer sticky con CTA */}
      <div className="manual-input-foot">
        <button className="manual-cta-primary" type="button" disabled={disabled} aria-label="Traduci il testo manuale">
          <span>Traduci</span><span className="arrow">→</span>
        </button>
        <p className="manual-flow-note">
          <strong>→ step 3 streaming</strong> (skip OCR) · ~10s · 💰 ~0,01 €
        </p>
      </div>
    </div>
  );
}

/**
 * ManualEntryCard — Empty state card (state M entry-point #3 di 3).
 * Render quando /translate aperto senza foto.
 * Click → naviga a ?mode=manual.
 */
function ManualEntryCard() {
  return (
    <div
      className="manual-empty-card"
      role="button"
      tabIndex={0}
      aria-label="Libro non a portata? Digita il paragrafo manualmente"
      onClick={() => { window.location.href = 'librogame-runthrough-translate-viewer.html?mode=manual'; /* DEMO-NAV: triple-entry #3 */ }}
    >
      <span className="icon">⌨️</span>
      <div className="l">
        <span className="v">Libro non a portata?</span>
        <span className="s">Digita manualmente il paragrafo · skip foto</span>
      </div>
      <span className="arrow">›</span>
    </div>
  );
}

/* ────────────────────────── BANNERS ────────────────────────── */

function OfflineBanner() {
  return (
    <div className="tv-banner warn" role="status" aria-live="polite">
      <span>🟡</span>
      <span>Connessione persa — testo cached</span>
      <span style={{ flex: 1 }} />
      <span className="retry-dot mai-pulse" aria-hidden="true" />
      <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11 }}>auto-retry</span>
    </div>
  );
}

/* ────────────────────────── MOBILE SCREEN ────────────────────────── */

function MobileScreen({
  state,                           // 'default-it' | 'side-by-side' | 'en-replace' | 'loading' | 'offline-cached' | 'mid-lost' | 'not-found'
  fs = 'md', lh = 'default',
  mode = 'IT',                     // for default mostly
  themeOverride = null,            // 'dark' | 'sepia' (light) — for theme proofs
  pnum = '§147',
  showMenu = false,
}) {
  const dataTheme = themeOverride === 'dark' ? 'dark' : 'light';
  const phoneClass = 'phone-sp6' + (themeOverride === 'dark' ? ' t-dark' : '');

  let body = null;
  let banner = null;
  let bottomMode = mode;

  if (state === 'default-it') {
    body = (
      <div className="tv-body mai-noscroll">
        <BodyHead pnum={pnum} />
        <ReadingBodyIT fs={fs} lh={lh} />
      </div>
    );
  } else if (state === 'en-replace') {
    body = (
      <div className="tv-body mai-noscroll">
        <BodyHead pnum={pnum} />
        <ReadingBodyEN fs={fs} lh={lh} kicker="Originale · EN" />
      </div>
    );
    bottomMode = 'EN';
  } else if (state === 'side-by-side') {
    // Su mobile fa stack verticale (brief)
    body = (
      <div className="tv-body mai-noscroll">
        <BodyHead pnum={pnum} />
        <ReadingBodyIT fs="sm" lh={lh} kicker="Italiano" />
        <div style={{ height: 1, background: 'var(--border)', width: '60ch', maxWidth: '100%', margin: '20px 0' }} />
        <ReadingBodyEN fs="sm" lh={lh} kicker="Originale · EN" />
      </div>
    );
    bottomMode = 'SBS';
  } else if (state === 'loading') {
    body = (
      <div className="tv-body mai-noscroll">
        <BodyHead pnum={pnum} />
        <SkeletonBody />
      </div>
    );
  } else if (state === 'offline-cached') {
    banner = <OfflineBanner />;
    body = (
      <div className="tv-body mai-noscroll">
        <BodyHead pnum={pnum} />
        <ReadingBodyIT fs={fs} lh={lh} kicker="📦 Cached · 2 min fa" />
      </div>
    );
  } else if (state === 'mid-lost') {
    banner = <OfflineBanner />;
    body = (
      <div className="tv-body mai-noscroll">
        <BodyHead pnum={pnum} />
        <PartialBody />
      </div>
    );
  } else if (state === 'not-found') {
    body = <NotFoundBody pnum="§299" />;
    bottomMode = 'IT';
  }

  return (
    <div className={phoneClass} data-theme={dataTheme}>
      <StatusBar />
      <div className="phone-screen" style={{ background: 'var(--bg)' }}>
        <TopBar pnum={state === 'not-found' ? '§299' : pnum} menuOpen={showMenu} onToggleMenu={() => {}} />
        {banner}
        {body}
        {state !== 'not-found' && (
          <BottomControls
            mode={bottomMode}
            fs={fs}
            lh={lh}
            theme={themeOverride === 'dark' ? 'dark' : (themeOverride === 'sepia' ? 'sepia' : 'light')}
            onMode={() => {}}
            onFs={() => {}}
            onLh={() => {}}
            onTheme={() => {}}
          />
        )}
      </div>
    </div>
  );
}

/* ────────────────────────── DESKTOP SCREEN ────────────────────────── */

function DesktopScreen({ variant = 'sbs', themeOverride = null }) {
  const dataTheme = themeOverride === 'dark' ? 'dark' : 'light';

  let body = null;
  let bottomMode = 'IT';
  if (variant === 'sbs') {
    bottomMode = 'SBS';
    body = (
      <div className="tv-body mai-noscroll" style={{ alignItems: 'stretch' }}>
        <div style={{ width: '100%', maxWidth: 1200, margin: '0 auto' }}>
          <BodyHead pnum="§147" />
          <div className="sbs-grid">
            <div className="sbs-col">
              <div className="col-label it">Italiano · IT</div>
              <ReadingBodyIT fs="md" lh="default" />
            </div>
            <div className="sbs-col">
              <div className="col-label en">Originale · EN</div>
              <ReadingBodyEN fs="md" lh="default" kicker={null} />
            </div>
          </div>
        </div>
      </div>
    );
  } else if (variant === 'default-it') {
    body = (
      <div className="tv-body mai-noscroll">
        <BodyHead pnum="§147" />
        <ReadingBodyIT fs="md" lh="default" />
      </div>
    );
  } else if (variant === 'not-found') {
    body = <NotFoundBody pnum="§299" />;
  }

  return (
    <div className="desktop-frame" data-theme={dataTheme}>
      <DesktopChrome url="meepleai.app/gamebook/tainted-grail/play/paragraph/147" />
      <div style={{
        height: 720,
        display: 'flex', flexDirection: 'column',
        background: 'var(--bg)',
        color: 'var(--text)',
      }}>
        <TopBar pnum={variant === 'not-found' ? '§299' : '§147'} menuOpen={false} onToggleMenu={() => {}} />
        {body}
        {variant !== 'not-found' && (
          <BottomControls
            mode={bottomMode}
            fs="md" lh="default"
            theme={themeOverride === 'dark' ? 'dark' : 'light'}
            onMode={()=>{}} onFs={()=>{}} onLh={()=>{}} onTheme={()=>{}}
          />
        )}
      </div>
    </div>
  );
}

/* ────────────────────────── MOUNTS ────────────────────────── */

function MobileSection() {
  // 7 stati richiesti, ognuno con anchor id state-NN-…
  const items = [
    { id: 'state-01-default-it',     label: '01 · Default IT',
      sub: 'Tainted Grail §147 · glossary: Niamh, Spada di Avalon, Pietra Spettrale',
      gherkin: '@G4 default',
      screen: <MobileScreen state="default-it" /> },
    { id: 'state-02-side-by-side',   label: '02 · Side-by-side IT+EN (mobile = stack)',
      sub: 'Mobile fa stack verticale · brief riga 525',
      gherkin: '@G4 sbs',
      screen: <MobileScreen state="side-by-side" /> },
    { id: 'state-03-en-replace',     label: '03 · EN replace',
      sub: 'Kicker top "Originale · EN" · toggle bottom = EN',
      gherkin: '@G4 en',
      screen: <MobileScreen state="en-replace" /> },
    { id: 'state-04-loading',        label: '04 · Loading skeleton',
      sub: '5 righe shimmer · no full-screen blocking spinner',
      gherkin: '@G4 loading',
      screen: <MobileScreen state="loading" /> },
    { id: 'state-05-offline-cached', label: '05 · Offline cached',
      sub: 'Banner top + auto-retry discreto · cached 2 min fa',
      gherkin: '@G4.7 cached',
      screen: <MobileScreen state="offline-cached" /> },
    { id: 'state-06-mid-translation-lost', label: '06 · Mid-translation lost',
      sub: 'Partial: ultime righe + "Sto riprovando…" inline + CTA "Mostra EN"',
      gherkin: '@G4.7 partial',
      screen: <MobileScreen state="mid-lost" /> },
    { id: 'state-07-not-found',      label: '07 · Not found §299',
      sub: 'Full-screen empty + 3 azioni (Torna play · Scansiona · Segnala)',
      gherkin: '@G4.4 not-found',
      screen: <MobileScreen state="not-found" /> },
  ];
  return (
    <>
      {items.map(it => (
        <div className="frame-cell" id={it.id} key={it.id}>
          <FrameMeta anchor={it.id} gherkin={it.gherkin} label={it.label} sub={it.sub} />
          {it.screen}
        </div>
      ))}
    </>
  );
}

function DesktopSection() {
  const items = [
    { id: 'state-02-side-by-side-desktop', label: '02d · Side-by-side IT|EN (desktop 2 col 50/50)',
      gherkin: '@G4 sbs-desktop', sub: 'Glossary applicato a IT (3 termini)', variant: 'sbs' },
    { id: 'state-01-default-it-desktop', label: '01d · Default IT (desktop)',
      gherkin: '@G4 default-desktop', sub: 'Reading column 60ch · centered', variant: 'default-it' },
    { id: 'state-07-not-found-desktop', label: '07d · Not found §299 (desktop)',
      gherkin: '@G4.4 not-found-desktop', sub: '3 azioni stack centered', variant: 'not-found' },
  ];
  return (
    <>
      {items.map(it => (
        <div id={it.id} key={it.id} style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <FrameMeta anchor={it.id} gherkin={it.gherkin} label={it.label} sub={it.sub} />
          <DesktopScreen variant={it.variant} />
        </div>
      ))}
    </>
  );
}

function ThemesSection() {
  return (
    <>
      <div className="frame-cell" id="state-01-default-it-dark">
        <FrameMeta anchor="state-01-default-it-dark" gherkin="@G4 dark"
                   label="01·dark — Default IT, dark theme"
                   sub="Token dark · glossary kb hue shifted lighter" />
        <MobileScreen state="default-it" themeOverride="dark" />
      </div>
      <div className="frame-cell" id="state-01-default-it-sepia">
        <FrameMeta anchor="state-01-default-it-sepia" gherkin="@G4 sepia"
                   label="01·sepia — Default IT, sepia theme"
                   sub="Sepia = light token map (warm sand --bg) — NO nuova palette · brief L566" />
        <MobileScreen state="default-it" themeOverride="sepia" />
      </div>
      <div className="frame-cell" id="state-04-loading-dark">
        <FrameMeta anchor="state-04-loading-dark" gherkin="@G4 loading-dark"
                   label="04·dark — Loading skeleton, dark"
                   sub="5 righe shimmer · prefers-reduced-motion guard" />
        <MobileScreen state="loading" themeOverride="dark" />
      </div>
    </>
  );
}

function AidsSection() {
  return (
    <>
      <div className="frame-cell" id="aid-fs-sm">
        <FrameMeta anchor="aid-fs-sm" gherkin="@aids font" label="A− · 24px (18pt)" sub="Font size step 1 di 3" />
        <MobileScreen state="default-it" fs="sm" />
      </div>
      <div className="frame-cell" id="aid-fs-md">
        <FrameMeta anchor="aid-fs-md" gherkin="@aids font" label="A · 26px (20pt) default" sub="Brief riga 519 — DEVIAZIONE da --fs-xl documentata in CSS" />
        <MobileScreen state="default-it" fs="md" />
      </div>
      <div className="frame-cell" id="aid-fs-lg">
        <FrameMeta anchor="aid-fs-lg" gherkin="@aids font" label="A+ · 32px (24pt) lettura distante" sub="Vincolo non-functional vision §4.2" />
        <MobileScreen state="default-it" fs="lg" />
      </div>
      <div className="frame-cell" id="aid-lh-tight">
        <FrameMeta anchor="aid-lh-tight" gherkin="@aids line" label="≡ 1.4 — tight" sub="Line spacing variant 1 di 3" />
        <MobileScreen state="default-it" fs="md" lh="tight" />
      </div>
      <div className="frame-cell" id="aid-lh-default">
        <FrameMeta anchor="aid-lh-default" gherkin="@aids line" label="≡ 1.6 — default" sub="Brief riga 521 — esplicito" />
        <MobileScreen state="default-it" fs="md" lh="default" />
      </div>
      <div className="frame-cell" id="aid-lh-loose">
        <FrameMeta anchor="aid-lh-loose" gherkin="@aids line" label="≡ 1.8 — loose" sub="Line spacing variant 3 di 3" />
        <MobileScreen state="default-it" fs="md" lh="loose" />
      </div>
    </>
  );
}

/* ────────────────────────── TASK 4 SEZ 1d · MULTI-LANG + MANUAL INPUT SECTION ────────────────────────── */

/**
 * MultiLangSection — Stati K (badge), L (modal), M (manual input).
 *
 * Demonstra:
 *   · 3 variants di LangDetectionBadge (high / medium / low)
 *   · LangOverrideModal con FR pre-selected (from medium badge)
 *   · ManualInputMode con textarea pre-filled
 *   · ManualEntryCard (empty state entry-point #3)
 *
 * Lingue v1: 🇬🇧 EN · 🇫🇷 FR · 🇩🇪 DE · 🇪🇸 ES · 🇮🇹 IT.
 * Cross-ref: librogame-runthrough-translate-viewer.html stati K/L/M (sez 1d Task 4).
 */
function MultiLangSection() {
  const items = [
    {
      id: 'state-K1-lang-detect-high',
      label: 'K.1 · LangDetectionBadge — high confidence (>0.8)',
      gherkin: '@TASK4-1d badge-high',
      sub: 'EN 0.92 · informativo · no friction · success color',
      screen: (
        <div className="phone-sp6" data-theme="light">
          <StatusBar />
          <div className="phone-screen" style={{ background: 'var(--bg)' }}>
            <div className="tv-topbar">
              <button className="icon-btn" aria-label="Indietro">←</button>
              <div className="center">§147</div>
              <LangDetectionBadge langCode="EN" confidence={0.92} />
            </div>
            <div className="tv-body mai-noscroll">
              <BodyHead pnum="§147" />
              <ReadingBodyIT fs="md" lh="default" />
            </div>
          </div>
        </div>
      ),
    },
    {
      id: 'state-K2-lang-detect-medium',
      label: 'K.2 · LangDetectionBadge — medium confidence (0.5-0.8)',
      gherkin: '@TASK4-1d badge-medium',
      sub: 'FR 0.67 · tap-to-confirm · BLOCKING prima di translate · warning color',
      screen: (
        <div className="phone-sp6" data-theme="light">
          <StatusBar />
          <div className="phone-screen" style={{ background: 'var(--bg)' }}>
            <div className="tv-topbar">
              <button className="icon-btn" aria-label="Indietro">←</button>
              <div className="center">§147</div>
              <LangDetectionBadge langCode="FR" confidence={0.67} />
            </div>
            <div className="tv-body mai-noscroll">
              <BodyHead pnum="§147" />
              <p style={{
                padding: '20px',
                fontSize: '14px',
                color: 'var(--text-sec)',
                textAlign: 'center',
                fontStyle: 'italic',
              }}>
                ⏸ Traduzione in pausa — tappa il badge per confermare la lingua sorgente prima di procedere.
              </p>
            </div>
          </div>
        </div>
      ),
    },
    {
      id: 'state-K3-lang-detect-low',
      label: 'K.3 · LangDetectionBadge — low confidence (<0.5) · cross-link state-H',
      gherkin: '@TASK4-1d badge-low',
      sub: '? 0.31 · cross-link state-H source-lang-unknown (error-states.html sez 1a)',
      screen: (
        <div className="phone-sp6" data-theme="light">
          <StatusBar />
          <div className="phone-screen" style={{ background: 'var(--bg)' }}>
            <div className="tv-topbar">
              <button className="icon-btn" aria-label="Indietro">←</button>
              <div className="center">§147</div>
              <LangDetectionBadge langCode="EN" confidence={0.31} />
            </div>
            <div className="tv-body mai-noscroll">
              <p style={{
                padding: '20px',
                fontSize: '14px',
                color: 'var(--text)',
                textAlign: 'center',
                lineHeight: 1.55,
              }}>
                ⚠️ Lingua non riconosciuta. Devi scegliere manualmente prima di procedere.
                <br /><br />
                <span style={{ fontSize: '12px', color: 'hsl(var(--c-kb))', fontFamily: 'var(--f-mono)' }}>
                  → state-H source-lang-unknown<br/>in <code>librogame-runthrough-error-states.html</code>
                </span>
              </p>
            </div>
          </div>
        </div>
      ),
    },
    {
      id: 'state-L-lang-override-modal',
      label: 'L · LangOverrideModal — radio group 5 lingue v1 (EN·FR·DE·ES·IT)',
      gherkin: '@TASK4-1d modal',
      sub: 'FR pre-selected (Auto tag) · CTA "Conferma e ritraduci" · OCR preservato (cache hit)',
      screen: (
        <div className="phone-sp6" data-theme="light" style={{ position: 'relative' }}>
          <StatusBar />
          <div className="phone-screen" style={{ background: 'var(--bg)' }}>
            <div className="tv-topbar">
              <button className="icon-btn" aria-label="Indietro">←</button>
              <div className="center">§147</div>
              <LangDetectionBadge langCode="FR" confidence={0.67} />
            </div>
            <div className="tv-body mai-noscroll" style={{ opacity: 0.4 }}>
              <BodyHead pnum="§147" />
            </div>
            <LangOverrideModal selectedCode="FR" autoDetectedCode="FR" />
          </div>
        </div>
      ),
    },
    {
      id: 'state-M-manual-input-mode',
      label: 'M · ManualInputMode — textarea fallback (?mode=manual) · 247/2000 char',
      gherkin: '@TASK4-1d manual-input',
      sub: 'No foto · lang dropdown EN pre-filled · book ref chip Runa di Ardenel · counter 247/2000',
      screen: (
        <div className="phone-sp6" data-theme="light">
          <StatusBar />
          <div className="phone-screen" style={{ background: 'var(--bg)' }}>
            <ManualInputMode
              initialText="As Niamh approached the altar, the air grew thick with whispers. The Goblin guards drew their blades, eyes gleaming with bloodlust, while the Voidstone pulsed with sickly light upon the dais."
              initialLang="EN"
              bookRef="Runa di Ardenel"
            />
          </div>
        </div>
      ),
    },
    {
      id: 'state-M-manual-input-warn',
      label: 'M · ManualInputMode — counter warn (1856/2000)',
      gherkin: '@TASK4-1d manual-input-warn',
      sub: 'DE lang · counter > 90% (yellow warn) · paragrafo lungo',
      screen: (
        <div className="phone-sp6" data-theme="light">
          <StatusBar />
          <div className="phone-screen" style={{ background: 'var(--bg)' }}>
            <ManualInputMode
              initialText={'Als Niamh sich dem Altar näherte, wurde die Luft dicht von Geflüster. Die Goblin-Wachen zogen ihre Klingen, die Augen funkelten vor Blutlust, während der Leerenstein mit krankem Licht auf dem Podest pulsierte. Niamh umklammerte den Schwertgriff fester und fühlte das Echo alter Eide im Stahl schwingen. Sie hatte zwei Möglichkeiten zur Wahl... [paragrafo lungo continua per altre 1400+ caratteri inclusi dettagli combattimento, descrizioni ambientali, e dialogo del capitano Goblin che minaccia la compagnia di avventurieri]'.padEnd(1856, ' ')}
              initialLang="DE"
              bookRef="Runa di Ardenel"
            />
          </div>
        </div>
      ),
    },
    {
      id: 'state-M-empty-card',
      label: 'M.3 · ManualEntryCard — empty state (entry-point #3 di 3)',
      gherkin: '@TASK4-1d empty-card',
      sub: '/translate aperto senza foto · card "Libro non a portata?" → ?mode=manual',
      screen: (
        <div className="phone-sp6" data-theme="light">
          <StatusBar />
          <div className="phone-screen" style={{ background: 'var(--bg)' }}>
            <div className="tv-topbar">
              <button className="icon-btn" aria-label="Indietro">←</button>
              <div className="center">§147</div>
              <button className="icon-btn" aria-label="Altre azioni">⋯</button>
            </div>
            <div className="tv-body mai-noscroll" style={{ display: 'flex', flexDirection: 'column', gap: 12, padding: 16 }}>
              <div style={{ textAlign: 'center', padding: '20px 12px' }}>
                <div style={{ fontSize: 56, opacity: 0.4, marginBottom: 8 }}>📷</div>
                <h3 style={{ fontFamily: 'var(--f-display)', fontSize: 18, fontWeight: 700, margin: '0 0 8px' }}>Fotografa il paragrafo</h3>
                <p style={{ color: 'var(--text-sec)', margin: '0 0 16px', fontSize: 13 }}>OCR + traduzione automatica.</p>
                <button className="nav-btn primary" type="button" style={{ padding: '10px 20px' }}>📷 Apri fotocamera</button>
              </div>
              <ManualEntryCard />
              <p style={{ fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', textAlign: 'center', margin: 0 }}>
                Entry-point 3 di 3 · vedi state-F error-states + kebab ⋮ camera step
              </p>
            </div>
          </div>
        </div>
      ),
    },
  ];

  return (
    <>
      {items.map(it => (
        <div className="frame-cell" id={it.id} key={it.id}>
          <FrameMeta anchor={it.id} gherkin={it.gherkin} label={it.label} sub={it.sub} />
          {it.screen}
        </div>
      ))}
    </>
  );
}

/* ────────────────────────── BOOT ────────────────────────── */

const ReactDOMClient = ReactDOM;
ReactDOMClient.createRoot(document.getElementById('mount-mobile')).render(<MobileSection />);
ReactDOMClient.createRoot(document.getElementById('mount-desktop')).render(<DesktopSection />);
ReactDOMClient.createRoot(document.getElementById('mount-themes')).render(<ThemesSection />);
ReactDOMClient.createRoot(document.getElementById('mount-aids')).render(<AidsSection />);
/* Task 4 sez 1d · mount opzionale per nuova section (host page deve avere <div id="mount-multilang"/>) */
const multilangMount = document.getElementById('mount-multilang');
if (multilangMount) {
  ReactDOMClient.createRoot(multilangMount).render(<MultiLangSection />);
}
