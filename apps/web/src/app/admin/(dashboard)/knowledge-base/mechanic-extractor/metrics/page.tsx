'use client';

import { useMemo, useState } from 'react';
import type { JSX } from 'react';

import { useQuery } from '@tanstack/react-query';
import { CheckCircle2, Clock, DollarSign, ListChecks } from 'lucide-react';

import { MechanicCostChart } from '@/components/admin/mechanic-extractor/metrics/MechanicCostChart';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { StatCard } from '@/components/ui/data-display/stat-card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

type Period = 7 | 30 | 90;
const PERIODS: Period[] = [7, 30, 90];
const RECENT_PAGE_SIZE = 25;

const STATUS_LABELS: Record<number, string> = {
  0: 'Draft',
  1: 'In Review',
  2: 'Published',
  3: 'Rejected',
  4: 'Partial',
};
const STATUS_FILTER_OPTIONS = [0, 1, 2, 3, 4];

const ALL = 'all';

export default function MechanicMetricsPage(): JSX.Element {
  const adminClient = useMemo(() => createAdminClient({ httpClient: new HttpClient() }), []);

  const [period, setPeriod] = useState<Period>(30);
  const [gameId, setGameId] = useState<string | undefined>();
  const [reviewerId, setReviewerId] = useState<string | undefined>();
  const [status, setStatus] = useState<number | undefined>();
  const [offset, setOffset] = useState(0);

  const startDate = useMemo(() => {
    const d = new Date();
    d.setUTCDate(d.getUTCDate() - period);
    return d.toISOString();
  }, [period]);

  const summaryQuery = useQuery({
    queryKey: ['me-metrics', 'summary', gameId, reviewerId, startDate],
    queryFn: () => adminClient.getMechanicMetricsSummary({ gameId, reviewerId, startDate }),
    staleTime: 60_000,
  });

  const costQuery = useQuery({
    queryKey: ['me-metrics', 'cost', period, gameId, reviewerId],
    queryFn: () => adminClient.getMechanicCostByDay(period, { gameId, reviewerId }),
    staleTime: 60_000,
  });

  const recentQuery = useQuery({
    queryKey: ['me-metrics', 'recent', offset, gameId, reviewerId, status],
    queryFn: () =>
      adminClient.getMechanicRecentAnalyses({
        limit: RECENT_PAGE_SIZE,
        offset,
        gameId,
        reviewerId,
        status,
      }),
    staleTime: 30_000,
  });

  // Filter dropdown options: distinct games/reviewers across a broad unfiltered fetch.
  const optionsQuery = useQuery({
    queryKey: ['me-metrics', 'filter-options'],
    queryFn: () => adminClient.getMechanicRecentAnalyses({ limit: 200 }),
    staleTime: 5 * 60_000,
  });

  const gameOptions = useMemo(() => {
    const map = new Map<string, string>();
    for (const r of optionsQuery.data?.items ?? []) {
      map.set(r.sharedGameId, r.gameName);
    }
    return Array.from(map, ([id, name]) => ({ id, name })).sort((a, b) =>
      a.name.localeCompare(b.name)
    );
  }, [optionsQuery.data]);

  const reviewerOptions = useMemo(() => {
    const map = new Map<string, string>();
    for (const r of optionsQuery.data?.items ?? []) {
      if (r.reviewedBy) {
        map.set(r.reviewedBy, r.reviewerName ?? r.reviewedBy);
      }
    }
    return Array.from(map, ([id, name]) => ({ id, name })).sort((a, b) =>
      a.name.localeCompare(b.name)
    );
  }, [optionsQuery.data]);

  const summary = summaryQuery.data;
  const recent = recentQuery.data;

  const handleExport = async () => {
    const blob = await adminClient.exportMechanicAnalysesCsv({
      gameId,
      reviewerId,
      status,
      startDate,
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'mechanic-analyses.csv';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  };

  return (
    <div className="space-y-6" data-testid="mechanic-metrics-page">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Mechanic Extractor — Metriche
          </h1>
          <p className="text-sm text-muted-foreground">
            Costi, tempi di review e approval rate della pipeline AI.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {PERIODS.map(p => (
            <Button
              key={p}
              size="sm"
              variant={period === p ? 'default' : 'outline'}
              onClick={() => setPeriod(p)}
            >
              {p}g
            </Button>
          ))}
          <Button size="sm" variant="outline" onClick={handleExport} data-testid="export-csv">
            Export CSV
          </Button>
        </div>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-2">
        <Select
          value={gameId ?? ALL}
          onValueChange={v => {
            setGameId(v === ALL ? undefined : v);
            setOffset(0);
          }}
        >
          <SelectTrigger className="w-48" aria-label="Filtra per gioco">
            <SelectValue placeholder="Gioco" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tutti i giochi</SelectItem>
            {gameOptions.map(g => (
              <SelectItem key={g.id} value={g.id}>
                {g.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={reviewerId ?? ALL}
          onValueChange={v => {
            setReviewerId(v === ALL ? undefined : v);
            setOffset(0);
          }}
        >
          <SelectTrigger className="w-48" aria-label="Filtra per reviewer">
            <SelectValue placeholder="Reviewer" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tutti i reviewer</SelectItem>
            {reviewerOptions.map(r => (
              <SelectItem key={r.id} value={r.id}>
                {r.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={status === undefined ? ALL : String(status)}
          onValueChange={v => {
            setStatus(v === ALL ? undefined : Number(v));
            setOffset(0);
          }}
        >
          <SelectTrigger className="w-40" aria-label="Filtra per status">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tutti gli status</SelectItem>
            {STATUS_FILTER_OPTIONS.map(s => (
              <SelectItem key={s} value={String(s)}>
                {STATUS_LABELS[s]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {/* KPI tiles */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          label="Costo medio / analisi"
          value={summary ? `$${summary.averageCostUsd.toFixed(2)}` : '—'}
          icon={DollarSign}
          loading={summaryQuery.isLoading}
        />
        <StatCard
          label="Tempo review medio"
          value={
            summary?.averageReviewTimeHours != null
              ? `${summary.averageReviewTimeHours.toFixed(1)} h`
              : '—'
          }
          icon={Clock}
          loading={summaryQuery.isLoading}
        />
        <StatCard
          label="Approval rate"
          value={summary ? `${summary.approvalRatePct.toFixed(0)}%` : '—'}
          icon={CheckCircle2}
          loading={summaryQuery.isLoading}
        />
        <StatCard
          label="Analisi totali"
          value={summary ? summary.totalAnalyses : '—'}
          icon={ListChecks}
          loading={summaryQuery.isLoading}
        />
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        {/* Cost time-series */}
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle className="text-base">Costo giornaliero ({period}g)</CardTitle>
          </CardHeader>
          <CardContent>
            <MechanicCostChart data={costQuery.data ?? []} />
          </CardContent>
        </Card>

        {/* Rejection breakdown */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Motivi di rifiuto</CardTitle>
          </CardHeader>
          <CardContent>
            {summary && summary.rejectionBreakdown.length > 0 ? (
              <ul className="space-y-2" data-testid="rejection-breakdown">
                {summary.rejectionBreakdown.map(r => (
                  <li key={r.reason} className="flex items-center justify-between text-sm">
                    <span className="text-foreground">{r.reason}</span>
                    <span className="font-semibold text-muted-foreground">{r.count}</span>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-muted-foreground">Nessun rifiuto nel periodo.</p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Recent analyses table */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Analisi recenti</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Gioco</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Reviewer</TableHead>
                  <TableHead>Creata</TableHead>
                  <TableHead className="text-right">Costo</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {(recent?.items ?? []).map(row => (
                  <TableRow key={row.id}>
                    <TableCell className="font-medium">{row.gameName}</TableCell>
                    <TableCell>{STATUS_LABELS[row.status] ?? row.status}</TableCell>
                    <TableCell>{row.reviewerName ?? '—'}</TableCell>
                    <TableCell>{new Date(row.createdAt).toLocaleDateString()}</TableCell>
                    <TableCell className="text-right">${row.estimatedCostUsd.toFixed(2)}</TableCell>
                  </TableRow>
                ))}
                {(recent?.items?.length ?? 0) === 0 && (
                  <TableRow>
                    <TableCell colSpan={5} className="text-center text-sm text-muted-foreground">
                      Nessuna analisi.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
          <div className="mt-3 flex items-center justify-between">
            <span className="text-xs text-muted-foreground">
              {recent ? `${recent.totalCount} totali` : ''}
            </span>
            <div className="flex items-center gap-2">
              <Button
                size="sm"
                variant="outline"
                disabled={offset === 0}
                onClick={() => setOffset(Math.max(0, offset - RECENT_PAGE_SIZE))}
              >
                Precedente
              </Button>
              <Button
                size="sm"
                variant="outline"
                disabled={!recent || offset + RECENT_PAGE_SIZE >= recent.totalCount}
                onClick={() => setOffset(offset + RECENT_PAGE_SIZE)}
              >
                Successiva
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
