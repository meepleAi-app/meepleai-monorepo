/* MeepleAI SP4 — PDF Dedup/Riuso · AddGameDrawer · Issue #912
   Route: AddGameDrawer (step: pdf-upload)
   File: admin-mockups/design_files/sp4-add-game-pdf-dedup.{html,jsx}

   Surface: PDF dedup detection & reuse decision when uploading a PDF
   during PrivateGame creation. Backend signals SHA256 collision.

   Entity color: --c-kb (teal/green 174 60% 40%)

   6 screen states stacked vertically:
   1. Dedup — Source: catalogo community (shared catalog match)
   2. Dedup — Source: tua libreria     (same user, different game)
   3. Dedup — Source: altro utente     (different user, privacy-safe)
   4. Confirm reuse                    (success animation + ETA)
   5. Force re-upload                  (4-stage progress bar)
   6. AGENT_CREATION_FAILED            (banner, --c-event red tint)

   Light + dark mode. Responsive mobile-first, desktop 2-col variant.
*/

const { useState, useEffect, useRef } = React;
const DS = window.DS;

// ─── ENTITY HSL HELPER ─────────────────────────────────
const kbHsl = (alpha) => {
  // --c-kb: 174 60% 40%  (light)   174 60% 55% (dark)
  const ec = DS.EC.kb;
  return alpha !== undefined
    ? `hsla(${ec.h}, ${ec.s}%, ${ec.l}%, ${alpha})`
    : `hsl(${ec.h}, ${ec.s}%, ${ec.l}%)`;
};

const eventHsl = (alpha) => {
  const ec = DS.EC.event;
  return alpha !== undefined
    ? `hsla(${ec.h}, ${ec.s}%, ${ec.l}%, ${alpha})`
    : `hsl(${ec.h}, ${ec.s}%, ${ec.l}%)`;
};

// ─── SAMPLE DATA ───────────────────────────────────────
const SOURCE_CATALOG = {
  gameTitle: 'Gloomhaven',
  cover: 'linear-gradient(135deg, hsl(5,35%,40%), hsl(5,25%,22%) 60%, hsl(35,25%,30%))',
  coverEmoji: '⚔️',
  totalUsers: 847,
  estimatedTimeSaved: '5 min',
  estimatedCostSaved: '$0.30',
  pdfName: 'gloomhaven-rulebook.pdf',
  pdfSize: '18.4 MB',
  chunks: 312,
};

const SOURCE_OWN = {
  gameTitle: 'Catan (la mia copia)',
  cover: 'linear-gradient(135deg, hsl(30,80%,50%), hsl(30,60%,30%) 60%, hsl(60,50%,35%))',
  coverEmoji: '🌾',
  pdfName: 'catan-regole.pdf',
  pdfSize: '3.1 MB',
  chunks: 62,
};

const SOURCE_OTHER_USER = {
  catalogTimes: 42,
  pdfName: 'wingspan-rules.pdf',
  pdfSize: '4.8 MB',
  chunks: 89,
};

// ─── KICKER BADGE ──────────────────────────────────────
const KickerBadge = () => (
  <div style={{
    display: 'inline-flex', alignItems: 'center', gap: 6,
    padding: '3px 10px', borderRadius: 9999,
    background: kbHsl(0.12), color: kbHsl(),
    fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
    textTransform: 'uppercase', letterSpacing: '.08em',
  }}>
    <span aria-hidden="true">📄</span>
    Mockup · sp4-add-game-pdf-dedup · #912
  </div>
);

// ─── SECTION HEADER ────────────────────────────────────
const SectionHeader = ({ n, title, desc }) => (
  <div style={{ marginBottom: 24 }}>
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8,
    }}>
      <div style={{
        width: 28, height: 28, borderRadius: 8,
        background: kbHsl(0.15), color: kbHsl(),
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontFamily: 'var(--f-mono)', fontSize: 12, fontWeight: 800,
        flexShrink: 0,
      }}>{n}</div>
      <h2 style={{
        fontFamily: 'var(--f-display)', fontWeight: 800,
        fontSize: 18, color: 'var(--text)', margin: 0,
      }}>{title}</h2>
    </div>
    {desc && (
      <p style={{
        fontSize: 13, color: 'var(--text-muted)', lineHeight: 1.55,
        margin: '0 0 0 38px', maxWidth: 680,
      }}>{desc}</p>
    )}
  </div>
);

// ─── MINI COVER THUMBNAIL ──────────────────────────────
const CoverThumb = ({ cover, emoji, size = 52 }) => (
  <div style={{
    width: size, height: size,
    borderRadius: 10, background: cover,
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    fontSize: Math.floor(size * 0.48), flexShrink: 0,
    boxShadow: '0 2px 8px rgba(0,0,0,.18)',
  }} aria-hidden="true">{emoji}</div>
);

// ─── PDF FILE CHIP ─────────────────────────────────────
const PdfChip = ({ name, size }) => (
  <div style={{
    display: 'inline-flex', alignItems: 'center', gap: 6,
    padding: '4px 10px', borderRadius: 8,
    background: 'var(--bg-muted)', border: '1px solid var(--border)',
    fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)',
    fontWeight: 600, maxWidth: '100%',
  }}>
    <span aria-hidden="true" style={{ color: kbHsl() }}>📄</span>
    <span style={{
      overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
    }}>{name}</span>
    {size && <span style={{ color: 'var(--text-muted)', flexShrink: 0 }}>· {size}</span>}
  </div>
);

// ─── SAVINGS HINT CHIP ─────────────────────────────────
const SavingsHint = ({ time, cost }) => (
  <div style={{
    display: 'inline-flex', alignItems: 'center', gap: 6,
    padding: '4px 12px', borderRadius: 9999,
    background: 'hsl(142 60% 40% / 0.1)',
    border: '1px solid hsl(142 60% 40% / 0.2)',
    color: 'hsl(142, 60%, 38%)',
    fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
  }}>
    <span aria-hidden="true">⚡</span>
    Risparmi ~{time} e ~{cost}
  </div>
);

