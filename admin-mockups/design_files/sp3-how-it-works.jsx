/* MeepleAI SP3 missing — #2 · How it works
   Route: /how-it-works
   Riusa: HeroGradient pattern from public.jsx (pub-hero/pub-wrap inlined as styles)
          + EntityChip (e-chip pattern)
   Nuovo v2: HowItWorksStep
   Pagina educativa statica — nessuna variante dinamica per stato.
*/

const { useState, useEffect, useRef, useMemo } = React;

// ── ENTITY HELPERS ──────────────────────────────────────────────────────────
const ENTITIES = {
  game:    { c: 'var(--c-game)',    icon: '🎲', label: 'Game' },
  kb:      { c: 'var(--c-kb)',      icon: '📄', label: 'KB' },
  agent:   { c: 'var(--c-agent)',   icon: '🤖', label: 'Agent' },
  session: { c: 'var(--c-session)', icon: '🎯', label: 'Session' },
  toolkit: { c: 'var(--c-toolkit)', icon: '🧰', label: 'Toolkit' },
  player:  { c: 'var(--c-player)',  icon: '👤', label: 'Player' },
  chat:    { c: 'var(--c-chat)',    icon: '💬', label: 'Chat' },
  event:   { c: 'var(--c-event)',   icon: '🎉', label: 'Event' },
};
const eHsl = (k, a = 1) => `hsl(var(--${ENTITIES[k]?.c.replace('var(--', '').replace(')', '') || 'c-game'}) / ${a})`;

// EntityChip — riusa pattern e-chip da public.jsx
const EntityChip = ({ entity, label, size = 'sm', icon = true }) => {
  const e = ENTITIES[entity] || ENTITIES.game;
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 5,
      padding: size === 'lg' ? '5px 10px' : '3px 8px',
      borderRadius: 999,
      background: `hsl(${e.c.replace('var(--', '').replace(')', '')} / 0.12)`.replace('var(--', '').replace(')', ''),
      backgroundColor: `hsl(var(${e.c.startsWith('var') ? e.c.slice(4, -1) : ''}) / 0.12)`,
      color: `hsl(${e.c.startsWith('var') ? `var(${e.c.slice(4, -1)})` : e.c})`,
      fontSize: size === 'lg' ? 12 : 11,
      fontFamily: 'var(--f-display)',
      fontWeight: 600,
      letterSpacing: '0.01em',
      whiteSpace: 'nowrap',
      border: `1px solid hsl(var(${e.c.slice(4, -1)}) / 0.22)`,
    }}>
      {icon && <span style={{ fontSize: size === 'lg' ? 12 : 11 }}>{e.icon}</span>}
      {label || e.label}
    </span>
  );
};

