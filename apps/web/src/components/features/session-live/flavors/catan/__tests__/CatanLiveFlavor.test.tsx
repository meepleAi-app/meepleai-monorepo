import { render, screen, within } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';

import { CatanLiveFlavor, type CatanLiveFlavorLabels } from '../CatanLiveFlavor';

const LABELS: CatanLiveFlavorLabels = {
  panelAriaLabel: 'Pannello Catan',
  roundTemplate: 'Round {n}',
  activePlayerTemplate: 'Turno di {name}',
  phaseTemplate: 'Fase: {name}',
  leaderboardHeading: 'Punti Vittoria',
  leaderBadgeLabel: 'In testa',
  scoreAriaTemplate: 'Punti di {name}: {score}',
  dimensionsHeading: 'Dettaglio punti',
  emptyLabel: 'In attesa dei dati della partita…',
};

function makeSession(over: Partial<LiveSessionDto> = {}): LiveSessionDto {
  const base: LiveSessionDto = {
    id: '11111111-1111-1111-1111-111111111111',
    sessionCode: 'S-CATAN',
    gameId: '22222222-2222-2222-2222-222222222222',
    gameName: 'Catan',
    gameSlug: 'catan',
    createdByUserId: '33333333-3333-3333-3333-333333333333',
    status: 'InProgress',
    visibility: 'Private',
    groupId: null,
    createdAt: '2026-07-16T10:00:00Z',
    startedAt: '2026-07-16T10:05:00Z',
    pausedAt: null,
    completedAt: null,
    updatedAt: '2026-07-16T10:30:00Z',
    lastSavedAt: null,
    currentTurnIndex: 3,
    currentTurnPlayerId: 'p2',
    agentMode: 'None',
    notes: null,
    players: [
      {
        id: 'p1',
        userId: null,
        displayName: 'Alice',
        avatarUrl: null,
        color: 'Red',
        role: 'Player',
        teamId: null,
        totalScore: 8,
        currentRank: 1,
        joinedAt: '2026-07-16T10:00:00Z',
        isActive: false,
      },
      {
        id: 'p2',
        userId: null,
        displayName: 'Bruno',
        avatarUrl: null,
        color: 'Blue',
        role: 'Player',
        teamId: null,
        totalScore: 6,
        currentRank: 2,
        joinedAt: '2026-07-16T10:00:00Z',
        isActive: true,
      },
    ],
    teams: [],
    roundScores: [],
    scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
    ...over,
  };
  return base;
}

