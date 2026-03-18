'use client';

/**
 * RAG Pipeline Explorer - Admin Dashboard
 *
 * Interactive visualization of RAG execution pipeline with:
 * - Execution selector dropdown with metadata
 * - 6-step pipeline diagram (Query → Embedding → VectorSearch → Reranking → LLM → Response)
 * - Expandable timeline with detailed step information
 * - Click node to scroll to timeline step
 *
 * Based on: admin-mockups/agents-pipeline.html
 * Issue: Epic RAG observability
 */

import { useState, useEffect, useCallback } from 'react';

import { useRouter } from 'next/navigation';

import {
  PipelineDiagram,
  TimelineStep,
  ConfidenceBadge,
  StrategyBadge,
  type PipelineStep,
  type TimelineStepDetail,
} from '@/components/admin/rag';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  createAdminClient,
  type RagExecutionListItem,
  type RagExecutionDetail,
} from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

// Initialize AdminClient instance
const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

// =============================================================================
// Type Definitions
// =============================================================================

interface ExecutionTraceStep {
  name: string;
  icon: string;
  latencyMs: number;
  status: 'ok' | 'error' | 'cache';
  details: Record<string, unknown>;
}

interface ExecutionTrace {
  steps: ExecutionTraceStep[];
  totalLatencyMs: number;
}

// =============================================================================
// Helper Functions
// =============================================================================

/**
 * Parse executionTrace JSON string into structured data
 */
function parseExecutionTrace(traceJson: string | null): ExecutionTrace | null {
  if (!traceJson) return null;

  try {
    const parsed = JSON.parse(traceJson);

    // Transform backend trace format to frontend format
    if (parsed.steps && Array.isArray(parsed.steps)) {
      return {
        steps: parsed.steps,
        totalLatencyMs: parsed.totalLatencyMs || 0,
      };
    }

    return null;
  } catch (error) {
    logger.error('Failed to parse execution trace:', error);
    return null;
  }
}

/**
 * Calculate percentage of total time
 */
function calculatePercentage(stepMs: number, totalMs: number): number {
  return totalMs > 0 ? (stepMs / totalMs) * 100 : 0;
}

/**
 * Get latency color class
 */
function getLatencyClass(ms: number): 'green' | 'amber' | 'red' {
  if (ms < 100) return 'green';
  if (ms <= 500) return 'amber';
  return 'red';
}

/**
 * Get color for step bar based on percentage
 */
function getStepColor(percentage: number): string {
  if (percentage < 10) return 'hsl(142, 60%, 42%)'; // green
  if (percentage < 30) return 'hsl(142, 50%, 50%)'; // light green
  if (percentage < 50) return 'hsl(38, 92%, 50%)'; // amber
  return 'hsl(0, 72%, 51%)'; // red
}

/**
 * Format timestamp for display
 */
function formatTime(dateString: string | null): string {
  if (!dateString) return 'N/A';

  try {
    const date = new Date(dateString);
    return date.toLocaleTimeString('en-US', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  } catch {
    return 'N/A';
  }
}

/**
 * Transform execution trace step details to timeline detail format
 */
function transformStepDetails(details: Record<string, unknown>): TimelineStepDetail[] {
  const result: TimelineStepDetail[] = [];

  Object.entries(details).forEach(([key, value]) => {
    // Skip null/undefined values
    if (value === null || value === undefined) return;

    // Determine formatting based on key patterns
    const isMono =
      /^(model|query|tokens|score|count|type|collection|dimension|cache|provider|temperature)$/i.test(
        key
      );
    const isWide = /^(query|answer|response|preview)$/i.test(key);

    // Determine badge variant
    let badge: TimelineStepDetail['badge'] = undefined;
    if (key === 'cache' && value === 'HIT') badge = 'green';
    if (key === 'cache' && value === 'MISS') badge = 'red';
    if (key === 'quality' || key === 'intent') badge = 'primary';

    // Format value
    const displayValue = String(value);

    result.push({
      label: key.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase()),
      value: displayValue,
      mono: isMono,
      badge,
      wide: isWide,
    });
  });

  return result;
}

// =============================================================================
// Main Component
// =============================================================================

