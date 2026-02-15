import { create } from 'zustand';
import { persist } from 'zustand/middleware';

import type { CompletionMetadata, Snippet } from '@/lib/agent/playground-sse-parser';

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

  // SSE-derived state (per response cycle)
  citations: Snippet[];
  stateUpdates: string[];
  pipelineSteps: PipelineStep[];
  followUpQuestions: string[];
  tokenBreakdown: { prompt: number; completion: number; total: number } | null;
  confidence: number | null;
  latencyMs: number | null;

  // System message
  systemMessage: string;

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

  // Actions - SSE state
  addCitations: (citations: Snippet[]) => void;
  addStateUpdate: (message: string) => void;
  setFollowUpQuestions: (questions: string[]) => void;
  setCompletionMetadata: (metadata: CompletionMetadata) => void;
  setLatencyMs: (latencyMs: number) => void;
  clearResponseState: () => void;

  // Actions - System message
  setSystemMessage: (message: string) => void;
}

export const usePlaygroundStore = create<PlaygroundState>()(
  persist(
    (set) => ({
      // Core state
      messages: [],
      sessionId: null,
      isStreaming: false,
      currentAgentId: null,

      // SSE-derived state
      citations: [],
      stateUpdates: [],
      pipelineSteps: [],
      followUpQuestions: [],
      tokenBreakdown: null,
      confidence: null,
      latencyMs: null,

      // System message
      systemMessage: '',

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
        }),

      endSession: () =>
        set({
          sessionId: null,
          isStreaming: false,
        }),

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
        set({
          tokenBreakdown: {
            prompt: metadata.promptTokens,
            completion: metadata.completionTokens,
            total: metadata.totalTokens,
          },
          confidence: metadata.confidence ?? null,
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
        }),

      // ─── System Message ──────────────────────────

      setSystemMessage: (message) => set({ systemMessage: message }),
    }),
    {
      name: 'playground-storage',
      partialize: (state) => ({
        messages: state.messages,
        currentAgentId: state.currentAgentId,
        systemMessage: state.systemMessage,
      }),
    }
  )
);
