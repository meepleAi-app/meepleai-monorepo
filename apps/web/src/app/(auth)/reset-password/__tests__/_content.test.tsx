/**
 * Reset Password page (_content.tsx) tests — v2 AuthCard migration (Task 13).
 *
 * Verifies:
 * - Already-authenticated guard redirects to /chat
 * - Request mode: renders form, successful submit → SuccessCard, error alert, "try again"
 * - Reset mode: token verification pending / invalid / valid branches
 * - Reset mode: successful confirm → success state; failed confirm → error alert
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const {
  mockPush,
  mockGetMe,
  mockVerifyResetToken,
  mockRequestPasswordReset,
  mockConfirmPasswordReset,
  mockLogin,
  mockSearchParamsGet,
  MockApiError,
} = vi.hoisted(() => {
  // Minimal stand-in for the real ApiError class. The shared getErrorMessage
  // helper (@/lib/utils/errorHandler) performs `error instanceof ApiError`,
  // so the mock module must re-export a constructor under that name.
  class MockApiError extends Error {
    statusCode: number;
    correlationId?: string;
    constructor(message: string, statusCode = 500, correlationId?: string) {
      super(message);
      this.name = 'ApiError';
      this.statusCode = statusCode;
      this.correlationId = correlationId;
    }
  }
  return {
    mockPush: vi.fn(),
    mockGetMe: vi.fn(),
    mockVerifyResetToken: vi.fn(),
    mockRequestPasswordReset: vi.fn(),
    mockConfirmPasswordReset: vi.fn(),
    mockLogin: vi.fn(),
    mockSearchParamsGet: vi.fn<(key: string) => string | null>(),
    MockApiError,
  };
});

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  useSearchParams: () => ({ get: mockSearchParamsGet }),
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getMe: (...args: unknown[]) => mockGetMe(...args),
      verifyResetToken: (...args: unknown[]) => mockVerifyResetToken(...args),
      requestPasswordReset: (...args: unknown[]) => mockRequestPasswordReset(...args),
      confirmPasswordReset: (...args: unknown[]) => mockConfirmPasswordReset(...args),
      login: (...args: unknown[]) => mockLogin(...args),
    },
  },
  ApiError: MockApiError,
}));

// Import AFTER mocks
import { ResetPasswordPageContent } from '../_content';

beforeEach(() => {
  // Reset everything (including any queued mockResolvedValueOnce implementations
  // so tests don't leak into each other).
  mockPush.mockReset();
  mockGetMe.mockReset();
  mockVerifyResetToken.mockReset();
  mockRequestPasswordReset.mockReset();
  mockConfirmPasswordReset.mockReset();
  mockLogin.mockReset();
  mockSearchParamsGet.mockReset();

  mockSearchParamsGet.mockReturnValue(null);
  // default: not authenticated (so the auth guard clears isCheckingAuth)
  mockGetMe.mockRejectedValue(new Error('401'));
});

// ---------------------------------------------------------------------------
// Auth guard
// ---------------------------------------------------------------------------

describe('ResetPasswordPageContent — auth guard', () => {
  it('redirects to /chat when the user is already authenticated', async () => {
    mockGetMe.mockResolvedValueOnce({
      id: 'u1',
      email: 'me@example.com',
      role: 'User',
    });

    render(<ResetPasswordPageContent />);

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/chat');
    });
  });
});

// ---------------------------------------------------------------------------
// Request mode (no token)
// ---------------------------------------------------------------------------

describe('ResetPasswordPageContent — request mode', () => {
  it('renders the email form when no token is present', async () => {
    render(<ResetPasswordPageContent />);

    expect(
      await screen.findByRole('heading', { name: 'auth.resetPassword.title' })
    ).toBeInTheDocument();
    expect(screen.getByLabelText('auth.resetPassword.email')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'auth.resetPassword.submitCta' })
    ).toBeInTheDocument();
  });

  it('submits the form and shows the success state on success', async () => {
    mockRequestPasswordReset.mockResolvedValueOnce({ message: 'ok' });

    render(<ResetPasswordPageContent />);

    const emailInput = await screen.findByLabelText('auth.resetPassword.email');
    fireEvent.change(emailInput, { target: { value: 'user@example.com' } });
    fireEvent.click(screen.getByRole('button', { name: 'auth.resetPassword.submitCta' }));

    await waitFor(() => {
      expect(mockRequestPasswordReset).toHaveBeenCalledWith('user@example.com');
    });

    // AuthCard's h1 renders the page title; SuccessCard no longer duplicates it.
    const headings = await screen.findAllByRole('heading', {
      name: 'auth.resetPassword.sentTitle',
    });
    expect(headings.length).toBeGreaterThan(0);
  });

  it('shows an error alert when the request fails', async () => {
    mockRequestPasswordReset.mockRejectedValueOnce(new Error('Service unavailable'));

    render(<ResetPasswordPageContent />);

    const emailInput = await screen.findByLabelText('auth.resetPassword.email');
    fireEvent.change(emailInput, { target: { value: 'user@example.com' } });
    fireEvent.click(screen.getByRole('button', { name: 'auth.resetPassword.submitCta' }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/Service unavailable/i);
  });

  it('clicking "try again" returns to the request form from the success state', async () => {
    mockRequestPasswordReset.mockResolvedValueOnce({ message: 'ok' });

    render(<ResetPasswordPageContent />);

    const emailInput = await screen.findByLabelText('auth.resetPassword.email');
    fireEvent.change(emailInput, { target: { value: 'user@example.com' } });
    fireEvent.click(screen.getByRole('button', { name: 'auth.resetPassword.submitCta' }));

    await screen.findAllByRole('heading', { name: 'auth.resetPassword.sentTitle' });

    fireEvent.click(screen.getByRole('button', { name: 'auth.resetPassword.tryAgain' }));

    expect(await screen.findByLabelText('auth.resetPassword.email')).toBeInTheDocument();
  });
});

// ---------------------------------------------------------------------------
// Reset mode (with token)
// ---------------------------------------------------------------------------

describe('ResetPasswordPageContent — reset mode', () => {
  beforeEach(() => {
    mockSearchParamsGet.mockImplementation((key: string) => (key === 'token' ? 'tok-123' : null));
  });

  it('shows the verifying state while the token is being checked', async () => {
    // Use mockImplementation (not mockResolvedValueOnce): the verify-token
    // effect re-fires on every render because the mocked useTranslation
    // returns a fresh `t` per call, which invalidates the effect's deps.
    // A single-shot mock would be exhausted after the first render.
    mockVerifyResetToken.mockImplementation(() => new Promise(() => {}));

    render(<ResetPasswordPageContent />);

    expect(
      await screen.findByRole('heading', { name: 'auth.resetPassword.verifyingTitle' })
    ).toBeInTheDocument();
  });

  it('shows the invalid-link card when token verification fails', async () => {
    mockVerifyResetToken.mockRejectedValue(new Error('bad token'));

    render(<ResetPasswordPageContent />);

    expect(
      await screen.findByRole('heading', { name: 'auth.resetPassword.invalidLinkTitle' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: 'auth.resetPassword.invalidLinkCta' })
    ).toBeInTheDocument();
  });

  it('renders the new password form when token is valid', async () => {
    mockVerifyResetToken.mockResolvedValue(undefined);

    render(<ResetPasswordPageContent />);

    expect(
      await screen.findByRole('heading', { name: 'auth.resetPassword.confirmTitle' })
    ).toBeInTheDocument();
    expect(screen.getByLabelText('auth.resetPassword.passwordLabel')).toBeInTheDocument();
    expect(screen.getByLabelText('auth.resetPassword.confirmPasswordLabel')).toBeInTheDocument();
  });

  it('submits a matching valid password and reaches the success state', async () => {
    mockVerifyResetToken.mockResolvedValue(undefined);
    mockConfirmPasswordReset.mockResolvedValue({ message: 'ok' });
    mockLogin.mockRejectedValue(new Error('no auto-login'));

    render(<ResetPasswordPageContent />);

    const pwd = await screen.findByLabelText('auth.resetPassword.passwordLabel');
    const confirm = screen.getByLabelText('auth.resetPassword.confirmPasswordLabel');

    // Wait for the confirm button to be present and no longer busy (verifyToken
    // finished → isLoading is false).
    const submitBtn = await screen.findByTestId('reset-password-confirm');
    await waitFor(() => {
      expect(submitBtn).not.toHaveAttribute('aria-busy', 'true');
    });

    fireEvent.change(pwd, { target: { value: 'StrongPass1' } });
    fireEvent.change(confirm, { target: { value: 'StrongPass1' } });

    // Wait for the button to become enabled after state has propagated.
    await waitFor(() => {
      expect(submitBtn).not.toBeDisabled();
    });

    // Submit the form directly to avoid timing issues around disabled toggles.
    const form = submitBtn.closest('form')!;
    fireEvent.submit(form);

    await waitFor(() => {
      expect(mockConfirmPasswordReset).toHaveBeenCalledWith('tok-123', 'StrongPass1');
    });

    expect(
      await screen.findByRole('heading', { name: 'auth.resetPassword.successTitle' })
    ).toBeInTheDocument();
  });

  it('shows an error alert when confirm password reset fails', async () => {
    mockVerifyResetToken.mockResolvedValue(undefined);
    mockConfirmPasswordReset.mockRejectedValue(new Error('Token expired'));

    render(<ResetPasswordPageContent />);

    const pwd = await screen.findByLabelText('auth.resetPassword.passwordLabel');
    const confirm = screen.getByLabelText('auth.resetPassword.confirmPasswordLabel');

    const submitBtn = await screen.findByTestId('reset-password-confirm');
    await waitFor(() => {
      expect(submitBtn).not.toHaveAttribute('aria-busy', 'true');
    });

    fireEvent.change(pwd, { target: { value: 'StrongPass1' } });
    fireEvent.change(confirm, { target: { value: 'StrongPass1' } });

    await waitFor(() => {
      expect(submitBtn).not.toBeDisabled();
    });

    const form = submitBtn.closest('form')!;
    fireEvent.submit(form);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/Token expired/i);
  });
});
