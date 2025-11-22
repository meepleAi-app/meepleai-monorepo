/**
 * Custom metrics for k6 tests
 *
 * Defines application-specific metrics beyond standard HTTP metrics.
 */

import { Counter, Gauge, Rate, Trend } from 'k6/metrics';

/**
 * RAG Search Metrics
 */
export const ragConfidence = new Trend('rag_confidence', true);
export const ragSnippetCount = new Trend('rag_snippet_count', true);
export const ragTokens = new Trend('rag_tokens_total', true);

/**
 * Cache Metrics
 */
export const cacheHitRate = new Rate('cache_hit_rate');
export const cacheOperations = new Counter('cache_operations_total');

/**
 * Database Metrics
 */
export const dbQueryTime = new Trend('db_query_time', true);
export const dbConnectionErrors = new Counter('db_connection_errors');

/**
 * WebSocket Metrics
 */
export const wsMessageRate = new Rate('ws_message_rate');
export const wsConnectionDuration = new Trend('ws_connection_duration', true);
export const wsErrors = new Counter('ws_errors_total');

/**
 * Custom Counters
 */
export const authErrors = new Counter('auth_errors_total');
export const validationErrors = new Counter('validation_errors_total');
export const timeoutErrors = new Counter('timeout_errors_total');

/**
 * Response Quality Metrics
 */
export const responseSize = new Trend('response_size_bytes', true);
export const errorRate = new Rate('error_rate');

/**
 * Record RAG search metrics from response
 */
export function recordRagMetrics(response) {
  if (response.status === 200) {
    try {
      const body = JSON.parse(response.body);

      if (body.results && Array.isArray(body.results)) {
        ragSnippetCount.add(body.results.length);

        // Calculate average confidence from snippets
        if (body.results.length > 0) {
          const avgConfidence = body.results.reduce((sum, r) => sum + (r.score || 0), 0) / body.results.length;
          ragConfidence.add(avgConfidence);
        }
      }

      if (body.totalTokens) {
        ragTokens.add(body.totalTokens);
      }

      responseSize.add(response.body.length);
    } catch (e) {
      console.error('Failed to parse RAG response:', e);
    }
  } else {
    errorRate.add(1);
  }
}

/**
 * Record cache metrics
 */
export function recordCacheMetrics(isHit) {
  cacheOperations.add(1);
  cacheHitRate.add(isHit ? 1 : 0);
}

/**
 * Record database metrics
 */
export function recordDbMetrics(queryTime, hasError = false) {
  dbQueryTime.add(queryTime);
  if (hasError) {
    dbConnectionErrors.add(1);
  }
}

/**
 * Record WebSocket metrics
 */
export function recordWsMetrics(connectionDuration, messageCount, hasError = false) {
  wsConnectionDuration.add(connectionDuration);
  wsMessageRate.add(messageCount > 0 ? 1 : 0);
  if (hasError) {
    wsErrors.add(1);
  }
}

/**
 * Record error by type
 */
export function recordError(errorType) {
  errorRate.add(1);

  switch (errorType) {
    case 'auth':
      authErrors.add(1);
      break;
    case 'validation':
      validationErrors.add(1);
      break;
    case 'timeout':
      timeoutErrors.add(1);
      break;
    default:
      break;
  }
}
