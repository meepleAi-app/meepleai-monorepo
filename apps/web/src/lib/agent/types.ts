/**
 * Agent Type Definitions
 * Issue #3237 (FRONT-001)
 */

export interface AgentTypology {
  id: string;
  name: string;
  description: string;
  systemPrompt: string;
  status: 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected';
}

export interface AgentSession {
  id: string;
  gameSessionId: string;
  userId: string;
  gameId: string;
  typologyId: string;
  isActive: boolean;
  startedAt: Date;
  endedAt?: Date;
}

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
  citations?: Citation[];
  confidence?: number;
}

export interface Citation {
  sourceId: string;
  pageNumber: number;
  excerpt: string;
}

export interface AgentConfig {
  modelType: string;
  temperature: number;
  maxTokens: number;
  ragStrategy: 'HybridSearch' | 'VectorOnly' | 'MultiModelConsensus';
  ragParams: Record<string, unknown>;
}

export interface SSEEvent {
  type: 'message' | 'citation' | 'done' | 'error';
  data: unknown;
}
