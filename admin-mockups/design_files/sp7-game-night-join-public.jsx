/* ===================================================================
   SP7 Game Night — Public RSVP (sp7-game-night-join-public.jsx)
   Route: /join/event/[code]
   Persona: invitato anonimo via QR / link diretto (no login)
   Coverage: issue #1169 — public RSVP surface
   Variant of: sp7-game-night-detail-rsvp.jsx (host/invitee authenticated)

   Pattern mobile: public banner + hero (read-only) + RSVP form + footer CTA
   Pattern desktop: identical single-column centered, max-w-2xl

   Stati (6 pubblici, no host controls, no admin views):
   - state-01-pending-empty       Form pulito, nessun displayName, idle
   - state-02-pending-filled      User digita displayName 'Marco', idle
   - state-03-submitting          POST in flight, Accept button spinner
   - state-04-already-responded   200 OK con `respondedByName='Marco'`
   - state-05-token-expired       GET 410 (Expired) — terminal banner
   - state-06-rate-limited        429 con Retry-After countdown

   ⚠ Variant simplifications:
   - NO admin controls (edit, cancel, voting tools) — host-only views removed
   - NO "Invite more" / share actions
   - NO Maybe button — backend public POST validator accepts only Accepted/Declined
   - NO ConnectionBar — public route is not real-time SSE
   - Hero status badge always = entity-event (rose) for the public view; the
     route only renders if invitation.status ∈ {Pending, Accepted, Declined}.
     Cancelled/Expired → routed to error surfaces before reaching this layout.
   - "me" pill suppressed in RSVPRow (no signed-in user to match against).

   Componenti v2 riusati:
   - GameNightDetailHero (mode='public')   apps/web/src/components/features/game-night-detail/
   - PublicRsvpForm                         apps/web/src/components/features/game-night-detail/
   - InvalidTokenError                      apps/web/src/components/features/game-night-detail/error-states/
   - ExpiredOrCancelledError                apps/web/src/components/features/game-night-detail/error-states/
   - RateLimitedError                       apps/web/src/components/features/game-night-detail/error-states/
   - GenericError                           apps/web/src/components/features/game-night-detail/error-states/

   Entity color mapping:
   - hero status badge   = entityHsl('event')   rose (always)
   - RSVP confirmed body = var(--c-success) green
   - public banner       = bg-muted + text-muted-foreground
   - error banner        = bg-destructive/10 + text-destructive
   - countdown timer     = text-muted-foreground mono
   =================================================================== */

const { useState, useMemo } = React;
const DS = window.DS;

const entityHsl = (type, alpha) => {
  const c = DS.EC[type] || DS.EC.event;
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
};

// ─── FIXTURE ───────────────────────────────────────────
const INVITATION_FIXTURE = {
  token: 'tok-public-1169',
  status: 'Pending',
  hostUserId: '00000000-0000-4000-8000-000000000001',
  hostDisplayName: 'Marco R.',
  hostAvatarUrl: null,
  hostWelcomeMessage: 'Ci vediamo sabato per una serata di Wingspan e snack!',
  gameNightId: '00000000-0000-4000-8000-000000000002',
  title: 'Sabato sera con i Padovani',
  scheduledAt: '2026-05-23T20:00:00.000Z',
  location: 'Casa Marco · Padova',
  expectedPlayers: 6,
  acceptedSoFar: 3,
  primaryGameName: 'Wingspan',
  alreadyRespondedAs: null,
  respondedByName: null,
};

// ─── STATE FIXTURES ────────────────────────────────────
const STATES = [
  { id: 'state-01-pending-empty', label: 'Pending — empty form' },
  { id: 'state-02-pending-filled', label: 'Pending — filled displayName' },
  { id: 'state-03-submitting', label: 'Submitting' },
  { id: 'state-04-already-responded', label: 'Already responded as "Marco"' },
  { id: 'state-05-token-expired', label: '410 Gone (expired)' },
  { id: 'state-06-rate-limited', label: '429 Rate-limited' },
];

// ─── COMPONENTS ────────────────────────────────────────

function PublicBanner({ message }) {
  return (
    <div
      role="note"
      style={{
        padding: '8px 12px',
        borderRadius: 6,
        background: 'var(--bg-muted)',
        color: 'var(--text-muted)',
        fontSize: 12,
        border: '1px solid var(--border-light)',
      }}
    >
      {message}
    </div>
  );
}

