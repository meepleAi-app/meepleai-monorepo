'use client';

/**
 * Model Performance Tab - Admin Dashboard
 * Issue #3716: Model comparison dashboard with latency, cost, quality metrics
 */

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';

import { Card } from '@/components/ui/data-display/card';
import { api } from '@/lib/api';
import type { ModelPerformanceDto } from '@/lib/api/schemas';

/** Format cost to human-readable USD */
function formatCost(cost: number): string {
  if (cost === 0) return '$0.00';
  if (cost < 0.01) return `$${cost.toFixed(4)}`;
  return `$${cost.toFixed(2)}`;
}

/** Format latency in ms to readable */
function formatLatency(ms: number): string {
  if (ms === 0) return '0ms';
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

/** Shorten model ID for display (e.g., "openai/gpt-4o-mini" → "gpt-4o-mini") */
function shortModelId(modelId: string): string {
  const parts = modelId.split('/');
  return parts[parts.length - 1];
}

const TIME_RANGES = [
  { label: '7 days', value: 7 },
  { label: '30 days', value: 30 },
  { label: '90 days', value: 90 },
  { label: '365 days', value: 365 },
] as const;

export function ModelPerformanceTab() {
  const [days, setDays] = useState(30);

  const { data, isLoading, error } = useQuery<ModelPerformanceDto | null>({
    queryKey: ['admin', 'model-performance', days],
    queryFn: () => api.admin.getModelPerformance(days),
    staleTime: 60000,
    retry: 2,
  });

  if (error) {
    return (
      <div className="rounded-2xl bg-red-50/70 backdrop-blur-md border border-red-200 p-8 text-center">
        <p className="text-red-700 font-nunito">Failed to load model performance data</p>
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

      {/* Global Stat Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        <StatCard
          label="Total Requests"
          value={isLoading ? '...' : (data?.totalRequests.toLocaleString() ?? '0')}
          className="bg-white/70 border-amber-200"
        />
        <StatCard
          label="Total Cost"
          value={isLoading ? '...' : formatCost(data?.totalCost ?? 0)}
          className="bg-amber-50/70 border-amber-200"
        />
        <StatCard
          label="Total Tokens"
          value={isLoading ? '...' : (data?.totalTokens.toLocaleString() ?? '0')}
          className="bg-blue-50/70 border-blue-200"
        />
        <StatCard
          label="Avg Latency"
          value={isLoading ? '...' : formatLatency(data?.avgLatencyMs ?? 0)}
          className="bg-slate-50/70 border-slate-200"
        />
        <StatCard
          label="Success Rate"
          value={isLoading ? '...' : `${data?.successRate ?? 0}%`}
          className="bg-green-50/70 border-green-200"
        />
      </div>

      {/* Model Comparison Table */}
      <Card className="p-6 bg-white/70 backdrop-blur-md">
        <h3 className="font-quicksand font-semibold text-lg mb-4">Model Comparison</h3>
        {isLoading ? (
          <div className="h-48 flex items-center justify-center text-slate-400 animate-pulse">
            Loading data...
          </div>
        ) : data?.models && data.models.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200">
                  <th className="text-left py-2 px-3 font-nunito font-semibold text-slate-600">Model</th>
                  <th className="text-left py-2 px-3 font-nunito font-semibold text-slate-600">Provider</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Requests</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Usage %</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Cost</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Avg Latency</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Success</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Avg Tokens</th>
                </tr>
              </thead>
              <tbody>
                {data.models.map((model) => (
                  <tr key={model.modelId} className="border-b border-slate-100 hover:bg-white/50">
                    <td className="py-2 px-3 font-nunito text-slate-700 font-medium">{shortModelId(model.modelId)}</td>
                    <td className="py-2 px-3 font-nunito text-slate-500">{model.provider}</td>
                    <td className="py-2 px-3 text-right font-nunito">{model.requestCount.toLocaleString()}</td>
                    <td className="py-2 px-3 text-right font-nunito">
                      <span className="inline-flex items-center gap-1">
                        <span
                          className="inline-block h-2 rounded-full bg-amber-400"
                          style={{ width: `${Math.max(model.usagePercent, 2)}px` }}
                        />
                        {model.usagePercent}%
                      </span>
                    </td>
                    <td className="py-2 px-3 text-right font-nunito">{formatCost(model.totalCost)}</td>
                    <td className="py-2 px-3 text-right font-nunito">{formatLatency(model.avgLatencyMs)}</td>
                    <td className={`py-2 px-3 text-right font-nunito ${model.successRate >= 95 ? 'text-green-700' : 'text-amber-700'}`}>
                      {model.successRate}%
                    </td>
                    <td className="py-2 px-3 text-right font-nunito">{Math.round(model.avgTokensPerRequest).toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="h-48 flex items-center justify-center text-slate-400">
            No model data for this period
          </div>
        )}
      </Card>

      {/* Daily Stats Table */}
      <Card className="p-6 bg-white/70 backdrop-blur-md">
        <h3 className="font-quicksand font-semibold text-lg mb-4">Daily Trends ({days}d)</h3>
        {isLoading ? (
          <div className="h-48 flex items-center justify-center text-slate-400 animate-pulse">
            Loading data...
          </div>
        ) : data?.dailyStats && data.dailyStats.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200">
                  <th className="text-left py-2 px-3 font-nunito font-semibold text-slate-600">Date</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Requests</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Cost</th>
                  <th className="text-right py-2 px-3 font-nunito font-semibold text-slate-600">Avg Latency</th>
                </tr>
              </thead>
              <tbody>
                {data.dailyStats.slice(-14).map((day) => (
                  <tr key={day.date} className="border-b border-slate-100 hover:bg-white/50">
                    <td className="py-2 px-3 font-nunito text-slate-700">{day.date}</td>
                    <td className="py-2 px-3 text-right font-nunito">{day.requestCount}</td>
                    <td className="py-2 px-3 text-right font-nunito">{formatCost(day.totalCost)}</td>
                    <td className="py-2 px-3 text-right font-nunito">{formatLatency(day.avgLatencyMs)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="h-48 flex items-center justify-center text-slate-400">
            No daily data for this period
          </div>
        )}
      </Card>
    </div>
  );
}

function StatCard({
  label,
  value,
  className = '',
}: {
  label: string;
  value: string;
  className?: string;
}) {
  return (
    <Card className={`p-4 backdrop-blur-md ${className}`}>
      <p className="text-xs font-nunito text-slate-600">{label}</p>
      <p className="text-2xl font-quicksand font-bold text-slate-900">{value}</p>
    </Card>
  );
}
