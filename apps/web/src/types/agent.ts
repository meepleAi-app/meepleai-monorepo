/**
 * Agent Chat Types (Issue #3187)
 * Agent Metadata Types (Epic #4068 - Issue #4184)
 */

import { Citation } from '@/lib/api/schemas/streaming.schemas';

/**
 * Agent message type
 */
export type AgentMessageType = 'user' | 'agent' | 'system';

/**
 * Agent chat message
 */
export interface AgentMessage {
  /** Message type */
  type: AgentMessageType;

  /** Message content */
  content: string;

  /** Message timestamp */
  timestamp: Date;

  /** Citations (for agent messages) */
  citations?: Citation[];

  /** Confidence score 0.0-1.0 (for agent messages) - Issue #3244 */
  confidence?: number;
}

/**
 * Agent metadata for status and stats display
 * Epic #4068 - Issue #4184
 */
export interface AgentMetadata {
  /** Agent status (Active/Idle/Training/Error) */
  status: 'active' | 'idle' | 'training' | 'error';

  /** AI model information */
  model: {
    name: string;
    temperature?: number;
    maxTokens?: number;
  };

  /** Total invocation count */
  invocationCount: number;

  /** Last execution timestamp (ISO 8601) */
  lastExecuted?: string;

  /** Average response time in milliseconds */
  avgResponseTime?: number;

  /** Agent capabilities */
  capabilities: Array<'RAG' | 'Vision' | 'Code' | 'Functions' | 'MultiTurn'>;
}