// ─── PRIMARY CTA BUTTON ────────────────────────────────
const PrimaryBtn = ({ children, onClick, color = 'kb', disabled = false }) => {
  const bg = color === 'kb' ? kbHsl() : eventHsl();
  const shadow = color === 'kb' ? kbHsl(0.35) : eventHsl(0.35);
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      style={{
        padding: '10px 18px', borderRadius: 10,
        background: disabled ? 'var(--bg-muted)' : bg,
        color: disabled ? 'var(--text-muted)' : '#fff',
        border: 'none',
        fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800,
        cursor: disabled ? 'not-allowed' : 'pointer',
        boxShadow: disabled ? 'none' : `0 4px 14px ${shadow}`,
        transition: 'all 180ms cubic-bezier(.16,1,.3,1)',
        whiteSpace: 'nowrap',
      }}
    >{children}</button>
  );
};

// ─── SECONDARY CTA BUTTON ──────────────────────────────
const SecondaryBtn = ({ children, onClick }) => (
  <button
    type="button"
    onClick={onClick}
    style={{
      padding: '10px 18px', borderRadius: 10,
      background: 'transparent',
      color: 'var(--text-sec)',
      border: '1px solid var(--border-strong)',
      fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
      cursor: 'pointer',
      transition: 'all 180ms cubic-bezier(.16,1,.3,1)',
      whiteSpace: 'nowrap',
    }}
  >{children}</button>
);

// ─── GHOST LINK BUTTON ─────────────────────────────────
const GhostLinkBtn = ({ children, onClick }) => (
  <button
    type="button"
    onClick={onClick}
    style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      padding: '2px 0', background: 'transparent', border: 'none',
      color: kbHsl(), fontFamily: 'var(--f-display)', fontSize: 12,
      fontWeight: 700, cursor: 'pointer',
      textDecoration: 'underline', textDecorationColor: kbHsl(0.4),
      textUnderlineOffset: 3,
    }}
  >{children}</button>
);

// ─── DEDUP CARD SHELL ──────────────────────────────────
// Shared card container for states 1-3
const DedupCard = ({ children, accentHsl = kbHsl }) => (
  <div style={{
    background: 'var(--bg-card)',
    border: `1px solid ${accentHsl(0.25)}`,
    borderRadius: 16,
    padding: '20px 22px',
    boxShadow: `0 4px 20px ${accentHsl(0.1)}, 0 1px 4px rgba(0,0,0,.06)`,
    display: 'flex', flexDirection: 'column', gap: 16,
  }}>{children}</div>
);

// ─── STATE 1: DEDUP — SOURCE CATALOG ───────────────────
const State1CatalogDedup = ({ compact }) => {
  const [choice, setChoice] = useState(null);
  const g = SOURCE_CATALOG;
  return (
    <DedupCard>
      {/* Header row */}
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 14 }}>
        <CoverThumb cover={g.cover} emoji={g.coverEmoji}/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 6, marginBottom: 5,
          }}>
            <span style={{
              display: 'inline-flex', alignItems: 'center', gap: 4,
              padding: '2px 8px', borderRadius: 9999,
              background: kbHsl(0.12), color: kbHsl(),
              fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform: 'uppercase', letterSpacing: '.06em',
            }}>
              <span aria-hidden="true">🌐</span>
              Catalogo community
            </span>
          </div>
          <h3 style={{
            fontFamily: 'var(--f-display)', fontWeight: 800,
            fontSize: 15, color: 'var(--text)', margin: '0 0 4px',
            lineHeight: 1.2,
          }}>Questo PDF è già nel catalogo community</h3>
          <p style={{
            fontSize: 12.5, color: 'var(--text-sec)', margin: 0, lineHeight: 1.5,
          }}>
            Trovato in <strong style={{ color: 'var(--text)' }}>{g.gameTitle}</strong>
            {' '}· Usato da <strong style={{ color: 'var(--text)' }}>{g.totalUsers.toLocaleString()} utenti</strong>
            {' '}· {g.chunks} chunk indicizzati
          </p>
        </div>
      </div>

      {/* PDF chip */}
      <PdfChip name={g.pdfName} size={g.pdfSize}/>

      {/* Savings */}
      <SavingsHint time={g.estimatedTimeSaved} cost={g.estimatedCostSaved}/>

      {/* Info note */}
      <div style={{
        padding: '10px 13px', borderRadius: 9,
        background: kbHsl(0.07), border: `1px solid ${kbHsl(0.18)}`,
        fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.55,
      }}>
        <strong style={{ color: kbHsl() }}>Riusando gli indici</strong> non viene eseguita
        una nuova indicizzazione. L'agent sarà disponibile in pochi secondi, non minuti.
      </div>

      {/* CTAs */}
      <div style={{
        display: 'flex', flexWrap: 'wrap', gap: 10,
        paddingTop: 4,
      }}>
        <PrimaryBtn onClick={() => setChoice('reuse')}>
          ✓ Sì, riusa indici
        </PrimaryBtn>
        <SecondaryBtn onClick={() => setChoice('copy')}>
          Carica nuova copia
        </SecondaryBtn>
      </div>

      {choice && (
        <div style={{
          padding: '8px 12px', borderRadius: 8,
          background: choice === 'reuse' ? kbHsl(0.1) : 'var(--bg-muted)',
          color: choice === 'reuse' ? kbHsl() : 'var(--text-muted)',
          fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
        }}>
          {choice === 'reuse' ? '→ Passaggio a: State 4 (Confirm reuse)' : '→ Passaggio a: State 5 (Force re-upload)'}
        </div>
      )}
    </DedupCard>
  );
};