// ── HEROGRADIENT (riuso 1:1 da public.jsx) ─────────────────────────────────
// Pattern: pub-hero section + pub-hero-bg + pub-wrap + hero-kicker + h1 con .mark + hero-lead + hero-ctas
// Inline-styles equivalenti delle classi components.css di public.jsx
const HeroGradient = ({ kicker, title, mark, subtitle, emojiStack, isMobile = false }) => {
  return (
    <section style={{
      position: 'relative',
      padding: isMobile ? '48px 0 40px' : '88px 0 72px',
      overflow: 'hidden',
      background: 'var(--bg)',
      borderBottom: '1px solid var(--border)',
    }}>
      {/* pub-hero-bg: gradient radiale 3-color come public.jsx */}
      <div aria-hidden="true" style={{
        position: 'absolute', inset: 0,
        backgroundImage: `
          radial-gradient(800px circle at 18% 30%, hsl(var(--c-game) / 0.18), transparent 55%),
          radial-gradient(700px circle at 82% 25%, hsl(var(--c-event) / 0.16), transparent 55%),
          radial-gradient(900px circle at 50% 110%, hsl(var(--c-toolkit) / 0.14), transparent 60%)
        `,
        pointerEvents: 'none',
      }} />
      <div style={{
        position: 'relative', maxWidth: 1100, margin: '0 auto',
        padding: '0 24px', textAlign: 'center',
      }}>
        {emojiStack && (
          <div aria-hidden="true" style={{
            fontSize: isMobile ? 40 : 56, marginBottom: isMobile ? 14 : 18, letterSpacing: '-0.04em', lineHeight: 1,
            filter: 'drop-shadow(0 6px 16px hsl(var(--c-game) / 0.25))',
          }}>{emojiStack}</div>
        )}
        <p style={{
          margin: '0 0 14px', fontFamily: 'var(--f-mono)',
          fontSize: isMobile ? 10.5 : 12, fontWeight: 600,
          color: 'var(--text-muted)', letterSpacing: '0.12em', textTransform: 'uppercase',
        }}>{kicker}</p>
        <h1 style={{
          fontFamily: 'var(--f-display)',
          fontSize: isMobile ? 30 : 'clamp(34px, 5vw, 56px)',
          fontWeight: 700,
          lineHeight: 1.05,
          letterSpacing: '-0.02em',
          margin: '0 0 18px',
          color: 'var(--text)',
          textWrap: 'balance',
        }}>
          {title}
          {mark && (
            <>
              {' '}
              <span style={{
                background: `linear-gradient(120deg, hsl(var(--c-game)), hsl(var(--c-event)))`,
                WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent',
                backgroundClip: 'text',
              }}>{mark}</span>
            </>
          )}
        </h1>
        <p style={{
          margin: '0 auto', maxWidth: 640,
          fontSize: isMobile ? 14.5 : 17, lineHeight: 1.55,
          color: 'var(--text-sec)',
        }}>{subtitle}</p>
      </div>
    </section>
  );
};

// ── HOWITWORKSSTEP — nuovo v2 ──────────────────────────────────────────────
const HowItWorksStep = ({ stepNum, totalSteps, entity, title, bullets, illustration, reverse = false, isMobile = false }) => {
  const e = ENTITIES[entity];

  return (
    <article
      aria-label={`Passo ${stepNum} di ${totalSteps}`}
      style={{
        display: 'grid',
        gridTemplateColumns: isMobile ? '1fr' : '1fr 1fr',
        gap: isMobile ? 24 : 56,
        alignItems: 'center',
        padding: isMobile ? '36px 20px' : '56px 40px',
        background: 'var(--bg-card)',
        border: `1px solid var(--border)`,
        borderLeft: `4px solid hsl(var(--${e.c.replace('var(--', '').replace(')', '')}))`,
        borderRadius: 'var(--r-xl, 20px)',
        boxShadow: 'var(--shadow-sm)',
        position: 'relative',
        overflow: 'hidden',
      }}
    >
      {/* TEXT SIDE */}
      <div style={{ order: reverse && !isMobile ? 2 : 1 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 14, marginBottom: 14 }}>
          <span
            aria-label={`Passo ${stepNum} di ${totalSteps}`}
            style={{
              fontFamily: 'var(--f-display)',
              fontWeight: 700,
              fontSize: isMobile ? 64 : 96,
              lineHeight: 0.85,
              letterSpacing: '-0.04em',
              color: `hsl(var(--${e.c.replace('var(--', '').replace(')', '')}))`,
              fontVariantNumeric: 'tabular-nums',
              flexShrink: 0,
            }}
          >
            0{stepNum}
          </span>
          <EntityChip entity={entity} size="lg" />
        </div>
        <h2 style={{
          fontFamily: 'var(--f-display)',
          fontWeight: 700,
          fontSize: isMobile ? 24 : 32,
          lineHeight: 1.15,
          letterSpacing: '-0.015em',
          margin: '0 0 18px',
          color: 'var(--text)',
          textWrap: 'balance',
        }}>{title}</h2>
        <ul style={{
          listStyle: 'none', padding: 0, margin: 0,
          display: 'flex', flexDirection: 'column', gap: 12,
          fontFamily: 'var(--f-body)',
        }}>
          {bullets.map((b, i) => (
            <li key={i} style={{
              display: 'flex', gap: 12, alignItems: 'flex-start',
              fontSize: 15.5, lineHeight: 1.55,
              color: 'var(--text-sec)',
            }}>
              <span style={{
                flexShrink: 0,
                width: 22, height: 22, borderRadius: '50%',
                display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
                background: `hsl(var(--${e.c.replace('var(--', '').replace(')', '')}) / 0.14)`,
                color: `hsl(var(--${e.c.replace('var(--', '').replace(')', '')}))`,
                fontSize: 12, fontWeight: 700,
                marginTop: 1,
                fontFamily: 'var(--f-display)',
              }}>✓</span>
              <span>{b}</span>
            </li>
          ))}
        </ul>
      </div>

      {/* ILLUSTRATION SIDE */}
      <div style={{
        order: reverse && !isMobile ? 1 : 2,
        position: 'relative',
        minHeight: isMobile ? 220 : 300,
        borderRadius: 'var(--r-lg, 16px)',
        overflow: 'hidden',
        background: `linear-gradient(135deg,
          hsl(var(--${e.c.replace('var(--', '').replace(')', '')}) / 0.15),
          hsl(var(--${e.c.replace('var(--', '').replace(')', '')}) / 0.04))`,
        border: `1px dashed hsl(var(--${e.c.replace('var(--', '').replace(')', '')}) / 0.3)`,
        padding: 24,
        display: 'flex', alignItems: 'center', justifyContent: 'center',
      }}>
        {illustration}
      </div>
    </article>
  );
};

