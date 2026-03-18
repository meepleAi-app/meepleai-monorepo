'use client';

/**
 * RAG Debug Console - Admin Dashboard
 *
 * Live execution monitoring with:
 * - Auto-refresh polling (5s/10s/30s intervals)
 * - Execution table with real-time updates
 * - Click row to view detailed execution breakdown
 * - Advanced filters (strategy, status, confidence, latency, date range)
 * - Waterfall chart for call trace visualization
 * - Load more pagination
 *
 * Based on: admin-rag-observability-mockup.html (Debug Console tab)
 * Issue: Epic RAG observability
 */

import { useState, useEffect, useCallback } from 'react';

import {
  StrategyBadge,
  ConfidenceBadge,
  WaterfallChart,
  type WaterfallCall,
  type WaterfallCallType,
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
  latencyMs: number;
  status: 'ok' | 'error' | 'cache';
  type?: WaterfallCallType;
  startOffsetMs?: number;
}

interface FilterState {
  strategies: string[];
  status: 'all' | 'ok' | 'error' | 'cache';
  agent: string;
  minConfidence: number;
  maxLatencyMs: number;
  dateFrom: string;
  dateTo: string;
}

type RefreshInterval = 5000 | 10000 | 30000;

// =============================================================================
// Helper Functions
// =============================================================================

/**
 * Parse executionTrace JSON string into WaterfallCall array
 */
function parseExecutionTrace(traceJson: string | null): WaterfallCall[] | null {
  if (!traceJson) return null;

  try {
    const parsed = JSON.parse(traceJson);

    if (parsed.steps && Array.isArray(parsed.steps)) {
      // Transform backend trace steps to WaterfallCall format
      let currentOffset = 0;
      return parsed.steps.map((step: ExecutionTraceStep) => {
        const call: WaterfallCall = {
          label: step.name,
          durationMs: step.latencyMs,
          type: step.type || 'llm', // Default to llm if type not specified
          startOffsetMs: step.startOffsetMs ?? currentOffset,
        };
        currentOffset += step.latencyMs;
        return call;
      });
    }

    return null;
  } catch (error) {
    logger.error('Failed to parse execution trace:', error);
    return null;
  }
}

/**
 * Format timestamp for display
 */
