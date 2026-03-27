'use client';

/**
 * RAG Inspector - Unified Debug Console + Pipeline Explorer
 *
 * Three tabs:
 * 1. Esecuzioni — Live execution monitoring with filters, auto-refresh, stats
 * 2. Pipeline — 6-step pipeline diagram with execution selector and timeline
 * 3. Waterfall — Waterfall chart for selected execution trace
 *
 * Shared state: selectedExecutionId flows across all tabs.
 * Supports deep-linking via ?tab=pipeline|waterfall|esecuzioni
 */

import { useState, useEffect, useCallback } from 'react';

import { useSearchParams } from 'next/navigation';

import {
  StrategyBadge,
  ConfidenceBadge,
  WaterfallChart,
  PipelineDiagram,
  TimelineStep,
  type WaterfallCall,
  type WaterfallCallType,
  type PipelineStep,
  type TimelineStepDetail,
} from '@/components/admin/rag';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
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
  icon?: string;
  latencyMs: number;
  status: 'ok' | 'error' | 'cache';
  type?: WaterfallCallType;
  startOffsetMs?: number;
  details?: Record<string, unknown>;
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

type TabValue = 'esecuzioni' | 'pipeline' | 'waterfall';

// =============================================================================
// Helper Functions
// =============================================================================

function parseWaterfallCalls(traceJson: string | null): WaterfallCall[] | null {
  if (!traceJson) return null;
  try {
    const parsed = JSON.parse(traceJson);
    if (parsed.steps && Array.isArray(parsed.steps)) {
      let currentOffset = 0;
      return parsed.steps.map((step: ExecutionTraceStep) => {
        const call: WaterfallCall = {
          label: step.name,
          durationMs: step.latencyMs,
          type: step.type || 'llm',
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

function parsePipelineSteps(traceJson: string | null): PipelineStep[] {
  if (!traceJson) return [];
  try {
    const parsed = JSON.parse(traceJson);
    if (parsed.steps && Array.isArray(parsed.steps)) {
      return parsed.steps.map((step: ExecutionTraceStep) => ({
        name: step.name,
        icon: step.icon || '',
        latencyMs: step.latencyMs,
        status: step.status,
      }));
    }
    return [];
  } catch {
    return [];
  }
}

function parseTimelineDetails(details: Record<string, unknown>): TimelineStepDetail[] {
  const result: TimelineStepDetail[] = [];
  Object.entries(details).forEach(([key, value]) => {
    if (value === null || value === undefined) return;
    const isMono =
      /^(model|query|tokens|score|count|type|collection|dimension|cache|provider|temperature)$/i.test(
        key
      );
    const isWide = /^(query|answer|response|preview)$/i.test(key);
    let badge: TimelineStepDetail['badge'] = undefined;
    if (key === 'cache' && value === 'HIT') badge = 'green';
    if (key === 'cache' && value === 'MISS') badge = 'red';
    if (key === 'quality' || key === 'intent') badge = 'primary';
    result.push({
      label: key.replace(/([A-Z])/g, ' $1').replace(/^./, str => str.toUpperCase()),
      value: String(value),
      mono: isMono,
      badge,
      wide: isWide,
    });
  });
  return result;
}

function formatTimestamp(timestamp: string): string {
  const date = new Date(timestamp);
  return date.toLocaleTimeString('en-US', {
    hour12: false,
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

function getStatusIcon(status: string): string {
  switch (status) {
    case 'ok':
      return '\u2713';
    case 'error':
      return '\u2717';
    case 'cache':
      return '\u26A1';
    default:
      return '\u2022';
  }
}

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

function getLatencyClass(ms: number): string {
  if (ms < 100) return 'text-green-600 dark:text-green-400 bg-green-50 dark:bg-green-950/30';
  if (ms <= 500) return 'text-amber-600 dark:text-amber-400 bg-amber-50 dark:bg-amber-950/30';
  return 'text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-950/30';
}

function getLatencyColor(ms: number): 'green' | 'amber' | 'red' {
  if (ms < 100) return 'green';
  if (ms <= 500) return 'amber';
  return 'red';
}

function getStepBarColor(percentage: number): string {
  if (percentage < 10) return 'hsl(142, 60%, 42%)';
  if (percentage < 30) return 'hsl(142, 50%, 50%)';
  if (percentage < 50) return 'hsl(38, 92%, 50%)';
  return 'hsl(0, 72%, 51%)';
}

function getTodayDate(): string {
  return new Date().toISOString().split('T')[0];
}

// =============================================================================
// Main Component
// =============================================================================

export default function InspectorPage() {
  const searchParams = useSearchParams();
  const tabParam = searchParams.get('tab') as TabValue | null;
  const defaultTab: TabValue =
    tabParam === 'pipeline' || tabParam === 'waterfall' ? tabParam : 'esecuzioni';

  // Shared state
  const [selectedExecutionId, setSelectedExecutionId] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<TabValue>(defaultTab);

  // Execution list state
  const [executions, setExecutions] = useState<RagExecutionListItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  const [skip, setSkip] = useState(0);
  const take = 20;

  // Execution detail state
  const [executionDetail, setExecutionDetail] = useState<RagExecutionDetail | null>(null);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);

  // Auto-refresh state
  const [autoRefresh, setAutoRefresh] = useState(true);
  const [refreshInterval, setRefreshInterval] = useState<RefreshInterval>(10000);

  // Filter state
  const [filters, setFilters] = useState<FilterState>({
    strategies: ['POC', 'SingleModel', 'MultiModelConsensus', 'HybridRAG'],
    status: 'all',
    agent: 'all',
    minConfidence: 0,
    maxLatencyMs: 5000,
    dateFrom: getTodayDate(),
    dateTo: getTodayDate(),
  });

  // Pipeline state
  const [activeNodeIndex, setActiveNodeIndex] = useState<number | null>(0);
  const [openStepIndexes, setOpenStepIndexes] = useState<Set<number>>(new Set([0]));

  // Stats state
  const [stats, setStats] = useState({
    totalExecutions: 0,
    avgLatencyMs: 0,
    errorRate: 0,
    cacheHitRate: 0,
    totalCost: 0,
  });

  // =========================================================================
  // Data Fetching
  // =========================================================================

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

        if (filters.status !== 'all') {
          params.status = filters.status;
        }
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

        // Auto-select first execution if none selected
        if (!selectedExecutionId && result.items.length > 0) {
          setSelectedExecutionId(result.items[0].id);
        }
      } catch (error) {
        logger.error('Failed to fetch executions:', error);
      } finally {
        setIsLoading(false);
        setIsLoadingMore(false);
      }
    },
    [skip, filters, executions.length, selectedExecutionId]
  );

  const fetchExecutionDetail = useCallback(async (id: string) => {
    setIsLoadingDetail(true);
    try {
      const detail = await adminClient.getRagExecutionById(id);
      setExecutionDetail(detail);
    } catch (error) {
      logger.error('Failed to fetch execution detail:', error);
    } finally {
      setIsLoadingDetail(false);
    }
  }, []);

  const fetchStats = useCallback(async () => {
    try {
      const result = await adminClient.getRagExecutionStats({
        dateFrom: filters.dateFrom,
        dateTo: filters.dateTo,
      });
      setStats(result);
    } catch (error) {
      logger.error('Failed to fetch stats:', error);
    }
  }, [filters.dateFrom, filters.dateTo]);

  // Initial load
  useEffect(() => {
    fetchExecutions(true);
    fetchStats();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Auto-refresh polling
  useEffect(() => {
    if (!autoRefresh) return;
    const interval = setInterval(() => {
      fetchExecutions(true);
    }, refreshInterval);
    return () => clearInterval(interval);
  }, [autoRefresh, refreshInterval, fetchExecutions]);

  // Fetch detail when selection changes
  useEffect(() => {
    if (selectedExecutionId) {
      fetchExecutionDetail(selectedExecutionId);
    } else {
      setExecutionDetail(null);
    }
  }, [selectedExecutionId, fetchExecutionDetail]);

  // =========================================================================
  // Handlers
  // =========================================================================

  const handleRowClick = (execution: RagExecutionListItem) => {
    setSelectedExecutionId(execution.id);
  };

  const handleLoadMore = () => {
    fetchExecutions(false);
  };

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
    fetchStats();
  };

  const handleNodeClick = useCallback((index: number) => {
    setActiveNodeIndex(index);
    setOpenStepIndexes(prev => new Set(prev).add(index));
    setTimeout(() => {
      const element = document.querySelector(`[data-step-index="${index}"]`);
      element?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 100);
  }, []);

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

  // =========================================================================
  // Derived Data
  // =========================================================================

  const waterfallCalls = executionDetail
    ? parseWaterfallCalls(executionDetail.executionTrace)
    : null;

  const pipelineSteps = executionDetail ? parsePipelineSteps(executionDetail.executionTrace) : [];

  const totalLatency = executionDetail?.totalLatencyMs || 0;

  const selectedExecution = executions.find(e => e.id === selectedExecutionId);

  // Parse trace steps for timeline
  const traceSteps: ExecutionTraceStep[] = (() => {
    if (!executionDetail?.executionTrace) return [];
    try {
      const parsed = JSON.parse(executionDetail.executionTrace);
      return parsed.steps && Array.isArray(parsed.steps) ? parsed.steps : [];
    } catch {
      return [];
    }
  })();

  // =========================================================================
  // Render
  // =========================================================================

  return (
    <div className="min-h-screen space-y-6 p-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          RAG Inspector
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Monitora e analizza le esecuzioni RAG in tempo reale
        </p>
      </div>

      {/* Stats Bar */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
        {[
          { label: 'Esecuzioni', value: stats.totalExecutions.toLocaleString() },
          { label: 'Latenza Media', value: `${Math.round(stats.avgLatencyMs)}ms` },
          { label: 'Errori', value: `${(stats.errorRate * 100).toFixed(1)}%` },
          { label: 'Cache Hit', value: `${(stats.cacheHitRate * 100).toFixed(1)}%` },
          { label: 'Costo', value: `$${stats.totalCost.toFixed(2)}` },
        ].map(stat => (
          <div
            key={stat.label}
            className="rounded-xl border border-zinc-200 bg-white/70 px-4 py-3 dark:border-zinc-700 dark:bg-zinc-900/70"
          >
            <div className="text-xs font-medium text-muted-foreground">{stat.label}</div>
            <div className="mt-1 font-mono text-lg font-bold text-foreground">{stat.value}</div>
          </div>
        ))}
      </div>

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={v => setActiveTab(v as TabValue)} className="w-full">
        <TabsList>
          <TabsTrigger value="esecuzioni">Esecuzioni</TabsTrigger>
          <TabsTrigger value="pipeline">Pipeline</TabsTrigger>
          <TabsTrigger value="waterfall">Waterfall</TabsTrigger>
        </TabsList>

        {/* ================================================================= */}
        {/* Tab: Esecuzioni                                                   */}
        {/* ================================================================= */}
        <TabsContent value="esecuzioni">
          <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_320px]">
            {/* Execution Table */}
            <div className="glass min-h-[500px] overflow-hidden rounded-2xl border border-amber-200/40 bg-white/70 p-5 shadow-lg backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-900/70">
              {/* Live Header */}
              <div className="mb-4 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <h2 className="font-heading text-base font-bold text-zinc-800 dark:text-zinc-100">
                    Live Executions
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
                      {['Time', 'Status', 'Query', 'Strategy', 'Latency', 'Conf.', 'Agent'].map(
                        header => (
                          <th
                            key={header}
                            className="pb-3 pr-4 text-left text-xs font-semibold uppercase tracking-wide text-zinc-500 dark:text-zinc-400 last:pr-0"
                          >
                            {header}
                          </th>
                        )
                      )}
                    </tr>
                  </thead>
                  <tbody>
                    {isLoading ? (
                      Array.from({ length: 10 }).map((_, i) => (
                        <tr key={i} className="border-b border-zinc-100 dark:border-zinc-800">
                          {[16, 4, 48, 24, 16, 12, 20].map((w, j) => (
                            <td key={j} className="py-3 pr-4">
                              <Skeleton className={`h-4 w-${w}`} />
                            </td>
                          ))}
                        </tr>
                      ))
                    ) : executions.length === 0 ? (
                      <tr>
                        <td colSpan={7} className="py-12 text-center">
                          <div className="flex flex-col items-center gap-3">
                            <p className="text-sm font-medium text-zinc-400">No executions found</p>
                            <p className="text-xs text-zinc-400">
                              Try adjusting your filters or wait for new executions
                            </p>
                          </div>
                        </td>
                      </tr>
                    ) : (
                      executions.map(execution => (
                        <tr
                          key={execution.id}
                          onClick={() => handleRowClick(execution)}
                          className={cn(
                            'cursor-pointer border-b border-zinc-100 transition-colors hover:bg-amber-50/50 dark:border-zinc-800 dark:hover:bg-zinc-800/30',
                            selectedExecutionId === execution.id &&
                              'bg-amber-50 dark:bg-amber-950/20'
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

              {/* Load More */}
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
            </div>

            {/* Filters Panel */}
            <div className="glass h-fit overflow-hidden rounded-2xl border border-amber-200/40 bg-white/70 p-5 shadow-lg backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-900/70">
              <h2 className="mb-4 font-heading text-sm font-bold text-zinc-800 dark:text-zinc-100">
                Filters
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
                        <span className="text-xs text-zinc-700 dark:text-zinc-300">
                          {option.label}
                        </span>
                      </label>
                    ))}
                  </div>
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
                  <input
                    type="range"
                    min="0"
                    max="5000"
                    step="100"
                    value={filters.maxLatencyMs}
                    onChange={e =>
                      setFilters(prev => ({ ...prev, maxLatencyMs: Number(e.target.value) }))
                    }
                    className="w-full"
                  />
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
        </TabsContent>

        {/* ================================================================= */}
        {/* Tab: Pipeline                                                     */}
        {/* ================================================================= */}
        <TabsContent value="pipeline">
          <div className="space-y-5">
            {/* Execution Selector */}
            <div className="glass-card rounded-2xl border border-black/10 bg-white/90 p-5 backdrop-blur-md dark:border-white/10 dark:bg-zinc-800/90">
              <div className="flex flex-wrap items-center gap-4">
                <select
                  value={selectedExecutionId || ''}
                  onChange={e => setSelectedExecutionId(e.target.value)}
                  className={cn(
                    'flex-1 min-w-[280px] rounded-xl border border-black/20 px-4 py-2.5',
                    'bg-white/70 font-nunito text-sm text-zinc-800 backdrop-blur-sm',
                    'cursor-pointer appearance-none transition-colors',
                    'hover:bg-white/90 focus:outline-none focus:ring-2 focus:ring-amber-500',
                    'dark:border-white/20 dark:bg-zinc-700/70 dark:text-zinc-100'
                  )}
                >
                  {executions.map(exec => (
                    <option key={exec.id} value={exec.id}>
                      {exec.status === 'success' ? '\u2713' : '\u2717'} &quot;{exec.query}&quot; —{' '}
                      {exec.totalLatencyMs}ms
                      {exec.cacheHit ? ' (cache)' : ''}
                    </option>
                  ))}
                </select>

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
                      Total:{' '}
                      <strong
                        className={cn(
                          'font-mono',
                          getLatencyColor(selectedExecution.totalLatencyMs) === 'green' &&
                            'text-green-600 dark:text-green-400',
                          getLatencyColor(selectedExecution.totalLatencyMs) === 'amber' &&
                            'text-amber-600 dark:text-amber-400',
                          getLatencyColor(selectedExecution.totalLatencyMs) === 'red' &&
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

            {/* Pipeline Diagram */}
            <div className="glass-card rounded-2xl border border-black/10 bg-white/90 p-5 backdrop-blur-md dark:border-white/10 dark:bg-zinc-800/90">
              {isLoadingDetail ? (
                <div className="space-y-4">
                  <Skeleton className="h-8 w-48" />
                  <Skeleton className="h-48 w-full" />
                </div>
              ) : pipelineSteps.length > 0 ? (
                <>
                  <div className="mb-4 flex items-center gap-2">
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

            {/* Timeline */}
            <div className="glass-card rounded-2xl border border-black/10 bg-white/90 p-5 backdrop-blur-md dark:border-white/10 dark:bg-zinc-800/90">
              <div className="mb-4 flex items-center gap-2">
                <h2 className="font-quicksand text-lg font-bold text-zinc-800 dark:text-zinc-100">
                  Step Details
                </h2>
              </div>

              {isLoadingDetail ? (
                <div className="space-y-2">
                  {Array.from({ length: 6 }).map((_, i) => (
                    <Skeleton key={i} className="h-16 w-full" />
                  ))}
                </div>
              ) : traceSteps.length > 0 ? (
                <div className="space-y-2">
                  {traceSteps.map((step, index) => {
                    const percentage = totalLatency > 0 ? (step.latencyMs / totalLatency) * 100 : 0;
                    const barColor = getStepBarColor(percentage);
                    const latencyClass = getLatencyColor(step.latencyMs);
                    const details = step.details ? parseTimelineDetails(step.details) : [];

                    return (
                      <div key={index} data-step-index={index}>
                        <TimelineStep
                          name={step.name}
                          icon={step.icon || ''}
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
        </TabsContent>

        {/* ================================================================= */}
        {/* Tab: Waterfall                                                    */}
        {/* ================================================================= */}
        <TabsContent value="waterfall">
          <div className="glass-card rounded-2xl border border-black/10 bg-white/90 p-5 backdrop-blur-md dark:border-white/10 dark:bg-zinc-800/90">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="font-quicksand text-lg font-bold text-zinc-800 dark:text-zinc-100">
                Call Trace (Waterfall)
              </h2>
              {selectedExecution && (
                <span className="text-xs text-zinc-500 dark:text-zinc-400">
                  Execution: &quot;{selectedExecution.query}&quot; —{' '}
                  {selectedExecution.totalLatencyMs}ms
                </span>
              )}
            </div>

            {isLoadingDetail ? (
              <div className="space-y-4">
                <Skeleton className="h-48 w-full" />
              </div>
            ) : waterfallCalls && waterfallCalls.length > 0 ? (
              <WaterfallChart calls={waterfallCalls} />
            ) : (
              <div className="rounded-xl border border-zinc-200 bg-zinc-50 p-8 text-center dark:border-zinc-700 dark:bg-zinc-800">
                <p className="text-sm text-zinc-400">
                  {selectedExecutionId
                    ? 'No trace data available for this execution'
                    : 'Select an execution from the Esecuzioni tab to view its waterfall chart'}
                </p>
              </div>
            )}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
