/**
 * AchievementBadgeGrid unit tests — Wave 3 /players/[id] v2 (Task 2)
 *
 * Tests:
 *   1. Renders N badge placeholders when count > 0
 *   2. Renders empty state when count === 0
 *   3. Renders "view all" link with correct href
 *   4. data-slot attribute is present
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { AchievementBadgeGrid } from '../AchievementBadgeGrid';
import type { AchievementBadgeGridLabels } from '../AchievementBadgeGrid';

const labels: AchievementBadgeGridLabels = {
  title: 'Achievements',
  viewAll: 'View all',
  viewAllAriaLabel: 'View all achievements',
  empty: 'No achievements yet',
};

describe('AchievementBadgeGrid', () => {
  it('renders badge placeholders equal to count when count > 0', () => {
    const { container } = render(
      <AchievementBadgeGrid count={6} viewAllHref="/players/p-sara/achievements" labels={labels} />
    );
    const badges = container.querySelectorAll('[data-slot="achievement-badge"]');
    expect(badges).toHaveLength(6);
  });

  it('renders empty state text when count is 0', () => {
    render(
      <AchievementBadgeGrid count={0} viewAllHref="/players/p-sara/achievements" labels={labels} />
    );
    expect(screen.getByText('No achievements yet')).toBeInTheDocument();
  });

  it('renders "view all" link with correct href when count > 0', () => {
    render(
      <AchievementBadgeGrid count={3} viewAllHref="/players/p-sara/achievements" labels={labels} />
    );
    const link = screen.getByRole('link', { name: 'View all achievements' });
    expect(link).toHaveAttribute('href', '/players/p-sara/achievements');
  });

  it('has correct data-slot attribute for E2E targeting', () => {
    const { container } = render(
      <AchievementBadgeGrid count={2} viewAllHref="/players/p-sara/achievements" labels={labels} />
    );
    expect(
      container.querySelector('[data-slot="player-detail-achievement-grid"]')
    ).toBeInTheDocument();
  });
});
