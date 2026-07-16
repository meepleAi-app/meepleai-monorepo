import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { WingspanCategoryBreakdown } from '../WingspanCategoryBreakdown';

const players = [
  {
    id: 'p1',
    userId: null,
    displayName: 'Marco',
    avatarUrl: null,
    color: 'Red',
    role: 'Host',
    teamId: null,
    totalScore: 12,
    currentRank: 1,
    joinedAt: '',
    isActive: true,
  },
] as const;

const roundScores = [
  { playerId: 'p1', round: 1, dimension: 'eggs', value: 3, unit: null, recordedAt: '' },
  { playerId: 'p1', round: 2, dimension: 'eggs', value: 4, unit: null, recordedAt: '' },
  { playerId: 'p1', round: 1, dimension: 'birds', value: 5, unit: null, recordedAt: '' },
];

const categoryLabels = {
  birds: 'Uccelli',
  bonusCards: 'Bonus',
  endOfRoundGoals: 'Obiettivi',
  eggs: 'Uova',
  cachedFood: 'Cibo',
  tuckedCards: 'Infilate',
};

describe('WingspanCategoryBreakdown', () => {
  it('sums roundScores per player per category', () => {
    const { container } = render(
      <WingspanCategoryBreakdown
        players={players}
        roundScores={roundScores}
        categoryLabels={categoryLabels}
        heading="Categorie"
      />
    );
    // eggs = 3 + 4 = 7 for p1
    const eggs = container.querySelector('[data-player="p1"][data-category="eggs"]');
    expect(eggs?.textContent).toContain('7');
    const birds = container.querySelector('[data-player="p1"][data-category="birds"]');
    expect(birds?.textContent).toContain('5');
    // a category with no scores shows 0
    const food = container.querySelector('[data-player="p1"][data-category="cachedFood"]');
    expect(food?.textContent).toContain('0');
  });

  it('renders the player name and all 6 categories', () => {
    const { container } = render(
      <WingspanCategoryBreakdown
        players={players}
        roundScores={roundScores}
        categoryLabels={categoryLabels}
        heading="Categorie"
      />
    );
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(container.querySelectorAll('[data-player="p1"][data-category]')).toHaveLength(6);
  });
});
