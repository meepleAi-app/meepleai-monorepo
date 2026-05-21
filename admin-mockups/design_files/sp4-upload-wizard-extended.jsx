/* MeepleAI SP4 — Upload Wizard Extension
   Route: /kb/upload (extended states)
   File: admin-mockups/design_files/sp4-upload-wizard-extended.{html,jsx}
   Issue: #914 — Upload wizard extension: BGG + dedup + cost preview

   4 screen states (stacked, smoke-test scope):
   - Step 0:    Source picker (4 cards 2×2, "Da URL" disabled)
   - Step 1bis: Pre-upload dedup check (3 sub-states: loading · success · duplicate found)
   - Step 3:    Cost preview dialog (provider dropdown + breakdown + toggle)
   - Step 4:    Async indexing progress (timeline 5 steps · in-progress · failed variant)

   Entity: --c-kb (teal)
   Palette: light + dark via [data-theme]
   Cross-refs: sp4-add-game-bgg-step.jsx · sp4-add-game-pdf-dedup.jsx · sp4-kb-hub.jsx
   No Tailwind. No external deps beyond React 18 + Babel.
*/

const { useState, useEffect, useRef } = React;

// ─── Entity color helper ──────────────────────────────────────────────────────
const kbH = () => {
  const el = document.documentElement;
  const theme = el.getAttribute('data-theme') || 'light';
  return theme === 'dark' ? '174, 60%, 55%' : '174, 60%, 40%';
};
const kb = (alpha) =>
  alpha !== undefined
    ? `hsla(${kbH()}, ${alpha})`
    : `hsl(${kbH()})`;

const eventH = () => {
  const theme = document.documentElement.getAttribute('data-theme') || 'light';
  return theme === 'dark' ? '350, 85%, 70%' : '350, 89%, 60%';
};

// ─── Sample data ──────────────────────────────────────────────────────────────
const PROVIDERS = [
  { id: 'openai', name: 'OpenAI text-embedding-3-small', cost: '$0.18', time: '3-5 min', accuracy: 'Alta', tokens: '18.400' },
  { id: 'cohere', name: 'Cohere embed-english-v3.0',     cost: '$0.12', time: '4-6 min', accuracy: 'Alta', tokens: '18.400' },
  { id: 'local',  name: 'Local (e5-base)',                cost: '$0.00', time: '8-12 min', accuracy: 'Media (-15%)', tokens: '18.400' },
];

const PROGRESS_STEPS = [
  { id: 'queued',     label: 'In coda',              time: '15:42',       status: 'done'   },
  { id: 'extracting', label: 'Estrazione testo',      time: '15:42–15:43', status: 'done'   },
  { id: 'chunking',   label: 'Chunking semantico',    time: '15:43',       status: 'active', eta: '2 min' },
  { id: 'embedding',  label: 'Embedding vettoriale',  time: null,          status: 'pending' },
  { id: 'done',       label: 'Completato',            time: null,          status: 'pending' },
];

const PROGRESS_STEPS_FAILED = [
  { id: 'queued',     label: 'In coda',              time: '15:50',       status: 'done'   },
  { id: 'extracting', label: 'Estrazione testo',      time: '15:50–15:51', status: 'done'   },
  { id: 'chunking',   label: 'Chunking semantico',    time: '15:51–15:52', status: 'done'   },
  { id: 'embedding',  label: 'Embedding vettoriale',  time: '15:52',       status: 'failed' },
  { id: 'done',       label: 'Completato',            time: null,          status: 'pending' },
];

// ─── Theme toggle ─────────────────────────────────────────────────────────────
function ThemeToggle() {
  const [dark, setDark] = useState(false);
  const toggle = () => {
    const next = !dark;
    setDark(next);
    document.documentElement.setAttribute('data-theme', next ? 'dark' : 'light');
  };
  return (
    <button className="theme-toggle" onClick={(e) => { (toggle)(e); setTimeout(() => { window.location.href = 'sp4-game-detail.html'; }, 0); /* DEMO-NAV */ }} aria-label="Toggle dark mode">
      {dark ? '☀️' : '🌙'} {dark ? 'Light' : 'Dark'}
    </button>
  );
}

// ─── Spinner ──────────────────────────────────────────────────────────────────
function Spinner({ size = 18, color }) {
  return (
    <svg
      width={size} height={size} viewBox="0 0 24 24" fill="none"
      style={{ animation: 'spin 0.8s linear infinite', flexShrink: 0, color: color || kb() }}
      aria-hidden="true"
    >
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2.5"
        strokeDasharray="28.27" strokeDashoffset="14.14" strokeLinecap="round"/>
    </svg>
  );
}