// ── ILLUSTRAZIONI per i 4 step ─────────────────────────────────────────────
// Step 1: Library — search + add Game card
const IllusStep1 = () => (
  <div style={{ width: '100%', maxWidth: 360, display: 'flex', flexDirection: 'column', gap: 12 }}>
    {/* search bar mockup */}
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '10px 14px',
      background: 'var(--bg-card)',
      border: '1px solid var(--border-strong)',
      borderRadius: 999,
      fontSize: 13, color: 'var(--text-muted)',
      fontFamily: 'var(--f-body)',
    }}>
      <span aria-hidden="true">🔍</span>
      <span>cerca su BGG…</span>
      <span style={{ marginLeft: 'auto', fontFamily: 'var(--f-mono)', fontSize: 10,
        padding: '2px 6px', border: '1px solid var(--border)', borderRadius: 4,
        color: 'var(--text-muted)', background: 'var(--bg-muted)' }}>⌘K</span>
    </div>
    {/* 2 game cards */}
    {[
      { e: '🐦', t: 'Wingspan', m: 'Stonemaier · 2019', state: 'owned', cv: '--c-toolkit' },
      { e: '🔷', t: 'Azul', m: 'Plan B · 2017', state: 'wishlist', cv: '--c-event' },
    ].map(g => (
      <div key={g.t} style={{
        display: 'flex', alignItems: 'center', gap: 12,
        padding: '10px 12px',
        background: 'var(--bg-card)',
        border: '1px solid var(--border)',
        borderRadius: 12,
      }}>
        <div style={{
          width: 40, height: 40, borderRadius: 8,
          background: `linear-gradient(135deg, hsl(var(--c-game) / 0.3), hsl(var(--c-game) / 0.1))`,
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 20,
        }}>{g.e}</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 13.5, color: 'var(--text)' }}>{g.t}</div>
          <div style={{ fontSize: 11, color: 'var(--text-muted)', fontFamily: 'var(--f-body)' }}>{g.m}</div>
        </div>
        <span style={{
          fontSize: 10, fontFamily: 'var(--f-display)', fontWeight: 700,
          padding: '3px 8px', borderRadius: 999,
          background: `hsl(var(${g.cv}) / 0.14)`,
          color: `hsl(var(${g.cv}))`,
          textTransform: 'uppercase', letterSpacing: '0.04em',
        }}>{g.state}</span>
      </div>
    ))}
    {/* +Add button */}
    <button style={{
      padding: '8px 12px',
      background: 'transparent',
      border: '1px dashed var(--border-strong)',
      borderRadius: 12,
      color: 'var(--text-muted)',
      fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 12.5,
      cursor: 'pointer',
    }}>+ Aggiungi gioco</button>
  </div>
);

