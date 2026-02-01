/* eslint-disable security/detect-object-injection -- Safe Record access with provider/role name keys */
/**
 * AiUsageDashboardClient - Issue #3080
 *
 * Admin dashboard for AI usage analytics.
 * Displays cost breakdown, top users, time series, and export functionality.
 *
 * Backend endpoints:
 * - GET /llm-costs/report - Cost report with date range
 * - GET /admin/analytics/ai-usage - AI usage stats by model
 */

'use client';

import { useCallback, useState, useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Download, RefreshCw, Calendar, DollarSign, Users, TrendingUp, AlertTriangle } from 'lucide-react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Legend,
} from 'recharts';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { LoadingButton } from '@/components/loading/LoadingButton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';

// Types
type LlmCostReport = {
  startDate: string;
  endDate: string;
  totalCost: number;
  costsByProvider: Record<string, number>;
  costsByRole: Record<string, number>;
  dailyCost: number;
  exceedsThreshold: boolean;
  thresholdAmount: number;
};

type AiUsageStat = {
  category: string;
  count: number;
};

// Colors from MeepleAI design system
const PROVIDER_COLORS: Record<string, string> = {
  'OpenRouter': '#d2691e',
  'Ollama': '#8b5cf6',
  'Default': '#666666',
};

const ROLE_COLORS: Record<string, string> = {
  'Admin': '#d2691e',
  'Editor': '#8b5cf6',
  'User': '#16a34a',
  'Anonymous': '#666666',
};

function StatCard({
  icon: Icon,
  label,
  value,
  subValue,
  alert,
}: {
  icon: React.ElementType;
  label: string;
  value: string;
  subValue?: string;
  alert?: boolean;
}) {
  return (
    <div className={`rounded-xl border p-6 shadow-sm ${alert ? 'border-destructive/50 bg-destructive/5' : 'bg-card'}`}>
      <div className="flex items-center gap-3">
        <div className={`flex h-12 w-12 items-center justify-center rounded-full ${alert ? 'bg-destructive/10' : 'bg-primary/10'}`}>
          <Icon className={`h-6 w-6 ${alert ? 'text-destructive' : 'text-primary'}`} />
        </div>
        <div>
          <p className="text-sm text-muted-foreground">{label}</p>
          <p className="text-2xl font-bold">{value}</p>
          {subValue && <p className="text-xs text-muted-foreground">{subValue}</p>}
        </div>
      </div>
    </div>
  );
}

