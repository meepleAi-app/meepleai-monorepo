/**
 * UsageStatsClient - App Usage Dashboard Frontend (Issue #3728)
 *
 * Enhanced dashboard with:
 * - DAU/MAU trend line chart (time series)
 * - Peak hours heatmap (hour x day grid)
 * - Feature adoption funnel (progressive width)
 * - Retention cohort heatmap (color-coded table)
 * - Session duration distribution bar chart
 * - Geographic distribution table
 * - KPI cards, period selector, 30s polling
 */

'use client';

import { useState, useMemo } from 'react';

import {
  Activity,
  Users,
  Clock,
  TrendingUp,
  Globe,
  Layers,
  BarChart3,
  RefreshCw,
  Flame,
} from 'lucide-react';
import {
  BarChart,
  Bar,
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell,
  Legend,
} from 'recharts';

import { KPICard } from '@/components/admin/KPICard';
import { useAppUsageStats } from '@/hooks/queries';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

type Period = '7d' | '30d' | '90d';

interface DauMauTrendPoint {
  date: string;
  dau: number;
  mau: number;
}

interface PeakHourEntry {
  hour: number;
  dayOfWeek: number;
  value: number;
}

// ============================================================================
// Static mock data helpers (deterministic to avoid SSR hydration mismatches)
// ============================================================================

function buildDauMauTrend(): DauMauTrendPoint[] {
  // Static seed-like pattern: use index-based deterministic values instead of Math.random()
  return Array.from({ length: 30 }, (_, i) => {
    const base = 900 + ((i * 37 + 13) % 600);
    return {
      date: `2026-01-${String(i + 1).padStart(2, '0')}`,
      dau: base + ((i * 53 + 7) % 200),
      mau: 7500 + ((i * 89 + 23) % 3000),
    };
  });
}

function buildPeakHours(): PeakHourEntry[] {
  const entries: PeakHourEntry[] = [];
  for (let day = 0; day < 7; day++) {
    for (let hour = 0; hour < 24; hour++) {
      let base = 10;
      if (hour >= 9 && hour <= 22) base = 40;
      if (hour >= 18 && hour <= 21) base = 70;
      if (day >= 5) base = Math.floor(base * 0.6);
      // Deterministic jitter based on position
      const jitter = ((day * 24 + hour) * 41 + 17) % 30;
      entries.push({ hour, dayOfWeek: day, value: Math.min(100, Math.max(0, base + jitter)) });
    }
  }
  return entries;
}

// ============================================================================
// Mock data
// ============================================================================

const MOCK_DATA = {
  engagement: {
    dau: 1_247,
    mau: 8_932,
    dauMauRatio: 0.14,
    avgSessionDurationMinutes: 12.4,
    totalSessions: 34_521,
    bounceRate: 0.23,
  },
  retentionCohorts: [
    { cohortDate: '2026-01-13', cohortSize: 450, day1: 72, day7: 45, day14: 32, day30: 21 },
    { cohortDate: '2026-01-20', cohortSize: 520, day1: 68, day7: 41, day14: 29, day30: 18 },
    { cohortDate: '2026-01-27', cohortSize: 480, day1: 75, day7: 48, day14: 35, day30: 24 },
    { cohortDate: '2026-02-03', cohortSize: 510, day1: 71, day7: 44, day14: 31, day30: 0 },
  ],
  featureAdoption: [
    { featureName: 'Game Search', uniqueUsers: 5_670, totalUsages: 22_340, adoptionRate: 63.5 },
    { featureName: 'Library', uniqueUsers: 4_100, totalUsages: 12_800, adoptionRate: 45.9 },
    { featureName: 'AI Chat', uniqueUsers: 3_420, totalUsages: 18_750, adoptionRate: 38.3 },
    { featureName: 'PDF Upload', uniqueUsers: 2_180, totalUsages: 5_920, adoptionRate: 24.4 },
    { featureName: 'Wishlist', uniqueUsers: 1_890, totalUsages: 4_230, adoptionRate: 21.2 },
    { featureName: 'Community', uniqueUsers: 980, totalUsages: 2_450, adoptionRate: 11.0 },
  ],
  geoDistribution: [
    { country: 'United States', countryCode: 'US', users: 3_210, percentage: 35.9 },
    { country: 'Germany', countryCode: 'DE', users: 1_450, percentage: 16.2 },
    { country: 'United Kingdom', countryCode: 'GB', users: 980, percentage: 11.0 },
    { country: 'France', countryCode: 'FR', users: 720, percentage: 8.1 },
    { country: 'Italy', countryCode: 'IT', users: 650, percentage: 7.3 },
    { country: 'Canada', countryCode: 'CA', users: 480, percentage: 5.4 },
    { country: 'Spain', countryCode: 'ES', users: 390, percentage: 4.4 },
    { country: 'Other', countryCode: 'XX', users: 1_052, percentage: 11.8 },
  ],
  sessionDurationDistribution: [
    { label: '0-1 min', count: 4_521, percentage: 13.1 },
    { label: '1-5 min', count: 8_932, percentage: 25.9 },
    { label: '5-15 min', count: 11_240, percentage: 32.6 },
    { label: '15-30 min', count: 6_180, percentage: 17.9 },
    { label: '30-60 min', count: 2_450, percentage: 7.1 },
    { label: '60+ min', count: 1_198, percentage: 3.5 },
  ],
  dauMauTrend: buildDauMauTrend(),
  peakHours: buildPeakHours(),
  generatedAt: new Date().toISOString(),
};

