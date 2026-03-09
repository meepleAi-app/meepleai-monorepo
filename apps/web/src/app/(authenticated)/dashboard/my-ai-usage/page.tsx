'use client';

/**
 * My AI Usage Page — Issue #5484
 * Route: /dashboard/my-ai-usage
 * Self-service AI usage dashboard for Editor and Admin roles.
 */

import { useEffect, useState, useCallback } from 'react';

import {
  Brain,
  Cpu,
  Zap,
  DollarSign,
  BarChart3,
  RefreshCw,
} from 'lucide-react';

import { RequireRole } from '@/components/auth/RequireRole';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { useAuth } from '@/hooks/useAuth';
import { dashboardClient } from '@/lib/api/dashboard-client';
import type { UserAiUsageDto, ModelUsageDto, DailyUsageDto } from '@/lib/api/schemas/ai-usage.schemas';
import { cn } from '@/lib/utils';

type Period = 1 | 7 | 30;

const PERIOD_OPTIONS: { value: Period; label: string }[] = [
  { value: 1, label: 'Oggi' },
  { value: 7, label: '7 giorni' },
  { value: 30, label: '30 giorni' },
];

export default function MyAiUsagePage() {
  return (
    <RequireRole allowedRoles={['Editor', 'Admin']}>
      <MyAiUsageContent />
    </RequireRole>
  );
}