// Step 2: PDF → KB → Agent flow
const IllusStep2 = () => (
  <div style={{ width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8, flexWrap: 'wrap' }}>
    {[
      { icon: '📕', label: 'PDF', sub: 'wingspan-rules.pdf', cv: '--c-game' },
      { icon: '⚙️', label: 'Chunking', sub: 'OCR + 768d', cv: '--c-tool', dashed: true },
      { icon: '📄', label: 'KB', sub: '24 pag · 89 chunks', cv: '--c-kb' },
      { icon: '🤖', label: 'Agent', sub: 'Wingspan Rules', cv: '--c-agent' },
    ].map((n, i, arr) => (
      <React.Fragment key={n.label}>
        <div style={{
          display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 6,
          padding: '12px 10px',
          minWidth: 78,
          background: n.dashed ? 'transparent' : 'var(--bg-card)',
          border: n.dashed
            ? `1px dashed hsl(var(${n.cv}) / 0.4)`
            : `1px solid hsl(var(${n.cv}) / 0.3)`,
          borderRadius: 12,
          opacity: n.dashed ? 0.85 : 1,
        }}>
          <div style={{
            width: 36, height: 36, borderRadius: 8,
            background: `hsl(var(${n.cv}) / 0.15)`,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            fontSize: 18,
          }}>{n.icon}</div>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 11,
            color: `hsl(var(${n.cv}))`,
          }}>{n.label}</div>
          <div style={{ fontSize: 9, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)', textAlign: 'center' }}>{n.sub}</div>
        </div>
        {i < arr.length - 1 && (
          <span aria-hidden="true" style={{
            color: 'var(--text-muted)', fontSize: 14, fontFamily: 'var(--f-mono)',
          }}>→</span>
        )}
      </React.Fragment>
    ))}
  </div>
);

