'use client';

/**
 * Processing Pipeline Client Component
 *
 * Renders the RAG pipeline status dashboard with:
 * - Stage health grid (healthy/warning/error per pipeline stage)
 * - Aggregate summary counts
 * - Processing step duration metrics
 * - Document distribution statistics
 *
 * @see Issue #4892
 */

import { useEffect, useState } from 'react';

import {
  Activity,
  AlertTriangle,
  CheckCircle,
  Clock,
  Database,
  FileText,
  RefreshCw,
  XCircle,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import type {
  PipelineHealthResponse,
  PipelineStageStatus,
  ProcessingMetricsResponse,
} from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

// ─── Status Helpers ──────────────────────────────────────────────────────────

function statusIcon(status: PipelineStageStatus) {
  if (status === 'healthy') return <CheckCircle className="h-5 w-5 text-green-500" />;
  if (status === 'warning') return <AlertTriangle className="h-5 w-5 text-amber-500" />;
  return <XCircle className="h-5 w-5 text-red-500" />;
}

function statusBadge(status: PipelineStageStatus) {
  const variants: Record<PipelineStageStatus, string> = {
    healthy: 'bg-green-100 text-green-800 border-green-200',
    warning: 'bg-amber-100 text-amber-800 border-amber-200',
    error: 'bg-red-100 text-red-800 border-red-200',
  };
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium border font-nunito ${variants[status]}`}
    >
      {status}
    </span>
  );
}

function formatDuration(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function SummaryCard({
  label,
  count,
  icon: Icon,
  color,
}: {
  label: string;
  count: number;
  icon: React.ElementType;
  color: string;
}) {
  return (
    <Card className={`border-l-4 ${color} shadow-sm`}>
      <CardContent className="pt-5 pb-4">
        <div className="flex items-center gap-3">
          <Icon className="h-5 w-5 text-muted-foreground shrink-0" />
          <div>
            <p className="text-sm text-muted-foreground font-nunito">{label}</p>
            <p className="text-2xl font-bold font-quicksand">{count}</p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function StageCard({ name, status }: { name: string; status: PipelineStageStatus }) {
  return (
    <div className="flex items-center justify-between p-3 rounded-lg border border-border/60 hover:bg-muted/30 transition-colors">
      <div className="flex items-center gap-2">
        {statusIcon(status)}
        <span className="font-medium font-nunito text-sm capitalize">{name}</span>
      </div>
      {statusBadge(status)}
    </div>
  );
}

function MetricRow({
  step,
  avgMs,
  p50,
  p95,
  p99,
}: {
  step: string;
  avgMs: number;
  p50: number;
  p95: number;
  p99: number;
}) {
  return (
    <tr className="border-b border-border/40 hover:bg-muted/20 transition-colors">
      <td className="py-2.5 pr-4 font-nunito text-sm font-medium capitalize">{step}</td>
      <td className="py-2.5 px-3 text-sm text-muted-foreground font-nunito text-right">
        {formatDuration(avgMs)}
      </td>
      <td className="py-2.5 px-3 text-sm text-muted-foreground font-nunito text-right">
        {formatDuration(p50)}
      </td>
      <td className="py-2.5 px-3 text-sm text-muted-foreground font-nunito text-right">
        {formatDuration(p95)}
      </td>
      <td className="py-2.5 pl-3 text-sm text-muted-foreground font-nunito text-right">
        {formatDuration(p99)}
      </td>
    </tr>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

export function ProcessingPipelineClient() {
  const [health, setHealth] = useState<PipelineHealthResponse | null>(null);
  const [metrics, setMetrics] = useState<ProcessingMetricsResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastRefreshed, setLastRefreshed] = useState<Date | null>(null);

  const fetchData = async () => {
    setIsLoading(true);
    setError(null);
    try {
      const [healthData, metricsData] = await Promise.all([
        adminClient.getPipelineHealth(),
        adminClient.getProcessingMetrics(),
      ]);
      setHealth(healthData);
      setMetrics(metricsData);
      setLastRefreshed(new Date());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load pipeline data');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Activity className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-2xl font-bold font-quicksand">Pipeline Status</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              RAG pipeline health, stage monitoring, and processing metrics
            </p>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {lastRefreshed && (
            <span className="text-xs text-muted-foreground font-nunito">
              Updated {lastRefreshed.toLocaleTimeString()}
            </span>
          )}
          <Button
            variant="outline"
            size="sm"
            onClick={fetchData}
            disabled={isLoading}
            className="font-nunito gap-2"
          >
            <RefreshCw className={`h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      {/* Error */}
      {error && (
        <Alert variant="destructive">
          <AlertDescription className="font-nunito">{error}</AlertDescription>
        </Alert>
      )}

      {/* Loading */}
      {isLoading && (
        <div className="space-y-4">
          <div className="grid grid-cols-3 gap-4">
            {[...Array(3)].map((_, i) => (
              <Skeleton key={i} className="h-24 w-full rounded-xl" />
            ))}
          </div>
          <Skeleton className="h-64 w-full rounded-xl" />
          <Skeleton className="h-48 w-full rounded-xl" />
        </div>
      )}

      {!isLoading && !error && health && (
        <>
          {/* Summary Cards */}
          <div className="grid grid-cols-3 gap-4">
            <SummaryCard
              label="Healthy Stages"
              count={health.summary.healthyCount}
              icon={CheckCircle}
              color="border-l-green-400"
            />
            <SummaryCard
              label="Warning Stages"
              count={health.summary.warningCount}
              icon={AlertTriangle}
              color="border-l-amber-400"
            />
            <SummaryCard
              label="Error Stages"
              count={health.summary.errorCount}
              icon={XCircle}
              color="border-l-red-400"
            />
          </div>

          <div className="grid md:grid-cols-2 gap-6">
            {/* Stage Health */}
            <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-lg">
              <CardHeader>
                <CardTitle className="font-quicksand text-lg flex items-center gap-2">
                  <Activity className="h-4 w-4 text-primary" />
                  Stage Health
                </CardTitle>
              </CardHeader>
              <CardContent>
                {health.stages.length === 0 ? (
                  <p className="text-sm text-muted-foreground font-nunito">
                    No stage data available.
                  </p>
                ) : (
                  <div className="space-y-2">
                    {health.stages.map(stage => (
                      <StageCard key={stage.name} name={stage.name} status={stage.status} />
                    ))}
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Document Distribution */}
            <Card className="border-l-4 border-l-blue-400 shadow-lg">
              <CardHeader>
                <CardTitle className="font-quicksand text-lg flex items-center gap-2">
                  <Database className="h-4 w-4 text-blue-500" />
                  Document Distribution
                </CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="space-y-3 font-nunito">
                  {[
                    { label: 'Total Files', value: health.distribution.totalFiles },
                    { label: 'Total Documents', value: health.distribution.totalDocuments },
                    {
                      label: 'Total Chunks',
                      value: health.distribution.totalChunks.toLocaleString(),
                    },
                    {
                      label: 'Vector Count',
                      value: health.distribution.vectorCount.toLocaleString(),
                    },
                    { label: 'Storage Size', value: health.distribution.storageSizeFormatted },
                  ].map(({ label, value }) => (
                    <li
                      key={label}
                      className="flex justify-between text-sm items-center border-b border-border/30 pb-2 last:border-0 last:pb-0"
                    >
                      <span className="text-muted-foreground">{label}</span>
                      <span className="font-semibold">{value}</span>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          </div>

          {/* Recent Activity */}
          {health.recentActivity.length > 0 && (
            <Card className="border-l-4 border-l-[hsl(240,60%,55%)] shadow-lg">
              <CardHeader>
                <CardTitle className="font-quicksand text-lg flex items-center gap-2">
                  <Clock className="h-4 w-4 text-primary" />
                  Recent Activity
                </CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="space-y-2" role="list">
                  {health.recentActivity.map(activity => (
                    <li
                      key={activity.jobId}
                      className="flex items-center justify-between gap-3 p-2 rounded hover:bg-muted/30 transition-colors"
                    >
                      <div className="flex items-center gap-2 min-w-0">
                        <FileText className="h-4 w-4 text-muted-foreground shrink-0" />
                        <span className="text-sm font-nunito truncate">{activity.fileName}</span>
                      </div>
                      <div className="flex items-center gap-2 shrink-0">
                        {activity.durationMs !== null && (
                          <span className="text-xs text-muted-foreground font-nunito">
                            {formatDuration(activity.durationMs)}
                          </span>
                        )}
                        <Badge variant="secondary" className="text-xs font-nunito">
                          {activity.status}
                        </Badge>
                      </div>
                    </li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          )}
        </>
      )}

      {/* Processing Step Metrics */}
      {!isLoading && !error && metrics && Object.keys(metrics.averages).length > 0 && (
        <Card className="border-l-4 border-l-amber-400 shadow-lg">
          <CardHeader>
            <CardTitle className="font-quicksand text-lg flex items-center gap-2">
              <Clock className="h-4 w-4 text-amber-500" />
              Processing Step Durations
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-muted-foreground font-nunito border-b border-border">
                    <th className="pb-2 text-left font-medium">Step</th>
                    <th className="pb-2 text-right font-medium px-3">Avg</th>
                    <th className="pb-2 text-right font-medium px-3">p50</th>
                    <th className="pb-2 text-right font-medium px-3">p95</th>
                    <th className="pb-2 text-right font-medium pl-3">p99</th>
                  </tr>
                </thead>
                <tbody>
                  {Object.entries(metrics.averages).map(([step, avg]) => {
                    const perc = metrics.percentiles[step] ?? { p50: 0, p95: 0, p99: 0 };
                    return (
                      <MetricRow
                        key={step}
                        step={step}
                        avgMs={avg.avgDuration}
                        p50={perc.p50}
                        p95={perc.p95}
                        p99={perc.p99}
                      />
                    );
                  })}
                </tbody>
              </table>
            </div>
            <p className="text-xs text-muted-foreground font-nunito mt-3">
              Last updated: {new Date(metrics.lastUpdated).toLocaleString()}
            </p>
          </CardContent>
        </Card>
      )}

      {/* No data state */}
      {!isLoading && !error && !health && !metrics && (
        <Card>
          <CardContent className="py-12 text-center text-muted-foreground font-nunito">
            No pipeline data available.
          </CardContent>
        </Card>
      )}
    </div>
  );
}
