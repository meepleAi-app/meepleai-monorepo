'use client';

import { useQuery } from '@tanstack/react-query';
import { format, subDays } from 'date-fns';
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  Bot,
  CircleDollarSign,
  Clock,
  FileSearch,
  Gauge,
  PlusCircle,
  Search,
  Zap,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

// ─── Types ───────────────────────────────────────────────────────────────────

interface AgentMetrics {
  totalInvocations: number;
  totalTokensUsed: number;
  totalCost: number;
  avgLatencyMs: number;
  avgConfidenceScore: number;
  userSatisfactionRate: number;
  topQueries: Array<{ query: string; count: number }>;
  costBreakdown: Array<{ category: string; cost: number; invocations: number; tokens: number }>;
  usageOverTime: Array<{ date: string; count: number; cost: number; tokens: number }>;
}

interface RagExecution {
  id: string;
  query: string;
  strategy: string;
  totalLatencyMs: number;
  totalTokens: number;
  totalCost: number;
  confidence: number | null;
  status: string;
  createdAt: string;
}

interface EmbeddingInfo {
  status: string;
  model: string | null;
}

interface OpenRouterStatus {
  currentRpm: number;
  limitRpm: number;
  isThrottled: boolean;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatLatency(ms: number): string {
  return `${(ms / 1000).toFixed(1)}s`;
}

function formatTokens(n: number): string {
  if (n >= 1000) return `${(n / 1000).toFixed(1)}K`;
  return String(n);
}

function formatCost(n: number): string {
  return `$${n.toFixed(2)}`;
}

function formatErrorRate(rate: number): string {
  return `${(rate * 100).toFixed(1)}%`;
}

// ─── Component ───────────────────────────────────────────────────────────────

export default function MissionControlPage() {
  const router = useRouter();

  const today = format(new Date(), 'yyyy-MM-dd');
  const yesterday = format(subDays(new Date(), 1), 'yyyy-MM-dd');

  const { data: metrics, isLoading: metricsLoading } = useQuery({
    queryKey: ['admin', 'mission-control', 'metrics'],
    queryFn: () => api.admin.getAgentMetrics(yesterday, today) as Promise<AgentMetrics>,
    staleTime: 60_000,
  });

  const { data: ragData, isLoading: ragLoading } = useQuery({
    queryKey: ['admin', 'mission-control', 'rag-executions'],
    queryFn: () => api.admin.getRagExecutions({ take: 5 }),
    staleTime: 30_000,
  });

  const { data: embeddingInfo } = useQuery({
    queryKey: ['admin', 'mission-control', 'embedding'],
    queryFn: () => api.admin.getEmbeddingInfo(),
    staleTime: 120_000,
  });

  const { data: openRouterStatus } = useQuery({
    queryKey: ['admin', 'mission-control', 'openrouter'],
    queryFn: () => api.admin.getOpenRouterStatus(),
    staleTime: 60_000,
  });

  // Derive error rate from satisfaction (placeholder — real metric would come from backend)
  const errorRate = metrics ? 1 - metrics.userSatisfactionRate : 0;

  const executions = (ragData?.items ?? []) as RagExecution[];

  // ─── Service Health ──────────────────────────────────────────────────────

  type HealthStatus = 'healthy' | 'degraded' | 'down' | 'unknown';

  function getServiceHealth(name: string): HealthStatus {
    if (name === 'Embedding Service') {
      if (!embeddingInfo) return 'unknown';
      return (embeddingInfo as EmbeddingInfo).status === 'healthy' ? 'healthy' : 'degraded';
    }
    if (name === 'OpenRouter API') {
      if (!openRouterStatus) return 'unknown';
      const or = openRouterStatus as OpenRouterStatus;
      if (or.isThrottled) return 'degraded';
      return 'healthy';
    }
    // Reranker and Vector DB don't have dedicated endpoints yet
    return 'unknown';
  }

  function healthBadge(status: HealthStatus) {
    const variants: Record<HealthStatus, { label: string; className: string }> = {
      healthy: {
        label: 'Attivo',
        className: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
      },
      degraded: {
        label: 'Degradato',
        className: 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
      },
      down: {
        label: 'Non disponibile',
        className: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
      },
      unknown: {
        label: 'Sconosciuto',
        className: 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400',
      },
    };
    const v = variants[status];
    return (
      <span
        className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${v.className}`}
      >
        {v.label}
      </span>
    );
  }

  const services = [
    { name: 'Embedding Service', icon: Bot },
    { name: 'Reranker Service', icon: Gauge },
    { name: 'OpenRouter API', icon: Zap },
    { name: 'Vector DB', icon: Search },
  ];

  // ─── Quick Actions ───────────────────────────────────────────────────────

  const quickActions = [
    { label: 'Testa Query RAG', icon: Search, href: '/admin/agents/playground' },
    { label: 'Ispeziona Esecuzioni', icon: FileSearch, href: '/admin/agents/inspector' },
    { label: 'Report Costi', icon: CircleDollarSign, href: '/admin/agents/usage' },
    { label: 'Nuovo Agent', icon: PlusCircle, href: '/admin/agents/definitions/create' },
  ];

  // ─── Render ──────────────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          AI Mission Control
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Panoramica dell&apos;infrastruttura AI, agenti e RAG pipeline
        </p>
      </div>

      {/* Row 1 — KPI Cards */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
        {/* Esecuzioni Oggi */}
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardContent className="pt-4 pb-3 px-4">
            <div className="flex items-center gap-2 text-amber-600 dark:text-amber-400 mb-1">
              <Activity className="h-4 w-4" />
              <span className="text-xs font-medium">Esecuzioni Oggi</span>
            </div>
            {metricsLoading ? (
              <Skeleton className="h-7 w-20" />
            ) : (
              <p className="text-2xl font-bold tracking-tight">{metrics?.totalInvocations ?? 0}</p>
            )}
          </CardContent>
        </Card>

        {/* Latenza Media */}
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardContent className="pt-4 pb-3 px-4">
            <div className="flex items-center gap-2 text-blue-600 dark:text-blue-400 mb-1">
              <Clock className="h-4 w-4" />
              <span className="text-xs font-medium">Latenza Media</span>
            </div>
            {metricsLoading ? (
              <Skeleton className="h-7 w-20" />
            ) : (
              <p className="text-2xl font-bold tracking-tight">
                {metrics ? formatLatency(metrics.avgLatencyMs) : '—'}
              </p>
            )}
          </CardContent>
        </Card>

        {/* Error Rate */}
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardContent className="pt-4 pb-3 px-4">
            <div
              className={`flex items-center gap-2 mb-1 ${errorRate >= 0.05 ? 'text-red-600 dark:text-red-400' : 'text-emerald-600 dark:text-emerald-400'}`}
            >
              <AlertTriangle className="h-4 w-4" />
              <span className="text-xs font-medium">Error Rate</span>
            </div>
            {metricsLoading ? (
              <Skeleton className="h-7 w-20" />
            ) : (
              <p className="text-2xl font-bold tracking-tight">{formatErrorRate(errorRate)}</p>
            )}
          </CardContent>
        </Card>

        {/* Token Consumati */}
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardContent className="pt-4 pb-3 px-4">
            <div className="flex items-center gap-2 text-purple-600 dark:text-purple-400 mb-1">
              <Zap className="h-4 w-4" />
              <span className="text-xs font-medium">Token Consumati</span>
            </div>
            {metricsLoading ? (
              <Skeleton className="h-7 w-20" />
            ) : (
              <p className="text-2xl font-bold tracking-tight">
                {metrics ? formatTokens(metrics.totalTokensUsed) : '0'}
              </p>
            )}
          </CardContent>
        </Card>

        {/* Costo Oggi */}
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardContent className="pt-4 pb-3 px-4">
            <div className="flex items-center gap-2 text-red-600 dark:text-red-400 mb-1">
              <CircleDollarSign className="h-4 w-4" />
              <span className="text-xs font-medium">Costo Oggi</span>
            </div>
            {metricsLoading ? (
              <Skeleton className="h-7 w-20" />
            ) : (
              <p className="text-2xl font-bold tracking-tight">
                {metrics ? formatCost(metrics.totalCost) : '$0.00'}
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Row 2 — Health + Quick Actions */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        {/* Service Health */}
        <Card className="lg:col-span-2 bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-semibold">Stato Servizi</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {services.map(svc => {
              const Icon = svc.icon;
              const status = getServiceHealth(svc.name);
              return (
                <div
                  key={svc.name}
                  className="flex items-center justify-between rounded-lg border border-slate-200/60 dark:border-zinc-700/40 px-3 py-2"
                >
                  <div className="flex items-center gap-2">
                    <Icon className="h-4 w-4 text-muted-foreground" />
                    <span className="text-sm">{svc.name}</span>
                  </div>
                  {healthBadge(status)}
                </div>
              );
            })}
          </CardContent>
        </Card>

        {/* Azioni Rapide */}
        <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
          <CardHeader className="pb-3">
            <CardTitle className="text-sm font-semibold">Azioni Rapide</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {quickActions.map(action => {
              const Icon = action.icon;
              return (
                <Button
                  key={action.label}
                  variant="outline"
                  className="w-full justify-start gap-2"
                  onClick={() => router.push(action.href)}
                >
                  <Icon className="h-4 w-4" />
                  {action.label}
                </Button>
              );
            })}
          </CardContent>
        </Card>
      </div>

      {/* Row 3 — Recent Executions */}
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader className="flex flex-row items-center justify-between pb-3">
          <CardTitle className="text-sm font-semibold">Ultime Esecuzioni RAG</CardTitle>
          <Link
            href="/admin/agents/inspector"
            className="text-xs text-muted-foreground hover:text-foreground transition-colors flex items-center gap-1"
          >
            Vedi tutte <ArrowRight className="h-3 w-3" />
          </Link>
        </CardHeader>
        <CardContent>
          {ragLoading ? (
            <div className="space-y-2">
              {[1, 2, 3].map(i => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : executions.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-6">
              Nessuna esecuzione recente
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-slate-200/60 dark:border-zinc-700/40 text-left">
                    <th className="pb-2 font-medium text-muted-foreground">Query</th>
                    <th className="pb-2 font-medium text-muted-foreground">Strategy</th>
                    <th className="pb-2 font-medium text-muted-foreground text-right">Latenza</th>
                    <th className="pb-2 font-medium text-muted-foreground text-right">Token</th>
                    <th className="pb-2 font-medium text-muted-foreground text-right">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {executions.map(exec => (
                    <tr
                      key={exec.id}
                      className="border-b border-slate-100/60 dark:border-zinc-800/40 last:border-0"
                    >
                      <td className="py-2 pr-4 max-w-[280px] truncate">{exec.query}</td>
                      <td className="py-2 pr-4">
                        <Badge variant="secondary" className="text-xs">
                          {exec.strategy}
                        </Badge>
                      </td>
                      <td className="py-2 pr-4 text-right tabular-nums">
                        {formatLatency(exec.totalLatencyMs)}
                      </td>
                      <td className="py-2 pr-4 text-right tabular-nums">
                        {formatTokens(exec.totalTokens)}
                      </td>
                      <td className="py-2 text-right">
                        <Badge
                          variant={exec.status === 'ok' ? 'default' : 'destructive'}
                          className="text-xs"
                        >
                          {exec.status}
                        </Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
