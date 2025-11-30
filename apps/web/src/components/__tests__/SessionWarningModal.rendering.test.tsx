/**
 * Unit tests for SessionWarningModal component (AUTH-05)
 *
 * Tests cover:
 * - Modal rendering
 * - Countdown timer functionality
 * - "Stay Logged In" button behavior
 * - "Log Out Now" button behavior
 * - Auto-logout when countdown reaches 0
 * - Countdown updates every minute
 * - Proper singular/plural text display
 * - Accessibility features
 * - Edge cases (0 minutes, rapid clicks, etc.)
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SessionWarningModal } from '../SessionWarningModal';

// Mock AccessibleModal and AccessibleButton
vi.mock('../accessible/AccessibleModal', () => ({
  AccessibleModal: ({ children, title, description, isOpen }: any) => {
    if (!isOpen) return null;
    return (
      <div role="dialog" aria-labelledby="modal-title" aria-describedby="modal-desc">
        <h2 id="modal-title">{title}</h2>
        <p id="modal-desc">{description}</p>
        {children}
      </div>
    );
  },
}));

vi.mock('../accessible/AccessibleButton', () => ({
  AccessibleButton: ({ children, onClick, 'aria-label': ariaLabel }: any) => (
    <button onClick={onClick} aria-label={ariaLabel}>
      {children}
    </button>
  ),
}));

describe('SessionWarningModal', () => {
  const mockOnStayLoggedIn = vi.fn();
  const mockOnLogOut = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
  });

  describe('Rendering', () => {
    it('should render modal with title and description', () => {
      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('Session Expiring Soon')).toBeInTheDocument();
      expect(screen.getByText('Your session is about to expire due to inactivity')).toBeInTheDocument();
    });

    it('should display warning icon', () => {
      const { container } = render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });

    it('should display remaining minutes countdown', () => {
      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('5')).toBeInTheDocument();
      expect(screen.getByText('minutes remaining')).toBeInTheDocument();
    });

    it('should display singular "minute" when remaining minutes is 1', () => {
      render(
        <SessionWarningModal
          remainingMinutes={1}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('1')).toBeInTheDocument();
      expect(screen.getByText('minute remaining')).toBeInTheDocument();
    });

    it('should display plural "minutes" when remaining minutes is not 1', () => {
      render(
        <SessionWarningModal
          remainingMinutes={3}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('3')).toBeInTheDocument();
      expect(screen.getByText('minutes remaining')).toBeInTheDocument();
    });

    it('should display warning message', () => {
      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText(/You haven't been active for a while/)).toBeInTheDocument();
      expect(screen.getByText(/For your security, your session will expire soon/)).toBeInTheDocument();
    });

    it('should display help text about session expiration', () => {
      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText(/Sessions expire after 30 days of inactivity/)).toBeInTheDocument();
    });

    it('should display both action buttons', () => {
      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByRole('button', { name: /stay logged in/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /log out now/i })).toBeInTheDocument();
    });
  });

  describe('Countdown Timer', () => {
    it('should update countdown every minute', () => {
      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('5')).toBeInTheDocument();

      // Fast-forward 1 minute
      act(() => {
        vi.advanceTimersByTime(60 * 1000);
      });

      expect(screen.getByText('4')).toBeInTheDocument();

      // Fast-forward another minute
      act(() => {
        vi.advanceTimersByTime(60 * 1000);
      });

      expect(screen.getByText('3')).toBeInTheDocument();
    });

    it('should not go below 0 minutes', () => {
      render(
        <SessionWarningModal
          remainingMinutes={1}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('1')).toBeInTheDocument();

      // Fast-forward 1 minute
      act(() => {
        vi.advanceTimersByTime(60 * 1000);
      });

      expect(screen.getByText('0')).toBeInTheDocument();

      // Fast-forward another minute - should still be 0
      act(() => {
        vi.advanceTimersByTime(60 * 1000);
      });

      expect(screen.getByText('0')).toBeInTheDocument();
    });

    it('should update countdown when remainingMinutes prop changes', () => {
      const { rerender } = render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('5')).toBeInTheDocument();

      rerender(
        <SessionWarningModal
          remainingMinutes={10}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('10')).toBeInTheDocument();
    });

    it('should reset countdown interval when remainingMinutes prop changes', () => {
      const { rerender } = render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      // Fast-forward 30 seconds (halfway through first minute)
      act(() => {
        vi.advanceTimersByTime(30 * 1000);
      });

      // Update remainingMinutes prop
      rerender(
        <SessionWarningModal
          remainingMinutes={10}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('10')).toBeInTheDocument();

      // Fast-forward another 30 seconds (should not decrement yet because interval was reset)
      act(() => {
        vi.advanceTimersByTime(30 * 1000);
      });

      expect(screen.getByText('10')).toBeInTheDocument();

      // Fast-forward 30 more seconds (now a full minute since prop change)
      act(() => {
        vi.advanceTimersByTime(30 * 1000);
      });

      expect(screen.getByText('9')).toBeInTheDocument();
    });

    it('should cleanup interval on unmount', () => {
      const { unmount } = render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('5')).toBeInTheDocument();

      unmount();

      // Fast-forward time - should not throw errors
      act(() => {
        vi.advanceTimersByTime(60 * 1000);
      });

      // No assertions needed - we're just checking no errors occur
    });
  });

