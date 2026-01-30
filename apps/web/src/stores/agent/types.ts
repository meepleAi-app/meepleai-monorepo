/**
 * Agent Store Types (Issue #3188)
 *
 * Type definitions for agent state management
 */

import { AgentMode } from '@/components/agent';
import { AgentMessage } from '@/types/agent';

/**
 * Agent configuration for a specific game
 */
export interface AgentConfig {
  /** Game ID this config belongs to */
  gameId: string;

  /** Selected agent mode */
  mode: AgentMode;

  /** Temperature for LLM (0-1) */
  temperature: number;

  /** Max tokens for response */
  maxTokens: number;

  /** Whether to use RAG */
  useRAG: boolean;

  /** Custom system prompt (optional) */
  systemPrompt?: string;

  /** Last updated timestamp */
  updatedAt: Date;
}

/**
 * Active agent session
 */
export interface AgentSession {
  /** Unique session ID */
  sessionId: string;

  /** Game ID for this session */
  gameId: string;

  /** Agent mode for this session */
  mode: AgentMode;

  /** Session start time */
  startedAt: Date;

  /** Session status */
  status: 'active' | 'ended';

  /** Number of messages in session */
  messageCount: number;
}

/**
 * Conversation cache entry for localStorage
 */
export interface ConversationCache {
  /** Session ID */
  sessionId: string;

  /** Game ID */
  gameId: string;

  /** Conversation messages */
  messages: AgentMessage[];

  /** Last updated timestamp */
  lastUpdated: Date;

  /** Whether synced with backend */
  synced: boolean;
}

/**
 * Error state for store operations
 */
export interface AgentStoreError {
  /** Error message */
  message: string;

  /** Error code (for retry logic) */
  code?: string;

  /** Timestamp */
  timestamp: Date;
}

/**
 * Loading states for async operations
 */
export interface LoadingStates {
  loadingConfig: boolean;
  savingConfig: boolean;
  launchingSession: boolean;
  sendingMessage: boolean;
}
