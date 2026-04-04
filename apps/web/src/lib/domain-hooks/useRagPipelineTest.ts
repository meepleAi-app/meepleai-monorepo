/**
 * useRagPipelineTest Hook (Issue #3463)
 *
 * SSE streaming for real-time RAG pipeline testing results.
 * Used by the visual strategy builder to test custom pipelines.
 *
 * Features:
 * - Real-time block execution updates via SSE
 * - Progress tracking with block-level granularity
 * - Metrics aggregation (tokens, cost, latency)
 * - Retrieved documents display
 * - Validation results display
 *
 * Backend endpoint:
 * - POST /api/v1/admin/rag-pipeline/test
 *
 * @example
 * ```typescript
 * const {
 *   isRunning,
 *   progress,
 *   events,
 *   metrics,
 *   startTest,
 *   cancelTest
 * } = useRagPipelineTest({
 *   onBlockStart: (block) => console.log('Block started:', block),
 *   onBlockComplete: (block) => console.log('Block completed:', block),
 *   onComplete: (result) => console.log('Test completed:', result),
 * });
 * ```
 */

import { useState, useCallback, useRef } from 'react';

import type { PipelineDefinition } from '@/components/rag-dashboard/builder/types';
import { logger } from '@/lib/logger';

// =============================================================================
// Event Types (from backend)
// =============================================================================

export enum PipelineTestEventType {
  TestStarted = 'PipelineTestStartedEvent',
  BlockStarted = 'BlockExecutionStartedEvent',
  BlockCompleted = 'BlockExecutionCompletedEvent',
  DocumentsRetrieved = 'DocumentsRetrievedEvent',
  ValidationResult = 'ValidationResultEvent',
  TestCompleted = 'PipelineTestCompletedEvent',
}

export interface BaseTestEvent {
  eventType: string;
  timestamp: string;
}

export interface TestStartedEvent extends BaseTestEvent {
  eventType: 'PipelineTestStartedEvent';
  pipelineId: string;
  query: string;
  totalBlocks: number;
}

export interface BlockStartedEvent extends BaseTestEvent {
  eventType: 'BlockExecutionStartedEvent';
  blockId: string;
  blockType: string;
  blockName: string;
  blockIndex: number;
}

export interface BlockCompletedEvent extends BaseTestEvent {
  eventType: 'BlockExecutionCompletedEvent';
  blockId: string;
  blockType: string;
  success: boolean;
  durationMs: number;
  tokensUsed: number;
  cost: number;
  output?: string;
  error?: string;
}

export interface RetrievedDocument {
  id: string;
  title: string;
  content: string;
  score: number;
  metadata?: Record<string, string>;
}

export interface DocumentsRetrievedEvent extends BaseTestEvent {
  eventType: 'DocumentsRetrievedEvent';
  blockId: string;
  documentCount: number;
  documents: RetrievedDocument[];
}

export interface ValidationResultEvent extends BaseTestEvent {
  eventType: 'ValidationResultEvent';
  blockId: string;
  validationType: string;
  passed: boolean;
  score: number;
  details?: string;
}

export interface TestCompletedEvent extends BaseTestEvent {
  eventType: 'PipelineTestCompletedEvent';
  success: boolean;
  totalDurationMs: number;
  totalTokensUsed: number;
  totalCost: number;
  blocksExecuted: number;
  blocksFailed: number;
  finalResponse?: string;
  error?: string;
}

export type PipelineTestEvent =
  | TestStartedEvent
  | BlockStartedEvent
  | BlockCompletedEvent
  | DocumentsRetrievedEvent
  | ValidationResultEvent
  | TestCompletedEvent;

// =============================================================================
// Hook Types
// =============================================================================

export interface TestProgress {
  totalBlocks: number;
  completedBlocks: number;
  currentBlock?: {
    id: string;
    name: string;
    type: string;
    index: number;
  };
  percentage: number;
}

export interface TestMetrics {
  totalDurationMs: number;
  totalTokensUsed: number;
  totalCost: number;
  blocksExecuted: number;
  blocksFailed: number;
}

export interface BlockResult {
  blockId: string;
  blockType: string;
  blockName: string;
  success: boolean;
  durationMs: number;
  tokensUsed: number;
  cost: number;
  output?: string;
  error?: string;
  documents?: RetrievedDocument[];
  validation?: {
    type: string;
    passed: boolean;
    score: number;
    details?: string;
  };
}

