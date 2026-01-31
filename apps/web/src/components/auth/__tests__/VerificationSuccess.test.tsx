/**
 * Tests for VerificationSuccess component (Issue #3076)
 *
 * Tests email verification success state with:
 * - Success animation
 * - Auto-redirect countdown
 * - Manual redirect button
 * - Accessibility compliance (WCAG 2.1 AA)
 */

import { screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
import { VerificationSuccess } from '../VerificationSuccess';

// Extended test messages for email verification
const testMessages = {
  'auth.emailVerification.success.title': 'Email verified!',
  'auth.emailVerification.success.description': 'Your email has been successfully verified.',
  'auth.emailVerification.success.continueButton': 'Continue to Dashboard',
  'auth.emailVerification.success.redirectCountdown': 'Redirecting in {seconds} seconds...',
  'auth.emailVerification.success.welcomeMessage': 'Welcome to MeepleAI! You can now use all features.',
};

describe('VerificationSuccess', () => {
  const defaultProps = {
    redirectUrl: '/dashboard',
    autoRedirectSeconds: 3,
    onRedirect: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Rendering', () => {
    it('renders success title', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      expect(screen.getByText('Email verified!')).toBeInTheDocument();
    });

    it('renders success description', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      expect(screen.getByText(/successfully verified/i)).toBeInTheDocument();
    });

    it('renders continue button', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      expect(screen.getByTestId('continue-to-dashboard-button')).toBeInTheDocument();
    });

    it('renders welcome message', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      expect(screen.getByText(/Welcome to MeepleAI/i)).toBeInTheDocument();
    });

    it('renders check icon', () => {
      const { container } = renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('renders email when provided', () => {
      renderWithIntl(
        <VerificationSuccess {...defaultProps} email="user@example.com" />,
        {},
        testMessages
      );
      expect(screen.getByText('user@example.com')).toBeInTheDocument();
    });

    it('does not render email section when not provided', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      expect(screen.queryByText('@')).not.toBeInTheDocument();
    });
  });

  describe('Countdown', () => {
    it('displays initial countdown', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      expect(screen.getByText(/Redirecting in 3 seconds/)).toBeInTheDocument();
    });

    it('decrements countdown each second', async () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);

      // Initial state
      expect(screen.getByText(/Redirecting in 3 seconds/)).toBeInTheDocument();

      // After 1 second
      await act(async () => {
        vi.advanceTimersByTime(1000);
      });
      expect(screen.getByText(/Redirecting in 2 seconds/)).toBeInTheDocument();

      // After 2 seconds
      await act(async () => {
        vi.advanceTimersByTime(1000);
      });
      expect(screen.getByText(/Redirecting in 1 seconds/)).toBeInTheDocument();
    });

    it('calls onRedirect when countdown reaches 0', async () => {
      const onRedirect = vi.fn();
      renderWithIntl(
        <VerificationSuccess {...defaultProps} onRedirect={onRedirect} />,
        {},
        testMessages
      );

      // Wait for countdown to complete
      await act(async () => {
        vi.advanceTimersByTime(3000);
      });

      expect(onRedirect).toHaveBeenCalledTimes(1);
    });

    it('does not show countdown when autoRedirectSeconds is 0', () => {
      renderWithIntl(
        <VerificationSuccess {...defaultProps} autoRedirectSeconds={0} />,
        {},
        testMessages
      );
      expect(screen.queryByText(/Redirecting in/)).not.toBeInTheDocument();
    });
  });

  describe('Manual Redirect', () => {
    it('calls onRedirect when button is clicked', async () => {
      vi.useRealTimers(); // Use real timers for user interaction
      const user = userEvent.setup();
      const onRedirect = vi.fn();

      renderWithIntl(
        <VerificationSuccess {...defaultProps} onRedirect={onRedirect} />,
        {},
        testMessages
      );

      await user.click(screen.getByTestId('continue-to-dashboard-button'));
      expect(onRedirect).toHaveBeenCalledTimes(1);
    });

    it('only calls onRedirect once on multiple clicks', async () => {
      vi.useRealTimers();
      const user = userEvent.setup();
      const onRedirect = vi.fn();

      renderWithIntl(
        <VerificationSuccess {...defaultProps} onRedirect={onRedirect} />,
        {},
        testMessages
      );

      const button = screen.getByTestId('continue-to-dashboard-button');
      await user.click(button);
      await user.click(button);
      await user.click(button);

      // Should only redirect once
      expect(onRedirect).toHaveBeenCalledTimes(1);
    });
  });

  describe('Accessibility', () => {
    it('has status role on main container', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('has aria-live polite on main container', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      const container = screen.getByRole('status');
      expect(container).toHaveAttribute('aria-live', 'polite');
    });

    it('button has accessible name', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      expect(screen.getByRole('button', { name: /continue to dashboard/i })).toBeInTheDocument();
    });

    it('applies data-testid correctly', () => {
      renderWithIntl(
        <VerificationSuccess {...defaultProps} data-testid="test-success" />,
        {},
        testMessages
      );
      expect(screen.getByTestId('test-success')).toBeInTheDocument();
    });

    it('countdown message has aria-live for screen readers', () => {
      renderWithIntl(<VerificationSuccess {...defaultProps} />, {}, testMessages);
      const countdown = screen.getByText(/Redirecting in/);
      expect(countdown).toHaveAttribute('aria-live', 'polite');
    });
  });
});
