/**
 * useRagExecutionReplay Hook (Issue #4459)
 *
 * SSE streaming for replaying past RAG pipeline executions.
 * Based on useRagPipelineTest pattern with replay-specific additions.
 *
 * Features:
 * - Re-executes a stored pipeline execution via SSE
 * - Optional config overrides (strategy, topK, model, temperature)
 * - Reuses PipelineTestEvent types from useRagPipelineTest
 * - Automatic persistence of replay result on backend
 *
 * Backend endpoint:
 * - POST /api/v1/admin/rag-executions/{id}/replay
 */

import { useState, useCallback, useRef } from 'react';

import type { ReplayExecutionRequest } from '@/lib/api/schemas/rag-execution.schemas';
import type {
  PipelineTestEvent,
  TestStartedEvent,
  BlockStartedEvent,
  BlockCompletedEvent,
  DocumentsRetrievedEvent,
  ValidationResultEvent,
  TestCompletedEvent,
  TestProgress,
  TestMetrics,
  BlockResult,
} from '@/lib/hooks/useRagPipelineTest';


// =============================================================================
// Hook Types
// =============================================================================

export interface UseRagExecutionReplayOptions {
  onTestStart?: (event: TestStartedEvent) => void;
  onBlockStart?: (event: BlockStartedEvent) => void;
  onBlockComplete?: (event: BlockCompletedEvent) => void;
  onDocumentsRetrieved?: (event: DocumentsRetrievedEvent) => void;
  onValidationResult?: (event: ValidationResultEvent) => void;
  onComplete?: (event: TestCompletedEvent) => void;
  onError?: (error: Error) => void;
  apiBaseUrl?: string;
}

export interface UseRagExecutionReplayReturn {
  isRunning: boolean;
  progress: TestProgress;
  events: PipelineTestEvent[];
  metrics: TestMetrics;
  blockResults: Map<string, BlockResult>;
  finalResponse?: string;
  error: Error | null;
  startReplay: (executionId: string, overrides?: ReplayExecutionRequest) => Promise<void>;
  cancelReplay: () => void;
  reset: () => void;
}

// =============================================================================
// Hook Implementation
// =============================================================================

