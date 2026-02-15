/**
 * Playground SSE Parser
 * Parses RagStreamingEvent format from backend (Issue #4393)
 *
 * Backend format: data: {"type":<enum>,"data":{...},"timestamp":"..."}
 * See: apps/api/src/Api/Models/Contracts.cs → RagStreamingEvent
 */

// ─── Types ──────────────────────────────────────────────

export enum StreamingEventType {
  StateUpdate = 0,
  Citations = 1,
  Outline = 2,
  ScriptChunk = 3,
  Complete = 4,
  Error = 5,
  Heartbeat = 6,
  Token = 7,
  FollowUpQuestions = 8,
  SetupStep = 9,
}

export interface RagStreamingEvent {
  type: StreamingEventType;
  data: unknown;
  timestamp: string;
}

export interface Snippet {
  text: string;
  source: string;
  page: number;
  line: number;
  score: number;
}

export interface AgentConfigSnapshot {
  agentName: string;
  model: string;
  temperature: number;
  maxTokens: number;
  provider: string;
  isModelOverride?: boolean;
}

export interface LatencyBreakdown {
  totalMs: number;
  retrievalMs: number;
  generationMs: number;
}

export interface CostBreakdown {
  llmCost: number;
  inputCost: number;
  outputCost: number;
  totalCost: number;
  isFree: boolean;
}

export interface StrategyInfo {
  name: string;
  type: 'retrieval' | 'generation' | 'consensus' | 'validation' | 'custom';
  parameters: Record<string, unknown>;
}

export interface PipelineStepTiming {
  name: string;
  type: 'retrieval' | 'compute' | 'llm';
  durationMs: number;
  detail: string | null;
}

export interface CacheInfo {
  status: 'hit' | 'miss' | 'skip';
  tier: string | null;
  cacheKey: string | null;
  latencyMs: number;
  ttlSeconds: number;
}

export interface ApiTrace {
  service: 'vector_search' | 'llm' | 'embedding' | 'reranker' | string;
  method: string;
  url: string;
  requestSizeBytes: number;
  responseSizeBytes: number;
  statusCode: number;
  latencyMs: number;
  detail: string | null;
  requestPreview: string | null;
  responsePreview: string | null;
}

export interface LogEntry {
  level: 'info' | 'warn' | 'error' | 'debug';
  source: string;
  message: string;
  timestamp: string;
}

export interface TomacLayer {
  id: string;
  name: string;
  status: 'active' | 'planned' | 'bypassed';
  latencyMs: number;
  itemsProcessed: number;
  score: number | null;
  description: string;
}

export interface CompletionMetadata {
  estimatedReadingTimeMinutes?: number;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  confidence?: number;
  strategy?: string;
  costBreakdown?: CostBreakdown;
  agentConfig?: AgentConfigSnapshot;
  latencyBreakdown?: LatencyBreakdown;
  strategyInfo?: StrategyInfo;
  pipelineTimings?: PipelineStepTiming[];
  cacheInfo?: CacheInfo;
  apiTraces?: ApiTrace[];
  logEntries?: LogEntry[];
  tomacLayers?: TomacLayer[];
}

export interface StreamingError {
  errorMessage: string;
  errorCode?: string;
}

// ─── Handler Interface ──────────────────────────────────

export interface PlaygroundSSEHandlers {
  onStateUpdate: (message: string) => void;
  onCitations: (citations: Snippet[]) => void;
  onToken: (token: string) => void;
  onComplete: (metadata: CompletionMetadata) => void;
  onFollowUpQuestions: (questions: string[]) => void;
  onError: (error: StreamingError) => void;
  onHeartbeat: () => void;
}

// ─── Parser ─────────────────────────────────────────────

/**
 * Parse a single SSE line from the playground stream.
 * Expects format: `data: {"type":<n>,"data":{...},"timestamp":"..."}`
 * Non-data lines (empty, comments, event: lines) are silently ignored.
 */
export function parsePlaygroundSSELine(
  line: string,
  handlers: PlaygroundSSEHandlers,
): void {
  const trimmed = line.trim();

  // Only process data lines
  if (!trimmed.startsWith('data: ')) return;

  const jsonStr = trimmed.slice(6); // Remove "data: " prefix

  // Handle [DONE] signal
  if (jsonStr === '[DONE]') return;

  let event: RagStreamingEvent;
  try {
    event = JSON.parse(jsonStr) as RagStreamingEvent;
  } catch {
    // Malformed JSON - call error handler but don't crash
    handlers.onError({
      errorMessage: `Failed to parse SSE event: ${jsonStr.slice(0, 100)}`,
      errorCode: 'PARSE_ERROR',
    });
    return;
  }

  switch (event.type) {
    case StreamingEventType.StateUpdate: {
      const data = event.data as { message: string };
      handlers.onStateUpdate(data.message);
      break;
    }

    case StreamingEventType.Citations: {
      const data = event.data as { citations: Snippet[] };
      handlers.onCitations(data.citations ?? []);
      break;
    }

    case StreamingEventType.Token: {
      const data = event.data as { token: string };
      handlers.onToken(data.token);
      break;
    }

    case StreamingEventType.Complete: {
      const data = event.data as CompletionMetadata;
      handlers.onComplete(data);
      break;
    }

    case StreamingEventType.Error: {
      const data = event.data as StreamingError;
      handlers.onError(data);
      break;
    }

    case StreamingEventType.Heartbeat: {
      handlers.onHeartbeat();
      break;
    }

    case StreamingEventType.FollowUpQuestions: {
      const data = event.data as { questions: string[] };
      handlers.onFollowUpQuestions(data.questions ?? []);
      break;
    }

    case StreamingEventType.Outline:
    case StreamingEventType.ScriptChunk:
    case StreamingEventType.SetupStep:
      // These event types are used by other consumers (explain, setup guides)
      // Playground ignores them silently
      break;

    default:
      // Unknown event type - ignore gracefully
      break;
  }
}

/**
 * Process a chunk of SSE data that may contain multiple lines.
 * Handles the case where chunks arrive with multiple events.
 */
export function parsePlaygroundSSEChunk(
  chunk: string,
  handlers: PlaygroundSSEHandlers,
): void {
  const lines = chunk.split('\n');
  for (const line of lines) {
    parsePlaygroundSSELine(line, handlers);
  }
}
