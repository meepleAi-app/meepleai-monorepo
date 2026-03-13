/**
 * MAU Monitoring Dashboard
 *
 * Issue #113: Monthly Active AI Users monitoring.
 * Displays KPI cards and feature breakdown for admin monitoring.
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { Activity, Bot, FileText, MessageSquare, Users } from 'lucide-react';

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { ActiveAiUsersResult } from '@/lib/api/clients/adminClient';

// ─── Types ──────────────────────────────────────────────────────────────────

type Period = 7 | 30 | 90;

// ─── Component ──────────────────────────────────────────────────────────────

export function MauDashboard() {
  const [data, setData] = useState<ActiveAiUsersResult | null>(null);
  const [period, setPeriod] = useState<Period>(30);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async (p: Period) => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await api.admin.getActiveAiUsers(p);
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load MAU data');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData(period);
  }, [fetchData, period]);

  const handlePeriodChange = useCallback((p: Period) => {
    setPeriod(p);
  }, []);

  if (error) {
    return (
      <Card className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70">
        <CardContent className="p-6">
          <p className="text-red-600">{error}</p>
          <Button className="mt-2" variant="outline" onClick={() => fetchData(period)}>
            Retry
          </Button>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-6">
      {/* Period Selector */}
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">Active AI Users</h2>
        <div className="flex gap-1 rounded-lg border p-1">
          {([7, 30, 90] as const).map(p => (
            <Button
              key={p}
              variant={period === p ? 'default' : 'ghost'}
              size="sm"
              onClick={() => handlePeriodChange(p)}
            >
              {p}d
            </Button>
          ))}
        </div>
      </div>

      {/* KPI Cards */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <KpiCard
          title="Total Active Users"
          value={data?.totalActiveUsers}
          icon={<Users className="h-5 w-5 text-orange-500" />}
          description={`Last ${period} days`}
          isLoading={isLoading}
        />
        <KpiCard
          title="AI Chat Users"
          value={data?.aiChatUsers}
          icon={<MessageSquare className="h-5 w-5 text-blue-500" />}
          description="Used AI chat"
          isLoading={isLoading}
        />
        <KpiCard
          title="PDF Upload Users"
          value={data?.pdfUploadUsers}
          icon={<FileText className="h-5 w-5 text-green-500" />}
          description="Uploaded PDFs"
          isLoading={isLoading}
        />
        <KpiCard
          title="Agent Users"
          value={data?.agentUsers}
          icon={<Bot className="h-5 w-5 text-purple-500" />}
          description="Used AI agents"
          isLoading={isLoading}
        />
      </div>

      {/* Daily Trend */}
      {data && data.dailyBreakdown.length > 0 && (
        <Card className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Activity className="h-5 w-5" />
              Daily Active Users Trend
            </CardTitle>
            <CardDescription>
              {new Date(data.periodStart).toLocaleDateString()} -{' '}
              {new Date(data.periodEnd).toLocaleDateString()}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b text-left">
                    <th className="p-2">Date</th>
                    <th className="p-2 text-right">Total</th>
                    <th className="p-2 text-right">AI Chat</th>
                    <th className="p-2 text-right">PDF Upload</th>
                  </tr>
                </thead>
                <tbody>
                  {data.dailyBreakdown.slice(-14).map(day => (
                    <tr key={day.date} className="border-b last:border-0">
                      <td className="p-2 text-zinc-600 dark:text-zinc-400">
                        {new Date(day.date).toLocaleDateString(undefined, {
                          month: 'short',
                          day: 'numeric',
                        })}
                      </td>
                      <td className="p-2 text-right font-medium">{day.activeUsers}</td>
                      <td className="p-2 text-right text-blue-600">{day.aiChatUsers}</td>
                      <td className="p-2 text-right text-green-600">{day.pdfUploadUsers}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

// ─── KPI Card ───────────────────────────────────────────────────────────────

function KpiCard({
  title,
  value,
  icon,
  description,
  isLoading,
}: {
  title: string;
  value: number | undefined;
  icon: React.ReactNode;
  description: string;
  isLoading: boolean;
}) {
  return (
    <Card className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70">
      <CardContent className="p-4">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-xs text-zinc-500">{title}</p>
            <p className="mt-1 text-2xl font-bold">
              {isLoading ? (
                <span className="inline-block h-7 w-12 animate-pulse rounded bg-zinc-200 dark:bg-zinc-700" />
              ) : (
                (value ?? 0).toLocaleString()
              )}
            </p>
            <p className="mt-0.5 text-xs text-zinc-400">{description}</p>
          </div>
          {icon}
        </div>
      </CardContent>
    </Card>
  );
}
