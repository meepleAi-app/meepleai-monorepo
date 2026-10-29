/**
 * @vitest-environment jsdom
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

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
  useSessionStore: (select: (s: { recordScore: typeof recordScore }) => unknown) =>
    select({ recordScore }),
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
          void props.onSubmit({
            participantId: 'player-1',
            roundNumber: null,
            category: chosenCategory,
            scoreValue: 7,
          })
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

function renderSheet(dimensions: string[]) {
  return render(
    <LiveScoreSheet
      open
      onOpenChange={vi.fn()}
      players={players}
      currentUserId={null}
      currentRound={2}
      dimensions={dimensions}
    />
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