// Step 3: Session live + chat + timer
const IllusStep3 = () => (
  <div style={{ width: '100%', maxWidth: 340, display: 'flex', flexDirection: 'column', gap: 10 }}>
    {/* Session header */}
    <div style={{
      display: 'flex', alignItems: 'center', gap: 10,
      padding: '10px 12px',
      background: `hsl(var(--c-session) / 0.1)`,
      border: '1px solid hsl(var(--c-session) / 0.3)',
      borderRadius: 12,
    }}>
      <span style={{
        width: 8, height: 8, borderRadius: '50%',
        background: 'hsl(var(--c-session))',
        boxShadow: '0 0 0 4px hsl(var(--c-session) / 0.2)',
        flexShrink: 0,
      }} />
      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 12.5, color: 'var(--text)' }}>
          Serata Azul · Turno 3/5
        </div>
        <div style={{ fontSize: 10, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
          AZL-7X2K · 4 giocatori
        </div>
      </div>
      <span style={{
        fontFamily: 'var(--f-mono)', fontSize: 14, fontWeight: 700,
        color: 'hsl(var(--c-session))',
        fontVariantNumeric: 'tabular-nums',
      }}>⏱ 45:21</span>
    </div>
    {/* Chat bubbles */}
    <div style={{
      padding: '10px 12px',
      background: 'var(--bg-muted)',
      borderRadius: 12, fontSize: 12,
      display: 'flex', flexDirection: 'column', gap: 8,
    }}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
        <span style={{ fontSize: 14 }}>👤</span>
        <div style={{
          padding: '6px 10px',
          background: 'var(--bg-card)',
          borderRadius: '12px 12px 12px 2px',
          fontFamily: 'var(--f-body)', fontSize: 12,
          color: 'var(--text)',
          maxWidth: '80%',
        }}>Marco: posso prendere 2 tessere dal centro?</div>
      </div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start', justifyContent: 'flex-end' }}>
        <div style={{
          padding: '6px 10px',
          background: 'hsl(var(--c-agent) / 0.14)',
          border: '1px solid hsl(var(--c-agent) / 0.3)',
          borderRadius: '12px 12px 2px 12px',
          fontFamily: 'var(--f-body)', fontSize: 12,
          color: 'var(--text)',
          maxWidth: '80%',
        }}>
          Sì, prendi tutte le tessere di un colore (pag. 4).{' '}
          <span style={{ color: 'hsl(var(--c-kb))', fontFamily: 'var(--f-mono)', fontSize: 10 }}>📄 azul-regole-ita.pdf</span>
        </div>
        <span style={{ fontSize: 14 }}>🤖</span>
      </div>
    </div>
    {/* Live event log */}
    <div style={{
      fontFamily: 'var(--f-mono)', fontSize: 10,
      color: 'var(--text-muted)',
      padding: '6px 10px',
      background: 'var(--bg-sunken)',
      borderRadius: 8,
      borderLeft: '2px solid hsl(var(--c-session))',
    }}>
      [21:42] Davide → +5 punti riga · [21:43] Marco turno
    </div>
  </div>
);

// Step 4: Toolkit bundle
const IllusStep4 = () => (
  <div style={{ width: '100%', maxWidth: 340 }}>
    <div style={{
      padding: '16px 18px',
      background: 'var(--bg-card)',
      border: '1px solid hsl(var(--c-toolkit) / 0.4)',
      borderRadius: 16,
      boxShadow: 'var(--shadow-sm)',
      position: 'relative',
    }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 10 }}>
        <div style={{
          width: 40, height: 40, borderRadius: 10,
          background: `linear-gradient(135deg, hsl(var(--c-toolkit) / 0.3), hsl(var(--c-toolkit) / 0.08))`,
          display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 20,
        }}>🧰</div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 14, color: 'var(--text)' }}>
            Azul Helper Bundle
          </div>
          <div style={{ fontSize: 10.5, color: 'var(--text-muted)', fontFamily: 'var(--f-mono)' }}>
            v2.1.0 · Davide · 234 install
          </div>
        </div>
      </div>
      <div style={{
        display: 'flex', flexWrap: 'wrap', gap: 6,
        padding: '10px 0',
        borderTop: '1px solid var(--border-light)',
        borderBottom: '1px solid var(--border-light)',
        marginBottom: 10,
      }}>
        <EntityChip entity="agent" label="1 Agent" />
        <EntityChip entity="kb" label="3 KB" />
        <EntityChip entity="toolkit" label="2 Tools" />
      </div>
      <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 11.5, color: 'var(--text-sec)' }}>
        <span style={{ color: 'hsl(var(--c-event))', fontSize: 13 }}>★★★★★</span>
        <span style={{ fontFamily: 'var(--f-mono)' }}>4.9 · 47 review</span>
        <span style={{
          marginLeft: 'auto',
          padding: '3px 8px', borderRadius: 999,
          background: 'hsl(var(--c-toolkit) / 0.14)',
          color: 'hsl(var(--c-toolkit))',
          fontSize: 10, fontFamily: 'var(--f-display)', fontWeight: 700,
          textTransform: 'uppercase', letterSpacing: '0.04em',
        }}>v2.1.0</span>
      </div>
    </div>
  </div>
);

