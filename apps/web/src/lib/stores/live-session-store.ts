/**
 * Live Session Store
 *
 * Game Night Improvvisata — Tasks 13/14
 *
 * Zustand store for real-time session state management.
 * Driven by SignalR events from GameStateHub.
 */

import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
import type { TurnOrderType } from '@/lib/session-live/turn-state';

export interface PlayerInfo {
  id: string;
  name: string;
  /**
   * Optional user-facing label (#2389 Block A). Adapters should prefer
   * `displayName ?? name` when rendering rosters. Becomes required once all
   * SignalR/REST adapters populate it consistently (Block A finalization).
   */
  displayName?: string;
  isHost: boolean;
  isOnline: boolean;
}

export interface ScoreProposal {
  id: string;
  playerName: string;
  delta: number;
  timestamp: number;
}

export interface RuleDispute {
  id: string;
  description: string;
  verdict: string;
  ruleReferences: string[];
  raisedByPlayerName: string;
  timestamp: string;
  // v2 fields (optional for backward compatibility)
  confidence?: 'High' | 'Medium' | 'Low';
  outcome?: 'Pending' | 'VerdictAccepted' | 'VerdictOverridden';
  votesAccepted?: number;
  votesRejected?: number;
  overrideRule?: string;
}

export type SessionStatus = 'InProgress' | 'Paused' | 'Completed';

interface LiveSessionState {
  sessionId: string | null;
  gameName: string;
  status: SessionStatus;
  currentTurn: number;
  currentPhase: string | null;
  players: PlayerInfo[];
  scoringType: ScoreType | null;
  scoreData: ScoreDataByType[ScoreType] | null;
  /**
   * Turn order type for the session — populated from the REST DTO on initial load.
   * Static for the session lifecycle (path B, no SignalR event).
   * Null until the DTO is loaded (or if the session has no toolkit wired).
   * Issue #2483 Task 2.
   */
  turnOrderType: TurnOrderType | null;
  /**
   * Rate-limit deadline (Unix timestamp in ms). `null` when not rate-limited.
   * Set by `ScoreTabContent` on 429 response (Date.now() + 30000).
   * Persists across tab change (ScoreTabContent unmount/remount) so the
   * countdown UI continues from the correct remaining time.
   * Cleared on natural expiry, store reset(), or explicit setRateLimitedUntil(null).
   * Issue #2430 Block B+.
   */
  rateLimitedUntil: number | null;
  pendingProposals: ScoreProposal[];
  disputes: RuleDispute[];
  isConnected: boolean;
  isOffline: boolean;
  elapsedSeconds: number;

  // Actions
  setSession: (data: Partial<LiveSessionState>) => void;
  setScoringConfig: <T extends ScoreType>(args: {
    scoringType: T;
    scoreData: ScoreDataByType[T];
  }) => void;
  /** Sets the turn order type from the REST DTO. Issue #2483 Task 2. */
  setTurnOrderType: (type: TurnOrderType | null) => void;
  setRateLimitedUntil: (ts: number | null) => void;
  addProposal: (proposal: ScoreProposal) => void;
  resolveProposal: (proposalId: string, accepted: boolean) => void;
  addDispute: (dispute: RuleDispute) => void;
  setConnected: (connected: boolean) => void;
  setOffline: (offline: boolean) => void;
  reset: () => void;
}

const initialState: Omit<
  LiveSessionState,
  | 'setSession'
  | 'setScoringConfig'
  | 'setTurnOrderType'
  | 'setRateLimitedUntil'
  | 'addProposal'
  | 'resolveProposal'
  | 'addDispute'
  | 'setConnected'
  | 'setOffline'
  | 'reset'
> = {
  sessionId: null,
  gameName: '',
  status: 'InProgress',
  currentTurn: 1,
  currentPhase: null,
  players: [],
  scoringType: null,
  scoreData: null,
  turnOrderType: null,
  rateLimitedUntil: null,
  pendingProposals: [],
  disputes: [],
  isConnected: false,
  isOffline: false,
  elapsedSeconds: 0,
};

export const useLiveSessionStore = create<LiveSessionState>()(
  devtools(
    (set, get) => ({
      ...initialState,

      setSession: data => set(data as Partial<LiveSessionState>, false, 'setSession'),

      setScoringConfig: ({ scoringType, scoreData }) =>
        set({ scoringType, scoreData }, false, 'setScoringConfig'),

      setTurnOrderType: type => set({ turnOrderType: type }, false, 'setTurnOrderType'),

      setRateLimitedUntil: ts => set({ rateLimitedUntil: ts }, false, 'setRateLimitedUntil'),

      addProposal: proposal =>
        set(
          state => ({
            pendingProposals: [...state.pendingProposals, proposal],
          }),
          false,
          'addProposal'
        ),

      /**
       * Removes a proposal from the pending queue. The `accepted` flag is
       * preserved on the signature for SignalR adapter compatibility, but
       * Block C (#2389) no longer mutates a legacy `scores` map here —
       * actual score application lives in the polymorphic `scoreData`
       * pipeline (see `PolymorphicScoreEditor` + `UpdateSessionScoresCommand`).
       */
      resolveProposal: (proposalId, _accepted) => {
        const proposal = get().pendingProposals.find(p => p.id === proposalId);
        if (!proposal) return;
        set(
          state => ({
            pendingProposals: state.pendingProposals.filter(p => p.id !== proposalId),
          }),
          false,
          'resolveProposal'
        );
      },

      addDispute: dispute =>
        set(
          state => ({
            disputes: [...state.disputes, dispute],
          }),
          false,
          'addDispute'
        ),

      setConnected: connected => set({ isConnected: connected }, false, 'setConnected'),

      setOffline: offline => set({ isOffline: offline }, false, 'setOffline'),

      reset: () => set(initialState, false, 'reset'),
    }),
    { name: 'live-session-store' }
  )
);