function MyAiUsageContent() {
  const { user } = useAuth();
  const [usage, setUsage] = useState<UserAiUsageDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [period, setPeriod] = useState<Period>(7);

  const fetchUsage = useCallback(async (days: Period) => {
    try {
      setLoading(true);
      setError(null);
      const data = await dashboardClient.getMyAiUsage(days);
      setUsage(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Errore nel caricamento');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!user) return;
    fetchUsage(period);
  }, [user, period, fetchUsage]);

  const handlePeriodChange = (p: Period) => {
    setPeriod(p);
  };

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
            Statistiche personali di utilizzo dei modelli AI
          </p>
        </div>
        <div className="flex items-center gap-2">
          {/* Period selector */}
          <div className="flex rounded-md border" data-testid="period-selector">
            {PERIOD_OPTIONS.map(opt => (
              <button
                key={opt.value}
                type="button"
                onClick={() => handlePeriodChange(opt.value)}
                className={cn(
                  'px-3 py-1.5 text-xs font-medium transition-colors',
                  'first:rounded-l-md last:rounded-r-md',
                  period === opt.value
                    ? 'bg-primary text-primary-foreground'
                    : 'bg-background text-muted-foreground hover:bg-muted'
                )}
                data-testid={`period-${opt.value}d`}
              >
                {opt.label}
              </button>
            ))}
          </div>
          <button
            type="button"
            onClick={() => fetchUsage(period)}
            disabled={loading}
            className="p-2 rounded-md border hover:bg-muted transition-colors disabled:opacity-50"
            aria-label="Aggiorna"
          >
            <RefreshCw className={cn('h-4 w-4', loading && 'animate-spin')} />
          </button>
        </div>
      </div>

      {/* Error */}
      {error && (
        <Card className="border-destructive/50 bg-destructive/5">
          <CardContent className="py-4 text-sm text-destructive">
            {error}
          </CardContent>
        </Card>
      )}

      {/* Loading skeleton */}
      {loading && !usage && (
        <div className="grid gap-4 md:grid-cols-4" data-testid="usage-skeleton">
          {[...Array(4)].map((_, i) => (
            <Card key={i}>
              <CardContent className="py-6">
                <div className="h-4 w-24 bg-muted animate-pulse rounded mb-2" />
                <div className="h-8 w-16 bg-muted animate-pulse rounded" />
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* Stats cards */}
      {usage && (
        <>
          <div className="grid gap-4 md:grid-cols-4" data-testid="usage-stats">
            <StatCard
              icon={Zap}
              label="Richieste"
              value={usage.requestCount.toLocaleString()}
              description={`Ultimi ${period === 1 ? 'oggi' : `${period} giorni`}`}
            />
            <StatCard
              icon={Cpu}
              label="Token totali"
              value={formatTokens(usage.totalTokens)}
              description="Prompt + completamento"
            />
            <StatCard
              icon={DollarSign}
              label="Costo stimato"
              value={`$${usage.totalCostUsd.toFixed(4)}`}
              description="USD equivalente"
            />
            <StatCard
              icon={BarChart3}
              label="Modelli usati"
              value={usage.byModel.length.toString()}
              description="Modelli diversi"
            />
          </div>

          {/* Model distribution + Daily chart */}
          <div className="grid gap-4 md:grid-cols-2">
            <ModelDistribution models={usage.byModel} totalTokens={usage.totalTokens} />
            <DailyUsageChart dailyUsage={usage.dailyUsage} />
          </div>

          {/* Operation breakdown */}
          {usage.byOperation.length > 0 && (
            <Card>
              <CardHeader>
                <CardTitle className="text-base">Distribuzione per tipo</CardTitle>
                <CardDescription>Ripartizione delle richieste per tipologia operazione</CardDescription>
              </CardHeader>
              <CardContent>
                <div className="space-y-2">
                  {usage.byOperation.map(op => (
                    <div
                      key={op.operation}
                      className="flex items-center justify-between text-sm"
                    >
                      <span className="font-medium capitalize">{op.operation}</span>
                      <div className="flex items-center gap-4 tabular-nums">
                        <span className="text-muted-foreground">{op.count} richieste</span>
                        <span>{formatTokens(op.tokens)} token</span>
                      </div>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>
          )}
        </>
      )}

      {/* Empty state */}
      {!loading && usage && usage.requestCount === 0 && (
        <Card>
          <CardContent className="py-12 text-center text-muted-foreground">
            <Brain className="h-12 w-12 mx-auto mb-3 opacity-30" />
            <p>Nessun utilizzo AI nel periodo selezionato.</p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

// ─── Sub-components ────────────────────────────────────────────────────────

function StatCard({
  icon: Icon,
  label,
  value,
  description,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  value: string;
  description: string;
}) {
  return (
    <Card>
      <CardContent className="py-4">
        <div className="flex items-center gap-2 text-muted-foreground mb-1">
          <Icon className="h-4 w-4" />
          <span className="text-xs font-medium">{label}</span>
        </div>
        <div className="text-2xl font-bold tabular-nums">{value}</div>
        <p className="text-xs text-muted-foreground mt-0.5">{description}</p>
      </CardContent>
    </Card>
  );
}

function ModelDistribution({
  models,
  totalTokens,
}: {
  models: ModelUsageDto[];
  totalTokens: number;
}) {
  if (models.length === 0) return null;

  const sorted = [...models].sort((a, b) => b.tokens - a.tokens);
  const colors = [
    'bg-blue-500', 'bg-violet-500', 'bg-amber-500',
    'bg-emerald-500', 'bg-rose-500', 'bg-cyan-500',
  ];

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Distribuzione modelli</CardTitle>
        <CardDescription>Token per modello AI utilizzato</CardDescription>
      </CardHeader>
      <CardContent className="space-y-3">
        {sorted.map((m, i) => {
          const pct = totalTokens > 0 ? (m.tokens / totalTokens) * 100 : 0;
          return (
            <div key={m.model} className="space-y-1">
              <div className="flex items-center justify-between text-xs">
                <span className="font-mono truncate max-w-[200px]" title={m.model}>
                  {m.model}
                </span>
                <span className="tabular-nums text-muted-foreground ml-2">
                  {pct.toFixed(0)}%
                </span>
              </div>
              <div className="h-2 rounded-full bg-muted overflow-hidden">
                <div
                  className={cn('h-full rounded-full transition-all', colors[i % colors.length])}
                  style={{ width: `${pct}%` }}
                />
              </div>
              <div className="flex justify-between text-[10px] text-muted-foreground tabular-nums">
                <span>{formatTokens(m.tokens)} token</span>
                <span>${m.cost.toFixed(4)}</span>
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}

function DailyUsageChart({ dailyUsage }: { dailyUsage: DailyUsageDto[] }) {
  if (dailyUsage.length === 0) return null;

  const maxTokens = Math.max(...dailyUsage.map(d => d.tokens), 1);

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Utilizzo giornaliero</CardTitle>
        <CardDescription>Token consumati per giorno</CardDescription>
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

// ─── Helpers ───────────────────────────────────────────────────────────────

function formatTokens(tokens: number): string {
  if (tokens >= 1_000_000) return `${(tokens / 1_000_000).toFixed(1)}M`;
  if (tokens >= 1_000) return `${(tokens / 1_000).toFixed(1)}K`;
  return tokens.toString();
}
