/**
 * Contextual Hand Store — Unit Tests
 *
 * Tests for the Zustand store that powers Session Flow v2.1's
 * "Contextual Hand" UI. Validates state transitions, API delegation,
 * error handling, and selector correctness.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import type {
  CurrentSessionDto,
  CreateSessionResult,
  RollSessionDiceResult,
  SetTurnOrderResult,
  AdvanceTurnResult,
  UpsertScoreResult,
  DiaryEntryDto,
  KbReadinessDto,
} from '@/lib/api/session-flow/types';

// ─── Mock the API module ──────────────────────────────────────────────────
// The store imports `api` from `@/lib/api`. vi.mock is hoisted so we cannot
// reference variables declared in the test scope. We use vi.hoisted() to
// create the mocks before the factory runs.

const { mockSessionFlow } = vi.hoisted(() => ({
  mockSessionFlow: {
    getCurrentSession: vi.fn(),
    createSession: vi.fn(),
    pauseSession: vi.fn(),
    resumeSession: vi.fn(),
    setTurnOrder: vi.fn(),
    advanceTurn: vi.fn(),
    rollSessionDice: vi.fn(),
    upsertScore: vi.fn(),
    getSessionDiary: vi.fn(),
    getKbReadiness: vi.fn(),
    getGameNightDiary: vi.fn(),
    completeGameNight: vi.fn(),
  },
}));

vi.mock('@/lib/api', () => ({
  api: { sessionFlow: mockSessionFlow },
}));

// Import store AFTER mock is registered
import {
  useContextualHandStore,
  selectContext,
  selectCurrentSession,
  selectIsLoading,
  selectError,
  selectDiaryEntries,
  selectIsDiaryLoading,
  selectKbReadiness,
  selectCreateResult,
  selectHasActiveSession,
  selectSessionId,
} from '../store';

// ─── Helpers ──────────────────────────────────────────────────────────────

function resetStore() {
  useContextualHandStore.setState({
    context: 'idle',
    currentSession: null,
    createResult: null,
    isLoading: false,
    error: null,
    diaryEntries: [],
    isDiaryLoading: false,
    kbReadiness: null,
  });
}

function makeSession(overrides: Partial<CurrentSessionDto> = {}): CurrentSessionDto {
  return {
    sessionId: 's1',
    gameId: 'g1',
    status: 'Active',
    sessionCode: 'ABC123',
    sessionDate: '2026-04-10T12:00:00Z',
    updatedAt: null,
    gameNightEventId: null,
    ...overrides,
  };
}

function makeCreateResult(overrides: Partial<CreateSessionResult> = {}): CreateSessionResult {
  return {
    sessionId: 's1',
    sessionCode: 'XYZ',
    participants: [],
    gameNightEventId: 'gn1',
    gameNightWasCreated: true,
    agentDefinitionId: 'a1',
    toolkitId: 't1',
    ...overrides,
  };
}

// ─── Tests ────────────────────────────────────────────────────────────────

describe('useContextualHandStore', () => {
  beforeEach(() => {
    resetStore();
    vi.clearAllMocks();
  });

  // ── initialize ──────────────────────────────────────────────────────

  describe('initialize', () => {
    it('sets context to idle when no current session exists', async () => {
      mockSessionFlow.getCurrentSession.mockResolvedValue(null);

      await useContextualHandStore.getState().initialize();

      const state = useContextualHandStore.getState();
      expect(state.context).toBe('idle');
      expect(state.currentSession).toBeNull();
      expect(state.isLoading).toBe(false);
    });

    it('loads active session and sets context to active', async () => {
      const session = makeSession({ status: 'Active' });
      mockSessionFlow.getCurrentSession.mockResolvedValue(session);

      await useContextualHandStore.getState().initialize();

      const state = useContextualHandStore.getState();
      expect(state.context).toBe('active');
      expect(state.currentSession).toEqual(session);
      expect(state.isLoading).toBe(false);
    });

    it('loads paused session and sets context to paused', async () => {
      const session = makeSession({ status: 'Paused' });
      mockSessionFlow.getCurrentSession.mockResolvedValue(session);

      await useContextualHandStore.getState().initialize();

      expect(useContextualHandStore.getState().context).toBe('paused');
    });

    it('maps unknown status to idle context', async () => {
      const session = makeSession({ status: 'Completed' });
      mockSessionFlow.getCurrentSession.mockResolvedValue(session);

      await useContextualHandStore.getState().initialize();

      expect(useContextualHandStore.getState().context).toBe('idle');
    });

    it('sets error on API failure', async () => {
      mockSessionFlow.getCurrentSession.mockRejectedValue(new Error('Network error'));

      await useContextualHandStore.getState().initialize();

      const state = useContextualHandStore.getState();
      expect(state.error).toBe('Network error');
      expect(state.isLoading).toBe(false);
    });

    it('sets isLoading to true during the call', async () => {
      let resolvePromise: (v: null) => void;
      mockSessionFlow.getCurrentSession.mockReturnValue(
        new Promise(resolve => {
          resolvePromise = resolve;
        })
      );

      const promise = useContextualHandStore.getState().initialize();
      expect(useContextualHandStore.getState().isLoading).toBe(true);

      resolvePromise!(null);
      await promise;

      expect(useContextualHandStore.getState().isLoading).toBe(false);
    });
  });

  // ── startSession ────────────────────────────────────────────────────

  describe('startSession', () => {
    it('creates session and transitions to active', async () => {
      const result = makeCreateResult();
      mockSessionFlow.createSession.mockResolvedValue(result);

      await useContextualHandStore.getState().startSession('g1');

      const state = useContextualHandStore.getState();
      expect(state.context).toBe('active');
      expect(state.createResult).toEqual(result);
      expect(state.currentSession?.sessionId).toBe('s1');
      expect(state.currentSession?.gameId).toBe('g1');
      expect(state.currentSession?.status).toBe('Active');
      expect(state.isLoading).toBe(false);
    });

    it('passes guestNames and gameNightEventId to the API', async () => {
      mockSessionFlow.createSession.mockResolvedValue(makeCreateResult());

      await useContextualHandStore.getState().startSession('g1', ['Alice'], 'gn99');

      expect(mockSessionFlow.createSession).toHaveBeenCalledWith('g1', {
        sessionType: 'GameSpecific',
        participants: [],
        guestNames: ['Alice'],
        gameNightEventId: 'gn99',
      });
    });

    it('sets error on failure without changing context', async () => {
      mockSessionFlow.createSession.mockRejectedValue(new Error('KB not ready'));

      await useContextualHandStore.getState().startSession('g1');

      const state = useContextualHandStore.getState();
      expect(state.error).toBe('KB not ready');
      expect(state.context).toBe('idle'); // unchanged
      expect(state.isLoading).toBe(false);
    });
  });

  // ── pauseSession ────────────────────────────────────────────────────

  describe('pauseSession', () => {
    it('transitions active session to paused', async () => {
      mockSessionFlow.pauseSession.mockResolvedValue(undefined);
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      await useContextualHandStore.getState().pauseSession();

      const state = useContextualHandStore.getState();
      expect(state.context).toBe('paused');
      expect(state.currentSession?.status).toBe('Paused');
      expect(state.isLoading).toBe(false);
    });

    it('is a no-op when there is no current session', async () => {
      await useContextualHandStore.getState().pauseSession();

      expect(mockSessionFlow.pauseSession).not.toHaveBeenCalled();
    });

    it('sets error on failure', async () => {
      mockSessionFlow.pauseSession.mockRejectedValue(new Error('Forbidden'));
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      await useContextualHandStore.getState().pauseSession();

      expect(useContextualHandStore.getState().error).toBe('Forbidden');
    });
  });

  // ── resumeSession ───────────────────────────────────────────────────

  describe('resumeSession', () => {
    it('transitions paused session to active', async () => {
      mockSessionFlow.resumeSession.mockResolvedValue(undefined);
      useContextualHandStore.setState({
        context: 'paused',
        currentSession: makeSession({ status: 'Paused' }),
      });

      await useContextualHandStore.getState().resumeSession();

      const state = useContextualHandStore.getState();
      expect(state.context).toBe('active');
      expect(state.currentSession?.status).toBe('Active');
    });

    it('is a no-op when there is no current session', async () => {
      await useContextualHandStore.getState().resumeSession();

      expect(mockSessionFlow.resumeSession).not.toHaveBeenCalled();
    });
  });

  // ── setTurnOrder ────────────────────────────────────────────────────

  describe('setTurnOrder', () => {
    it('delegates to API and returns result', async () => {
      const result: SetTurnOrderResult = {
        method: 'manual',
        seed: null,
        order: ['p1', 'p2'],
      };
      mockSessionFlow.setTurnOrder.mockResolvedValue(result);
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      const returned = await useContextualHandStore.getState().setTurnOrder('manual', ['p1', 'p2']);

      expect(returned).toEqual(result);
    });

    it('returns null when no session exists', async () => {
      const returned = await useContextualHandStore.getState().setTurnOrder('random');
      expect(returned).toBeNull();
      expect(mockSessionFlow.setTurnOrder).not.toHaveBeenCalled();
    });

    it('sets error and returns null on failure', async () => {
      mockSessionFlow.setTurnOrder.mockRejectedValue(new Error('Invalid order'));
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      const returned = await useContextualHandStore.getState().setTurnOrder('manual');

      expect(returned).toBeNull();
      expect(useContextualHandStore.getState().error).toBe('Invalid order');
    });
  });

  // ── advanceTurn ─────────────────────────────────────────────────────

  describe('advanceTurn', () => {
    it('delegates to API and returns result', async () => {
      const result: AdvanceTurnResult = {
        fromIndex: 0,
        toIndex: 1,
        fromParticipantId: 'p1',
        toParticipantId: 'p2',
      };
      mockSessionFlow.advanceTurn.mockResolvedValue(result);
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      const returned = await useContextualHandStore.getState().advanceTurn();
      expect(returned).toEqual(result);
    });

    it('returns null when no session exists', async () => {
      const returned = await useContextualHandStore.getState().advanceTurn();
      expect(returned).toBeNull();
    });
  });

  // ── rollDice ────────────────────────────────────────────────────────

  describe('rollDice', () => {
    it('delegates to API and returns result', async () => {
      const result: RollSessionDiceResult = {
        diceRollId: 'd1',
        formula: '2d6',
        rolls: [3, 5],
        modifier: 0,
        total: 8,
        timestamp: '2026-04-10T12:00:00Z',
      };
      mockSessionFlow.rollSessionDice.mockResolvedValue(result);
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      const returned = await useContextualHandStore.getState().rollDice('p1', '2d6');
      expect(returned).toEqual(result);
      expect(mockSessionFlow.rollSessionDice).toHaveBeenCalledWith('s1', {
        participantId: 'p1',
        formula: '2d6',
        label: undefined,
      });
    });

    it('passes optional label to API', async () => {
      mockSessionFlow.rollSessionDice.mockResolvedValue({
        diceRollId: 'd2',
        formula: '1d20',
        rolls: [17],
        modifier: 0,
        total: 17,
        timestamp: '',
      });
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      await useContextualHandStore.getState().rollDice('p1', '1d20', 'Initiative');

      expect(mockSessionFlow.rollSessionDice).toHaveBeenCalledWith('s1', {
        participantId: 'p1',
        formula: '1d20',
        label: 'Initiative',
      });
    });

    it('returns null when no session exists', async () => {
      const returned = await useContextualHandStore.getState().rollDice('p1', '2d6');
      expect(returned).toBeNull();
    });

    it('sets error and returns null on failure', async () => {
      mockSessionFlow.rollSessionDice.mockRejectedValue(new Error('Server error'));
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      const returned = await useContextualHandStore.getState().rollDice('p1', '2d6');

      expect(returned).toBeNull();
      expect(useContextualHandStore.getState().error).toBe('Server error');
    });
  });

  // ── upsertScore ─────────────────────────────────────────────────────

  describe('upsertScore', () => {
    it('delegates to API and returns result', async () => {
      const result: UpsertScoreResult = {
        scoreEntryId: 'sc1',
        oldValue: 0,
        newValue: 42,
      };
      mockSessionFlow.upsertScore.mockResolvedValue(result);
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      const returned = await useContextualHandStore.getState().upsertScore('p1', 42, 1, 'points', 'Round win');

      expect(returned).toEqual(result);
      expect(mockSessionFlow.upsertScore).toHaveBeenCalledWith('s1', {
        participantId: 'p1',
        newValue: 42,
        roundNumber: 1,
        category: 'points',
        reason: 'Round win',
      });
    });

    it('returns null when no session exists', async () => {
      const returned = await useContextualHandStore.getState().upsertScore('p1', 10);
      expect(returned).toBeNull();
    });
  });

  // ── loadDiary ───────────────────────────────────────────────────────

  describe('loadDiary', () => {
    it('fetches diary entries and stores them', async () => {
      const entries: DiaryEntryDto[] = [
        {
          id: 'e1',
          sessionId: 's1',
          gameNightId: null,
          eventType: 'session_started',
          timestamp: '2026-04-10T12:00:00Z',
          payload: null,
          createdBy: null,
          source: null,
        },
      ];
      mockSessionFlow.getSessionDiary.mockResolvedValue(entries);
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      await useContextualHandStore.getState().loadDiary('session_started');

      const state = useContextualHandStore.getState();
      expect(state.diaryEntries).toEqual(entries);
      expect(state.isDiaryLoading).toBe(false);
    });

    it('is a no-op when there is no current session', async () => {
      await useContextualHandStore.getState().loadDiary();
      expect(mockSessionFlow.getSessionDiary).not.toHaveBeenCalled();
    });

    it('sets isDiaryLoading during the call', async () => {
      let resolvePromise: (v: DiaryEntryDto[]) => void;
      mockSessionFlow.getSessionDiary.mockReturnValue(
        new Promise(resolve => {
          resolvePromise = resolve;
        })
      );
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
      });

      const promise = useContextualHandStore.getState().loadDiary();
      expect(useContextualHandStore.getState().isDiaryLoading).toBe(true);

      resolvePromise!([]);
      await promise;

      expect(useContextualHandStore.getState().isDiaryLoading).toBe(false);
    });
  });

  // ── checkKbReadiness ────────────────────────────────────────────────

  describe('checkKbReadiness', () => {
    it('stores KB readiness result', async () => {
      const result: KbReadinessDto = {
        isReady: true,
        state: 'Ready',
        readyPdfCount: 3,
        failedPdfCount: 0,
        warnings: [],
      };
      mockSessionFlow.getKbReadiness.mockResolvedValue(result);

      await useContextualHandStore.getState().checkKbReadiness('g1');

      expect(useContextualHandStore.getState().kbReadiness).toEqual(result);
    });

    it('sets error on failure', async () => {
      mockSessionFlow.getKbReadiness.mockRejectedValue(new Error('Not found'));

      await useContextualHandStore.getState().checkKbReadiness('g1');

      expect(useContextualHandStore.getState().error).toBe('Not found');
    });
  });

  // ── reset ───────────────────────────────────────────────────────────

  describe('reset', () => {
    it('resets all state to initial values', () => {
      useContextualHandStore.setState({
        context: 'active',
        currentSession: makeSession(),
        createResult: makeCreateResult(),
        isLoading: true,
        error: 'something',
        diaryEntries: [{ id: 'e1' } as DiaryEntryDto],
        isDiaryLoading: true,
        kbReadiness: { isReady: true } as KbReadinessDto,
      });

      useContextualHandStore.getState().reset();

      const state = useContextualHandStore.getState();
      expect(state.context).toBe('idle');
      expect(state.currentSession).toBeNull();
      expect(state.createResult).toBeNull();
      expect(state.isLoading).toBe(false);
      expect(state.error).toBeNull();
      expect(state.diaryEntries).toEqual([]);
      expect(state.isDiaryLoading).toBe(false);
      expect(state.kbReadiness).toBeNull();
    });
  });

  // ── Selectors ───────────────────────────────────────────────────────

  describe('selectors', () => {
    it('selectContext returns current context', () => {
      useContextualHandStore.setState({ context: 'paused' });
      expect(selectContext(useContextualHandStore.getState())).toBe('paused');
    });

    it('selectCurrentSession returns session or null', () => {
      expect(selectCurrentSession(useContextualHandStore.getState())).toBeNull();
      const session = makeSession();
      useContextualHandStore.setState({ currentSession: session });
      expect(selectCurrentSession(useContextualHandStore.getState())).toEqual(session);
    });

    it('selectIsLoading returns loading state', () => {
      useContextualHandStore.setState({ isLoading: true });
      expect(selectIsLoading(useContextualHandStore.getState())).toBe(true);
    });

    it('selectError returns error string or null', () => {
      useContextualHandStore.setState({ error: 'oops' });
      expect(selectError(useContextualHandStore.getState())).toBe('oops');
    });

    it('selectDiaryEntries returns diary entries', () => {
      const entries = [{ id: 'e1' } as DiaryEntryDto];
      useContextualHandStore.setState({ diaryEntries: entries });
      expect(selectDiaryEntries(useContextualHandStore.getState())).toEqual(entries);
    });

    it('selectIsDiaryLoading returns diary loading state', () => {
      useContextualHandStore.setState({ isDiaryLoading: true });
      expect(selectIsDiaryLoading(useContextualHandStore.getState())).toBe(true);
    });

    it('selectKbReadiness returns KB readiness', () => {
      const kb = { isReady: true } as KbReadinessDto;
      useContextualHandStore.setState({ kbReadiness: kb });
      expect(selectKbReadiness(useContextualHandStore.getState())).toEqual(kb);
    });

    it('selectCreateResult returns create result', () => {
      const result = makeCreateResult();
      useContextualHandStore.setState({ createResult: result });
      expect(selectCreateResult(useContextualHandStore.getState())).toEqual(result);
    });

    it('selectHasActiveSession returns true for active or paused', () => {
      useContextualHandStore.setState({ context: 'idle' });
      expect(selectHasActiveSession(useContextualHandStore.getState())).toBe(false);

      useContextualHandStore.setState({ context: 'active' });
      expect(selectHasActiveSession(useContextualHandStore.getState())).toBe(true);

      useContextualHandStore.setState({ context: 'paused' });
      expect(selectHasActiveSession(useContextualHandStore.getState())).toBe(true);

      useContextualHandStore.setState({ context: 'setup' });
      expect(selectHasActiveSession(useContextualHandStore.getState())).toBe(false);
    });

    it('selectSessionId returns session ID or null', () => {
      expect(selectSessionId(useContextualHandStore.getState())).toBeNull();

      useContextualHandStore.setState({ currentSession: makeSession({ sessionId: 'abc' }) });
      expect(selectSessionId(useContextualHandStore.getState())).toBe('abc');
    });
  });
});
