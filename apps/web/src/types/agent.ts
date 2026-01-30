/**
 * Agent Chat Types (Issue #3187)
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
}
