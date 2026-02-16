'use client';

/**
 * Chat Analytics Tab - Admin Dashboard
 * Issue #3714: System-wide chat monitoring with real API data
 */

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';

import { Card } from '@/components/ui/data-display/card';
import { api } from '@/lib/api';
import type { ChatAnalyticsDto } from '@/lib/api/schemas';

const TIME_RANGES = [
  { label: '7 days', value: 7 },
  { label: '30 days', value: 30 },
  { label: '90 days', value: 90 },
  { label: '365 days', value: 365 },
] as const;

const AGENT_TYPE_LABELS: Record<string, string> = {
  auto: 'Auto',
  tutor: 'Tutor',
  arbitro: 'Arbitro',
  decisore: 'Decisore',
  unknown: 'Unknown',
};

export function ChatAnalyticsTab() {
  const [days, setDays] = useState(30);

  const { data, isLoading, error } = useQuery<ChatAnalyticsDto | null>({
    queryKey: ['admin', 'chat-analytics', days],
    queryFn: () => api.admin.getChatAnalytics(days),
    staleTime: 60000,
    retry: 2,
  });

  if (error) {
    return (
      <div className="rounded-2xl bg-red-50/70 backdrop-blur-md border border-red-200 p-8 text-center">
        <p className="text-red-700 font-nunito">Failed to load chat analytics</p>
        <p className="text-sm text-red-500 mt-1">{error instanceof Error ? error.message : 'Unknown error'}</p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Time range selector */}
      <div className="flex gap-2">
        {TIME_RANGES.map((range) => (
          <button
            key={range.value}
            onClick={() => setDays(range.value)}
            className={`px-3 py-1.5 text-sm rounded-lg font-nunito transition-colors ${
              days === range.value
                ? 'bg-amber-100 text-amber-900 font-semibold'
                : 'bg-white/50 text-slate-600 hover:bg-white/70'
            }`}
          >
            {range.label}
          </button>
        ))}
      </div>

      {/* Stat Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <StatCard
          label="Total Threads"
          value={isLoading ? '...' : (data?.totalThreads.toLocaleString() ?? '0')}
          subtext={data ? `${data.activeThreads} active / ${data.closedThreads} closed` : undefined}
          className="bg-white/70 border-amber-200"
        />
        <StatCard
          label="Total Messages"
          value={isLoading ? '...' : (data?.totalMessages.toLocaleString() ?? '0')}
          subtext={data ? `Avg ${data.avgMessagesPerThread} per thread` : undefined}
          className="bg-blue-50/70 border-blue-200"
        />
        <StatCard
          label="Unique Users"
          value={isLoading ? '...' : (data?.uniqueUsers.toLocaleString() ?? '0')}
          subtext="Distinct chat users"
          className="bg-green-50/70 border-green-200"
        />
        <StatCard
          label="Active Rate"
          value={isLoading ? '...' : `${data && data.totalThreads > 0 ? Math.round((data.activeThreads / data.totalThreads) * 100) : 0}%`}
          subtext="Active vs total threads"
          className="bg-amber-50/70 border-amber-200"
        />
      </div>

      {/* Agent Type Breakdown */}
      {data?.threadsByAgentType && Object.keys(data.threadsByAgentType).length > 0 && (
        <Card className="p-6 bg-white/70 backdrop-blur-md">
          <h3 className="font-quicksand font-semibold text-lg mb-4">Threads by Agent Type</h3>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {Object.entries(data.threadsByAgentType).map(([type, count]) => (
              <div key={type} className="p-3 rounded-lg bg-slate-50/70">
                <p className="text-xs font-nunito text-slate-500">{AGENT_TYPE_LABELS[type] ?? type}</p>
                <p className="text-lg font-quicksand font-bold text-slate-800">{count}</p>
              </div>
            ))}
          </div>
        </Card>
      )}

      {/* Daily threads table */}
      <Card className="p-6 bg-white/70 backdrop-blur-md">
        <h3 className="font-quicksand font-semibold text-lg mb-4">Daily Activity ({days}d)</h3>
        {isLoading ? (
          <div className="h-48 flex items-center justify-center text-slate-400 animate-pulse">
            Loading data...
          </div>
        ) : data?.threadsByDay && data.threadsByDay.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200">
                  <th className="text-left py-2 px-3 font-nunito font-semibold text-slate-600">Date</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Threads</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Active</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Closed</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Messages</th>
                </tr>
              </thead>
              <tbody>
                {data.threadsByDay.slice(-14).map((day) => (
                  <tr key={day.date} className="border-b border-slate-100 hover:bg-white/50">
                    <td className="py-2 px-3 font-nunito text-slate-700">{day.date}</td>
                    <td className="py-2 px-3 text-right font-nunito">{day.totalCount}</td>
                    <td className="py-2 px-3 text-right font-nunito text-green-700">{day.activeCount}</td>
                    <td className="py-2 px-3 text-right font-nunito text-slate-500">{day.closedCount}</td>
                    <td className="py-2 px-3 text-right font-nunito text-blue-700">{day.messageCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="h-48 flex items-center justify-center text-slate-400">
            No chat data for this period
          </div>
        )}
      </Card>
    </div>
  );
}

function StatCard({
  label,
  value,
  subtext,
  className = '',
}: {
  label: string;
  value: string;
  subtext?: string;
  className?: string;
}) {
  return (
    <Card className={`p-6 backdrop-blur-md ${className}`}>
      <div>
        <p className="text-sm font-nunito text-slate-600">{label}</p>
        <p className="text-3xl font-quicksand font-bold text-slate-900">{value}</p>
      </div>
      {subtext && <p className="text-xs text-slate-600 mt-2">{subtext}</p>}
    </Card>
  );
}