export function AiUsageDashboardClient() {
  const { user, loading: authLoading } = useAuthUser();
  const [days, setDays] = useState('30');
  const [isExporting, setIsExporting] = useState(false);

  // Fetch cost report
  const {
    data: costReport,
    isLoading: costLoading,
    refetch: refetchCost,
  } = useQuery<LlmCostReport>({
    queryKey: ['llm-cost-report', days],
    queryFn: async () => {
      const endDate = new Date().toISOString().split('T')[0];
      const startDate = new Date(Date.now() - parseInt(days) * 24 * 60 * 60 * 1000)
        .toISOString()
        .split('T')[0];
      const response = await fetch(`/api/v1/llm-costs/report?startDate=${startDate}&endDate=${endDate}`, {
        credentials: 'include',
      });
      if (!response.ok) {
        throw new Error('Failed to fetch cost report');
      }
      return response.json();
    },
    staleTime: 5 * 60 * 1000,
    enabled: !!user,
  });

  // Fetch AI usage stats
  const {
    data: usageStats,
    isLoading: usageLoading,
    refetch: refetchUsage,
  } = useQuery<AiUsageStat[]>({
    queryKey: ['ai-usage-stats'],
    queryFn: async () => {
      const response = await fetch('/api/v1/admin/analytics/ai-usage', {
        credentials: 'include',
      });
      if (!response.ok) {
        throw new Error('Failed to fetch usage stats');
      }
      return response.json();
    },
    staleTime: 5 * 60 * 1000,
    enabled: !!user,
  });

  const isLoading = costLoading || usageLoading;

  const handleRefresh = useCallback(() => {
    refetchCost();
    refetchUsage();
  }, [refetchCost, refetchUsage]);

  const handleExport = useCallback(async () => {
    setIsExporting(true);
    try {
      const response = await fetch('/api/v1/admin/analytics/export', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({
          format: 'csv',
          fromDate: costReport?.startDate,
          toDate: costReport?.endDate,
        }),
      });

      if (!response.ok) {
        throw new Error('Export failed');
      }

      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `ai-usage-${new Date().toISOString().split('T')[0]}.csv`;
      a.click();
      window.URL.revokeObjectURL(url);
    } catch (error) {
      console.error('Export failed:', error);
    } finally {
      setIsExporting(false);
    }
  }, [costReport]);

  // Transform data for charts
  const providerChartData = useMemo(() => {
    if (!costReport?.costsByProvider) return [];
    return Object.entries(costReport.costsByProvider).map(([name, value]) => ({
      name,
      value,
      color: PROVIDER_COLORS[name] ?? PROVIDER_COLORS['Default'],
    }));
  }, [costReport]);

  const roleChartData = useMemo(() => {
    if (!costReport?.costsByRole) return [];
    return Object.entries(costReport.costsByRole).map(([name, value]) => ({
      name,
      value,
      color: ROLE_COLORS[name] ?? ROLE_COLORS['Anonymous'],
    }));
  }, [costReport]);

  const formatCost = (cost: number) => {
    if (cost < 0.01) return '< $0.01';
    return `$${cost.toFixed(2)}`;
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="min-h-dvh bg-background p-8">
        <div className="mx-auto max-w-7xl space-y-6">
          {/* Header */}
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h1 className="font-['Quicksand',sans-serif] text-2xl font-bold">
                AI Usage Dashboard
              </h1>
              <p className="text-muted-foreground">
                Monitor AI costs, usage patterns, and resource allocation
              </p>
            </div>

            <div className="flex items-center gap-3">
              <Select value={days} onValueChange={setDays}>
                <SelectTrigger className="w-[140px]">
                  <Calendar className="mr-2 h-4 w-4" />
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="7">Last 7 days</SelectItem>
                  <SelectItem value="30">Last 30 days</SelectItem>
                  <SelectItem value="90">Last 90 days</SelectItem>
                  <SelectItem value="365">Last year</SelectItem>
                </SelectContent>
              </Select>

              <LoadingButton
                onClick={handleRefresh}
                isLoading={isLoading}
                variant="outline"
              >
                <RefreshCw className={`mr-2 h-4 w-4 ${isLoading ? 'animate-spin' : ''}`} />
                Refresh
              </LoadingButton>

              <LoadingButton
                onClick={handleExport}
                isLoading={isExporting}
                disabled={isLoading}
              >
                <Download className="mr-2 h-4 w-4" />
                Export CSV
              </LoadingButton>
            </div>
          </div>

          {/* Alert Banner */}
          {costReport?.exceedsThreshold && (
            <div className="flex items-center gap-3 rounded-lg border border-destructive/50 bg-destructive/10 p-4">
              <AlertTriangle className="h-5 w-5 text-destructive" />
              <div>
                <p className="font-medium text-destructive">Budget Alert</p>
                <p className="text-sm text-muted-foreground">
                  Daily cost ({formatCost(costReport.dailyCost)}) exceeds threshold ({formatCost(costReport.thresholdAmount)})
                </p>
              </div>
            </div>
          )}

          {/* Stats Cards */}
          <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
            <StatCard
              icon={DollarSign}
              label="Total Cost"
              value={costReport ? formatCost(costReport.totalCost) : '--'}
              subValue={`Last ${days} days`}
            />
            <StatCard
              icon={TrendingUp}
              label="Daily Average"
              value={costReport ? formatCost(costReport.totalCost / parseInt(days)) : '--'}
            />
            <StatCard
              icon={Users}
              label="Providers"
              value={costReport ? Object.keys(costReport.costsByProvider).length.toString() : '--'}
            />
            <StatCard
              icon={AlertTriangle}
              label="Daily Cost"
              value={costReport ? formatCost(costReport.dailyCost) : '--'}
              subValue={costReport?.exceedsThreshold ? 'Over budget!' : 'Within budget'}
              alert={costReport?.exceedsThreshold}
            />
          </div>

          {/* Charts */}
          <div className="grid gap-6 lg:grid-cols-2">
            {/* Cost by Provider */}
            <div className="rounded-xl border bg-card p-6 shadow-sm">
              <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold">
                Cost by Provider
              </h3>
              {providerChartData.length > 0 ? (
                <ResponsiveContainer width="100%" height={300}>
                  <PieChart>
                    <Pie
                      data={providerChartData}
                      cx="50%"
                      cy="50%"
                      innerRadius={60}
                      outerRadius={100}
                      dataKey="value"
                      nameKey="name"
                      label={({ name, value }) => `${name}: ${formatCost(value)}`}
                    >
                      {providerChartData.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Pie>
                    <Tooltip formatter={(value: number) => formatCost(value)} />
                    <Legend />
                  </PieChart>
                </ResponsiveContainer>
              ) : (
                <div className="flex h-[300px] items-center justify-center text-muted-foreground">
                  No data available
                </div>
              )}
            </div>

            {/* Cost by Role */}
            <div className="rounded-xl border bg-card p-6 shadow-sm">
              <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold">
                Cost by User Role
              </h3>
              {roleChartData.length > 0 ? (
                <ResponsiveContainer width="100%" height={300}>
                  <BarChart data={roleChartData}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="name" />
                    <YAxis tickFormatter={(value) => formatCost(value)} />
                    <Tooltip formatter={(value: number) => formatCost(value)} />
                    <Bar dataKey="value" fill="#d2691e" radius={[4, 4, 0, 0]}>
                      {roleChartData.map((entry, index) => (
                        <Cell key={`cell-${index}`} fill={entry.color} />
                      ))}
                    </Bar>
                  </BarChart>
                </ResponsiveContainer>
              ) : (
                <div className="flex h-[300px] items-center justify-center text-muted-foreground">
                  No data available
                </div>
              )}
            </div>
          </div>

          {/* AI Usage by Category */}
          {usageStats && usageStats.length > 0 && (
            <div className="rounded-xl border bg-card p-6 shadow-sm">
              <h3 className="mb-4 font-['Quicksand',sans-serif] text-lg font-bold">
                AI Usage by Category
              </h3>
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={usageStats} layout="vertical">
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis type="number" />
                  <YAxis dataKey="category" type="category" width={100} />
                  <Tooltip formatter={(value: number) => value.toLocaleString()} />
                  <Bar dataKey="count" fill="#8b5cf6" radius={[0, 4, 4, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}

          {/* Period Info */}
          {costReport && (
            <div className="text-center text-sm text-muted-foreground">
              Data from {costReport.startDate} to {costReport.endDate}
            </div>
          )}
        </div>
      </div>
    </AdminAuthGuard>
  );
}
