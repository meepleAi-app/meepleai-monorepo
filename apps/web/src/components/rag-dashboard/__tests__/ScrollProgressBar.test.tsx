/**
 * Tests for ScrollProgressBar component
 * Issue #3451: Breadcrumbs and Scroll Progress
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ScrollProgressBar } from '../ScrollProgressBar';

// Mock useScrollProgress hook
vi.mock('../hooks/useScrollProgress', () => ({
  useScrollProgress: vi.fn(() => 0),
}));

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, className, style, initial, animate, transition, ...props }: React.ComponentProps<'div'> & {
      initial?: object;
      animate?: object;
      transition?: object
    }) => (
      <div
        className={className}
        style={{
          ...style,
          ...(animate as React.CSSProperties)
        }}
        data-testid="motion-div"
        {...props}
      >
        {children}
      </div>
    ),
  },
}));

import { useScrollProgress } from '../hooks/useScrollProgress';

describe('ScrollProgressBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the progress bar container', () => {
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toBeInTheDocument();
    });

    it('should render with correct base classes', () => {
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveClass('sticky', 'top-0', 'z-50', 'h-1', 'bg-muted/50');
    });

    it('should render the animated progress indicator', () => {
      render(<ScrollProgressBar />);
      const motionDiv = screen.getByTestId('motion-div');
      expect(motionDiv).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      render(<ScrollProgressBar className="custom-class" />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveClass('custom-class');
    });
  });

  // =========================================================================
  // Accessibility Tests
  // =========================================================================

  describe('Accessibility', () => {
    it('should have progressbar role', () => {
      render(<ScrollProgressBar />);
      expect(screen.getByRole('progressbar')).toBeInTheDocument();
    });

    it('should have aria-valuenow attribute', () => {
      vi.mocked(useScrollProgress).mockReturnValue(50);
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '50');
    });

    it('should have aria-valuemin attribute set to 0', () => {
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuemin', '0');
    });

    it('should have aria-valuemax attribute set to 100', () => {
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuemax', '100');
    });

    it('should have aria-label for screen readers', () => {
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-label', 'Page scroll progress');
    });
  });

  // =========================================================================
  // Progress Indicator Tests
  // =========================================================================

  describe('Progress indicator', () => {
    it('should show 0% width at top of page', () => {
      vi.mocked(useScrollProgress).mockReturnValue(0);
      render(<ScrollProgressBar />);
      const motionDiv = screen.getByTestId('motion-div');
      expect(motionDiv).toHaveStyle({ width: '0%' });
    });

    it('should show 50% width when scrolled halfway', () => {
      vi.mocked(useScrollProgress).mockReturnValue(50);
      render(<ScrollProgressBar />);
      const motionDiv = screen.getByTestId('motion-div');
      expect(motionDiv).toHaveStyle({ width: '50%' });
    });

    it('should show 100% width at bottom of page', () => {
      vi.mocked(useScrollProgress).mockReturnValue(100);
      render(<ScrollProgressBar />);
      const motionDiv = screen.getByTestId('motion-div');
      expect(motionDiv).toHaveStyle({ width: '100%' });
    });

    it('should have gradient background class', () => {
      render(<ScrollProgressBar />);
      const motionDiv = screen.getByTestId('motion-div');
      expect(motionDiv).toHaveClass(
        'h-full',
        'bg-gradient-to-r',
        'from-orange-500',
        'via-primary',
        'to-purple-500'
      );
    });
  });

  // =========================================================================
  // Progress Value Updates
  // =========================================================================

  describe('Progress value updates', () => {
    it('should update aria-valuenow when progress changes', () => {
      vi.mocked(useScrollProgress).mockReturnValue(25);
      const { rerender } = render(<ScrollProgressBar />);

      let progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '25');

      vi.mocked(useScrollProgress).mockReturnValue(75);
      rerender(<ScrollProgressBar />);

      progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '75');
    });

    it('should update width when progress changes', () => {
      vi.mocked(useScrollProgress).mockReturnValue(10);
      const { rerender } = render(<ScrollProgressBar />);

      let motionDiv = screen.getByTestId('motion-div');
      expect(motionDiv).toHaveStyle({ width: '10%' });

      vi.mocked(useScrollProgress).mockReturnValue(90);
      rerender(<ScrollProgressBar />);

      motionDiv = screen.getByTestId('motion-div');
      expect(motionDiv).toHaveStyle({ width: '90%' });
    });
  });

  // =========================================================================
  // Edge Cases
  // =========================================================================

  describe('Edge cases', () => {
    it('should handle 0 progress correctly', () => {
      vi.mocked(useScrollProgress).mockReturnValue(0);
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '0');
    });

    it('should handle 100 progress correctly', () => {
      vi.mocked(useScrollProgress).mockReturnValue(100);
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '100');
    });
  });

  // =========================================================================
  // Styling Tests
  // =========================================================================

  describe('Styling', () => {
    it('should be sticky positioned', () => {
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveClass('sticky');
    });

    it('should have high z-index for visibility', () => {
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveClass('z-50');
    });

    it('should have thin height', () => {
      render(<ScrollProgressBar />);
      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveClass('h-1');
    });
  });
});
