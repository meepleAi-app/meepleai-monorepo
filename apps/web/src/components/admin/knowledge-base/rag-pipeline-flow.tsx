'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import {
  AlertCircleIcon,
  ArrowRightIcon,
  CheckCircleIcon,
  ChevronDownIcon,
  ChevronUpIcon,
  ClockIcon,
  DatabaseIcon,
  FileTextIcon,
  RefreshCwIcon,
  XCircleIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { createAdminClient, type PipelineStageStatus } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function statusColor(status: PipelineStageStatus) {
  switch (status) {
    case 'healthy':
      return 'bg-green-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]';
    case 'warning':
      return 'bg-amber-500 shadow-[0_0_8px_rgba(245,158,11,0.5)]';
    case 'error':
      return 'bg-red-500 shadow-[0_0_8px_rgba(239,68,68,0.5)]';
    default:
      return 'bg-gray-400';
  }
}

function statusBorder(status: PipelineStageStatus) {
  switch (status) {
    case 'healthy':
      return 'border-green-400/40 dark:border-green-600/40';
    case 'warning':
      return 'border-amber-400/40 dark:border-amber-600/40';
    case 'error':
      return 'border-red-400/40 dark:border-red-600/40';
    default:
      return 'border-gray-400/40';
  }
}

function formatDuration(ms: number | null): string {
  if (ms === null) return '\u2014';
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function formatRelativeTime(dateStr: string | null): string {
  if (!dateStr) return '\u2014';
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 1) return 'just now';
  if (diffMin < 60) return `${diffMin}m ago`;
  const diffHr = Math.floor(diffMin / 60);
  if (diffHr < 24) return `${diffHr}h ago`;
  return `${Math.floor(diffHr / 24)}d ago`;
}

