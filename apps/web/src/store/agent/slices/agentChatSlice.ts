/* eslint-disable security/detect-object-injection -- Safe Zustand store key access with typed session IDs */
/**
 * Agent Chat Slice (Issue #3187)
 *
 * Manages session-based agent chat state:
 * - Messages organized by session
 * - Streaming state
 * - Sidebar minimize/maximize
 * - Citations and metadata
 *
 * Dependencies: none (standalone slice)
 */

import { StateCreator } from 'zustand';

import { Citation } from '@/lib/api/schemas/streaming.schemas';
import { AgentMessage } from '@/types/agent';

export interface AgentChatSlice {
  // ============================================================================
  // State
  // ============================================================================

  /** Messages by session ID */
  messagesBySession: Record<string, AgentMessage[]>;

  /** Whether sidebar is minimized */
  isMinimized: boolean;

  /** Whether streaming is active */
  isStreaming: boolean;

  /** Current session ID (if any) */
  currentSessionId: string | null;

  // ============================================================================
  // Actions
  // ============================================================================

  /** Add a message to session */
  addMessage: (sessionId: string, message: AgentMessage) => void;

  /** Update last message in session (for token accumulation) */
  updateLastMessage: (sessionId: string, token: string, citations?: Citation[]) => void;

  /** Clear messages for session */
  clearMessages: (sessionId: string) => void;

  /** Set minimized state */
  setMinimized: (minimized: boolean) => void;

  /** Set streaming state */
  setStreaming: (streaming: boolean) => void;

  /** Set current session */
  setCurrentSession: (sessionId: string | null) => void;
}

export const createAgentChatSlice: StateCreator<
  AgentChatSlice,
  [['zustand/immer', never]],
  [],
  AgentChatSlice
> = set => ({
  // ============================================================================
  // Initial State
  // ============================================================================
  messagesBySession: {},
  isMinimized: false,
  isStreaming: false,
  currentSessionId: null,

  // ============================================================================
  // Actions
  // ============================================================================

  /**
   * Add message to session
   */
  addMessage: (sessionId, message) =>
    set(state => {
      if (!state.messagesBySession[sessionId]) {
        state.messagesBySession[sessionId] = [];
      }
      state.messagesBySession[sessionId].push(message);
    }),

  /**
   * Update last message (for streaming token accumulation)
   */
  updateLastMessage: (sessionId, token, citations) =>
    set(state => {
      const messages = state.messagesBySession[sessionId];
      if (!messages || messages.length === 0) return;

      const lastMessage = messages[messages.length - 1];
      if (lastMessage.type !== 'agent') return;

      // Append token to content
      if (token) {
        lastMessage.content += token;
      }

      // Add citations if provided
      if (citations) {
        lastMessage.citations = citations;
      }
    }),

  /**
   * Clear messages for session
   */
  clearMessages: sessionId =>
    set(state => {
      state.messagesBySession[sessionId] = [];
    }),

  /**
   * Set minimized state
   */
  setMinimized: minimized =>
    set(state => {
      state.isMinimized = minimized;
    }),

  /**
   * Set streaming state
   */
  setStreaming: streaming =>
    set(state => {
      state.isStreaming = streaming;
    }),

  /**
   * Set current session
   */
  setCurrentSession: sessionId =>
    set(state => {
      state.currentSessionId = sessionId;
    }),
});
