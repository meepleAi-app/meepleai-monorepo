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
jest.mock('../accessible/AccessibleModal', () => ({
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

jest.mock('../accessible/AccessibleButton', () => ({
  AccessibleButton: ({ children, onClick, 'aria-label': ariaLabel }: any) => (
    <button onClick={onClick} aria-label={ariaLabel}>
      {children}
    </button>
  ),
}));

describe('SessionWarningModal', () => {
  const mockOnStayLoggedIn = jest.fn();
  const mockOnLogOut = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
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
        jest.advanceTimersByTime(60 * 1000);
      });

      expect(screen.getByText('4')).toBeInTheDocument();

      // Fast-forward another minute
      act(() => {
        jest.advanceTimersByTime(60 * 1000);
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
        jest.advanceTimersByTime(60 * 1000);
      });

      expect(screen.getByText('0')).toBeInTheDocument();

      // Fast-forward another minute - should still be 0
      act(() => {
        jest.advanceTimersByTime(60 * 1000);
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
        jest.advanceTimersByTime(30 * 1000);
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
        jest.advanceTimersByTime(30 * 1000);
      });

      expect(screen.getByText('10')).toBeInTheDocument();

      // Fast-forward 30 more seconds (now a full minute since prop change)
      act(() => {
        jest.advanceTimersByTime(30 * 1000);
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
        jest.advanceTimersByTime(60 * 1000);
      });

      // No assertions needed - we're just checking no errors occur
    });
  });

  describe('Stay Logged In Button', () => {
    it('should call onStayLoggedIn when clicked', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      const stayLoggedInButton = screen.getByRole('button', { name: /stay logged in/i });
      await user.click(stayLoggedInButton);

      expect(mockOnStayLoggedIn).toHaveBeenCalledTimes(1);
      expect(mockOnLogOut).not.toHaveBeenCalled();
    });

    it('should handle multiple rapid clicks gracefully', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      const stayLoggedInButton = screen.getByRole('button', { name: /stay logged in/i });

      await user.click(stayLoggedInButton);
      await user.click(stayLoggedInButton);
      await user.click(stayLoggedInButton);

      expect(mockOnStayLoggedIn).toHaveBeenCalledTimes(3);
    });
  });

  describe('Log Out Now Button', () => {
    it('should call onLogOut when clicked', async () => {
      const user = userEvent.setup({ delay: null });

      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      const logOutButton = screen.getByRole('button', { name: /log out now/i });
      await user.click(logOutButton);

      expect(mockOnLogOut).toHaveBeenCalledTimes(1);
      expect(mockOnStayLoggedIn).not.toHaveBeenCalled();
    });
  });

  describe('Auto-Logout', () => {
    it('should call onLogOut when countdown reaches 0', () => {
      render(
        <SessionWarningModal
          remainingMinutes={2}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(mockOnLogOut).not.toHaveBeenCalled();

      // Fast-forward 1 minute (2 -> 1)
      act(() => {
        jest.advanceTimersByTime(60 * 1000);
      });

      expect(mockOnLogOut).not.toHaveBeenCalled();

      // Fast-forward another minute (1 -> 0)
      act(() => {
        jest.advanceTimersByTime(60 * 1000);
      });

      expect(mockOnLogOut).toHaveBeenCalledTimes(1);
    });

    it('should call onLogOut immediately when remainingMinutes is 0', () => {
      render(
        <SessionWarningModal
          remainingMinutes={0}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      // Should call onLogOut immediately due to useEffect
      expect(mockOnLogOut).toHaveBeenCalledTimes(1);
    });

    it('should only call onLogOut once when countdown reaches 0', () => {
      render(
        <SessionWarningModal
          remainingMinutes={1}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      // Fast-forward 1 minute (1 -> 0)
      act(() => {
        jest.advanceTimersByTime(60 * 1000);
      });

      expect(mockOnLogOut).toHaveBeenCalledTimes(1);

      // Fast-forward more time - should not call again
      act(() => {
        jest.advanceTimersByTime(60 * 1000);
      });

      expect(mockOnLogOut).toHaveBeenCalledTimes(1);
    });
  });

  describe('Accessibility', () => {
    it('should have aria-live region for countdown', () => {
      const { container } = render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      const countdown = container.querySelector('[aria-live="polite"]');
      expect(countdown).toBeInTheDocument();
      expect(countdown).toHaveAttribute('aria-atomic', 'true');
    });

    it('should have proper button labels', () => {
      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      const stayButton = screen.getByRole('button', { name: /stay logged in and extend session/i });
      const logoutButton = screen.getByRole('button', { name: /log out now/i });

      expect(stayButton).toBeInTheDocument();
      expect(logoutButton).toBeInTheDocument();
    });

    it('should have proper dialog role and labels', () => {
      render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-labelledby', 'modal-title');
      expect(dialog).toHaveAttribute('aria-describedby', 'modal-desc');
    });

    it('should mark warning icon as aria-hidden', () => {
      const { container } = render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      // The icon container should have aria-hidden
      const iconContainer = container.querySelector('[aria-hidden="true"]');
      expect(iconContainer).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle negative remainingMinutes gracefully', () => {
      render(
        <SessionWarningModal
          remainingMinutes={-5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      // Should display the negative value
      expect(screen.getByText('-5')).toBeInTheDocument();

      // Since negative is <= 0, should trigger auto-logout
      expect(mockOnLogOut).toHaveBeenCalled();
    });

    it('should handle very large remainingMinutes values', () => {
      render(
        <SessionWarningModal
          remainingMinutes={9999}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('9999')).toBeInTheDocument();
      expect(screen.getByText('minutes remaining')).toBeInTheDocument();
    });

    it('should handle prop changes from high to low values', () => {
      const { rerender } = render(
        <SessionWarningModal
          remainingMinutes={10}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('10')).toBeInTheDocument();

      rerender(
        <SessionWarningModal
          remainingMinutes={1}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      expect(screen.getByText('1')).toBeInTheDocument();
      expect(screen.getByText('minute remaining')).toBeInTheDocument();
    });

    it('should handle callback functions being replaced', () => {
      const newOnStayLoggedIn = jest.fn();
      const newOnLogOut = jest.fn();

      const { rerender } = render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      rerender(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={newOnStayLoggedIn}
          onLogOut={newOnLogOut}
        />
      );

      // Countdown to 0 should call new callback
      act(() => {
        jest.advanceTimersByTime(5 * 60 * 1000);
      });

      expect(newOnLogOut).toHaveBeenCalled();
      expect(mockOnLogOut).not.toHaveBeenCalled();
    });
  });

  describe('Visual States', () => {
    it('should apply correct styling classes for countdown', () => {
      const { container } = render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      const countdown = container.querySelector('.text-4xl');
      expect(countdown).toBeInTheDocument();
      expect(countdown).toHaveClass('font-bold', 'text-amber-600');
    });

    it('should have warning color scheme', () => {
      const { container } = render(
        <SessionWarningModal
          remainingMinutes={5}
          onStayLoggedIn={mockOnStayLoggedIn}
          onLogOut={mockOnLogOut}
        />
      );

      const iconBg = container.querySelector('.bg-amber-100');
      expect(iconBg).toBeInTheDocument();
    });
  });
});