function Hero({ invitation }) {
  return (
    <header
      data-mode="public"
      style={{
        position: 'relative',
        overflow: 'hidden',
        padding: '20px 20px 24px',
        borderBottom: '1px solid var(--border-light)',
        background: `linear-gradient(135deg, ${entityHsl('event', 0.16)}, ${entityHsl('event', 0.02)})`,
      }}
    >
      <span
        aria-hidden
        style={{
          position: 'absolute',
          top: -24,
          right: -24,
          width: 112,
          height: 112,
          borderRadius: '50%',
          background: entityHsl('event', 0.25),
          filter: 'blur(20px)',
        }}
      />
      <h1
        style={{
          margin: '8px 0 4px',
          fontFamily: 'var(--font-display)',
          fontSize: 22,
          fontWeight: 800,
          color: 'var(--text-primary)',
        }}
      >
        {invitation.title}
      </h1>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6, fontSize: 12, color: 'var(--text-muted)', fontFamily: 'monospace', fontWeight: 700 }}>
        <div>📅 {new Date(invitation.scheduledAt).toLocaleString('it-IT', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })}</div>
        <div>📍 {invitation.location}</div>
      </div>
      <div style={{ marginTop: 12, paddingTop: 12, borderTop: '1px solid var(--border-light)', display: 'flex', alignItems: 'center', gap: 10 }}>
        <div style={{ width: 28, height: 28, borderRadius: '50%', background: entityHsl('event'), color: 'white', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 800, fontSize: 11 }}>
          {invitation.hostDisplayName.split(' ').map(s => s[0]).join('').slice(0, 2)}
        </div>
        <div style={{ fontSize: 12, fontWeight: 800 }}>{invitation.hostDisplayName} ti ha invitato a giocare</div>
      </div>
    </header>
  );
}

function PublicRsvpFormSurface({ state, invitation }) {
  const [name, setName] = useState(state === 'state-02-pending-filled' ? 'Marco' : '');
  const submitting = state === 'state-03-submitting';
  const responded = state === 'state-04-already-responded';

  if (responded) {
    return (
      <section style={{ padding: 16, borderRadius: 8, border: '1px solid var(--border-light)', background: 'var(--bg-card)' }}>
        <h2 style={{ margin: 0, fontFamily: 'var(--font-display)', fontWeight: 800, fontSize: 15 }}>Hai già risposto</h2>
        <p style={{ margin: '6px 0 12px', color: 'var(--text-muted)', fontSize: 13 }}>Hai confermato come "Marco".</p>
        <button style={{ padding: '8px 14px', border: '1px solid var(--border-medium)', borderRadius: 6, background: 'var(--bg-muted)', cursor: 'pointer', fontWeight: 600, fontSize: 13 }}>Cambia risposta</button>
      </section>
    );
  }

  return (
    <section style={{ padding: 16, borderRadius: 8, border: '1px solid var(--border-light)', background: 'var(--bg-card)' }}>
      <h2 style={{ margin: 0, fontFamily: 'var(--font-display)', fontWeight: 800, fontSize: 15 }}>La tua risposta</h2>

      <div style={{ marginTop: 14 }}>
        <label htmlFor="dn" style={{ display: 'block', fontSize: 11, fontWeight: 800, textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--text-muted)', marginBottom: 6 }}>
          Il tuo nome (opzionale)
        </label>
        <input
          id="dn"
          type="text"
          value={name}
          onChange={e => setName(e.target.value)}
          maxLength={120}
          placeholder="Es. Marco"
          style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--border-medium)', fontSize: 14 }}
        />
        <p style={{ margin: '6px 0 0', fontSize: 11, color: 'var(--text-muted)' }}>Aiuta l'organizzatore a riconoscerti. Massimo 120 caratteri.</p>
      </div>

      <div style={{ display: 'flex', gap: 10, marginTop: 14 }}>
        <button
          disabled={submitting}
          style={{
            flex: 1,
            padding: '10px 16px',
            borderRadius: 6,
            border: 'none',
            background: 'var(--c-success)',
            color: 'white',
            cursor: submitting ? 'wait' : 'pointer',
            fontWeight: 700,
            fontSize: 14,
            opacity: submitting ? 0.7 : 1,
          }}
        >
          {submitting ? 'Invio risposta…' : 'Confermo'}
        </button>
        <button
          disabled={submitting}
          style={{
            flex: 1,
            padding: '10px 16px',
            borderRadius: 6,
            border: '1px solid var(--border-medium)',
            background: 'var(--bg-card)',
            cursor: submitting ? 'wait' : 'pointer',
            fontWeight: 600,
            fontSize: 14,
            opacity: submitting ? 0.7 : 1,
          }}
        >
          Non posso
        </button>
      </div>
    </section>
  );
}