// ============================================================================
// Constants
// ============================================================================

const PERIOD_OPTIONS: { value: Period; label: string }[] = [
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
  { value: '90d', label: 'Last 90 days' },
];

const DURATION_COLORS = [
  '#ef4444', '#f97316', '#eab308', '#22c55e', '#3b82f6', '#8b5cf6',
];

const FUNNEL_COLORS = [
  '#d2691e', '#e07a2f', '#ee8c40', '#f59e50', '#fcb060', '#ffc270',
];

const DAY_LABELS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

const TOOLTIP_STYLE = {
  backgroundColor: 'hsl(var(--card))',
  border: '1px solid hsl(var(--border))',
  borderRadius: '8px',
};

// ============================================================================
// Helper Components
// ============================================================================

function CardWrapper({
  children,
  className,
  testId,
}: {
  children: React.ReactNode;
  className?: string;
  testId?: string;
}) {
  return (
    <div
      className={cn(
        'rounded-xl border border-border/50 bg-card/90 p-5 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none',
        className
      )}
      data-testid={testId}
    >
      {children}
    </div>
  );
}

function RetentionHeatCell({ value }: { value: number }) {
  const intensity = Math.min(value / 100, 1);
  const bg =
    value === 0
      ? 'bg-muted/30 text-muted-foreground/50'
      : intensity > 0.6
        ? 'bg-green-500/80 text-white'
        : intensity > 0.3
          ? 'bg-green-500/40 text-green-900 dark:text-green-100'
          : 'bg-green-500/15 text-green-800 dark:text-green-200';

  return (
    <td className={cn('px-3 py-2 text-center text-sm font-medium rounded-sm', bg)}>
      {value === 0 ? '-' : `${value}%`}
    </td>
  );
}

// ============================================================================
// DAU/MAU Trend Chart
// ============================================================================

