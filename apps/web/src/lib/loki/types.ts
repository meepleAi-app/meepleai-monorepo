/** Risposta grezza dell'API Loki query_range */
export interface LokiQueryRangeResponse {
  status: 'success' | 'error';
  data: {
    resultType: 'streams';
    result: LokiStream[];
  };
}

export interface LokiStream {
  stream: Record<string, string>;
  /** Array di [nanosecond-timestamp-string, log-line] */
  values: [string, string][];
}

/** Entry normalizzata per il frontend */
export interface LokiLogEntry {
  /** ID sintetico: `${streamIndex}-${valueIndex}` */
  id: string;
  /** ISO 8601 */
  timestamp: string;
  /** Nome container, es. "meepleai-api" */
  container: string;
  /** Linea di log grezza */
  message: string;
  level: 'error' | 'warning' | 'info' | 'unknown';
}

/** Risposta della Next.js API route /api/logs */
export interface LogsApiResponse {
  entries: LokiLogEntry[];
  /** Indica se Loki è configurato ma non disponibile */
  lokiUnavailable?: boolean;
}
