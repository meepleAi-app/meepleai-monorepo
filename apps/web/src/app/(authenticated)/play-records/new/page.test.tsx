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
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { toast } from 'sonner';

import NewPlayRecordPage from './page';
import { SessionCreateFormSchema } from '@/lib/api/schemas/play-records.schemas';

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
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
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

// What the real form hands over, built with its own schema: if a field changed shape there, this would
// throw instead of letting a stale hand-written object keep the page test green.
const SAVE_DATA = SessionCreateFormSchema.parse({
  gameType: 'catalog',
  gameName: 'Azul',
  sessionDate: new Date('2026-07-12T10:00:00.000Z'),
  visibility: 'Private',
  enableScoring: true,
  scoringDimensions: ['points'],
  dimensionUnits: {},
  notes: '',
  location: 'HP-TEST location',
});
const SAVE_PLAYERS = [{ id: 'p1', name: 'Marco', score: '42' }];

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
      players: [{ id: 'srv-marco', displayName: 'Marco', scores: [] }],
    });

    renderPage();
    await screen.findByTestId('mock-form');

    const answer = await captured.onSubmit!(SAVE_DATA, SAVE_PLAYERS);

    // A save that went through does not say «not saved»: the form clears itself.
    expect(answer).toBeUndefined();

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