// ── PAGE BODY ──────────────────────────────────────────────────────────────
const HowItWorksBody = ({ isMobile = false }) => {
  const steps = [
    {
      entity: 'game',
      title: 'Costruisci la tua library',
      bullets: [
        'Cerca giochi direttamente da BoardGameGeek o aggiungili manualmente',
        'Importa il regolamento PDF — l\'OCR parte automaticamente',
        'Organizza per stato: posseduti, wishlist, già giocati',
      ],
      illustration: <IllusStep1 />,
    },
    {
      entity: 'kb',
      title: 'Gli agenti leggono le regole',
      bullets: [
        'Carica il regolamento — chunking + embedding e5-base 768d',
        'L\'agent custom per gioco indicizza ogni regola con citazione pagina',
        'Niente allucinazioni: ogni risposta linka al manuale ufficiale',
      ],
      illustration: <IllusStep2 />,
    },
    {
      entity: 'session',
      title: 'Gioca con l\'aiuto dell\'AI',
      bullets: [
        'Chiedi una regola in chat e ottieni la risposta in 1.2s',
        'Scoring automatico, timer turno, log eventi cronologico',
        'Tutto in tempo reale durante la partita — niente pause',
      ],
      illustration: <IllusStep3 />,
    },
    {
      entity: 'toolkit',
      title: 'Condividi toolkit con la community',
      bullets: [
        'Bundle Agent + KB + Tools in un toolkit installabile',
        'Pubblica con versioning semver, ricevi rating e feedback',
        'I migliori contributor (come Davide) entrano nei consigliati',
      ],
      illustration: <IllusStep4 />,
    },
  ];

  return (
    <>
      <HeroGradient
        kicker="Educazione · Onboarding · 4 step"
        title="Come funziona"
        mark="MeepleAI"
        subtitle="Dal regolamento PDF alla partita assistita in 4 passi. Niente vendita, solo come funziona davvero."
        emojiStack="🎲 🤖 🧰"
        isMobile={isMobile}
      />

      {/* STEP CARDS */}
      <section style={{
        padding: isMobile ? '32px 14px 16px' : '64px 24px 32px',
        maxWidth: 1180, margin: '0 auto',
        display: 'flex', flexDirection: 'column', gap: isMobile ? 18 : 28,
      }}>
        {steps.map((s, i) => (
          <HowItWorksStep
            key={i}
            stepNum={i + 1}
            totalSteps={steps.length}
            entity={s.entity}
            title={s.title}
            bullets={s.bullets}
            illustration={s.illustration}
            reverse={i % 2 === 1}
            isMobile={isMobile}
          />
        ))}
      </section>

      {/* FOOTER CTA */}
      <section style={{
        padding: isMobile ? '32px 14px 56px' : '64px 24px 96px',
        textAlign: 'center',
      }}>
        <div style={{
          maxWidth: 640, margin: '0 auto',
          padding: isMobile ? '32px 20px' : '48px 32px',
          background: `linear-gradient(135deg,
            hsl(var(--c-game) / 0.12),
            hsl(var(--c-toolkit) / 0.12))`,
          border: '1px solid hsl(var(--c-game) / 0.25)',
          borderRadius: 24,
          position: 'relative',
          overflow: 'hidden',
        }}>
          <div aria-hidden="true" style={{
            position: 'absolute', inset: 0,
            backgroundImage: 'radial-gradient(400px circle at 80% 20%, hsl(var(--c-event) / 0.18), transparent 60%)',
            pointerEvents: 'none',
          }} />
          <div style={{ position: 'relative' }}>
            <h2 style={{
              fontFamily: 'var(--f-display)',
              fontSize: isMobile ? 22 : 'clamp(24px, 3.5vw, 36px)',
              fontWeight: 700,
              margin: '0 0 14px',
              letterSpacing: '-0.015em',
              lineHeight: 1.1,
              color: 'var(--text)',
            }}>Pronto a giocare meglio?</h2>
            <p style={{
              fontFamily: 'var(--f-body)', fontSize: 15.5,
              color: 'var(--text-sec)', margin: '0 auto 24px',
              maxWidth: 460, lineHeight: 1.55,
            }}>Crea il tuo account gratis. Nessuna carta richiesta. Setup in 2 minuti.</p>
            <a href="#/register" style={{
              display: 'inline-flex', alignItems: 'center', gap: 8,
              padding: '12px 26px',
              background: `linear-gradient(135deg, hsl(var(--c-game)), hsl(var(--c-toolkit)))`,
              color: '#fff',
              fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 15,
              borderRadius: 999,
              textDecoration: 'none',
              boxShadow: '0 8px 24px hsl(var(--c-game) / 0.35)',
            }}>Inizia gratis →</a>
            <div style={{ marginTop: 18, fontSize: 13.5, color: 'var(--text-muted)' }}>
              Hai già un account?{' '}
              <a href="#/login" style={{
                color: 'hsl(var(--c-game))', fontWeight: 600, textDecoration: 'underline',
                textDecorationColor: 'hsl(var(--c-game) / 0.4)', textUnderlineOffset: 3,
              }}>Accedi</a>
            </div>
          </div>
        </div>
      </section>
    </>
  );
};

