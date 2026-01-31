/**
 * QuickActionCard Unit Tests (Issue #1834: UI-007)
 *
 * Coverage areas:
 * - Rendering with different props
 * - Click handlers
 * - Keyboard navigation (Enter, Space)
 * - Accessibility (ARIA, roles)
 * - Variants (default, secondary)
 * - Hover states
 *
 * Target: 95%+ coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QuickActionCard } from '../QuickActionCard';
import { PlusCircle, Settings } from 'lucide-react';

describe('QuickActionCard', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders with required props', () => {
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      expect(screen.getByRole('button', { name: 'Add Game' })).toBeInTheDocument();
      expect(screen.getByText('Add Game')).toBeInTheDocument();
    });

    it('renders with description', () => {
      const onClick = vi.fn();
      render(
        <QuickActionCard
          icon={PlusCircle}
          title="Add Game"
          description="Add a new board game"
          onClick={onClick}
        />
      );

      expect(screen.getByText('Add a new board game')).toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: 'Add Game: Add a new board game' })
      ).toBeInTheDocument();
    });

    it('renders without description', () => {
      const onClick = vi.fn();
      render(<QuickActionCard icon={Settings} title="Settings" onClick={onClick} />);

      expect(screen.queryByText(/description/i)).not.toBeInTheDocument();
    });

    it('renders icon correctly', () => {
      const onClick = vi.fn();
      const { container } = render(
        <QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />
      );

      // Icon should be rendered with aria-hidden
      const icon = container.querySelector('svg[aria-hidden="true"]');
      expect(icon).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const onClick = vi.fn();
      const { container } = render(
        <QuickActionCard
          icon={PlusCircle}
          title="Add Game"
          onClick={onClick}
          className="custom-class"
        />
      );

      const card = container.querySelector('.custom-class');
      expect(card).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Variant Tests
  // ============================================================================

  describe('Variants', () => {
    it('renders default variant correctly', () => {
      const onClick = vi.fn();
      const { container } = render(
        <QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} variant="default" />
      );

      const card = container.firstChild;
      expect(card).toHaveClass('bg-card');
    });

    it('renders secondary variant correctly', () => {
      const onClick = vi.fn();
      const { container } = render(
        <QuickActionCard icon={Settings} title="Settings" onClick={onClick} variant="secondary" />
      );

      const card = container.firstChild;
      expect(card).toHaveClass('bg-secondary/10');
    });

    it('uses default variant when not specified', () => {
      const onClick = vi.fn();
      const { container } = render(
        <QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />
      );

      const card = container.firstChild;
      expect(card).toHaveClass('bg-card');
    });
  });

  // ============================================================================
  // Interaction Tests
  // ============================================================================

  describe('Click Interactions', () => {
    it('calls onClick when clicked', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const card = screen.getByRole('button', { name: 'Add Game' });
      await user.click(card);

      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('calls onClick multiple times', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const card = screen.getByRole('button', { name: 'Add Game' });
      await user.click(card);
      await user.click(card);
      await user.click(card);

      expect(onClick).toHaveBeenCalledTimes(3);
    });
  });

  // ============================================================================
  // Keyboard Navigation Tests
  // ============================================================================

  describe('Keyboard Navigation', () => {
    it('calls onClick when Enter key is pressed', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const card = screen.getByRole('button', { name: 'Add Game' });
      card.focus();
      await user.keyboard('{Enter}');

      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('calls onClick when Space key is pressed', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const card = screen.getByRole('button', { name: 'Add Game' });
      card.focus();
      await user.keyboard(' ');

      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('does not call onClick for other keys', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const card = screen.getByRole('button', { name: 'Add Game' });
      card.focus();
      await user.keyboard('a');
      await user.keyboard('{Escape}');
      await user.keyboard('{Tab}');

      expect(onClick).not.toHaveBeenCalled();
    });

    it('is keyboard focusable', () => {
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const card = screen.getByRole('button', { name: 'Add Game' });
      expect(card).toHaveAttribute('tabIndex', '0');
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('has button role', () => {
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const card = screen.getByRole('button');
      expect(card).toBeInTheDocument();
    });

    it('has correct aria-label with title only', () => {
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const card = screen.getByRole('button', { name: 'Add Game' });
      expect(card).toHaveAttribute('aria-label', 'Add Game');
    });

    it('has correct aria-label with title and description', () => {
      const onClick = vi.fn();
      render(
        <QuickActionCard
          icon={PlusCircle}
          title="Add Game"
          description="Add a new board game"
          onClick={onClick}
        />
      );

      const card = screen.getByRole('button', { name: 'Add Game: Add a new board game' });
      expect(card).toHaveAttribute('aria-label', 'Add Game: Add a new board game');
    });

    it('icon has aria-hidden', () => {
      const onClick = vi.fn();
      const { container } = render(
        <QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />
      );

      const icon = container.querySelector('svg');
      expect(icon).toHaveAttribute('aria-hidden', 'true');
    });

    it('has semantic heading for title', () => {
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const heading = screen.getByRole('heading', { name: 'Add Game', level: 3 });
      expect(heading).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    it('handles very long title with line-clamp', () => {
      const onClick = vi.fn();
      const longTitle = 'This is a very long title that should be truncated properly with ellipsis';
      const { container } = render(
        <QuickActionCard icon={PlusCircle} title={longTitle} onClick={onClick} />
      );

      const titleElement = screen.getByText(longTitle);
      expect(titleElement).toHaveClass('line-clamp-1');
    });

    it('handles very long description with line-clamp', () => {
      const onClick = vi.fn();
      const longDescription =
        'This is a very long description that should be truncated properly with ellipsis to prevent layout issues';
      render(
        <QuickActionCard
          icon={PlusCircle}
          title="Add Game"
          description={longDescription}
          onClick={onClick}
        />
      );

      const descElement = screen.getByText(longDescription);
      expect(descElement).toHaveClass('line-clamp-1');
    });

    it('handles custom icon component', () => {
      const onClick = vi.fn();
      const CustomIcon = ({ className }: { className?: string }) => (
        <svg className={className} data-testid="custom-icon">
          <circle cx="12" cy="12" r="10" />
        </svg>
      );

      render(<QuickActionCard icon={CustomIcon} title="Custom" onClick={onClick} />);

      expect(screen.getByTestId('custom-icon')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // CSS Classes Tests
  // ============================================================================

  describe('CSS Classes', () => {
    it('applies hover transition classes', () => {
      const onClick = vi.fn();
      const { container } = render(
        <QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />
      );

      const card = container.firstChild;
      expect(card).toHaveClass('transition-all');
      expect(card).toHaveClass('duration-200');
    });

    it('applies cursor-pointer class', () => {
      const onClick = vi.fn();
      const { container } = render(
        <QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />
      );

      const card = container.firstChild;
      expect(card).toHaveClass('cursor-pointer');
    });

    it('applies font-quicksand to title', () => {
      const onClick = vi.fn();
      render(<QuickActionCard icon={PlusCircle} title="Add Game" onClick={onClick} />);

      const title = screen.getByText('Add Game');
      expect(title).toHaveClass('font-quicksand');
    });
  });
});
