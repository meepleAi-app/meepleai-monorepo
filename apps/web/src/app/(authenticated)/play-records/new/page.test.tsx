/**
 * NewPlayRecordPage — gameNightId deep-link prefill tests
 *
 * #2348: Verifies that ?gameNightId= is read from URL and forwarded
 * to SessionCreateForm as initialValues/initialPlayers via useGameNightPrefill.
 *
 * Backward-compat: when gameNightId is absent the form gets undefined initial
 * props (no change to existing behaviour).
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';

import NewPlayRecordPage from './page';

// ─── next/navigation mocks ────────────────────────────────────────────────────

const mockRouterPush = vi.fn();

vi.mock('next/navigation', async orig => ({
  ...(await orig<typeof import('next/navigation')>()),
  useSearchParams: () => new URLSearchParams('gameNightId=gn-1'),
  useRouter: () => ({ push: mockRouterPush, back: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
}));

// ─── SessionCreateForm — capture props ────────────────────────────────────────

const captured: {
  initialValues?: unknown;
  initialPlayers?: unknown;
  onSubmit?: (data: unknown, players: unknown) => unknown;
} = {};

vi.mock('@/components/play-records/SessionCreateForm', () => ({
  SessionCreateForm: (props: any) => {
    captured.initialValues = props.initialValues;
    captured.initialPlayers = props.initialPlayers;
    captured.onSubmit = props.onSubmit;
    return <div data-testid="mock-form" />;
  },
  // PlayerEntry is a type-only import — erased at runtime, no mock needed.
}));

// ─── playRecordsApi — #2847 (#BB) orchestration ───────────────────────────────

const mockAddPlayer = vi.fn().mockResolvedValue(undefined);
const mockRecordScore = vi.fn().mockResolvedValue(undefined);
const mockUpdateRecord = vi.fn().mockResolvedValue(undefined);
const mockGetRecord = vi.fn();

vi.mock('@/lib/api/play-records.api', () => ({
  playRecordsApi: {
    addPlayer: (...args: unknown[]) => mockAddPlayer(...args),
    recordScore: (...args: unknown[]) => mockRecordScore(...args),
    updateRecord: (...args: unknown[]) => mockUpdateRecord(...args),
    getRecord: (...args: unknown[]) => mockGetRecord(...args),
  },
}));

// ─── useGameNightPrefill — fixed prefill ──────────────────────────────────────

vi.mock('@/lib/domain-hooks/useGameNightPrefill', () => ({
  useGameNightPrefill: () => ({
    prefill: {
      initialValues: { gameName: 'Brass Birmingham', location: 'Padova' },
      initialPlayers: [],
    },
    isLoading: false,
    isError: false,
    enabled: true,
  }),
}));

// ─── Remaining hooks ──────────────────────────────────────────────────────────

const mockCreateMutate = vi.fn().mockResolvedValue('rec-1');

vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  useCreatePlayRecord: () => ({
    mutateAsync: (...args: unknown[]) => mockCreateMutate(...args),
    isPending: false,
  }),
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

// ─── Helper ───────────────────────────────────────────────────────────────────

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <NewPlayRecordPage />
    </QueryClientProvider>
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('NewPlayRecordPage — gameNightId prefill', () => {
  it('passes GameNight prefill into the form when ?gameNightId= is present', async () => {
    captured.initialValues = undefined;
    captured.initialPlayers = undefined;

    renderPage();

    // Wait for the mock form to be rendered (Suspense resolves synchronously in test)
    await screen.findByTestId('mock-form');

    expect(captured.initialValues).toMatchObject({
      gameName: 'Brass Birmingham',
      location: 'Padova',
    });
    expect(captured.initialPlayers).toEqual([]);
  });
});

describe('NewPlayRecordPage — persists roster/scores/location (#2847 #BB)', () => {
  it('creates the record then adds players, records scores and saves the location', async () => {
    mockCreateMutate.mockClear();
    mockAddPlayer.mockClear();
    mockRecordScore.mockClear();
    mockUpdateRecord.mockClear();
    mockGetRecord.mockClear();
    // After the players are added, the record exposes their server-assigned ids.
    mockGetRecord.mockResolvedValue({
      players: [{ id: 'srv-marco', displayName: 'Marco' }],
    });

    renderPage();
    await screen.findByTestId('mock-form');

    const data = {
      gameType: 'catalog',
      gameId: undefined,
      gameName: 'Azul',
      sessionDate: new Date('2026-07-12T10:00:00.000Z'),
      visibility: 'private',
      enableScoring: true,
      scoringDimensions: ['points'],
      dimensionUnits: {},
      notes: '',
      location: 'HP-TEST location',
    };
    const players = [{ id: 'p1', name: 'Marco', score: '42' }];

    await captured.onSubmit!(data, players);

    // Base record created with the wizard's core fields.
    expect(mockCreateMutate).toHaveBeenCalledWith(
      expect.objectContaining({ gameName: 'Azul', scoringDimensions: ['points'] })
    );
    // Roster persisted.
    expect(mockAddPlayer).toHaveBeenCalledWith('rec-1', { displayName: 'Marco' });
    // Score persisted against the re-fetched player id.
    expect(mockGetRecord).toHaveBeenCalledWith('rec-1');
    expect(mockRecordScore).toHaveBeenCalledWith('rec-1', {
      playerId: 'srv-marco',
      dimension: 'points',
      value: 42,
      unit: undefined,
    });
    // Location persisted (not part of CreatePlayRecordRequest).
    expect(mockUpdateRecord).toHaveBeenCalledWith('rec-1', { location: 'HP-TEST location' });
    // Navigates to the created record.
    expect(mockRouterPush).toHaveBeenCalledWith('/play-records/rec-1');
  });
});