export function RAGPipelineFlow() {
  const [expandedStage, setExpandedStage] = useState<string | null>(null);

  const {
    data: pipeline,
    isLoading,
    refetch,
    isRefetching,
  } = useQuery({
    queryKey: ['admin', 'pipeline', 'health'],
    queryFn: () => adminClient.getPipelineHealth(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  });

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="h-48 bg-white/40 dark:bg-zinc-800/40 rounded-xl animate-pulse" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-20 bg-white/40 dark:bg-zinc-800/40 rounded-lg animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  const stages = pipeline?.stages ?? [];
  const summary = pipeline?.summary ?? { healthyCount: 0, warningCount: 0, errorCount: 0 };
  const recentActivity = pipeline?.recentActivity ?? [];
  const distribution = pipeline?.distribution;

  return (
    <div className="space-y-6">
      {/* Pipeline Flow */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-8 border border-amber-200/50 dark:border-zinc-700/50">
        <div className="flex items-center justify-between mb-8">
          <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100">
            RAG Pipeline Flow
          </h2>
          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            disabled={isRefetching}
            className="gap-2"
          >
            <RefreshCwIcon className={`h-4 w-4 ${isRefetching ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>

        {/* Pipeline Visualization */}
        <div className="flex items-center justify-between gap-4 mb-6 overflow-x-auto pb-4">
          {stages.map((stage, index) => {
            const isExpanded = expandedStage === stage.name;
            return (
              <div key={stage.name} className="flex items-center gap-4">
                <div className="flex flex-col items-center">
                  <button
                    type="button"
                    onClick={() => setExpandedStage(isExpanded ? null : stage.name)}
                    className={`bg-slate-50 dark:bg-zinc-900 rounded-lg p-4 border-2 ${statusBorder(stage.status)} min-w-[110px] transition-all hover:shadow-md hover:-translate-y-0.5 ${isExpanded ? 'ring-2 ring-blue-400/60' : ''}`}
                  >
                    <div className="text-center">
                      <div className="font-semibold text-slate-900 dark:text-zinc-100 text-sm mb-2">
                        {stage.name}
                      </div>
                      <div className={`w-3 h-3 rounded-full mx-auto ${statusColor(stage.status)} animate-pulse`} />
                      {stage.metrics && Object.keys(stage.metrics).length > 0 && (
                        <div className="mt-2 text-[10px] text-gray-500 dark:text-zinc-500 leading-tight">
                          {renderStageMetric(stage.name, stage.metrics)}
                        </div>
                      )}
                      <div className="mt-2 flex justify-center">
                        {isExpanded
                          ? <ChevronUpIcon className="h-3 w-3 text-muted-foreground" />
                          : <ChevronDownIcon className="h-3 w-3 text-muted-foreground" />}
                      </div>
                    </div>
                  </button>
                </div>
                {index < stages.length - 1 && (
                  <ArrowRightIcon className="w-6 h-6 text-amber-500 dark:text-amber-400 flex-shrink-0" />
                )}
              </div>
            );
          })}
        </div>

        {/* Stage Drill-Down Panel */}
        {expandedStage && (() => {
          const stage = stages.find((s) => s.name === expandedStage);
          if (!stage) return null;
          const metricsEntries = stage.metrics ? Object.entries(stage.metrics) : [];
          return (
            <div className="mb-6 bg-slate-50/80 dark:bg-zinc-900/60 rounded-xl border border-slate-200/50 dark:border-zinc-700/50 p-5 space-y-3">
              <div className="flex items-center justify-between">
                <h3 className="font-semibold text-slate-900 dark:text-zinc-100 flex items-center gap-2">
                  <span className={`w-2.5 h-2.5 rounded-full ${statusColor(stage.status)}`} />
                  {stage.name} — Stage Details
                </h3>
                <button
                  type="button"
                  onClick={() => setExpandedStage(null)}
                  className="text-xs text-muted-foreground hover:text-foreground transition-colors"
                >
                  Close
                </button>
              </div>
              {metricsEntries.length > 0 ? (
                <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
                  {metricsEntries.map(([key, value]) => (
                    <div
                      key={key}
                      className="bg-white/70 dark:bg-zinc-800/70 rounded-lg px-3 py-2 border border-slate-200/40 dark:border-zinc-700/40"
                    >
                      <div className="text-[10px] uppercase tracking-wider text-muted-foreground mb-0.5">
                        {key.replace(/([A-Z])/g, ' $1').trim()}
                      </div>
                      <div className="text-sm font-semibold text-foreground truncate">
                        {value !== null && value !== undefined ? String(value) : '—'}
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground italic">No detailed metrics available for this stage.</p>
              )}
            </div>
          );
        })()}

        {/* Health Summary */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="bg-green-50 dark:bg-green-900/20 rounded-lg p-4 border border-green-200 dark:border-green-800">
            <div className="text-sm font-medium text-green-900 dark:text-green-300 flex items-center gap-1.5">
              <CheckCircleIcon className="h-4 w-4" />
              Healthy Stages
            </div>
            <div className="text-2xl font-bold text-green-700 dark:text-green-400">
              {summary.healthyCount}/{stages.length}
            </div>
          </div>
          <div className="bg-amber-50 dark:bg-amber-900/20 rounded-lg p-4 border border-amber-200 dark:border-amber-800">
            <div className="text-sm font-medium text-amber-900 dark:text-amber-300 flex items-center gap-1.5">
              <AlertCircleIcon className="h-4 w-4" />
              Warnings
            </div>
            <div className="text-2xl font-bold text-amber-700 dark:text-amber-400">
              {summary.warningCount}
            </div>
          </div>
          <div className="bg-red-50 dark:bg-red-900/20 rounded-lg p-4 border border-red-200 dark:border-red-800">
            <div className="text-sm font-medium text-red-900 dark:text-red-300 flex items-center gap-1.5">
              <XCircleIcon className="h-4 w-4" />
              Errors
            </div>
            <div className="text-2xl font-bold text-red-700 dark:text-red-400">
              {summary.errorCount}
            </div>
          </div>
        </div>
      </div>

      {/* Distribution Stats */}
      {distribution && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <StatCard
            icon={FileTextIcon}
            label="Documents"
            value={distribution.totalDocuments.toLocaleString()}
            color="text-blue-500"
          />
          <StatCard
            icon={DatabaseIcon}
            label="Chunks"
            value={distribution.totalChunks.toLocaleString()}
            color="text-violet-500"
          />
          <StatCard
            icon={DatabaseIcon}
            label="Vectors"
            value={distribution.vectorCount.toLocaleString()}
            color="text-emerald-500"
          />
          <StatCard
            icon={FileTextIcon}
            label="Storage"
            value={distribution.storageSizeFormatted}
            color="text-amber-500"
          />
        </div>
      )}

      {/* Recent Activity */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-slate-200/50 dark:border-zinc-700/50">
        <h2 className="font-quicksand text-xl font-bold text-slate-900 dark:text-zinc-100 mb-4">
          Recent Activity
        </h2>
        {recentActivity.length === 0 ? (
          <p className="text-sm text-slate-600 dark:text-zinc-400">
            No recent processing activity
          </p>
        ) : (
          <div className="space-y-2">
            {recentActivity.map((item) => (
              <div
                key={item.jobId}
                className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-slate-50 dark:hover:bg-zinc-800 transition-colors"
              >
                <div className="flex items-center gap-3 min-w-0">
                  <JobStatusIcon status={item.status} />
                  <span className="text-sm font-medium text-slate-900 dark:text-zinc-100 truncate">
                    {item.fileName}
                  </span>
                </div>
                <div className="flex items-center gap-4 text-xs text-slate-500 dark:text-zinc-500 flex-shrink-0">
                  {item.durationMs !== null && (
                    <span className="flex items-center gap-1">
                      <ClockIcon className="h-3 w-3" />
                      {formatDuration(item.durationMs)}
                    </span>
                  )}
                  <span>{formatRelativeTime(item.completedAt)}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function StatCard({ icon: Icon, label, value, color }: {
  icon: React.ElementType;
  label: string;
  value: string;
  color: string;
}) {
  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-white/40 dark:border-zinc-700/40">
      <div className="flex items-center gap-2 text-sm text-gray-600 dark:text-zinc-400 mb-1">
        <Icon className={`h-4 w-4 ${color}`} />
        {label}
      </div>
      <div className="text-2xl font-bold text-gray-900 dark:text-zinc-100">{value}</div>
    </div>
  );
}

function JobStatusIcon({ status }: { status: string }) {
  if (status.toLowerCase() === 'completed') {
    return <CheckCircleIcon className="h-4 w-4 text-green-500 flex-shrink-0" />;
  }
  if (status.toLowerCase() === 'failed') {
    return <XCircleIcon className="h-4 w-4 text-red-500 flex-shrink-0" />;
  }
  return <AlertCircleIcon className="h-4 w-4 text-amber-500 flex-shrink-0" />;
}

function renderStageMetric(stageName: string, metrics: Record<string, unknown>): string | null {
  switch (stageName) {
    case 'Ingest': {
      const active = metrics.activeJobs as number | undefined;
      const queued = metrics.queuedJobs as number | undefined;
      if (active || queued) return `${active ?? 0} active, ${queued ?? 0} queued`;
      return null;
    }
    case 'Extract':
    case 'Chunk': {
      const avg = metrics.avgDurationMs as string | undefined;
      if (avg) return `avg ${avg}ms`;
      return null;
    }
    case 'Embed': {
      const reqs = metrics.requestsTotal as number | undefined;
      if (reqs) return `${reqs} reqs`;
      return null;
    }
    case 'Index': {
      const vecs = metrics.vectorCount as number | undefined;
      if (vecs) return `${vecs.toLocaleString()} vecs`;
      return null;
    }
    default:
      return null;
  }
}
