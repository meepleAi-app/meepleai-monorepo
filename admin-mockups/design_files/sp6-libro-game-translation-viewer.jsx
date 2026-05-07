/* SP6 D — Translation Viewer Fullscreen
   Brief SP6 §D (lines 492-566). 7 stati + theme/device variants.

   Anchor id su ogni screen wrapper (state-NN-…) + Gherkin tag (G4.4 / G4.7).
   Reading-body: 26px Nunito assoluto, line-height 1.6, max-width 60ch — DEVIAZIONE brief L519.
   prefers-reduced-motion guard ereditato (mai-shimmer / mai-spinner / mai-pulse).
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
      }}>
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
        <button className="nav-btn primary">← Torna al play</button>
        <button className="nav-btn">📷 Scansiona il libro</button>
        <button className="nav-btn ghost">🐛 Segnala link errato</button>
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
        <button className="nav-btn ghost">← {navPrev}</button>
        <button className="nav-btn primary">Letto, vai a {navNext} →</button>
      </div>

      {/* Toggle modes IT / EN / IT+EN */}
      <div className="row center">
        <div className="seg" role="tablist" aria-label="Modalità lingua">
          <button className={mode==='IT'?'active':''} onClick={() => onMode('IT')}>IT</button>
          <button className={mode==='EN'?'active':''} onClick={() => onMode('EN')}>EN</button>
          <button className={mode==='SBS'?'active':''} onClick={() => onMode('SBS')}>IT + EN</button>
        </div>
      </div>

      {/* Row 2 — reading aids: font · line · theme */}
      <div className="row aids">
        <div className="seg" aria-label="Dimensione testo">
          <button className={fs==='sm'?'active':''} onClick={()=>onFs('sm')} title="A− 24px / 18pt">A−</button>
          <button className={fs==='md'?'active':''} onClick={()=>onFs('md')} title="A 26px / 20pt default">A</button>
          <button className={fs==='lg'?'active':''} onClick={()=>onFs('lg')} title="A+ 32px / 24pt lettura distante">A+</button>
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

/* ────────────────────────── BOOT ────────────────────────── */

const ReactDOMClient = ReactDOM;
ReactDOMClient.createRoot(document.getElementById('mount-mobile')).render(<MobileSection />);
ReactDOMClient.createRoot(document.getElementById('mount-desktop')).render(<DesktopSection />);
ReactDOMClient.createRoot(document.getElementById('mount-themes')).render(<ThemesSection />);
ReactDOMClient.createRoot(document.getElementById('mount-aids')).render(<AidsSection />);
