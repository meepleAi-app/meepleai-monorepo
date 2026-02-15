import { create } from 'zustand';
import { persist } from 'zustand/middleware';

import type { AgentConfigSnapshot, CacheInfo, CompletionMetadata, CostBreakdown, LatencyBreakdown, PipelineStepTiming, Snippet, StrategyInfo } from '@/lib/agent/playground-sse-parser';

export type PlaygroundStrategy = 'RetrievalOnly' | 'SingleModel' | 'MultiModelConsensus';

export interface Message {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
  metadata?: {
    tokens?: number;
    latency?: number;
    model?: string;
  };
  feedback?: 'up' | 'down' | null;
}

export interface PipelineStep {
  message: string;
  timestamp: number;
}

interface PlaygroundState {
  // Core chat state
  messages: Message[];
  sessionId: string | null;
  isStreaming: boolean;
  currentAgentId: string | null;

  // Strategy selection
  strategy: PlaygroundStrategy;

  // Model/Provider overrides
  modelOverride: string | null;
  providerOverride: string | null;

  // SSE-derived state (per response cycle)
  citations: Snippet[];
  stateUpdates: string[];
  pipelineSteps: PipelineStep[];
  followUpQuestions: string[];
  tokenBreakdown: { prompt: number; completion: number; total: number } | null;
  confidence: number | null;
  latencyMs: number | null;
  agentConfig: AgentConfigSnapshot | null;
  latencyBreakdown: LatencyBreakdown | null;
  costBreakdown: CostBreakdown | null;
  sessionTotalCost: number;
  activeStrategy: string | null;
  strategyInfo: StrategyInfo | null;
  pipelineTimings: PipelineStepTiming[];

  // Cache observability (Issue #4443)
  cacheInfo: CacheInfo | null;
  sessionCacheHits: number;
  sessionCacheRequests: number;

  // System message
  systemMessage: string;

  // Game context for RAG
  currentGameId: string | null;

  // Actions - Core
  addMessage: (message: Omit<Message, 'id' | 'timestamp'>) => void;
  appendToLastMessage: (content: string) => void;
  updateMessageMetadata: (messageId: string, metadata: Message['metadata']) => void;
  setMessageFeedback: (messageId: string, feedback: 'up' | 'down' | null) => void;
  clearMessages: () => void;
  setStreaming: (isStreaming: boolean) => void;
  setCurrentAgent: (agentId: string | null) => void;
  startSession: () => void;
  endSession: () => void;

  // Actions - Strategy
  setStrategy: (strategy: PlaygroundStrategy) => void;

  // Actions - Model override
  setModelOverride: (model: string | null) => void;
  setProviderOverride: (provider: string | null) => void;
  resetOverrides: () => void;

  // Actions - SSE state
  addCitations: (citations: Snippet[]) => void;
  addStateUpdate: (message: string) => void;
  setFollowUpQuestions: (questions: string[]) => void;
  setCompletionMetadata: (metadata: CompletionMetadata) => void;
  setLatencyMs: (latencyMs: number) => void;
  clearResponseState: () => void;

  // Actions - System message
  setSystemMessage: (message: string) => void;

  // Actions - Game context
  setCurrentGameId: (gameId: string | null) => void;
}