describe('CatanLiveFlavor', () => {
  it('renders the empty state when there are no players', () => {
    render(<CatanLiveFlavor session={makeSession({ players: [] })} labels={LABELS} />);
    expect(screen.getByText('In attesa dei dati della partita…')).toBeInTheDocument();
  });

  it('renders the turn header with round + active player', () => {
    const { container } = render(<CatanLiveFlavor session={makeSession()} labels={LABELS} />);
    const header = container.querySelector('[data-slot="catan-flavor-turn"]');
    expect(header).toHaveTextContent('Round 4'); // currentTurnIndex 3 + 1
    expect(header).toHaveTextContent('Turno di Bruno'); // currentTurnPlayerId p2
  });

  it('renders the leaderboard sorted by score desc with the leader first', () => {
    const { container } = render(<CatanLiveFlavor session={makeSession()} labels={LABELS} />);
    const lb = container.querySelector('[data-slot="catan-flavor-leaderboard"]') as HTMLElement;
    const rows = within(lb).getAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent('Alice'); // 8 pts leads
    expect(rows[0]).toHaveTextContent('8');
    expect(rows[1]).toHaveTextContent('Bruno');
  });

  it('omits the dimensions section when no scoring dimensions are configured', () => {
    const { container } = render(<CatanLiveFlavor session={makeSession()} labels={LABELS} />);
    expect(container.querySelector('[data-slot="catan-flavor-dimensions"]')).toBeNull();
  });

  it('renders per-dimension breakdown from roundScores when dimensions are configured', () => {
    const session = makeSession({
      scoringConfig: { enabledDimensions: ['Città'], dimensionUnits: {} },
      roundScores: [
        {
          playerId: 'p1',
          round: 1,
          dimension: 'Città',
          value: 2,
          unit: null,
          recordedAt: '2026-07-16T10:10:00Z',
        },
        {
          playerId: 'p1',
          round: 2,
          dimension: 'Città',
          value: 2,
          unit: null,
          recordedAt: '2026-07-16T10:20:00Z',
        },
        {
          playerId: 'p2',
          round: 1,
          dimension: 'Città',
          value: 4,
          unit: null,
          recordedAt: '2026-07-16T10:20:00Z',
        },
      ],
    });
    const { container } = render(<CatanLiveFlavor session={session} labels={LABELS} />);
    const dim = container.querySelector('[data-slot="catan-flavor-dimensions"]') as HTMLElement;
    expect(dim).not.toBeNull();
    expect(dim).toHaveTextContent('Città');
    expect(dim).toHaveTextContent('Alice'); // p1 summed 2+2 = 4
    const p1Cell = dim.querySelector('[data-player="p1"]'); // robust: avoids ambiguous "4"
    expect(p1Cell).not.toBeNull();
    expect(p1Cell).toHaveTextContent('4');
  });

  it('overlays live SignalR points over the DTO totalScore (re-sorts + leader)', () => {
    // DTO: Alice 8, Bruno 6. Live: Bruno 12, Alice 8 → Bruno now leads.
    const livePoints = new Map<string, number>([
      ['p1', 8],
      ['p2', 12],
    ]);
    const { container } = render(
      <CatanLiveFlavor session={makeSession()} labels={LABELS} livePoints={livePoints} />
    );
    const lb = container.querySelector('[data-slot="catan-flavor-leaderboard"]') as HTMLElement;
    const rows = within(lb).getAllByRole('listitem');
    expect(rows[0]).toHaveTextContent('Bruno'); // live 12 leads
    expect(rows[0]).toHaveTextContent('12');
    expect(rows[1]).toHaveTextContent('Alice');
    expect(rows[1]).toHaveTextContent('8');
  });

  it('falls back to totalScore for players missing from livePoints', () => {
    const livePoints = new Map<string, number>([['p2', 20]]); // only Bruno has a live entry
    const { container } = render(
      <CatanLiveFlavor session={makeSession()} labels={LABELS} livePoints={livePoints} />
    );
    const lb = container.querySelector('[data-slot="catan-flavor-leaderboard"]') as HTMLElement;
    const rows = within(lb).getAllByRole('listitem');
    expect(rows[0]).toHaveTextContent('Bruno'); // 20 (live)
    expect(rows[0]).toHaveTextContent('20');
    expect(rows[1]).toHaveTextContent('Alice'); // 8 (DTO fallback)
  });

  it('renders the phase name in the header when provided (joined with active player)', () => {
    const { container } = render(
      <CatanLiveFlavor session={makeSession()} labels={LABELS} phaseName="Costruisci" />
    );
    const header = container.querySelector('[data-slot="catan-flavor-turn"]');
    expect(header).toHaveTextContent('Fase: Costruisci');
    expect(header).toHaveTextContent('Turno di Bruno'); // active player still shown
  });

  it('omits the phase segment when phaseName is null/absent', () => {
    const { container } = render(<CatanLiveFlavor session={makeSession()} labels={LABELS} />);
    const header = container.querySelector('[data-slot="catan-flavor-turn"]');
    expect(header).not.toHaveTextContent('Fase:');
  });
});

describe('CatanLiveFlavor — a11y (axe AA)', () => {
  it('passes axe with players + active turn + dimensions', async () => {
    const session = makeSession({
      scoringConfig: { enabledDimensions: ['Città'], dimensionUnits: {} },
      roundScores: [
        {
          playerId: 'p1',
          round: 1,
          dimension: 'Città',
          value: 2,
          unit: null,
          recordedAt: '2026-07-16T10:10:00Z',
        },
      ],
    });
    const { container } = render(<CatanLiveFlavor session={session} labels={LABELS} />);
    expect(await axe(container)).toHaveNoViolations();
  });

  it('passes axe in the empty (no-players) state', async () => {
    const { container } = render(
      <CatanLiveFlavor session={makeSession({ players: [] })} labels={LABELS} />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