// ─── STATE 2: DEDUP — SOURCE TUA LIBRERIA ──────────────
const State2OwnLibraryDedup = ({ compact }) => {
  const [choice, setChoice] = useState(null);
  const g = SOURCE_OWN;
  return (
    <DedupCard>
      {/* Header row */}
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 14 }}>
        <CoverThumb cover={g.cover} emoji={g.coverEmoji}/>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 6, marginBottom: 5,
          }}>
            <span style={{
              display: 'inline-flex', alignItems: 'center', gap: 4,
              padding: '2px 8px', borderRadius: 9999,
              background: 'hsl(var(--c-player) / 0.12)',
              color: 'hsl(var(--c-player))',
              fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform: 'uppercase', letterSpacing: '.06em',
            }}>
              <span aria-hidden="true">📚</span>
              Tua libreria
            </span>
          </div>
          <h3 style={{
            fontFamily: 'var(--f-display)', fontWeight: 800,
            fontSize: 15, color: 'var(--text)', margin: '0 0 4px',
            lineHeight: 1.2,
          }}>
            Hai già questo PDF in{' '}
            <span style={{ color: kbHsl() }}>{g.gameTitle}</span>
          </h3>
          <p style={{
            fontSize: 12.5, color: 'var(--text-sec)', margin: 0, lineHeight: 1.5,
          }}>
            {g.chunks} chunk già indicizzati · nessun costo aggiuntivo
          </p>
        </div>
        {/* Open link */}
        <GhostLinkBtn onClick={() => {}}>
          <span aria-hidden="true">↗</span> Apri
        </GhostLinkBtn>
      </div>

      {/* PDF chip */}
      <PdfChip name={g.pdfName} size={g.pdfSize}/>

      {/* Info note */}
      <div style={{
        padding: '10px 13px', borderRadius: 9,
        background: 'hsl(var(--c-player) / 0.07)',
        border: '1px solid hsl(var(--c-player) / 0.18)',
        fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.55,
      }}>
        Riusare gli indici di <strong>{g.gameTitle}</strong> ti permette di condividere
        la Knowledge Base tra più giochi nella tua libreria senza duplicare lo spazio.
      </div>

      {/* CTAs */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, paddingTop: 4 }}>
        <PrimaryBtn onClick={() => setChoice('reuse')}>
          ✓ Sì, riusa
        </PrimaryBtn>
        <SecondaryBtn onClick={() => setChoice('copy')}>
          Carica nuova copia
        </SecondaryBtn>
      </div>

      {choice && (
        <div style={{
          padding: '8px 12px', borderRadius: 8,
          background: choice === 'reuse' ? kbHsl(0.1) : 'var(--bg-muted)',
          color: choice === 'reuse' ? kbHsl() : 'var(--text-muted)',
          fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
        }}>
          {choice === 'reuse' ? '→ Passaggio a: State 4 (Confirm reuse)' : '→ Passaggio a: State 5 (Force re-upload)'}
        </div>
      )}
    </DedupCard>
  );
};

// ─── STATE 3: DEDUP — SOURCE ALTRO UTENTE (PRIVACY) ────
const State3OtherUserDedup = ({ compact }) => {
  const [choice, setChoice] = useState(null);
  const g = SOURCE_OTHER_USER;
  return (
    <DedupCard>
      {/* Header row — NO user identifier, only anonymized count */}
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 14 }}>
        {/* Anonymized avatar */}
        <div style={{
          width: 52, height: 52, borderRadius: 10, flexShrink: 0,
          background: 'var(--bg-muted)',
          border: '2px dashed var(--border-strong)',
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 22, color: 'var(--text-muted)',
        }} aria-hidden="true" title="Utente anonimizzato">🔒</div>

        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            display: 'flex', alignItems: 'center', gap: 6, marginBottom: 5,
          }}>
            <span style={{
              display: 'inline-flex', alignItems: 'center', gap: 4,
              padding: '2px 8px', borderRadius: 9999,
              background: 'var(--bg-muted)',
              color: 'var(--text-muted)',
              fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
              textTransform: 'uppercase', letterSpacing: '.06em',
            }}>
              <span aria-hidden="true">👥</span>
              Utente esterno
            </span>
          </div>
          <h3 style={{
            fontFamily: 'var(--f-display)', fontWeight: 800,
            fontSize: 15, color: 'var(--text)', margin: '0 0 4px',
            lineHeight: 1.2,
          }}>Questo PDF è stato caricato da un altro utente</h3>
          <p style={{
            fontSize: 12.5, color: 'var(--text-sec)', margin: 0, lineHeight: 1.5,
          }}>
            Condiviso da <strong style={{ color: 'var(--text)' }}>{g.catalogTimes} utenti</strong>
            {' '}· {g.chunks} chunk disponibili
          </p>
        </div>
      </div>

      {/* PDF chip */}
      <PdfChip name={g.pdfName} size={g.pdfSize}/>

      {/* PRIVACY NOTICE — prominent */}
      <div style={{
        padding: '12px 14px', borderRadius: 10,
        background: kbHsl(0.08), border: `1px solid ${kbHsl(0.22)}`,
        display: 'flex', gap: 10, alignItems: 'flex-start',
      }}>
        <span aria-hidden="true" style={{ fontSize: 18, flexShrink: 0, marginTop: 1 }}>🔒</span>
        <div>
          <div style={{
            fontFamily: 'var(--f-display)', fontSize: 12.5, fontWeight: 800,
            color: kbHsl(), marginBottom: 3,
          }}>Privacy garantita</div>
          <div style={{
            fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.55,
          }}>
            Riusare questi indici è più veloce e non richiede re-embedding.{' '}
            <strong style={{ color: 'var(--text)' }}>
              Nessun dato personale dell'altro utente è esposto:
            </strong>{' '}
            nome, email e profilo rimangono completamente anonimi.
            Solo il contenuto indicizzato del PDF viene riutilizzato.
          </div>
        </div>
      </div>

      {/* CTAs */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, paddingTop: 4 }}>
        <PrimaryBtn onClick={() => setChoice('reuse')}>
          ✓ Sì, riusa
        </PrimaryBtn>
        <SecondaryBtn onClick={() => setChoice('copy')}>
          Carica nuova copia
        </SecondaryBtn>
      </div>

      {choice && (
        <div style={{
          padding: '8px 12px', borderRadius: 8,
          background: choice === 'reuse' ? kbHsl(0.1) : 'var(--bg-muted)',
          color: choice === 'reuse' ? kbHsl() : 'var(--text-muted)',
          fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
        }}>
          {choice === 'reuse' ? '→ Passaggio a: State 4 (Confirm reuse)' : '→ Passaggio a: State 5 (Force re-upload)'}
        </div>
      )}
    </DedupCard>
  );
};

