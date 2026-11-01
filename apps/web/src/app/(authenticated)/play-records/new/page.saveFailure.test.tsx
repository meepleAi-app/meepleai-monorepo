/**
 * NewPlayRecordPage — what the create wizard keeps when the save does not complete (#4800).
 *
 * The page used to catch every error, show a toast and resolve. The form took that for «saved» and, after
 * `await onSubmit(...)`, cleared itself: wizard back to the first step, roster emptied, and the draft in
 * localStorage — the safety net made for exactly this — deleted. `onSubmit` can answer `false` for «not
 * saved» (#4793); the create page now does.
 *
 * This is the real form (`page.test.tsx` fakes it, and a fake form that never wipes itself cannot tell the
 * two behaviours apart): replaced are the wizard store, the draft hook, the data hooks, the router and the
 * picker widgets. The reset is seen through `resetSessionCreation`, the draft through `clear`, and the
 * roster through what the last step draws.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { playRecordsNewMessages } from '@/__tests__/fixtures/i18n-test-messages';

import NewPlayRecordPage from './page';

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) =>
      (playRecordsNewMessages as Record<string, string>)[key.replace('playRecords.new.', '')] ??
      key,
  }),
}));

const mockRouterPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockRouterPush }),
  useSearchParams: () => new URLSearchParams(),
}));

// The last step (Punteggi, index 2) is the one from which the form really submits.
const mockResetSessionCreation = vi.fn();
vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: () => ({
    sessionCreation: { currentStep: 2 },
    nextStep: vi.fn(),
    prevStep: vi.fn(),
    resetSessionCreation: mockResetSessionCreation,
    setSessionField: vi.fn(),
  }),
}));

const mockClearDraft = vi.fn();
vi.mock('@/lib/play-records/hooks/usePlayRecordDraftPersist', () => ({
  usePlayRecordDraftPersist: () => ({
    initialDraft: null,
    clear: mockClearDraft,
    isPending: false,
    lastSavedAt: null,
  }),
}));

// The values and the roster the wizard starts from, as the prefill of a game night would give them.
vi.mock('@/lib/domain-hooks/useGameNightPrefill', () => ({
  useGameNightPrefill: () => ({
    prefill: {
      initialValues: {
        gameType: 'freeform',
        gameName: 'Azul',
        sessionDate: new Date('2026-07-12T10:00:00.000Z'),
        visibility: 'Private',
        enableScoring: false,
        notes: '',
        location: '',
      },
      initialPlayers: [{ id: 'p1', name: 'Marco', score: '' }],
    },
    isLoading: false,
    isError: false,
    enabled: false,
  }),
}));

const mockCreate = vi.fn();
vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  useCreatePlayRecord: () => ({
    mutateAsync: (...args: unknown[]) => mockCreate(...args),
    isPending: false,
  }),
}));

const mockAddPlayer = vi.fn();
const mockGetRecord = vi.fn();
vi.mock('@/lib/api/play-records.api', () => ({
  playRecordsApi: {
    addPlayer: (...args: unknown[]) => mockAddPlayer(...args),
    getRecord: (...args: unknown[]) => mockGetRecord(...args),
    recordScore: vi.fn(),
    updateRecord: vi.fn(),
  },
}));

vi.mock('@/lib/hooks/useMediaQuery', () => ({ useMediaQuery: () => true }));
vi.mock('@/hooks/queries/useCurrentUser', () => ({ useCurrentUser: () => ({ data: undefined }) }));
vi.mock('@/components/features/game-picker', () => ({
  GamePicker: () => <div data-testid="play-record-game-stub" />,
}));
vi.mock('@/components/library/AddPrivateGameForm', () => ({ AddPrivateGameForm: () => <div /> }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } }));

// ─── Helpers ──────────────────────────────────────────────────────────────────

function renderPage() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={qc}>
      <NewPlayRecordPage />
    </QueryClientProvider>
  );
}

/** Submits from the last step and lets what follows `await onSubmit(...)` run. */
async function submit(container: HTMLElement) {
  fireEvent.submit(container.querySelector('form') as HTMLFormElement);
  // What follows the awaited `onSubmit` is a few microtasks away; a timer tick is after all of them.
  await act(async () => {
    await new Promise(resolve => setTimeout(resolve, 0));
  });
}

const rosterIsDrawn = () => screen.getByTestId('player-score-Marco');

beforeEach(() => {
  vi.clearAllMocks();
  mockCreate.mockReset().mockResolvedValue('rec-1');
  mockAddPlayer.mockReset().mockResolvedValue(undefined);
  mockGetRecord.mockReset().mockResolvedValue({
    id: 'rec-1',
    location: null,
    players: [{ id: 'srv-marco', displayName: 'Marco', scores: [] }],
  });
});

describe('NewPlayRecordPage — the real form after a save that does not complete (#4800)', () => {
  it('keeps the wizard, the roster and the draft when the record cannot be created', async () => {
    mockCreate.mockRejectedValueOnce(new Error('Server error'));
    const { container } = renderPage();

    await submit(container);

    await waitFor(() => expect(mockCreate).toHaveBeenCalledTimes(1));
    expect(mockResetSessionCreation).not.toHaveBeenCalled();
    expect(mockClearDraft).not.toHaveBeenCalled();
    expect(rosterIsDrawn()).toBeInTheDocument();
    expect(mockRouterPush).not.toHaveBeenCalled();
  });

  it('keeps them too when the record exists and a later step fails, and the retry finishes that record', async () => {
    mockAddPlayer.mockRejectedValueOnce(new Error('No'));
    const { container } = renderPage();

    await submit(container);

    expect(mockResetSessionCreation).not.toHaveBeenCalled();
    expect(mockClearDraft).not.toHaveBeenCalled();
    expect(rosterIsDrawn()).toBeInTheDocument();

    await submit(container);

    // The same record, not a second one; the form is cleared only now that it is complete.
    expect(mockCreate).toHaveBeenCalledTimes(1);
    expect(mockResetSessionCreation).toHaveBeenCalledTimes(1);
    expect(mockClearDraft).toHaveBeenCalledTimes(1);
    expect(mockRouterPush).toHaveBeenCalledWith('/play-records/rec-1');
  });

  it('still clears the form and the draft after a save that went through, so the tests above can tell them apart', async () => {
    const { container } = renderPage();

    await submit(container);

    expect(mockResetSessionCreation).toHaveBeenCalledTimes(1);
    expect(mockClearDraft).toHaveBeenCalledTimes(1);
    expect(screen.queryByTestId('player-score-Marco')).not.toBeInTheDocument();
    expect(mockRouterPush).toHaveBeenCalledWith('/play-records/rec-1');
  });
});
