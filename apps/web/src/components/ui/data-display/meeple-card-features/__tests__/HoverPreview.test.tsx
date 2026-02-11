/**
 * HoverPreview Tests - Issue #3827, #3859
 *
 * Test coverage for HoverPreview component:
 * - Rendering (static data, all fields, empty states)
 * - Async fetching (loading, success, error)
 * - Disabled state
 * - Accessibility
 * - Edge cases (cleanup, rapid toggles)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { HoverPreview, type HoverPreviewData } from '../HoverPreview';

// Radix Popover uses Portal which needs this
beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

afterEach(() => {
  vi.useRealTimers();
});

const fullPreviewData: HoverPreviewData = {
  description: 'A strategy game of trading and building on the island of Catan.',
  designer: 'Klaus Teuber',
  complexity: 2.3,
  weight: 'Medium',
  categories: ['Strategy', 'Economic', 'Negotiation'],
  mechanics: ['Dice Rolling', 'Trading', 'Route Building'],
};

function renderHoverPreview(props: Partial<React.ComponentProps<typeof HoverPreview>> = {}) {
  const defaultProps = {
    gameId: 'game-123',
    children: <button data-testid="trigger">Hover me</button>,
    ...props,
  };

  return render(<HoverPreview {...defaultProps} />);
}

describe('HoverPreview', () => {
  // ==========================================================================
  // Rendering Tests
  // ==========================================================================

  describe('Rendering', () => {
    it('should render children as trigger', () => {
      renderHoverPreview();

      expect(screen.getByTestId('trigger')).toBeInTheDocument();
    });

    it('should show all data fields when popover is opened with full data', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({ previewData: fullPreviewData });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        // Description
        expect(
          screen.getByText('A strategy game of trading and building on the island of Catan.')
        ).toBeInTheDocument();
        // Designer
        expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
        // Complexity
        expect(screen.getByText('2.3/5')).toBeInTheDocument();
        // Weight badge
        expect(screen.getByText('Medium')).toBeInTheDocument();
        // Categories
        expect(screen.getByText('Strategy')).toBeInTheDocument();
        expect(screen.getByText('Economic')).toBeInTheDocument();
        expect(screen.getByText('Negotiation')).toBeInTheDocument();
        // Mechanics
        expect(screen.getByText('Dice Rolling')).toBeInTheDocument();
        expect(screen.getByText('Trading')).toBeInTheDocument();
        expect(screen.getByText('Route Building')).toBeInTheDocument();
      });
    });

    it('should show "No preview available" when data is null and no fetcher', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview();

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        expect(screen.getByText('No preview available')).toBeInTheDocument();
      });
    });

    it('should render only description when other fields are absent', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { description: 'Just a description' },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        expect(screen.getByText('Just a description')).toBeInTheDocument();
      });

      // Should not render designer/complexity/weight/categories/mechanics sections
      expect(screen.queryByText('Designer:')).not.toBeInTheDocument();
      expect(screen.queryByText('Complexity:')).not.toBeInTheDocument();
      expect(screen.queryByText('Weight:')).not.toBeInTheDocument();
      expect(screen.queryByText('Categories')).not.toBeInTheDocument();
      expect(screen.queryByText('Mechanics')).not.toBeInTheDocument();
    });

    it('should render designer and complexity without description', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { designer: 'Reiner Knizia', complexity: 3.5 },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        expect(screen.getByText('Reiner Knizia')).toBeInTheDocument();
        expect(screen.getByText('3.5/5')).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Weight Badge Styling
  // ==========================================================================

  describe('Weight Badge', () => {
    it('should render Light weight with green styling', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { weight: 'Light' },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        const badge = screen.getByText('Light');
        expect(badge).toHaveClass('bg-green-100', 'text-green-900');
      });
    });

    it('should render Medium weight with amber styling', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { weight: 'Medium' },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        const badge = screen.getByText('Medium');
        expect(badge).toHaveClass('bg-amber-100', 'text-amber-900');
      });
    });

    it('should render Heavy weight with red styling', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { weight: 'Heavy' },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        const badge = screen.getByText('Heavy');
        expect(badge).toHaveClass('bg-red-100', 'text-red-900');
      });
    });
  });

  // ==========================================================================
  // Async Fetching
  // ==========================================================================

  describe('Async Fetching', () => {
    it('should show loading skeleton while fetching', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const slowFetch = vi.fn(
        () => new Promise<HoverPreviewData>((resolve) => setTimeout(() => resolve(fullPreviewData), 2000))
      );

      renderHoverPreview({ onFetchPreview: slowFetch });

      await user.click(screen.getByTestId('trigger'));

      // Advance past the delay
      await act(async () => {
        vi.advanceTimersByTime(600);
      });

      await waitFor(() => {
        const skeletons = document.querySelectorAll('.animate-pulse');
        expect(skeletons.length).toBeGreaterThan(0);
      });
    });

    it('should display fetched data after successful load', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const fetchPreview = vi.fn().mockResolvedValue(fullPreviewData);

      renderHoverPreview({ onFetchPreview: fetchPreview });

      await user.click(screen.getByTestId('trigger'));

      // Advance past the delay (default 500ms)
      await act(async () => {
        vi.advanceTimersByTime(600);
      });

      await waitFor(() => {
        expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
      });

      expect(fetchPreview).toHaveBeenCalledWith('game-123');
    });

    it('should show error message on fetch failure', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const failingFetch = vi.fn().mockRejectedValue(new Error('Network error'));

      renderHoverPreview({ onFetchPreview: failingFetch });

      await user.click(screen.getByTestId('trigger'));

      await act(async () => {
        vi.advanceTimersByTime(600);
      });

      await waitFor(() => {
        expect(screen.getByText('Error loading preview')).toBeInTheDocument();
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });
    });

    it('should show generic error message for non-Error exceptions', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const failingFetch = vi.fn().mockRejectedValue('string error');

      renderHoverPreview({ onFetchPreview: failingFetch });

      await user.click(screen.getByTestId('trigger'));

      await act(async () => {
        vi.advanceTimersByTime(600);
      });

      await waitFor(() => {
        expect(screen.getByText('Failed to load preview')).toBeInTheDocument();
      });
    });

    it('should respect custom delay', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const fetchPreview = vi.fn().mockResolvedValue(fullPreviewData);

      renderHoverPreview({ onFetchPreview: fetchPreview, delay: 1000 });

      await user.click(screen.getByTestId('trigger'));

      // At 500ms, fetch should not have been called yet
      await act(async () => {
        vi.advanceTimersByTime(500);
      });
      expect(fetchPreview).not.toHaveBeenCalled();

      // At 1100ms, fetch should have been triggered
      await act(async () => {
        vi.advanceTimersByTime(600);
      });
      expect(fetchPreview).toHaveBeenCalledTimes(1);
    });

    it('should not fetch when previewData is already provided', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const fetchPreview = vi.fn().mockResolvedValue(fullPreviewData);

      renderHoverPreview({
        previewData: { description: 'Already have data' },
        onFetchPreview: fetchPreview,
      });

      await user.click(screen.getByTestId('trigger'));

      await act(async () => {
        vi.advanceTimersByTime(1000);
      });

      expect(fetchPreview).not.toHaveBeenCalled();
      expect(screen.getByText('Already have data')).toBeInTheDocument();
    });

    it('should not fetch when disabled', async () => {
      const fetchPreview = vi.fn().mockResolvedValue(fullPreviewData);

      renderHoverPreview({ onFetchPreview: fetchPreview, disabled: true });

      await act(async () => {
        vi.advanceTimersByTime(1000);
      });

      expect(fetchPreview).not.toHaveBeenCalled();
    });
  });

  // ==========================================================================
  // Disabled State
  // ==========================================================================

  describe('Disabled State', () => {
    it('should render only children when disabled', () => {
      renderHoverPreview({ disabled: true, previewData: fullPreviewData });

      expect(screen.getByTestId('trigger')).toBeInTheDocument();
      // No popover content should be in the DOM
      expect(screen.queryByText('Klaus Teuber')).not.toBeInTheDocument();
    });

    it('should not open popover when disabled', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({ disabled: true, previewData: fullPreviewData });

      await user.click(screen.getByTestId('trigger'));

      expect(
        screen.queryByText('A strategy game of trading and building on the island of Catan.')
      ).not.toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Categories & Mechanics
  // ==========================================================================

  describe('Categories & Mechanics', () => {
    it('should render category labels', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { categories: ['Strategy', 'Family'] },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        expect(screen.getByText('Categories')).toBeInTheDocument();
        expect(screen.getByText('Strategy')).toBeInTheDocument();
        expect(screen.getByText('Family')).toBeInTheDocument();
      });
    });

    it('should render mechanic labels', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { mechanics: ['Worker Placement', 'Area Control'] },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        expect(screen.getByText('Mechanics')).toBeInTheDocument();
        expect(screen.getByText('Worker Placement')).toBeInTheDocument();
        expect(screen.getByText('Area Control')).toBeInTheDocument();
      });
    });

    it('should not render categories section when array is empty', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { description: 'A game', categories: [] },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        expect(screen.getByText('A game')).toBeInTheDocument();
      });

      expect(screen.queryByText('Categories')).not.toBeInTheDocument();
    });

    it('should not render mechanics section when array is empty', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { description: 'A game', mechanics: [] },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        expect(screen.getByText('A game')).toBeInTheDocument();
      });

      expect(screen.queryByText('Mechanics')).not.toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Edge Cases
  // ==========================================================================

  describe('Edge Cases', () => {
    it('should cleanup timeout on unmount during fetch', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const fetchPreview = vi.fn().mockResolvedValue(fullPreviewData);

      const { unmount } = renderHoverPreview({ onFetchPreview: fetchPreview });

      await user.click(screen.getByTestId('trigger'));

      // Unmount before delay fires
      unmount();

      await act(async () => {
        vi.advanceTimersByTime(1000);
      });

      // Fetch should not have been called since we unmounted before delay
      expect(fetchPreview).not.toHaveBeenCalled();
    });

    it('should handle complexity of exactly 0', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      renderHoverPreview({
        previewData: { complexity: 0 },
      });

      await user.click(screen.getByTestId('trigger'));

      await waitFor(() => {
        expect(screen.getByText('0/5')).toBeInTheDocument();
      });
    });

    it('should not re-fetch if data was already loaded', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const fetchPreview = vi.fn().mockResolvedValue(fullPreviewData);

      renderHoverPreview({ onFetchPreview: fetchPreview });

      // First open - triggers fetch
      await user.click(screen.getByTestId('trigger'));
      await act(async () => {
        vi.advanceTimersByTime(600);
      });
      await waitFor(() => {
        expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
      });

      // Close
      await user.click(screen.getByTestId('trigger'));

      // Re-open - should NOT re-fetch
      await user.click(screen.getByTestId('trigger'));
      await act(async () => {
        vi.advanceTimersByTime(600);
      });

      expect(fetchPreview).toHaveBeenCalledTimes(1);
    });
  });
});
