'use client';

/**
 * A/B Test Analytics Dashboard.
 * Issue #5504: A/B Testing frontend — Analytics dashboard.
 *
 * Charts: Win rate bar chart, score breakdown radar, cost-per-quality comparison.
 * Features: Date range filtering, KPI summary cards, empty state.
 */

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { BarChart3, FlaskConical, Loader2, Trophy, DollarSign, TrendingUp } from 'lucide-react';
import Link from 'next/link';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  RadarChart,
  Radar,
  PolarGrid,
  PolarAngleAxis,
  PolarRadiusAxis,
} from 'recharts';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { api } from '@/lib/api';
import type { AbTestAnalyticsDto } from '@/lib/api/schemas/ab-test.schemas';

import { AgentsNavConfig } from '../../NavConfig';

// ────── KPI Card ──────

function KpiCard({
  label,
  value,
  icon: Icon,
}: {
  label: string;
  value: string | number;
  icon: React.ElementType;
}) {
  return (
    <div className="rounded-2xl border border-slate-200/60 bg-white/70 p-4 backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-800/70">
      <div className="flex items-center gap-3">
        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-amber-100 dark:bg-amber-900/30">
          <Icon className="h-4 w-4 text-amber-600 dark:text-amber-400" />
        </div>
        <div>
          <p className="text-xs text-muted-foreground">{label}</p>
          <p className="text-lg font-bold text-foreground">{value}</p>
        </div>
      </div>
    </div>
  );
}

// ────── Win Rate Chart ──────