export const usePlaygroundStore = create<PlaygroundState>()(
  persist(
    (set) => ({
      // Core state
      messages: [],
      sessionId: null,
      isStreaming: false,
      currentAgentId: null,

      // Strategy
      strategy: 'SingleModel' as PlaygroundStrategy,

      // Model/Provider overrides
      modelOverride: null,
      providerOverride: null,

      // SSE-derived state
      citations: [],
      stateUpdates: [],
      pipelineSteps: [],
      followUpQuestions: [],
      tokenBreakdown: null,
      confidence: null,
      latencyMs: null,
      agentConfig: null,
      latencyBreakdown: null,
      costBreakdown: null,
      sessionTotalCost: 0,
      activeStrategy: null,
      strategyInfo: null,
      pipelineTimings: [],

      // Cache observability
      cacheInfo: null,
      sessionCacheHits: 0,
      sessionCacheRequests: 0,

      // System message
      systemMessage: '',

      // Game context
      currentGameId: null,

      // ─── Core Actions ────────────────────────────

      addMessage: (message) =>
        set((state) => ({
          messages: [
            ...state.messages,
            {
              ...message,
              id: crypto.randomUUID(),
              timestamp: new Date(),
            },
          ],
        })),

      appendToLastMessage: (content) =>
        set((state) => {
          const messages = [...state.messages];
          const lastMessage = messages[messages.length - 1];
          if (lastMessage && lastMessage.role === 'assistant') {
            lastMessage.content += content;
          }
          return { messages };
        }),

      updateMessageMetadata: (messageId, metadata) =>
        set((state) => ({
          messages: state.messages.map((msg) =>
            msg.id === messageId ? { ...msg, metadata: { ...msg.metadata, ...metadata } } : msg
          ),
        })),

      setMessageFeedback: (messageId, feedback) =>
        set((state) => ({
          messages: state.messages.map((msg) =>
            msg.id === messageId ? { ...msg, feedback } : msg
          ),
        })),

      clearMessages: () =>
        set({
          messages: [],
          sessionId: null,
          citations: [],
          stateUpdates: [],
          pipelineSteps: [],
          followUpQuestions: [],
          tokenBreakdown: null,
          confidence: null,
          latencyMs: null,
          agentConfig: null,
          latencyBreakdown: null,
          costBreakdown: null,
          sessionTotalCost: 0,
          activeStrategy: null,
          strategyInfo: null,
          pipelineTimings: [],
          cacheInfo: null,
          sessionCacheHits: 0,
          sessionCacheRequests: 0,
        }),

      setStreaming: (isStreaming) => set({ isStreaming }),

      setCurrentAgent: (agentId) => set({ currentAgentId: agentId }),

      startSession: () =>
        set({
          sessionId: crypto.randomUUID(),
          messages: [],
          isStreaming: false,
          citations: [],
          stateUpdates: [],
          pipelineSteps: [],
          followUpQuestions: [],
          tokenBreakdown: null,
          confidence: null,
          latencyMs: null,
          agentConfig: null,
          latencyBreakdown: null,
          costBreakdown: null,
          sessionTotalCost: 0,
          activeStrategy: null,
          strategyInfo: null,
          pipelineTimings: [],
          cacheInfo: null,
          sessionCacheHits: 0,
          sessionCacheRequests: 0,
        }),

      endSession: () =>
        set({
          sessionId: null,
          isStreaming: false,
        }),

      // ─── Strategy Actions ──────────────────────────

      setStrategy: (strategy) => set({ strategy }),

      // ─── Model Override Actions ────────────────────

      setModelOverride: (model) => set({ modelOverride: model }),
      setProviderOverride: (provider) => set({ providerOverride: provider }),
      resetOverrides: () => set({ modelOverride: null, providerOverride: null }),

      // ─── SSE State Actions ───────────────────────

      addCitations: (citations) =>
        set((state) => ({
          citations: [...state.citations, ...citations],
        })),

      addStateUpdate: (message) =>
        set((state) => ({
          stateUpdates: [...state.stateUpdates, message],
          pipelineSteps: [
            ...state.pipelineSteps,
            { message, timestamp: Date.now() },
          ],
        })),

      setFollowUpQuestions: (questions) =>
        set({ followUpQuestions: questions }),

      setCompletionMetadata: (metadata) =>
        set((state) => {
          const ci = metadata.cacheInfo ?? null;
          const isRequest = ci && ci.status !== 'skip';
          const isHit = ci?.status === 'hit';
          return {
            tokenBreakdown: {
              prompt: metadata.promptTokens,
              completion: metadata.completionTokens,
              total: metadata.totalTokens,
            },
            confidence: metadata.confidence ?? null,
            agentConfig: metadata.agentConfig ?? null,
            latencyBreakdown: metadata.latencyBreakdown ?? null,
            costBreakdown: metadata.costBreakdown ?? null,
            sessionTotalCost: state.sessionTotalCost + (metadata.costBreakdown?.totalCost ?? 0),
            activeStrategy: metadata.strategy ?? null,
            strategyInfo: metadata.strategyInfo ?? null,
            pipelineTimings: metadata.pipelineTimings ?? [],
            cacheInfo: ci,
            sessionCacheRequests: state.sessionCacheRequests + (isRequest ? 1 : 0),
            sessionCacheHits: state.sessionCacheHits + (isHit ? 1 : 0),
          };
        }),

      setLatencyMs: (latencyMs) => set({ latencyMs }),

      clearResponseState: () =>
        set({
          citations: [],
          stateUpdates: [],
          pipelineSteps: [],
          followUpQuestions: [],
          tokenBreakdown: null,
          confidence: null,
          latencyMs: null,
          agentConfig: null,
          latencyBreakdown: null,
          costBreakdown: null,
          activeStrategy: null,
          strategyInfo: null,
          pipelineTimings: [],
          cacheInfo: null,
        }),

      // ─── System Message ──────────────────────────

      setSystemMessage: (message) => set({ systemMessage: message }),

      // ─── Game Context ─────────────────────────────

      setCurrentGameId: (gameId) => set({ currentGameId: gameId }),
    }),
    {
      name: 'playground-storage',
      partialize: (state) => ({
        messages: state.messages,
        currentAgentId: state.currentAgentId,
        systemMessage: state.systemMessage,
        currentGameId: state.currentGameId,
        strategy: state.strategy,
      }),
    }
  )
);
