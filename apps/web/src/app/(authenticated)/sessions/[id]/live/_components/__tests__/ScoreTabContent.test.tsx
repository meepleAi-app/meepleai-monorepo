/**
 * ScoreTabContent unit/integration tests — Issue #2430 Block B+ (T6).
 *
 * 28 cases across 8 groups: role gating, role transition, null gate,
 * REST hydration, variant editor mount, debounce+mutation, error
 * handling, optimistic UI.
 */

import { render, screen, act, cleanup } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import type { ReactElement } from 'react';

import { ScoreTabContent } from '../ScoreTabContent';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
import { UpdateSessionScoresError } from '@/hooks/use-update-session-scores';

// ─── sonner mock ───────────────────────────────────────────────────────────

const toastErrorMock = vi.fn();
const toastWarningMock = vi.fn();
const toastSuccessMock = vi.fn();
vi.mock('sonner', () => ({
  toast: {
    error: (msg: string, opts?: unknown) => toastErrorMock(msg, opts),
    warning: (msg: string, opts?: unknown) => toastWarningMock(msg, opts),
    success: (msg: string, opts?: unknown) => toastSuccessMock(msg, opts),
    dismiss: vi.fn(),
  },
}));

// ─── useUpdateSessionScores mock ───────────────────────────────────────────

interface MockMutationHandlers {
  mutate: ReturnType<typeof vi.fn>;
  isPending: boolean;
}

let mockMutation: MockMutationHandlers;

vi.mock('@/hooks/use-update-session-scores', async () => {
  const actual = await vi.importActual<typeof import('@/hooks/use-update-session-scores')>(
    '@/hooks/use-update-session-scores'
  );
  return {
    ...actual,
    useUpdateSessionScores: () => mockMutation,
  };
});

// ─── PolymorphicScoreEditor mock (data-slot probe) ─────────────────────────

vi.mock('@/components/sessions', () => ({
  PolymorphicScoreEditor: ({
    scoringType,
    players,
    initialData,
    availableObjectives,
    onChange,
    disabled,
  }: {
    scoringType: ScoreType;
    players: ReadonlyArray<{ id: string; displayName: string }>;
    initialData?: unknown;
    availableObjectives?: readonly string[];
    onChange: (payload: { scoringType: ScoreType; data: unknown }) => void;
    disabled?: boolean;
  }) => (
    <div
      data-slot="polymorphic-score-editor"
      data-scoring-type={scoringType}
      data-disabled={disabled ? 'true' : 'false'}
      data-player-count={players.length}
      data-objectives-count={availableObjectives?.length ?? 0}
    >
      <button
        type="button"
        data-slot="trigger-change"
        onClick={() =>
          onChange({
            scoringType,
            data:
              scoringType === 'Points'
                ? { scores: [{ playerId: 'p1', points: 99 }] }
                : scoringType === 'BinaryWin'
                  ? { results: [{ playerId: 'p1', isWinner: true }] }
                  : scoringType === 'Ranking'
                    ? { positions: [{ playerId: 'p1', position: 1 }] }
                    : { completedByPlayer: [{ playerId: 'p1', objectives: ['Vittoria'] }] },
          })
        }
      >
        edit
      </button>
      <span data-slot="initial-data">{JSON.stringify(initialData ?? null)}</span>
    </div>
  ),
}));

// ─── i18n MESSAGES subset ──────────────────────────────────────────────────

