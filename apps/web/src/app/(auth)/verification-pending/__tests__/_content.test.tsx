/**
 * Verification-pending page (_content.tsx) tests — v2 AuthCard migration (Task 12).
 *
 * Verifies:
 * - AuthCard title from `auth.emailVerification.pending.title`
 * - Email read from ?email query param
 * - Email read from sessionStorage fallback
 * - No-email branch renders redirect CTA to /register
 * - Resend button calls useEmailVerification.resendVerificationEmail(email)
 * - Cooldown > 0: button disabled; shows cooldown message
 * - Cooldown 0: button enabled
 * - Error from hook rendered in alert
 * - Email is masked (u***r@example.com for user@example.com)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// ---------------------------------------------------------------------------
// Mocks (must be registered before component import)
// ---------------------------------------------------------------------------

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, values?: Record<string, unknown>) => {
      if (!values) return key;
      const pairs = Object.entries(values)
        .map(([k, v]) => `${k}=${String(v)}`)
        .join(',');
      return `${key}:${pairs}`;
    },
  }),
}));

const pushMock = vi.fn().mockResolvedValue(undefined);

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock }),
}));

// Controllable hook state
const hookState = {
  isResending: false,
  error: null as string | null,
  cooldownSeconds: 0,
};
const resendMock = vi.fn().mockResolvedValue(true);
const clearErrorMock = vi.fn();

vi.mock('@/hooks/useEmailVerification', () => ({
  useEmailVerification: () => ({
    isLoading: false,
    isResending: hookState.isResending,
    error: hookState.error,
    errorType: null,
    isVerified: false,
    cooldownSeconds: hookState.cooldownSeconds,
    verifyEmail: vi.fn(),
    resendVerificationEmail: resendMock,
    clearError: clearErrorMock,
  }),
}));

// Import AFTER mocks
import { VerificationPendingContent } from '../_content';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

// #2773: ?email= now flows as the `emailParam` prop from the async page.tsx.
// This helper stores the value; renderContent() forwards it to the component.
let emailParamValue: string | null = null;
function setSearchParams(params: Record<string, string | null>) {
  emailParamValue = params.email ?? null;
}

function renderContent() {
  return render(<VerificationPendingContent emailParam={emailParamValue} />);
}

function setHookState(partial: Partial<typeof hookState>) {
  Object.assign(hookState, partial);
}

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
  setSearchParams({});
  hookState.isResending = false;
  hookState.error = null;
  hookState.cooldownSeconds = 0;
});

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('VerificationPendingContent (v2 AuthCard)', () => {
  describe('Email resolution', () => {
    it('renders with email from ?email query param', () => {
      setSearchParams({ email: 'user@example.com' });
      renderContent();
      // Card title rendered verbatim (t returns key)
      expect(
        screen.getByRole('heading', { name: 'auth.emailVerification.pending.title' })
      ).toBeInTheDocument();
      // Masked email appears
      expect(screen.getByText(/u\*\*\*r@example\.com/)).toBeInTheDocument();
    });

    it('persists query-param email into sessionStorage', () => {
      setSearchParams({ email: 'user@example.com' });
      renderContent();
      expect(sessionStorage.getItem('pendingVerificationEmail')).toBe('user@example.com');
    });

    it('falls back to sessionStorage when no query param', () => {
      sessionStorage.setItem('pendingVerificationEmail', 'stored@example.com');
      setSearchParams({});
      renderContent();
      expect(screen.getByText(/s\*\*\*d@example\.com/)).toBeInTheDocument();
    });

    it('renders no-email redirect CTA when neither source has email', () => {
      setSearchParams({});
      renderContent();
      expect(screen.getByText('auth.emailVerification.pending.noEmail')).toBeInTheDocument();
      const cta = screen.getByRole('button', {
        name: 'auth.emailVerification.pending.goToRegister',
      });
      expect(cta).toBeInTheDocument();
    });

    it('no-email CTA navigates to /register on click', () => {
      setSearchParams({});
      renderContent();
      const cta = screen.getByRole('button', {
        name: 'auth.emailVerification.pending.goToRegister',
      });
      fireEvent.click(cta);
      expect(pushMock).toHaveBeenCalledWith('/register');
    });
  });

  describe('Resend flow', () => {
    function getResendButton(): HTMLButtonElement {
      return screen.getByTestId('resend-verification-button') as HTMLButtonElement;
    }

    it('calls resendVerificationEmail(email) when resend button clicked', () => {
      setSearchParams({ email: 'user@example.com' });
      renderContent();
      const button = getResendButton();
      fireEvent.click(button);
      expect(resendMock).toHaveBeenCalledWith('user@example.com');
    });

    it('disables resend button when cooldownSeconds > 0', () => {
      setSearchParams({ email: 'user@example.com' });
      setHookState({ cooldownSeconds: 30 });
      renderContent();
      expect(getResendButton()).toBeDisabled();
    });

    it('shows cooldown message when cooldownSeconds > 0', () => {
      setSearchParams({ email: 'user@example.com' });
      setHookState({ cooldownSeconds: 30 });
      renderContent();
      expect(
        screen.getByText(/auth\.emailVerification\.pending\.cooldownMessage/)
      ).toBeInTheDocument();
    });

    it('enables resend button when cooldownSeconds is 0 and not resending', () => {
      setSearchParams({ email: 'user@example.com' });
      setHookState({ cooldownSeconds: 0, isResending: false });
      renderContent();
      expect(getResendButton()).not.toBeDisabled();
    });
  });

  describe('Error display', () => {
    it('renders error from hook in alert role', () => {
      setSearchParams({ email: 'user@example.com' });
      setHookState({ error: 'Failed to resend email' });
      renderContent();
      const alert = screen.getByRole('alert');
      expect(alert).toHaveTextContent('Failed to resend email');
    });
  });
});