function formatTimestamp(timestamp: string): string {
  const date = new Date(timestamp);
  const time = date.toLocaleTimeString('en-US', {
    hour12: false,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
  return time;
}

/**
 * Get status icon
 */
function getStatusIcon(status: string): string {
  switch (status) {
    case 'ok':
      return '✓';
    case 'error':
      return '✗';
    case 'cache':
      return '⚡';
    default:
      return '•';
  }
}

/**
 * Get status color
 */
function getStatusColor(status: string): string {
  switch (status) {
    case 'ok':
      return 'text-green-600 dark:text-green-400';
    case 'error':
      return 'text-red-600 dark:text-red-400';
    case 'cache':
      return 'text-blue-600 dark:text-blue-400';
    default:
      return 'text-zinc-400';
  }
}

/**
 * Get latency color class
 */
function getLatencyClass(ms: number): string {
  if (ms < 100) return 'text-green-600 dark:text-green-400 bg-green-50 dark:bg-green-950/30';
  if (ms <= 500) return 'text-amber-600 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/30';
  return 'text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-950/30';
}

/**
 * Get today's date in YYYY-MM-DD format
 */
function getTodayDate(): string {
  return new Date().toISOString().split('T')[0];
}

// =============================================================================
// Main Component
// =============================================================================

export default function DebugConsolePage() {
  // State
  const [executions, setExecutions] = useState<RagExecutionListItem[]>([]);
  const [selectedExecution, setSelectedExecution] = useState<RagExecutionDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  const [skip, setSkip] = useState(0);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshInterval, setRefreshInterval] = useState<RefreshInterval>(10000);
  const [filters, setFilters] = useState<FilterState>({
    strategies: ['POC', 'SingleModel', 'MultiModelConsensus', 'HybridRAG'],
    status: 'all',
    agent: 'all',
    minConfidence: 0,
    maxLatencyMs: 5000,
    dateFrom: getTodayDate(),
    dateTo: getTodayDate(),
  });

  const take = 20;

  // Fetch executions
  const fetchExecutions = useCallback(
    async (isRefresh = false) => {
      if (!isRefresh && executions.length > 0) {
        setIsLoadingMore(true);
      } else {
        setIsLoading(true);
      }

      try {
        const params: Parameters<typeof adminClient.getRagExecutions>[0] = {
          skip: isRefresh ? 0 : skip,
          take,
          minConfidence: filters.minConfidence > 0 ? filters.minConfidence : undefined,
          maxLatencyMs: filters.maxLatencyMs < 5000 ? filters.maxLatencyMs : undefined,
          dateFrom: filters.dateFrom,
          dateTo: filters.dateTo,
        };

        // Add status filter if not 'all'
        if (filters.status !== 'all') {
          params.status = filters.status;
        }

        // Add strategy filter if not all selected
        if (filters.strategies.length > 0 && filters.strategies.length < 4) {
          params.strategy = filters.strategies.join(',');
        }

        const result = await adminClient.getRagExecutions(params);

        if (isRefresh) {
          setExecutions(result.items);
          setSkip(result.items.length);
        } else {
          setExecutions(prev => [...prev, ...result.items]);
          setSkip(prev => prev + result.items.length);
        }

        setTotalCount(result.totalCount);
      } catch (error) {
        logger.error('Failed to fetch executions:', error);
      } finally {
        setIsLoading(false);
        setIsLoadingMore(false);
      }
    },
    [skip, filters, executions.length]
  );

  // Fetch execution detail
  const fetchExecutionDetail = useCallback(async (id: string) => {
    setIsLoadingDetail(true);
    try {
      const detail = await adminClient.getRagExecutionById(id);
      setSelectedExecution(detail);
    } catch (error) {
      logger.error('Failed to fetch execution detail:', error);
    } finally {
      setIsLoadingDetail(false);
    }
  }, []);

  // Initial load
  useEffect(() => {
    fetchExecutions(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Run once on mount

  // Auto-refresh polling
  useEffect(() => {
    if (!autoRefresh) return;

    const interval = setInterval(() => {
      fetchExecutions(true);
    }, refreshInterval);

    return () => clearInterval(interval);
  }, [autoRefresh, refreshInterval, fetchExecutions]);

  // Handle row click
  const handleRowClick = (execution: RagExecutionListItem) => {
    fetchExecutionDetail(execution.id);
  };

  // Handle load more
  const handleLoadMore = () => {
    fetchExecutions(false);
  };

  // Handle filter changes
  const handleStrategyToggle = (strategy: string) => {
    setFilters(prev => ({
      ...prev,
      strategies: prev.strategies.includes(strategy)
        ? prev.strategies.filter(s => s !== strategy)
        : [...prev.strategies, strategy],
    }));
  };

  const handleApplyFilters = () => {
    setSkip(0);
    setExecutions([]);
    fetchExecutions(true);
  };

  // Parse execution trace for waterfall
  const waterfallCalls = selectedExecution
    ? parseExecutionTrace(selectedExecution.executionTrace)
    : null;

  return (
    <div className="min-h-screen space-y-6 p-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-heading text-2xl font-bold text-zinc-800 dark:text-zinc-100">
            Debug Console
          </h1>
          <p className="mt-1 text-sm text-zinc-500 dark:text-zinc-400">
            Live RAG execution monitoring and debugging
          </p>
        </div>
      </div>

      {/* Main Layout: Table + Filters */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_320px]">
        {/* Left: Execution Table */}
        <div className="glass min-h-[500px] overflow-hidden rounded-2xl border border-amber-200/40 bg-white/70 p-5 shadow-lg backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-900/70">
          {/* Live Header */}
          <div className="mb-4 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <h2 className="font-heading text-base font-bold text-zinc-800 dark:text-zinc-100">
                🔔 Live Executions
              </h2>
              {autoRefresh && (
                <>
                  <div className="h-2 w-2 animate-pulse rounded-full bg-green-500 shadow-[0_0_8px_rgba(34,197,94,0.5)]" />
                  <span className="text-xs font-semibold text-green-600 dark:text-green-400">
                    AUTO
                  </span>
                </>
              )}
            </div>

            {/* Refresh Controls */}
            <div className="flex items-center gap-2">
              <label className="text-xs text-zinc-400">Refresh:</label>
              <select
                value={refreshInterval}
                onChange={e => setRefreshInterval(Number(e.target.value) as RefreshInterval)}
                disabled={!autoRefresh}
                className="rounded-md border border-zinc-200 bg-zinc-50 px-2 py-1 text-xs font-mono text-zinc-700 disabled:opacity-50 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300"
              >
                <option value={5000}>5s</option>
                <option value={10000}>10s</option>
                <option value={30000}>30s</option>
              </select>
              <button
                onClick={() => setAutoRefresh(!autoRefresh)}
                className={cn(
                  'rounded-md px-3 py-1 text-xs font-semibold transition-colors',
                  autoRefresh
                    ? 'bg-green-100 text-green-700 dark:bg-green-950/30 dark:text-green-400'
                    : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400'
                )}
              >
                {autoRefresh ? 'ON' : 'OFF'}
              </button>
            </div>
          </div>

          {/* Execution Table */}
          <div className="overflow-x-auto">
            <table className="w-full border-collapse">
              <thead>
                <tr className="border-b border-zinc-200 dark:border-zinc-700">
                  <th className="pb-3 pr-4 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                    Time
                  </th>
                  <th className="pb-3 pr-4 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                    Status
                  </th>
                  <th className="pb-3 pr-4 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                    Query
                  </th>
                  <th className="pb-3 pr-4 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                    Strategy
                  </th>
                  <th className="pb-3 pr-4 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                    Latency
                  </th>
                  <th className="pb-3 pr-4 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                    Conf.
                  </th>
                  <th className="pb-3 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                    Agent
                  </th>
                </tr>
              </thead>
              <tbody>
                {isLoading ? (
                  // Loading skeleton
                  Array.from({ length: 10 }).map((_, i) => (
                    <tr key={i} className="border-b border-zinc-100 dark:border-zinc-800">
                      <td className="py-3 pr-4">
                        <Skeleton className="h-4 w-16" />
                      </td>
                      <td className="py-3 pr-4">
                        <Skeleton className="h-4 w-4" />
                      </td>
                      <td className="py-3 pr-4">
                        <Skeleton className="h-4 w-48" />
                      </td>
                      <td className="py-3 pr-4">
                        <Skeleton className="h-5 w-24" />
                      </td>
                      <td className="py-3 pr-4">
                        <Skeleton className="h-5 w-16" />
                      </td>
                      <td className="py-3 pr-4">
                        <Skeleton className="h-5 w-12" />
                      </td>
                      <td className="py-3">
                        <Skeleton className="h-4 w-20" />
                      </td>
                    </tr>
                  ))
                ) : executions.length === 0 ? (
                  // Empty state
                  <tr>
                    <td colSpan={7} className="py-12 text-center">
                      <div className="flex flex-col items-center gap-3">
                        <div className="text-4xl opacity-30">🔍</div>
                        <p className="text-sm font-medium text-zinc-400">No executions found</p>
                        <p className="text-xs text-zinc-400">
                          Try adjusting your filters or wait for new executions
                        </p>
                      </div>
                    </td>
                  </tr>
                ) : (
                  // Data rows
                  executions.map(execution => (
                    <tr
                      key={execution.id}
                      onClick={() => handleRowClick(execution)}
                      className={cn(
                        'cursor-pointer border-b border-zinc-100 transition-colors hover:bg-amber-50/50 dark:border-zinc-800 dark:hover:bg-zinc-800/30',
                        selectedExecution?.id === execution.id && 'bg-amber-50 dark:bg-amber-950/20'
                      )}
                    >
                      <td className="py-3 pr-4 text-xs font-mono text-zinc-500 dark:text-zinc-400">
                        {formatTimestamp(execution.createdAt)}
                      </td>
                      <td className="py-3 pr-4">
                        <span className={cn('text-sm', getStatusColor(execution.status))}>
                          {getStatusIcon(execution.status)}
                        </span>
                      </td>
                      <td className="py-3 pr-4 text-sm text-zinc-700 dark:text-zinc-300">
                        <div className="max-w-md truncate">{execution.query}</div>
                      </td>
                      <td className="py-3 pr-4">
                        <StrategyBadge strategy={execution.strategy} />
                      </td>
                      <td className="py-3 pr-4">
                        <span
                          className={cn(
                            'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-mono font-semibold',
                            getLatencyClass(execution.totalLatencyMs)
                          )}
                        >
                          {execution.totalLatencyMs}ms
                        </span>
                      </td>
                      <td className="py-3 pr-4">
                        {execution.confidence !== null && (
                          <ConfidenceBadge confidence={execution.confidence} />
                        )}
                      </td>
                      <td className="py-3 text-xs text-zinc-500 dark:text-zinc-400">
                        {execution.agentName || 'N/A'}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Load More Button */}
          {!isLoading && executions.length < totalCount && (
            <div className="mt-4 text-center">
              <button
                onClick={handleLoadMore}
                disabled={isLoadingMore}
                className="rounded-lg bg-amber-100 px-4 py-2 text-xs font-semibold text-amber-900 transition-colors hover:bg-amber-200 disabled:opacity-50 dark:bg-amber-950/30 dark:text-amber-400 dark:hover:bg-amber-950/50"
              >
                {isLoadingMore
                  ? 'Loading...'
                  : `Load More (${totalCount - executions.length} remaining)`}
              </button>
            </div>
          )}

          {/* Execution Detail Panel */}
          {selectedExecution && (
            <div className="mt-6 border-t border-zinc-200 pt-6 dark:border-zinc-700">
              <h3 className="mb-4 font-heading text-sm font-bold text-zinc-800 dark:text-zinc-100">
                🔍 Execution Detail
              </h3>

              {isLoadingDetail ? (
                <div className="space-y-4">
                  <Skeleton className="h-24 w-full" />
                  <Skeleton className="h-32 w-full" />
                  <Skeleton className="h-48 w-full" />
                </div>
              ) : (
                <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
                  {/* Left Column: Query, Response, Documents */}
                  <div className="space-y-4">
                    {/* User Query */}
                    <div>
                      <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                        User Query
                      </div>
                      <div className="rounded-xl border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-700 dark:bg-zinc-800">
                        <span className="text-sm font-mono text-zinc-700 dark:text-zinc-300">
                          {selectedExecution.query}
                        </span>
                      </div>
                    </div>

                    {/* Execution Metadata */}
                    <div>
                      <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                        Execution Details
                      </div>
                      <div className="space-y-2 rounded-xl border border-zinc-200 bg-zinc-50 p-3 dark:border-zinc-700 dark:bg-zinc-800">
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-zinc-500 dark:text-zinc-400">Status:</span>
                          <span
                            className={cn(
                              'font-semibold',
                              getStatusColor(selectedExecution.status)
                            )}
                          >
                            {selectedExecution.status}
                          </span>
                        </div>
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-zinc-500 dark:text-zinc-400">Model:</span>
                          <span className="font-mono font-semibold text-zinc-700 dark:text-zinc-300">
                            {selectedExecution.model || 'N/A'}
                          </span>
                        </div>
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-zinc-500 dark:text-zinc-400">Provider:</span>
                          <span className="font-semibold text-zinc-700 dark:text-zinc-300">
                            {selectedExecution.provider || 'N/A'}
                          </span>
                        </div>
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-zinc-500 dark:text-zinc-400">Tokens:</span>
                          <span className="font-mono font-semibold text-zinc-700 dark:text-zinc-300">
                            {selectedExecution.totalTokens.toLocaleString()}
                          </span>
                        </div>
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-zinc-500 dark:text-zinc-400">Cost:</span>
                          <span className="font-mono font-semibold text-zinc-700 dark:text-zinc-300">
                            ${selectedExecution.totalCost.toFixed(4)}
                          </span>
                        </div>
                        <div className="flex items-center justify-between text-xs">
                          <span className="text-zinc-500 dark:text-zinc-400">Cache Hit:</span>
                          <span
                            className={cn(
                              'font-semibold',
                              selectedExecution.cacheHit
                                ? 'text-green-600 dark:text-green-400'
                                : 'text-zinc-400'
                            )}
                          >
                            {selectedExecution.cacheHit ? 'Yes' : 'No'}
                          </span>
                        </div>
                        {selectedExecution.errorMessage && (
                          <div className="border-t border-zinc-200 pt-2 dark:border-zinc-700">
                            <span className="text-xs text-red-600 dark:text-red-400">
                              {selectedExecution.errorMessage}
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Right Column: Waterfall Chart */}
                  <div>
                    <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                      Call Trace (Waterfall)
                    </div>
                    {waterfallCalls && waterfallCalls.length > 0 ? (
                      <WaterfallChart calls={waterfallCalls} />
                    ) : (
                      <div className="rounded-xl border border-zinc-200 bg-zinc-50 p-4 text-center dark:border-zinc-700 dark:bg-zinc-800">
                        <span className="text-xs text-zinc-400">No trace data available</span>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Right: Filters Panel */}
        <div className="glass h-fit overflow-hidden rounded-2xl border border-amber-200/40 bg-white/70 p-5 shadow-lg backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-900/70">
          <h2 className="mb-4 font-heading text-sm font-bold text-zinc-800 dark:text-zinc-100">
            🔧 Filters
          </h2>

          <div className="space-y-5">
            {/* Strategy Filter */}
            <div>
              <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                Strategy
              </div>
              <div className="space-y-1.5">
                {['POC', 'SingleModel', 'MultiModelConsensus', 'HybridRAG'].map(strategy => (
                  <label
                    key={strategy}
                    className="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 transition-colors hover:bg-zinc-100 dark:hover:bg-zinc-800"
                  >
                    <input
                      type="checkbox"
                      checked={filters.strategies.includes(strategy)}
                      onChange={() => handleStrategyToggle(strategy)}
                      className="h-3.5 w-3.5 rounded border-zinc-300 text-amber-600 focus:ring-amber-500 dark:border-zinc-600"
                    />
                    <span className="text-xs text-zinc-700 dark:text-zinc-300">{strategy}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Status Filter */}
            <div>
              <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                Status
              </div>
              <div className="space-y-1.5">
                {[
                  { value: 'all', label: 'All' },
                  { value: 'ok', label: 'Success' },
                  { value: 'error', label: 'Error' },
                  { value: 'cache', label: 'Cached' },
                ].map(option => (
                  <label
                    key={option.value}
                    className="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 transition-colors hover:bg-zinc-100 dark:hover:bg-zinc-800"
                  >
                    <input
                      type="radio"
                      name="status"
                      value={option.value}
                      checked={filters.status === option.value}
                      onChange={e =>
                        setFilters(prev => ({
                          ...prev,
                          status: e.target.value as FilterState['status'],
                        }))
                      }
                      className="h-3.5 w-3.5 border-zinc-300 text-amber-600 focus:ring-amber-500 dark:border-zinc-600"
                    />
                    <span className="text-xs text-zinc-700 dark:text-zinc-300">{option.label}</span>
                  </label>
                ))}
              </div>
            </div>

            {/* Agent Filter */}
            <div>
              <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                Agent
              </div>
              <select
                value={filters.agent}
                onChange={e => setFilters(prev => ({ ...prev, agent: e.target.value }))}
                className="w-full rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2 text-xs text-zinc-700 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300"
              >
                <option value="all">All Agents</option>
                <option value="GameAdvisor">GameAdvisor</option>
                <option value="RulesExpert">RulesExpert</option>
                <option value="Recommender">Recommender</option>
              </select>
            </div>

            {/* Confidence Slider */}
            <div>
              <div className="mb-2 flex items-center justify-between text-xs">
                <span className="font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                  Confidence
                </span>
                <span className="font-mono font-semibold text-zinc-700 dark:text-zinc-300">
                  {filters.minConfidence.toFixed(2)}
                </span>
              </div>
              <input
                type="range"
                min="0"
                max="1"
                step="0.01"
                value={filters.minConfidence}
                onChange={e =>
                  setFilters(prev => ({ ...prev, minConfidence: Number(e.target.value) }))
                }
                className="w-full"
              />
            </div>

            {/* Latency Slider */}
            <div>
              <div className="mb-2 flex items-center justify-between text-xs">
                <span className="font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                  Max Latency (ms)
                </span>
                <span className="font-mono font-semibold text-zinc-700 dark:text-zinc-300">
                  {filters.maxLatencyMs}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <span className="text-xs font-mono text-zinc-400">0</span>
                <input
                  type="range"
                  min="0"
                  max="5000"
                  step="100"
                  value={filters.maxLatencyMs}
                  onChange={e =>
                    setFilters(prev => ({ ...prev, maxLatencyMs: Number(e.target.value) }))
                  }
                  className="flex-1"
                />
                <span className="text-xs font-mono text-zinc-400">5000</span>
              </div>
            </div>

            {/* Date Range */}
            <div>
              <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400">
                Date Range
              </div>
              <div className="space-y-2">
                <input
                  type="date"
                  value={filters.dateFrom}
                  onChange={e => setFilters(prev => ({ ...prev, dateFrom: e.target.value }))}
                  className="w-full rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2 text-xs font-mono text-zinc-700 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300"
                />
                <input
                  type="date"
                  value={filters.dateTo}
                  onChange={e => setFilters(prev => ({ ...prev, dateTo: e.target.value }))}
                  className="w-full rounded-lg border border-zinc-200 bg-zinc-50 px-3 py-2 text-xs font-mono text-zinc-700 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-300"
                />
              </div>
            </div>

            {/* Apply Button */}
            <button
              onClick={handleApplyFilters}
              className="w-full rounded-lg bg-amber-500 py-2.5 text-xs font-bold uppercase tracking-wide text-white transition-colors hover:bg-amber-600"
            >
              Apply Filters
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
