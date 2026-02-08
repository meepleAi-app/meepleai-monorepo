import { create } from 'zustand';
import { persist } from 'zustand/middleware';

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
}

interface PlaygroundState {
  messages: Message[];
  sessionId: string | null;
  isStreaming: boolean;
  currentAgentId: string | null;

  // Actions
  addMessage: (message: Omit<Message, 'id' | 'timestamp'>) => void;
  appendToLastMessage: (content: string) => void;
  updateMessageMetadata: (messageId: string, metadata: Message['metadata']) => void;
  clearMessages: () => void;
  setStreaming: (isStreaming: boolean) => void;
  setCurrentAgent: (agentId: string | null) => void;
  startSession: () => void;
  endSession: () => void;
}

export const usePlaygroundStore = create<PlaygroundState>()(
  persist(
    (set) => ({
      messages: [],
      sessionId: null,
      isStreaming: false,
      currentAgentId: null,

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

      clearMessages: () => set({ messages: [], sessionId: null }),

      setStreaming: (isStreaming) => set({ isStreaming }),

      setCurrentAgent: (agentId) => set({ currentAgentId: agentId }),

      startSession: () =>
        set({
          sessionId: crypto.randomUUID(),
          messages: [],
          isStreaming: false,
        }),

      endSession: () =>
        set({
          sessionId: null,
          isStreaming: false,
        }),
    }),
    {
      name: 'playground-storage',
      partialize: (state) => ({
        messages: state.messages,
        currentAgentId: state.currentAgentId,
      }),
    }
  )
);
