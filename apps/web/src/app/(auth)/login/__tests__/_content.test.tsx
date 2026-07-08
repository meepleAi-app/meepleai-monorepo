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

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

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

const { mockAuth } = vi.hoisted(() => ({
  mockAuth: {
    register: vi.fn(),
    loadCurrentUser: vi.fn().mockResolvedValue(undefined),
    clearError: vi.fn(),
  },
}));
vi.mock('@/hooks/useAuth', () => ({
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