// #4800: the page used to catch every error and resolve, which the form took for «saved»: wizard back to
// step one, roster gone and the draft — the safety net for exactly this — deleted. `false` is «not saved».
describe('NewPlayRecordPage — a save that does not complete (#4800)', () => {
  const SERVER_RECORD = {
    id: 'rec-1',
    location: null,
    players: [{ id: 'srv-marco', displayName: 'Marco', scores: [] }],
  };
  const ROSTER = [...SAVE_PLAYERS, { id: 'p2', name: 'Lucia', score: '' }];

  async function renderForm() {
    renderPage();
    await screen.findByTestId('mock-form');
    return captured.onSubmit!;
  }

  beforeEach(() => {
    vi.clearAllMocks();
    mockCreateMutate.mockReset().mockResolvedValue('rec-1');
    mockAddPlayer.mockReset().mockResolvedValue(undefined);
    mockRecordScore.mockReset().mockResolvedValue(undefined);
    mockUpdateRecord.mockReset().mockResolvedValue(undefined);
    mockGetRecord.mockReset().mockResolvedValue(SERVER_RECORD);
  });

  it('says «not saved» when the record cannot be created, and nothing else is attempted', async () => {
    mockCreateMutate.mockRejectedValue(new Error('Server error'));
    const submit = await renderForm();

    const answer = await submit(SAVE_DATA, ROSTER);

    expect(answer).toBe(false);
    expect(toast.error).toHaveBeenCalledWith(
      'playRecords.new.error.saveFailed',
      expect.objectContaining({ description: 'Server error' })
    );
    expect(mockAddPlayer).not.toHaveBeenCalled();
    expect(mockRouterPush).not.toHaveBeenCalled();
  });

  it('after a failed creation a second save is an ordinary second attempt', async () => {
    mockCreateMutate.mockRejectedValueOnce(new Error('Server error'));
    const submit = await renderForm();

    expect(await submit(SAVE_DATA, ROSTER)).toBe(false);
    const answer = await submit(SAVE_DATA, ROSTER);

    expect(answer).toBeUndefined();
    expect(mockCreateMutate).toHaveBeenCalledTimes(2);
    expect(mockRouterPush).toHaveBeenCalledWith('/play-records/rec-1');
  });

  it('says «not saved» when the record exists but a later step fails, and names that state', async () => {
    mockAddPlayer.mockResolvedValueOnce(undefined).mockRejectedValueOnce(new Error('No'));
    const submit = await renderForm();

    const answer = await submit(SAVE_DATA, ROSTER);

    expect(answer).toBe(false);
    expect(toast.error).toHaveBeenCalledWith(
      'playRecords.new.error.partialSaveFailed',
      expect.objectContaining({ description: 'No' })
    );
    expect(toast.success).not.toHaveBeenCalled();
    expect(mockRouterPush).not.toHaveBeenCalled();
  });

  it('resumes the same record on the next save: no second record, only what is missing is done', async () => {
    // First attempt: Marco is added, Lucia fails. Second: the server already holds Marco.
    mockAddPlayer.mockResolvedValueOnce(undefined).mockRejectedValueOnce(new Error('No'));
    const submit = await renderForm();
    expect(await submit(SAVE_DATA, ROSTER)).toBe(false);
    mockAddPlayer.mockClear();
    mockCreateMutate.mockClear();

    const answer = await submit(SAVE_DATA, ROSTER);

    expect(answer).toBeUndefined();
    expect(mockCreateMutate).not.toHaveBeenCalled();
    expect(mockAddPlayer).toHaveBeenCalledTimes(1);
    expect(mockAddPlayer).toHaveBeenCalledWith('rec-1', { displayName: 'Lucia' });
    expect(mockRouterPush).toHaveBeenCalledWith('/play-records/rec-1');
  });

  it('does not record a score twice, nor rewrite a location the first attempt already saved', async () => {
    mockUpdateRecord.mockRejectedValueOnce(new Error('No'));
    const submit = await renderForm();
    expect(await submit(SAVE_DATA, SAVE_PLAYERS)).toBe(false);
    expect(mockRecordScore).toHaveBeenCalledTimes(1);
    mockRecordScore.mockClear();
    mockUpdateRecord.mockClear();
    // What the server holds after the first attempt: Marco with his score; the location was not saved.
    mockGetRecord.mockResolvedValue({
      ...SERVER_RECORD,
      players: [
        {
          id: 'srv-marco',
          displayName: 'Marco',
          scores: [{ dimension: 'points', value: 42, unit: null }],
        },
      ],
    });

    const answer = await submit(SAVE_DATA, SAVE_PLAYERS);

    expect(answer).toBeUndefined();
    expect(mockRecordScore).not.toHaveBeenCalled();
    expect(mockUpdateRecord).toHaveBeenCalledWith('rec-1', { location: 'HP-TEST location' });
  });

  it('does not rewrite a location that is already on the server', async () => {
    mockAddPlayer.mockRejectedValueOnce(new Error('No'));
    const submit = await renderForm();
    expect(await submit(SAVE_DATA, SAVE_PLAYERS)).toBe(false);
    mockUpdateRecord.mockClear();
    mockGetRecord.mockResolvedValue({ ...SERVER_RECORD, location: 'HP-TEST location' });

    await submit(SAVE_DATA, SAVE_PLAYERS);

    expect(mockUpdateRecord).not.toHaveBeenCalled();
  });

  it('creates a new record when the inputs that define it changed, and says the half one is still there', async () => {
    mockAddPlayer.mockRejectedValueOnce(new Error('No'));
    const submit = await renderForm();
    expect(await submit(SAVE_DATA, SAVE_PLAYERS)).toBe(false);
    mockCreateMutate.mockClear().mockResolvedValue('rec-2');

    const answer = await submit({ ...SAVE_DATA, gameName: 'Wingspan' }, SAVE_PLAYERS);

    expect(answer).toBeUndefined();
    expect(mockCreateMutate).toHaveBeenCalledTimes(1);
    expect(mockCreateMutate).toHaveBeenCalledWith(
      expect.objectContaining({ gameName: 'Wingspan' })
    );
    expect(mockRouterPush).toHaveBeenCalledWith('/play-records/rec-2');
    // The warning points at the record that was left.
    const [title, options] = vi.mocked(toast.warning).mock.calls[0] as [
      string,
      { action: { onClick: () => void } },
    ];
    expect(title).toBe('playRecords.new.error.strayRecord');
    mockRouterPush.mockClear();
    options.action.onClick();
    expect(mockRouterPush).toHaveBeenCalledWith('/play-records/rec-1');
  });

  it('starts over after a save that completed', async () => {
    const submit = await renderForm();
    expect(await submit(SAVE_DATA, SAVE_PLAYERS)).toBeUndefined();
    mockCreateMutate.mockClear().mockResolvedValue('rec-3');

    await submit(SAVE_DATA, SAVE_PLAYERS);

    // Not mistaken for a resumed one: the finished record is not reused.
    expect(mockCreateMutate).toHaveBeenCalledTimes(1);
    expect(mockRouterPush).toHaveBeenLastCalledWith('/play-records/rec-3');
  });
});