// ─── STATE 4: CONFIRM REUSE (success animation) ────────
const SuccessCheckmark = () => (
  <div style={{
    width: 64, height: 64, borderRadius: '50%',
    background: kbHsl(0.15),
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    fontSize: 30,
    animation: 'mai-pulse-kb 2s ease-in-out infinite',
    boxShadow: `0 0 0 0 ${kbHsl(0.4)}`,
  }} aria-label="Successo">
    ✓
  </div>
);

const EtaSpinner = () => (
  <div style={{
    display: 'inline-flex', alignItems: 'center', gap: 8,
    padding: '6px 14px', borderRadius: 9999,
    background: kbHsl(0.1), border: `1px solid ${kbHsl(0.2)}`,
    color: kbHsl(), fontFamily: 'var(--f-mono)', fontSize: 11, fontWeight: 700,
  }}>
    <span style={{
      width: 12, height: 12, borderRadius: '50%',
      border: `2px solid ${kbHsl(0.3)}`,
      borderTopColor: kbHsl(),
      display: 'inline-block',
      animation: 'mai-spin 0.8s linear infinite',
    }} aria-hidden="true"/>
    Agent disponibile in ~5s
  </div>
);

const State4ConfirmReuse = () => (
  <div style={{
    background: 'var(--bg-card)',
    border: `1px solid ${kbHsl(0.3)}`,
    borderRadius: 16,
    padding: '32px 28px',
    boxShadow: `0 4px 24px ${kbHsl(0.12)}`,
    display: 'flex', flexDirection: 'column',
    alignItems: 'center', textAlign: 'center', gap: 18,
  }}>
    <SuccessCheckmark/>

    <div>
      <h3 style={{
        fontFamily: 'var(--f-display)', fontWeight: 800,
        fontSize: 17, color: 'var(--text)', margin: '0 0 6px',
      }}>Indici riusati con successo</h3>
      <p style={{
        fontSize: 13, color: 'var(--text-sec)', margin: 0, lineHeight: 1.55,
      }}>
        Nessuna re-indicizzazione necessaria.
        La Knowledge Base è stata collegata al tuo gioco.
      </p>
    </div>

    {/* Stat pills */}
    <div style={{
      display: 'flex', flexWrap: 'wrap', gap: 8, justifyContent: 'center',
    }}>
      {[
        { icon: '⚡', label: 'Tempo risparmiato', value: '~5 min' },
        { icon: '💰', label: 'Costo risparmiato', value: '~$0.30' },
        { icon: '📊', label: 'Chunk riusati', value: '312' },
      ].map(pill => (
        <div key={pill.label} style={{
          display: 'flex', flexDirection: 'column', alignItems: 'center',
          padding: '8px 16px', borderRadius: 10,
          background: 'var(--bg-muted)', border: '1px solid var(--border)',
          minWidth: 90,
        }}>
          <span aria-hidden="true" style={{ fontSize: 16, marginBottom: 2 }}>{pill.icon}</span>
          <span style={{
            fontFamily: 'var(--f-display)', fontSize: 15, fontWeight: 800,
            color: kbHsl(), lineHeight: 1.1,
          }}>{pill.value}</span>
          <span style={{
            fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700,
            color: 'var(--text-muted)', textTransform: 'uppercase',
            letterSpacing: '.05em', marginTop: 2,
          }}>{pill.label}</span>
        </div>
      ))}
    </div>

    <EtaSpinner/>
  </div>
);

// ─── STATE 5: FORCE RE-UPLOAD (4-stage progress bar) ───
const STAGES = [
  { id: 'queued',    label: 'In coda',      icon: '⏳', desc: 'Il file è in attesa di elaborazione' },
  { id: 'extract',  label: 'Estrazione',   icon: '📑', desc: 'Estrazione testo e struttura dal PDF' },
  { id: 'chunk',    label: 'Chunking',     icon: '✂️', desc: 'Suddivisione in segmenti semantici' },
  { id: 'embed',    label: 'Embedding',    icon: '🧠', desc: 'Generazione vettori e indicizzazione' },
];

const ProgressBar4Stage = ({ activeStage = 1 }) => (
  <div>
    {/* Stage indicators */}
    <div style={{
      display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)',
      gap: 6, marginBottom: 10,
    }}>
      {STAGES.map((stage, i) => {
        const isDone = i < activeStage;
        const isActive = i === activeStage;
        const isPending = i > activeStage;
        return (
          <div key={stage.id} style={{
            display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 5,
          }}>
            <div style={{
              width: '100%', height: 5, borderRadius: 9999,
              background: isDone
                ? kbHsl()
                : isActive
                  ? `linear-gradient(90deg, ${kbHsl()} 60%, ${kbHsl(0.2)} 100%)`
                  : kbHsl(0.15),
              position: 'relative', overflow: 'hidden',
            }}>
              {isActive && (
                <div style={{
                  position: 'absolute', inset: 0,
                  background: `linear-gradient(90deg, transparent 0%, rgba(255,255,255,0.35) 50%, transparent 100%)`,
                  animation: 'mai-sheen 1.6s ease-in-out infinite',
                }}/>
              )}
            </div>
            <div style={{
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2,
            }}>
              <span aria-hidden="true" style={{
                fontSize: 14,
                opacity: isPending ? 0.4 : 1,
              }}>{isDone ? '✓' : stage.icon}</span>
              <span style={{
                fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 700,
                textTransform: 'uppercase', letterSpacing: '.04em',
                color: isDone ? kbHsl() : isActive ? 'var(--text)' : 'var(--text-muted)',
              }}>{stage.label}</span>
            </div>
          </div>
        );
      })}
    </div>
    {/* Active stage description */}
    <div style={{
      padding: '8px 12px', borderRadius: 8,
      background: kbHsl(0.08), border: `1px solid ${kbHsl(0.15)}`,
      display: 'flex', alignItems: 'center', gap: 8,
    }}>
      <span style={{
        width: 10, height: 10, borderRadius: '50%',
        border: `2px solid ${kbHsl(0.35)}`,
        borderTopColor: kbHsl(),
        display: 'inline-block',
        animation: 'mai-spin 0.8s linear infinite', flexShrink: 0,
      }} aria-hidden="true"/>
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)', fontWeight: 600,
      }}>
        <strong style={{ color: kbHsl() }}>{STAGES[activeStage]?.label}</strong>
        {' '}— {STAGES[activeStage]?.desc}
      </span>
    </div>
  </div>
);

