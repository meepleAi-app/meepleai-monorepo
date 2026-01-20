/**
 * Tests for Progress component
 * Issue #1951: Add coverage for shadcn/ui components
 */

import { render, screen } from '@testing-library/react';
import { Progress } from '../feedback/progress';

describe('Progress', () => {
  describe('Rendering', () => {
    it('renders with default props', () => {
      const { container } = render(<Progress value={0} />);
      const progressRoot = container.querySelector('[role="progressbar"]');
      expect(progressRoot).toBeInTheDocument();
    });

    it('renders with specific value', () => {
      const { container } = render(<Progress value={50} />);
      const progressRoot = container.querySelector('[role="progressbar"]');
      expect(progressRoot).toHaveAttribute('data-value', '50');
    });

    it('renders at 100% completion', () => {
      const { container } = render(<Progress value={100} />);
      const progressRoot = container.querySelector('[role="progressbar"]');
      expect(progressRoot).toHaveAttribute('data-value', '100');
    });

    it('renders at 0% (no progress)', () => {
      const { container } = render(<Progress value={0} />);
      const progressRoot = container.querySelector('[role="progressbar"]');
      expect(progressRoot).toHaveAttribute('data-value', '0');
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      const { container } = render(<Progress value={50} className="custom-class" />);
      const progressRoot = container.querySelector('[role="progressbar"]');
      expect(progressRoot).toHaveClass('custom-class');
    });

    it('applies default styling classes', () => {
      const { container } = render(<Progress value={50} />);
      const progressRoot = container.querySelector('[role="progressbar"]');
      expect(progressRoot).toHaveClass(
        'relative',
        'h-2',
        'w-full',
        'overflow-hidden',
        'rounded-full'
      );
    });
  });

  describe('Value Handling', () => {
    it('handles undefined value (fallback to 0)', () => {
      const { container } = render(<Progress value={undefined} />);
      const indicator =
        container.querySelector('[data-state="loading"]') ||
        container.querySelector('[style*="translateX"]');
      expect(indicator).toBeInTheDocument();
    });

    it('handles intermediate values', () => {
      const { container } = render(<Progress value={75} />);
      const progressRoot = container.querySelector('[role="progressbar"]');
      expect(progressRoot).toHaveAttribute('data-value', '75');
    });
  });
});