export interface UseRagPipelineTestOptions {
  /** Callback when test starts */
  onTestStart?: (event: TestStartedEvent) => void;
  /** Callback when a block starts */
  onBlockStart?: (event: BlockStartedEvent) => void;
  /** Callback when a block completes */
  onBlockComplete?: (event: BlockCompletedEvent) => void;
  /** Callback when documents are retrieved */
  onDocumentsRetrieved?: (event: DocumentsRetrievedEvent) => void;
  /** Callback when validation completes */
  onValidationResult?: (event: ValidationResultEvent) => void;
  /** Callback when test completes */
  onComplete?: (event: TestCompletedEvent) => void;
  /** Callback when an error occurs */
  onError?: (error: Error) => void;
  /** API base URL */
  apiBaseUrl?: string;
}

export interface UseRagPipelineTestReturn {
  /** Whether test is currently running */
  isRunning: boolean;
  /** Current test progress */
  progress: TestProgress;
  /** All events received */
  events: PipelineTestEvent[];
  /** Aggregated metrics */
  metrics: TestMetrics;
  /** Block results map */
  blockResults: Map<string, BlockResult>;
  /** Final response from agent blocks */
  finalResponse?: string;
  /** Error if test failed */
  error: Error | null;
  /** Start a pipeline test */
  startTest: (pipeline: PipelineDefinition, query: string) => Promise<void>;
  /** Cancel running test */
  cancelTest: () => void;
  /** Reset state */
  reset: () => void;
}

// =============================================================================
// Hook Implementation
// =============================================================================