export function useRagExecutionReplay(
  options: UseRagExecutionReplayOptions = {}
): UseRagExecutionReplayReturn {
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

  const abortControllerRef = useRef<AbortController | null>(null);
  const currentBlockNameRef = useRef<string>('');

  const parseSSEEvent = useCallback(
    (_eventType: string, data: string): PipelineTestEvent | null => {
      try {
        return JSON.parse(data) as PipelineTestEvent;
      } catch {
        return null;
      }
    },
    []
  );

  const processEvent = useCallback(
    (event: PipelineTestEvent) => {
      setEvents((prev) => [...prev, event]);

      switch (event.eventType) {
        case 'PipelineTestStartedEvent': {
          const e = event as TestStartedEvent;
          setProgress({ totalBlocks: e.totalBlocks, completedBlocks: 0, percentage: 0 });
          onTestStart?.(e);
          break;
        }
        case 'BlockExecutionStartedEvent': {
          const e = event as BlockStartedEvent;
          currentBlockNameRef.current = e.blockName;
          setProgress((prev) => ({
            ...prev,
            currentBlock: { id: e.blockId, name: e.blockName, type: e.blockType, index: e.blockIndex },
          }));
          onBlockStart?.(e);
          break;
        }
        case 'BlockExecutionCompletedEvent': {
          const e = event as BlockCompletedEvent;
          setProgress((prev) => ({
            ...prev,
            completedBlocks: prev.completedBlocks + 1,
            percentage: prev.totalBlocks > 0
              ? Math.round(((prev.completedBlocks + 1) / prev.totalBlocks) * 100)
              : 0,
          }));
          setMetrics((prev) => ({
            ...prev,
            totalTokensUsed: prev.totalTokensUsed + e.tokensUsed,
            totalCost: prev.totalCost + e.cost,
            blocksExecuted: prev.blocksExecuted + 1,
            blocksFailed: prev.blocksFailed + (e.success ? 0 : 1),
          }));
          setBlockResults((prev) => {
            const result: BlockResult = {
              blockId: e.blockId,
              blockType: e.blockType,
              blockName: currentBlockNameRef.current || e.blockType,
              success: e.success,
              durationMs: e.durationMs,
              tokensUsed: e.tokensUsed,
              cost: e.cost,
              output: e.output,
              error: e.error,
              ...prev.get(e.blockId),
            };
            return new Map(prev).set(e.blockId, result);
          });
          onBlockComplete?.(e);
          break;
        }
        case 'DocumentsRetrievedEvent': {
          const e = event as DocumentsRetrievedEvent;
          setBlockResults((prev) => {
            const existing = prev.get(e.blockId) || {
              blockId: e.blockId, blockType: 'unknown', blockName: 'Unknown',
              success: true, durationMs: 0, tokensUsed: 0, cost: 0,
            };
            return new Map(prev).set(e.blockId, { ...existing, documents: e.documents });
          });
          onDocumentsRetrieved?.(e);
          break;
        }
        case 'ValidationResultEvent': {
          const e = event as ValidationResultEvent;
          setBlockResults((prev) => {
            const existing = prev.get(e.blockId) || {
              blockId: e.blockId, blockType: 'unknown', blockName: 'Unknown',
              success: true, durationMs: 0, tokensUsed: 0, cost: 0,
            };
            return new Map(prev).set(e.blockId, {
              ...existing,
              validation: { type: e.validationType, passed: e.passed, score: e.score, details: e.details },
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
          if (e.finalResponse) setFinalResponse(e.finalResponse);
          if (!e.success && e.error) setError(new Error(e.error));
          onComplete?.(e);
          break;
        }
      }
    },
    [onTestStart, onBlockStart, onBlockComplete, onDocumentsRetrieved, onValidationResult, onComplete]
  );

  const startReplay = useCallback(
    async (executionId: string, overrides?: ReplayExecutionRequest) => {
      setIsRunning(true);
      setEvents([]);
      setError(null);
      setFinalResponse(undefined);
      setProgress({ totalBlocks: 0, completedBlocks: 0, percentage: 0 });
      setMetrics({ totalDurationMs: 0, totalTokensUsed: 0, totalCost: 0, blocksExecuted: 0, blocksFailed: 0 });
      setBlockResults(new Map());

      abortControllerRef.current = new AbortController();
      const baseUrl = apiBaseUrl || process.env.NEXT_PUBLIC_API_BASE || '';
      const endpoint = `${baseUrl}/api/v1/admin/rag-executions/${encodeURIComponent(executionId)}/replay`;

      try {
        const response = await fetch(endpoint, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify(overrides || {}),
          signal: abortControllerRef.current.signal,
        });

        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(`Replay failed: ${response.status} - ${errorText}`);
        }

        if (!response.body) throw new Error('No response body');

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
              const event = parseSSEEvent(currentEventType, line.slice(6));
              if (event) processEvent(event);
              currentEventType = '';
            }
          }
        }
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') {
          setIsRunning(false);
          return;
        }
        const errorObj = err instanceof Error ? err : new Error('Replay failed');
        setError(errorObj);
        setIsRunning(false);
        onError?.(errorObj);
      }
    },
    [apiBaseUrl, parseSSEEvent, processEvent, onError]
  );

  const cancelReplay = useCallback(() => {
    abortControllerRef.current?.abort();
    setIsRunning(false);
  }, []);

  const reset = useCallback(() => {
    setIsRunning(false);
    setEvents([]);
    setError(null);
    setFinalResponse(undefined);
    setProgress({ totalBlocks: 0, completedBlocks: 0, percentage: 0 });
    setMetrics({ totalDurationMs: 0, totalTokensUsed: 0, totalCost: 0, blocksExecuted: 0, blocksFailed: 0 });
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
    startReplay,
    cancelReplay,
    reset,
  };
}