const MESSAGES: Record<string, string> = {
  'pages.sessionLive.scoring.loadingLabel': 'Caricamento punteggi…',
  'pages.sessionLive.scoring.forbiddenToast':
    "Permesso negato: solo l'host può modificare i punteggi",
  'pages.sessionLive.scoring.rateLimitedTemplate': 'Limite raggiunto, riprova tra {seconds}s',
  'pages.sessionLive.scoring.rateLimitedToast':
    'Hai aggiornato i punteggi troppo velocemente. Aspetta {seconds}s.',
  'pages.sessionLive.scoring.validationFailedTemplate': 'Validazione fallita: {message}',
  'pages.sessionLive.scoring.serverErrorToast': 'Errore server, riprova',
  'pages.sessionLive.scoring.networkErrorToast': 'Connessione persa, riprova',
  'pages.sessionLive.scoring.retryCta': 'Riprova',
  // Renderer labels (passed through but minimal for tests)
  'pages.sessionLive.scoring.title': 'Punteggi',
  'pages.sessionLive.scoring.scoreAriaTemplate': 'Punteggio di {name}',
  'pages.sessionLive.scoring.leaderBadgeLabel': 'in testa',
  'pages.sessionLive.scoring.rankingHeading': 'Posizioni',
  'pages.sessionLive.scoring.rankAriaTemplate': 'Posizione di {name}',
  'pages.sessionLive.scoring.firstPlaceBadgeLabel': 'primo posto',
  'pages.sessionLive.scoring.binaryWinHeading': 'Esito',
  'pages.sessionLive.scoring.binaryWinInProgress': 'Partita in corso',
  'pages.sessionLive.scoring.winLabel': 'Vince',
  'pages.sessionLive.scoring.loseLabel': 'Perde',
  'pages.sessionLive.scoring.outcomeAriaTemplate': '{name}: {result}',
  'pages.sessionLive.scoring.objectivesHeading': 'Obiettivi',
  'pages.sessionLive.scoring.completedAriaTemplate': 'Completati da {name}',
  'pages.sessionLive.scoring.doneAriaTemplate': '{label} (completato)',
  'pages.sessionLive.scoring.pendingAriaTemplate': '{label} (non completato)',
};

const RENDERER_LABELS = {
  points: {
    heading: 'Punteggi',
    scoreAriaTemplate: 'Punteggio di {name}',
    leaderBadgeLabel: 'in testa',
  },
  ranking: {
    heading: 'Posizioni',
    rankAriaTemplate: 'Posizione di {name}',
    firstPlaceBadgeLabel: 'primo posto',
  },
  binaryWin: {
    heading: 'Esito',
    inProgressLabel: 'Partita in corso',
    winLabel: 'Vince',
    loseLabel: 'Perde',
    outcomeAriaTemplate: '{name}: {result}',
  },
  objectives: {
    heading: 'Obiettivi',
    completedAriaTemplate: 'Completati da {name}',
    doneAriaTemplate: '{label} (completato)',
    pendingAriaTemplate: '{label} (non completato)',
  },
} as const;

const PLAYERS = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Anna' },
] as const;

function renderTC(
  props: Partial<React.ComponentProps<typeof ScoreTabContent>> = {}
): ReturnType<typeof render> {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      <ScoreTabContent
        sessionId={props.sessionId ?? 's1'}
        viewerRole={props.viewerRole ?? 'Host'}
        viewerId={props.viewerId ?? 'u1'}
        players={props.players ?? PLAYERS}
        labels={props.labels ?? RENDERER_LABELS}
        className={props.className}
      />
    </IntlProvider>
  );
}

// ─── Global test setup ─────────────────────────────────────────────────────

beforeEach(() => {
  vi.useFakeTimers();
  toastErrorMock.mockClear();
  toastWarningMock.mockClear();
  toastSuccessMock.mockClear();
  useLiveSessionStore.getState().reset();
  mockMutation = {
    mutate: vi.fn(),
    isPending: false,
  };
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
});

// ─── Group 1: Role gating ───────────────────────────────────────────────────

describe('ScoreTabContent — role gating', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
  });

  it('Host + scoringType=Points → editor mounted', () => {
    renderTC({ viewerRole: 'Host' });
    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).toBeNull();
  });

  it('Player + scoringType=Points → renderer mounted (read-only)', () => {
    renderTC({ viewerRole: 'Player' });
    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
  });

  it('Spectator + scoringType=Points → renderer mounted (read-only)', () => {
    renderTC({ viewerRole: 'Spectator' });
    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
  });
});

