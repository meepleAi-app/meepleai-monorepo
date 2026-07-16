import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import type { CatanLiveFlavorLabels } from '../flavors/catan/CatanLiveFlavor';
import { FlavorRenderer, hasFlavor } from '../FlavorRenderer';

const LABELS: CatanLiveFlavorLabels = {
  panelAriaLabel: 'Pannello Catan',
  roundTemplate: 'Round {n}',
  activePlayerTemplate: 'Turno di {name}',
  leaderboardHeading: 'Punti Vittoria',
  leaderBadgeLabel: 'In testa',
  scoreAriaTemplate: 'Punti di {name}: {score}',
  dimensionsHeading: 'Dettaglio punti',
  emptyLabel: 'In attesa…',
};

const SESSION = {
  gameSlug: 'catan',
  currentTurnIndex: 0,
  currentTurnPlayerId: null,
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
  roundScores: [],
  players: [
    {
      id: 'p1',
      userId: null,
      displayName: 'Alice',
      avatarUrl: null,
      color: 'Red',
      role: 'Player',
      teamId: null,
      totalScore: 5,
      currentRank: 1,
      joinedAt: '',
      isActive: false,
    },
  ],
} as unknown as LiveSessionDto;

describe('hasFlavor', () => {
  it('is true for catan, false for unknown / null', () => {
    expect(hasFlavor('catan')).toBe(true);
    expect(hasFlavor('chess')).toBe(false);
    expect(hasFlavor(null)).toBe(false);
    expect(hasFlavor(undefined)).toBe(false);
  });
});

describe('FlavorRenderer', () => {
  it('returns null for a game without a flavor', () => {
    const { container } = render(
      <FlavorRenderer gameSlug="chess" view="live" session={SESSION} labels={LABELS} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('lazy-loads and renders the Catan flavor for gameSlug=catan', async () => {
    render(<FlavorRenderer gameSlug="catan" view="live" session={SESSION} labels={LABELS} />);
    // <section aria-label="Pannello Catan"> → implicit role="region"
    expect(await screen.findByRole('region', { name: 'Pannello Catan' })).toBeInTheDocument();
  });
});