const State5ForceReUpload = () => {
  const [activeStage, setActiveStage] = useState(1);

  // Cycle through stages for demo
  useEffect(() => {
    const iv = setInterval(() => {
      setActiveStage(s => (s + 1) % STAGES.length);
    }, 2200);
    return () => clearInterval(iv);
  }, []);

  return (
    <div style={{
      background: 'var(--bg-card)',
      border: '1px solid var(--border)',
      borderRadius: 16,
      padding: '22px 24px',
      display: 'flex', flexDirection: 'column', gap: 16,
    }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12 }}>
        <div style={{
          width: 40, height: 40, borderRadius: 10,
          background: kbHsl(0.12), color: kbHsl(),
          display: 'flex', alignItems: 'center', justifyContent: 'center',
          fontSize: 20, flexShrink: 0,
        }} aria-hidden="true">📤</div>
        <div>
          <h3 style={{
            fontFamily: 'var(--f-display)', fontWeight: 800,
            fontSize: 15, color: 'var(--text)', margin: '0 0 3px',
          }}>Nuova copia caricata</h3>
          <p style={{
            fontSize: 12.5, color: 'var(--text-sec)', margin: 0, lineHeight: 1.5,
          }}>
            Indicizzazione in corso (3–5 min). Puoi chiudere — ti notificheremo al termine.
          </p>
        </div>
      </div>

      <PdfChip name={SOURCE_CATALOG.pdfName} size={SOURCE_CATALOG.pdfSize}/>

      {/* 4-stage progress bar */}
      <ProgressBar4Stage activeStage={activeStage}/>

      {/* ETA */}
      <div style={{
        display: 'flex', justifyContent: 'space-between', alignItems: 'center',
        flexWrap: 'wrap', gap: 8,
      }}>
        <span style={{
          fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', fontWeight: 600,
        }}>
          Tempo stimato: 3–5 min
        </span>
        <span style={{
          padding: '3px 10px', borderRadius: 9999,
          background: 'var(--bg-muted)', border: '1px solid var(--border)',
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
          color: 'var(--text-muted)',
        }}>
          Fase {activeStage + 1}/{STAGES.length}
        </span>
      </div>
    </div>
  );
};

// ─── STATE 6: AGENT_CREATION_FAILED BANNER ─────────────
const State6AgentCreationFailed = () => (
  <div style={{
    padding: '14px 16px',
    borderRadius: 12,
    background: eventHsl(0.09),
    border: `1px solid ${eventHsl(0.3)}`,
    display: 'flex', gap: 12, alignItems: 'flex-start',
    boxShadow: `0 2px 12px ${eventHsl(0.12)}`,
  }}>
    {/* Warning icon */}
    <div style={{
      width: 36, height: 36, borderRadius: 9,
      background: eventHsl(0.15), color: eventHsl(),
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: 18, flexShrink: 0,
    }} aria-hidden="true">⚠</div>

    <div style={{ flex: 1, minWidth: 0 }}>
      {/* Title + error code */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8, marginBottom: 5, flexWrap: 'wrap',
      }}>
        <span style={{
          fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 800,
          color: eventHsl(),
        }}>Agent non creato</span>
        <span style={{
          padding: '1px 7px', borderRadius: 6,
          background: eventHsl(0.15),
          fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
          color: eventHsl(), letterSpacing: '.04em',
        }}>AGENT_SLOT_QUOTA_EXCEEDED</span>
      </div>

      {/* Description */}
      <p style={{
        fontSize: 12.5, color: 'var(--text-sec)', margin: '0 0 12px', lineHeight: 1.55,
      }}>
        Hai raggiunto il limite di <strong style={{ color: 'var(--text)' }}>1 agent</strong>{' '}
        disponibile sul tuo piano free-tier. Il PDF è stato caricato e indicizzato correttamente —
        solo la creazione automatica dell'agent è fallita.
      </p>

      {/* Inline CTAs */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8 }}>
        <button type="button" style={{
          padding: '7px 14px', borderRadius: 8,
          background: eventHsl(), color: '#fff',
          border: 'none',
          fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 800,
          cursor: 'pointer',
          boxShadow: `0 3px 10px ${eventHsl(0.3)}`,
        }}>
          ↑ Upgrade tier
        </button>
        <button type="button" style={{
          padding: '7px 14px', borderRadius: 8,
          background: 'transparent',
          border: `1px solid ${eventHsl(0.4)}`,
          color: eventHsl(),
          fontFamily: 'var(--f-display)', fontSize: 12, fontWeight: 700,
          cursor: 'pointer',
        }}>
          🗑 Elimina agent esistente
        </button>
      </div>
    </div>
  </div>
);

// ─── RESPONSIVE LAYOUT WRAPPER ─────────────────────────
// Shows a phone frame + description below
const PhoneShell = ({ label, desc, children }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 10 }}>
    <div style={{
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)',
      textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700,
    }}>{label}</div>
    <div className="phone">
      <PhoneSbar/>
      <div style={{
        flex: 1, overflowY: 'auto', background: 'var(--bg)',
        padding: '16px 14px 32px',
        display: 'flex', flexDirection: 'column', gap: 16,
      }}>
        {children}
      </div>
    </div>
    {desc && (
      <div style={{
        fontSize: 11, color: 'var(--text-muted)', maxWidth: 340,
        textAlign: 'center', lineHeight: 1.55,
      }}>{desc}</div>
    )}
  </div>
);

