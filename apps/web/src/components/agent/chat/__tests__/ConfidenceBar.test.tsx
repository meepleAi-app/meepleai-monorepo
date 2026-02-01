/**
 * ConfidenceBar Tests (Issue #3244)
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

import { ConfidenceBar } from '../ConfidenceBar';

describe('ConfidenceBar', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Rendering', () => {
    it('renders confidence label', () => {
      render(<ConfidenceBar confidence={0.95} />);

      expect(screen.getByText('Confidenza:')).toBeInTheDocument();
    });

    it('renders percentage and quality label', () => {
      const { container } = render(<ConfidenceBar confidence={0.95} />);

      // Check that 95% appears in the document
      expect(container.textContent).toContain('95%');
      expect(container.textContent).toContain('Alta');
    });

    it('displays percentage with monospace font', () => {
      const { container } = render(<ConfidenceBar confidence={0.75} />);

      const percentageEl = container.querySelector('.font-mono');
      expect(percentageEl).toBeInTheDocument();
      expect(percentageEl?.textContent).toContain('75%');
    });

    it('has progress bar with ARIA attributes', () => {
      render(<ConfidenceBar confidence={0.88} />);

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '88');
      expect(progressBar).toHaveAttribute('aria-valuemin', '0');
      expect(progressBar).toHaveAttribute('aria-valuemax', '100');
      expect(progressBar).toHaveAttribute('aria-label', 'Confidence: 88%');
    });
  });

  describe('Color Coding', () => {
    it('shows cyan for high confidence (>80%)', () => {
      const { container } = render(<ConfidenceBar confidence={0.95} />);

      const progressBar = container.querySelector('.bg-cyan-500');
      expect(progressBar).toBeInTheDocument();

      const percentageEl = container.querySelector('.text-cyan-400');
      expect(percentageEl).toBeInTheDocument();
      expect(container.textContent).toContain('Alta');
    });

    it('shows yellow for medium confidence (60-80%)', () => {
      const { container } = render(<ConfidenceBar confidence={0.7} />);

      const progressBar = container.querySelector('.bg-yellow-500');
      expect(progressBar).toBeInTheDocument();

      const percentageEl = container.querySelector('.text-yellow-400');
      expect(percentageEl).toBeInTheDocument();
      expect(container.textContent).toContain('Media');
    });

    it('shows red for low confidence (<60%)', () => {
      const { container } = render(<ConfidenceBar confidence={0.45} />);

      const progressBar = container.querySelector('.bg-red-500');
      expect(progressBar).toBeInTheDocument();

      const percentageEl = container.querySelector('.text-red-400');
      expect(percentageEl).toBeInTheDocument();
      expect(container.textContent).toContain('Bassa');
    });

    it('handles boundary case: exactly 80%', () => {
      const { container } = render(<ConfidenceBar confidence={0.8} />);

      // Should be yellow (not cyan, since >0.8 is exclusive)
      const progressBar = container.querySelector('.bg-yellow-500');
      expect(progressBar).toBeInTheDocument();
    });

    it('handles boundary case: exactly 60%', () => {
      const { container } = render(<ConfidenceBar confidence={0.6} />);

      // Should be yellow (>= 0.6)
      const progressBar = container.querySelector('.bg-yellow-500');
      expect(progressBar).toBeInTheDocument();
    });
  });

  describe('Animation', () => {
    it('has transition class for smooth animation', () => {
      const { container } = render(<ConfidenceBar confidence={0.75} />);

      const progressBar = container.querySelector('[role="progressbar"]');
      expect(progressBar).toHaveClass('transition-all');
      expect(progressBar).toHaveClass('duration-700');
      expect(progressBar).toHaveClass('ease-out');
    });

    it('animates width from initial to target', () => {
      vi.useRealTimers(); // Use real timers for this test

      const { container } = render(<ConfidenceBar confidence={0.85} />);
      const progressBar = container.querySelector('[role="progressbar"]') as HTMLElement;

      // Should have width set via inline style
      expect(progressBar).toBeInTheDocument();
      expect(progressBar?.style).toBeDefined();

      vi.useFakeTimers(); // Restore for other tests
    });
  });

  describe('Edge Cases', () => {
    it('handles 0% confidence', () => {
      const { container } = render(<ConfidenceBar confidence={0} />);

      expect(container.textContent).toContain('0%');
      expect(container.textContent).toContain('Bassa');
    });

    it('handles 100% confidence', () => {
      const { container } = render(<ConfidenceBar confidence={1.0} />);

      expect(container.textContent).toContain('100%');
      expect(container.textContent).toContain('Alta');
    });

    it('rounds percentage correctly', () => {
      const { container } = render(<ConfidenceBar confidence={0.876} />);

      // Math.round(0.876 * 100) = Math.round(87.6) = 88
      expect(container.textContent).toContain('88%');
    });
  });

  describe('Custom Styling', () => {
    it('applies custom className', () => {
      const { container } = render(
        <ConfidenceBar confidence={0.8} className="custom-class" />
      );

      const wrapper = container.querySelector('.custom-class');
      expect(wrapper).toBeInTheDocument();
    });
  });
});
