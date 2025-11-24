import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { InlineCommentIndicator } from '../comments/InlineCommentIndicator';

describe('InlineCommentIndicator', () => {
  const mockOnClick = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.runOnlyPendingTimers();
    vi.useRealTimers();
  });

  describe('Basic Rendering', () => {
    it('renders button with message icon', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');
      expect(button).toBeInTheDocument();

      // Check for SVG icon
      const svg = button.querySelector('svg');
      expect(svg).toBeInTheDocument();
      expect(svg).toHaveAttribute('width', '16');
      expect(svg).toHaveAttribute('height', '16');
    });

    it('renders with correct aria-label', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      expect(screen.getByLabelText('View 1 comment on line 10')).toBeInTheDocument();
    });

    it('renders plural aria-label when commentCount > 1', () => {
      render(
        <InlineCommentIndicator
          lineNumber={5}
          commentCount={3}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      expect(screen.getByLabelText('View 3 comments on line 5')).toBeInTheDocument();
    });
  });

  describe('Comment Count Badge', () => {
    it('shows count badge when commentCount > 1', () => {
      const { container } = render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={3}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      // Badge now uses Tailwind classes (absolute -top-1 -right-1)
      const badge = container.querySelector('span.absolute.-top-1.-right-1');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveTextContent('3');
    });

    it('hides count badge when commentCount = 1', () => {
      const { container } = render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      // Check that no badge with count text exists
      const button = screen.getByRole('button');
      const badges = Array.from(button.querySelectorAll('span')).filter((span) =>
        span.textContent?.match(/^\d+$/)
      );
      expect(badges).toHaveLength(0);
    });

    it('displays correct count in badge for large numbers', () => {
      const { container } = render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={15}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      // Badge now uses Tailwind classes
      const badge = container.querySelector('span.absolute.-top-1.-right-1');
      expect(badge).toHaveTextContent('15');
    });
  });

  describe('Unresolved Indicator', () => {
    it('shows unresolved dot when hasUnresolved = true', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={true}
          onClick={mockOnClick}
        />
      );

      const dot = screen.getByTestId('inline-comment-unresolved-dot') as HTMLSpanElement;
      expect(dot).toBeInTheDocument();
      // Dot now uses Tailwind classes (w-2 h-2 rounded-full)
      expect(dot).toHaveClass('w-2', 'h-2', 'rounded-full');
    });

    it('hides unresolved dot when hasUnresolved = false', () => {
      const { queryByTestId } = render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      expect(queryByTestId('inline-comment-unresolved-dot')).toBeNull();
    });

    it('shows pulse animation when unresolved', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={true}
          onClick={mockOnClick}
        />
      );

      const dot = screen.getByTestId('inline-comment-unresolved-dot') as HTMLSpanElement;
      expect(dot.style.animation).toContain('pulse');
      expect(dot.style.animation).toContain('infinite');
    });
  });

  describe('Visual States', () => {
    it('applies resolved styling when not unresolved', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');
      expect(button).toHaveStyle({
        background: '#f5f5f5',
        borderColor: '#ddd',
        color: '#666',
      });
    });

    it('applies unresolved styling when hasUnresolved = true', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={true}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');
      expect(button).toHaveStyle({
        background: '#fff3cd',
        borderColor: '#ff9800',
        color: '#ff9800',
      });
    });

    it('applies correct badge color when unresolved', () => {
      const { container } = render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={3}
          hasUnresolved={true}
          onClick={mockOnClick}
        />
      );

      // Badge uses Tailwind classes and inline style for background
      const badge = container.querySelector('span.absolute.-top-1.-right-1');
      expect(badge).toHaveStyle({ background: '#ff9800' });
    });

    it('applies correct badge color when resolved', () => {
      const { container } = render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={3}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      // Badge uses Tailwind classes and inline style for background
      const badge = container.querySelector('span.absolute.-top-1.-right-1');
      expect(badge).toHaveStyle({ background: '#0070f3' });
    });
  });

  describe('Click Handler', () => {
    it('calls onClick when button clicked', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');
      fireEvent.click(button);

      expect(mockOnClick).toHaveBeenCalledTimes(1);
    });

    it('calls onClick when Enter key pressed', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');
      fireEvent.keyDown(button, { key: 'Enter' });

      expect(mockOnClick).toHaveBeenCalledTimes(1);
    });

    it('calls onClick when Space key pressed', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');
      fireEvent.keyDown(button, { key: ' ' });

      expect(mockOnClick).toHaveBeenCalledTimes(1);
    });

    it('does not call onClick for other keys', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');
      fireEvent.keyDown(button, { key: 'a' });

      expect(mockOnClick).not.toHaveBeenCalled();
    });
  });

  describe('Tooltip', () => {
    it('shows tooltip on hover after 500ms delay', async () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
          previewText="This is a comment preview"
        />
      );

      const button = screen.getByRole('button');
      fireEvent.mouseEnter(button);

      // Before delay
      expect(screen.queryByText('This is a comment preview')).not.toBeInTheDocument();

      // After delay
      act(() => {
        vi.advanceTimersByTime(500);
      });

      await waitFor(() => {
        expect(screen.getByText('This is a comment preview')).toBeInTheDocument();
      });
    });

    it('hides tooltip when mouse leaves', async () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
          previewText="This is a comment preview"
        />
      );

      const button = screen.getByRole('button');

      // Show tooltip
      fireEvent.mouseEnter(button);
      act(() => {
        vi.advanceTimersByTime(500);
      });

      await waitFor(() => {
        expect(screen.getByText('This is a comment preview')).toBeInTheDocument();
      });

      // Hide tooltip
      fireEvent.mouseLeave(button);

      await waitFor(() => {
        expect(screen.queryByText('This is a comment preview')).not.toBeInTheDocument();
      });
    });

    it('cancels tooltip timer when mouse leaves before delay', async () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
          previewText="This is a comment preview"
        />
      );

      const button = screen.getByRole('button');

      fireEvent.mouseEnter(button);
      act(() => {
        vi.advanceTimersByTime(200); // Before 500ms
      });
      fireEvent.mouseLeave(button);
      act(() => {
        vi.advanceTimersByTime(300); // Complete the 500ms
      });

      expect(screen.queryByText('This is a comment preview')).not.toBeInTheDocument();
    });

    it('truncates long preview text to 100 characters', async () => {
      const longText = 'A'.repeat(150);
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
          previewText={longText}
        />
      );

      const button = screen.getByRole('button');
      fireEvent.mouseEnter(button);
      act(() => {
        vi.advanceTimersByTime(500);
      });

      await waitFor(() => {
        const tooltip = screen.getByText(/^A+\.\.\.$/);
        expect(tooltip.textContent?.length).toBe(103); // 100 chars + '...'
      });
    });

    it('does not show tooltip when previewText is empty', async () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
          previewText=""
        />
      );

      const button = screen.getByRole('button');
      fireEvent.mouseEnter(button);
      act(() => {
        vi.advanceTimersByTime(500);
      });

      await waitFor(() => {
        const tooltipDiv = button.parentElement?.querySelector(
          'div[style*="position: absolute"]'
        );
        expect(tooltipDiv).not.toBeInTheDocument();
      });
    });

    it('does not show tooltip when previewText is undefined', async () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');
      fireEvent.mouseEnter(button);
      act(() => {
        vi.advanceTimersByTime(500);
      });

      await waitFor(() => {
        const tooltipDiv = button.parentElement?.querySelector(
          'div[style*="position: absolute"]'
        );
        expect(tooltipDiv).not.toBeInTheDocument();
      });
    });

    it('shows tooltip with arrow', async () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
          previewText="Preview with arrow"
        />
      );

      const button = screen.getByRole('button');
      fireEvent.mouseEnter(button);
      act(() => {
        vi.advanceTimersByTime(500);
      });

      await waitFor(() => {
        const tooltip = screen.getByText('Preview with arrow');
        const tooltipContainer = tooltip.parentElement;
        const arrow = tooltipContainer?.querySelector('div[class*="border-t-"]');
        expect(arrow).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    it('has role="button" attribute', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      expect(screen.getByRole('button')).toHaveAttribute('role', 'button');
    });

    it('has aria-pressed="false" attribute', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      expect(screen.getByRole('button')).toHaveAttribute('aria-pressed', 'false');
    });

    it('has title attribute for native tooltip', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={2}
          hasUnresolved={false}
          onClick={mockOnClick}
          previewText="Test preview"
        />
      );

      expect(screen.getByRole('button')).toHaveAttribute('title', 'Test preview');
    });

    it('falls back to aria-label in title when no preview text', () => {
      render(
        <InlineCommentIndicator
          lineNumber={5}
          commentCount={2}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      expect(screen.getByRole('button')).toHaveAttribute(
        'title',
        'View 2 comments on line 5'
      );
    });

    it('has keyboard focus styles', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');

      // Focus
      fireEvent.focus(button);
      expect(button).toHaveStyle({
        boxShadow: '0 0 0 3px rgba(0,112,243,0.3)',
      });

      // Blur
      fireEvent.blur(button);
      expect(button).toHaveStyle({
        boxShadow: 'none',
      });
    });

    it('has hover transform effect', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      const button = screen.getByRole('button');

      // Hover
      fireEvent.mouseOver(button);
      expect(button).toHaveStyle({
        transform: 'scale(1.1)',
        boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
      });

      // Leave
      fireEvent.mouseOut(button);
      expect(button).toHaveStyle({
        transform: 'scale(1)',
        boxShadow: 'none',
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles lineNumber 0', () => {
      render(
        <InlineCommentIndicator
          lineNumber={0}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      expect(screen.getByLabelText('View 1 comment on line 0')).toBeInTheDocument();
    });

    it('handles very large line numbers', () => {
      render(
        <InlineCommentIndicator
          lineNumber={99999}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      expect(screen.getByLabelText('View 1 comment on line 99999')).toBeInTheDocument();
    });

    it('handles commentCount 0', () => {
      render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={0}
          hasUnresolved={false}
          onClick={mockOnClick}
        />
      );

      expect(screen.getByLabelText('View 0 comments on line 10')).toBeInTheDocument();
    });

    it('cleans up timer on unmount', () => {
      const { unmount } = render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={1}
          hasUnresolved={false}
          onClick={mockOnClick}
          previewText="Preview text"
        />
      );

      const button = screen.getByRole('button');
      fireEvent.mouseEnter(button);

      // Unmount before timer fires
      unmount();

      // Advance timers - should not cause errors
      act(() => {
        vi.advanceTimersByTime(500);
      });
    });

    it('handles both badge and unresolved dot simultaneously', () => {
      const { container } = render(
        <InlineCommentIndicator
          lineNumber={10}
          commentCount={5}
          hasUnresolved={true}
          onClick={mockOnClick}
        />
      );

      // Check for count badge
      const badge = Array.from(container.querySelectorAll('span')).find(
        (span) => span.textContent === '5'
      );
      expect(badge).toBeInTheDocument();

      // Check for unresolved dot
      const dot = container.querySelector('[data-testid="inline-comment-unresolved-dot"]');
      expect(dot).toBeInTheDocument();
    });
  });
});