export function useRagPipelineTest(
  options: UseRagPipelineTestOptions = {}
): UseRagPipelineTestReturn {
  const {
    onTestStart,
    onBlockStart,
    onBlockComplete,
    onDocumentsRetrieved,
    onValidationResult,
    onComplete,
    onError,
    apiBaseUrl,
  } = options;

  // State
  const [isRunning, setIsRunning] = useState(false);
  const [events, setEvents] = useState<PipelineTestEvent[]>([]);
  const [error, setError] = useState<Error | null>(null);
  const [finalResponse, setFinalResponse] = useState<string>();
  const [progress, setProgress] = useState<TestProgress>({
    totalBlocks: 0,
    completedBlocks: 0,
    percentage: 0,
  });
  const [metrics, setMetrics] = useState<TestMetrics>({
    totalDurationMs: 0,
    totalTokensUsed: 0,
    totalCost: 0,
    blocksExecuted: 0,
    blocksFailed: 0,
  });
  const [blockResults, setBlockResults] = useState<Map<string, BlockResult>>(new Map());

  // Abort controller for cancellation
  const abortControllerRef = useRef<AbortController | null>(null);

  /**
   * Parse SSE event line
   */
  const parseSSEEvent = useCallback((eventType: string, data: string): PipelineTestEvent | null => {
    try {
      return JSON.parse(data) as PipelineTestEvent;
    } catch {
      logger.error(`[useRagPipelineTest] Failed to parse event: ${eventType} ${data}`);
      return null;
    }
  }, []);

  /**
   * Process incoming event
   */
  const processEvent = useCallback(
    (event: PipelineTestEvent) => {
      setEvents(prev => [...prev, event]);

      switch (event.eventType) {
        case 'PipelineTestStartedEvent': {
          const e = event as TestStartedEvent;
          setProgress({
            totalBlocks: e.totalBlocks,
            completedBlocks: 0,
            percentage: 0,
          });
          onTestStart?.(e);
          break;
        }

        case 'BlockExecutionStartedEvent': {
          const e = event as BlockStartedEvent;
          setProgress(prev => ({
            ...prev,
            currentBlock: {
              id: e.blockId,
              name: e.blockName,
              type: e.blockType,
              index: e.blockIndex,
            },
          }));
          onBlockStart?.(e);
          break;
        }

        case 'BlockExecutionCompletedEvent': {
          const e = event as BlockCompletedEvent;
          setProgress(prev => ({
            ...prev,
            completedBlocks: prev.completedBlocks + 1,
            percentage:
              prev.totalBlocks > 0
                ? Math.round(((prev.completedBlocks + 1) / prev.totalBlocks) * 100)
                : 0,
          }));
          setMetrics(prev => ({
            ...prev,
            totalTokensUsed: prev.totalTokensUsed + e.tokensUsed,
            totalCost: prev.totalCost + e.cost,
            blocksExecuted: prev.blocksExecuted + 1,
            blocksFailed: prev.blocksFailed + (e.success ? 0 : 1),
          }));
          setBlockResults(prev => {
            const result: BlockResult = {
              blockId: e.blockId,
              blockType: e.blockType,
              blockName: progress.currentBlock?.name || e.blockType,
              success: e.success,
              durationMs: e.durationMs,
              tokensUsed: e.tokensUsed,
              cost: e.cost,
              output: e.output,
              error: e.error,
              // Merge with existing documents/validation
              ...prev.get(e.blockId),
            };
            return new Map(prev).set(e.blockId, result);
          });
          onBlockComplete?.(e);
          break;
        }

        case 'DocumentsRetrievedEvent': {
          const e = event as DocumentsRetrievedEvent;
          setBlockResults(prev => {
            const existing = prev.get(e.blockId) || {
              blockId: e.blockId,
              blockType: 'unknown',
              blockName: 'Unknown',
              success: true,
              durationMs: 0,
              tokensUsed: 0,
              cost: 0,
            };
            return new Map(prev).set(e.blockId, {
              ...existing,
              documents: e.documents,
            });
          });
          onDocumentsRetrieved?.(e);
          break;
        }

        case 'ValidationResultEvent': {
          const e = event as ValidationResultEvent;
          setBlockResults(prev => {
            const existing = prev.get(e.blockId) || {
              blockId: e.blockId,
              blockType: 'unknown',
              blockName: 'Unknown',
              success: true,
              durationMs: 0,
              tokensUsed: 0,
              cost: 0,
            };
            return new Map(prev).set(e.blockId, {
              ...existing,
              validation: {
                type: e.validationType,
                passed: e.passed,
                score: e.score,
                details: e.details,
              },
            });
          });
          onValidationResult?.(e);
          break;
        }

        case 'PipelineTestCompletedEvent': {
          const e = event as TestCompletedEvent;
          setIsRunning(false);
          setMetrics({
            totalDurationMs: e.totalDurationMs,
            totalTokensUsed: e.totalTokensUsed,
            totalCost: e.totalCost,
            blocksExecuted: e.blocksExecuted,
            blocksFailed: e.blocksFailed,
          });
          if (e.finalResponse) {
            setFinalResponse(e.finalResponse);
          }
          if (!e.success && e.error) {
            setError(new Error(e.error));
          }
          onComplete?.(e);
          break;
        }
      }
    },
    [
      onTestStart,
      onBlockStart,
      onBlockComplete,
      onDocumentsRetrieved,
      onValidationResult,
      onComplete,
      progress.currentBlock?.name,
    ]
  );

  /**
   * Start pipeline test
   */
  const startTest = useCallback(
    async (pipeline: PipelineDefinition, query: string) => {
      // Reset state
      setIsRunning(true);
      setEvents([]);
      setError(null);
      setFinalResponse(undefined);
      setProgress({ totalBlocks: 0, completedBlocks: 0, percentage: 0 });
      setMetrics({
        totalDurationMs: 0,
        totalTokensUsed: 0,
        totalCost: 0,
        blocksExecuted: 0,
        blocksFailed: 0,
      });
      setBlockResults(new Map());

      // Create abort controller
      abortControllerRef.current = new AbortController();

      const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';
      const endpoint = `${baseUrl}/api/v1/admin/rag-pipeline/test`;

      try {
        const response = await fetch(endpoint, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          credentials: 'include',
          body: JSON.stringify({
            pipelineDefinition: JSON.stringify(pipeline),
            testQuery: query,
          }),
          signal: abortControllerRef.current.signal,
        });

        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(`Test failed: ${response.status} - ${errorText}`);
        }

        if (!response.body) {
          throw new Error('No response body');
        }

        // Read SSE stream
        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';
        let currentEventType = '';

        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split('\n');
          buffer = lines.pop() || '';

          for (const line of lines) {
            if (line.startsWith('event: ')) {
              currentEventType = line.slice(7).trim();
            } else if (line.startsWith('data: ') && currentEventType) {
              const data = line.slice(6);
              const event = parseSSEEvent(currentEventType, data);
              if (event) {
                processEvent(event);
              }
              currentEventType = '';
            }
          }
        }
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') {
          // Test was cancelled
          setIsRunning(false);
          return;
        }

        const errorObj = err instanceof Error ? err : new Error('Pipeline test failed');
        setError(errorObj);
        setIsRunning(false);
        onError?.(errorObj);
      }
    },
    [apiBaseUrl, parseSSEEvent, processEvent, onError]
  );

  /**
   * Cancel running test
   */
  const cancelTest = useCallback(() => {
    abortControllerRef.current?.abort();
    setIsRunning(false);
  }, []);

  /**
   * Reset all state
   */
  const reset = useCallback(() => {
    setIsRunning(false);
    setEvents([]);
    setError(null);
    setFinalResponse(undefined);
    setProgress({ totalBlocks: 0, completedBlocks: 0, percentage: 0 });
    setMetrics({
      totalDurationMs: 0,
      totalTokensUsed: 0,
      totalCost: 0,
      blocksExecuted: 0,
      blocksFailed: 0,
    });
    setBlockResults(new Map());
  }, []);

  return {
    isRunning,
    progress,
    events,
    metrics,
    blockResults,
    finalResponse,
    error,
    startTest,
    cancelTest,
    reset,
  };
}

export default useRagPipelineTest;
