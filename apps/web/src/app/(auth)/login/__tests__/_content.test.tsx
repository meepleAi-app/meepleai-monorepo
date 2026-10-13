/**
 * Login page (_content.tsx) tests — v2 AuthCard migration (Task 10).
 *
 * #2650 — LoginPageContent now receives `fromParam`/`reason` as PROPS from the
 * Server Component page.tsx (which reads searchParams). This removes the
 * `useSearchParams` client hook, so the email form renders server-side (SSR)
 * instead of behind a <Suspense> fallback — fixing the flaky staging E2E
 * "Auth login form visibile" (hydration >30s on the headless VPS runner).
 *
 * Verifies:
 * - AuthCard with title from i18n
 * - LoginForm submit calls `api.auth.login` and redirects to fromParam on success
 * - 2FA challenge: when API returns requiresTwoFactor, renders TwoFactorVerification
 * - OAuth buttons call window.location.assign with buildOAuthUrl(provider)
 * - Session-expired banner visible when reason="session_expired"
 * - Forgot password link → /reset-password
 * - Footer register link → /register
 * - open-redirect protection on fromParam (#2168)
 */

import { useEffect, useState } from 'react';

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// ---------------------------------------------------------------------------
// Mocks (must be registered before component import)
// ---------------------------------------------------------------------------

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

const pushMock = vi.fn().mockResolvedValue(undefined);
const refreshMock = vi.fn();

// #2650: LoginPageContent no longer calls useSearchParams — it reads from props.
// Only useRouter is still used.
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock, refresh: refreshMock }),
}));

// #4190: il form usa AuthProvider, non più l'hook omonimo di @/hooks/useAuth. Il percorso con il
// vero AuthProvider è in _content.auth-provider.test.tsx.
const { mockAuth } = vi.hoisted(() => ({
  mockAuth: {
    refreshUser: vi.fn().mockResolvedValue(undefined),
    clearError: vi.fn(),
  },
}));
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => mockAuth,
}));

const loginMock = vi.fn();
const verify2FAMock = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      login: (...args: unknown[]) => loginMock(...args),
      verify2FALogin: (...args: unknown[]) => verify2FAMock(...args),
    },
  },
}));

vi.mock('@/lib/logger', () => ({
  logger: { error: vi.fn(), info: vi.fn(), warn: vi.fn() },
}));

vi.mock('@/components/auth/oauth-url', () => ({
  buildOAuthUrl: (provider: string) => `https://api.test/oauth/${provider}`,
}));

// Import AFTER mocks
import { LoginPageContent } from '../_content';
import { logger } from '@/lib/logger';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function fillAndSubmit() {
  fireEvent.change(screen.getByLabelText(/auth\.login\.emailLabel/i), {
    target: { value: 'user@example.com' },
  });
  fireEvent.change(screen.getByLabelText(/auth\.login\.passwordLabel/i), {
    target: { value: 'StrongPassword1' },
  });
  fireEvent.submit(screen.getByTestId('login-form'));
}

