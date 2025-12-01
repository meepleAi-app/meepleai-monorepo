/**
 * ConfidenceBadge Component Tests
 *
 * Comprehensive test suite for ConfidenceBadge component covering:
 * - Rendering with different confidence levels
 * - Color-coded badge variants (green/yellow/red)
 * - Tooltip integration and explanations
 * - Edge cases and boundary conditions
 * - Accessibility (ARIA labels, keyboard navigation)
 * - Helper functions (validation, level detection)
 *
 * Target Coverage: ≥90%
 *
 * @see Issue #1832 (UI-005)
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  ConfidenceBadge,
  getConfidenceLevel,
  getConfidenceConfig,
  validateConfidence,
  CONFIDENCE_LEVELS,
} from '../confidence-badge';

// ============================================================================
// Helper Function Tests
// ============================================================================

describe('ConfidenceBadge Helper Functions', () => {
  describe('getConfidenceLevel', () => {
    it('returns "high" for confidence ≥85', () => {
      expect(getConfidenceLevel(100)).toBe('high');
      expect(getConfidenceLevel(95)).toBe('high');
      expect(getConfidenceLevel(85)).toBe('high');
    });

    it('returns "medium" for confidence 70-84', () => {
      expect(getConfidenceLevel(84)).toBe('medium');
      expect(getConfidenceLevel(75)).toBe('medium');
      expect(getConfidenceLevel(70)).toBe('medium');
    });

    it('returns "low" for confidence <70', () => {
      expect(getConfidenceLevel(69)).toBe('low');
      expect(getConfidenceLevel(50)).toBe('low');
      expect(getConfidenceLevel(0)).toBe('low');
    });

    it('handles boundary values correctly', () => {
      expect(getConfidenceLevel(85)).toBe('high'); // Exact high threshold
      expect(getConfidenceLevel(84.9999)).toBe('medium'); // Just below
      expect(getConfidenceLevel(70)).toBe('medium'); // Exact medium threshold
      expect(getConfidenceLevel(69.9999)).toBe('low'); // Just below
    });
  });

  describe('getConfidenceConfig', () => {
    it('returns correct config for high level', () => {
      const config = getConfidenceConfig('high');
      expect(config.label).toBe('Confident');
      expect(config.description).toBe('High accuracy expected');
      expect(config.colorClass).toContain('bg-green-500');
    });

    it('returns correct config for medium level', () => {
      const config = getConfidenceConfig('medium');
      expect(config.label).toBe('Likely Correct');
      expect(config.description).toBe('Moderate confidence');
      expect(config.colorClass).toContain('bg-yellow-500');
    });

    it('returns correct config for low level', () => {
      const config = getConfidenceConfig('low');
      expect(config.label).toBe('Uncertain');
      expect(config.description).toBe('Low confidence, verify manually');
      expect(config.colorClass).toContain('bg-red-500');
    });
  });

  describe('validateConfidence', () => {
    it('clamps negative values to 0', () => {
      expect(validateConfidence(-10)).toBe(0);
      expect(validateConfidence(-1)).toBe(0);
    });

    it('clamps values >100 to 100', () => {
      expect(validateConfidence(150)).toBe(100);
      expect(validateConfidence(101)).toBe(100);
    });

    it('passes through valid values unchanged', () => {
      expect(validateConfidence(0)).toBe(0);
      expect(validateConfidence(50)).toBe(50);
      expect(validateConfidence(100)).toBe(100);
    });
  });

  describe('CONFIDENCE_LEVELS constant', () => {
    it('has correct thresholds', () => {
      expect(CONFIDENCE_LEVELS.high.threshold).toBe(85);
      expect(CONFIDENCE_LEVELS.medium.threshold).toBe(70);
      expect(CONFIDENCE_LEVELS.low.threshold).toBe(0);
    });

    it('has all required properties', () => {
      Object.values(CONFIDENCE_LEVELS).forEach(level => {
        expect(level).toHaveProperty('threshold');
        expect(level).toHaveProperty('label');
        expect(level).toHaveProperty('description');
        expect(level).toHaveProperty('colorClass');
        expect(level).toHaveProperty('ariaLabel');
      });
    });
  });
});

// ============================================================================
// Component Rendering Tests
// ============================================================================

describe('ConfidenceBadge Component Rendering', () => {
  describe('Basic Rendering', () => {
    it('renders badge with confidence percentage', () => {
      render(<ConfidenceBadge confidence={85} />);
      expect(screen.getByText('85%')).toBeInTheDocument();
    });

    it('renders high confidence badge (green)', () => {
      const { container } = render(<ConfidenceBadge confidence={95} />);
      const badge = screen.getByText('95%');
      expect(badge).toHaveClass('bg-green-500');
    });

    it('renders medium confidence badge (yellow)', () => {
      const { container } = render(<ConfidenceBadge confidence={75} />);
      const badge = screen.getByText('75%');
      expect(badge).toHaveClass('bg-yellow-500');
    });

    it('renders low confidence badge (red)', () => {
      const { container } = render(<ConfidenceBadge confidence={55} />);
      const badge = screen.getByText('55%');
      expect(badge).toHaveClass('bg-red-500');
    });
  });

  describe('Tooltip Integration', () => {
    it('renders tooltip by default', async () => {
      const user = userEvent.setup();
      render(<ConfidenceBadge confidence={95} />);

      const badge = screen.getByText('95%');
      await user.hover(badge);

      await waitFor(() => {
        expect(screen.getAllByText('Confident')[0]).toBeInTheDocument();
        expect(screen.getAllByText('High accuracy expected')[0]).toBeInTheDocument();
      });
    });

    it('shows correct tooltip for medium confidence', async () => {
      const user = userEvent.setup();
      render(<ConfidenceBadge confidence={75} />);

      const badge = screen.getByText('75%');
      await user.hover(badge);

      await waitFor(() => {
        expect(screen.getAllByText('Likely Correct')[0]).toBeInTheDocument();
        expect(screen.getAllByText('Moderate confidence')[0]).toBeInTheDocument();
      });
    });

    it('shows correct tooltip for low confidence', async () => {
      const user = userEvent.setup();
      render(<ConfidenceBadge confidence={55} />);

      const badge = screen.getByText('55%');
      await user.hover(badge);

      await waitFor(() => {
        expect(screen.getAllByText('Uncertain')[0]).toBeInTheDocument();
        expect(screen.getAllByText('Low confidence, verify manually')[0]).toBeInTheDocument();
      });
    });

    it('does not render tooltip when showTooltip=false', () => {
      render(<ConfidenceBadge confidence={95} showTooltip={false} />);

      // Badge exists
      expect(screen.getByText('95%')).toBeInTheDocument();

      // Tooltip content NOT in document immediately
      expect(screen.queryByText('Confident')).not.toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('handles minimum confidence (0%)', () => {
      render(<ConfidenceBadge confidence={0} />);
      expect(screen.getByText('0%')).toBeInTheDocument();
      expect(screen.getByText('0%')).toHaveClass('bg-red-500'); // Low confidence
    });

    it('handles maximum confidence (100%)', () => {
      render(<ConfidenceBadge confidence={100} />);
      expect(screen.getByText('100%')).toBeInTheDocument();
      expect(screen.getByText('100%')).toHaveClass('bg-green-500'); // High confidence
    });

    it('validates negative confidence to 0', () => {
      render(<ConfidenceBadge confidence={-50} />);
      expect(screen.getByText('0%')).toBeInTheDocument();
    });

    it('validates >100 confidence to 100', () => {
      render(<ConfidenceBadge confidence={150} />);
      expect(screen.getByText('100%')).toBeInTheDocument();
    });

    it('handles high threshold boundary (85%)', () => {
      render(<ConfidenceBadge confidence={85} />);
      const badge = screen.getByText('85%');
      expect(badge).toHaveClass('bg-green-500'); // Should be high confidence
    });

    it('handles just below high threshold (84%)', () => {
      render(<ConfidenceBadge confidence={84} />);
      const badge = screen.getByText('84%');
      expect(badge).toHaveClass('bg-yellow-500'); // Should be medium confidence
    });

    it('handles medium threshold boundary (70%)', () => {
      render(<ConfidenceBadge confidence={70} />);
      const badge = screen.getByText('70%');
      expect(badge).toHaveClass('bg-yellow-500'); // Should be medium confidence
    });

    it('handles just below medium threshold (69%)', () => {
      render(<ConfidenceBadge confidence={69} />);
      const badge = screen.getByText('69%');
      expect(badge).toHaveClass('bg-red-500'); // Should be low confidence
    });
  });

  describe('Custom Styling', () => {
    it('applies custom className', () => {
      const { container } = render(
        <ConfidenceBadge confidence={85} className="custom-class shadow-lg" />
      );
      const badge = screen.getByText('85%');
      expect(badge).toHaveClass('custom-class');
      expect(badge).toHaveClass('shadow-lg');
    });

    it('preserves default classes with custom className', () => {
      render(<ConfidenceBadge confidence={85} className="custom-class" />);
      const badge = screen.getByText('85%');
      expect(badge).toHaveClass('custom-class');
      expect(badge).toHaveClass('bg-green-500'); // Default class preserved
      expect(badge).toHaveClass('text-xs'); // Default class preserved
    });
  });
});

// ============================================================================
// Accessibility Tests
// ============================================================================

describe('ConfidenceBadge Accessibility', () => {
  describe('ARIA Labels', () => {
    it('has correct aria-label for high confidence', () => {
      render(<ConfidenceBadge confidence={95} />);
      const badge = screen.getByText('95%');
      expect(badge).toHaveAttribute('aria-label', 'High confidence: 95%');
    });

    it('has correct aria-label for medium confidence', () => {
      render(<ConfidenceBadge confidence={75} />);
      const badge = screen.getByText('75%');
      expect(badge).toHaveAttribute('aria-label', 'Medium confidence: 75%');
    });

    it('has correct aria-label for low confidence', () => {
      render(<ConfidenceBadge confidence={55} />);
      const badge = screen.getByText('55%');
      expect(badge).toHaveAttribute('aria-label', 'Low confidence: 55%');
    });

    it('updates aria-label when confidence changes', () => {
      const { rerender } = render(<ConfidenceBadge confidence={95} />);
      let badge = screen.getByText('95%');
      expect(badge).toHaveAttribute('aria-label', 'High confidence: 95%');

      rerender(<ConfidenceBadge confidence={55} />);
      badge = screen.getByText('55%');
      expect(badge).toHaveAttribute('aria-label', 'Low confidence: 55%');
    });
  });

  describe('Keyboard Navigation', () => {
    // Note: Radix Tooltip keyboard navigation tests are flaky in JSDOM
    // Keyboard accessibility is validated through Storybook manual testing
    it('has proper focus ring styles for keyboard navigation', () => {
      const { container } = render(<ConfidenceBadge confidence={95} />);
      const badge = screen.getByText('95%');

      // Badge should have focus styles
      expect(badge).toHaveClass('focus:ring-2');
      expect(badge).toHaveClass('focus:ring-ring');
    });
  });
});

// ============================================================================
// Integration Tests
// ============================================================================

describe('ConfidenceBadge Integration', () => {
  it('works with multiple instances simultaneously', () => {
    render(
      <div>
        <ConfidenceBadge confidence={95} />
        <ConfidenceBadge confidence={75} />
        <ConfidenceBadge confidence={55} />
      </div>
    );

    expect(screen.getByText('95%')).toBeInTheDocument();
    expect(screen.getByText('75%')).toBeInTheDocument();
    expect(screen.getByText('55%')).toBeInTheDocument();

    expect(screen.getByText('95%')).toHaveClass('bg-green-500');
    expect(screen.getByText('75%')).toHaveClass('bg-yellow-500');
    expect(screen.getByText('55%')).toHaveClass('bg-red-500');
  });

  it('maintains tooltip independence across instances', async () => {
    const user = userEvent.setup();
    render(
      <div>
        <ConfidenceBadge confidence={95} />
        <ConfidenceBadge confidence={55} />
      </div>
    );

    const highBadge = screen.getByText('95%');
    await user.hover(highBadge);

    await waitFor(() => {
      expect(screen.getAllByText('Confident').length).toBeGreaterThan(0);
      expect(screen.queryAllByText('Uncertain')).toHaveLength(0);
    });
  });
});

// ============================================================================
// Snapshot Tests
// ============================================================================

describe('ConfidenceBadge Snapshots', () => {
  it('matches snapshot for high confidence', () => {
    const { container } = render(<ConfidenceBadge confidence={95} showTooltip={false} />);
    expect(container.firstChild).toMatchSnapshot();
  });

  it('matches snapshot for medium confidence', () => {
    const { container } = render(<ConfidenceBadge confidence={75} showTooltip={false} />);
    expect(container.firstChild).toMatchSnapshot();
  });

  it('matches snapshot for low confidence', () => {
    const { container } = render(<ConfidenceBadge confidence={55} showTooltip={false} />);
    expect(container.firstChild).toMatchSnapshot();
  });
});