// ── DESKTOP / MOBILE FRAMES ────────────────────────────────────────────────
const DesktopScreen = ({ label, theme }) => (
  <div data-screen-label={label} style={{
    width: 1440,
    background: 'var(--bg)',
    borderRadius: 16,
    border: '1px solid var(--border-strong)',
    overflow: 'hidden',
    boxShadow: '0 32px 64px -24px rgba(0,0,0,0.25), 0 12px 32px -12px rgba(0,0,0,0.12)',
  }} data-theme={theme}>
    <div style={{
      padding: '8px 14px',
      background: 'var(--bg-sunken)',
      borderBottom: '1px solid var(--border)',
      display: 'flex', alignItems: 'center', gap: 10,
      fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)',
    }}>
      <span style={{ display: 'inline-flex', gap: 6 }}>
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#FF5F57' }} />
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#FEBC2E' }} />
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: '#28C840' }} />
      </span>
      <span style={{ marginLeft: 8 }}>meepleai.app/how-it-works · {label}</span>
    </div>
    <div style={{ background: 'var(--bg)' }}>
      <HowItWorksBody isMobile={false} />
    </div>
  </div>
);

const MobileScreen = ({ label, theme }) => (
  <div style={{
    width: 375,
    background: 'var(--bg)',
    borderRadius: 36,
    border: '8px solid #1a1410',
    overflow: 'hidden',
    boxShadow: '0 30px 60px rgba(0,0,0,0.6)',
    position: 'relative',
  }} data-theme={theme} data-screen-label={label}>
    {/* status bar */}
    <div style={{
      height: 30, background: 'var(--bg-sunken)',
      display: 'flex', alignItems: 'center', justifyContent: 'space-between',
      padding: '0 18px',
      fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 11,
      color: 'var(--text)',
    }}>
      <span>9:41</span>
      <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
        <span>●●●</span><span>📶</span><span>🔋</span>
      </span>
    </div>
    <HowItWorksBody isMobile={true} />
  </div>
);

