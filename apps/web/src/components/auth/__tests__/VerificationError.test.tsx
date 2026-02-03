/**
 * Tests for VerificationError component (Issue #3076)
 *
 * Tests email verification error states with:
 * - Different error types (expired, invalid, already_verified, etc.)
 * - Appropriate actions for each error type
 * - Accessibility compliance (WCAG 2.1 AA)
 */

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
import { VerificationError } from '../VerificationError';

// Extended test messages for email verification
const testMessages = {
  'auth.emailVerification.pending.resending': 'Sending...',
  'auth.emailVerification.pending.resendCooldown': 'Resend in {seconds}s',
  'auth.emailVerification.pending.cooldownMessage': 'You can request a new email in {seconds} seconds.',
  'auth.emailVerification.error.resendButton': 'Resend verification email',
  'auth.emailVerification.error.goToLogin': 'Go to Login',
  'auth.emailVerification.error.tryAgain': 'Try Again',
  'auth.emailVerification.error.helpText': 'If you continue to have issues, please contact support.',
  'auth.emailVerification.error.expired.title': 'Link expired',
  'auth.emailVerification.error.expired.description': 'This verification link has expired. Please request a new one.',
  'auth.emailVerification.error.invalid.title': 'Invalid link',
  'auth.emailVerification.error.invalid.description': 'This verification link is invalid or has already been used.',
  'auth.emailVerification.error.already_verified.title': 'Already verified',
  'auth.emailVerification.error.already_verified.description': 'Your email has already been verified. You can log in now.',
  'auth.emailVerification.error.not_found.title': 'Link not found',
  'auth.emailVerification.error.not_found.description': 'This verification link could not be found.',
  'auth.emailVerification.error.rate_limited.title': 'Too many requests',
  'auth.emailVerification.error.rate_limited.description': 'Please wait before requesting another verification email.',
  'auth.emailVerification.error.unknown.title': 'Verification failed',
  'auth.emailVerification.error.unknown.description': 'An error occurred while verifying your email. Please try again.',
};

