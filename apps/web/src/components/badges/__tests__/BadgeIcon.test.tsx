/**
 * BadgeIcon Component Tests
 * Issue #2746: Frontend - Contributor Display su SharedGame
 *
 * Tests:
 * - Tier-based styling and glow effects
 * - Image rendering vs fallback
 * - Tooltip display
 * - Size variants
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { BadgeIcon } from '../BadgeIcon';
import type { BadgeSummaryDto } from '@/lib/api';

// Test data
const mockBadgeBronze: BadgeSummaryDto = {
  id: 'badge-1',
  name: 'First Contribution',
  tier: 'Bronze',
  iconUrl: 'https://example.com/bronze.png',
};

const mockBadgeDiamond: BadgeSummaryDto = {
  id: 'badge-2',
  name: 'Community Champion',
  tier: 'Diamond',
  iconUrl: null,
};

describe('BadgeIcon', () => {
  it('renders badge with image when iconUrl is provided', () => {
    render(<BadgeIcon badge={mockBadgeBronze} />);
    const img = screen.getByAltText('First Contribution');
    expect(img).toBeInTheDocument();
    expect(img).toHaveAttribute('src', 'https://example.com/bronze.png');
  });

  it('renders fallback with tier initial when iconUrl is null', () => {
    render(<BadgeIcon badge={mockBadgeDiamond} />);
    expect(screen.getByText('D')).toBeInTheDocument(); // 'D' for Diamond
  });

  it('displays tooltip with badge name and tier', () => {
    render(<BadgeIcon badge={mockBadgeBronze} />);
    expect(screen.getByText('First Contribution')).toBeInTheDocument();
    expect(screen.getByText('Bronze tier')).toBeInTheDocument();
  });

  it('applies correct size classes', () => {
    const { rerender, container } = render(<BadgeIcon badge={mockBadgeBronze} size="sm" />);
    expect(container.querySelector('.w-6')).toBeInTheDocument();

    rerender(<BadgeIcon badge={mockBadgeBronze} size="lg" />);
    expect(container.querySelector('.w-12')).toBeInTheDocument();
  });

  it('applies tier-specific glow for Diamond tier', () => {
    const { container } = render(<BadgeIcon badge={mockBadgeDiamond} />);
    const badge = container.querySelector('.ring-cyan-400\\/50');
    expect(badge).toBeInTheDocument();
  });

  it('applies Bronze tier styling', () => {
    const { container } = render(<BadgeIcon badge={mockBadgeBronze} />);
    const badge = container.querySelector('.ring-amber-600\\/30');
    expect(badge).toBeInTheDocument();
  });
});
