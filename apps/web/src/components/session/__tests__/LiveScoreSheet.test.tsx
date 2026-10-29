/**
 * @vitest-environment jsdom
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { liveSessionKeys } from '@/hooks/queries/useLiveSession';
import type { LiveSessionPlayerDto } from '@/lib/api/schemas/live-sessions.schemas';

import { LiveScoreSheet } from '../LiveScoreSheet';

// A score added by hand from the sheet went to dimension 'default' whenever "No Category" was left selected, and a
// session created from the wizard is configured with only "Punti": the server answered 400 "Dimension 'default' is
// not enabled in this session's scoring config", so the default way of adding a score failed. The assistant, which
// names the configured dimension, worked.

const recordScore = vi.fn();
let chosenCategory: string | null = null;
let lastCategories: string[] | undefined;

vi.mock('@/lib/stores/session-store', () => ({
  useSessionStore: (
    select: (s: {
      recordScore: typeof recordScore;
      activeSession: { id: string } | null;
    }) => unknown
  ) => select({ recordScore, activeSession: { id: 'session-1' } }),
}));

// ScoreInput is exercised by its own tests; here it only hands LiveScoreSheet what the form would submit.
vi.mock('../ScoreInput', () => ({
  ScoreInput: (props: {
    categories?: string[];
    onSubmit: (data: {
      participantId: string;
      roundNumber: number | null;
      category: string | null;
      scoreValue: number;
    }) => Promise<void>;
  }) => {
    lastCategories = props.categories;
    return (
      <button
        type="button"
        data-testid="submit-score"
        onClick={() =>
          // The real form shows the error and swallows the rejection; here it is simply ignored.
          void props
            .onSubmit({
              participantId: 'player-1',
              roundNumber: null,
              category: chosenCategory,
              scoreValue: 7,
            })
            .catch(() => undefined)
        }
      />
    );
  },
}));

const players = [
  {
    id: 'player-1',
    displayName: 'Anna',
    userId: null,
    color: 'red',
    role: 'Player',
    totalScore: 0,
    isActive: true,
  },
] as unknown as LiveSessionPlayerDto[];

let queryClient: QueryClient;

function renderSheet(dimensions: string[]) {
  return render(
    <QueryClientProvider client={queryClient}>
      <LiveScoreSheet
        open
        onOpenChange={vi.fn()}
        players={players}
        currentUserId={null}
        currentRound={2}
        dimensions={dimensions}
      />
    </QueryClientProvider>
  );
}

async function submit() {
  fireEvent.click(screen.getByTestId('submit-score'));
  await vi.waitFor(() => expect(recordScore).toHaveBeenCalledTimes(1));
  return recordScore.mock.calls[0][0] as {
    dimension: string;
    round: number;
    value: number;
    playerId: string;
  };
}

beforeEach(() => {
  vi.clearAllMocks();
  recordScore.mockResolvedValue(undefined);
  chosenCategory = null;
  lastCategories = undefined;
  queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
});

// The sheet writes through the store, which only updates the store's own copy. The live page reads the session from
// the `liveSessionKeys.detail(id)` query, so without invalidating it the table kept the old numbers until a reload
// (measured on the live page: the POST answered 204, no re-read followed, 20 s later the row was unchanged).
describe('LiveScoreSheet — the live page sees the new score', () => {
  it('refreshes the session query after the score is saved', async () => {
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
    renderSheet(['Punti']);
    await submit();
    await vi.waitFor(() =>
      expect(invalidate).toHaveBeenCalledWith({ queryKey: liveSessionKeys.detail('session-1') })
    );
  });

  it('does not refresh when saving fails (the form shows the error)', async () => {
    recordScore.mockRejectedValue(new Error('boom'));
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries');
    renderSheet(['Punti']);
    fireEvent.click(screen.getByTestId('submit-score'));
    await vi.waitFor(() => expect(recordScore).toHaveBeenCalledTimes(1));
    await Promise.resolve();
    expect(invalidate).not.toHaveBeenCalled();
  });
});

describe('LiveScoreSheet — which dimension "No Category" records into', () => {
  it('uses the only configured dimension when the session has no "default" one', async () => {
    renderSheet(['Punti']);
    const call = await submit();
    expect(call).toMatchObject({ dimension: 'Punti', playerId: 'player-1', round: 2, value: 7 });
  });

  it('uses the first configured dimension when there are several and no "default"', async () => {
    renderSheet(['Punti', 'Risorse']);
    expect((await submit()).dimension).toBe('Punti');
  });

  it('keeps "default" for a session that has it', async () => {
    renderSheet(['default', 'Risorse']);
    expect((await submit()).dimension).toBe('default');
  });

  it('keeps "default" when the layout has no session yet and falls back to it', async () => {
    renderSheet(['default']);
    expect((await submit()).dimension).toBe('default');
  });

  it('records into the dimension the user picked', async () => {
    chosenCategory = 'Risorse';
    renderSheet(['Punti', 'Risorse']);
    expect((await submit()).dimension).toBe('Risorse');
  });
});

describe('LiveScoreSheet — the category select', () => {
  it('is not offered when there is a single configured dimension and nothing to choose', () => {
    renderSheet(['Punti']);
    expect(lastCategories).toBeUndefined();
  });

  it('is offered when there are several dimensions', () => {
    renderSheet(['Punti', 'Risorse']);
    expect(lastCategories).toEqual(['Punti', 'Risorse']);
  });

  it('offers the extra dimension next to "default"', () => {
    renderSheet(['default', 'Risorse']);
    expect(lastCategories).toEqual(['Risorse']);
  });
});