// ─── Step 0: Source Picker ────────────────────────────────────────────────────
const SOURCE_CARDS = [
  {
    id: 'file',
    icon: '📄',
    title: 'Carica file dal computer',
    sub: 'Drag-drop o clicca per selezionare',
    hint: 'PDF, max 50 MB',
    disabled: false,
    href: null,
  },
  {
    id: 'bgg',
    icon: '🎲',
    title: 'Da BoardGameGeek',
    sub: 'Cerca nel catalogo BGG',
    hint: 'Importa PDF ufficiali',
    disabled: false,
    href: 'sp4-add-game-bgg-step.html',
  },
  {
    id: 'dedup',
    icon: '♻️',
    title: 'Riusa PDF esistente',
    sub: 'Verifica hash e ricollegati',
    hint: 'Deduplicazione SHA-256',
    disabled: false,
    href: 'sp4-add-game-pdf-dedup.html',
  },
  {
    id: 'url',
    icon: '🔗',
    title: 'Da URL',
    sub: 'Importa da un link esterno',
    hint: null,
    disabled: true,
    badge: 'Coming soon',
    href: null,
  },
];

function SourcePicker({ onSelect }) {
  return (
    <div style={{
      background: 'var(--bg-card)', border: '1px solid var(--border)',
      borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-md)',
      padding: '28px 28px 24px', maxWidth: 520,
    }}>
      {/* Header */}
      <div style={{ marginBottom: 20 }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '0.1em', marginBottom: 6,
        }}>
          Nuovo documento · Step 0 di 4
        </div>
        <h3 style={{ fontSize: 'var(--fs-xl)', margin: 0, lineHeight: 1.2 }}>
          Da dove vuoi importare?
        </h3>
        <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-sm)', marginTop: 6 }}>
          Scegli la sorgente per il tuo documento PDF
        </p>
      </div>

      {/* 2×2 grid */}
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(2, 1fr)',
        gap: 12,
      }}>
        {SOURCE_CARDS.map(card => (
          <SourceCard key={card.id} card={card} onSelect={onSelect} />
        ))}
      </div>
    </div>
  );
}

function SourceCard({ card, onSelect }) {
  const [hovered, setHovered] = useState(false);
  const isInteractive = !card.disabled;

  const borderColor = hovered && isInteractive
    ? `hsl(${kbH()} / 0.5)`
    : 'var(--border)';
  const bg = hovered && isInteractive ? `hsl(${kbH()} / 0.05)` : 'var(--bg-muted)';

  const handleClick = () => {
    if (card.disabled) return;
    if (card.href) {
      window.open(card.href, '_blank');
    } else {
      onSelect && onSelect(card.id);
    }
  };

  return (
    <div
      role={isInteractive ? 'button' : undefined}
      tabIndex={isInteractive ? 0 : undefined}
      onClick={handleClick}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      onKeyDown={e => e.key === 'Enter' && handleClick()}
      style={{
        position: 'relative',
        background: bg,
        border: `1.5px solid ${borderColor}`,
        borderRadius: 'var(--r-lg)',
        padding: '16px 14px 14px',
        cursor: card.disabled ? 'not-allowed' : 'pointer',
        opacity: card.disabled ? 0.5 : 1,
        transition: 'all var(--dur-sm) var(--ease-out)',
        transform: hovered && isInteractive ? 'translateY(-1px)' : 'none',
        boxShadow: hovered && isInteractive ? 'var(--shadow-sm)' : 'none',
        display: 'flex', flexDirection: 'column', gap: 8,
        minHeight: 120,
        userSelect: 'none',
      }}
    >
      {/* Coming soon badge */}
      {card.badge && (
        <div style={{
          position: 'absolute', top: 8, right: 8,
          background: `hsl(${eventH()} / 0.15)`,
          color: `hsl(${eventH()})`,
          border: `1px solid hsl(${eventH()} / 0.3)`,
          borderRadius: 'var(--r-pill)',
          fontSize: 9, fontWeight: 700, fontFamily: 'var(--f-display)',
          padding: '2px 7px', letterSpacing: '0.04em', textTransform: 'uppercase',
        }}>
          {card.badge}
        </div>
      )}

      {/* Icon */}
      <div style={{ fontSize: 28, lineHeight: 1 }}>{card.icon}</div>

      {/* Text */}
      <div>
        <div style={{
          fontFamily: 'var(--f-display)', fontWeight: 700,
          fontSize: 'var(--fs-sm)', color: 'var(--text)', lineHeight: 1.3,
        }}>
          {card.title}
        </div>
        <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--text-sec)', marginTop: 3 }}>
          {card.sub}
        </div>
        {card.hint && (
          <div style={{
            fontSize: 10, color: `hsl(${kbH()})`,
            fontFamily: 'var(--f-mono)', marginTop: 6,
          }}>
            {card.hint}
          </div>
        )}
      </div>

      {/* Arrow (non-disabled) */}
      {isInteractive && (
        <div style={{
          position: 'absolute', bottom: 12, right: 12,
          color: `hsl(${kbH()})`, fontSize: 14, opacity: hovered ? 1 : 0.5,
          transition: 'opacity var(--dur-sm)',
        }}>→</div>
      )}
    </div>
  );
}

