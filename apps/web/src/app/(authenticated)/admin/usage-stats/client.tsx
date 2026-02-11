/**
 * UsageStatsClient - App Usage Stats Dashboard (Issue #3719)
 *
 * Features:
 * - DAU/MAU engagement metrics with KPI cards
 * - Retention cohort heatmap table
 * - Session duration distribution bar chart
 * - Feature adoption funnel bar chart
 * - Geographic distribution table
 * - Period selector (7d/30d/90d)
 * - 30s polling via React Query
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
} from 'lucide-react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  Cell,
} from 'recharts';

import { KPICard } from '@/components/admin/KPICard';
import { useAppUsageStats } from '@/hooks/queries';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

type Period = '7d' | '30d' | '90d';

// ============================================================================
// Mock data for development (until backend endpoint exists)
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
    { featureName: 'AI Chat', uniqueUsers: 3_420, totalUsages: 18_750, adoptionRate: 38.3 },
    { featureName: 'PDF Upload', uniqueUsers: 2_180, totalUsages: 5_920, adoptionRate: 24.4 },
    { featureName: 'Game Search', uniqueUsers: 5_670, totalUsages: 22_340, adoptionRate: 63.5 },
    { featureName: 'Library', uniqueUsers: 4_100, totalUsages: 12_800, adoptionRate: 45.9 },
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
  generatedAt: new Date().toISOString(),
};

// ============================================================================
// Helper Components
// ============================================================================

const PERIOD_OPTIONS: { value: Period; label: string }[] = [
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' },
  { value: '90d', label: 'Last 90 days' },
];

const DURATION_COLORS = [
  '#ef4444', // 0-1 min (red - high bounce)
  '#f97316', // 1-5 min
  '#eab308', // 5-15 min
  '#22c55e', // 15-30 min (green - good)
  '#3b82f6', // 30-60 min
  '#8b5cf6', // 60+ min (purple - power users)
];

const ADOPTION_COLORS = [
  '#f97316', // orange
  '#3b82f6', // blue
  '#22c55e', // green
  '#8b5cf6', // purple
  '#ec4899', // pink
  '#06b6d4', // cyan
];

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
// Main Component
// ============================================================================

export function UsageStatsClient() {
  const [period, setPeriod] = useState<Period>('30d');

  const { data: apiData, isLoading, isError, error, isFetching, refetch } = useAppUsageStats(period);

  // Use API data if available, otherwise fall back to mock data
  const data = useMemo(() => apiData ?? MOCK_DATA, [apiData]);

  const engagement = data.engagement;
  const retentionCohorts = data.retentionCohorts;
  const featureAdoption = data.featureAdoption;
  const geoDistribution = data.geoDistribution;
  const sessionDuration = data.sessionDurationDistribution;

  return (
    <div className="space-y-6" data-testid="usage-stats-page">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">App Usage Stats</h1>
          <p className="text-sm text-muted-foreground">
            DAU/MAU metrics, retention, feature adoption, and geographic distribution
          </p>
        </div>
        <div className="flex items-center gap-3">
          {/* Period selector */}
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
          {/* Refresh */}
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

      {/* Loading overlay */}
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

          {/* Charts Row */}
          <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
            {/* Session Duration Distribution */}
            <div className="rounded-xl border border-border/50 bg-card/90 p-5 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none" data-testid="session-duration-chart">
              <h2 className="mb-4 text-lg font-semibold">Session Duration Distribution</h2>
              <div className="h-[300px]">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={sessionDuration} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" className="stroke-border/30" />
                    <XAxis dataKey="label" tick={{ fontSize: 12 }} className="fill-muted-foreground" />
                    <YAxis tick={{ fontSize: 12 }} className="fill-muted-foreground" />
                    <Tooltip
                      contentStyle={{
                        backgroundColor: 'hsl(var(--card))',
                        border: '1px solid hsl(var(--border))',
                        borderRadius: '8px',
                      }}
                      formatter={(value: number, _name: string, props: { payload?: { percentage?: number } }) => [
                        `${value.toLocaleString()} sessions (${props.payload?.percentage ?? 0}%)`,
                        'Count',
                      ]}
                    />
                    <Bar dataKey="count" radius={[4, 4, 0, 0]}>
                      {sessionDuration.map((_entry, index) => (
                        <Cell key={`cell-${index}`} fill={DURATION_COLORS[index % DURATION_COLORS.length]} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>

            {/* Feature Adoption */}
            <div className="rounded-xl border border-border/50 bg-card/90 p-5 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none" data-testid="feature-adoption-chart">
              <h2 className="mb-4 text-lg font-semibold">Feature Adoption Rate</h2>
              <div className="h-[300px]">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={featureAdoption} layout="vertical" margin={{ top: 5, right: 20, left: 80, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" className="stroke-border/30" />
                    <XAxis type="number" domain={[0, 100]} tick={{ fontSize: 12 }} unit="%" className="fill-muted-foreground" />
                    <YAxis dataKey="featureName" type="category" tick={{ fontSize: 12 }} className="fill-muted-foreground" width={75} />
                    <Tooltip
                      contentStyle={{
                        backgroundColor: 'hsl(var(--card))',
                        border: '1px solid hsl(var(--border))',
                        borderRadius: '8px',
                      }}
                      formatter={(value: number, _name: string, props: { payload?: { uniqueUsers?: number; totalUsages?: number } }) => [
                        `${value.toFixed(1)}% (${(props.payload?.uniqueUsers ?? 0).toLocaleString()} users, ${(props.payload?.totalUsages ?? 0).toLocaleString()} uses)`,
                        'Adoption',
                      ]}
                    />
                    <Bar dataKey="adoptionRate" radius={[0, 4, 4, 0]}>
                      {featureAdoption.map((_entry, index) => (
                        <Cell key={`cell-${index}`} fill={ADOPTION_COLORS[index % ADOPTION_COLORS.length]} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>
          </div>

          {/* Retention Cohort Table */}
          <div className="rounded-xl border border-border/50 bg-card/90 p-5 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none" data-testid="retention-cohort-table">
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
                  {retentionCohorts.map((cohort) => (
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
          </div>

          {/* Geo Distribution Table */}
          <div className="rounded-xl border border-border/50 bg-card/90 p-5 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none" data-testid="geo-distribution-table">
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
                  {geoDistribution.map((geo) => (
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
          </div>

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
