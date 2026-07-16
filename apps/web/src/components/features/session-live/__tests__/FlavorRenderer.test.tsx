import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import type { CatanLiveFlavorLabels } from '../flavors/catan/CatanLiveFlavor';
import { FlavorRenderer, hasFlavor } from '../FlavorRenderer';

vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

const LABELS: CatanLiveFlavorLabels = {
  panelAriaLabel: 'Pannello Catan',
  roundTemplate: 'Round {n}',
  activePlayerTemplate: 'Turno di {name}',
  phaseTemplate: 'Fase: {name}',
  initBoardCta: 'Genera board Catan',
  viewerWaiting: 'In attesa dell’host',
  hexAriaTemplate: '{terrain} {number}',
  robberLabel: 'Ladro',
  diceLastLabel: 'Ultimo tiro',
  diceHistoryLabel: 'Cronologia',
  rollAriaTemplate: 'Registra tiro {n}',
  vpLabel: 'PV',
  handLabel: 'Mano',
  devLabel: 'Sviluppo',
  settlementsLabel: 'Insediamenti',
  citiesLabel: 'Città',
  roadsLabel: 'Strade',
  longestRoadLabel: 'Strada+',
  largestArmyLabel: 'Armata+',
  incAriaTemplate: '{field} +1',
  decAriaTemplate: '{field} -1',
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
  it('is true for catan, false for unknown / null / undefined', () => {
    expect(hasFlavor('catan')).toBe(true);
    expect(hasFlavor('wingspan')).toBe(false);
    expect(hasFlavor(null)).toBe(false);
    expect(hasFlavor(undefined)).toBe(false);
  });
});

describe('FlavorRenderer', () => {
  it('returns null for a game without a flavor', () => {
    const { container } = render(
      <FlavorRenderer
        gameSlug="chess"
        view="live"
        session={SESSION}
        labels={LABELS}
        viewerRole="Player"
        sessionId="s1"
      />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('lazy-loads and renders the Catan flavor for gameSlug=catan, forwarding viewerRole/sessionId', async () => {
    render(
      <FlavorRenderer
        gameSlug="catan"
        view="live"
        session={SESSION}
        labels={LABELS}
        viewerRole="Player"
        sessionId="s1"
      />
    );
    // <section aria-label="Pannello Catan"> → implicit role="region"
    expect(await screen.findByRole('region', { name: 'Pannello Catan' })).toBeInTheDocument();
    // Player (non-Host) sees the waiting message, not the host CTA — proves
    // viewerRole was actually forwarded through to the lazy flavor.
    expect(screen.getByText('In attesa dell’host')).toBeInTheDocument();
  });
});
