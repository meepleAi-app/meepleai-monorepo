import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import {
  ManaPips,
  getKbPipColor,
  type ManaPip,
  type KbPipState,
} from '@/components/ui/data-display/meeple-card/parts/ManaPips';

// ========== getKbPipColor ==========

describe('getKbPipColor', () => {
  it('returns green when kbIndexedCount > 0', () => {
    const state: KbPipState = { kbIndexedCount: 3, kbProcessingCount: 0 };
    expect(getKbPipColor(state)).toBe('hsl(142, 71%, 45%)');
  });

  it('returns green when both indexed and processing exist (indexed takes priority)', () => {
    const state: KbPipState = { kbIndexedCount: 2, kbProcessingCount: 1 };
    expect(getKbPipColor(state)).toBe('hsl(142, 71%, 45%)');
  });

  it('returns yellow when only processing', () => {
    const state: KbPipState = { kbIndexedCount: 0, kbProcessingCount: 2 };
    expect(getKbPipColor(state)).toBe('hsl(45, 93%, 47%)');
  });

  it('returns grey when no indexed and no processing', () => {
    const state: KbPipState = { kbIndexedCount: 0, kbProcessingCount: 0 };
    expect(getKbPipColor(state)).toBe('hsl(0, 0%, 60%)');
  });
});

// ========== ManaPips component ==========

describe('ManaPips', () => {
  it('renders nothing for empty pips array', () => {
    const { container } = render(<ManaPips pips={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders pip dots for each entity type', () => {
    const pips: ManaPip[] = [
      { entityType: 'session', count: 2 },
      { entityType: 'kb', count: 1 },
    ];
    render(<ManaPips pips={pips} />);
    const pipElements = screen.getAllByTitle(/session|kb/);
    expect(pipElements).toHaveLength(2);
  });

  it('shows overflow indicator when more than 3 pips', () => {
    const pips: ManaPip[] = [
      { entityType: 'session', count: 1 },
      { entityType: 'kb', count: 1 },
      { entityType: 'agent', count: 1 },
      { entityType: 'game', count: 1 },
    ];
    render(<ManaPips pips={pips} />);
    expect(screen.getByText('+1')).toBeDefined();
  });

  it('applies colorOverride to the pip dot style', () => {
    const greenOverride = 'hsl(142, 71%, 45%)';
    const pips: ManaPip[] = [{ entityType: 'kb', count: 3, colorOverride: greenOverride }];
    const { container } = render(<ManaPips pips={pips} size="sm" />);
    const dot = container.querySelector('.rounded-full');
    expect(dot).not.toBeNull();
    // jsdom converts hsl to rgb — green hsl(142, 71%, 45%) => rgb(33, 196, 93)
    expect((dot as HTMLElement).style.background).toBe('rgb(33, 196, 93)');
  });

  it('uses default entityHsl color when colorOverride is not set', () => {
    const pips: ManaPip[] = [{ entityType: 'kb', count: 1 }];
    const { container } = render(<ManaPips pips={pips} size="sm" />);
    const dot = container.querySelector('.rounded-full');
    expect(dot).not.toBeNull();
    // Default KB color hsl(210, 40%, 55%) => rgb(94, 140, 186) in jsdom
    expect((dot as HTMLElement).style.background).toBe('rgb(94, 140, 186)');
  });

  it('uses different color for colorOverride vs default', () => {
    const greyOverride = 'hsl(0, 0%, 60%)';
    const withOverride: ManaPip[] = [{ entityType: 'kb', count: 1, colorOverride: greyOverride }];
    const withoutOverride: ManaPip[] = [{ entityType: 'kb', count: 1 }];

    const { container: c1 } = render(<ManaPips pips={withOverride} size="sm" />);
    const { container: c2 } = render(<ManaPips pips={withoutOverride} size="sm" />);

    const dot1 = c1.querySelector('.rounded-full') as HTMLElement;
    const dot2 = c2.querySelector('.rounded-full') as HTMLElement;

    // Grey override should differ from default KB blue
    expect(dot1.style.background).not.toBe(dot2.style.background);
  });
});