function WinRateChart({ data }: { data: AbTestAnalyticsDto['modelWinRates'] }) {
  const chartData = data.map(m => ({
    model: m.modelId.split('/').pop() ?? m.modelId,
    winRate: Math.round(m.winRate * 100),
    wins: m.wins,
    total: m.total,
  }));

  return (
    <div className="rounded-2xl border border-slate-200/60 bg-white/70 p-5 backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-800/70">
      <h3 className="mb-4 font-quicksand text-lg font-bold text-foreground">Win Rate by Model</h3>
      <div className="h-[300px]">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={chartData} layout="vertical">
            <CartesianGrid strokeDasharray="3 3" opacity={0.3} />
            <XAxis type="number" domain={[0, 100]} unit="%" />
            <YAxis type="category" dataKey="model" width={120} tick={{ fontSize: 12 }} />
            <Tooltip
              formatter={value => [`${value}%`, 'Win Rate']}
              contentStyle={{ borderRadius: '12px', border: '1px solid #e2e8f0' }}
            />
            <Bar dataKey="winRate" fill="#f59e0b" radius={[0, 6, 6, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

// ────── Score Radar Chart ──────

function ScoreRadarChart({ data }: { data: AbTestAnalyticsDto['modelAvgScores'] }) {
  const dimensions = ['Accuracy', 'Completeness', 'Clarity', 'Tone'];
  const radarData = dimensions.map(dim => {
    const entry: Record<string, string | number> = { dimension: dim };
    for (const model of data) {
      const shortName = model.modelId.split('/').pop() ?? model.modelId;
      const key = `avg${dim}` as keyof typeof model;
      const val = model[key];
      entry[shortName] = typeof val === 'number' ? Number(val.toFixed(2)) : 0;
    }
    return entry;
  });

  const COLORS = ['#f59e0b', '#3b82f6', '#10b981', '#8b5cf6'];

  return (
    <div className="rounded-2xl border border-slate-200/60 bg-white/70 p-5 backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-800/70">
      <h3 className="mb-4 font-quicksand text-lg font-bold text-foreground">
        Score Breakdown by Dimension
      </h3>
      <div className="h-[300px]">
        <ResponsiveContainer width="100%" height="100%">
          <RadarChart data={radarData}>
            <PolarGrid />
            <PolarAngleAxis dataKey="dimension" tick={{ fontSize: 12 }} />
            <PolarRadiusAxis domain={[0, 5]} tick={{ fontSize: 10 }} />
            {data.map((model, i) => {
              const shortName = model.modelId.split('/').pop() ?? model.modelId;
              return (
                <Radar
                  key={model.modelId}
                  name={shortName}
                  dataKey={shortName}
                  stroke={COLORS[i % COLORS.length]}
                  fill={COLORS[i % COLORS.length]}
                  fillOpacity={0.15}
                />
              );
            })}
            <Tooltip contentStyle={{ borderRadius: '12px', border: '1px solid #e2e8f0' }} />
          </RadarChart>
        </ResponsiveContainer>
      </div>
      {/* Legend */}
      <div className="mt-2 flex flex-wrap gap-3 justify-center">
        {data.map((model, i) => {
          const shortName = model.modelId.split('/').pop() ?? model.modelId;
          return (
            <div key={model.modelId} className="flex items-center gap-1.5 text-xs">
              <span
                className="h-2.5 w-2.5 rounded-full"
                style={{ backgroundColor: COLORS[i % COLORS.length] }}
              />
              {shortName}
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ────── Cost-per-Quality Table ──────

function CostQualityTable({ data }: { data: AbTestAnalyticsDto['modelAvgScores'] }) {
  return (
    <div className="rounded-2xl border border-slate-200/60 bg-white/70 p-5 backdrop-blur-md dark:border-zinc-700/40 dark:bg-zinc-800/70">
      <h3 className="mb-4 font-quicksand text-lg font-bold text-foreground">Model Leaderboard</h3>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-200 dark:border-zinc-700">
              <th className="pb-2 text-left font-medium text-muted-foreground">Model</th>
              <th className="pb-2 text-right font-medium text-muted-foreground">Avg Score</th>
              <th className="pb-2 text-right font-medium text-muted-foreground">Accuracy</th>
              <th className="pb-2 text-right font-medium text-muted-foreground">Completeness</th>
              <th className="pb-2 text-right font-medium text-muted-foreground">Clarity</th>
              <th className="pb-2 text-right font-medium text-muted-foreground">Tone</th>
              <th className="pb-2 text-right font-medium text-muted-foreground">Evaluations</th>
            </tr>
          </thead>
          <tbody>
            {[...data]
              .sort((a, b) => b.avgOverall - a.avgOverall)
              .map(model => {
                const shortName = model.modelId.split('/').pop() ?? model.modelId;
                return (
                  <tr
                    key={model.modelId}
                    className="border-b border-slate-100 dark:border-zinc-800"
                  >
                    <td className="py-2 font-medium">{shortName}</td>
                    <td className="py-2 text-right font-bold text-amber-600">
                      {model.avgOverall.toFixed(2)}
                    </td>
                    <td className="py-2 text-right">{model.avgAccuracy.toFixed(2)}</td>
                    <td className="py-2 text-right">{model.avgCompleteness.toFixed(2)}</td>
                    <td className="py-2 text-right">{model.avgClarity.toFixed(2)}</td>
                    <td className="py-2 text-right">{model.avgTone.toFixed(2)}</td>
                    <td className="py-2 text-right">{model.evaluationCount}</td>
                  </tr>
                );
              })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// ────── Empty State ──────

function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center py-20 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-amber-100 dark:bg-amber-900/30">
        <BarChart3 className="h-8 w-8 text-amber-600 dark:text-amber-400" />
      </div>
      <h3 className="mt-4 font-quicksand text-lg font-bold text-foreground">No data yet</h3>
      <p className="mt-1 text-sm text-muted-foreground">
        Run some A/B tests and evaluate them to see analytics here.
      </p>
      <Link href="/admin/agents/ab-testing/new" className="mt-4">
        <Button>
          <FlaskConical className="mr-2 h-4 w-4" />
          Create First Test
        </Button>
      </Link>
    </div>
  );
}

// ────── Main Page ──────

export default function AbTestAnalyticsPage() {
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  const isDateRangeValid = !dateFrom || !dateTo || dateFrom <= dateTo;

  const { data, isLoading } = useQuery({
    queryKey: ['abTestAnalytics', dateFrom, dateTo],
    queryFn: () =>
      api.admin.getAbTestAnalytics({
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
      }),
    enabled: isDateRangeValid,
    staleTime: 30_000,
  });

  const hasData = data && data.totalTests > 0;

  return (
    <div className="space-y-8">
      <AgentsNavConfig />

      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100 dark:bg-amber-900/30">
            <BarChart3 className="h-5 w-5 text-amber-600 dark:text-amber-400" />
          </div>
          <div>
            <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
              A/B Test Analytics
            </h1>
            <p className="text-sm text-muted-foreground">
              Model comparison results and performance trends
            </p>
          </div>
        </div>
        <Link href="/admin/agents/ab-testing/new">
          <Button variant="outline">
            <FlaskConical className="mr-2 h-4 w-4" />
            New Test
          </Button>
        </Link>
      </div>

      {/* Date Filter */}
      <div className="flex items-end gap-4">
        <div>
          <Label htmlFor="date-from" className="text-xs">
            From
          </Label>
          <Input
            id="date-from"
            type="date"
            value={dateFrom}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setDateFrom(e.target.value)}
            className="mt-1 w-40"
          />
        </div>
        <div>
          <Label htmlFor="date-to" className="text-xs">
            To
          </Label>
          <Input
            id="date-to"
            type="date"
            value={dateTo}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setDateTo(e.target.value)}
            className="mt-1 w-40"
          />
        </div>
        {(dateFrom || dateTo) && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setDateFrom('');
              setDateTo('');
            }}
          >
            Clear
          </Button>
        )}
        {!isDateRangeValid && (
          <p className="text-xs text-red-500">From date must be before To date</p>
        )}
      </div>

      {/* Loading */}
      {isLoading ? (
        <div className="flex items-center justify-center py-20">
          <Loader2 className="h-8 w-8 animate-spin text-amber-500" />
        </div>
      ) : !hasData ? (
        <EmptyState />
      ) : (
        <>
          {/* KPI Cards */}
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <KpiCard label="Total Tests" value={data.totalTests} icon={FlaskConical} />
            <KpiCard label="Completed" value={data.completedTests} icon={Trophy} />
            <KpiCard label="Total Cost" value={`$${data.totalCost.toFixed(4)}`} icon={DollarSign} />
            <KpiCard
              label="Completion Rate"
              value={`${data.totalTests > 0 ? Math.round((data.completedTests / data.totalTests) * 100) : 0}%`}
              icon={TrendingUp}
            />
          </div>

          {/* Charts Grid */}
          <div className="grid gap-6 lg:grid-cols-2">
            {data.modelWinRates.length > 0 && <WinRateChart data={data.modelWinRates} />}
            {data.modelAvgScores.length > 0 && <ScoreRadarChart data={data.modelAvgScores} />}
          </div>

          {/* Leaderboard Table */}
          {data.modelAvgScores.length > 0 && <CostQualityTable data={data.modelAvgScores} />}
        </>
      )}
    </div>
  );
}