// ── APP ────────────────────────────────────────────────────────────────────
const App = () => {
  const [theme, setTheme] = useState(() => {
    try { return localStorage.getItem('mai-sp3-how-it-works-theme') || 'light'; } catch { return 'light'; }
  });

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    try { localStorage.setItem('mai-sp3-how-it-works-theme', theme); } catch {}
  }, [theme]);

  return (
    <div style={{
      minHeight: '100vh',
      background: theme === 'dark' ? '#0a0805' : '#e8dfcf',
      padding: '40px 24px 80px',
    }}>
      {/* Toolbar */}
      <div style={{
        maxWidth: 1480, margin: '0 auto 32px',
        display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap',
      }}>
        <div>
          <div style={{
            fontFamily: 'var(--f-display)', fontWeight: 700, fontSize: 22,
            color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
            letterSpacing: '-0.015em',
          }}>SP3 · #2 — How it works</div>
          <div style={{
            fontFamily: 'var(--f-mono)', fontSize: 12,
            color: theme === 'dark' ? '#8a7860' : '#9a8870',
          }}>Route: /how-it-works · Pagina educativa statica · Riusa HeroGradient da public.jsx</div>
        </div>
        <div style={{ marginLeft: 'auto', display: 'inline-flex', gap: 8 }}>
          <button onClick={() => setTheme(theme === 'light' ? 'dark' : 'light')}
            style={{
              padding: '8px 14px', borderRadius: 999,
              border: '1px solid ' + (theme === 'dark' ? 'rgba(224,180,110,0.28)' : 'rgba(180,130,80,0.32)'),
              background: theme === 'dark' ? '#1e1710' : '#fff',
              color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
              fontFamily: 'var(--f-display)', fontWeight: 600, fontSize: 13,
              cursor: 'pointer',
            }}>{theme === 'light' ? '🌙 Dark' : '☀️ Light'}</button>
        </div>
      </div>

      {/* DESKTOP */}
      <h2 style={{
        maxWidth: 1480, margin: '0 auto 14px',
        fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 700,
        textTransform: 'uppercase', letterSpacing: '0.08em',
        color: theme === 'dark' ? '#c8b896' : '#5a4a38',
      }}>Desktop · 1440</h2>
      <div style={{
        maxWidth: 1480, margin: '0 auto 48px',
        display: 'flex', justifyContent: 'center',
      }}>
        <DesktopScreen label="01 Default desktop · How it works" theme={theme} />
      </div>

      {/* MOBILE */}
      <h2 style={{
        maxWidth: 1480, margin: '0 auto 14px',
        fontFamily: 'var(--f-display)', fontSize: 14, fontWeight: 700,
        textTransform: 'uppercase', letterSpacing: '0.08em',
        color: theme === 'dark' ? '#c8b896' : '#5a4a38',
      }}>Mobile · 375</h2>
      <div style={{
        maxWidth: 1480, margin: '0 auto 48px',
        display: 'flex', justifyContent: 'center',
      }}>
        <MobileScreen label="02 Default mobile · How it works" theme={theme} />
      </div>

      {/* DOD CHECKLIST */}
      <div style={{
        maxWidth: 900, margin: '40px auto 0',
        padding: '24px 28px',
        background: theme === 'dark' ? '#1e1710' : '#fff',
        border: '1px solid ' + (theme === 'dark' ? 'rgba(224,180,110,0.14)' : 'rgba(180,130,80,0.18)'),
        borderRadius: 16,
        color: theme === 'dark' ? '#f0e4d2' : '#2b1f12',
      }}>
        <h3 style={{ fontFamily: 'var(--f-display)', margin: '0 0 12px', fontSize: 18 }}>DoD Checklist · sp3-how-it-works</h3>
        <ul style={{ fontFamily: 'var(--f-body)', fontSize: 13.5, lineHeight: 1.7, color: theme === 'dark' ? '#c8b896' : '#5a4a38', paddingLeft: 20, margin: 0 }}>
          <li>✓ Route target /how-it-works</li>
          <li>✓ Hero: emoji stack 🎲🤖🧰 + h1 "Come funziona MeepleAI" + subtitle 1-riga</li>
          <li>✓ HeroGradient riusato 1:1 da public.jsx (pub-hero pattern: kicker mono + h1 con .mark gradient + hero-lead)</li>
          <li>✓ 4 step alternati left/right desktop (reverse su index pari), stacked mobile</li>
          <li>✓ Step cards: numero ordinale 01/02/03/04 grande Quicksand + EntityChip palette + h2 + 3 bullet + illustrazione gradient placeholder</li>
          <li>✓ Border-left palette entity progressione cromatica: Game → KB → Session → Toolkit</li>
          <li>✓ Illustrazioni inline (NO immagini esterne): library mockup · PDF→KB→Agent flow · Session+chat+timer · Toolkit bundle card</li>
          <li>✓ Dati realistici (Wingspan, Azul, Marco, Davide, AZL-7X2K, e5-base 768d)</li>
          <li>✓ Footer: gradient game→toolkit pill CTA "Inizia gratis" → /register + sub-CTA testuale "Accedi" → /login</li>
          <li>✓ ARIA: h1 unico hero · article+h2 step · aria-label "Passo N di 4" su numero</li>
          <li>✓ Light + Dark mode toggle persistito</li>
          <li>✓ Mobile 375 + Desktop 1440</li>
          <li>✓ Componente v2 nuovo: HowItWorksStep · Riusi: HeroGradient (public.jsx), EntityChip (e-chip pattern)</li>
          <li>✓ NON sovrascritto components.css né data.js</li>
          <li>✓ Stato singolo (default) — pagina statica come da contratto</li>
        </ul>
      </div>
    </div>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(<App />);