function ErrorSurface({ state }) {
  if (state === 'state-05-token-expired') {
    return (
      <section role="alert" style={{ padding: 32, textAlign: 'center', background: 'var(--bg-card)', borderRadius: 12, border: '1px solid var(--border-light)' }}>
        <div style={{ fontSize: 32, marginBottom: 12 }}>⌛</div>
        <h1 style={{ margin: '0 0 6px', fontSize: 18, fontWeight: 800 }}>Invito scaduto</h1>
        <p style={{ margin: '0 0 16px', color: 'var(--text-muted)', fontSize: 13 }}>Questo invito non è più valido. Chiedi all'organizzatore di rimandartelo.</p>
        <button style={{ padding: '10px 16px', borderRadius: 6, background: 'var(--c-toolkit)', color: 'white', border: 'none', fontWeight: 700 }}>Richiedi un nuovo invito</button>
      </section>
    );
  }
  if (state === 'state-06-rate-limited') {
    return (
      <section role="alert" style={{ padding: 32, textAlign: 'center', background: 'var(--bg-card)', borderRadius: 12, border: '1px solid var(--border-light)' }}>
        <div style={{ fontSize: 32, marginBottom: 12 }}>⏱️</div>
        <h1 style={{ margin: '0 0 6px', fontSize: 18, fontWeight: 800 }}>Troppe richieste</h1>
        <p style={{ margin: '0 0 16px', color: 'var(--text-muted)', fontSize: 13 }}>Hai inviato troppe risposte in poco tempo. Attendi qualche secondo prima di riprovare.</p>
        <p style={{ margin: 0, fontFamily: 'monospace', fontSize: 14, fontWeight: 700, color: 'var(--text-muted)' }}>Riprova tra 12 secondi</p>
      </section>
    );
  }
  return null;
}

function StateView({ state }) {
  const isError = state === 'state-05-token-expired' || state === 'state-06-rate-limited';

  return (
    <main
      data-surface={state}
      style={{
        minHeight: '100vh',
        background: 'var(--bg-app)',
        padding: '24px 16px',
      }}
    >
      <div style={{ maxWidth: 640, margin: '0 auto', display: 'flex', flexDirection: 'column', gap: 16 }}>
        <PublicBanner message="Stai vedendo un invito pubblico. La tua risposta verrà condivisa con l'organizzatore." />

        {isError ? (
          <ErrorSurface state={state} />
        ) : (
          <article style={{ overflow: 'hidden', borderRadius: 16, border: '1px solid var(--border-light)', background: 'var(--bg-card)' }}>
            <Hero invitation={INVITATION_FIXTURE} />
            <div style={{ padding: 16 }}>
              <PublicRsvpFormSurface state={state} invitation={INVITATION_FIXTURE} />
            </div>
            {state === 'state-04-already-responded' ? (
              <aside style={{ borderTop: '1px solid var(--border-light)', background: 'var(--bg-muted)', padding: 16 }}>
                <h2 style={{ margin: 0, fontWeight: 800, fontSize: 13 }}>Vuoi tenere traccia delle tue serate?</h2>
                <p style={{ margin: '4px 0 10px', fontSize: 11, color: 'var(--text-muted)' }}>Crea un account gratuito per gestire i tuoi inviti, vedere le statistiche e ricevere promemoria.</p>
                <button style={{ padding: '8px 14px', borderRadius: 6, background: 'var(--c-toolkit)', color: 'white', border: 'none', fontWeight: 700, fontSize: 12 }}>Crea account</button>
              </aside>
            ) : null}
          </article>
        )}
      </div>
    </main>
  );
}

// ─── ROOT ──────────────────────────────────────────────
function Root() {
  const [active, setActive] = useState(STATES[0].id);

  return (
    <div>
      <nav style={{ position: 'sticky', top: 0, zIndex: 10, background: 'var(--bg-app)', borderBottom: '1px solid var(--border-light)', padding: 12, display: 'flex', gap: 8, overflowX: 'auto' }}>
        {STATES.map(s => (
          <button
            key={s.id}
            onClick={() => setActive(s.id)}
            style={{
              padding: '6px 12px',
              borderRadius: 6,
              border: '1px solid var(--border-medium)',
              background: active === s.id ? entityHsl('event') : 'var(--bg-card)',
              color: active === s.id ? 'white' : 'var(--text-primary)',
              cursor: 'pointer',
              fontSize: 12,
              whiteSpace: 'nowrap',
              fontWeight: 600,
            }}
          >
            {s.label}
          </button>
        ))}
      </nav>
      <StateView state={active} />
    </div>
  );
}

ReactDOM.render(<Root />, document.getElementById('root'));
