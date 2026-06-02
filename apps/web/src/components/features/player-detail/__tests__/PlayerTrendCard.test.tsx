/**
 * PlayerTrendCard unit tests — /players/[id] v2 (#1549, #1485 follow-up F3).
 *
 * Coverage (mirror PlayerTopGamesCard.test pattern):
 *   T1. Renders chart + delta badge with positive trend (rising win rate)
 *   T2. Renders chart + delta badge with negative trend (falling win rate)
 *   T3. Renders empty state when points.length < 2 (insufficient data)
 *   T4. Computes flat-delta arrow when first and last win rates are equal
 *   T5. Renders axis labels using monthsShort[N-1] from labels (i18n localization)
 *   T6. Passes axe a11y scan in populated and empty states; SVG aria-hidden;
 *       sr-only summary exposes the trend numerically.
 */

import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, it, expect } from 'vitest';

import { PlayerTrendCard } from '../PlayerTrendCard';
import type { MonthlyWinRatePoint, PlayerTrendCardLabels } from '../PlayerTrendCard';

const monthsShort = [
  'Gen',
  'Feb',
  'Mar',
  'Apr',
  'Mag',
  'Giu',
  'Lug',
  'Ago',
  'Set',
  'Ott',
  'Nov',
  'Dic',
];

const labels: PlayerTrendCardLabels = {
  title: 'Andamento ultimi 6 mesi',
  deltaUp: '↗ +{percent}%',
  deltaDown: '↘ {percent}%',
  deltaFlat: '→ 0%',
  deltaUpAriaLabel: 'Andamento in salita di {percent}%',
  deltaDownAriaLabel: 'Andamento in discesa di {percent}%',
  deltaFlatAriaLabel: 'Andamento stabile',
  empty: 'Non ci sono ancora dati sufficienti per mostrare un trend',
  monthsShort,
  trendSummaryAriaLabel: 'Andamento win rate da {from}% a {to}% negli ultimi {count} mesi',
};

const RISING: ReadonlyArray<MonthlyWinRatePoint> = [
  { month: '2026-01', winRate: 0.4 },
  { month: '2026-02', winRate: 0.5 },
  { month: '2026-03', winRate: 0.6 },
];

const FALLING: ReadonlyArray<MonthlyWinRatePoint> = [
  { month: '2026-04', winRate: 0.7 },
  { month: '2026-05', winRate: 0.5 },
  { month: '2026-06', winRate: 0.3 },
];

describe('PlayerTrendCard', () => {
  it('T1: renders chart + positive delta badge when trend is rising', () => {
    const { container } = render(<PlayerTrendCard points={RISING} labels={labels} />);

    expect(screen.getByText('Andamento ultimi 6 mesi')).toBeInTheDocument();
    // Last (0.6) - First (0.4) = 0.2 → 20% positive delta
    expect(screen.getByText('↗ +20%')).toBeInTheDocument();
    // SVG and summary present (chart rendered)
    expect(container.querySelector('[data-slot="player-detail-trend-svg"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="player-detail-trend-summary"]')).not.toBeNull();
  });

  it('T2: renders chart + negative delta badge when trend is falling', () => {
    render(<PlayerTrendCard points={FALLING} labels={labels} />);

    // Last (0.3) - First (0.7) = -0.4 → -40% (template includes the minus)
    expect(screen.getByText('↘ -40%')).toBeInTheDocument();
    // Accessible name uses absolute value via deltaDownAriaLabel template.
    expect(screen.getByText('Andamento in discesa di 40%')).toHaveClass('sr-only');
  });

  it('T3: renders empty state when points.length < 2', () => {
    const { container } = render(
      <PlayerTrendCard points={[{ month: '2026-06', winRate: 0.5 }]} labels={labels} />
    );

    const empty = screen.getByText('Non ci sono ancora dati sufficienti per mostrare un trend');
    expect(empty).toBeInTheDocument();
    expect(empty).toHaveAttribute('role', 'status');
    // SVG and summary NOT rendered in empty state.
    expect(container.querySelector('[data-slot="player-detail-trend-svg"]')).toBeNull();
    expect(container.querySelector('[data-slot="player-detail-trend-summary"]')).toBeNull();
    // Delta badge NOT rendered with insufficient data.
    expect(container.querySelector('[data-slot="player-detail-trend-delta"]')).toBeNull();
  });

  it('T4: renders flat delta arrow when first and last win rates are equal', () => {
    const flat: ReadonlyArray<MonthlyWinRatePoint> = [
      { month: '2026-05', winRate: 0.5 },
      { month: '2026-06', winRate: 0.5 },
    ];
    render(<PlayerTrendCard points={flat} labels={labels} />);

    expect(screen.getByText('→ 0%')).toBeInTheDocument();
    expect(screen.getByText('Andamento stabile')).toHaveClass('sr-only');
  });

  it('T5: renders axis labels using monthsShort[N-1] from i18n', () => {
    const { container } = render(<PlayerTrendCard points={RISING} labels={labels} />);

    const axis = container.querySelector('[data-slot="player-detail-trend-axis"]');
    expect(axis).not.toBeNull();
    // RISING covers months 01, 02, 03 → Gen, Feb, Mar.
    expect(axis?.textContent).toContain('Gen');
    expect(axis?.textContent).toContain('Feb');
    expect(axis?.textContent).toContain('Mar');
  });

  it('T6: passes axe a11y scan in populated and empty states; SVG aria-hidden', async () => {
    const populated = render(<PlayerTrendCard points={RISING} labels={labels} />);
    const populatedResults = await axe(populated.container);
    expect(populatedResults).toHaveNoViolations();
    // SVG is decorative.
    const svg = populated.container.querySelector('[data-slot="player-detail-trend-svg"]');
    expect(svg).toHaveAttribute('aria-hidden', 'true');
    // sr-only summary exposes the trend numerically (40% → 60% over 3 months).
    expect(
      populated.container.querySelector('[data-slot="player-detail-trend-summary"]')
    ).toHaveTextContent('Andamento win rate da 40% a 60% negli ultimi 3 mesi');
    populated.unmount();

    const empty = render(<PlayerTrendCard points={[]} labels={labels} />);
    const emptyResults = await axe(empty.container);
    expect(emptyResults).toHaveNoViolations();
  });
});
