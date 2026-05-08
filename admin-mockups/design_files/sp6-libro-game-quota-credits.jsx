/* ===================================================================
   SP6 Libro Game — Quota Credits Checkout (sp6-libro-game-quota-credits.jsx)
   Route: modale centrale + /gamebook/checkout/success
   Descrizione: Multi-step modal 4 step + soft warning 47/50 separato.
   Persona: Sara, 32, casual italian gamer / GM al tavolo (mobile-first).

   Gherkin: @G4.6 quota raggiunta + checkout flow.

   Stati implementati (7 V5 + light/dark + mobile/desktop):
     state-01-step1-quota-raggiunta        — 50/50 fill 100%
     state-02-step2-pack-picker-starter    — Starter selected default
     state-03-step3-checkout-form-filled   — form filled
     state-04-step3-payment-loading        — spinner
     state-05-step3-payment-failed         — banner red
     state-06-step4-success                — 🎉 CSS-only + prefers-reduced-motion guard
     state-07-soft-warning-47-50           — modal/toast SEPARATO dai 4 step

   Deviazioni:
     - Step 3 form payment è VISUAL ONLY (non integra real Stripe Elements).
       Test card 4242 placeholder. Brief riga 709.
     - Animation 🎉 step 4 = CSS-only + prefers-reduced-motion guard.
       Brief riga 710 (lezione SP4 wave 2 confetti).
     - Icon hero 💎 step 1 può usare gradient event→toolkit (warning→solution).

   Scope-out (descritti nel brief MA NON in stati richiesti riga 695-705):
     - stripe-down (riga 681-683)
     - partial-credit-issue (riga 684-685)

   StepIndicator: riusato dal pattern emerso in mockup B (sp6-libro-game-photo-upload).
   =================================================================== */

const { useState, useEffect } = React;