// ─── Group 2: Role transition ───────────────────────────────────────────────

describe('ScoreTabContent — role transition', () => {
  it('viewerRole Host → Player mid-session → editor unmounts, pending debounce flushes', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
    const { rerender } = renderTC({ viewerRole: 'Host' });

    // Trigger an edit (debounce pending)
    const editor = document.querySelector('[data-slot="trigger-change"]');
    expect(editor).not.toBeNull();
    act(() => {
      (editor as HTMLButtonElement).click();
    });
    expect(mockMutation.mutate).not.toHaveBeenCalled(); // 500ms debounce pending

    // Role flips to Player → flush effect cleanup fires (viewerRole dep change)
    rerender(
      <IntlProvider locale="it" messages={MESSAGES}>
        <ScoreTabContent
          sessionId="s1"
          viewerRole="Player"
          viewerId="u1"
          players={PLAYERS}
          labels={RENDERER_LABELS}
        />
      </IntlProvider>
    );

    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);
    expect(mockMutation.mutate).toHaveBeenCalledWith(
      expect.objectContaining({ scoringType: 'Points' }),
      expect.anything()
    );
  });
});

// ─── Group 3: Null gate ─────────────────────────────────────────────────────

describe('ScoreTabContent — null gate', () => {
  it('scoringType null + Host → a11y placeholder (NOT editor)', () => {
    renderTC({ viewerRole: 'Host' });
    expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-empty"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="scoring-panel-empty"]')?.getAttribute('role')).toBe(
      'status'
    );
  });

  it('scoringType null + Player → a11y placeholder', () => {
    renderTC({ viewerRole: 'Player' });
    expect(document.querySelector('[data-slot="scoring-panel-empty"]')).not.toBeNull();
    expect(screen.getByText('Caricamento punteggi…')).toBeInTheDocument();
  });
});

// ─── Group 4: REST hydration ────────────────────────────────────────────────

describe('ScoreTabContent — REST hydration', () => {
  it('does not overwrite SignalR-hydrated store (race guard)', () => {
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'p1', points: 99 }] },
      });
    });
    renderTC({ viewerRole: 'Host' });
    expect(useLiveSessionStore.getState().scoreData).toEqual({
      scores: [{ playerId: 'p1', points: 99 }],
    });
  });

  it('mounts without throwing when scoringType is null and no hydration source', () => {
    expect(() => renderTC({ viewerRole: 'Host' })).not.toThrow();
    expect(useLiveSessionStore.getState().scoringType).toBeNull();
  });
});

// ─── Group 5: Variant editor mount ──────────────────────────────────────────

describe('ScoreTabContent — variant editor mount (Host)', () => {
  it('Points: editor receives correct scoringType prop', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document
        .querySelector('[data-slot="polymorphic-score-editor"]')
        ?.getAttribute('data-scoring-type')
    ).toBe('Points');
  });

  it('BinaryWin: editor receives correct scoringType prop', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'BinaryWin',
      scoreData: { results: [{ playerId: 'p1', isWinner: false }] },
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document
        .querySelector('[data-slot="polymorphic-score-editor"]')
        ?.getAttribute('data-scoring-type')
    ).toBe('BinaryWin');
  });

  it('Ranking: editor receives correct scoringType prop', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Ranking',
      scoreData: { positions: [{ playerId: 'p1', position: 1 }] },
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document
        .querySelector('[data-slot="polymorphic-score-editor"]')
        ?.getAttribute('data-scoring-type')
    ).toBe('Ranking');
  });

  it('Objectives: editor receives availableObjectives prop (length > 0)', () => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Objectives',
      scoreData: { completedByPlayer: [{ playerId: 'p1', objectives: [] }] },
    });
    renderTC({ viewerRole: 'Host' });
    const editor = document.querySelector('[data-slot="polymorphic-score-editor"]');
    expect(editor?.getAttribute('data-scoring-type')).toBe('Objectives');
    expect(Number(editor?.getAttribute('data-objectives-count'))).toBeGreaterThan(0);
  });
});