// ─── Step 1bis: Dedup Check ───────────────────────────────────────────────────
function DedupPanel({ subState }) {
  return (
    <div style={{
      background: 'var(--bg-card)', border: '1px solid var(--border)',
      borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-md)',
      padding: '24px 28px', maxWidth: 520,
    }}>
      <div style={{ marginBottom: 16 }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '0.1em', marginBottom: 6,
        }}>
          Step 1bis · Verifica hash
        </div>
        <h3 style={{ fontSize: 'var(--fs-lg)', margin: 0 }}>Controllo pre-upload</h3>
      </div>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
        {/* Sub-state A: Loading */}
        <DedupSubState
          active={subState === 'loading'}
          bg={`hsl(${kbH()} / 0.06)`}
          border={`hsl(${kbH()} / 0.2)`}
        >
          <Spinner size={16} />
          <div>
            <div style={{ fontWeight: 700, fontSize: 'var(--fs-sm)' }}>Calcolo hash SHA-256…</div>
            <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', marginTop: 2 }}>
              Lettura file in corso, 1-2 sec
            </div>
          </div>
          <div style={{
            marginLeft: 'auto', fontSize: 'var(--fs-xs)', fontFamily: 'var(--f-mono)',
            color: 'var(--text-muted)',
          }}>
            12.4 MB
          </div>
        </DedupSubState>

        {/* Sub-state B: Success */}
        <DedupSubState
          active={subState === 'success'}
          bg="hsl(142, 70%, 45%, 0.07)"
          border="hsl(142, 70%, 45%, 0.25)"
        >
          <div style={{
            width: 20, height: 20, borderRadius: '50%',
            background: 'hsl(142, 70%, 45%)', color: '#fff',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 11, fontWeight: 800, flexShrink: 0,
          }}>✓</div>
          <div>
            <div style={{ fontWeight: 700, fontSize: 'var(--fs-sm)', color: 'hsl(142, 70%, 40%)' }}>
              Hash calcolato · Nessun duplicato
            </div>
            <div style={{
              fontSize: 10, fontFamily: 'var(--f-mono)', color: 'var(--text-muted)', marginTop: 3,
            }}>
              sha256: 7a3c8f2d…e4b9f1a2
            </div>
          </div>
          <div style={{ marginLeft: 'auto' }}>
            <span style={{
              fontSize: 10, fontFamily: 'var(--f-display)', fontWeight: 700,
              background: 'hsl(142, 70%, 45%, 0.12)', color: 'hsl(142, 70%, 40%)',
              padding: '2px 8px', borderRadius: 'var(--r-pill)',
            }}>
              Procedi →
            </span>
          </div>
        </DedupSubState>

        {/* Sub-state C: Duplicate found */}
        <DedupSubState
          active={subState === 'duplicate'}
          bg={`hsl(${eventH()} / 0.07)`}
          border={`hsl(${eventH()} / 0.3)`}
        >
          <div style={{
            width: 20, height: 20, borderRadius: '50%',
            background: `hsl(${eventH()})`, color: '#fff',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 12, fontWeight: 800, flexShrink: 0,
          }}>!</div>
          <div>
            <div style={{
              fontWeight: 700, fontSize: 'var(--fs-sm)',
              color: `hsl(${eventH()})`,
            }}>
              Trovato duplicato
            </div>
            <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--text-sec)', marginTop: 2 }}>
              Questo PDF esiste già nella KB —{' '}
              <a href="sp4-add-game-pdf-dedup.html"
                style={{ color: `hsl(${kbH()})`, textDecoration: 'underline', fontWeight: 700 }}>
                vai a gestione dedup →
              </a>
            </div>
          </div>
        </DedupSubState>
      </div>
    </div>
  );
}

function DedupSubState({ children, active, bg, border }) {
  return (
    <div style={{
      display: 'flex', alignItems: 'flex-start', gap: 12,
      padding: '12px 14px', borderRadius: 'var(--r-md)',
      background: active ? bg : 'var(--bg-muted)',
      border: `1.5px solid ${active ? border : 'var(--border-light)'}`,
      opacity: active ? 1 : 0.45,
      transition: 'all var(--dur-md) var(--ease-out)',
    }}>
      {children}
    </div>
  );
}

