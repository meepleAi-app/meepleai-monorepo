/**
 * Agent Module - Barrel Exports
 * Issue #3237 (FRONT-001)
 */

export { AgentApiClient, agentApi } from './api-client';
export { SSEHandler } from './sse-handler';
export type {
  AgentTypology,
  AgentSession,
  ChatMessage,
  Citation,
  AgentConfig,
  SSEEvent,
} from './types';
export type { SSEHandlerOptions } from './sse-handler';
