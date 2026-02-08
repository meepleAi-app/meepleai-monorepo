/**
 * WishlistButton Tests - Issue #3824
 *
 * Test coverage for WishlistButton component:
 * - Rendering (states, sizes, tooltip)
 * - Behavior (toggle, event propagation, callbacks)
 * - Accessibility (keyboard, ARIA, screen reader)
 * - Edge cases (loading, rapid clicks)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { WishlistButton } from '../WishlistButton';

describe('WishlistButton', () => {
  const mockToggle = vi.fn();
  const defaultProps = {
    gameId: 'test-game-123',
    isWishlisted: false,
    onToggle: mockToggle,
  };

  beforeEach(() => {
    mockToggle.mockClear();
  });

  // ==========================================================================
  // Rendering Tests
  // ==========================================================================

  describe('Rendering', () => {
    it('should render with heart outline when not wishlisted', () => {
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      expect(button).toBeInTheDocument();

      // Heart should NOT have fill-current class
      const heart = button.querySelector('svg');
      expect(heart).not.toHaveClass('fill-current');
    });

    it('should render with heart filled when wishlisted', () => {
      render(<WishlistButton {...defaultProps} isWishlisted={true} />);

      const button = screen.getByTestId('wishlist-button');
      const heart = button.querySelector('svg');

      // Heart should have fill-current class
      expect(heart).toHaveClass('fill-current');
    });

    it('should render with correct size variants', () => {
      const { rerender } = render(<WishlistButton {...defaultProps} size="sm" />);
      let heart = screen.getByTestId('wishlist-button').querySelector('svg');
      expect(heart).toHaveClass('h-4', 'w-4');

      rerender(<WishlistButton {...defaultProps} size="md" />);
      heart = screen.getByTestId('wishlist-button').querySelector('svg');
      expect(heart).toHaveClass('h-5', 'w-5');

      rerender(<WishlistButton {...defaultProps} size="lg" />);
      heart = screen.getByTestId('wishlist-button').querySelector('svg');
      expect(heart).toHaveClass('h-6', 'w-6');
    });

    it('should render with custom className', () => {
      render(<WishlistButton {...defaultProps} className="custom-class" />);
      const button = screen.getByTestId('wishlist-button');
      expect(button).toHaveClass('custom-class');
    });

    it('should show tooltip on hover', async () => {
      const user = userEvent.setup();
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      await user.hover(button);

      await waitFor(() => {
        // Tooltip renders text twice (visible + sr-only), use getAllByText
        const tooltips = screen.getAllByText('Aggiungi alla wishlist');
        expect(tooltips.length).toBeGreaterThan(0);
      });
    });

    it('should show different tooltip when wishlisted', async () => {
      const user = userEvent.setup();
      render(<WishlistButton {...defaultProps} isWishlisted={true} />);

      const button = screen.getByTestId('wishlist-button');
      await user.hover(button);

      await waitFor(() => {
        // Tooltip renders text twice (visible + sr-only), use getAllByText
        const tooltips = screen.getAllByText('Rimuovi dalla wishlist');
        expect(tooltips.length).toBeGreaterThan(0);
      });
    });

    it('should apply heart-beat animation when wishlisted', () => {
      render(<WishlistButton {...defaultProps} isWishlisted={true} />);

      const heart = screen.getByTestId('wishlist-button').querySelector('svg');
      expect(heart).toHaveClass('animate-heart-beat');
    });

    it('should render in loading state', () => {
      render(<WishlistButton {...defaultProps} loading={true} />);

      const button = screen.getByTestId('wishlist-button');
      expect(button).toBeDisabled();
      expect(button).toHaveClass('opacity-50', 'cursor-wait');
    });
  });

  // ==========================================================================
  // Behavior Tests
  // ==========================================================================

  describe('Behavior', () => {
    it('should call onToggle with correct arguments on click', async () => {
      const user = userEvent.setup();
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      await user.click(button);

      expect(mockToggle).toHaveBeenCalledTimes(1);
      expect(mockToggle).toHaveBeenCalledWith('test-game-123', true); // Toggle to true
    });

    it('should toggle from wishlisted to not wishlisted', async () => {
      const user = userEvent.setup();
      render(<WishlistButton {...defaultProps} isWishlisted={true} />);

      const button = screen.getByTestId('wishlist-button');
      await user.click(button);

      expect(mockToggle).toHaveBeenCalledWith('test-game-123', false); // Toggle to false
    });

    it('should stop event propagation on click', async () => {
      const cardClick = vi.fn();
      const user = userEvent.setup();

      render(
        <div onClick={cardClick}>
          <WishlistButton {...defaultProps} />
        </div>
      );

      const button = screen.getByTestId('wishlist-button');
      await user.click(button);

      // onToggle should be called, but parent onClick should NOT
      expect(mockToggle).toHaveBeenCalledTimes(1);
      expect(cardClick).not.toHaveBeenCalled();
    });

    it('should not trigger toggle when loading', async () => {
      const user = userEvent.setup();
      render(<WishlistButton {...defaultProps} loading={true} />);

      const button = screen.getByTestId('wishlist-button');
      await user.click(button);

      expect(mockToggle).not.toHaveBeenCalled();
    });

    it('should handle async onToggle', async () => {
      const asyncToggle = vi.fn().mockResolvedValue(undefined);
      const user = userEvent.setup();

      render(<WishlistButton {...defaultProps} onToggle={asyncToggle} />);

      const button = screen.getByTestId('wishlist-button');
      await user.click(button);

      await waitFor(() => {
        expect(asyncToggle).toHaveBeenCalled();
      });
    });

    it('should handle rapid successive clicks gracefully', async () => {
      const user = userEvent.setup();
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');

      // Rapid clicks
      await user.click(button);
      await user.click(button);
      await user.click(button);

      // Should be called 3 times (no debouncing by default)
      expect(mockToggle).toHaveBeenCalledTimes(3);
    });
  });

  // ==========================================================================
  // Accessibility Tests
  // ==========================================================================

  describe('Accessibility', () => {
    it('should have correct ARIA label when not wishlisted', () => {
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      expect(button).toHaveAttribute('aria-label', 'Wishlist button, currently not added');
    });

    it('should have correct ARIA label when wishlisted', () => {
      render(<WishlistButton {...defaultProps} isWishlisted={true} />);

      const button = screen.getByTestId('wishlist-button');
      expect(button).toHaveAttribute('aria-label', 'Wishlist button, currently added');
    });

    it('should be keyboard accessible with Enter key', () => {
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      fireEvent.keyDown(button, { key: 'Enter' });

      expect(mockToggle).toHaveBeenCalledTimes(1);
      expect(mockToggle).toHaveBeenCalledWith('test-game-123', true);
    });

    it('should be keyboard accessible with Space key', () => {
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      fireEvent.keyDown(button, { key: ' ' });

      expect(mockToggle).toHaveBeenCalledTimes(1);
    });

    it('should stop event propagation on keyboard activation', () => {
      const cardClick = vi.fn();

      render(
        <div onClick={cardClick}>
          <WishlistButton {...defaultProps} />
        </div>
      );

      const button = screen.getByTestId('wishlist-button');
      fireEvent.keyDown(button, { key: 'Enter' });

      expect(mockToggle).toHaveBeenCalled();
      expect(cardClick).not.toHaveBeenCalled();
    });

    it('should not respond to other keys', () => {
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      fireEvent.keyDown(button, { key: 'a' });
      fireEvent.keyDown(button, { key: 'Escape' });

      expect(mockToggle).not.toHaveBeenCalled();
    });

    it('should be focusable', () => {
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      button.focus();

      expect(button).toHaveFocus();
    });

    it('should not be focusable when loading', () => {
      render(<WishlistButton {...defaultProps} loading={true} />);

      const button = screen.getByTestId('wishlist-button');
      expect(button).toBeDisabled();
    });
  });

  // ==========================================================================
  // Edge Cases
  // ==========================================================================

  describe('Edge Cases', () => {
    it('should handle custom testId', () => {
      render(<WishlistButton {...defaultProps} data-testid="custom-test-id" />);
      expect(screen.getByTestId('custom-test-id')).toBeInTheDocument();
    });

    it('should handle empty gameId gracefully', async () => {
      const user = userEvent.setup();
      render(<WishlistButton {...defaultProps} gameId="" />);

      const button = screen.getByTestId('wishlist-button');
      await user.click(button);

      expect(mockToggle).toHaveBeenCalledWith('', true);
    });

    it('should handle onToggle error gracefully', async () => {
      const errorToggle = vi.fn().mockRejectedValue(new Error('API Error'));
      const user = userEvent.setup();

      render(<WishlistButton {...defaultProps} onToggle={errorToggle} />);

      const button = screen.getByTestId('wishlist-button');

      // Click and catch the error
      await user.click(button).catch(() => {
        // Error expected, component should not crash
      });

      expect(errorToggle).toHaveBeenCalledTimes(1);
    });
  });

  // ==========================================================================
  // Visual States
  // ==========================================================================

  describe('Visual States', () => {
    it('should have rose color when wishlisted', () => {
      render(<WishlistButton {...defaultProps} isWishlisted={true} />);

      const button = screen.getByTestId('wishlist-button');
      expect(button).toHaveClass('text-rose-500');
    });

    it('should have hover scale effect', () => {
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      expect(button).toHaveClass('hover:scale-110');
    });

    it('should have active scale effect', () => {
      render(<WishlistButton {...defaultProps} />);

      const button = screen.getByTestId('wishlist-button');
      expect(button).toHaveClass('active:scale-95');
    });
  });
});
