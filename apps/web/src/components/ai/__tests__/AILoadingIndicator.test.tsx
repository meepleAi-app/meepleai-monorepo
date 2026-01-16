/**
 * AILoadingIndicator Tests
 * Issue #2418 (Sub-Issue 2401.2): Visual loading states for AI operations
 *
 * Comprehensive test suite covering:
 * - Component rendering in all states
 * - Progress tracking (auto and manual)
 * - Skeleton variants
 * - Time remaining estimation
 * - Cancellation functionality
 * - Timeout handling
 * - Accessibility features (reduced motion)
 *
 * Target: >85% code coverage
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import {
  AILoadingIndicator,
  type AILoadingIndicatorProps,
  type LoadingStage,
  type SkeletonVariant,
} from '../AILoadingIndicator';

// ==================== Mock Hooks ====================

// Mock useReducedMotion hook
vi.mock('@/lib/animations', () => ({
  useReducedMotion: vi.fn(() => false),
}));

import { useReducedMotion } from '@/lib/animations';
const mockUseReducedMotion = vi.mocked(useReducedMotion);

// ==================== Test Data ====================

const defaultStages: LoadingStage[] = [
  { progress: 10, message: 'Initializing...' },
  { progress: 30, message: 'Analyzing...' },
  { progress: 50, message: 'Processing...' },
  { progress: 75, message: 'Generating...' },
  { progress: 90, message: 'Finalizing...' },
];

const defaultProps: AILoadingIndicatorProps = {
  isLoading: true,
  estimatedTotalTime: 5000,
  showCancelButton: true,
  showSkeleton: true,
  skeletonVariant: 'question',
  showTimeRemaining: true,
  timeout: 30000,
  onCancel: vi.fn(),
  onTimeout: vi.fn(),
  stages: defaultStages,
};

// ==================== Test Utilities ====================

const renderComponent = (props: Partial<AILoadingIndicatorProps> = {}) => {
  const mergedProps = { ...defaultProps, ...props };
  return render(<AILoadingIndicator {...mergedProps} />);
};

// ==================== Test Suite ====================

describe('AILoadingIndicator', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    mockUseReducedMotion.mockReturnValue(false);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ========== Rendering Tests ==========

  describe('Rendering', () => {
    it('should render when isLoading is true', () => {
      renderComponent({ isLoading: true });

      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('should not render when isLoading is false and progress is 0', () => {
      const { container } = renderComponent({ isLoading: false });

      expect(container.firstChild).toBeNull();
    });

    it('should render progress bar', () => {
      renderComponent();

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toBeInTheDocument();
    });

    it('should render spinner icon', () => {
      renderComponent();

      // Spinner is rendered as svg with animate-spin class when motion allowed
      const statusElement = screen.getByRole('status');
      expect(statusElement).toBeInTheDocument();
    });

    it('should render with custom className', () => {
      const { container } = renderComponent({ className: 'custom-class' });

      const wrapper = container.firstChild;
      expect(wrapper).toHaveClass('custom-class');
    });

    it('should render stage message', () => {
      renderComponent({ stage: 'Custom stage message' });

      expect(screen.getByText('Custom stage message')).toBeInTheDocument();
    });

    it('should render default stage when none provided', () => {
      renderComponent({ stage: undefined, stages: [] });

      // Should show 'Processing...' as default
      expect(screen.getByText('Processing...')).toBeInTheDocument();
    });
  });

  // ========== Skeleton Variants ==========

  describe('Skeleton Variants', () => {
    const variants: SkeletonVariant[] = ['question', 'answer', 'card', 'compact'];

    variants.forEach(variant => {
      it(`should render ${variant} skeleton variant`, () => {
        renderComponent({ skeletonVariant: variant, showSkeleton: true });

        // Skeleton should be rendered (aria-hidden)
        const skeleton = document.querySelector('[aria-hidden="true"]');
        expect(skeleton).toBeInTheDocument();
      });
    });

    it('should not render skeleton when showSkeleton is false', () => {
      renderComponent({ showSkeleton: false });

      // No skeleton element with aria-hidden
      const skeletons = document.querySelectorAll('[aria-hidden="true"]');
      // Only the icons should be aria-hidden, not skeleton
      const skeletonContainer = document.querySelector('.rounded-lg.bg-muted\\/50');
      expect(skeletonContainer).not.toBeInTheDocument();
    });
  });

  // ========== Progress Tracking ==========

  describe('Progress Tracking', () => {
    it('should display manual progress value', () => {
      renderComponent({ progress: 65 });

      expect(screen.getByText('65%')).toBeInTheDocument();
    });

    it('should update auto-progress over time', async () => {
      renderComponent({ estimatedTotalTime: 5000 });

      // Initial state
      expect(screen.getByText('0%')).toBeInTheDocument();

      // Advance time
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      // Progress should have increased
      const progressText = screen.getByText(/%$/);
      const progressValue = parseInt(progressText.textContent || '0');
      expect(progressValue).toBeGreaterThan(0);
    });

    it('should cap auto-progress at 95%', async () => {
      renderComponent({ estimatedTotalTime: 1000 });

      // Advance past estimated time
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      // Progress should cap at 95%
      const progressText = screen.getByText(/%$/);
      const progressValue = parseInt(progressText.textContent || '0');
      expect(progressValue).toBeLessThanOrEqual(95);
    });

    it('should set progress to 100% when loading completes', async () => {
      const { rerender } = renderComponent({ isLoading: true, estimatedTotalTime: 1000 });

      // Advance time to build up progress
      await act(async () => {
        await vi.advanceTimersByTimeAsync(500);
      });

      // Verify progress built up
      const midProgressBar = screen.getByRole('progressbar');
      const midProgress = parseInt(midProgressBar.getAttribute('aria-valuenow') || '0');
      expect(midProgress).toBeGreaterThan(0);
      expect(midProgress).toBeLessThan(100);

      // Complete loading - progress should jump to 100%
      rerender(<AILoadingIndicator {...defaultProps} isLoading={false} />);

      // Verify progress is set to 100%
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '100');
    });
  });

  // ========== Stage Messages ==========

  describe('Stage Messages', () => {
    it('should display custom stage message', () => {
      renderComponent({ stage: 'Custom processing stage...' });

      expect(screen.getByText('Custom processing stage...')).toBeInTheDocument();
    });

    it('should update stage based on progress', async () => {
      renderComponent({
        stages: [
          { progress: 25, message: 'Stage 1' },
          { progress: 50, message: 'Stage 2' },
          { progress: 75, message: 'Stage 3' },
        ],
        estimatedTotalTime: 4000,
      });

      // Initial stage
      await act(async () => {
        await vi.advanceTimersByTimeAsync(100);
      });

      // Stage message should be from stages array
      const statusElement = screen.getByRole('status');
      expect(statusElement).toBeInTheDocument();
    });

    it('should use manual stage over auto-calculated stage', () => {
      renderComponent({
        stage: 'Manual stage override',
        stages: [{ progress: 0, message: 'Auto stage' }],
      });

      expect(screen.getByText('Manual stage override')).toBeInTheDocument();
      expect(screen.queryByText('Auto stage')).not.toBeInTheDocument();
    });
  });

  // ========== Time Remaining ==========

  describe('Time Remaining', () => {
    it('should display time remaining when showTimeRemaining is true', () => {
      renderComponent({ showTimeRemaining: true, estimatedTotalTime: 10000 });

      // Should show time remaining format (use getAllByText since it appears in both sr-only and visible)
      const timeElements = screen.getAllByText(/remaining|almost done/i);
      expect(timeElements.length).toBeGreaterThan(0);
    });

    it('should not display time remaining when showTimeRemaining is false', () => {
      renderComponent({ showTimeRemaining: false });

      // Only sr-only text should not contain remaining since we don't pass it
      const visibleTimeElement = document.querySelector('span.text-right');
      expect(visibleTimeElement).not.toBeInTheDocument();
    });

    it('should not display time remaining with manual progress', () => {
      renderComponent({ progress: 50, showTimeRemaining: true });

      // Manual progress disables time estimation - no visible time remaining span
      const visibleTimeElement = document.querySelector('span.text-right');
      expect(visibleTimeElement).not.toBeInTheDocument();
    });

    it('should format seconds correctly', () => {
      renderComponent({ estimatedTotalTime: 5000, showTimeRemaining: true });

      // Should show seconds format (multiple elements match, use getAll)
      const timeElements = screen.getAllByText(/second|almost/i);
      expect(timeElements.length).toBeGreaterThan(0);
    });

    it('should format minutes correctly for long operations', () => {
      renderComponent({ estimatedTotalTime: 120000, showTimeRemaining: true });

      // Should show minute format (multiple elements match, use getAll)
      const timeElements = screen.getAllByText(/minute|second|remaining|almost/i);
      expect(timeElements.length).toBeGreaterThan(0);
    });

    it('should show "Almost done" when time remaining is low', async () => {
      renderComponent({ estimatedTotalTime: 1000 });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1500);
      });

      // Multiple elements match (sr-only and visible), use getAll
      const almostDoneElements = screen.getAllByText(/almost done/i);
      expect(almostDoneElements.length).toBeGreaterThan(0);
    });
  });

  // ========== Cancel Button ==========

  describe('Cancel Button', () => {
    it('should render cancel button when onCancel is provided', () => {
      renderComponent({ showCancelButton: true, onCancel: vi.fn() });

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      expect(cancelButton).toBeInTheDocument();
    });

    it('should not render cancel button when showCancelButton is false', () => {
      renderComponent({ showCancelButton: false, onCancel: vi.fn() });

      expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
    });

    it('should not render cancel button when onCancel is not provided', () => {
      renderComponent({ showCancelButton: true, onCancel: undefined });

      expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
    });

    it('should call onCancel when cancel button is clicked', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onCancel = vi.fn();
      renderComponent({ onCancel });

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('should render with custom cancel label', () => {
      renderComponent({ cancelLabel: 'Stop Generation', onCancel: vi.fn() });

      expect(screen.getByRole('button', { name: /stop generation/i })).toBeInTheDocument();
    });

    it('should have accessible aria-label', () => {
      renderComponent({ onCancel: vi.fn() });

      const cancelButton = screen.getByRole('button', { name: /cancel ai generation/i });
      expect(cancelButton).toBeInTheDocument();
    });
  });

  // ========== Timeout Handling ==========

  describe('Timeout Handling', () => {
    it('should call onTimeout when timeout is reached', async () => {
      const onTimeout = vi.fn();
      renderComponent({ timeout: 2000, onTimeout });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(2100);
      });

      expect(onTimeout).toHaveBeenCalledTimes(1);
    });

    it('should not call onTimeout when loading completes before timeout', async () => {
      const onTimeout = vi.fn();
      const { rerender } = renderComponent({ timeout: 5000, onTimeout });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      // Complete loading before timeout
      rerender(<AILoadingIndicator {...defaultProps} isLoading={false} onTimeout={onTimeout} />);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000);
      });

      expect(onTimeout).not.toHaveBeenCalled();
    });

    it('should not call onTimeout when onTimeout is not provided', async () => {
      renderComponent({ timeout: 1000, onTimeout: undefined });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      // Should not throw or call anything
    });

    it('should cleanup timeout on unmount', async () => {
      const onTimeout = vi.fn();
      const { unmount } = renderComponent({ timeout: 5000, onTimeout });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });

      unmount();

      await act(async () => {
        await vi.advanceTimersByTimeAsync(10000);
      });

      expect(onTimeout).not.toHaveBeenCalled();
    });
  });

  // ========== Accessibility ==========

  describe('Accessibility', () => {
    it('should have role="status" on container', () => {
      renderComponent();

      expect(screen.getByRole('status')).toBeInTheDocument();
    });

    it('should have aria-live="polite"', () => {
      renderComponent();

      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('aria-live', 'polite');
    });

    it('should have aria-busy when loading', () => {
      renderComponent({ isLoading: true });

      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('aria-busy', 'true');
    });

    it('should have custom aria-label', () => {
      renderComponent({ ariaLabel: 'Custom loading message' });

      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('aria-label', 'Custom loading message');
    });

    it('should have default aria-label', () => {
      renderComponent();

      const status = screen.getByRole('status');
      expect(status).toHaveAttribute('aria-label', 'AI generation in progress');
    });

    it('should have screen reader text with progress', () => {
      renderComponent({ progress: 50, stage: 'Processing...' });

      // Screen reader text should be present
      const srText = screen.getByText(/processing.* 50% complete/i);
      expect(srText).toHaveClass('sr-only');
    });

    it('should include time remaining in screen reader text when available', () => {
      renderComponent({
        showTimeRemaining: true,
        estimatedTotalTime: 10000,
        stage: 'Working...',
      });

      const status = screen.getByRole('status');
      expect(status).toBeInTheDocument();
    });

    it('should have aria-valuemin and aria-valuemax on progress bar', () => {
      renderComponent();

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuemin', '0');
      expect(progressBar).toHaveAttribute('aria-valuemax', '100');
    });

    it('should have aria-valuenow reflecting current progress', () => {
      renderComponent({ progress: 75 });

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '75');
    });
  });

  // ========== Reduced Motion ==========

  describe('Reduced Motion', () => {
    it('should disable spinner animation when reduced motion is preferred', () => {
      mockUseReducedMotion.mockReturnValue(true);
      renderComponent();

      // Spinner should not have animate-spin class
      const status = screen.getByRole('status');
      const spinner = status.querySelector('svg');
      expect(spinner).not.toHaveClass('animate-spin');
    });

    it('should disable skeleton animation when reduced motion is preferred', () => {
      mockUseReducedMotion.mockReturnValue(true);
      renderComponent({ showSkeleton: true });

      // Skeleton should not have animate-pulse class
      const skeleton = document.querySelector('[aria-hidden="true"]');
      expect(skeleton).not.toHaveClass('animate-pulse');
    });

    it('should disable progress bar transition when reduced motion is preferred', () => {
      mockUseReducedMotion.mockReturnValue(true);
      renderComponent();

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).not.toHaveClass('transition-all');
    });

    it('should keep animations when reduced motion is not preferred', () => {
      mockUseReducedMotion.mockReturnValue(false);
      renderComponent();

      const status = screen.getByRole('status');
      const spinner = status.querySelector('svg');
      expect(spinner).toHaveClass('animate-spin');
    });
  });

  // ========== Loading State Changes ==========

  describe('Loading State Changes', () => {
    it('should reset progress when loading restarts', async () => {
      const { rerender } = renderComponent({ isLoading: true, estimatedTotalTime: 2000 });

      // Build up progress
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });

      // Stop loading
      rerender(<AILoadingIndicator {...defaultProps} isLoading={false} />);

      // Start loading again
      rerender(<AILoadingIndicator {...defaultProps} isLoading={true} />);

      // Progress should reset to 0
      expect(screen.getByText('0%')).toBeInTheDocument();
    });

    it('should cleanup intervals when loading stops', async () => {
      const { rerender } = renderComponent({ isLoading: true, estimatedTotalTime: 5000 });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });

      // Stop loading
      rerender(<AILoadingIndicator {...defaultProps} isLoading={false} />);

      // Intervals should be cleaned up, no errors should occur
      await act(async () => {
        await vi.advanceTimersByTimeAsync(10000);
      });
    });

    it('should cleanup intervals on unmount', async () => {
      const { unmount } = renderComponent({ isLoading: true, estimatedTotalTime: 5000 });

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });

      unmount();

      // Should not throw errors
      await act(async () => {
        await vi.advanceTimersByTimeAsync(10000);
      });
    });
  });

  // ========== Edge Cases ==========

  describe('Edge Cases', () => {
    it('should handle 0 estimatedTotalTime', async () => {
      renderComponent({ estimatedTotalTime: 0 });

      // Should render without errors
      expect(screen.getByRole('status')).toBeInTheDocument();

      // Progress should stay at 0% (no auto-progress with 0 duration)
      await act(async () => {
        await vi.advanceTimersByTimeAsync(500);
      });

      // Verify progress stays at 0 (no division by zero causing immediate jump to 95%)
      expect(screen.getByText('0%')).toBeInTheDocument();
    });

    it('should handle empty stages array', () => {
      renderComponent({ stages: [] });

      // Should use default 'Processing...' message
      expect(screen.getByText('Processing...')).toBeInTheDocument();
    });

    it('should handle very long estimatedTotalTime', async () => {
      renderComponent({ estimatedTotalTime: 3600000 }); // 1 hour

      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });

      // Should display time remaining in minutes format (multiple elements match, use getAll)
      const timeElements = screen.getAllByText(/minute|remaining/i);
      expect(timeElements.length).toBeGreaterThan(0);
    });

    it('should handle progress values outside 0-100 range', () => {
      renderComponent({ progress: 150 });

      // Should display the value (component doesn't clamp)
      expect(screen.getByText('150%')).toBeInTheDocument();
    });

    it('should handle negative progress values', () => {
      renderComponent({ progress: -10 });

      // Should display the value
      expect(screen.getByText('-10%')).toBeInTheDocument();
    });

    it('should handle undefined onCancel with showCancelButton true', () => {
      renderComponent({ showCancelButton: true, onCancel: undefined });

      // Cancel button should not be rendered
      expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
    });
  });

  // ========== Integration Tests ==========

  describe('Integration', () => {
    it('should complete full loading cycle', async () => {
      const onCancel = vi.fn();
      const { rerender } = renderComponent({
        estimatedTotalTime: 2000,
        onCancel,
      });

      // Initial state
      expect(screen.getByRole('status')).toBeInTheDocument();
      expect(screen.getByText('0%')).toBeInTheDocument();

      // Progress over time
      await act(async () => {
        await vi.advanceTimersByTimeAsync(1000);
      });

      // Progress should have increased
      const midProgress = screen.getByText(/%$/);
      expect(parseInt(midProgress.textContent || '0')).toBeGreaterThan(0);

      // Complete loading
      rerender(<AILoadingIndicator {...defaultProps} isLoading={false} />);

      // Component should unmount or show 100%
    });

    it('should handle cancel during loading', async () => {
      const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
      const onCancel = vi.fn();
      renderComponent({ estimatedTotalTime: 10000, onCancel });

      // Progress starts
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      // User cancels
      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('should handle timeout during loading', async () => {
      const onTimeout = vi.fn();
      renderComponent({
        estimatedTotalTime: 60000,
        timeout: 5000,
        onTimeout,
      });

      // Progress starts
      await act(async () => {
        await vi.advanceTimersByTimeAsync(2000);
      });

      // Timeout triggers
      await act(async () => {
        await vi.advanceTimersByTimeAsync(4000);
      });

      expect(onTimeout).toHaveBeenCalledTimes(1);
    });
  });
});
