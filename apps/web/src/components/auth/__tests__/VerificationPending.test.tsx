/**
 * Tests for VerificationPending component (Issue #3076)
 *
 * Tests email verification pending state with:
 * - Email display (masked)
 * - Resend button functionality
 * - Cooldown timer
 * - Error handling
 * - Accessibility compliance (WCAG 2.1 AA)
 */

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
import { VerificationPending } from '../VerificationPending';

// Extended test messages for email verification
const testMessages = {
  'auth.oauth.separator': 'Or continue with',
  'auth.emailVerification.pending.title': 'Check your email',
  'auth.emailVerification.pending.description': "We've sent a verification link to your email address.",
  'auth.emailVerification.pending.sentTo': 'Verification email sent to:',
  'auth.emailVerification.pending.instructions': 'Click the link in the email to verify your account.',
  'auth.emailVerification.pending.spamHint': "If you don't see it, check your spam folder.",
  'auth.emailVerification.pending.resendButton': 'Resend verification email',
  'auth.emailVerification.pending.resending': 'Sending...',
  'auth.emailVerification.pending.resendCooldown': 'Resend in {seconds}s',
  'auth.emailVerification.pending.cooldownMessage': 'You can request a new email in {seconds} seconds.',
};

describe('VerificationPending', () => {
  const defaultProps = {
    email: 'user@example.com',
    onResend: vi.fn(),
    isResending: false,
    cooldownSeconds: 0,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders title text', () => {
      renderWithIntl(<VerificationPending {...defaultProps} />, {}, testMessages);
      expect(screen.getByText('Check your email')).toBeInTheDocument();
    });

    it('renders description text', () => {
      renderWithIntl(<VerificationPending {...defaultProps} />, {}, testMessages);
      expect(screen.getByText(/verification link/i)).toBeInTheDocument();
    });

    it('renders masked email address', () => {
      renderWithIntl(<VerificationPending {...defaultProps} />, {}, testMessages);
      // Email should be masked: user@example.com -> u***r@example.com
      expect(screen.getByText(/u\*\*\*r@example\.com/)).toBeInTheDocument();
    });

    it('renders resend button', () => {
      renderWithIntl(<VerificationPending {...defaultProps} />, {}, testMessages);
      expect(screen.getByTestId('resend-verification-button')).toBeInTheDocument();
    });

    it('renders spam hint', () => {
      renderWithIntl(<VerificationPending {...defaultProps} />, {}, testMessages);
      expect(screen.getByText(/spam folder/i)).toBeInTheDocument();
    });

    it('renders mail icon', () => {
      const { container } = renderWithIntl(<VerificationPending {...defaultProps} />, {}, testMessages);
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('Email Masking', () => {
    it('masks long email correctly', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} email="testuser@example.com" />,
        {},
        testMessages
      );
      expect(screen.getByText(/t\*\*\*r@example\.com/)).toBeInTheDocument();
    });

    it('masks short email correctly', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} email="ab@example.com" />,
        {},
        testMessages
      );
      expect(screen.getByText(/a\*\*\*@example\.com/)).toBeInTheDocument();
    });

    it('handles single character email', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} email="a@example.com" />,
        {},
        testMessages
      );
      expect(screen.getByText(/a\*\*\*@example\.com/)).toBeInTheDocument();
    });
  });

  describe('Resend Button', () => {
    it('calls onResend when button is clicked', async () => {
      const user = userEvent.setup();
      const onResend = vi.fn();

      renderWithIntl(
        <VerificationPending {...defaultProps} onResend={onResend} />,
        {},
        testMessages
      );

      await user.click(screen.getByTestId('resend-verification-button'));
      expect(onResend).toHaveBeenCalledTimes(1);
    });

    it('disables button when isResending is true', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} isResending={true} />,
        {},
        testMessages
      );

      const button = screen.getByTestId('resend-verification-button');
      expect(button).toBeDisabled();
    });

    it('shows loading state when isResending is true', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} isResending={true} />,
        {},
        testMessages
      );

      expect(screen.getByText('Sending...')).toBeInTheDocument();
    });

    it('disables button during cooldown', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} cooldownSeconds={30} />,
        {},
        testMessages
      );

      const button = screen.getByTestId('resend-verification-button');
      expect(button).toBeDisabled();
    });

    it('shows cooldown timer in button text', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} cooldownSeconds={45} />,
        {},
        testMessages
      );

      expect(screen.getByText(/Resend in 45s/)).toBeInTheDocument();
    });

    it('shows cooldown message below button', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} cooldownSeconds={30} />,
        {},
        testMessages
      );

      expect(screen.getByText(/You can request a new email in 30 seconds/)).toBeInTheDocument();
    });

    it('enables button when cooldown is 0', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} cooldownSeconds={0} />,
        {},
        testMessages
      );

      const button = screen.getByTestId('resend-verification-button');
      expect(button).not.toBeDisabled();
    });
  });

  describe('Error Display', () => {
    it('displays error message when error prop is provided', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} error="Failed to resend email" />,
        {},
        testMessages
      );

      expect(screen.getByText('Failed to resend email')).toBeInTheDocument();
    });

    it('does not display error container when error is null', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} error={null} />,
        {},
        testMessages
      );

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('renders error with alert role for accessibility', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} error="Error message" />,
        {},
        testMessages
      );

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has status role on main container', () => {
      renderWithIntl(<VerificationPending {...defaultProps} />, {}, testMessages);
      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('has aria-live polite on main container', () => {
      renderWithIntl(<VerificationPending {...defaultProps} />, {}, testMessages);
      const container = screen.getByRole('status');
      expect(container).toHaveAttribute('aria-live', 'polite');
    });

    it('button has accessible name', () => {
      renderWithIntl(<VerificationPending {...defaultProps} />, {}, testMessages);
      expect(screen.getByRole('button', { name: /resend/i })).toBeInTheDocument();
    });

    it('applies data-testid correctly', () => {
      renderWithIntl(
        <VerificationPending {...defaultProps} data-testid="test-pending" />,
        {},
        testMessages
      );
      expect(screen.getByTestId('test-pending')).toBeInTheDocument();
    });
  });
});
