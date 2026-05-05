/**
 * Register page (_content.tsx) tests — v2 AuthCard migration (Task 11).
 *
 * Verifies:
 * - Loading state while registration mode resolves
 * - Invite-only mode: renders RequestAccessForm in AuthCard, oauth_disabled banner
 * - Invite-only fallback on API failure (fail closed)
 * - Public mode: AuthCard with title + subtitle from i18n, RegisterForm, OAuth
 * - Public mode submit: calls useAuth().register, trackSignUp, redirects to /verification-pending
 * - Public mode error handling with dismiss
 * - Footer link to /login
 * - OAuth buttons respect oauthEnabled flag and call buildOAuthUrl
 */

import { act, render, screen, fireEvent, waitFor } from '@testing-library/react';
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

const getRegistrationModeMock = vi.fn();
vi.mock('@/lib/api/context', () => ({
  useApiClient: () => ({
    accessRequests: {
      getRegistrationMode: (...args: unknown[]) => getRegistrationModeMock(...args),
      requestAccess: vi.fn().mockResolvedValue({ message: 'ok' }),
    },
  }),
}));

vi.mock('@/lib/logger', () => ({
  logger: { error: vi.fn(), info: vi.fn(), warn: vi.fn() },
}));

vi.mock('@/components/auth/oauth-url', () => ({
  buildOAuthUrl: (provider: string) => `https://api.test/oauth/${provider}`,
}));

const trackSignUpMock = vi.fn();
vi.mock('@/lib/analytics/flywheel-events', () => ({
  trackSignUp: (...args: unknown[]) => trackSignUpMock(...args),
}));

// Import AFTER mocks
import { RegisterPageContent, RegisterFallback } from '../_content';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function setSearchParams(params: Record<string, string | null>) {
  searchParamsMock.get.mockImplementation((key: string) =>
    Object.prototype.hasOwnProperty.call(params, key) ? (params[key] ?? null) : null
  );
}

function resolveMode(mode: { publicRegistrationEnabled: boolean; oauthEnabled?: boolean }) {
  getRegistrationModeMock.mockResolvedValue(mode);
}

beforeEach(() => {
  vi.clearAllMocks();
  setSearchParams({});
  // default: public registration with OAuth enabled
  resolveMode({ publicRegistrationEnabled: true, oauthEnabled: true });
});

