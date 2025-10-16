// UI-04: Timeline types for RAG conversation events

export type TimelineEventType =
  | "message" // User or assistant message
  | "rag_search" // Vector search initiated
  | "rag_retrieval" // Citations retrieved
  | "rag_generation" // Answer generation started
  | "rag_complete" // Generation completed with metrics
  | "error"; // Error occurred

export type TimelineEventStatus = "pending" | "in_progress" | "success" | "error";

export interface Snippet {
  text: string;
  source: string;
  page?: number | null;
  line?: number | null;
}

export interface TimelineMetrics {
  latencyMs?: number;
  promptTokens?: number;
  completionTokens?: number;
  totalTokens?: number;
  confidence?: number;
}

export interface TimelineEventData {
  message?: string;
  role?: "user" | "assistant";
  citations?: Snippet[];
  metrics?: TimelineMetrics;
  error?: string;
  endpoint?: string;
  gameId?: string;
  chatId?: string;
}

export interface TimelineEvent {
  id: string;
  type: TimelineEventType;
  timestamp: Date;
  status: TimelineEventStatus;
  data: TimelineEventData;
  relatedMessageId?: string; // Links to chat message
}

export interface TimelineFilters {
  eventTypes: Set<TimelineEventType>;
  statuses: Set<TimelineEventStatus>;
  startDate?: Date;
  endDate?: Date;
  searchText?: string;
}

export const DEFAULT_FILTERS: TimelineFilters = {
  eventTypes: new Set([
    "message",
    "rag_search",
    "rag_retrieval",
    "rag_generation",
    "rag_complete",
    "error"
  ]),
  statuses: new Set(["pending", "in_progress", "success", "error"])
};

// Helper functions
export const getEventTypeLabel = (type: TimelineEventType): string => {
  const labels: Record<TimelineEventType, string> = {
    message: "Messaggio",
    rag_search: "Ricerca RAG",
    rag_retrieval: "Recupero Citazioni",
    rag_generation: "Generazione Risposta",
    rag_complete: "Completato",
    error: "Errore"
  };
  return labels[type];
};

export const getEventTypeColor = (type: TimelineEventType): string => {
  const colors: Record<TimelineEventType, string> = {
    message: "#1a73e8",
    rag_search: "#e37400",
    rag_retrieval: "#1e8e3e",
    rag_generation: "#9334e6",
    rag_complete: "#188038",
    error: "#d93025"
  };
  return colors[type];
};

export const getStatusColor = (status: TimelineEventStatus): string => {
  const colors: Record<TimelineEventStatus, string> = {
    pending: "#9aa0a6",
    in_progress: "#1a73e8",
    success: "#188038",
    error: "#d93025"
  };
  return colors[status];
};

export const getStatusIcon = (status: TimelineEventStatus): string => {
  const icons: Record<TimelineEventStatus, string> = {
    pending: "⏱️",
    in_progress: "🔄",
    success: "✅",
    error: "❌"
  };
  return icons[status];
};

export const formatDuration = (ms: number): string => {
  if (ms < 1000) return `${ms}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(2)}s`;
  return `${(ms / 60000).toFixed(2)}m`;
};
