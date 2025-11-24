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