async function renderAndWait(mode?: 'public' | 'invite-only') {
  const result = render(<RegisterPageContent />);
  // Wait for async mode fetch to settle (loading indicator disappears)
  if (mode === 'public') {
    await screen.findByTestId('register-form');
  } else if (mode === 'invite-only') {
    await screen.findByTestId('request-access-form');
  }
  return result;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('RegisterPageContent (v2 AuthCard)', () => {
  describe('Loading state', () => {
    it('renders a loading indicator while registration mode is pending', () => {
      // Never-resolving promise keeps the component in loading state
      getRegistrationModeMock.mockReturnValueOnce(new Promise(() => {}));
      render(<RegisterPageContent />);
      expect(screen.getByTestId('register-loading')).toBeInTheDocument();
    });
  });

  describe('Invite-only mode', () => {
    beforeEach(() => {
      resolveMode({ publicRegistrationEnabled: false, oauthEnabled: false });
    });

    it('renders RequestAccessForm inside AuthCard with invite-only title', async () => {
      await renderAndWait('invite-only');
      expect(screen.getByTestId('request-access-form')).toBeInTheDocument();
      expect(
        screen.getByRole('heading', { name: 'auth.register.inviteOnlyTitle' })
      ).toBeInTheDocument();
      expect(screen.getByText('auth.register.inviteOnlySubtitle')).toBeInTheDocument();
    });

    it('shows inviteOauthDisabled alert when ?oauth_disabled=true', async () => {
      setSearchParams({ oauth_disabled: 'true' });
      await renderAndWait('invite-only');
      const alerts = screen.getAllByRole('alert');
      expect(alerts.some(a => a.textContent?.includes('auth.register.inviteOauthDisabled'))).toBe(
        true
      );
    });

    it('does not show inviteOauthDisabled alert when oauth_disabled is absent', async () => {
      await renderAndWait('invite-only');
      const alerts = screen.queryAllByRole('alert');
      expect(alerts.some(a => a.textContent?.includes('auth.register.inviteOauthDisabled'))).toBe(
        false
      );
    });

    it('defaults to invite-only when getRegistrationMode API call fails (fail closed)', async () => {
      getRegistrationModeMock.mockRejectedValueOnce(new Error('network down'));
      render(<RegisterPageContent />);
      await screen.findByTestId('request-access-form');
      expect(
        screen.getByRole('heading', { name: 'auth.register.inviteOnlyTitle' })
      ).toBeInTheDocument();
    });
  });

  describe('Public mode', () => {
    it('renders AuthCard with title and subtitle from i18n keys', async () => {
      await renderAndWait('public');
      expect(screen.getByRole('heading', { name: 'auth.register.title' })).toBeInTheDocument();
      expect(screen.getByText('auth.register.subtitle')).toBeInTheDocument();
    });

    it('renders the RegisterForm by default', async () => {
      await renderAndWait('public');
      expect(screen.getByTestId('register-form')).toBeInTheDocument();
    });

    it('renders footer link to /login', async () => {
      await renderAndWait('public');
      const link = screen.getByRole('link', { name: /auth\.register\.loginCta/i });
      expect(link).toHaveAttribute('href', '/login');
    });

    it('submits the register form, tracks sign-up, and redirects to /verification-pending', async () => {
      mockAuth.register.mockResolvedValueOnce({
        id: 'u1',
        email: 'new@example.com',
        role: 'User',
      });

      await renderAndWait('public');

      fireEvent.change(screen.getByLabelText(/auth\.register\.emailLabel/i), {
        target: { value: 'new@example.com' },
      });
      fireEvent.change(screen.getByLabelText(/auth\.register\.passwordLabel/i), {
        target: { value: 'password123' },
      });
      fireEvent.click(screen.getByTestId('register-terms'));
      fireEvent.submit(screen.getByTestId('register-form'));

      await waitFor(() => {
        expect(mockAuth.register).toHaveBeenCalledWith({
          email: 'new@example.com',
          password: 'password123',
        });
      });

      await waitFor(() => {
        expect(trackSignUpMock).toHaveBeenCalledWith({ method: 'email' });
      });

      await waitFor(() => {
        expect(pushMock).toHaveBeenCalledWith('/verification-pending?email=new%40example.com');
      });
    });

    it('shows an error message when registration fails', async () => {
      mockAuth.register.mockRejectedValueOnce(new Error('Email already taken'));

      await renderAndWait('public');

      fireEvent.change(screen.getByLabelText(/auth\.register\.emailLabel/i), {
        target: { value: 'dup@example.com' },
      });
      fireEvent.change(screen.getByLabelText(/auth\.register\.passwordLabel/i), {
        target: { value: 'password123' },
      });
      fireEvent.click(screen.getByTestId('register-terms'));
      fireEvent.submit(screen.getByTestId('register-form'));

      await waitFor(() => {
        expect(screen.getByText(/Email already taken/i)).toBeInTheDocument();
      });
      expect(pushMock).not.toHaveBeenCalled();
    });

    it('renders OAuth buttons when oauthEnabled flag is true', async () => {
      resolveMode({ publicRegistrationEnabled: true, oauthEnabled: true });
      await renderAndWait('public');
      expect(screen.getByRole('button', { name: /Google/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Discord/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /GitHub/i })).toBeInTheDocument();
    });

    it('hides OAuth buttons when oauthEnabled flag is false', async () => {
      resolveMode({ publicRegistrationEnabled: true, oauthEnabled: false });
      await renderAndWait('public');
      expect(screen.queryByRole('button', { name: /Google/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Discord/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /GitHub/i })).not.toBeInTheDocument();
    });

    it('clicking an OAuth button calls window.location.assign with buildOAuthUrl', async () => {
      const assignMock = vi.fn();
      const originalLocation = window.location;
      Object.defineProperty(window, 'location', {
        configurable: true,
        writable: true,
        value: { ...originalLocation, assign: assignMock },
      });

      try {
        await renderAndWait('public');
        await act(async () => {
          fireEvent.click(screen.getByRole('button', { name: /Google/i }));
        });
        expect(assignMock).toHaveBeenCalledWith('https://api.test/oauth/google');
      } finally {
        Object.defineProperty(window, 'location', {
          configurable: true,
          writable: true,
          value: originalLocation,
        });
      }
    });
  });
});

describe('RegisterFallback', () => {
  it('renders loading message', () => {
    render(<RegisterFallback />);
    expect(screen.getByText('auth.register.loadingMessage')).toBeInTheDocument();
  });
});
