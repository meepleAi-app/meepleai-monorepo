/**
 * Login page (_content.tsx) tests — v2 AuthCard migration (Task 10).
 *
 * Verifies:
 * - AuthCard with title from i18n
 * - LoginForm submit calls `api.auth.login` and redirects to ?from on success
 * - 2FA challenge: when API returns requiresTwoFactor, renders TwoFactorVerification
 * - OAuth buttons call window.location.assign with buildOAuthUrl(provider)
 * - Session-expired banner visible when ?reason=session_expired
 * - Forgot password link → /reset-password
 * - Footer register link → /register
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
const searchParamsMock = { get: vi.fn<(key: string) => string | null>() };

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock, refresh: refreshMock }),
  useSearchParams: () => searchParamsMock,
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
import { LoginPageContent, LoginFallback } from '../_content';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function setSearchParams(params: Record<string, string | null>) {
  searchParamsMock.get.mockImplementation((key: string) =>
    Object.prototype.hasOwnProperty.call(params, key) ? (params[key] ?? null) : null
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  setSearchParams({});
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

  it('shows session-expired banner when ?reason=session_expired', () => {
    setSearchParams({ reason: 'session_expired' });
    render(<LoginPageContent />);
    // Banner uses role="alert"
    const alerts = screen.getAllByRole('alert');
    expect(alerts.length).toBeGreaterThan(0);
    // Should contain a localized expired message key — we look for presence of an alert
    // with the session key OR textual fallback
    expect(alerts.some(a => /session|expired|scaduta/i.test(a.textContent ?? ''))).toBe(true);
  });

  it('does not show session-expired banner when reason param is absent', () => {
    render(<LoginPageContent />);
    // no alert containing the session-expired key should exist
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

  it('submits the login form and redirects to ?from on success', async () => {
    setSearchParams({ from: '/library/custom' });
    loginMock.mockResolvedValueOnce({
      user: { id: 'u1', email: 't@e.com', role: 'User' },
      requiresTwoFactor: false,
    });

    render(<LoginPageContent />);

    fireEvent.change(screen.getByLabelText(/auth\.login\.emailLabel/i), {
      target: { value: 'user@example.com' },
    });
    fireEvent.change(screen.getByLabelText(/auth\.login\.passwordLabel/i), {
      target: { value: 'password123' },
    });
    fireEvent.submit(screen.getByTestId('login-form'));

    await waitFor(() => {
      expect(loginMock).toHaveBeenCalledWith({
        email: 'user@example.com',
        password: 'password123',
      });
    });

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith('/library/custom');
    });
  });

  it('redirects to /library when ?from is not provided', async () => {
    loginMock.mockResolvedValueOnce({
      user: { id: 'u1', email: 't@e.com', role: 'User' },
      requiresTwoFactor: false,
    });

    render(<LoginPageContent />);

    fireEvent.change(screen.getByLabelText(/auth\.login\.emailLabel/i), {
      target: { value: 'user@example.com' },
    });
    fireEvent.change(screen.getByLabelText(/auth\.login\.passwordLabel/i), {
      target: { value: 'password123' },
    });
    fireEvent.submit(screen.getByTestId('login-form'));

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

    fireEvent.change(screen.getByLabelText(/auth\.login\.emailLabel/i), {
      target: { value: 'user@example.com' },
    });
    fireEvent.change(screen.getByLabelText(/auth\.login\.passwordLabel/i), {
      target: { value: 'password123' },
    });
    fireEvent.submit(screen.getByTestId('login-form'));

    await waitFor(() => {
      expect(screen.getByTestId('two-factor-verification')).toBeInTheDocument();
    });
    // Should NOT have redirected
    expect(pushMock).not.toHaveBeenCalled();
  });

  it('redirects admins to /admin after successful login', async () => {
    loginMock.mockResolvedValueOnce({
      user: { id: 'u2', email: 'a@e.com', role: 'Admin' },
      requiresTwoFactor: false,
    });

    render(<LoginPageContent />);

    fireEvent.change(screen.getByLabelText(/auth\.login\.emailLabel/i), {
      target: { value: 'admin@example.com' },
    });
    fireEvent.change(screen.getByLabelText(/auth\.login\.passwordLabel/i), {
      target: { value: 'password123' },
    });
    fireEvent.submit(screen.getByTestId('login-form'));

    await waitFor(() => {
      expect(pushMock).toHaveBeenCalledWith('/admin');
    });
  });
});

describe('LoginFallback', () => {
  it('renders loading message', () => {
    render(<LoginFallback />);
    expect(screen.getByText('auth.login.loadingMessage')).toBeInTheDocument();
  });
});