beforeEach(() => {
  vi.clearAllMocks();
});

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('LoginPageContent (v2 AuthCard)', () => {
  it('renders AuthCard with title and subtitle from i18n keys', () => {
    render(<LoginPageContent />);
    // With (k)=>k translation, these keys should appear verbatim
    expect(screen.getByRole('heading', { name: 'auth.login.title' })).toBeInTheDocument();
    expect(screen.getByText('auth.login.subtitle')).toBeInTheDocument();
  });

  it('renders the LoginForm by default', () => {
    render(<LoginPageContent />);
    expect(screen.getByTestId('login-form')).toBeInTheDocument();
  });

  it('renders OAuth buttons for google, discord and github', () => {
    render(<LoginPageContent />);
    // OAuthButton uses default Italian labels from component
    expect(screen.getByRole('button', { name: /Google/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Discord/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /GitHub/i })).toBeInTheDocument();
  });

  it('clicking an OAuth button calls window.location.assign with buildOAuthUrl', () => {
    const assignMock = vi.fn();
    const originalLocation = window.location;
    Object.defineProperty(window, 'location', {
      configurable: true,
      writable: true,
      value: { ...originalLocation, assign: assignMock },
    });

    try {
      render(<LoginPageContent />);
      fireEvent.click(screen.getByRole('button', { name: /Google/i }));
      expect(assignMock).toHaveBeenCalledWith('https://api.test/oauth/google');
    } finally {
      Object.defineProperty(window, 'location', {
        configurable: true,
        writable: true,
        value: originalLocation,
      });
    }
  });

  it('shows session-expired banner when reason="session_expired"', () => {
    render(<LoginPageContent reason="session_expired" />);
    // Banner uses role="alert"
    const alerts = screen.getAllByRole('alert');
    expect(alerts.length).toBeGreaterThan(0);
    expect(alerts.some(a => /session|expired|scaduta/i.test(a.textContent ?? ''))).toBe(true);
  });

  it('does not show session-expired banner when reason prop is absent', () => {
    render(<LoginPageContent />);
    const alerts = screen.queryAllByRole('alert');
    expect(alerts.some(a => /session.*expired|session_expired/i.test(a.textContent ?? ''))).toBe(
      false
    );
  });

  it('has a Forgot password link to /reset-password', () => {
    render(<LoginPageContent />);
    const link = screen.getByRole('link', { name: /forgotPassword|Forgot|dimenticata/i });
    expect(link).toHaveAttribute('href', '/reset-password');
  });

  it('has a footer link to /register', () => {
    render(<LoginPageContent />);
    const link = screen.getByRole('link', { name: /registerCta|register|Registrati|Sign up/i });
    expect(link).toHaveAttribute('href', '/register');
  });

  it('submits the login form and redirects to fromParam on success', async () => {
    loginMock.mockResolvedValueOnce({
      user: { id: 'u1', email: 't@e.com', role: 'User' },
      requiresTwoFactor: false,
    });

    render(<LoginPageContent fromParam="/library/custom" />);
    fillAndSubmit();

    await waitFor(() => {
      expect(loginMock).toHaveBeenCalledWith({
        email: 'user@example.com',
        password: 'StrongPassword1',
      });
    });

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith('/library/custom');
    });
  });

  it('redirects to /library when fromParam is not provided', async () => {
    loginMock.mockResolvedValueOnce({
      user: { id: 'u1', email: 't@e.com', role: 'User' },
      requiresTwoFactor: false,
    });

    render(<LoginPageContent />);
    fillAndSubmit();

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith('/library');
    });
  });

  it('shows TwoFactorVerification when API returns requiresTwoFactor', async () => {
    loginMock.mockResolvedValueOnce({
      user: null,
      requiresTwoFactor: true,
      tempSessionToken: 'temp-token-abc',
    });

    render(<LoginPageContent />);
    fillAndSubmit();

    await waitFor(() => {
      expect(screen.getByTestId('two-factor-verification')).toBeInTheDocument();
    });
    // Should NOT have redirected
    expect(pushMock).not.toHaveBeenCalled();
  });

  it('redirects admins to /library (user app) after successful login', async () => {
    // Issue #893 demo follow-up: admins/superadmins land on the user app by default.
    loginMock.mockResolvedValueOnce({
      user: { id: 'u2', email: 'a@e.com', role: 'Admin' },
      requiresTwoFactor: false,
    });

    render(<LoginPageContent />);
    fillAndSubmit();

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith('/library');
    });
  });
});

describe('LoginPageContent — open redirect protection (#2168)', () => {
  it('falls back to /library when fromParam is external URL', async () => {
    loginMock.mockResolvedValueOnce({
      user: { id: 'u1', email: 't@e.com', role: 'User' },
      requiresTwoFactor: false,
    });

    render(<LoginPageContent fromParam="https://evil.com" />);
    fillAndSubmit();

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith('/library');
    });
    expect(pushMock).not.toHaveBeenCalledWith('https://evil.com');

    // Unsafe input must trigger a warn log
    expect(logger.warn).toHaveBeenCalledWith(
      'Rejected unsafe ?from= redirect target on login',
      expect.objectContaining({
        metadata: expect.objectContaining({ fromMasked: expect.any(String) }),
      })
    );
  });

  it('preserves valid relative fromParam path', async () => {
    loginMock.mockResolvedValueOnce({
      user: { id: 'u1', email: 't@e.com', role: 'User' },
      requiresTwoFactor: false,
    });

    render(<LoginPageContent fromParam="/sessions/abc-123" />);
    fillAndSubmit();

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith('/sessions/abc-123');
    });

    // Safe input must NOT trigger a warn log
    expect(logger.warn).not.toHaveBeenCalled();
  });

  it('logs unsafe fromParam attempt on initial render (before any user action)', async () => {
    render(<LoginPageContent fromParam="https://evil.com" />);

    // useEffect fires after render commit — wait for it
    await waitFor(() => {
      expect(logger.warn).toHaveBeenCalledWith(
        'Rejected unsafe ?from= redirect target on login',
        expect.objectContaining({
          metadata: expect.objectContaining({ fromMasked: expect.any(String) }),
        })
      );
    });

    // No form submission — warn fires on mount only, not tied to login action
    expect(loginMock).not.toHaveBeenCalled();
    expect(pushMock).not.toHaveBeenCalled();
  });
});

