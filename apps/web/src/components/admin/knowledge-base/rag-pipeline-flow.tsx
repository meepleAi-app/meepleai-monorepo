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

function statusDotColor(status: PipelineStageStatus) {
  switch (status) {
    case 'healthy':
      return 'bg-entity-toolkit shadow-[0_0_8px_hsl(var(--entity-toolkit)/0.5)]';
    case 'warning':
      return 'bg-amber-500 shadow-[0_0_8px_rgba(245,158,11,0.5)]';
    case 'error':
      return 'bg-entity-event shadow-[0_0_8px_hsl(var(--entity-event)/0.5)]';
    default:
      return 'bg-muted-foreground';
  }
}

function statusBorderColor(status: PipelineStageStatus) {
  switch (status) {
    case 'healthy':
      return 'border-entity-toolkit/40';
    case 'warning':
      return 'border-amber-500/40';
    case 'error':
      return 'border-entity-event/40';
    default:
      return 'border-border/40';
  }
}

function formatDuration(ms: number | null): string {
  if (ms === null) return '—';
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function formatRelativeTime(dateStr: string | null): string {
  if (!dateStr) return '—';
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
        <div className="h-48 bg-card/40 rounded-[10px] animate-pulse" />
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-20 bg-card/40 rounded-[10px] animate-pulse" />
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
      <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
        {/* Panel header */}
        <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
          <h2 className="font-quicksand text-[13px] font-extrabold text-foreground flex-1">
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

        {/* Panel body */}
        <div className="p-4 space-y-4">
          {/* Pipeline Visualization */}
          <div className="flex items-center justify-between gap-4 overflow-x-auto pb-2">
            {stages.map((stage, index) => {
              const isExpanded = expandedStage === stage.name;
              return (
                <div key={stage.name} className="flex items-center gap-4">
                  <div className="flex flex-col items-center">
                    <button
                      type="button"
                      onClick={() => setExpandedStage(isExpanded ? null : stage.name)}
                      className={`bg-muted rounded-[10px] p-4 border-2 ${statusBorderColor(stage.status)} min-w-[110px] transition-all hover:shadow-md hover:-translate-y-0.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 ${isExpanded ? 'ring-2 ring-ring/50' : ''}`}
                    >
                      <div className="text-center">
                        <div className="font-quicksand font-bold text-foreground text-sm mb-2">
                          {stage.name}
                        </div>
                        <div
                          className={`w-3 h-3 rounded-full mx-auto ${statusDotColor(stage.status)} animate-pulse`}
                        />
                        {stage.metrics && Object.keys(stage.metrics).length > 0 && (
                          <div className="mt-2 text-[10px] text-muted-foreground leading-tight">
                            {renderStageMetric(stage.name, stage.metrics)}
                          </div>
                        )}
                        <div className="mt-2 flex justify-center">
                          {isExpanded ? (
                            <ChevronUpIcon className="h-3 w-3 text-muted-foreground" />
                          ) : (
                            <ChevronDownIcon className="h-3 w-3 text-muted-foreground" />
                          )}
                        </div>
                      </div>
                    </button>
                  </div>
                  {index < stages.length - 1 && (
                    <ArrowRightIcon className="w-6 h-6 text-entity-agent flex-shrink-0" />
                  )}
                </div>
              );
            })}
          </div>

          {/* Stage Drill-Down Panel */}
          {expandedStage &&
            (() => {
              const stage = stages.find(s => s.name === expandedStage);
              if (!stage) return null;
              const metricsEntries = stage.metrics ? Object.entries(stage.metrics) : [];
              return (
                <div className="bg-background rounded-[10px] border border-border/60 border-l-4 border-l-entity-kb p-5 space-y-3">
                  <div className="flex items-center justify-between">
                    <h3 className="font-quicksand font-bold text-foreground flex items-center gap-2">
                      <span
                        className={`w-2.5 h-2.5 rounded-full ${statusDotColor(stage.status)}`}
                      />
                      {stage.name} — Stage Details
                    </h3>
                    <button
                      type="button"
                      onClick={() => setExpandedStage(null)}
                      className="text-xs text-muted-foreground hover:text-foreground transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 rounded"
                    >
                      Close
                    </button>
                  </div>
                  {metricsEntries.length > 0 ? (
                    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
                      {metricsEntries.map(([key, value]) => (
                        <div
                          key={key}
                          className="bg-card rounded-[10px] px-3 py-2 border border-border/60"
                        >
                          <div className="text-[10px] uppercase tracking-wider text-muted-foreground mb-0.5 font-mono font-bold">
                            {key.replace(/([A-Z])/g, ' $1').trim()}
                          </div>
                          <div className="text-sm font-semibold text-foreground truncate font-mono">
                            {value !== null && value !== undefined ? String(value) : '—'}
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="text-sm text-muted-foreground italic">
                      No detailed metrics available for this stage.
                    </p>
                  )}
                </div>
              );
            })()}

          {/* Health Summary */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="rounded-[10px] p-4 border bg-entity-toolkit/10 border-entity-toolkit/30">
              <div className="text-sm font-medium text-entity-toolkit flex items-center gap-1.5">
                <CheckCircleIcon className="h-4 w-4" />
                Healthy Stages
              </div>
              <div className="text-2xl font-bold text-entity-toolkit">
                {summary.healthyCount}/{stages.length}
              </div>
            </div>
            <div className="rounded-[10px] p-4 border bg-amber-500/12 border-amber-500/30">
              <div className="text-sm font-medium text-amber-600 flex items-center gap-1.5">
                <AlertCircleIcon className="h-4 w-4" />
                Warnings
              </div>
              <div className="text-2xl font-bold text-amber-600">{summary.warningCount}</div>
            </div>
            <div className="rounded-[10px] p-4 border bg-entity-event/10 border-entity-event/30">
              <div className="text-sm font-medium text-entity-event flex items-center gap-1.5">
                <XCircleIcon className="h-4 w-4" />
                Errors
              </div>
              <div className="text-2xl font-bold text-entity-event">{summary.errorCount}</div>
            </div>
          </div>
        </div>
      </section>

      {/* Distribution Stats */}
      {distribution && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <StatCard
            icon={FileTextIcon}
            label="Documents"
            value={distribution.totalDocuments.toLocaleString()}
            accent="border-l-entity-kb"
            iconColor="text-entity-kb"
          />
          <StatCard
            icon={DatabaseIcon}
            label="Chunks"
            value={distribution.totalChunks.toLocaleString()}
            accent="border-l-entity-chat"
            iconColor="text-entity-chat"
          />
          <StatCard
            icon={DatabaseIcon}
            label="Vectors"
            value={distribution.vectorCount.toLocaleString()}
            accent="border-l-entity-agent"
            iconColor="text-entity-agent"
          />
          <StatCard
            icon={FileTextIcon}
            label="Storage"
            value={distribution.storageSizeFormatted}
            accent="border-l-entity-toolkit"
            iconColor="text-entity-toolkit"
          />
        </div>
      )}

      {/* Recent Activity */}
      <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
        {/* Panel header */}
        <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
          <h2 className="font-quicksand text-[13px] font-extrabold text-foreground">
            Recent Activity
          </h2>
        </div>

        {/* Panel body */}
        <div className="p-4">
          {recentActivity.length === 0 ? (
            <p className="text-sm text-muted-foreground">No recent processing activity</p>
          ) : (
            <div className="space-y-1">
              {recentActivity.map(item => (
                <div
                  key={item.jobId}
                  className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-muted transition-colors"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <JobStatusIcon status={item.status} />
                    <span className="text-sm font-medium text-foreground truncate">
                      {item.fileName}
                    </span>
                  </div>
                  <div className="flex items-center gap-4 text-xs text-muted-foreground flex-shrink-0">
                    {item.durationMs !== null && (
                      <span className="flex items-center gap-1 font-mono">
                        <ClockIcon className="h-3 w-3" />
                        {formatDuration(item.durationMs)}
                      </span>
                    )}
                    <span className="font-mono">{formatRelativeTime(item.completedAt)}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </section>
    </div>
  );
}

function StatCard({
  icon: Icon,
  label,
  value,
  accent,
  iconColor,
}: {
  icon: React.ElementType;
  label: string;
  value: string;
  accent: string;
  iconColor: string;
}) {
  return (
    <div
      className={`flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 ${accent} min-h-[88px]`}
    >
      <div className="flex items-center gap-2 font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
        <Icon className={`h-4 w-4 ${iconColor}`} />
        {label}
      </div>
      <div className="font-quicksand text-[28px] font-extrabold tabular-nums text-foreground leading-tight">
        {value}
      </div>
    </div>
  );
}

function JobStatusIcon({ status }: { status: string }) {
  if (status.toLowerCase() === 'completed') {
    return <CheckCircleIcon className="h-4 w-4 text-entity-toolkit flex-shrink-0" />;
  }
  if (status.toLowerCase() === 'failed') {
    return <XCircleIcon className="h-4 w-4 text-entity-event flex-shrink-0" />;
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