// ─── Step 3: Cost Preview ─────────────────────────────────────────────────────
function CostPreview() {
  const [providerId, setProviderId] = useState('openai');
  const [downgrade, setDowngrade] = useState(false);
  const [open, setOpen] = useState(false);

  const effectiveId = downgrade ? 'local' : providerId;
  const provider = PROVIDERS.find(p => p.id === effectiveId) || PROVIDERS[0];

  return (
    <div style={{
      background: 'var(--bg-card)', border: '1px solid var(--border)',
      borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-md)',
      padding: '28px 28px 24px', maxWidth: 520,
    }}>
      <div style={{ marginBottom: 20 }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '0.1em', marginBottom: 6,
        }}>
          Step 3 · Conferma
        </div>
        <h3 style={{ fontSize: 'var(--fs-xl)', margin: 0, lineHeight: 1.2 }}>
          Conferma indicizzazione
        </h3>
        <p style={{ color: 'var(--text-sec)', fontSize: 'var(--fs-sm)', marginTop: 6 }}>
          Revisiona i costi stimati prima di avviare il processo.
        </p>
      </div>

      {/* Provider selector */}
      <div style={{ marginBottom: 16 }}>
        <label style={{
          display: 'block', fontSize: 'var(--fs-xs)', fontWeight: 700,
          color: 'var(--text-muted)', textTransform: 'uppercase',
          letterSpacing: '0.06em', marginBottom: 6,
        }}>
          Provider embedding
        </label>
        <div style={{ position: 'relative' }}>
          <select
            value={downgrade ? 'local' : providerId}
            disabled={downgrade}
            onChange={e => setProviderId(e.target.value)}
            style={{
              width: '100%', padding: '9px 36px 9px 12px',
              background: downgrade ? 'var(--bg-muted)' : 'var(--bg-card)',
              border: `1.5px solid ${downgrade ? 'var(--border-light)' : `hsl(${kbH()} / 0.35)`}`,
              borderRadius: 'var(--r-md)', color: 'var(--text)',
              fontFamily: 'var(--f-body)', fontSize: 'var(--fs-sm)',
              cursor: downgrade ? 'not-allowed' : 'pointer', appearance: 'none',
              opacity: downgrade ? 0.6 : 1,
            }}
          >
            {PROVIDERS.map(p => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
          <span style={{
            position: 'absolute', right: 12, top: '50%', transform: 'translateY(-50%)',
            pointerEvents: 'none', color: 'var(--text-muted)', fontSize: 12,
          }}>▾</span>
        </div>
      </div>

      {/* Cost breakdown table */}
      <div style={{
        background: 'var(--bg-muted)', borderRadius: 'var(--r-md)',
        border: '1px solid var(--border-light)', overflow: 'hidden', marginBottom: 16,
      }}>
        {[
          { label: 'Provider', value: provider.name, mono: false },
          { label: 'Tokens stimati', value: provider.tokens, mono: true },
          { label: 'Costo stimato', value: provider.cost, mono: true, accent: true },
          { label: 'Tempo stimato', value: provider.time, mono: true },
          { label: 'Accuracy', value: provider.accuracy, mono: false },
        ].map((row, i, arr) => (
          <div key={i} style={{
            display: 'flex', alignItems: 'center', justifyContent: 'space-between',
            padding: '9px 14px',
            borderBottom: i < arr.length - 1 ? '1px solid var(--border-light)' : 'none',
          }}>
            <span style={{ fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', fontWeight: 600 }}>
              {row.label}
            </span>
            <span style={{
              fontFamily: row.mono ? 'var(--f-mono)' : 'var(--f-body)',
              fontSize: row.mono ? 12 : 'var(--fs-xs)',
              fontWeight: row.accent ? 800 : 600,
              color: row.accent ? `hsl(${kbH()})` : 'var(--text)',
            }}>
              {row.value}
            </span>
          </div>
        ))}
      </div>

      {/* Downgrade toggle */}
      <label style={{
        display: 'flex', alignItems: 'flex-start', gap: 10,
        cursor: 'pointer', marginBottom: 20,
        padding: '10px 12px', borderRadius: 'var(--r-md)',
        background: downgrade ? `hsl(${kbH()} / 0.07)` : 'transparent',
        border: `1px solid ${downgrade ? `hsl(${kbH()} / 0.25)` : 'var(--border-light)'}`,
        transition: 'all var(--dur-sm) var(--ease-out)',
      }}>
        <input
          type="checkbox"
          checked={downgrade}
          onChange={e => setDowngrade(e.target.checked)}
          style={{ marginTop: 2, accentColor: `hsl(${kbH()})`, flexShrink: 0 }}
        />
        <div>
          <div style={{ fontSize: 'var(--fs-sm)', fontWeight: 700, color: 'var(--text)' }}>
            Downgrade a local model (gratuito)
          </div>
          <div style={{ fontSize: 'var(--fs-xs)', color: 'var(--text-muted)', marginTop: 2 }}>
            Usa e5-base locale — nessun costo, ma accuratezza -15%
          </div>
        </div>
      </label>

      {/* CTAs */}
      <div style={{ display: 'flex', gap: 10 }}>
        <button
          className="btn primary e-kb"
          style={{
            flex: 1, justifyContent: 'center',
            background: `hsl(${kbH()})`,
            fontSize: 'var(--fs-sm)', fontWeight: 700,
          }}
        >
          ✓ Conferma e indicizza
        </button>
        <button
          className="btn ghost"
          style={{ fontSize: 'var(--fs-sm)' }}
        >
          Annulla
        </button>
      </div>
    </div>
  );
}

// ─── Step 4: Async Progress ───────────────────────────────────────────────────
function StepIcon({ status, size = 22 }) {
  if (status === 'done') {
    return (
      <div style={{
        width: size, height: size, borderRadius: '50%',
        background: 'hsl(142, 70%, 45%)', color: '#fff',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: size * 0.52, fontWeight: 800, flexShrink: 0,
      }}>✓</div>
    );
  }
  if (status === 'active') {
    return (
      <div style={{
        width: size, height: size, borderRadius: '50%',
        border: `2.5px solid hsl(${kbH()})`,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        flexShrink: 0,
      }}>
        <Spinner size={size * 0.6} />
      </div>
    );
  }
  if (status === 'failed') {
    return (
      <div style={{
        width: size, height: size, borderRadius: '50%',
        background: `hsl(${eventH()})`, color: '#fff',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        fontSize: size * 0.55, fontWeight: 800, flexShrink: 0,
      }}>✕</div>
    );
  }
  // pending
  return (
    <div style={{
      width: size, height: size, borderRadius: '50%',
      border: '2px solid var(--border-strong)', flexShrink: 0,
    }} />
  );
}

function ProgressTimeline({ steps, failed }) {
  const [logOpen, setLogOpen] = useState(false);

  return (
    <div>
      {/* Steps */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
        {steps.map((step, i) => {
          const isLast = i === steps.length - 1;
          const isActive = step.status === 'active';
          const isFailed = step.status === 'failed';

          return (
            <div key={step.id} style={{ display: 'flex', gap: 14 }}>
              {/* Left: icon + connector */}
              <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', flexShrink: 0 }}>
                <StepIcon status={step.status} />
                {!isLast && (
                  <div style={{
                    width: 2, flex: 1, minHeight: 24,
                    background: step.status === 'done'
                      ? 'hsl(142, 70%, 45%)'
                      : 'var(--border)',
                    margin: '4px 0',
                  }} />
                )}
              </div>

              {/* Right: content */}
              <div style={{
                paddingBottom: isLast ? 0 : 20,
                paddingTop: 2,
                flex: 1,
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
                  <span style={{
                    fontFamily: 'var(--f-display)', fontWeight: isActive || isFailed ? 700 : 600,
                    fontSize: 'var(--fs-sm)',
                    color: isFailed
                      ? `hsl(${eventH()})`
                      : isActive
                        ? `hsl(${kbH()})`
                        : step.status === 'done'
                          ? 'var(--text)'
                          : 'var(--text-muted)',
                  }}>
                    {step.label}
                  </span>

                  {/* ETA pill */}
                  {isActive && step.eta && (
                    <span style={{
                      fontSize: 10, fontFamily: 'var(--f-mono)',
                      background: `hsl(${kbH()} / 0.12)`,
                      color: `hsl(${kbH()})`,
                      border: `1px solid hsl(${kbH()} / 0.25)`,
                      padding: '1px 7px', borderRadius: 'var(--r-pill)',
                      fontWeight: 700,
                      animation: 'pulse 2s ease-in-out infinite',
                    }}>
                      ETA {step.eta}
                    </span>
                  )}

                  {/* Timestamp */}
                  {step.time && (
                    <span style={{
                      fontSize: 10, fontFamily: 'var(--f-mono)',
                      color: 'var(--text-muted)',
                    }}>
                      {step.time}
                    </span>
                  )}

                  {isFailed && (
                    <span style={{
                      fontSize: 10, fontFamily: 'var(--f-mono)', fontWeight: 700,
                      background: `hsl(${eventH()} / 0.12)`,
                      color: `hsl(${eventH()})`,
                      border: `1px solid hsl(${eventH()} / 0.3)`,
                      padding: '1px 8px', borderRadius: 'var(--r-pill)',
                    }}>
                      EMBEDDING_SERVICE_UNAVAILABLE
                    </span>
                  )}
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Actions */}
      {!failed && (
        <div style={{ marginTop: 20, display: 'flex', gap: 8 }}>
          <button className="btn ghost" style={{ fontSize: 'var(--fs-xs)', color: `hsl(${eventH()})`, borderColor: `hsl(${eventH()} / 0.35)` }}>
            ✕ Annulla indicizzazione
          </button>
        </div>
      )}

      {/* Failed actions */}
      {failed && (
        <div style={{ marginTop: 20, display: 'flex', flexDirection: 'column', gap: 12 }}>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn primary e-kb" style={{
              background: `hsl(${kbH()})`, fontSize: 'var(--fs-xs)',
            }}>
              ↺ Riprova
            </button>
            <button className="btn ghost" style={{ fontSize: 'var(--fs-xs)' }}>
              Annulla
            </button>
          </div>

          {/* Log accordion */}
          <div style={{
            border: `1px solid hsl(${eventH()} / 0.25)`,
            borderRadius: 'var(--r-md)', overflow: 'hidden',
          }}>
            <button
              onClick={() => setLogOpen(v => !v)}
              style={{
                width: '100%', display: 'flex', alignItems: 'center', gap: 8,
                padding: '8px 12px', background: `hsl(${eventH()} / 0.06)`,
                border: 'none', cursor: 'pointer', color: 'var(--text-sec)',
                fontSize: 'var(--fs-xs)', fontFamily: 'var(--f-display)', fontWeight: 700,
                textAlign: 'left',
              }}
            >
              <span>{logOpen ? '▾' : '▸'}</span>
              <span>Log di errore</span>
            </button>
            {logOpen && (
              <pre style={{
                margin: 0, padding: '10px 12px',
                fontFamily: 'var(--f-mono)', fontSize: 10,
                color: `hsl(${eventH()})`,
                background: 'var(--bg-sunken)',
                lineHeight: 1.6, whiteSpace: 'pre-wrap', wordBreak: 'break-all',
              }}>
{`[2026-05-09T15:52:03Z] INFO  Chunk batch 3/7 sent to embedding service
[2026-05-09T15:52:04Z] ERROR Connection refused: embedding-service:8000
[2026-05-09T15:52:04Z] ERROR EMBEDDING_SERVICE_UNAVAILABLE
[2026-05-09T15:52:04Z] INFO  Job marked as FAILED (jobId: 8f3c-...)
[2026-05-09T15:52:04Z] INFO  Partial chunks (21/49) rolled back`}
              </pre>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function AsyncProgress({ variant }) {
  const failed = variant === 'failed';
  const steps = failed ? PROGRESS_STEPS_FAILED : PROGRESS_STEPS;
  const fileName = 'Azul_Regolamento_ITA_v1.2.pdf';

  return (
    <div style={{
      background: 'var(--bg-card)', border: `1.5px solid ${failed ? `hsl(${eventH()} / 0.3)` : 'var(--border)'}`,
      borderRadius: 'var(--r-xl)', boxShadow: 'var(--shadow-md)',
      padding: '24px 28px', maxWidth: 520,
    }}>
      {/* Header */}
      <div style={{ marginBottom: 20 }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '0.1em', marginBottom: 6,
        }}>
          Step 4 · {failed ? 'Errore · Riprova' : 'Indicizzazione in corso'}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <h3 style={{ fontSize: 'var(--fs-lg)', margin: 0, flex: 1, lineHeight: 1.2 }}>
            {failed ? 'Indicizzazione fallita' : 'Indicizzazione in corso…'}
          </h3>
          {!failed && <Spinner size={18} />}
        </div>
        <div style={{
          marginTop: 6, display: 'flex', alignItems: 'center', gap: 6,
          fontSize: 'var(--fs-xs)', color: 'var(--text-muted)',
        }}>
          <span>📄</span>
          <span style={{ fontFamily: 'var(--f-mono)' }}>{fileName}</span>
          <span style={{
            fontSize: 10, fontFamily: 'var(--f-display)', fontWeight: 700,
            background: `hsl(${kbH()} / 0.1)`,
            color: `hsl(${kbH()})`,
            padding: '1px 7px', borderRadius: 'var(--r-pill)',
          }}>
            OpenAI · e3-small
          </span>
        </div>
      </div>

      <ProgressTimeline steps={steps} failed={failed} />
    </div>
  );
}

// ─── Global CSS injected via style tag ───────────────────────────────────────
function GlobalStyles() {
  useEffect(() => {
    const id = 'uw-ext-styles';
    if (document.getElementById(id)) return;
    const s = document.createElement('style');
    s.id = id;
    s.textContent = `
      @keyframes spin { to { transform: rotate(360deg); } }
      @keyframes pulse {
        0%, 100% { opacity: 1; }
        50% { opacity: 0.55; }
      }
      @keyframes activeGlow {
        0%, 100% { box-shadow: 0 0 0 0 hsl(174 60% 40% / 0.0); }
        50%       { box-shadow: 0 0 0 4px hsl(174 60% 40% / 0.18); }
      }
      .uw-active-step { animation: activeGlow 2s ease-in-out infinite; }
      /* Responsive source grid */
      @media (max-width: 540px) {
        .source-grid { grid-template-columns: 1fr !important; }
      }
    `;
    document.head.appendChild(s);
  }, []);
  return null;
}

// ─── Section Label (reuse pattern from components.css .section-label) ────────
function SectionLabel({ step, label, sub }) {
  return (
    <div style={{ marginBottom: 'var(--s-5)' }}>
      <div style={{
        display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6,
      }}>
        <div style={{
          fontFamily: 'var(--f-mono)', fontSize: 'var(--fs-xs)', color: 'var(--text-muted)',
          textTransform: 'uppercase', letterSpacing: '0.08em', flex: 1,
          paddingBottom: 8, borderBottom: '1px solid var(--border)',
        }}>
          {step} — {label}
        </div>
      </div>
      {sub && (
        <p style={{ margin: 0, fontSize: 'var(--fs-xs)', color: 'var(--text-muted)' }}>{sub}</p>
      )}
    </div>
  );
}

// ─── Header strip ─────────────────────────────────────────────────────────────
function HeaderStrip() {
  return (
    <div style={{
      background: `hsl(${kbH()} / 0.08)`,
      borderBottom: `1px solid hsl(${kbH()} / 0.2)`,
      padding: '10px 28px',
      display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap',
    }}>
      <div style={{
        fontFamily: 'var(--f-mono)', fontSize: 10,
        color: `hsl(${kbH()})`, fontWeight: 700,
        textTransform: 'uppercase', letterSpacing: '0.08em',
      }}>
        Mockup · sp4-upload-wizard-extended · #914
      </div>
      <div style={{
        height: 14, width: 1, background: `hsl(${kbH()} / 0.3)`,
      }} />
      <div style={{ fontSize: 10, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
        Issue: #914 · Upload wizard extension: BGG + dedup + cost preview
      </div>
    </div>
  );
}

// ─── Footer ───────────────────────────────────────────────────────────────────
function Footer() {
  const links = [
    { label: '← Hub',           href: '00-hub.html' },
    { label: 'BGG Step',        href: 'sp4-add-game-bgg-step.html' },
    { label: 'PDF Dedup',       href: 'sp4-add-game-pdf-dedup.html' },
    { label: 'KB Hub',          href: 'sp4-kb-hub.html' },
    { label: 'KB Detail',       href: 'sp4-kb-detail.html' },
  ];
  return (
    <footer style={{
      borderTop: '1px solid var(--border)',
      padding: '24px 28px',
      marginTop: 'var(--s-10)',
      display: 'flex', alignItems: 'center', gap: 20, flexWrap: 'wrap',
    }}>
      <div style={{
        fontFamily: 'var(--f-mono)', fontSize: 10, color: 'var(--text-muted)',
      }}>
        sp4-upload-wizard-extended · issue #914 · 2026-05-09
      </div>
      <div style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
        {links.map(l => (
          <a key={l.href} href={l.href} style={{
            fontSize: 11, color: `hsl(${kbH()})`,
            fontFamily: 'var(--f-display)', fontWeight: 700,
            textDecoration: 'none',
            padding: '3px 8px', borderRadius: 'var(--r-sm)',
            background: `hsl(${kbH()} / 0.08)`,
            border: `1px solid hsl(${kbH()} / 0.2)`,
            transition: 'all var(--dur-sm)',
          }}>
            {l.label}
          </a>
        ))}
      </div>
    </footer>
  );
}

// ─── Dedup sub-state selector ─────────────────────────────────────────────────
function DedupStateSelector({ value, onChange }) {
  const opts = [
    { id: 'loading',   label: '⏳ Caricamento hash' },
    { id: 'success',   label: '✓ Hash calcolato' },
    { id: 'duplicate', label: '! Duplicato trovato' },
  ];
  return (
    <div style={{ display: 'flex', gap: 8, marginBottom: 16, flexWrap: 'wrap' }}>
      {opts.map(o => (
        <button
          key={o.id}
          onClick={() => onChange(o.id)}
          style={{
            fontSize: 11, fontFamily: 'var(--f-display)', fontWeight: 700,
            padding: '4px 12px', borderRadius: 'var(--r-pill)',
            background: value === o.id ? `hsl(${kbH()})` : 'var(--bg-muted)',
            color: value === o.id ? '#fff' : 'var(--text-sec)',
            border: `1px solid ${value === o.id ? `hsl(${kbH()})` : 'var(--border)'}`,
            cursor: 'pointer', transition: 'all var(--dur-sm) var(--ease-out)',
          }}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

// ─── Root ─────────────────────────────────────────────────────────────────────
function UploadWizardExtended() {
  const [dedupState, setDedupState] = useState('loading');

  return (
    <>
      <GlobalStyles />
      <ThemeToggle />
      <HeaderStrip />

      <div className="stage">
        <div className="stage-wrap">
          {/* Page title */}
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 8,
            padding: '4px 12px', borderRadius: 'var(--r-pill)',
            background: `hsl(${kbH()} / 0.12)`, color: `hsl(${kbH()})`,
            fontSize: 11, fontFamily: 'var(--f-display)', fontWeight: 700,
            marginBottom: 16, border: `1px solid hsl(${kbH()} / 0.25)`,
          }}>
            📚 KB — Upload Wizard Extension
          </div>
          <h1 style={{ marginBottom: 8 }}>Upload Wizard Extended</h1>
          <p className="lead">
            4 stati specifici per l'integrazione BGG + dedup + cost preview (scope smoke-test issue #914).
            Parte del wizard completo #490 — questi stati sono il sottoinsieme incrementale.
          </p>

          {/* ── STATE 0: Source Picker ── */}
          <div className="section-label">
            SCREEN 1 / 4 — Step 0: Source Picker · "Da dove vuoi importare?"
          </div>
          <div style={{ marginBottom: 'var(--s-9)' }}>
            <SourcePicker onSelect={id => alert(`Selezione: ${id}`)} />
            <div style={{
              marginTop: 14, fontSize: 'var(--fs-xs)', color: 'var(--text-muted)',
              fontFamily: 'var(--f-mono)',
            }}>
              Nota: "Da URL" → disabled (opacity-50 + badge "Coming soon"). BGG + Dedup → link cross-ref.
            </div>
          </div>

          {/* ── STATE 1bis: Dedup Check ── */}
          <div className="section-label">
            SCREEN 2 / 4 — Step 1bis: Pre-upload dedup check · 3 sub-stati
          </div>
          <div style={{ marginBottom: 'var(--s-9)' }}>
            <DedupStateSelector value={dedupState} onChange={setDedupState} />
            <DedupPanel subState={dedupState} />
            <div style={{
              marginTop: 14, fontSize: 'var(--fs-xs)', color: 'var(--text-muted)',
              fontFamily: 'var(--f-mono)',
            }}>
              I 3 sub-stati sono impilati; quello attivo (selezionato sopra) ha opacity piena.
            </div>
          </div>

          {/* ── STATE 3: Cost Preview ── */}
          <div className="section-label">
            SCREEN 3 / 4 — Step 3: Cost preview · Conferma indicizzazione
          </div>
          <div style={{ marginBottom: 'var(--s-9)' }}>
            <CostPreview />
            <div style={{
              marginTop: 14, fontSize: 'var(--fs-xs)', color: 'var(--text-muted)',
              fontFamily: 'var(--f-mono)',
            }}>
              Provider dropdown → 3 opzioni. Toggle downgrade forza local e disabilita select. Valori numerici in JetBrains Mono.
            </div>
          </div>

          {/* ── STATE 4: Async Progress (in-progress + failed) ── */}
          <div className="section-label">
            SCREEN 4 / 4 — Step 4: Indicizzazione asincrona · variante in-progress + errore
          </div>
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(460px, 1fr))',
            gap: 28,
            marginBottom: 'var(--s-9)',
          }}>
            {/* In-progress variant */}
            <div>
              <div style={{
                fontFamily: 'var(--f-mono)', fontSize: 10,
                color: `hsl(${kbH()})`, fontWeight: 700,
                textTransform: 'uppercase', letterSpacing: '0.07em',
                marginBottom: 10,
              }}>
                ● Variante A — In progress (chunking attivo)
              </div>
              <AsyncProgress variant="progress" />
            </div>

            {/* Failed variant */}
            <div>
              <div style={{
                fontFamily: 'var(--f-mono)', fontSize: 10,
                color: `hsl(${eventH()})`, fontWeight: 700,
                textTransform: 'uppercase', letterSpacing: '0.07em',
                marginBottom: 10,
              }}>
                ✕ Variante B — Failed (embedding service unavailable)
              </div>
              <AsyncProgress variant="failed" />
            </div>
          </div>

        </div>
      </div>

      <Footer />
    </>
  );
}

// ─── Mount ────────────────────────────────────────────────────────────────────
const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(<UploadWizardExtended />);