describe('LoginPageContent — error logging dedup (#2171)', () => {
  it('does NOT call logger.error when login API call rejects', async () => {
    loginMock.mockRejectedValueOnce(new Error('Invalid email or password'));

    render(<LoginPageContent />);
    fillAndSubmit();

    // Wait until error path settles (setError → re-render)
    await waitFor(() => {
      expect(screen.getByText('Invalid email or password')).toBeInTheDocument();
    });

    expect(logger.error).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// #4134 — il login si conclude a pagina smontata
// ---------------------------------------------------------------------------

/**
 * Un login riuscito naviga via: quando `router.push` si risolve la pagina non c'è più, e il
 * `finally` dei due gestori chiamava comunque `setIsAuthenticating(false)`. Per dare una priorità
 * a quell'aggiornamento React legge `window.event`; se nel frattempo jsdom è stato smantellato
 * esce «ReferenceError: window is not defined» come rifiuto non gestito, con zero test falliti
 * (shard 3/4 della PR #4293, da `_content.auth-provider.test.tsx`).
 *
 * Sotto lo smantellamento non è riproducibile in isolamento. Quello che si può asserire è il
 * contratto: a pagina smontata il codice dell'app non chiede nessun aggiornamento.
 *
 * ⚠️ «Nessuna lettura di `window.event`» NON è quel contratto, e la prima stesura asseriva
 * quello. React legge `window.event` anche per conto suo: per esempio nel compito che svuota gli
 * effetti passivi, che un commit avvenuto a pagina ancora MONTATA accoda nello scheduler. Se lo
 * scheduler cede il passo fra il commit e quel compito, il compito gira dopo lo smontaggio e il
 * conteggio sale senza che nessuno abbia aggiornato nulla (shard 1/4 della PR #4304: rosso su un
 * diff che il login non lo toccava). Per questo si contano solo le letture con il codice dell'app
 * nello stack, e i primi due test tengono onesto il rilevatore in entrambe le direzioni.
 */
describe('LoginPageContent — il login si conclude a pagina smontata (#4134)', () => {
  const USER = { id: 'u1', email: 'user@example.com', role: 'User' };

  let originalWindowEvent: PropertyDescriptor | undefined;

  /** Un frame dello stack che viene dal codice sotto `src/`, escluso il getter qui sotto. */
  const isAppFrame = (frame: string) =>
    /[\\/]src[\\/]/.test(frame) &&
    !frame.includes('node_modules') &&
    !frame.includes('readWindowEvent');

  /**
   * Sostituisce `window.event` con un getter che registra lo stack di chi lo legge.
   *
   * Un `setState` chiamato da un componente ha quel componente nello stack. Le letture che React
   * fa per conto suo hanno solo React e lo scheduler.
   */
  function watchReadsOfWindowEvent() {
    const stacks: string[] = [];
    originalWindowEvent = Object.getOwnPropertyDescriptor(window, 'event');
    Object.defineProperty(window, 'event', {
      configurable: true,
      get: function readWindowEvent() {
        // I dieci frame di default oggi bastano. Un wrapper in più non deve nascondere l'app.
        const limit = Error.stackTraceLimit;
        Error.stackTraceLimit = 50;
        stacks.push(new Error('window.event').stack ?? '');
        Error.stackTraceLimit = limit;
        return undefined;
      },
    });
    return {
      /** Tutte le letture, di chiunque. */
      all: () => stacks,
      /** Le letture fatte per un aggiornamento che il codice dell'app ha chiesto. */
      requestedByTheApp: () => stacks.filter(stack => stack.split('\n').some(isAppFrame)),
    };
  }

  /** Una navigazione che resta in volo finché il test non la conclude. */
  function navigationInFlight() {
    let finish!: () => void;
    pushMock.mockImplementationOnce(
      () =>
        new Promise<void>(resolve => {
          finish = resolve;
        })
    );
    return () => finish();
  }

  const settle = () => new Promise(resolve => setTimeout(resolve, 0));

  afterEach(() => {
    // `window.event` torna com'era: una proprietà sua, se lo era, oppure quella ereditata.
    delete (window as unknown as { event?: unknown }).event;
    if (originalWindowEvent) {
      Object.defineProperty(window, 'event', originalWindowEvent);
      originalWindowEvent = undefined;
    }
    // Una risposta che un test rosso non ha consumato non deve arrivare al test dopo.
    verify2FAMock.mockReset();
  });

  /**
   * Il rilevatore, prima direzione: un aggiornamento chiesto dopo lo smontaggio DEVE comparire.
   * Il giorno in cui React smette di leggere `window.event` per un `setState`, o lo stack cambia
   * forma, è questo test a dirlo. Senza, i due sul login passerebbero per niente.
   */
  it('sees an update the app requests after unmount', () => {
    const late = { requestUpdate: () => {} };
    function UpdatesLate() {
      const [, setValue] = useState(0);
      useEffect(() => {
        late.requestUpdate = () => setValue(1);
      }, []);
      return null;
    }
    const { unmount } = render(<UpdatesLate />);
    unmount();
    const reads = watchReadsOfWindowEvent();

    late.requestUpdate();

    expect(reads.requestedByTheApp()).toHaveLength(1);
  });

  /**
   * Il rilevatore, seconda direzione: una lettura che nessuno nell'app ha chiesto NON conta.
   * È la forma del rosso della PR #4304. Qui la lettura la fa un timer che chiama `Reflect.get`,
   * nativo: nello stack non c'è nessun file dell'app, come quando a leggere è lo scheduler.
   */
  it('does not count a read nobody in the app asked for', async () => {
    const reads = watchReadsOfWindowEvent();

    setTimeout(Reflect.get, 0, window, 'event');
    await settle();

    expect(reads.all()).toHaveLength(1);
    expect(reads.requestedByTheApp()).toEqual([]);
  });

  it('does not schedule a React update when the password login finishes after unmount', async () => {
    const finishNavigation = navigationInFlight();
    loginMock.mockResolvedValueOnce({ user: USER, requiresTwoFactor: false });

    const { unmount } = render(<LoginPageContent />);
    fillAndSubmit();
    await waitFor(() => expect(pushMock).toHaveBeenCalled());

    unmount();
    const reads = watchReadsOfWindowEvent();
    finishNavigation();
    await settle();

    expect(reads.requestedByTheApp()).toEqual([]);
  });

  it('does not schedule a React update when the 2FA login finishes after unmount', async () => {
    const finishNavigation = navigationInFlight();
    loginMock.mockResolvedValueOnce({
      user: null,
      requiresTwoFactor: true,
      tempSessionToken: 'temp-token-abc',
    });
    verify2FAMock.mockResolvedValueOnce(USER);

    const { unmount } = render(<LoginPageContent />);
    fillAndSubmit();
    // Sei cifre bastano: il campo invia da solo. Un `fireEvent.submit` in più avvia una SECONDA
    // verifica, che finisce a pagina montata proprio mentre il test smonta.
    fireEvent.change(await screen.findByLabelText(/code|codice/i), {
      target: { value: '123456' },
    });
    await waitFor(() => expect(pushMock).toHaveBeenCalled());
    expect(verify2FAMock).toHaveBeenCalledTimes(1);

    unmount();
    const reads = watchReadsOfWindowEvent();
    finishNavigation();
    await settle();

    expect(reads.requestedByTheApp()).toEqual([]);
  });

  /**
   * L'altra direzione: a pagina montata il form deve uscire dallo stato di attesa. Senza questa
   * asserzione una guardia sbagliata lascerebbe il form bloccato dopo un login fallito, e nessun
   * test se ne accorgerebbe.
   */
  it('still leaves the loading state when the login fails and the page is mounted', async () => {
    loginMock.mockRejectedValueOnce(new Error('Credenziali non valide'));

    render(<LoginPageContent />);
    fillAndSubmit();

    expect(await screen.findByText('Credenziali non valide')).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByLabelText(/auth\.login\.emailLabel/i)).not.toBeDisabled()
    );
  });

  it('still leaves the loading state when the 2FA verification fails and the page is mounted', async () => {
    loginMock.mockResolvedValueOnce({
      user: null,
      requiresTwoFactor: true,
      tempSessionToken: 'temp-token-abc',
    });
    verify2FAMock.mockRejectedValueOnce(new Error('Codice non valido'));

    render(<LoginPageContent />);
    fillAndSubmit();
    // Sei cifre bastano: il campo invia da solo.
    fireEvent.change(await screen.findByLabelText(/code|codice/i), {
      target: { value: '123456' },
    });

    expect(await screen.findByText('Codice non valido')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText(/code|codice/i)).not.toBeDisabled());
    expect(verify2FAMock).toHaveBeenCalledTimes(1);
  });
});