export default function PipelineExplorerPage() {
  const router = useRouter();

  // State
  const [executions, setExecutions] = useState<RagExecutionListItem[]>([]);
  const [selectedExecutionId, setSelectedExecutionId] = useState<string | null>(null);
  const [executionDetail, setExecutionDetail] = useState<RagExecutionDetail | null>(null);
  const [activeNodeIndex, setActiveNodeIndex] = useState<number | null>(0);
  const [openStepIndexes, setOpenStepIndexes] = useState<Set<number>>(new Set([0]));
  const [isLoadingList, setIsLoadingList] = useState(true);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Fetch executions list on mount
  useEffect(() => {
    async function fetchExecutions() {
      try {
        setIsLoadingList(true);
        setError(null);
        const result = await adminClient.getRagExecutions({ take: 10 });
        setExecutions(result.items);

        // Auto-select first execution
        if (result.items.length > 0) {
          setSelectedExecutionId(result.items[0].id);
        }
      } catch (err) {
        logger.error('Failed to fetch executions:', err);
        setError('Failed to load RAG executions');
      } finally {
        setIsLoadingList(false);
      }
    }

    fetchExecutions();
  }, []);

  // Fetch execution detail when selection changes
  useEffect(() => {
    async function fetchExecutionDetail() {
      if (!selectedExecutionId) {
        setExecutionDetail(null);
        return;
      }

      try {
        setIsLoadingDetail(true);
        setError(null);
        const detail = await adminClient.getRagExecutionById(selectedExecutionId);
        setExecutionDetail(detail);
      } catch (err) {
        logger.error('Failed to fetch execution detail:', err);
        setError('Failed to load execution details');
      } finally {
        setIsLoadingDetail(false);
      }
    }

    fetchExecutionDetail();
  }, [selectedExecutionId]);

  // Parse execution trace
  const executionTrace = executionDetail?.executionTrace
    ? parseExecutionTrace(executionDetail.executionTrace)
    : null;

  // Selected execution metadata
  const selectedExecution = executions.find(e => e.id === selectedExecutionId);

  // Handle node click - scroll to timeline step
  const handleNodeClick = useCallback((index: number) => {
    setActiveNodeIndex(index);

    // Open the step if closed
    setOpenStepIndexes(prev => new Set(prev).add(index));

    // Scroll to timeline step
    setTimeout(() => {
      const element = document.querySelector(`[data-step-index="${index}"]`);
      element?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 100);
  }, []);

  // Handle timeline step toggle
  const handleStepToggle = useCallback((index: number) => {
    setOpenStepIndexes(prev => {
      const next = new Set(prev);
      if (next.has(index)) {
        next.delete(index);
      } else {
        next.add(index);
      }
      return next;
    });
  }, []);

  // Transform trace steps to pipeline steps
  const pipelineSteps: PipelineStep[] =
    executionTrace?.steps.map(step => ({
      name: step.name,
      icon: step.icon,
      latencyMs: step.latencyMs,
      status: step.status,
    })) || [];

  // Calculate total latency
  const totalLatency = executionTrace?.totalLatencyMs || executionDetail?.totalLatencyMs || 0;

  // =============================================================================
  // Render: Loading State
  // =============================================================================

  if (isLoadingList) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-64 w-full" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  // =============================================================================
  // Render: Error State
  // =============================================================================

  if (error && executions.length === 0) {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 p-8 text-center dark:border-red-900 dark:bg-red-950/30">
        <p className="text-red-700 dark:text-red-300">{error}</p>
        <button
          onClick={() => router.refresh()}
          className="mt-4 rounded-lg bg-red-600 px-4 py-2 text-white transition-colors hover:bg-red-700"
        >
          Retry
        </button>
      </div>
    );
  }

  // =============================================================================
  // Render: Empty State
  // =============================================================================

  if (executions.length === 0) {
    return (
      <div className="rounded-xl border border-amber-200 bg-amber-50 p-12 text-center dark:border-amber-900 dark:bg-amber-950/30">
        <span className="mb-4 text-5xl">📊</span>
        <h3 className="font-quicksand text-xl font-bold text-amber-900 dark:text-amber-100">
          No RAG Executions Found
        </h3>
        <p className="mt-2 text-amber-700 dark:text-amber-300">
          Execute some RAG queries to see pipeline visualizations here.
        </p>
      </div>
    );
  }

  // =============================================================================
  // Render: Main Content
  // =============================================================================

  return (
    <div className="space-y-5">
      {/* Execution Selector Card */}
      <div className="glass-card rounded-2xl border border-black/10 bg-white/90 p-5 backdrop-blur-md dark:border-white/10 dark:bg-zinc-800/90">
        <div className="flex flex-wrap items-center gap-4">
          {/* Dropdown */}
          <select
            value={selectedExecutionId || ''}
            onChange={e => setSelectedExecutionId(e.target.value)}
            className={cn(
              'flex-1 min-w-[280px] rounded-xl border border-black/20 px-4 py-2.5',
              'bg-white/70 font-nunito text-sm text-zinc-800 backdrop-blur-sm',
              'cursor-pointer appearance-none transition-colors',
              'hover:bg-white/90 focus:outline-none focus:ring-2 focus:ring-amber-500',
              'dark:border-white/20 dark:bg-zinc-700/70 dark:text-zinc-100',
              'bg-[url(data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxMiIgaGVpZ2h0PSIxMiIgZmlsbD0ibm9uZSIgdmlld0JveD0iMCAwIDEyIDEyIj48cGF0aCBzdHJva2U9IiM4ODgiIHN0cm9rZS13aWR0aD0iMS41IiBkPSJtMyA0LjUgMyAzIDMtMyIvPjwvc3ZnPg==)]',
              'bg-[length:12px] bg-[position:right_14px_center] bg-no-repeat'
            )}
            disabled={isLoadingList}
          >
            {executions.map(exec => (
              <option key={exec.id} value={exec.id}>
                {exec.status === 'success' ? '✓' : '✗'} &quot;{exec.query}&quot; —{' '}
                {exec.totalLatencyMs}ms
                {exec.cacheHit ? ' (cache)' : ''}
              </option>
            ))}
          </select>

          {/* Metadata */}
          {selectedExecution && (
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <div className="flex items-center gap-1.5 text-zinc-600 dark:text-zinc-400">
                Agent:{' '}
                <strong className="text-zinc-800 dark:text-zinc-100">
                  {selectedExecution.agentName || 'Unknown'}
                </strong>
              </div>
              <div className="flex items-center gap-1.5">
                Strategy: <StrategyBadge strategy={selectedExecution.strategy} />
              </div>
              <div className="flex items-center gap-1.5 text-zinc-600 dark:text-zinc-400">
                Time:{' '}
                <strong className="font-mono text-zinc-800 dark:text-zinc-100">
                  {formatTime(selectedExecution.createdAt)}
                </strong>
              </div>
              <div className="flex items-center gap-1.5 text-zinc-600 dark:text-zinc-400">
                Total:{' '}
                <strong
                  className={cn(
                    'font-mono',
                    getLatencyClass(selectedExecution.totalLatencyMs) === 'green' &&
                      'text-green-600 dark:text-green-400',
                    getLatencyClass(selectedExecution.totalLatencyMs) === 'amber' &&
                      'text-amber-600 dark:text-amber-400',
                    getLatencyClass(selectedExecution.totalLatencyMs) === 'red' &&
                      'text-red-600 dark:text-red-400'
                  )}
                >
                  {selectedExecution.totalLatencyMs}ms
                </strong>
              </div>
              {selectedExecution.confidence !== null && (
                <div className="flex items-center gap-1.5">
                  <ConfidenceBadge confidence={selectedExecution.confidence} showValue />
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* Pipeline Diagram Card */}
      <div className="glass-card rounded-2xl border border-black/10 bg-white/90 p-5 backdrop-blur-md dark:border-white/10 dark:bg-zinc-800/90">
        {isLoadingDetail ? (
          <div className="space-y-4">
            <Skeleton className="h-8 w-48" />
            <Skeleton className="h-48 w-full" />
          </div>
        ) : executionTrace && pipelineSteps.length > 0 ? (
          <>
            <div className="mb-4 flex items-center gap-2">
              <span className="text-lg" aria-hidden="true">
                🔍
              </span>
              <h2 className="font-quicksand text-lg font-bold text-zinc-800 dark:text-zinc-100">
                Pipeline Flow
              </h2>
            </div>
            <PipelineDiagram
              steps={pipelineSteps}
              onNodeClick={handleNodeClick}
              activeNodeIndex={activeNodeIndex}
            />
          </>
        ) : (
          <div className="py-12 text-center text-zinc-500 dark:text-zinc-400">
            <p>No execution trace available for this execution.</p>
          </div>
        )}
      </div>

      {/* Timeline Card */}
      <div className="glass-card rounded-2xl border border-black/10 bg-white/90 p-5 backdrop-blur-md dark:border-white/10 dark:bg-zinc-800/90">
        <div className="mb-4 flex items-center gap-2">
          <span className="text-lg" aria-hidden="true">
            📅
          </span>
          <h2 className="font-quicksand text-lg font-bold text-zinc-800 dark:text-zinc-100">
            Step Details
          </h2>
        </div>

        {isLoadingDetail ? (
          <div className="space-y-2">
            {[...Array(6)].map((_, i) => (
              <Skeleton key={i} className="h-16 w-full" />
            ))}
          </div>
        ) : executionTrace && executionTrace.steps.length > 0 ? (
          <div className="space-y-2">
            {executionTrace.steps.map((step, index) => {
              const percentage = calculatePercentage(step.latencyMs, totalLatency);
              const barColor = getStepColor(percentage);
              const latencyClass = getLatencyClass(step.latencyMs);
              const details = transformStepDetails(step.details);

              return (
                <div key={index} data-step-index={index}>
                  <TimelineStep
                    name={step.name}
                    icon={step.icon}
                    durationMs={step.latencyMs}
                    percentOfTotal={percentage}
                    details={details}
                    isActive={activeNodeIndex === index}
                    isOpen={openStepIndexes.has(index)}
                    barColor={barColor}
                    latencyClass={latencyClass}
                    onToggle={() => handleStepToggle(index)}
                  />
                </div>
              );
            })}
          </div>
        ) : (
          <div className="py-12 text-center text-zinc-500 dark:text-zinc-400">
            <p>No step details available.</p>
          </div>
        )}
      </div>
    </div>
  );
}