const PhoneSbar = () => (
  <div className="phone-sbar" style={{ color: 'var(--text)' }}>
    <span style={{ fontFamily: 'var(--f-mono)' }}>14:32</span>
    <div className="ind"><span aria-hidden="true">●●●●</span><span aria-hidden="true">100%</span></div>
  </div>
);

// Desktop frame wrapper
const DesktopFrame = ({ label, desc, children }) => (
  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, width: '100%' }}>
    <div style={{
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-sec)',
      textTransform: 'uppercase', letterSpacing: '.08em', fontWeight: 700,
    }}>{label}</div>
    <div style={{
      width: '100%', maxWidth: 860,
      borderRadius: 16, border: '1px solid var(--border)',
      background: 'var(--bg)', overflow: 'hidden',
      boxShadow: 'var(--shadow-lg)',
    }}>
      {/* Browser chrome */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 8,
        padding: '9px 14px',
        background: 'var(--bg-muted)', borderBottom: '1px solid var(--border)',
        fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
      }}>
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-event))' }}/>
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-warning))' }}/>
        <span style={{ width: 11, height: 11, borderRadius: '50%', background: 'hsl(var(--c-toolkit))' }}/>
        <span style={{ flex: 1, textAlign: 'center', letterSpacing: '.04em' }}>
          meepleai.app · AddGameDrawer › Upload PDF
        </span>
      </div>
      {children}
    </div>
    {desc && (
      <div style={{
        fontSize: 11, color: 'var(--text-muted)', maxWidth: 720,
        textAlign: 'center', lineHeight: 1.55,
      }}>{desc}</div>
    )}
  </div>
);

// ─── DESKTOP 2-COL LAYOUT FOR DEDUP STATES ─────────────
// Side-by-side: left = upload area context, right = dedup card
const DrawerDeupDesktop = ({ cardContent, stateLabel }) => (
  <div style={{ padding: '24px 28px', background: 'var(--bg)' }}>
    {/* Drawer header */}
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10, marginBottom: 20,
      paddingBottom: 16, borderBottom: '1px solid var(--border-light)',
    }}>
      <div style={{
        width: 32, height: 32, borderRadius: 9,
        background: kbHsl(0.12), color: kbHsl(),
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: 16, flexShrink: 0,
      }} aria-hidden="true">📄</div>
      <div>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 15, color: 'var(--text)',
        }}>Aggiungi gioco — Carica PDF</div>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)', fontWeight: 600,
        }}>Step 3 / 4 · Knowledge Base</div>
      </div>
      <div style={{ flex: 1 }}/>
      <span style={{
        padding: '2px 9px', borderRadius: 9999,
        background: kbHsl(0.1), color: kbHsl(),
        fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 800,
        textTransform: 'uppercase', letterSpacing: '.05em',
      }}>PDF già trovato</span>
    </div>

    {/* 2-column grid */}
    <div style={{
      display: 'grid',
      gridTemplateColumns: '1fr 1.1fr',
      gap: 20, alignItems: 'start',
    }}>
      {/* Left: upload context */}
      <div style={{
        display: 'flex', flexDirection: 'column', gap: 12,
      }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
          textTransform: 'uppercase', letterSpacing: '.06em', color: 'var(--text-muted)',
        }}>File caricato</div>
        {/* Drop zone (frozen, file already dropped) */}
        <div style={{
          border: `2px dashed ${kbHsl(0.35)}`,
          borderRadius: 12,
          background: kbHsl(0.05),
          padding: '18px 16px',
          display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 8,
          textAlign: 'center',
        }}>
          <span aria-hidden="true" style={{ fontSize: 32 }}>📄</span>
          <div style={{
            fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700,
            color: 'var(--text)',
          }}>{SOURCE_CATALOG.pdfName}</div>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)', fontWeight: 600,
          }}>
            {SOURCE_CATALOG.pdfSize} · SHA-256 match rilevato
          </div>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 5,
            padding: '3px 10px', borderRadius: 9999,
            background: kbHsl(0.1), color: kbHsl(),
            fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
          }}>
            <span aria-hidden="true">🔍</span>
            Duplicato rilevato
          </div>
        </div>

        {/* What happens info */}
        <div style={{
          padding: '12px 14px', borderRadius: 10,
          background: 'var(--bg-muted)', border: '1px solid var(--border)',
          fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.6,
        }}>
          <strong style={{ color: 'var(--text)', display: 'block', marginBottom: 5 }}>
            Cosa succede se riusi?
          </strong>
          <ul style={{ margin: 0, paddingLeft: 16, display: 'flex', flexDirection: 'column', gap: 3 }}>
            <li>✅ Indici già pronti — agent attivo subito</li>
            <li>✅ Nessun costo di embedding</li>
            <li>✅ KB condivisa aggiornabile da te</li>
          </ul>
        </div>
      </div>

      {/* Right: dedup decision card */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
          textTransform: 'uppercase', letterSpacing: '.06em', color: 'var(--text-muted)',
        }}>Sorgente rilevata</div>
        {cardContent}
      </div>
    </div>
  </div>
);

// ─── THEME TOGGLE ──────────────────────────────────────
function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }, [dark]);
  return (
    <button onClick={() => setDark(d => !d)} className="theme-toggle"
      aria-label={dark ? 'Tema chiaro' : 'Tema scuro'}>
      <span aria-hidden="true">{dark ? '🌙' : '☀️'}</span>
      <span>{dark ? 'Dark' : 'Light'}</span>
    </button>
  );
}