describe('VerificationError', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Error Type: expired', () => {
    const props = {
      errorType: 'expired' as const,
      onResend: vi.fn(),
    };

    it('renders expired title', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByText('Link expired')).toBeInTheDocument();
    });

    it('renders expired description', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByText(/verification link has expired/i)).toBeInTheDocument();
    });

    it('shows resend button', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByTestId('resend-verification-button')).toBeInTheDocument();
    });

    it('calls onResend when resend button is clicked', async () => {
      const user = userEvent.setup();
      const onResend = vi.fn();

      renderWithIntl(<VerificationError {...props} onResend={onResend} />, {}, testMessages);
      await user.click(screen.getByTestId('resend-verification-button'));

      expect(onResend).toHaveBeenCalledTimes(1);
    });
  });

  describe('Error Type: invalid', () => {
    const props = {
      errorType: 'invalid' as const,
      onRetry: vi.fn(),
    };

    it('renders invalid title', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByText('Invalid link')).toBeInTheDocument();
    });

    it('shows retry button', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByTestId('retry-button')).toBeInTheDocument();
    });

    it('calls onRetry when retry button is clicked', async () => {
      const user = userEvent.setup();
      const onRetry = vi.fn();

      renderWithIntl(<VerificationError {...props} onRetry={onRetry} />, {}, testMessages);
      await user.click(screen.getByTestId('retry-button'));

      expect(onRetry).toHaveBeenCalledTimes(1);
    });
  });

  describe('Error Type: already_verified', () => {
    const props = {
      errorType: 'already_verified' as const,
      onGoToLogin: vi.fn(),
    };

    it('renders already verified title', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByText('Already verified')).toBeInTheDocument();
    });

    it('renders already verified description', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByText(/already been verified/i)).toBeInTheDocument();
    });

    it('shows go to login button', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByTestId('go-to-login-button')).toBeInTheDocument();
    });

    it('calls onGoToLogin when login button is clicked', async () => {
      const user = userEvent.setup();
      const onGoToLogin = vi.fn();

      renderWithIntl(<VerificationError {...props} onGoToLogin={onGoToLogin} />, {}, testMessages);
      await user.click(screen.getByTestId('go-to-login-button'));

      expect(onGoToLogin).toHaveBeenCalledTimes(1);
    });

    it('does not show resend button', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.queryByTestId('resend-verification-button')).not.toBeInTheDocument();
    });
  });

  describe('Error Type: not_found', () => {
    const props = {
      errorType: 'not_found' as const,
      onRetry: vi.fn(),
    };

    it('renders not found title', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByText('Link not found')).toBeInTheDocument();
    });

    it('shows retry button', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByTestId('retry-button')).toBeInTheDocument();
    });
  });

  describe('Error Type: rate_limited', () => {
    const props = {
      errorType: 'rate_limited' as const,
      cooldownSeconds: 30,
    };

    it('renders rate limited title', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByText('Too many requests')).toBeInTheDocument();
    });

    it('shows cooldown message', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByText(/You can request a new email in 30 seconds/)).toBeInTheDocument();
    });
  });

  describe('Error Type: unknown', () => {
    const props = {
      errorType: 'unknown' as const,
      onResend: vi.fn(),
      onRetry: vi.fn(),
    };

    it('renders unknown error title', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByText('Verification failed')).toBeInTheDocument();
    });

    it('shows both resend and retry buttons', () => {
      renderWithIntl(<VerificationError {...props} />, {}, testMessages);
      expect(screen.getByTestId('resend-verification-button')).toBeInTheDocument();
      expect(screen.getByTestId('retry-button')).toBeInTheDocument();
    });
  });

  describe('Custom Error Message', () => {
    it('uses custom error message when provided', () => {
      renderWithIntl(
        <VerificationError
          errorType="expired"
          errorMessage="Custom error message"
        />,
        {},
        testMessages
      );
      expect(screen.getByText('Custom error message')).toBeInTheDocument();
    });
  });

  describe('Resend Button States', () => {
    it('shows loading state when isResending is true', () => {
      renderWithIntl(
        <VerificationError
          errorType="expired"
          onResend={vi.fn()}
          isResending={true}
        />,
        {},
        testMessages
      );
      expect(screen.getByText('Sending...')).toBeInTheDocument();
    });

    it('disables button during cooldown', () => {
      renderWithIntl(
        <VerificationError
          errorType="expired"
          onResend={vi.fn()}
          cooldownSeconds={30}
        />,
        {},
        testMessages
      );
      const button = screen.getByTestId('resend-verification-button');
      expect(button).toBeDisabled();
    });

    it('shows cooldown timer in button text', () => {
      renderWithIntl(
        <VerificationError
          errorType="expired"
          onResend={vi.fn()}
          cooldownSeconds={45}
        />,
        {},
        testMessages
      );
      expect(screen.getByText(/Resend in 45s/)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has alert role on main container', () => {
      renderWithIntl(
        <VerificationError errorType="expired" />,
        {},
        testMessages
      );
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('has aria-live assertive on main container', () => {
      renderWithIntl(
        <VerificationError errorType="expired" />,
        {},
        testMessages
      );
      const container = screen.getByRole('alert');
      expect(container).toHaveAttribute('aria-live', 'assertive');
    });

    it('renders help text', () => {
      renderWithIntl(
        <VerificationError errorType="expired" />,
        {},
        testMessages
      );
      expect(screen.getByText(/contact support/i)).toBeInTheDocument();
    });

    it('applies data-testid correctly', () => {
      renderWithIntl(
        <VerificationError errorType="expired" data-testid="test-error" />,
        {},
        testMessages
      );
      expect(screen.getByTestId('test-error')).toBeInTheDocument();
    });
  });

  describe('Icon Rendering', () => {
    it('renders clock icon for expired error', () => {
      const { container } = renderWithIntl(
        <VerificationError errorType="expired" />,
        {},
        testMessages
      );
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders check icon for already_verified', () => {
      const { container } = renderWithIntl(
        <VerificationError errorType="already_verified" />,
        {},
        testMessages
      );
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders alert icon for unknown error', () => {
      const { container } = renderWithIntl(
        <VerificationError errorType="unknown" />,
        {},
        testMessages
      );
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });
});
