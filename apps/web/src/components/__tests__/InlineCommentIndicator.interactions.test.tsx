import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { InlineCommentIndicator } from '../comments/InlineCommentIndicator';

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