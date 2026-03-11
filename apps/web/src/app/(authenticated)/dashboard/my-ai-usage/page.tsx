'use client';

/**
 * My AI Usage Page — Issue #94 (C3: Editor Self-Service AI Usage Page)
 * Route: /dashboard/my-ai-usage
 * Self-service AI usage dashboard for Editor and Admin roles.
 * Shows multi-period summary, distribution charts, and recent requests.
 */

import { useEffect, useState, useCallback } from 'react';

import {
  Brain,
  Cpu,
  Zap,
  DollarSign,
  Clock,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
  Info,
} from 'lucide-react';

import { RequireRole } from '@/components/auth/RequireRole';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuth } from '@/hooks/useAuth';
import { dashboardClient } from '@/lib/api/dashboard-client';
import type {
  AiUsageSummaryDto,
  AiUsagePeriodSummaryDto,
  AiUsageDistributionsDto,
  DistributionItemDto,
  AiUsageRecentDto,
  UserAiUsageDto,
} from '@/lib/api/schemas/ai-usage.schemas';
import { cn } from '@/lib/utils';

export default function MyAiUsagePage() {
  return (
    <RequireRole allowedRoles={['Editor', 'Admin']}>
      <MyAiUsageContent />
    </RequireRole>
  );
}

function MyAiUsageContent() {
  const { user } = useAuth();
  const [summary, setSummary] = useState<AiUsageSummaryDto | null>(null);
  const [distributions, setDistributions] = useState<AiUsageDistributionsDto | null>(null);
  const [recent, setRecent] = useState<AiUsageRecentDto | null>(null);
  const [dailyData, setDailyData] = useState<UserAiUsageDto | null>(null);
  const [recentPage, setRecentPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchAll = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [summaryData, distData, recentData, daily] = await Promise.all([
        dashboardClient.getMyAiUsageSummary(),
        dashboardClient.getMyAiUsageDistributions(30),
        dashboardClient.getMyAiUsageRecent(1, 20),
        dashboardClient.getMyAiUsage(30),
      ]);
      setSummary(summaryData);
      setDistributions(distData);
      setRecent(recentData);
      setDailyData(daily);
      setRecentPage(1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Errore nel caricamento');
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchRecentPage = useCallback(async (page: number) => {
    try {
      const data = await dashboardClient.getMyAiUsageRecent(page, 20);
      setRecent(data);
      setRecentPage(page);
    } catch {
      // Keep existing data on pagination error
    }
  }, []);

  useEffect(() => {
    if (!user) return;
    fetchAll();
  }, [user, fetchAll]);

  return (
    <div className="container mx-auto py-8 space-y-6" data-testid="my-ai-usage-page">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <Brain className="h-6 w-6" />
            Il mio utilizzo AI
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Scopri come utilizzi le funzionalità AI di MeepleAI
          </p>
        </div>
        <button
          type="button"
          onClick={fetchAll}
          disabled={loading}
          className="p-2 rounded-md border hover:bg-muted transition-colors disabled:opacity-50"
          aria-label="Aggiorna"
        >
          <RefreshCw className={cn('h-4 w-4', loading && 'animate-spin')} />
        </button>
      </div>

      {/* Error */}
      {error && (
        <Card className="border-destructive/50 bg-destructive/5">
          <CardContent className="py-4 text-sm text-destructive">{error}</CardContent>
        </Card>
      )}

      {/* Loading skeleton */}
      {loading && !summary && (
        <div className="grid gap-4 md:grid-cols-3" data-testid="usage-skeleton">
          {[...Array(3)].map((_, i) => (
            <Card key={i}>
              <CardContent className="py-6">
                <div className="h-4 w-24 bg-muted animate-pulse rounded mb-2" />
                <div className="h-8 w-16 bg-muted animate-pulse rounded" />
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* Summary cards — today / 7d / 30d */}
      {summary && (
        <div className="grid gap-4 md:grid-cols-3" data-testid="usage-summary">
          <SummaryCard label="Oggi" period={summary.today} />
          <SummaryCard label="7 Giorni" period={summary.last7Days} />
          <SummaryCard label="30 Giorni" period={summary.last30Days} />
        </div>
      )}

      {/* Daily chart + Distributions */}
      {(dailyData || distributions) && (
        <div className="grid gap-4 md:grid-cols-2">
          {dailyData && <DailyUsageChart dailyUsage={dailyData.dailyUsage} />}
          {distributions && distributions.models.length > 0 && (
            <DistributionCard title="Distribuzione modelli" items={distributions.models} />
          )}
        </div>
      )}

      {distributions && distributions.providers.length > 0 && (
        <div className="grid gap-4 md:grid-cols-2">
          <DistributionCard title="Distribuzione provider" items={distributions.providers} />
          {distributions.operations.length > 0 && (
            <DistributionCard title="Distribuzione operazioni" items={distributions.operations} />
          )}
        </div>
      )}

      {/* Recent requests table */}
      {recent && (
        <RecentRequestsTable
          data={recent}
          currentPage={recentPage}
          onPageChange={fetchRecentPage}
        />
      )}

      {/* Info banner */}
      <div
        className="flex items-start gap-2 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm text-blue-800 dark:border-blue-800 dark:bg-blue-950/30 dark:text-blue-200"
        data-testid="usage-info-banner"
      >
        <Info className="h-4 w-4 mt-0.5 flex-shrink-0" />
        <p>
          I dettagli individuali delle richieste sono disponibili per gli ultimi 7 giorni. I totali
          aggregati coprono fino a 30 giorni.
        </p>
      </div>

      {/* Empty state */}
      {!loading && summary && summary.last30Days.requestCount === 0 && (
        <Card>
          <CardContent className="py-12 text-center text-muted-foreground">
            <Brain className="h-12 w-12 mx-auto mb-3 opacity-30" />
            <p>Non hai ancora utilizzato le funzionalità AI.</p>
            <p className="text-xs mt-1">Prova a chattare con un agente!</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

// ─── Sub-components ────────────────────────────────────────────────────────

function SummaryCard({ label, period }: { label: string; period: AiUsagePeriodSummaryDto }) {
  return (
    <Card data-testid={`summary-card-${label.toLowerCase().replace(/\s/g, '-')}`}>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{label}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-2">
        <div className="flex items-center gap-2">
          <Zap className="h-4 w-4 text-amber-500" />
          <span className="text-2xl font-bold tabular-nums">
            {period.requestCount.toLocaleString()}
          </span>
          <span className="text-xs text-muted-foreground">richieste</span>
        </div>
        <div className="grid grid-cols-3 gap-2 text-xs">
          <div className="flex items-center gap-1">
            <Cpu className="h-3 w-3 text-muted-foreground" />
            <span className="tabular-nums">{formatTokens(period.totalTokens)}</span>
          </div>
          <div className="flex items-center gap-1">
            <DollarSign className="h-3 w-3 text-muted-foreground" />
            <span className="tabular-nums">${period.costUsd.toFixed(4)}</span>
          </div>
          <div className="flex items-center gap-1">
            <Clock className="h-3 w-3 text-muted-foreground" />
            <span className="tabular-nums">{period.averageLatencyMs}ms</span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

function DailyUsageChart({ dailyUsage }: { dailyUsage: { date: string; tokens: number }[] }) {
  if (dailyUsage.length === 0) return null;

  const maxTokens = Math.max(...dailyUsage.map(d => d.tokens), 1);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Utilizzo giornaliero</CardTitle>
        <CardDescription>Token consumati per giorno (30g)</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="flex items-end gap-1 h-32" data-testid="daily-usage-chart">
          {dailyUsage.map(d => {
            const height = (d.tokens / maxTokens) * 100;
            return (
              <div
                key={d.date}
                className="flex-1 flex flex-col items-center gap-1"
                title={`${d.date}: ${d.tokens.toLocaleString()} token`}
              >
                <div className="w-full flex items-end justify-center" style={{ height: '100px' }}>
                  <div
                    className="w-full max-w-[20px] rounded-t bg-primary/70 transition-all"
                    style={{ height: `${Math.max(height, 2)}%` }}
                  />
                </div>
                <span className="text-[9px] text-muted-foreground tabular-nums">
                  {d.date.slice(-2)}
                </span>
              </div>
            );
          })}
        </div>
      </CardContent>
    </Card>
  );
}

const DISTRIBUTION_COLORS = [
  'bg-blue-500',
  'bg-violet-500',
  'bg-amber-500',
  'bg-emerald-500',
  'bg-rose-500',
  'bg-cyan-500',
  'bg-orange-500',
  'bg-teal-500',
];

function DistributionCard({ title, items }: { title: string; items: DistributionItemDto[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {items.map((item, i) => (
          <div key={item.name} className="space-y-1">
            <div className="flex items-center justify-between text-xs">
              <span className="font-mono truncate max-w-[200px]" title={item.name}>
                {item.name}
              </span>
              <span className="tabular-nums text-muted-foreground ml-2">
                {item.percentage.toFixed(1)}%
              </span>
            </div>
            <div className="h-2 rounded-full bg-muted overflow-hidden">
              <div
                className={cn(
                  'h-full rounded-full transition-all',
                  DISTRIBUTION_COLORS[i % DISTRIBUTION_COLORS.length]
                )}
                style={{ width: `${item.percentage}%` }}
              />
            </div>
            <div className="text-[10px] text-muted-foreground tabular-nums">
              {item.count} richieste
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

function RecentRequestsTable({
  data,
  currentPage,
  onPageChange,
}: {
  data: AiUsageRecentDto;
  currentPage: number;
  onPageChange: (page: number) => void;
}) {
  if (data.items.length === 0 && currentPage === 1) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Richieste recenti</CardTitle>
          <CardDescription>Ultimi 7 giorni</CardDescription>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground text-center py-4">
            Nessuna richiesta negli ultimi 7 giorni.
          </p>
        </CardContent>
      </Card>
    );
  }

  const totalPages = Math.ceil(data.total / data.pageSize);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Richieste recenti</CardTitle>
        <CardDescription>Ultimi 7 giorni — {data.total} richieste totali</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="overflow-x-auto">
          <table className="w-full text-xs" data-testid="recent-requests-table">
            <thead>
              <tr className="border-b text-left text-muted-foreground">
                <th className="pb-2 pr-4">Data/Ora</th>
                <th className="pb-2 pr-4">Modello</th>
                <th className="pb-2 pr-4">Provider</th>
                <th className="pb-2 pr-4">Operazione</th>
                <th className="pb-2 pr-4 text-right">Token</th>
                <th className="pb-2 pr-4 text-right">Costo</th>
                <th className="pb-2 text-right">Latenza</th>
              </tr>
            </thead>
            <tbody>
              {data.items.map((item, i) => (
                <tr key={i} className="border-b last:border-0">
                  <td className="py-2 pr-4 tabular-nums whitespace-nowrap">
                    {new Date(item.requestedAt).toLocaleString('it-IT', {
                      month: '2-digit',
                      day: '2-digit',
                      hour: '2-digit',
                      minute: '2-digit',
                    })}
                  </td>
                  <td className="py-2 pr-4 font-mono truncate max-w-[160px]" title={item.model}>
                    {item.model}
                  </td>
                  <td className="py-2 pr-4">{item.provider}</td>
                  <td className="py-2 pr-4 capitalize">{item.operation}</td>
                  <td className="py-2 pr-4 tabular-nums text-right">
                    {(item.promptTokens + item.completionTokens).toLocaleString()}
                  </td>
                  <td className="py-2 pr-4 tabular-nums text-right">${item.costUsd.toFixed(4)}</td>
                  <td className="py-2 tabular-nums text-right">{item.latencyMs}ms</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {totalPages > 1 && (
          <div className="flex items-center justify-between mt-3 pt-3 border-t">
            <span className="text-xs text-muted-foreground">
              Pagina {currentPage} di {totalPages}
            </span>
            <div className="flex gap-1">
              <button
                type="button"
                onClick={() => onPageChange(currentPage - 1)}
                disabled={currentPage <= 1}
                className="p-1 rounded hover:bg-muted disabled:opacity-30"
                aria-label="Pagina precedente"
              >
                <ChevronLeft className="h-4 w-4" />
              </button>
              <button
                type="button"
                onClick={() => onPageChange(currentPage + 1)}
                disabled={currentPage >= totalPages}
                className="p-1 rounded hover:bg-muted disabled:opacity-30"
                aria-label="Pagina successiva"
              >
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ─── Helpers ───────────────────────────────────────────────────────────────

function formatTokens(tokens: number): string {
  if (tokens >= 1_000_000) return `${(tokens / 1_000_000).toFixed(1)}M`;
  if (tokens >= 1_000) return `${(tokens / 1_000).toFixed(1)}K`;
  return tokens.toString();
}