function DauMauTrendChart({ data }: { data: DauMauTrendPoint[] }) {
  const formatted = useMemo(
    () =>
      data.map((p) => ({
        ...p,
        label: new Date(p.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
      })),
    [data]
  );

  return (
    <CardWrapper testId="dau-mau-trend-chart">
      <div className="mb-4 flex items-center gap-2">
        <TrendingUp className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
        <h2 className="text-lg font-semibold">DAU / MAU Trend</h2>
      </div>
      <div className="h-[320px]">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={formatted} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
            <CartesianGrid strokeDasharray="3 3" className="stroke-border/30" />
            <XAxis
              dataKey="label"
              tick={{ fontSize: 11 }}
              className="fill-muted-foreground"
              interval={Math.max(0, Math.floor(formatted.length / 8) - 1)}
            />
            <YAxis tick={{ fontSize: 12 }} className="fill-muted-foreground" />
            <Tooltip contentStyle={TOOLTIP_STYLE} />
            <Legend />
            <Line
              type="monotone"
              dataKey="dau"
              stroke="#d2691e"
              strokeWidth={2}
              dot={false}
              name="DAU"
              activeDot={{ r: 5 }}
            />
            <Line
              type="monotone"
              dataKey="mau"
              stroke="#8b5cf6"
              strokeWidth={2}
              dot={false}
              name="MAU"
              activeDot={{ r: 5 }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </CardWrapper>
  );
}

// ============================================================================
// Peak Hours Heatmap
// ============================================================================

function PeakHoursHeatmap({ data }: { data: PeakHourEntry[] }) {
  const grid = useMemo(() => {
    const matrix: number[][] = Array.from({ length: 7 }, () => Array(24).fill(0));
    for (const entry of data) {
      if (entry.dayOfWeek >= 0 && entry.dayOfWeek < 7 && entry.hour >= 0 && entry.hour < 24) {
        matrix[entry.dayOfWeek][entry.hour] = entry.value;
      }
    }
    return matrix;
  }, [data]);

  const maxValue = useMemo(() => Math.max(1, ...data.map((e) => e.value)), [data]);

  function getCellColor(value: number): string {
    const ratio = value / maxValue;
    if (ratio < 0.2) return 'bg-orange-500/10';
    if (ratio < 0.4) return 'bg-orange-500/25';
    if (ratio < 0.6) return 'bg-orange-500/45';
    if (ratio < 0.8) return 'bg-orange-500/65';
    return 'bg-orange-500/90';
  }

  return (
    <CardWrapper testId="peak-hours-heatmap">
      <div className="mb-4 flex items-center gap-2">
        <Flame className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
        <h2 className="text-lg font-semibold">Peak Activity Hours</h2>
      </div>
      <div className="overflow-x-auto">
        <div className="min-w-[600px]">
          {/* Hour labels */}
          <div className="flex mb-1">
            <div className="w-10 shrink-0" />
            {Array.from({ length: 24 }, (_, h) => (
              <div
                key={h}
                className="flex-1 text-center text-[10px] text-muted-foreground"
              >
                {h % 3 === 0 ? `${h}:00` : ''}
              </div>
            ))}
          </div>
          {/* Rows */}
          {grid.map((row, dayIndex) => (
            <div key={dayIndex} className="flex items-center mb-0.5" data-testid={`heatmap-row-${dayIndex}`}>
              <div className="w-10 shrink-0 text-xs font-medium text-muted-foreground pr-1 text-right">
                {DAY_LABELS[dayIndex]}
              </div>
              {row.map((value, hourIndex) => (
                <div
                  key={hourIndex}
                  className={cn(
                    'flex-1 aspect-square rounded-[2px] mx-px transition-colors',
                    getCellColor(value)
                  )}
                  title={`${DAY_LABELS[dayIndex]} ${hourIndex}:00 - ${value} sessions`}
                  role="gridcell"
                  aria-label={`${DAY_LABELS[dayIndex]} ${hourIndex}:00: ${value} sessions`}
                />
              ))}
            </div>
          ))}
          {/* Legend */}
          <div className="flex items-center justify-end gap-1 mt-3 text-xs text-muted-foreground">
            <span>Low</span>
            <div className="flex gap-px">
              {['bg-orange-500/10', 'bg-orange-500/25', 'bg-orange-500/45', 'bg-orange-500/65', 'bg-orange-500/90'].map(
                (cls, i) => (
                  <div key={i} className={cn('w-4 h-3 rounded-[2px]', cls)} />
                )
              )}
            </div>
            <span>High</span>
          </div>
        </div>
      </div>
    </CardWrapper>
  );
}

// ============================================================================
// Feature Adoption Funnel
// ============================================================================

function FeatureAdoptionFunnel({
  data,
}: {
  data: Array<{ featureName: string; uniqueUsers: number; totalUsages: number; adoptionRate: number }>;
}) {
  const sorted = useMemo(() => [...data].sort((a, b) => b.adoptionRate - a.adoptionRate), [data]);
  const maxRate = sorted.length > 0 ? sorted[0].adoptionRate : 100;

  return (
    <CardWrapper testId="feature-adoption-funnel">
      <h2 className="mb-4 text-lg font-semibold">Feature Adoption Funnel</h2>
      <div className="space-y-2">
        {sorted.map((item, index) => {
          const widthPercent = maxRate > 0 ? (item.adoptionRate / maxRate) * 100 : 0;
          return (
            <div key={item.featureName} className="flex items-center gap-3" data-testid={`funnel-item-${index}`}>
              <span className="w-24 shrink-0 text-sm font-medium text-right truncate" title={item.featureName}>
                {item.featureName}
              </span>
              <div className="flex-1 relative">
                <div
                  className="h-8 rounded-md flex items-center px-3 transition-all"
                  style={{
                    width: `${Math.max(widthPercent, 8)}%`,
                    backgroundColor: FUNNEL_COLORS[index % FUNNEL_COLORS.length],
                  }}
                >
                  <span className="text-xs font-bold text-white whitespace-nowrap">
                    {item.adoptionRate.toFixed(1)}%
                  </span>
                </div>
              </div>
              <span className="w-20 shrink-0 text-xs text-muted-foreground text-right">
                {item.uniqueUsers.toLocaleString()} users
              </span>
            </div>
          );
        })}
      </div>
    </CardWrapper>
  );
}

// ============================================================================
// Session Duration Chart
// ============================================================================

function SessionDurationChart({
  data,
}: {
  data: Array<{ label: string; count: number; percentage: number }>;
}) {
  return (
    <CardWrapper testId="session-duration-chart">
      <h2 className="mb-4 text-lg font-semibold">Session Duration Distribution</h2>
      <div className="h-[300px]">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={data} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
            <CartesianGrid strokeDasharray="3 3" className="stroke-border/30" />
            <XAxis dataKey="label" tick={{ fontSize: 12 }} className="fill-muted-foreground" />
            <YAxis tick={{ fontSize: 12 }} className="fill-muted-foreground" />
            <Tooltip
              contentStyle={TOOLTIP_STYLE}
              formatter={(value: number, _name: string, props: { payload?: { percentage?: number } }) => [
                `${value.toLocaleString()} sessions (${props.payload?.percentage ?? 0}%)`,
                'Count',
              ]}
            />
            <Bar dataKey="count" radius={[4, 4, 0, 0]}>
              {data.map((_entry, index) => (
                <Cell key={`cell-${index}`} fill={DURATION_COLORS[index % DURATION_COLORS.length]} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </CardWrapper>
  );
}

// ============================================================================
// Retention Cohort Table
// ============================================================================

function RetentionCohortTable({
  data,
}: {
  data: Array<{ cohortDate: string; cohortSize: number; day1: number; day7: number; day14: number; day30: number }>;
}) {
  return (
    <CardWrapper testId="retention-cohort-table">
      <h2 className="mb-4 text-lg font-semibold">Retention Cohorts</h2>
      <div className="overflow-x-auto">
        <table className="w-full text-sm" role="table">
          <thead>
            <tr className="border-b border-border/50">
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Cohort</th>
              <th className="px-3 py-2 text-center font-medium text-muted-foreground">Size</th>
              <th className="px-3 py-2 text-center font-medium text-muted-foreground">Day 1</th>
              <th className="px-3 py-2 text-center font-medium text-muted-foreground">Day 7</th>
              <th className="px-3 py-2 text-center font-medium text-muted-foreground">Day 14</th>
              <th className="px-3 py-2 text-center font-medium text-muted-foreground">Day 30</th>
            </tr>
          </thead>
          <tbody>
            {data.map((cohort) => (
              <tr key={cohort.cohortDate} className="border-b border-border/20">
                <td className="px-3 py-2 font-medium">
                  {new Date(cohort.cohortDate).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}
                </td>
                <td className="px-3 py-2 text-center text-muted-foreground">{cohort.cohortSize}</td>
                <RetentionHeatCell value={cohort.day1} />
                <RetentionHeatCell value={cohort.day7} />
                <RetentionHeatCell value={cohort.day14} />
                <RetentionHeatCell value={cohort.day30} />
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </CardWrapper>
  );
}

// ============================================================================
// Geo Distribution Table
// ============================================================================

function GeoDistributionTable({
  data,
}: {
  data: Array<{ country: string; countryCode: string; users: number; percentage: number }>;
}) {
  return (
    <CardWrapper testId="geo-distribution-table">
      <div className="mb-4 flex items-center gap-2">
        <Globe className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
        <h2 className="text-lg font-semibold">Geographic Distribution</h2>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm" role="table">
          <thead>
            <tr className="border-b border-border/50">
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">Country</th>
              <th className="px-3 py-2 text-right font-medium text-muted-foreground">Users</th>
              <th className="px-3 py-2 text-right font-medium text-muted-foreground">Share</th>
              <th className="px-3 py-2 text-left font-medium text-muted-foreground" style={{ width: '40%' }}>
                <span className="sr-only">Distribution bar</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {data.map((geo) => (
              <tr key={geo.countryCode} className="border-b border-border/20">
                <td className="px-3 py-2 font-medium">{geo.country}</td>
                <td className="px-3 py-2 text-right text-muted-foreground">{geo.users.toLocaleString()}</td>
                <td className="px-3 py-2 text-right">{geo.percentage.toFixed(1)}%</td>
                <td className="px-3 py-2">
                  <div className="h-2 w-full rounded-full bg-muted/30">
                    <div
                      className="h-2 rounded-full bg-orange-500/70"
                      style={{ width: `${geo.percentage}%` }}
                      role="progressbar"
                      aria-valuenow={geo.percentage}
                      aria-valuemin={0}
                      aria-valuemax={100}
                      aria-label={`${geo.country}: ${geo.percentage}%`}
                    />
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </CardWrapper>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function UsageStatsClient() {
  const [period, setPeriod] = useState<Period>('30d');

  const { data: apiData, isLoading, isError, error, isFetching, refetch } = useAppUsageStats(period);

  const data = useMemo(() => apiData ?? MOCK_DATA, [apiData]);

  const engagement = data.engagement;
  const dauMauTrend = data.dauMauTrend ?? MOCK_DATA.dauMauTrend;
  const peakHours = data.peakHours ?? MOCK_DATA.peakHours;

  return (
    <div className="space-y-6" data-testid="usage-stats-page">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">App Usage Dashboard</h1>
          <p className="text-sm text-muted-foreground">
            DAU/MAU trends, peak hours, retention cohorts, feature adoption, and geographic distribution
          </p>
        </div>
        <div className="flex items-center gap-3">
          <div className="flex rounded-lg border border-border/50 bg-muted/30 p-0.5" role="radiogroup" aria-label="Time period">
            {PERIOD_OPTIONS.map((opt) => (
              <button
                key={opt.value}
                role="radio"
                aria-checked={period === opt.value}
                onClick={() => setPeriod(opt.value)}
                className={cn(
                  'rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
                  period === opt.value
                    ? 'bg-background text-foreground shadow-sm'
                    : 'text-muted-foreground hover:text-foreground'
                )}
              >
                {opt.label}
              </button>
            ))}
          </div>
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            className="flex items-center gap-1.5 rounded-lg border border-border/50 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors disabled:opacity-50"
            aria-label="Refresh data"
          >
            <RefreshCw className={cn('h-4 w-4', isFetching && 'animate-spin')} aria-hidden="true" />
            Refresh
          </button>
        </div>
      </div>

      {/* Error state */}
      {isError && (
        <div role="alert" className="rounded-lg border border-destructive/50 bg-destructive/10 p-4 text-sm text-destructive">
          Failed to load usage stats: {error?.message ?? 'Unknown error'}
        </div>
      )}

      {/* Loading */}
      {isLoading && (
        <div className="flex items-center justify-center py-20" data-testid="usage-stats-loading">
          <div className="flex flex-col items-center gap-3 text-muted-foreground">
            <RefreshCw className="h-8 w-8 animate-spin" aria-hidden="true" />
            <p className="text-sm">Loading usage stats...</p>
          </div>
        </div>
      )}

      {/* Content */}
      {!isLoading && (
        <>
          {/* KPI Cards */}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6" data-testid="usage-stats-kpis">
            <KPICard
              title="Daily Active Users"
              value={engagement.dau.toLocaleString()}
              icon={<Users className="h-5 w-5" />}
              data-testid="kpi-dau"
            />
            <KPICard
              title="Monthly Active Users"
              value={engagement.mau.toLocaleString()}
              icon={<Activity className="h-5 w-5" />}
              data-testid="kpi-mau"
            />
            <KPICard
              title="DAU/MAU Ratio"
              value={`${(engagement.dauMauRatio * 100).toFixed(1)}%`}
              icon={<TrendingUp className="h-5 w-5" />}
              subtitle="Stickiness"
              data-testid="kpi-dau-mau-ratio"
            />
            <KPICard
              title="Avg Session Duration"
              value={`${engagement.avgSessionDurationMinutes.toFixed(1)}m`}
              icon={<Clock className="h-5 w-5" />}
              data-testid="kpi-avg-session"
            />
            <KPICard
              title="Total Sessions"
              value={engagement.totalSessions.toLocaleString()}
              icon={<BarChart3 className="h-5 w-5" />}
              data-testid="kpi-total-sessions"
            />
            <KPICard
              title="Bounce Rate"
              value={`${(engagement.bounceRate * 100).toFixed(1)}%`}
              icon={<Layers className="h-5 w-5" />}
              badge={engagement.bounceRate > 0.3 ? 'High' : undefined}
              badgeVariant={engagement.bounceRate > 0.3 ? 'warning' : undefined}
              data-testid="kpi-bounce-rate"
            />
          </div>

          {/* DAU/MAU Trend (full width) */}
          <DauMauTrendChart data={dauMauTrend} />

          {/* Peak Hours + Session Duration */}
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <PeakHoursHeatmap data={peakHours} />
            <SessionDurationChart data={data.sessionDurationDistribution} />
          </div>

          {/* Feature Adoption Funnel + Retention Cohort */}
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            <FeatureAdoptionFunnel data={data.featureAdoption} />
            <RetentionCohortTable data={data.retentionCohorts} />
          </div>

          {/* Geo Distribution */}
          <GeoDistributionTable data={data.geoDistribution} />

          {/* Footer */}
          <p className="text-xs text-muted-foreground text-right" data-testid="usage-stats-generated-at">
            Data generated at: {new Date(data.generatedAt).toLocaleString()}
          </p>
        </>
      )}
    </div>
  );
}

export default UsageStatsClient;