/* ─── entityHsl helper (replica apps/web/src/lib/theme/entity-hsl.ts) ─── */
const ENTITY_HSL = {
  game:    '25 95% 45%',
  player:  '262 83% 58%',
  session: '240 60% 55%',
  agent:   '38 92% 50%',
  kb:      '174 60% 40%',
  chat:    '220 80% 55%',
  event:   '350 89% 60%',
  toolkit: '142 70% 45%',
  tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;

/* ─── Pack catalog (brief riga 605-610) ─── */
const PACKS = [
  { id: 'starter', name: 'Starter', price: 5,  credits: 100,  perPar: '0.05',  badge: 'Più popolare', badgeKind: 'popular' },
  { id: 'mid',     name: 'Mid',     price: 20, credits: 500,  perPar: '0.04',  badge: null,           badgeKind: null },
  { id: 'pro',     name: 'Pro',     price: 35, credits: 1000, perPar: '0.035', badge: 'Risparmia 30%', badgeKind: 'save' },
];

/* ─── StepIndicator riusato da B (NON ridefinire pattern) ─── */
function StepIndicator({ current }) {
  const steps = [
    { n: 1, label: 'Quota',    icon: '💎' },
    { n: 2, label: 'Pacchetto',icon: '📦' },
    { n: 3, label: 'Pagamento',icon: '💳' },
    { n: 4, label: 'Fatto',    icon: '🎉' },
  ];
  return (
    <div className="qc-stepbar" role="list" aria-label="Progresso checkout">
      <div className="qc-step-list">
        {steps.map((s, i) => {
          const done = s.n < current;
          const active = s.n === current;
          return (
            <React.Fragment key={s.n}>
              <div className={`qc-step ${active ? 'active' : ''} ${done ? 'done' : ''}`} role="listitem">
                <span
                  className="qc-step-circle"
                  style={{
                    background: done || active ? entityHsl('toolkit') : 'transparent',
                    color: done || active ? '#fff' : 'var(--text-muted)',
                    border: `2px solid ${done || active ? entityHsl('toolkit') : 'var(--border)'}`,
                  }}
                  aria-current={active ? 'step' : undefined}
                >
                  {done ? '✓' : s.n}
                </span>
                <span className="qc-step-label" style={{ color: active ? 'var(--text)' : 'var(--text-muted)' }}>
                  {s.label}
                </span>
              </div>
              {i < steps.length - 1 && (
                <span className="qc-step-line" style={{
                  background: s.n < current ? entityHsl('toolkit') : 'var(--border)',
                }}/>
              )}
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
}

/* ─── Modal close X ─── */
function CloseX({ ariaLabel = 'Chiudi modal' }) {
  return (
    <button type="button" className="qc-close" aria-label={ariaLabel}>×</button>
  );
}

/* ───────────────────────────────────────────────────────────────
   STEP 1 — Quota raggiunta (warning hard 50/50)
   ─────────────────────────────────────────────────────────────── */

function Step1QuotaRaggiunta() {
  return (
    <div className="qc-step-content">
      <div className="qc-hero-icon" aria-hidden="true" style={{
        background: `radial-gradient(circle at 30% 30%, ${entityHsl('event', 0.25)}, ${entityHsl('toolkit', 0.18)} 70%)`,
        border: `1px solid ${entityHsl('event', 0.3)}`,
      }}>
        <span style={{ fontSize: 48 }}>💎</span>
      </div>

      <div className="qc-step-header">
        <h2 className="qc-step-h2">Quota raggiunta</h2>
        <p className="qc-step-sub">
          Hai tradotto 50 paragrafi questo mese. La quota gratuita si resetta il 1° giugno.
        </p>
      </div>

      <div className="qc-quota-card" style={{
        background: entityHsl('event', 0.06),
        borderColor: entityHsl('event', 0.25),
      }}>
        <div className="qc-quota-row">
          <span className="qc-quota-label">Quota mensile</span>
          <span className="qc-quota-counter" style={{ color: entityHsl('event') }}>
            <span style={{ fontVariantNumeric: 'tabular-nums' }}>50</span>
            <span style={{ color: 'var(--text-muted)', fontSize: '60%' }}> / 50</span>
          </span>
        </div>
        <div className="qc-quota-bar" aria-label="Quota 50 su 50 paragrafi">
          <div className="qc-quota-fill" style={{ width: '100%', background: entityHsl('event', 0.7) }}/>
        </div>
        <div className="qc-quota-foot">
          <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11, color: 'var(--text-muted)' }}>
            Reset tra 12 giorni
          </span>
        </div>
      </div>

      <div className="qc-step-footer qc-cta-stack">
        <button
          type="button"
          className="qc-cta-primary"
          style={{ background: entityHsl('event') }}
          aria-label="Acquista 100 crediti per 5 euro"
        >
          💎 Acquista 100 crediti (€5)
        </button>
        <button type="button" className="qc-cta-outline">
          ⏸️ Continua senza traduzione
        </button>
        <a href="#" className="qc-footer-link">
          Cosa sono i crediti? →
        </a>
      </div>
    </div>
  );
}

/* ───────────────────────────────────────────────────────────────
   STEP 2 — Pack picker
   ─────────────────────────────────────────────────────────────── */

function PackCard({ pack, selected }) {
  return (
    <label
      className={`qc-pack-card ${selected ? 'selected' : ''}`}
      style={{
        borderColor: selected ? 'var(--border-strong)' : 'var(--border)',
        background: selected ? entityHsl('toolkit', 0.06) : 'var(--bg-card)',
        boxShadow: selected ? `0 0 0 3px ${entityHsl('toolkit', 0.15)}` : 'var(--shadow-xs)',
      }}
    >
      <input type="radio" name="pack" defaultChecked={selected} className="qc-pack-radio" aria-label={`Pacchetto ${pack.name}`}/>
      {pack.badge && (
        <span
          className="qc-pack-badge"
          style={{
            background: pack.badgeKind === 'popular' ? entityHsl('toolkit') : entityHsl('event'),
            color: '#fff',
          }}
        >
          {pack.badge}
        </span>
      )}
      <div className="qc-pack-name">{pack.name}</div>
      <div className="qc-pack-credits" style={{ color: entityHsl('toolkit') }}>
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{pack.credits.toLocaleString('it')}</span>
        <span className="qc-pack-credits-sub">crediti</span>
      </div>
      <div className="qc-pack-price">
        <span style={{ fontVariantNumeric: 'tabular-nums' }}>€{pack.price}</span>
      </div>
      <div className="qc-pack-perpar">
        €{pack.perPar} / paragrafo
      </div>
      <div className="qc-pack-radio-visual" style={{
        borderColor: selected ? entityHsl('toolkit') : 'var(--border)',
        background: selected ? entityHsl('toolkit') : 'transparent',
      }}>
        {selected && <span className="qc-pack-radio-dot"/>}
      </div>
    </label>
  );
}

function Step2PackPicker({ selectedId = 'starter' }) {
  const selectedPack = PACKS.find(p => p.id === selectedId);
  return (
    <div className="qc-step-content">
      <div className="qc-step-header">
        <h2 className="qc-step-h2">Scegli il tuo pacchetto</h2>
        <p className="qc-step-sub">Più paragrafi = miglior prezzo. I crediti non scadono.</p>
      </div>

      <div className="qc-pack-grid" role="radiogroup" aria-label="Scelta pacchetto crediti">
        {PACKS.map(p => (
          <PackCard key={p.id} pack={p} selected={p.id === selectedId}/>
        ))}
      </div>

      <div className="qc-disclaimer">
        I crediti non scadono. La quota free di 50 paragrafi si resetta automaticamente ogni mese.
        {' '}
        <a href="#" style={{ color: entityHsl('toolkit'), textDecoration: 'underline' }}>Privacy & GDPR →</a>
      </div>

      <div className="qc-step-footer qc-step2-footer">
        <div className="qc-total-recap">
          <span className="qc-total-label">Totale</span>
          <span className="qc-total-value" style={{ fontVariantNumeric: 'tabular-nums' }}>€{selectedPack.price}</span>
        </div>
        <button
          type="button"
          className="qc-cta-primary"
          style={{ background: entityHsl('toolkit'), opacity: selectedId ? 1 : 0.5 }}
          disabled={!selectedId}
        >
          Continua →
        </button>
      </div>
    </div>
  );
}

/* ───────────────────────────────────────────────────────────────
   STEP 3 — Checkout placeholder (Stripe Elements style — VISUAL ONLY)
   ─────────────────────────────────────────────────────────────── */

function CheckoutForm({ variant = 'filled' }) {
  // variant: filled | loading | failed
  const isLoading = variant === 'loading';
  const isFailed = variant === 'failed';

  return (
    <div className="qc-step-content">
      <div className="qc-step-header">
        <h2 className="qc-step-h2">Pagamento</h2>
        <p className="qc-step-sub">
          Pacchetto <strong>Starter</strong> · 100 crediti · <span style={{ fontVariantNumeric: 'tabular-nums' }}>€5,00</span>
        </p>
      </div>

      {isFailed && (
        <div className="qc-banner qc-banner-error" role="alert" style={{
          background: entityHsl('event', 0.1),
          borderColor: entityHsl('event', 0.4),
        }}>
          <span aria-hidden="true" className="qc-banner-icon" style={{ background: entityHsl('event'), color: '#fff' }}>!</span>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="qc-banner-title" style={{ color: entityHsl('event') }}>Pagamento rifiutato</div>
            <div className="qc-banner-sub">Carta scaduta · prova un altro metodo o riscrivi i dati.</div>
          </div>
        </div>
      )}

      {/* Form payment VISUAL ONLY — non integra real Stripe Elements (brief riga 709) */}
      <div className="qc-pay-form" aria-label="Modulo pagamento (placeholder visual)">
        <div className="qc-pay-field">
          <label className="qc-pay-label">Numero carta</label>
          <div className="qc-pay-input qc-pay-card-input">
            <span className="qc-pay-value" style={{ fontFamily: 'var(--f-mono)' }}>
              •••• •••• •••• 4242
            </span>
            <span className="qc-card-brand" aria-hidden="true">VISA</span>
          </div>
        </div>

        <div className="qc-pay-row">
          <div className="qc-pay-field">
            <label className="qc-pay-label">Scadenza</label>
            <div className="qc-pay-input">
              <span className="qc-pay-value" style={{ fontFamily: 'var(--f-mono)' }}>12 / 27</span>
            </div>
          </div>
          <div className="qc-pay-field">
            <label className="qc-pay-label">CVC</label>
            <div className="qc-pay-input">
              <span className="qc-pay-value" style={{ fontFamily: 'var(--f-mono)' }}>•••</span>
            </div>
          </div>
        </div>

        <div className="qc-pay-field">
          <label className="qc-pay-label">Nome sulla carta</label>
          <div className="qc-pay-input">
            <span className="qc-pay-value">Sara Bianchi</span>
          </div>
        </div>

        <div className="qc-pay-field">
          <label className="qc-pay-label">Paese</label>
          <div className="qc-pay-input qc-pay-select">
            <span className="qc-pay-value">🇮🇹 Italia</span>
            <span className="qc-pay-chev" aria-hidden="true">▾</span>
          </div>
        </div>
      </div>

      <div className="qc-recap">
        <div className="qc-recap-row">
          <span>100 crediti</span>
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>€5,00</span>
        </div>
        <div className="qc-recap-row qc-recap-muted">
          <span>IVA inclusa</span>
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>—</span>
        </div>
        <div className="qc-recap-row qc-recap-total">
          <span>Totale</span>
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>€5,00</span>
        </div>
      </div>

      <div className="qc-trust-row" aria-label="Garanzie sicurezza">
        <span className="qc-trust-chip">
          <span aria-hidden="true">🔒</span>
          <span>SSL secure</span>
        </span>
        <span className="qc-trust-chip">
          <span aria-hidden="true">✓</span>
          <span>Stripe powered</span>
        </span>
        <span className="qc-trust-chip">
          <span aria-hidden="true">✓</span>
          <span>Politica rimborso 14gg</span>
        </span>
      </div>

      <div className="qc-step-footer qc-cta-stack">
        <button
          type="button"
          className="qc-cta-primary"
          disabled={isLoading}
          style={{
            background: entityHsl('toolkit'),
            opacity: isLoading ? 0.85 : 1,
            cursor: isLoading ? 'wait' : 'pointer',
          }}
          aria-busy={isLoading || undefined}
        >
          {isLoading ? (
            <>
              <span className="qc-spinner" aria-hidden="true"/>
              <span>Elaborazione…</span>
            </>
          ) : (
            <>Paga €5</>
          )}
        </button>
        <a href="#" className="qc-footer-link">← Torna ai pacchetti</a>
      </div>
    </div>
  );
}

/* ───────────────────────────────────────────────────────────────
   STEP 4 — Success
   ─────────────────────────────────────────────────────────────── */

function Step4Success() {
  return (
    <div className="qc-step-content qc-success">
      {/* Confetti CSS-only (rispetta prefers-reduced-motion via @media — vedi <style>) */}
      <div className="qc-confetti" aria-hidden="true">
        {Array.from({ length: 14 }).map((_, i) => (
          <span
            key={i}
            className="qc-confetto"
            style={{
              left: `${(i * 7.3) % 100}%`,
              background: [
                entityHsl('toolkit'),
                entityHsl('event'),
                entityHsl('agent'),
                entityHsl('chat'),
                entityHsl('game'),
              ][i % 5],
              animationDelay: `${(i * 0.13) % 1.4}s`,
              animationDuration: `${1.6 + (i % 3) * 0.4}s`,
            }}
          />
        ))}
      </div>

      <div className="qc-success-hero">
        <div className="qc-success-emoji" aria-hidden="true">🎉</div>
        <h2 className="qc-success-title">Crediti aggiunti!</h2>
        <p className="qc-success-sub">100 crediti aggiunti al tuo account. Buon gioco!</p>
      </div>

      <div className="qc-recap-card" style={{ background: 'var(--bg-card)', borderColor: 'var(--border)' }}>
        <div className="qc-recap-row">
          <span>Crediti precedenti</span>
          <span style={{ fontFamily: 'var(--f-mono)', fontVariantNumeric: 'tabular-nums' }}>0</span>
        </div>
        <div className="qc-recap-row" style={{ color: entityHsl('toolkit') }}>
          <span>Crediti acquistati</span>
          <span style={{ fontFamily: 'var(--f-mono)', fontVariantNumeric: 'tabular-nums' }}>+100</span>
        </div>
        <div className="qc-recap-row qc-recap-total" style={{ color: entityHsl('toolkit') }}>
          <span>Bilancio crediti</span>
          <span style={{ fontFamily: 'var(--f-mono)', fontVariantNumeric: 'tabular-nums' }}>100</span>
        </div>
        <div className="qc-recap-divider"/>
        <div className="qc-recap-row qc-recap-muted">
          <span>Quota free questo mese</span>
          <span style={{ fontFamily: 'var(--f-mono)', fontVariantNumeric: 'tabular-nums' }}>50 / 50</span>
        </div>
        <div className="qc-recap-row qc-recap-muted">
          <span style={{ fontSize: 11 }}>Si resetta il 1° giugno</span>
          <span style={{ fontSize: 11 }}>1 paragrafo = 1 credito</span>
        </div>
      </div>

      <div className="qc-step-footer qc-cta-stack">
        <button
          type="button"
          className="qc-cta-primary"
          style={{ background: entityHsl('session') }}
        >
          🎯 Torna al gioco →
        </button>
        <a href="#" className="qc-footer-link">Vedi ricevuta · email a sara@…</a>
      </div>
    </div>
  );
}

/* ───────────────────────────────────────────────────────────────
   SOFT WARNING 47/50 — separato dai 4 step (brief riga 687-693)
   Toast bottom mobile / modal centro desktop.
   ─────────────────────────────────────────────────────────────── */

function SoftWarning4750({ desktop }) {
  if (desktop) {
    return (
      <div className="qc-soft-modal" role="dialog" aria-modal="true" aria-labelledby="soft-title">
        <div className="qc-soft-modal-inner" style={{
          background: 'var(--bg-card)',
          borderColor: entityHsl('agent', 0.3),
        }}>
          <div className="qc-soft-icon" style={{
            background: entityHsl('agent', 0.15),
            color: entityHsl('agent'),
          }} aria-hidden="true">🟡</div>
          <div className="qc-soft-body">
            <h3 id="soft-title" className="qc-soft-title">Quasi alla fine della quota</h3>
            <p className="qc-soft-sub">Hai usato 47/50 paragrafi gratis questo mese. Restano 3.</p>
            <div className="qc-soft-actions">
              <button
                type="button"
                className="qc-cta-primary qc-cta-soft"
                style={{ background: entityHsl('toolkit') }}
              >
                Acquista crediti ora
              </button>
              <button type="button" className="qc-cta-outline qc-cta-soft">
                Ok, continua
              </button>
            </div>
          </div>
          <CloseX ariaLabel="Chiudi avviso"/>
        </div>
      </div>
    );
  }
  // Mobile = toast bottom
  return (
    <div className="qc-soft-toast" role="status" aria-live="polite" style={{
      background: 'var(--bg-card)',
      borderColor: entityHsl('agent', 0.3),
    }}>
      <div className="qc-soft-toast-row">
        <span className="qc-soft-icon-sm" style={{
          background: entityHsl('agent', 0.15),
          color: entityHsl('agent'),
        }} aria-hidden="true">🟡</span>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div className="qc-soft-title" style={{ fontSize: 13 }}>Quasi alla fine della quota</div>
          <div className="qc-soft-sub" style={{ fontSize: 11 }}>47/50 — restano 3 paragrafi gratis.</div>
        </div>
      </div>
      <div className="qc-soft-actions">
        <button
          type="button"
          className="qc-cta-primary qc-cta-soft"
          style={{ background: entityHsl('toolkit') }}
        >
          Acquista crediti
        </button>
        <button type="button" className="qc-cta-outline qc-cta-soft">
          Ok
        </button>
      </div>
    </div>
  );
}

/* ───────────────────────────────────────────────────────────────
   Modal shell (4 step lineari + close X)
   ─────────────────────────────────────────────────────────────── */

function ModalShell({ step, children, desktop }) {
  return (
    <div className="qc-modal-overlay">
      <div
        className={`qc-modal ${desktop ? 'qc-modal-desktop' : 'qc-modal-mobile'}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={`qc-modal-title-${step}`}
        style={{
          background: 'var(--bg)',
          borderColor: 'var(--border)',
        }}
      >
        <div className="qc-modal-head">
          <StepIndicator current={step}/>
          <CloseX/>
        </div>
        <div className="qc-modal-body" id={`qc-modal-title-${step}`}>
          {children}
        </div>
      </div>
    </div>
  );
}

/* ───────────────────────────────────────────────────────────────
   State → composed UI
   ─────────────────────────────────────────────────────────────── */

function StateBody({ stateId, desktop }) {
  if (stateId === 'state-01-step1-quota-raggiunta') {
    return <ModalShell step={1} desktop={desktop}><Step1QuotaRaggiunta/></ModalShell>;
  }
  if (stateId === 'state-02-step2-pack-picker-starter') {
    return <ModalShell step={2} desktop={desktop}><Step2PackPicker selectedId="starter"/></ModalShell>;
  }
  if (stateId === 'state-03-step3-checkout-form-filled') {
    return <ModalShell step={3} desktop={desktop}><CheckoutForm variant="filled"/></ModalShell>;
  }
  if (stateId === 'state-04-step3-payment-loading') {
    return <ModalShell step={3} desktop={desktop}><CheckoutForm variant="loading"/></ModalShell>;
  }
  if (stateId === 'state-05-step3-payment-failed') {
    return <ModalShell step={3} desktop={desktop}><CheckoutForm variant="failed"/></ModalShell>;
  }
  if (stateId === 'state-06-step4-success') {
    return <ModalShell step={4} desktop={desktop}><Step4Success/></ModalShell>;
  }
  if (stateId === 'state-07-soft-warning-47-50') {
    return (
      <div className="qc-soft-stage">
        <div className="qc-soft-bg" aria-hidden="true">
          {/* fake page behind toast/modal */}
          <div className="qc-fake-line" style={{ width: '70%' }}/>
          <div className="qc-fake-line" style={{ width: '90%' }}/>
          <div className="qc-fake-line" style={{ width: '60%' }}/>
          <div className="qc-fake-line" style={{ width: '85%' }}/>
          <div className="qc-fake-line" style={{ width: '40%' }}/>
          <div className="qc-fake-line" style={{ width: '78%' }}/>
        </div>
        <SoftWarning4750 desktop={desktop}/>
      </div>
    );
  }
  return null;
}

/* ───────────────────────────────────────────────────────────────
   Frames (mobile 375 + desktop 1440)
   ─────────────────────────────────────────────────────────────── */

function MobileFrame({ stateId, label, gherkin }) {
  return (
    <div data-screen-label={`${label} · mobile`}>
      <div className="qc-frame-caption">
        📱 Mobile 375 — {label}
        {gherkin && <span className="gherkin">{gherkin}</span>}
      </div>
      <div className="qc-phone" id={`${stateId}-mobile`}>
        <div className="qc-phone-sbar">
          <span style={{ fontFamily: 'var(--f-mono)', fontSize: 11 }}>9:41</span>
          <span style={{ fontSize: 11 }}>▲ ◆ ▶</span>
        </div>
        <div className="qc-phone-body">
          <StateBody stateId={stateId} desktop={false}/>
        </div>
      </div>
    </div>
  );
}

function DesktopFrame({ stateId, label, gherkin }) {
  return (
    <div style={{ width: '100%' }} data-screen-label={`${label} · desktop`}>
      <div className="qc-frame-caption">
        🖥️ Desktop 1440 — {label}
        {gherkin && <span className="gherkin">{gherkin}</span>}
      </div>
      <div className="qc-desktop" id={`${stateId}-desktop`}>
        <div className="qc-desktop-bar">
          <span className="traffic"/><span className="traffic"/><span className="traffic"/>
          <span className="url">meepleai.app/gamebook/checkout</span>
        </div>
        <div className="qc-desktop-body">
          <StateBody stateId={stateId} desktop={true}/>
        </div>
      </div>
    </div>
  );
}

/* ───────────────────────────────────────────────────────────────
   STATES catalog (7 V5)
   ─────────────────────────────────────────────────────────────── */

const STATES = [
  { id: 'state-01-step1-quota-raggiunta',     label: 'Step 1 · Quota raggiunta (50/50)',          group: 1, gherkin: 'G4.6' },
  { id: 'state-02-step2-pack-picker-starter', label: 'Step 2 · Pack picker (Starter selected)',   group: 2, gherkin: 'G4.6' },
  { id: 'state-03-step3-checkout-form-filled',label: 'Step 3 · Checkout form filled',             group: 3, gherkin: 'G4.6' },
  { id: 'state-04-step3-payment-loading',     label: 'Step 3 · Payment loading',                  group: 3, gherkin: 'G4.6' },
  { id: 'state-05-step3-payment-failed',      label: 'Step 3 · Payment failed (banner red)',      group: 3, gherkin: 'G4.6' },
  { id: 'state-06-step4-success',             label: 'Step 4 · Success (🎉 reduced-motion guard)',group: 4, gherkin: 'G4.6' },
  { id: 'state-07-soft-warning-47-50',        label: 'Soft warning 47/50 (separato dai 4 step)',  group: 'soft', gherkin: 'G4.6' },
];

const GROUP_LABELS = {
  1: 'STEP 1 · Quota raggiunta',
  2: 'STEP 2 · Pack picker',
  3: 'STEP 3 · Checkout',
  4: 'STEP 4 · Success',
  soft: 'SOFT WARNING · separato dai 4 step',
};

function App() {
  const [stepFilter, setStepFilter] = useState('all');
  const [frameFilter, setFrameFilter] = useState('both');

  useEffect(() => {
    const sh = e => setStepFilter(e.detail);
    const fh = e => setFrameFilter(e.detail);
    document.addEventListener('qc-tweak-step', sh);
    document.addEventListener('qc-tweak-frame', fh);
    return () => {
      document.removeEventListener('qc-tweak-step', sh);
      document.removeEventListener('qc-tweak-frame', fh);
    };
  }, []);

  const visible = stepFilter === 'all'
    ? STATES
    : STATES.filter(s => String(s.group) === String(stepFilter));

  const groups = ['1', '2', '3', '4', 'soft']
    .map(g => ({ key: g, items: visible.filter(s => String(s.group) === g) }))
    .filter(g => g.items.length > 0);

  return (
    <div>
      {groups.map(g => (
        <div key={g.key}>
          <div className="qc-section-label">
            <span className="step">{GROUP_LABELS[g.key]}</span>
          </div>
          {g.items.map(s => (
            <section key={s.id} id={s.id} style={{ marginBottom: 36 }} data-screen-label={s.label}>
              <div className="qc-state-grid">
                {(frameFilter === 'both' || frameFilter === 'mobile') && (
                  <MobileFrame stateId={s.id} label={s.label} gherkin={s.gherkin}/>
                )}
                {(frameFilter === 'both' || frameFilter === 'desktop') && (
                  <DesktopFrame stateId={s.id} label={s.label} gherkin={s.gherkin}/>
                )}
              </div>
            </section>
          ))}
        </div>
      ))}
    </div>
  );
}

/* ─── Inline scoped styles ─── */
const qcStyleEl = document.createElement('style');
qcStyleEl.textContent = `
  /* Modal overlay + shell */
  .qc-modal-overlay {
    position: absolute; inset: 0;
    background: rgba(0,0,0,.45);
    backdrop-filter: blur(3px);
    display: flex; align-items: center; justify-content: center;
    padding: 12px;
    z-index: 60;
  }
  :root[data-theme="dark"] .qc-modal-overlay { background: rgba(0,0,0,.65); }

  .qc-modal {
    width: 100%;
    max-height: calc(100% - 24px);
    border-radius: var(--r-2xl);
    border: 1px solid var(--border);
    box-shadow: var(--shadow-lg);
    display: flex; flex-direction: column;
    overflow: hidden;
  }
  .qc-modal-mobile { max-width: 100%; }
  .qc-modal-desktop { max-width: 520px; }

  .qc-modal-head {
    flex-shrink: 0;
    display: flex; align-items: center; gap: 8px;
    padding: 14px 14px 0 14px;
    position: relative;
  }
  .qc-modal-body {
    flex: 1; min-height: 0;
    overflow-y: auto;
    -webkit-overflow-scrolling: touch;
  }

  .qc-close {
    position: absolute; top: 10px; right: 10px;
    width: 32px; height: 32px; border-radius: 50%;
    background: var(--bg-muted); border: 1px solid var(--border);
    color: var(--text); cursor: pointer; font-size: 18px; line-height: 1;
    display: flex; align-items: center; justify-content: center;
    flex-shrink: 0;
  }

  /* Step indicator */
  .qc-stepbar { flex: 1; padding: 4px 0; }
  .qc-step-list { display: flex; align-items: center; gap: 6px; }
  .qc-step { display: flex; align-items: center; gap: 6px; }
  .qc-step-circle {
    width: 22px; height: 22px; border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    font-family: var(--f-display); font-weight: 700; font-size: 11px;
    flex-shrink: 0;
  }
  .qc-step-label {
    font-family: var(--f-display); font-weight: 600; font-size: 11px;
    white-space: nowrap;
  }
  .qc-step-line { flex: 1; height: 2px; min-width: 8px; border-radius: var(--r-pill); }

  /* Step content */
  .qc-step-content {
    padding: 18px 18px 22px;
    display: flex; flex-direction: column; gap: 16px;
  }
  .qc-step-header { display: flex; flex-direction: column; gap: 4px; }
  .qc-step-h2 {
    font-family: var(--f-display); font-weight: 700; font-size: 22px;
    color: var(--text); margin: 0; line-height: 1.2;
  }
  .qc-step-sub { font-size: 13px; color: var(--text-sec); margin: 0; line-height: 1.5; }

  /* Hero icon (step 1) */
  .qc-hero-icon {
    width: 96px; height: 96px; border-radius: 28px;
    margin: 4px auto 0;
    display: flex; align-items: center; justify-content: center;
  }

  /* Quota card (step 1) */
  .qc-quota-card {
    border: 1px solid; border-radius: var(--r-lg);
    padding: 14px 16px;
    display: flex; flex-direction: column; gap: 10px;
  }
  .qc-quota-row {
    display: flex; justify-content: space-between; align-items: flex-end;
  }
  .qc-quota-label {
    font-family: var(--f-mono); font-size: 11px;
    text-transform: uppercase; letter-spacing: 0.06em;
    color: var(--text-muted);
  }
  .qc-quota-counter {
    font-family: var(--f-display); font-weight: 800; font-size: 28px;
    line-height: 1;
  }
  .qc-quota-bar {
    height: 8px; border-radius: var(--r-pill);
    background: var(--bg-muted); overflow: hidden;
  }
  .qc-quota-fill { height: 100%; transition: width var(--dur-md) var(--ease-out); }
  .qc-quota-foot { display: flex; justify-content: space-between; }

  /* CTA */
  .qc-step-footer { margin-top: auto; padding-top: 8px; }
  .qc-cta-stack { display: flex; flex-direction: column; gap: 8px; align-items: stretch; }

  .qc-cta-primary {
    width: 100%; padding: 14px 18px; border-radius: var(--r-lg);
    color: #fff; border: none; cursor: pointer;
    font-family: var(--f-display); font-weight: 700; font-size: 14px;
    display: inline-flex; align-items: center; justify-content: center; gap: 8px;
    transition: filter var(--dur-sm) var(--ease-out), transform var(--dur-sm) var(--ease-out);
  }
  .qc-cta-primary:hover:not(:disabled) { filter: brightness(1.06); }
  .qc-cta-primary:active:not(:disabled) { transform: scale(0.98); }

  .qc-cta-outline {
    width: 100%; padding: 14px 18px; border-radius: var(--r-lg);
    background: var(--bg-card); border: 1px solid var(--border-strong);
    color: var(--text); cursor: pointer;
    font-family: var(--f-display); font-weight: 600; font-size: 14px;
  }
  .qc-cta-outline:hover { background: var(--bg-hover); }

  .qc-footer-link {
    display: inline-block; padding: 6px 4px;
    color: var(--text-muted); font-size: 12px; text-align: center;
    text-decoration: none;
  }
  .qc-footer-link:hover { color: var(--text-sec); }

  /* Pack picker */
  .qc-pack-grid {
    display: flex; flex-direction: column; gap: 10px;
  }
  .qc-pack-card {
    position: relative; padding: 14px 14px 14px 16px;
    border-radius: var(--r-lg); border: 2px solid;
    cursor: pointer;
    display: grid;
    grid-template-columns: 1fr auto auto;
    grid-template-rows: auto auto auto;
    grid-template-areas:
      "name . radio"
      "credits price radio"
      "perpar . radio";
    column-gap: 12px; row-gap: 2px;
    transition: transform var(--dur-sm) var(--ease-out);
  }
  .qc-pack-card:hover { transform: translateY(-1px); }
  .qc-pack-radio { position: absolute; opacity: 0; pointer-events: none; }
  .qc-pack-name {
    grid-area: name;
    font-family: var(--f-display); font-weight: 700; font-size: 13px;
    color: var(--text-sec); text-transform: uppercase; letter-spacing: 0.06em;
  }
  .qc-pack-credits {
    grid-area: credits;
    font-family: var(--f-display); font-weight: 800; font-size: 22px;
    line-height: 1; display: flex; align-items: baseline; gap: 6px;
  }
  .qc-pack-credits-sub {
    font-family: var(--f-body); font-size: 12px; font-weight: 600;
    color: var(--text-muted); text-transform: lowercase;
  }
  .qc-pack-price {
    grid-area: price;
    font-family: var(--f-display); font-weight: 800; font-size: 24px;
    color: var(--text); line-height: 1;
  }
  .qc-pack-perpar {
    grid-area: perpar;
    font-family: var(--f-mono); font-size: 11px;
    color: var(--text-muted);
  }
  .qc-pack-radio-visual {
    grid-area: radio;
    align-self: center; justify-self: end;
    width: 22px; height: 22px; border-radius: 50%;
    border: 2px solid;
    display: flex; align-items: center; justify-content: center;
    transition: background var(--dur-sm) var(--ease-out), border-color var(--dur-sm) var(--ease-out);
  }
  .qc-pack-radio-dot {
    width: 8px; height: 8px; border-radius: 50%; background: #fff;
  }
  .qc-pack-badge {
    position: absolute; top: -8px; right: 12px;
    padding: 3px 8px; border-radius: var(--r-pill);
    font-family: var(--f-display); font-weight: 700; font-size: 10px;
    text-transform: uppercase; letter-spacing: 0.04em;
  }

  .qc-disclaimer {
    font-size: 11px; color: var(--text-muted); line-height: 1.5;
    padding: 10px 12px; background: var(--bg-muted);
    border-radius: var(--r-md);
  }

  .qc-step2-footer {
    display: flex; align-items: center; gap: 10px;
    flex-direction: row;
  }
  .qc-total-recap {
    display: flex; flex-direction: column; align-items: flex-start;
    flex-shrink: 0;
  }
  .qc-total-label {
    font-family: var(--f-mono); font-size: 10px;
    text-transform: uppercase; letter-spacing: 0.06em;
    color: var(--text-muted);
  }
  .qc-total-value {
    font-family: var(--f-display); font-weight: 800; font-size: 22px;
    color: var(--text); line-height: 1;
  }

  /* Pay form */
  .qc-pay-form { display: flex; flex-direction: column; gap: 10px; }
  .qc-pay-row { display: flex; gap: 10px; }
  .qc-pay-row .qc-pay-field { flex: 1; }
  .qc-pay-field { display: flex; flex-direction: column; gap: 4px; }
  .qc-pay-label {
    font-family: var(--f-mono); font-size: 10px;
    text-transform: uppercase; letter-spacing: 0.06em;
    color: var(--text-muted);
  }
  .qc-pay-input {
    display: flex; align-items: center; justify-content: space-between;
    padding: 12px 14px; border-radius: var(--r-md);
    background: var(--bg-card); border: 1px solid var(--border);
    min-height: 44px;
  }
  .qc-pay-input:focus-within { outline: 2px solid hsl(var(--c-toolkit)); outline-offset: 1px; }
  .qc-pay-value { font-family: var(--f-body); font-size: 14px; color: var(--text); }
  .qc-card-brand {
    font-family: var(--f-mono); font-weight: 700; font-size: 10px;
    color: var(--text-muted); letter-spacing: 0.1em;
  }
  .qc-pay-select { cursor: pointer; }
  .qc-pay-chev { color: var(--text-muted); font-size: 10px; }

  /* Recap */
  .qc-recap {
    border: 1px solid var(--border); border-radius: var(--r-md);
    padding: 12px 14px; background: var(--bg-card);
    display: flex; flex-direction: column; gap: 6px;
  }
  .qc-recap-row {
    display: flex; justify-content: space-between; align-items: center;
    font-size: 13px; color: var(--text);
  }
  .qc-recap-muted { color: var(--text-muted); font-size: 11px; }
  .qc-recap-total {
    font-family: var(--f-display); font-weight: 800; font-size: 16px;
    padding-top: 6px; border-top: 1px solid var(--border-light);
    margin-top: 4px;
  }
  .qc-recap-divider {
    height: 1px; background: var(--border-light); margin: 4px 0;
  }
  .qc-recap-card {
    border: 1px solid; border-radius: var(--r-lg);
    padding: 14px 16px;
    display: flex; flex-direction: column; gap: 6px;
  }

  /* Trust badges */
  .qc-trust-row {
    display: flex; flex-wrap: wrap; gap: 6px; justify-content: center;
  }
  .qc-trust-chip {
    display: inline-flex; align-items: center; gap: 4px;
    padding: 4px 10px; border-radius: var(--r-pill);
    background: var(--bg-muted);
    font-family: var(--f-mono); font-size: 10px;
    color: var(--text-sec); white-space: nowrap;
  }

  /* Banner error */
  .qc-banner {
    display: flex; align-items: flex-start; gap: 10px;
    padding: 10px 12px; border-radius: var(--r-md);
    border: 1px solid;
  }
  .qc-banner-icon {
    width: 22px; height: 22px; border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    font-family: var(--f-display); font-weight: 800; font-size: 13px;
    flex-shrink: 0;
  }
  .qc-banner-title {
    font-family: var(--f-display); font-weight: 700; font-size: 13px;
  }
  .qc-banner-sub { font-size: 12px; color: var(--text-sec); margin-top: 2px; }

  /* Spinner */
  .qc-spinner {
    width: 16px; height: 16px; border-radius: 50%;
    border: 2px solid rgba(255,255,255,.4);
    border-top-color: #fff;
    animation: qcSpin 0.7s linear infinite;
  }
  @keyframes qcSpin { to { transform: rotate(360deg); } }

  /* Success */
  .qc-success {
    align-items: stretch; text-align: center; gap: 18px;
    position: relative;
  }
  .qc-success-hero {
    display: flex; flex-direction: column; align-items: center; gap: 6px;
    padding: 12px 0 4px;
  }
  .qc-success-emoji {
    font-size: 64px; line-height: 1;
    animation: qcPop 0.6s var(--ease-spring) both;
  }
  @keyframes qcPop {
    0% { transform: scale(0.4) rotate(-12deg); opacity: 0; }
    60% { transform: scale(1.15) rotate(8deg); opacity: 1; }
    100% { transform: scale(1) rotate(0deg); opacity: 1; }
  }
  .qc-success-title {
    font-family: var(--f-display); font-weight: 800; font-size: 28px;
    color: var(--text); margin: 0;
  }
  .qc-success-sub {
    font-size: 13px; color: var(--text-sec); margin: 0;
  }

  /* Confetti CSS-only — rispetta prefers-reduced-motion (brief riga 710) */
  .qc-confetti {
    position: absolute; inset: 0; pointer-events: none;
    overflow: hidden;
  }
  .qc-confetto {
    position: absolute; top: -10px;
    width: 7px; height: 12px;
    border-radius: 2px;
    animation: qcConfetto 2s ease-in infinite;
    opacity: 0.85;
  }
  @keyframes qcConfetto {
    0% { transform: translateY(-10px) rotate(0deg); opacity: 0; }
    20% { opacity: 0.95; }
    100% { transform: translateY(420px) rotate(540deg); opacity: 0; }
  }

  @media (prefers-reduced-motion: reduce) {
    .qc-success-emoji { animation: none; }
    .qc-confetto { animation: none; opacity: 0; }
    .qc-spinner { animation: none; }
  }

  /* Soft warning (mobile toast / desktop modal) */
  .qc-soft-stage {
    position: relative; height: 100%; min-height: 100%;
    background: var(--bg);
    overflow: hidden;
  }
  .qc-soft-bg {
    padding: 32px 18px; display: flex; flex-direction: column; gap: 10px;
    opacity: 0.4;
  }
  .qc-fake-line {
    height: 10px; border-radius: var(--r-pill);
    background: var(--bg-muted);
  }
  .qc-soft-toast {
    position: absolute; left: 12px; right: 12px; bottom: 16px;
    border: 1px solid; border-radius: var(--r-lg);
    padding: 12px 14px;
    box-shadow: var(--shadow-md);
    display: flex; flex-direction: column; gap: 10px;
  }
  .qc-soft-toast-row {
    display: flex; align-items: flex-start; gap: 10px;
  }
  .qc-soft-icon-sm {
    width: 28px; height: 28px; border-radius: 50%;
    display: flex; align-items: center; justify-content: center;
    flex-shrink: 0;
  }
  .qc-soft-modal {
    position: absolute; inset: 0;
    background: rgba(0,0,0,.4); backdrop-filter: blur(2px);
    display: flex; align-items: center; justify-content: center;
    padding: 20px;
  }
  .qc-soft-modal-inner {
    position: relative;
    width: 100%; max-width: 420px;
    border: 1px solid; border-radius: var(--r-xl);
    padding: 22px 22px 18px;
    display: flex; gap: 14px;
    box-shadow: var(--shadow-lg);
  }
  .qc-soft-icon {
    width: 44px; height: 44px; border-radius: 12px;
    display: flex; align-items: center; justify-content: center;
    flex-shrink: 0; font-size: 20px;
  }
  .qc-soft-body { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 4px; }
  .qc-soft-title {
    font-family: var(--f-display); font-weight: 700; font-size: 15px;
    color: var(--text); margin: 0;
  }
  .qc-soft-sub { font-size: 12px; color: var(--text-sec); margin: 0; line-height: 1.5; }
  .qc-soft-actions {
    display: flex; gap: 8px; margin-top: 8px;
  }
  .qc-cta-soft {
    flex: 1; padding: 10px 14px; font-size: 12px;
  }

  /* Frames + canvas chrome */
  .qc-section-label {
    font-family: var(--f-mono); font-weight: 700; font-size: 11px;
    text-transform: uppercase; letter-spacing: 0.08em;
    color: var(--text-muted);
    margin: 32px 0 12px;
    padding-bottom: 6px; border-bottom: 1px solid var(--border-light);
  }
  .qc-section-label .step {
    color: var(--text-sec);
  }
  .qc-frame-caption {
    font-family: var(--f-mono); font-size: 11px;
    color: var(--text-muted);
    margin-bottom: 6px;
    display: flex; align-items: center; gap: 8px;
  }
  .qc-frame-caption .gherkin {
    padding: 2px 6px; border-radius: var(--r-sm);
    background: hsl(var(--c-agent) / .15); color: hsl(var(--c-agent));
    font-weight: 700; font-size: 10px;
  }

  .qc-state-grid {
    display: grid; grid-template-columns: 375px 1fr; gap: 24px;
    align-items: start;
  }
  @media (max-width: 900px) {
    .qc-state-grid { grid-template-columns: 1fr; }
  }

  .qc-phone {
    width: 375px; height: 720px;
    border-radius: 36px;
    background: var(--bg);
    border: 8px solid #1a1410;
    overflow: hidden; position: relative;
    box-shadow: var(--shadow-md);
    display: flex; flex-direction: column;
  }
  .qc-phone-sbar {
    flex-shrink: 0; height: 28px;
    display: flex; justify-content: space-between; align-items: center;
    padding: 0 18px;
    background: var(--bg);
    color: var(--text);
  }
  .qc-phone-body { flex: 1; min-height: 0; position: relative; overflow: hidden; }

  .qc-desktop {
    width: 100%; height: 720px;
    border-radius: var(--r-xl);
    background: var(--bg);
    border: 1px solid var(--border);
    overflow: hidden; position: relative;
    box-shadow: var(--shadow-md);
    display: flex; flex-direction: column;
  }
  .qc-desktop-bar {
    flex-shrink: 0;
    display: flex; align-items: center; gap: 8px;
    padding: 10px 14px;
    background: var(--bg-muted);
    border-bottom: 1px solid var(--border);
  }
  .qc-desktop-bar .traffic {
    width: 10px; height: 10px; border-radius: 50%;
    background: var(--border-strong);
  }
  .qc-desktop-bar .url {
    margin-left: 14px; padding: 4px 12px; border-radius: var(--r-pill);
    background: var(--bg); font-family: var(--f-mono); font-size: 11px;
    color: var(--text-muted);
  }
  .qc-desktop-body { flex: 1; min-height: 0; position: relative; overflow: hidden; }
`;
document.head.appendChild(qcStyleEl);

ReactDOM.createRoot(document.getElementById('root')).render(<App/>);