// ─── MAIN APP ──────────────────────────────────────────
function App() {
  return (
    <div className="stage">
      <ThemeToggle/>
      <div className="stage-wrap">

        {/* ── PAGE HEADER ── */}
        <div style={{ marginBottom: 8 }}><KickerBadge/></div>
        <h1 style={{ fontFamily: 'var(--f-display)', fontWeight: 800, fontSize: 32, margin: '0 0 8px' }}>
          PDF Dedup / Riuso — AddGameDrawer
        </h1>
        <p className="lead" style={{ color: 'var(--text-sec)', fontSize: 14, maxWidth: 680, marginBottom: 40, lineHeight: 1.6 }}>
          UI per la gestione dei duplicati PDF (SHA-256) durante il flusso di creazione gioco privato.
          Il backend rileva un match esistente e il frontend propone riuso o copia separata.
          6 stati · entità <strong style={{ color: kbHsl() }}>KB</strong> (teal) ·
          notifica errore su <strong style={{ color: eventHsl() }}>--c-event</strong>.
          <br/>
          <a href="00-hub.html" style={{ color: kbHsl(), textDecoration: 'underline', fontSize: 12 }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-dashboard.html'; }, 0); /* DEMO-NAV */ }}>← Torna al hub</a>
        </p>

        {/* ════════════════════════════════════════════════
            MOBILE SECTION — 6 phone states
        ════════════════════════════════════════════════ */}
        <div className="section-label">Mobile · 375 — 6 stati</div>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(370px, 1fr))',
          gap: 36, alignItems: 'start', marginBottom: 64,
        }}>

          {/* State 1 — Catalog dedup */}
          <PhoneShell
            label="01 · Dedup · Catalogo community"
            desc="PDF trovato nel catalogo condiviso: mostra gioco sorgente con cover thumbnail, utenti, chunk. CTA riusa/copia.">
            <div style={{
              display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8,
            }}>
              <span style={{
                padding: '2px 7px', borderRadius: 6,
                background: kbHsl(0.1), color: kbHsl(),
                fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform: 'uppercase',
              }}>Step 3/4</span>
              <span style={{
                fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700, color: 'var(--text)',
              }}>Carica PDF</span>
            </div>
            <State1CatalogDedup compact/>
          </PhoneShell>

          {/* State 2 — Own library dedup */}
          <PhoneShell
            label="02 · Dedup · Tua libreria"
            desc="PDF già presente in un gioco dell'utente. Link diretto al gioco sorgente. Badge player-color.">
            <div style={{
              display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8,
            }}>
              <span style={{
                padding: '2px 7px', borderRadius: 6,
                background: kbHsl(0.1), color: kbHsl(),
                fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform: 'uppercase',
              }}>Step 3/4</span>
              <span style={{
                fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700, color: 'var(--text)',
              }}>Carica PDF</span>
            </div>
            <State2OwnLibraryDedup compact/>
          </PhoneShell>

          {/* State 3 — Other user dedup (privacy) */}
          <PhoneShell
            label="03 · Dedup · Altro utente (privacy)"
            desc="PDF caricato da un utente esterno. Avatar anonimizzato 🔒, nessun dato personale esposto. Privacy badge prominente.">
            <div style={{
              display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8,
            }}>
              <span style={{
                padding: '2px 7px', borderRadius: 6,
                background: kbHsl(0.1), color: kbHsl(),
                fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform: 'uppercase',
              }}>Step 3/4</span>
              <span style={{
                fontFamily: 'var(--f-display)', fontSize: 13, fontWeight: 700, color: 'var(--text)',
              }}>Carica PDF</span>
            </div>
            <State3OtherUserDedup compact/>
          </PhoneShell>

          {/* State 4 — Confirm reuse */}
          <PhoneShell
            label="04 · Confirm Reuse (successo)"
            desc="Animazione pulse + checkmark verde. Stat pills: tempo/costo risparmiato, chunk riusati. Spinner ETA 5s.">
            <div style={{
              display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8,
            }}>
              <span style={{
                padding: '2px 7px', borderRadius: 6,
                background: kbHsl(0.1), color: kbHsl(),
                fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform: 'uppercase',
              }}>Riuso confermato</span>
            </div>
            <State4ConfirmReuse/>
          </PhoneShell>

          {/* State 5 — Force re-upload */}
          <PhoneShell
            label="05 · Force Re-Upload (progress)"
            desc="4-stage progress bar animata: In coda → Estrazione → Chunking → Embedding. Stage label + spinner corrente.">
            <div style={{
              display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8,
            }}>
              <span style={{
                padding: '2px 7px', borderRadius: 6,
                background: 'var(--bg-muted)', color: 'var(--text-sec)',
                fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform: 'uppercase',
              }}>Re-upload</span>
            </div>
            <State5ForceReUpload/>
          </PhoneShell>

          {/* State 6 — AGENT_CREATION_FAILED */}
          <PhoneShell
            label="06 · AGENT_CREATION_FAILED (banner)"
            desc="Toast/banner rosso --c-event. Errore AGENT_SLOT_QUOTA_EXCEEDED. 2 CTA inline: Upgrade / Elimina agent esistente.">
            <div style={{
              display: 'flex', alignItems: 'center', gap: 8, marginBottom: 8,
            }}>
              <span style={{
                padding: '2px 7px', borderRadius: 6,
                background: eventHsl(0.12), color: eventHsl(),
                fontFamily: 'var(--f-mono)', fontSize: 9, fontWeight: 800,
                textTransform: 'uppercase',
              }}>Errore post-upload</span>
            </div>
            <State6AgentCreationFailed/>
            {/* Show a "success" KB indexing note above the error to provide context */}
            <div style={{
              padding: '10px 12px', borderRadius: 10,
              background: kbHsl(0.08), border: `1px solid ${kbHsl(0.18)}`,
              fontSize: 12, color: 'var(--text-sec)', lineHeight: 1.5,
            }}>
              <strong style={{ color: kbHsl() }}>✓ KB indicizzata correttamente.</strong>{' '}
              Solo l'agent auto-creation è fallita per quota.
            </div>
          </PhoneShell>
        </div>

        {/* ════════════════════════════════════════════════
            DESKTOP SECTION — 2-col drawer layout
        ════════════════════════════════════════════════ */}
        <div className="section-label">Desktop · Drawer layout 2-colonne</div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 36, marginBottom: 64 }}>

          <DesktopFrame
            label="07 · Desktop · Dedup catalogo community (2-col)"
            desc="AddGameDrawer aperto al Step 3. Left: drop zone PDF con SHA-256 match info + benefici riuso. Right: dedup card con CTA.">
            <DrawerDeupDesktop
              stateLabel="Catalogo community"
              cardContent={<State1CatalogDedup/>}
            />
          </DesktopFrame>

          <DesktopFrame
            label="08 · Desktop · Dedup tua libreria (2-col)"
            desc="Same layout. Right: dedup card variante tua libreria. Player-color badge + open link.">
            <DrawerDeupDesktop
              stateLabel="Tua libreria"
              cardContent={<State2OwnLibraryDedup/>}
            />
          </DesktopFrame>

          <DesktopFrame
            label="09 · Desktop · Dedup altro utente — Privacy enforced (2-col)"
            desc="Right: anonimizzazione 🔒 completa. Nessun email/username. Privacy notice a tutta larghezza nella card.">
            <DrawerDeupDesktop
              stateLabel="Altro utente"
              cardContent={<State3OtherUserDedup/>}
            />
          </DesktopFrame>

          <DesktopFrame
            label="10 · Desktop · Confirm reuse + progress side-by-side"
            desc="Left: confirm reuse state. Right: re-upload 4-stage progress. Mostra entrambi per confronto.">
            <div style={{ padding: '24px 28px', background: 'var(--bg)' }}>
              <div style={{
                fontFamily: 'var(--f-display)', fontWeight: 800,
                fontSize: 15, color: 'var(--text)', marginBottom: 20,
              }}>Esiti post-decisione</div>
              <div style={{
                display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 20,
              }}>
                <div>
                  <div style={{
                    fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
                    textTransform: 'uppercase', letterSpacing: '.06em',
                    color: 'var(--text-muted)', marginBottom: 10,
                  }}>Scelta: Riusa indici</div>
                  <State4ConfirmReuse/>
                </div>
                <div>
                  <div style={{
                    fontFamily: 'var(--f-mono)', fontSize: 10, fontWeight: 700,
                    textTransform: 'uppercase', letterSpacing: '.06em',
                    color: 'var(--text-muted)', marginBottom: 10,
                  }}>Scelta: Carica nuova copia</div>
                  <State5ForceReUpload/>
                </div>
              </div>
            </div>
          </DesktopFrame>

          <DesktopFrame
            label="11 · Desktop · AGENT_CREATION_FAILED banner (full-width)"
            desc="Banner errore post-upload --c-event. Appare in fondo al drawer dopo upload ok. Differenziato da --c-kb.">
            <div style={{ padding: '24px 28px', background: 'var(--bg)' }}>
              {/* Success KB note first */}
              <div style={{
                marginBottom: 16,
                padding: '12px 16px', borderRadius: 10,
                background: kbHsl(0.08), border: `1px solid ${kbHsl(0.2)}`,
                display: 'flex', alignItems: 'center', gap: 10,
              }}>
                <span aria-hidden="true" style={{ fontSize: 20 }}>✓</span>
                <div style={{ fontSize: 13, color: 'var(--text-sec)', lineHeight: 1.5 }}>
                  <strong style={{ color: kbHsl() }}>PDF caricato e indicizzato.</strong>{' '}
                  {SOURCE_CATALOG.pdfName} · {SOURCE_CATALOG.chunks} chunk · embedding completato.
                </div>
              </div>
              {/* Error banner */}
              <State6AgentCreationFailed/>
            </div>
          </DesktopFrame>
        </div>

        {/* Footer */}
        <div style={{
          borderTop: '1px solid var(--border-light)',
          paddingTop: 24, marginTop: 16,
          display: 'flex', justifyContent: 'space-between', alignItems: 'center',
          flexWrap: 'wrap', gap: 12,
        }}>
          <a href="00-hub.html" style={{
            color: kbHsl(), fontFamily: 'var(--f-mono)', fontSize: 11,
            fontWeight: 700, textDecoration: 'underline',
          }} onClick={() => { setTimeout(() => { window.location.href = 'sp4-dashboard.html'; }, 0); /* DEMO-NAV */ }}>← Torna a 00-hub.html</a>
          <span style={{
            fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
            fontWeight: 600,
          }}>sp4-add-game-pdf-dedup · issue #912 · MeepleAI DS v1</span>
        </div>

      </div>{/* end stage-wrap */}

      {/* ── KEYFRAMES & LOCAL STYLES ── */}
      <style>{`
        @keyframes mai-pulse-kb {
          0%, 100% {
            box-shadow: 0 0 0 0 ${kbHsl(0.4)}, 0 0 0 0 ${kbHsl(0.15)};
          }
          50% {
            box-shadow: 0 0 0 10px ${kbHsl(0)}, 0 0 0 20px ${kbHsl(0)};
          }
        }
        @keyframes mai-spin {
          to { transform: rotate(360deg); }
        }
        @keyframes mai-sheen {
          0%   { transform: translateX(-100%); }
          100% { transform: translateX(100%); }
        }
        @keyframes mai-shimmer-anim {
          0%   { background-position: -200% 0; }
          100% { background-position:  200% 0; }
        }
        .mai-shimmer {
          background: linear-gradient(90deg, var(--bg-muted) 0%, var(--bg-hover) 50%, var(--bg-muted) 100%) !important;
          background-size: 200% 100% !important;
          animation: mai-shimmer-anim 1.4s linear infinite;
        }
        .stage h1 { font-size: var(--fs-3xl); }
        .stage .lead { line-height: var(--lh-relax); }
        .phone > div::-webkit-scrollbar { display: none; }
        button:hover { filter: brightness(1.05); }
        button:active { transform: scale(0.98); }
        select:focus-visible, input:focus-visible,
        button:focus-visible, a:focus-visible {
          outline: 2px solid hsl(var(--c-kb));
          outline-offset: 2px;
        }
      `}</style>
    </div>
  );
}

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
