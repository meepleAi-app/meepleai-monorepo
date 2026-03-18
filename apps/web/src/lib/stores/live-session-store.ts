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

export interface PlayerInfo {
  id: string;
  name: string;
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
  scores: Record<string, number>;
  pendingProposals: ScoreProposal[];
  disputes: RuleDispute[];
  isConnected: boolean;
  isOffline: boolean;
  elapsedSeconds: number;

  // Actions
  setSession: (data: Partial<LiveSessionState>) => void;
  updateScore: (playerName: string, score: number) => void;
  addProposal: (proposal: ScoreProposal) => void;
  resolveProposal: (proposalId: string, accepted: boolean) => void;
  addDispute: (dispute: RuleDispute) => void;
  setConnected: (connected: boolean) => void;
  setOffline: (offline: boolean) => void;
  reset: () => void;
}

const initialState = {
  sessionId: null as string | null,
  gameName: '',
  status: 'InProgress' as SessionStatus,
  currentTurn: 1,
  currentPhase: null as string | null,
  players: [] as PlayerInfo[],
  scores: {} as Record<string, number>,
  pendingProposals: [] as ScoreProposal[],
  disputes: [] as RuleDispute[],
  isConnected: false,
  isOffline: false,
  elapsedSeconds: 0,
};

export const useLiveSessionStore = create<LiveSessionState>()(
  devtools(
    (set, get) => ({
      ...initialState,

      setSession: data => set(data as Partial<LiveSessionState>, false, 'setSession'),

      updateScore: (playerName, score) =>
        set(
          state => ({
            scores: { ...state.scores, [playerName]: score },
          }),
          false,
          'updateScore'
        ),

      addProposal: proposal =>
        set(
          state => ({
            pendingProposals: [...state.pendingProposals, proposal],
          }),
          false,
          'addProposal'
        ),

      resolveProposal: (proposalId, accepted) => {
        const proposal = get().pendingProposals.find(p => p.id === proposalId);
        if (!proposal) return;

        set(
          state => {
            const pendingProposals = state.pendingProposals.filter(p => p.id !== proposalId);
            if (!accepted) {
              return { pendingProposals };
            }
            const currentScore = state.scores[proposal.playerName] ?? 0;
            return {
              pendingProposals,
              scores: {
                ...state.scores,
                [proposal.playerName]: currentScore + proposal.delta,
              },
            };
          },
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