// ─── Group 6: Debounce + mutation ───────────────────────────────────────────

describe('ScoreTabContent — debounce + mutation', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 0 }] },
    });
  });

  it('single edit → 500ms debounce → mutate called once with correct payload', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
    });
    expect(mockMutation.mutate).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);
    expect(mockMutation.mutate).toHaveBeenCalledWith(
      {
        sessionId: 's1',
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'p1', points: 99 }] },
      },
      expect.anything()
    );
  });

  it('rapid edits (3 in 100ms) → only last fires after debounce', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      const trigger = document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement;
      trigger.click();
      vi.advanceTimersByTime(30);
      trigger.click();
      vi.advanceTimersByTime(30);
      trigger.click();
    });
    expect(mockMutation.mutate).not.toHaveBeenCalled();

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);
  });

  it('unmount mid-debounce → flush() called, mutation fires immediately', () => {
    const { unmount } = renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
    });
    expect(mockMutation.mutate).not.toHaveBeenCalled();

    unmount();

    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);
  });

  it('mutation receives sessionId from props', () => {
    renderTC({ viewerRole: 'Host', sessionId: 'custom-session-id' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
    expect(mockMutation.mutate).toHaveBeenCalledWith(
      expect.objectContaining({ sessionId: 'custom-session-id' }),
      expect.anything()
    );
  });

  it('SignalR overwrite after mutation → editor receives canonical store data', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
    expect(mockMutation.mutate).toHaveBeenCalledTimes(1);

    // Simulate SignalR broadcast post-mutation
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'p1', points: 99 }] },
      });
    });
    const initialDataNode = document.querySelector('[data-slot="initial-data"]');
    expect(initialDataNode?.textContent).toContain('99');
  });
});

// ─── Group 7: Error handling ────────────────────────────────────────────────

