/**
 * ConfidenceBadge Component Tests - Issue #4163
 *
 * Test coverage:
 * - Confidence level color coding
 * - Icon display
 * - Tooltip descriptions
 * - Size variants
 *
 * Target: 100% coverage
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect } from 'vitest';

import { ConfidenceBadge } from '../ConfidenceBadge';

describe('ConfidenceBadge', () => {
  describe('Confidence Level Color Coding', () => {
    it('displays high confidence badge (green) for ≥80%', () => {
      render(<ConfidenceBadge confidence={85} />);

      expect(screen.getByTestId('confidence-badge-high')).toBeInTheDocument();
      expect(screen.getByText(/alta confidenza/i)).toBeInTheDocument();
      expect(screen.getByText(/85%/)).toBeInTheDocument();
    });

    it('displays medium confidence badge (yellow) for 50-79%', () => {
      render(<ConfidenceBadge confidence={65} />);

      expect(screen.getByTestId('confidence-badge-medium')).toBeInTheDocument();
      expect(screen.getByText(/media confidenza/i)).toBeInTheDocument();
      expect(screen.getByText(/65%/)).toBeInTheDocument();
    });

    it('displays low confidence badge (red) for <50%', () => {
      render(<ConfidenceBadge confidence={30} />);

      expect(screen.getByTestId('confidence-badge-low')).toBeInTheDocument();
      expect(screen.getByText(/bassa confidenza/i)).toBeInTheDocument();
      expect(screen.getByText(/30%/)).toBeInTheDocument();
    });

    it('handles exact threshold values correctly', () => {
      const { rerender } = render(<ConfidenceBadge confidence={80} />);
      expect(screen.getByTestId('confidence-badge-high')).toBeInTheDocument();

      rerender(<ConfidenceBadge confidence={79} />);
      expect(screen.getByTestId('confidence-badge-medium')).toBeInTheDocument();

      rerender(<ConfidenceBadge confidence={50} />);
      expect(screen.getByTestId('confidence-badge-medium')).toBeInTheDocument();

      rerender(<ConfidenceBadge confidence={49} />);
      expect(screen.getByTestId('confidence-badge-low')).toBeInTheDocument();
    });
  });

  describe('Icon Display', () => {
    it('displays CheckCircle icon for high confidence', () => {
      const { container } = render(<ConfidenceBadge confidence={90} />);

      // CheckCircle icon is present
      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });

    it('displays AlertTriangle icon for medium confidence', () => {
      const { container } = render(<ConfidenceBadge confidence={60} />);

      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });

    it('displays AlertCircle icon for low confidence', () => {
      const { container } = render(<ConfidenceBadge confidence={20} />);

      const icon = container.querySelector('svg');
      expect(icon).toBeInTheDocument();
    });
  });

  describe('Tooltip', () => {
    it('shows tooltip with quality description on hover (high)', async () => {
      const user = userEvent.setup();
      render(<ConfidenceBadge confidence={85} />);

      const badge = screen.getByTestId('confidence-badge-high');
      await user.hover(badge);

      // Tooltip may render multiple times (visible + screen reader)
      const tooltips = await screen.findAllByText(/estrazione di alta qualità/i);
      expect(tooltips.length).toBeGreaterThan(0);
    });

    it('shows tooltip with quality description on hover (medium)', async () => {
      const user = userEvent.setup();
      render(<ConfidenceBadge confidence={65} />);

      const badge = screen.getByTestId('confidence-badge-medium');
      await user.hover(badge);

      const tooltips = await screen.findAllByText(/alcuni campi potrebbero essere mancanti/i);
      expect(tooltips.length).toBeGreaterThan(0);
    });

    it('shows tooltip with quality description on hover (low)', async () => {
      const user = userEvent.setup();
      render(<ConfidenceBadge confidence={30} />);

      const badge = screen.getByTestId('confidence-badge-low');
      await user.hover(badge);

      const tooltips = await screen.findAllByText(/revisione manuale fortemente consigliata/i);
      expect(tooltips.length).toBeGreaterThan(0);
    });
  });

  describe('Size Variants', () => {
    it('renders medium size by default', () => {
      const { container } = render(<ConfidenceBadge confidence={80} />);

      const badge = container.querySelector('[data-testid="confidence-badge-high"]');
      expect(badge).toHaveClass('text-sm');
    });

    it('renders small size when specified', () => {
      const { container } = render(<ConfidenceBadge confidence={80} size="sm" />);

      const badge = container.querySelector('[data-testid="confidence-badge-high"]');
      expect(badge).toHaveClass('text-xs');
    });
  });

  describe('Custom Styling', () => {
    it('applies custom className', () => {
      const { container } = render(<ConfidenceBadge confidence={80} className="custom-class" />);

      const badge = container.querySelector('[data-testid="confidence-badge-high"]');
      expect(badge).toHaveClass('custom-class');
    });
  });

  describe('Edge Cases', () => {
    it('handles 0% confidence', () => {
      render(<ConfidenceBadge confidence={0} />);

      expect(screen.getByTestId('confidence-badge-low')).toBeInTheDocument();
      expect(screen.getByText(/0%/)).toBeInTheDocument();
    });

    it('handles 100% confidence', () => {
      render(<ConfidenceBadge confidence={100} />);

      expect(screen.getByTestId('confidence-badge-high')).toBeInTheDocument();
      expect(screen.getByText(/100%/)).toBeInTheDocument();
    });

    it('rounds confidence to nearest integer', () => {
      render(<ConfidenceBadge confidence={84.7} />);

      expect(screen.getByText(/85%/)).toBeInTheDocument();
    });
  });
});
