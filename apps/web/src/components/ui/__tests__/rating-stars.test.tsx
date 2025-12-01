/**
 * RatingStars Component Tests (Issue #1830: UI-003)
 *
 * Test Coverage:
 * - BGG rating conversion (0-10 → 0-5)
 * - Star rendering (full, half, empty)
 * - Size variants
 * - Value display
 * - Color variants
 * - Accessibility
 * - Edge cases
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { RatingStars } from '../rating-stars';

// ============================================================================
// BGG Rating Conversion Tests (0-10 → 0-5)
// ============================================================================

describe('RatingStars - BGG Conversion', () => {
  it('converts BGG 0/10 to 0 stars', () => {
    const { container } = render(<RatingStars rating={0} maxRating={10} />);

    const filledStars = container.querySelectorAll('[fill="currentColor"]');
    expect(filledStars).toHaveLength(0);
  });

  it('converts BGG 5/10 to 2.5 stars', () => {
    render(<RatingStars rating={5.0} maxRating={10} showHalfStars />);

    const ratingLabel = screen.getByRole('img', { name: /Rating: 5.0/i });
    expect(ratingLabel).toBeInTheDocument();
  });

  it('converts BGG 7.5/10 to 3.75 stars (rounds to 4 with half-star)', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} showHalfStars />);

    // 3 full stars + 1 half star
    const filledStars = container.querySelectorAll('[fill="currentColor"]');
    expect(filledStars.length).toBeGreaterThanOrEqual(3);
  });

  it('converts BGG 10/10 to 5 stars (perfect)', () => {
    const { container } = render(<RatingStars rating={10.0} maxRating={10} />);

    const filledStars = container.querySelectorAll('[fill="currentColor"]');
    expect(filledStars).toHaveLength(5);
  });

  it('handles decimal BGG ratings correctly', () => {
    render(<RatingStars rating={8.3} maxRating={10} showValue />);

    expect(screen.getByText('8.3')).toBeInTheDocument();
  });
});

// ============================================================================
// Star Rendering Tests
// ============================================================================

describe('RatingStars - Star Rendering', () => {
  it('renders correct number of full stars', () => {
    const { container } = render(<RatingStars rating={8.0} maxRating={10} />);

    // 8/10 = 4/5 stars
    const filledStars = container.querySelectorAll('[fill="currentColor"]');
    expect(filledStars).toHaveLength(4);
  });

  it('renders half star when enabled', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} showHalfStars />);

    // Should have a half-star container
    const halfStarContainers = container.querySelectorAll('.relative');
    expect(halfStarContainers.length).toBeGreaterThan(0);
  });

  it('does not render half star when disabled', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} showHalfStars={false} />);

    // Should not have half-star container
    const halfStarContainers = container.querySelectorAll('.relative');
    expect(halfStarContainers).toHaveLength(0);
  });

  it('renders total of 5 stars always', () => {
    const { container } = render(<RatingStars rating={6.3} maxRating={10} />);

    // Count all star SVG elements (full + empty + half)
    const allStars = container.querySelectorAll('svg');
    // Should be 5 stars total (accounting for half-star which has 2 SVGs)
    expect(allStars.length).toBeGreaterThanOrEqual(5);
  });

  it('shows all empty stars for zero rating', () => {
    const { container } = render(<RatingStars rating={0} maxRating={10} />);

    const emptyStars = container.querySelectorAll('svg:not([fill="currentColor"])');
    expect(emptyStars).toHaveLength(5);
  });

  it('shows all full stars for perfect rating', () => {
    const { container } = render(<RatingStars rating={10} maxRating={10} />);

    const filledStars = container.querySelectorAll('[fill="currentColor"]');
    expect(filledStars).toHaveLength(5);
  });
});

// ============================================================================
// Size Variants Tests
// ============================================================================

describe('RatingStars - Sizes', () => {
  it('applies small size classes', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} size="sm" />);

    const star = container.querySelector('svg');
    expect(star).toHaveClass('h-3', 'w-3');
  });

  it('applies medium size classes', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} size="md" />);

    const star = container.querySelector('svg');
    expect(star).toHaveClass('h-4', 'w-4');
  });

  it('applies large size classes', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} size="lg" />);

    const star = container.querySelector('svg');
    expect(star).toHaveClass('h-5', 'w-5');
  });

  it('uses medium size by default', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} />);

    const star = container.querySelector('svg');
    expect(star).toHaveClass('h-4', 'w-4');
  });
});

// ============================================================================
// Value Display Tests
// ============================================================================

describe('RatingStars - Value Display', () => {
  it('shows rating value when showValue is true', () => {
    render(<RatingStars rating={8.3} maxRating={10} showValue />);

    expect(screen.getByText('8.3')).toBeInTheDocument();
  });

  it('hides rating value when showValue is false', () => {
    render(<RatingStars rating={8.3} maxRating={10} showValue={false} />);

    expect(screen.queryByText('8.3')).not.toBeInTheDocument();
  });

  it('formats rating value to 1 decimal place', () => {
    render(<RatingStars rating={7.8642} maxRating={10} showValue />);

    expect(screen.getByText('7.9')).toBeInTheDocument();
  });

  it('applies correct text size for sm', () => {
    render(<RatingStars rating={7.5} maxRating={10} size="sm" showValue />);

    const valueText = screen.getByText('7.5');
    expect(valueText).toHaveClass('text-xs');
  });

  it('applies correct text size for md', () => {
    render(<RatingStars rating={7.5} maxRating={10} size="md" showValue />);

    const valueText = screen.getByText('7.5');
    expect(valueText).toHaveClass('text-sm');
  });

  it('applies correct text size for lg', () => {
    render(<RatingStars rating={7.5} maxRating={10} size="lg" showValue />);

    const valueText = screen.getByText('7.5');
    expect(valueText).toHaveClass('text-base');
  });
});

// ============================================================================
// Color Variants Tests
// ============================================================================

describe('RatingStars - Variants', () => {
  it('applies default variant yellow color', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} variant="default" />);

    const filledStar = container.querySelector('[fill="currentColor"]');
    expect(filledStar).toHaveClass('text-yellow-500');
  });

  it('applies muted variant color', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} variant="muted" />);

    const filledStar = container.querySelector('[fill="currentColor"]');
    expect(filledStar).toHaveClass('text-muted-foreground');
  });

  it('uses default variant when not specified', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} />);

    const filledStar = container.querySelector('[fill="currentColor"]');
    expect(filledStar).toHaveClass('text-yellow-500');
  });
});

// ============================================================================
// Accessibility Tests
// ============================================================================

describe('RatingStars - Accessibility', () => {
  it('has role="img" for screen readers', () => {
    render(<RatingStars rating={7.8} maxRating={10} />);

    const ratingContainer = screen.getByRole('img');
    expect(ratingContainer).toBeInTheDocument();
  });

  it('has accessible label with rating value', () => {
    render(<RatingStars rating={7.8} maxRating={10} />);

    expect(screen.getByRole('img', { name: /Rating: 7.8 out of 10/i })).toBeInTheDocument();
  });

  it('marks individual stars as aria-hidden', () => {
    const { container } = render(<RatingStars rating={7.5} maxRating={10} />);

    const stars = container.querySelectorAll('[aria-hidden="true"]');
    expect(stars.length).toBeGreaterThan(0);
  });

  it('provides correct aria-label for different ratings', () => {
    render(<RatingStars rating={9.2} maxRating={10} />);

    expect(screen.getByRole('img', { name: /Rating: 9.2 out of 10/i })).toBeInTheDocument();
  });
});

// ============================================================================
// 5-Star Scale Tests (Direct, No Conversion)
// ============================================================================

describe('RatingStars - 5-Star Scale', () => {
  it('handles 5-star scale without conversion', () => {
    const { container } = render(<RatingStars rating={3.5} maxRating={5} />);

    // 3.5/5 should show 3.5 stars directly
    const filledStars = container.querySelectorAll('[fill="currentColor"]');
    expect(filledStars.length).toBeGreaterThanOrEqual(3);
  });

  it('shows correct value for 5-star scale', () => {
    render(<RatingStars rating={4.2} maxRating={5} showValue />);

    expect(screen.getByText('4.2')).toBeInTheDocument();
  });

  it('aria-label reflects 5-star scale', () => {
    render(<RatingStars rating={3.5} maxRating={5} />);

    expect(screen.getByRole('img', { name: /Rating: 3.5 out of 5/i })).toBeInTheDocument();
  });
});

// ============================================================================
// Edge Cases Tests
// ============================================================================

describe('RatingStars - Edge Cases', () => {
  it('handles zero rating gracefully', () => {
    render(<RatingStars rating={0} maxRating={10} showValue />);

    expect(screen.getByText('0.0')).toBeInTheDocument();
  });

  it('handles perfect 10 rating', () => {
    render(<RatingStars rating={10.0} maxRating={10} showValue />);

    expect(screen.getByText('10.0')).toBeInTheDocument();
  });

  it('handles very low decimal ratings', () => {
    render(<RatingStars rating={0.1} maxRating={10} />);

    const ratingLabel = screen.getByRole('img', { name: /Rating: 0.1/i });
    expect(ratingLabel).toBeInTheDocument();
  });

  it('handles ratings at half-star threshold (x.5)', () => {
    const { container } = render(<RatingStars rating={7.0} maxRating={10} showHalfStars />);

    // 7.0/10 = 3.5/5 → should show half star
    const halfStarContainers = container.querySelectorAll('.relative');
    expect(halfStarContainers.length).toBeGreaterThan(0);
  });

  it('handles ratings just below half-star threshold', () => {
    const { container } = render(<RatingStars rating={6.8} maxRating={10} showHalfStars />);

    // 6.8/10 = 3.4/5 → no half star (< 0.5 threshold)
    const filledStars = container.querySelectorAll('[fill="currentColor"]');
    expect(filledStars).toHaveLength(3);
  });

  it('accepts custom className', () => {
    const { container } = render(
      <RatingStars rating={7.5} maxRating={10} className="custom-class" />
    );

    const ratingContainer = container.firstChild as HTMLElement;
    expect(ratingContainer).toHaveClass('custom-class');
  });

  it('renders without crashing with unusual maxRating', () => {
    expect(() => {
      render(<RatingStars rating={50} maxRating={100} />);
    }).not.toThrow();
  });

  it('handles fractional star counts correctly', () => {
    // 7.3/10 = 3.65/5 → should round to 3.5 with half-star
    const { container } = render(<RatingStars rating={7.3} maxRating={10} showHalfStars />);

    const halfStarContainers = container.querySelectorAll('.relative');
    expect(halfStarContainers.length).toBeGreaterThan(0);
  });
});

// ============================================================================
// Half-Star Logic Tests
// ============================================================================

describe('RatingStars - Half-Star Logic', () => {
  it('shows half-star for x.5 normalized rating', () => {
    // 7.0/10 = 3.5/5 → half star
    const { container } = render(<RatingStars rating={7.0} maxRating={10} showHalfStars />);

    const halfStarContainers = container.querySelectorAll('.relative');
    expect(halfStarContainers.length).toBeGreaterThan(0);
  });

  it('shows half-star for >= 0.5 decimal', () => {
    // 7.1/10 = 3.55/5 → half star (>= 0.5)
    const { container } = render(<RatingStars rating={7.1} maxRating={10} showHalfStars />);

    const halfStarContainers = container.querySelectorAll('.relative');
    expect(halfStarContainers.length).toBeGreaterThan(0);
  });

  it('does not show half-star for < 0.5 decimal', () => {
    // 6.8/10 = 3.4/5 → no half star (< 0.5)
    const { container } = render(<RatingStars rating={6.8} maxRating={10} showHalfStars />);

    const filledStars = container.querySelectorAll('[fill="currentColor"]');
    expect(filledStars).toHaveLength(3); // Only 3 full stars
  });

  it('uses 50% width for half-star overflow', () => {
    const { container } = render(<RatingStars rating={7.0} maxRating={10} showHalfStars />);

    const halfStarOverflow = container.querySelector('[style*="width: 50%"]');
    expect(halfStarOverflow).toBeInTheDocument();
  });
});
