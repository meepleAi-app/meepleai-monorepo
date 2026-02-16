'use client';

/**
 * PDF Analytics Tab - Admin Dashboard
 * Issue #3715: System-wide PDF monitoring with real API data
 */

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';

import { Card } from '@/components/ui/data-display/card';
import { api } from '@/lib/api';
import type { PdfAnalyticsDto } from '@/lib/api/schemas';

/** Format .NET TimeSpan string "HH:mm:ss.fffffff" to human-readable */
function formatTimeSpan(ts: string | null): string {
  if (!ts) return '—';
  const parts = ts.split(':');
  if (parts.length < 3) return ts;
  const hours = parseInt(parts[0], 10);
  const minutes = parseInt(parts[1], 10);
  const seconds = parseFloat(parts[2]);
  if (hours > 0) return `${hours}h ${minutes}m`;
  if (minutes > 0) return `${minutes}m ${Math.round(seconds)}s`;
  return `${seconds.toFixed(1)}s`;
}

/** Format bytes to human-readable size */
function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  const value = bytes / Math.pow(1024, i);
  return `${value.toFixed(1)} ${units[i]}`;
}

const TIME_RANGES = [
  { label: '7 days', value: 7 },
  { label: '30 days', value: 30 },
  { label: '90 days', value: 90 },
  { label: '365 days', value: 365 },
] as const;

export function PdfAnalyticsTab() {
  const [days, setDays] = useState(30);

  const { data, isLoading, error } = useQuery<PdfAnalyticsDto | null>({
    queryKey: ['admin', 'pdf-analytics', days],
    queryFn: () => api.admin.getPdfAnalytics(days),
    staleTime: 60000,
    retry: 2,
  });

  if (error) {
    return (
      <div className="rounded-2xl bg-red-50/70 backdrop-blur-md border border-red-200 p-8 text-center">
        <p className="text-red-700 font-nunito">Failed to load PDF analytics</p>
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
          label="Total PDFs"
          value={isLoading ? '...' : (data?.totalUploaded.toLocaleString() ?? '0')}
          icon="📄"
          subtext={data ? `${data.successCount} success / ${data.failedCount} failed` : undefined}
          className="bg-white/70 border-amber-200"
        />
        <StatCard
          label="Success Rate"
          value={isLoading ? '...' : `${data?.successRate ?? 0}%`}
          icon="✅"
          subtext="Target: >95%"
          className="bg-green-50/70 border-green-200"
          valueClass={data && data.successRate >= 95 ? 'text-green-900' : 'text-amber-900'}
        />
        <StatCard
          label="Avg Processing"
          value={isLoading ? '...' : formatTimeSpan(data?.avgProcessingTime ?? null)}
          icon="⏱️"
          subtext={data?.p95ProcessingTime ? `P95: ${formatTimeSpan(data.p95ProcessingTime)}` : undefined}
          className="bg-blue-50/70 border-blue-200"
        />
        <StatCard
          label="Storage"
          value={isLoading ? '...' : formatBytes(data?.totalStorageBytes ?? 0)}
          icon="💾"
          subtext={data?.storageByTier ? `${Object.keys(data.storageByTier).length} tiers` : undefined}
          className="bg-amber-50/70 border-amber-200"
        />
      </div>

      {/* Daily uploads table */}
      <Card className="p-6 bg-white/70 backdrop-blur-md">
        <h3 className="font-quicksand font-semibold text-lg mb-4">Daily Uploads ({days}d)</h3>
        {isLoading ? (
          <div className="h-48 flex items-center justify-center text-slate-400 animate-pulse">
            Loading data...
          </div>
        ) : data?.uploadsByDay && data.uploadsByDay.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200">
                  <th className="text-left py-2 px-3 font-nunito font-semibold text-slate-600">Date</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Total</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Success</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Failed</th>
                </tr>
              </thead>
              <tbody>
                {data.uploadsByDay.slice(-14).map((day) => (
                  <tr key={day.date} className="border-b border-slate-100 hover:bg-white/50">
                    <td className="py-2 px-3 font-nunito text-slate-700">{day.date}</td>
                    <td className="py-2 px-3 text-right font-nunito">{day.totalCount}</td>
                    <td className="py-2 px-3 text-right font-nunito text-green-700">{day.successCount}</td>
                    <td className="py-2 px-3 text-right font-nunito text-red-600">{day.failedCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="h-48 flex items-center justify-center text-slate-400">
            No upload data for this period
          </div>
        )}
      </Card>

      {/* Storage breakdown */}
      {data?.storageByTier && Object.keys(data.storageByTier).length > 0 && (
        <Card className="p-6 bg-white/70 backdrop-blur-md">
          <h3 className="font-quicksand font-semibold text-lg mb-4">Storage by State</h3>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            {Object.entries(data.storageByTier).map(([state, bytes]) => (
              <div key={state} className="p-3 rounded-lg bg-slate-50/70">
                <p className="text-xs font-nunito text-slate-500">{state}</p>
                <p className="text-lg font-quicksand font-bold text-slate-800">{formatBytes(bytes)}</p>
              </div>
            ))}
          </div>
        </Card>
      )}
    </div>
  );
}

function StatCard({
  label,
  value,
  icon,
  subtext,
  className = '',
  valueClass = 'text-slate-900',
}: {
  label: string;
  value: string;
  icon: string;
  subtext?: string;
  className?: string;
  valueClass?: string;
}) {
  return (
    <Card className={`p-6 backdrop-blur-md ${className}`}>
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-nunito text-slate-600">{label}</p>
          <p className={`text-3xl font-quicksand font-bold ${valueClass}`}>{value}</p>
        </div>
        <span className="text-4xl">{icon}</span>
      </div>
      {subtext && <p className="text-xs text-slate-600 mt-2">{subtext}</p>}
    </Card>
  );
}