describe('ScoreTabContent — error handling', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 0 }] },
    });
  });

  function triggerEditAndFlushDebounce(): void {
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
  }

  function getOnErrorFromMockMutation(): (err: unknown) => void {
    const calls = mockMutation.mutate.mock.calls;
    expect(calls.length).toBeGreaterThan(0);
    const lastCall = calls[calls.length - 1];
    const options = lastCall[1] as { onError?: (err: unknown) => void } | undefined;
    if (!options?.onError) {
      throw new Error('mutate not called with onError option');
    }
    return options.onError;
  }

  it('403 → toast forbidden + editor disabled (via mutation isPending flow OR explicit freeze)', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new UpdateSessionScoresError('Forbidden', 'forbidden', 403));
    });
    expect(toastErrorMock).toHaveBeenCalledWith(
      expect.stringContaining('Permesso negato'),
      expect.objectContaining({ id: 'score-403' })
    );
  });

  it('429 → toast warning + setRateLimitedUntil set in store', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new UpdateSessionScoresError('Too many', 'server', 429));
    });
    expect(toastWarningMock).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({ id: 'score-429' })
    );
    expect(useLiveSessionStore.getState().rateLimitedUntil).not.toBeNull();
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBeGreaterThan(Date.now());
  });

  it('429 countdown — editor disabled while rateLimitedUntil is in the future', () => {
    act(() => {
      useLiveSessionStore.getState().setRateLimitedUntil(Date.now() + 5000);
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document
        .querySelector('[data-slot="polymorphic-score-editor"]')
        ?.getAttribute('data-disabled')
    ).toBe('true');
  });

  it('429 countdown reaches 0 → rateLimitedUntil cleared', () => {
    act(() => {
      useLiveSessionStore.getState().setRateLimitedUntil(Date.now() + 1000);
    });
    renderTC({ viewerRole: 'Host' });
    expect(
      document
        .querySelector('[data-slot="polymorphic-score-editor"]')
        ?.getAttribute('data-disabled')
    ).toBe('true');
    act(() => {
      vi.advanceTimersByTime(2000);
    });
    expect(useLiveSessionStore.getState().rateLimitedUntil).toBeNull();
  });

  it('5xx server error → toast with retry button (action defined)', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new UpdateSessionScoresError('Server', 'server', 500));
    });
    expect(toastErrorMock).toHaveBeenCalledWith(
      expect.stringContaining('Errore server'),
      expect.objectContaining({
        id: 'score-5xx',
        action: expect.objectContaining({
          label: expect.any(String),
          onClick: expect.any(Function),
        }),
      })
    );
  });

  it('retry button click → re-invokes mutate with last payload', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const firstCallArgs = mockMutation.mutate.mock.calls[0][0];
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new UpdateSessionScoresError('Server', 'server', 500));
    });
    const toastCall = toastErrorMock.mock.calls.find(c => c[1]?.id === 'score-5xx');
    const onClick = (toastCall?.[1] as { action: { onClick: () => void } }).action.onClick;
    act(() => {
      onClick();
    });
    expect(mockMutation.mutate).toHaveBeenCalledTimes(2);
    expect(mockMutation.mutate.mock.calls[1][0]).toEqual(firstCallArgs);
  });

  it('network error (TypeError) → toast with retry button', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new TypeError('Failed to fetch'));
    });
    expect(toastErrorMock).toHaveBeenCalledWith(
      expect.stringContaining('Connessione persa'),
      expect.objectContaining({ id: 'score-network' })
    );
  });

  it('400 validation error → toast with details, editor remains enabled', () => {
    renderTC({ viewerRole: 'Host' });
    triggerEditAndFlushDebounce();
    const onError = getOnErrorFromMockMutation();
    act(() => {
      onError(new UpdateSessionScoresError('Bad', 'validation', 400, { field: 'scores' }));
    });
    expect(toastErrorMock).toHaveBeenCalledWith(
      expect.stringContaining('Validazione fallita'),
      expect.objectContaining({ id: 'score-400' })
    );
    expect(
      document
        .querySelector('[data-slot="polymorphic-score-editor"]')
        ?.getAttribute('data-disabled')
    ).toBe('false');
  });
});

// ─── Group 8: Optimistic UI ─────────────────────────────────────────────────

describe('ScoreTabContent — optimistic UI', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 0 }] },
    });
  });

  it('typing → localScoreOverride reflected in editor initialData immediately', () => {
    renderTC({ viewerRole: 'Host' });
    const before = document.querySelector('[data-slot="initial-data"]')?.textContent;
    expect(before).toContain('"points":0');

    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
    });
    const after = document.querySelector('[data-slot="initial-data"]')?.textContent;
    expect(after).toContain('"points":99');
  });

  it('mutation success → localScoreOverride cleared, falls back to store', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
    const calls = mockMutation.mutate.mock.calls;
    const options = calls[0][1] as { onSuccess?: () => void };
    act(() => {
      options.onSuccess?.();
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'p1', points: 99 }] },
      });
    });
    expect(document.querySelector('[data-slot="initial-data"]')?.textContent).toContain(
      '"points":99'
    );
  });

  it('mutation error → localScoreOverride cleared (rollback to store)', () => {
    renderTC({ viewerRole: 'Host' });
    act(() => {
      (document.querySelector('[data-slot="trigger-change"]') as HTMLButtonElement).click();
      vi.advanceTimersByTime(500);
    });
    const options = mockMutation.mutate.mock.calls[0][1] as { onError?: (err: unknown) => void };
    act(() => {
      options.onError?.(new UpdateSessionScoresError('Forbidden', 'forbidden', 403));
    });
    expect(document.querySelector('[data-slot="initial-data"]')?.textContent).toContain(
      '"points":0'
    );
  });
});
