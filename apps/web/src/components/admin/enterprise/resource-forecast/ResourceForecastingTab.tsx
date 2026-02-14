/**
 * Resource Forecasting Simulator Tab
 * Issue #3726: Resource Forecasting Simulator (Epic #3688)
 *
 * 12-month resource projections: DB, Token, Cache, Vector.
 * Growth patterns, action recommendations, scenario save/compare.
 */

'use client';

import React, { useCallback, useEffect, useState } from 'react';

import {
  TrendingUp,
  Database,
  Zap,
  HardDrive,
  Layers,
  Users,
  Save,
  Trash2,
  AlertTriangle,
  DollarSign,
  Calculator,
  ArrowUpRight,
  Shield,
} from 'lucide-react';

import { api } from '@/lib/api';
import {
  GROWTH_PATTERNS,
  GROWTH_PATTERN_LABELS,
  type GrowthPattern,
  type ResourceForecastEstimationResult,
  type ResourceForecastDto,
} from '@/lib/api/schemas/resource-forecast.schemas';

// ========== Deterministic Mock Data (SSR-safe) ==========

const MOCK_ESTIMATION: ResourceForecastEstimationResult = {
  growthPattern: 'Linear',
  monthlyGrowthRate: 10,
  currentUsers: 1000,
  projectedUsersMonth12: 2200,
  projections: Array.from({ length: 12 }, (_, i) => ({
    month: i + 1,
    projectedUsers: 1000 + (i + 1) * 100,
    projectedDbGb: 5.0 + (i + 1) * 0.49,
    projectedDailyTokens: 500000 + (i + 1) * 50000,
    projectedCacheMb: 256 + (i + 1) * 25.6,
    projectedVectorEntries: 100000 + (i + 1) * 10000,
    estimatedMonthlyCostUsd: 12.5 + (i + 1) * 1.8,
  })),
  recommendations: [
    {
      resourceType: 'CacheMemory',
      triggerMonth: 8,
      severity: 'warning',
      message: 'Cache projected to reach 2150 MB by month 8',
      action: 'Plan cache memory scaling or optimize TTL settings',
    },
  ],
  projectedMonthlyCostMonth12: 34.1,
};

const MOCK_SCENARIOS: ResourceForecastDto[] = [
  {
    id: '00000000-0000-0000-0000-000000000011',
    name: 'Conservative Growth',
    growthPattern: 'Linear',
    monthlyGrowthRate: 5,
    currentUsers: 500,
    currentDbSizeGb: 2.5,
    currentDailyTokens: 250000,
    currentCacheMb: 128,
    currentVectorEntries: 50000,
    dbPerUserMb: 5,
    tokensPerUserPerDay: 500,
    cachePerUserMb: 0.25,
    vectorsPerUser: 100,
    projectedMonthlyCost: 18.5,
    createdByUserId: '00000000-0000-0000-0000-000000000099',
    createdAt: '2026-02-10T10:00:00Z',
  },
  {
    id: '00000000-0000-0000-0000-000000000012',
    name: 'Aggressive Expansion',
    growthPattern: 'Exponential',
    monthlyGrowthRate: 15,
    currentUsers: 1000,
    currentDbSizeGb: 10,
    currentDailyTokens: 1000000,
    currentCacheMb: 512,
    currentVectorEntries: 200000,
    dbPerUserMb: 10,
    tokensPerUserPerDay: 1000,
    cachePerUserMb: 0.5,
    vectorsPerUser: 200,
    projectedMonthlyCost: 245.8,
    createdByUserId: '00000000-0000-0000-0000-000000000099',
    createdAt: '2026-02-09T15:30:00Z',
  },
];

// ========== Resource Type Colors ==========

const RESOURCE_COLORS: Record<string, string> = {
  Database: 'text-blue-400',
  TokenUsage: 'text-amber-400',
  CacheMemory: 'text-emerald-400',
  VectorStorage: 'text-purple-400',
};

const SEVERITY_STYLES: Record<string, string> = {
  warning: 'border-amber-500/20 bg-amber-500/10 text-amber-300',
  critical: 'border-red-500/20 bg-red-500/10 text-red-300',
};

// ========== Component ==========

export function ResourceForecastingTab() {
  // Form state - growth parameters
  const [growthPattern, setGrowthPattern] = useState<GrowthPattern>('Linear');
  const [monthlyGrowthRate, setMonthlyGrowthRate] = useState(10);

  // Form state - current metrics
  const [currentUsers, setCurrentUsers] = useState(1000);
  const [currentDbSizeGb, setCurrentDbSizeGb] = useState(5);
  const [currentDailyTokens, setCurrentDailyTokens] = useState(500000);
  const [currentCacheMb, setCurrentCacheMb] = useState(256);
  const [currentVectorEntries, setCurrentVectorEntries] = useState(100000);

  // Form state - per-user multipliers
  const [dbPerUserMb, setDbPerUserMb] = useState(5);
  const [tokensPerUserPerDay, setTokensPerUserPerDay] = useState(500);
  const [cachePerUserMb, setCachePerUserMb] = useState(0.25);
  const [vectorsPerUser, setVectorsPerUser] = useState(100);

  // Results state
  const [estimation, setEstimation] = useState<ResourceForecastEstimationResult | null>(null);
  const [scenarios, setScenarios] = useState<ResourceForecastDto[]>(MOCK_SCENARIOS);
  const [loading, setLoading] = useState(false);
  const [saveName, setSaveName] = useState('');
  const [showSaveDialog, setShowSaveDialog] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Fetch saved scenarios on mount
  const fetchScenarios = useCallback(async () => {
    try {
      const result = await api.admin.getResourceForecasts({ page: 1, pageSize: 50 });
      setScenarios(result.items);
    } catch {
      // Keep mock data on error
    }
  }, []);

  useEffect(() => {
    fetchScenarios();
  }, [fetchScenarios]);

  // Estimate forecast
  const handleEstimate = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.admin.estimateResourceForecast({
        growthPattern,
        monthlyGrowthRate,
        currentUsers,
        currentDbSizeGb,
        currentDailyTokens,
        currentCacheMb,
        currentVectorEntries,
        dbPerUserMb,
        tokensPerUserPerDay,
        cachePerUserMb,
        vectorsPerUser,
      });
      setEstimation(result);
    } catch {
      setEstimation(MOCK_ESTIMATION);
      setError('Using estimated data. Connect to API for live calculations.');
    } finally {
      setLoading(false);
    }
  }, [
    growthPattern,
    monthlyGrowthRate,
    currentUsers,
    currentDbSizeGb,
    currentDailyTokens,
    currentCacheMb,
    currentVectorEntries,
    dbPerUserMb,
    tokensPerUserPerDay,
    cachePerUserMb,
    vectorsPerUser,
  ]);

  // Save scenario
  const handleSave = useCallback(async () => {
    if (!estimation || !saveName.trim()) return;
    try {
      await api.admin.saveResourceForecast({
        name: saveName.trim(),
        growthPattern: estimation.growthPattern,
        monthlyGrowthRate: estimation.monthlyGrowthRate,
        currentUsers,
        currentDbSizeGb,
        currentDailyTokens,
        currentCacheMb,
        currentVectorEntries,
        dbPerUserMb,
        tokensPerUserPerDay,
        cachePerUserMb,
        vectorsPerUser,
        projectionsJson: JSON.stringify(estimation.projections),
        recommendationsJson:
          estimation.recommendations.length > 0
            ? JSON.stringify(estimation.recommendations)
            : null,
        projectedMonthlyCost: estimation.projectedMonthlyCostMonth12,
      });
      setSaveName('');
      setShowSaveDialog(false);
      await fetchScenarios();
    } catch {
      setError('Failed to save scenario.');
    }
  }, [
    estimation,
    saveName,
    currentUsers,
    currentDbSizeGb,
    currentDailyTokens,
    currentCacheMb,
    currentVectorEntries,
    dbPerUserMb,
    tokensPerUserPerDay,
    cachePerUserMb,
    vectorsPerUser,
    fetchScenarios,
  ]);

  // Delete scenario
  const handleDelete = useCallback(
    async (id: string) => {
      try {
        await api.admin.deleteResourceForecast(id);
        await fetchScenarios();
      } catch {
        setError('Failed to delete scenario.');
      }
    },
    [fetchScenarios],
  );

  // Load scenario into form
  const handleLoadScenario = useCallback((scenario: ResourceForecastDto) => {
    setGrowthPattern(scenario.growthPattern as GrowthPattern);
    setMonthlyGrowthRate(scenario.monthlyGrowthRate);
    setCurrentUsers(scenario.currentUsers);
    setCurrentDbSizeGb(scenario.currentDbSizeGb);
    setCurrentDailyTokens(scenario.currentDailyTokens);
    setCurrentCacheMb(scenario.currentCacheMb);
    setCurrentVectorEntries(scenario.currentVectorEntries);
    setDbPerUserMb(scenario.dbPerUserMb);
    setTokensPerUserPerDay(scenario.tokensPerUserPerDay);
    setCachePerUserMb(scenario.cachePerUserMb);
    setVectorsPerUser(scenario.vectorsPerUser);
    setEstimation(null);
  }, []);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <TrendingUp className="h-6 w-6 text-teal-400" />
        <div>
          <h2 className="text-lg font-semibold text-white">Resource Forecasting Simulator</h2>
          <p className="text-sm text-zinc-400">
            Project resource usage over 12 months with growth scenarios and recommendations
          </p>
        </div>
      </div>

      {error && (
        <div className="flex items-center gap-2 rounded-lg border border-yellow-500/30 bg-yellow-500/10 p-3 text-sm text-yellow-300">
          <AlertTriangle className="h-4 w-4 shrink-0" />
          {error}
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Left: Input Form */}
        <div className="space-y-4 rounded-xl border border-zinc-700/50 bg-zinc-800/50 p-5">
          {/* Growth Parameters */}
          <h3 className="text-sm font-medium text-zinc-300">Growth Parameters</h3>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label
                htmlFor="growth-pattern-select"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                Growth Pattern
              </label>
              <select
                id="growth-pattern-select"
                value={growthPattern}
                onChange={(e) => setGrowthPattern(e.target.value as GrowthPattern)}
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              >
                {GROWTH_PATTERNS.map((p) => (
                  <option key={p} value={p}>
                    {GROWTH_PATTERN_LABELS[p]}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label
                htmlFor="growth-rate-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                <ArrowUpRight className="mr-1 inline h-3 w-3" />
                Growth %/mo
              </label>
              <input
                id="growth-rate-input"
                type="number"
                min={0}
                max={100}
                step={0.5}
                value={monthlyGrowthRate}
                onChange={(e) =>
                  setMonthlyGrowthRate(Math.max(0, Math.min(100, parseFloat(e.target.value) || 0)))
                }
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
          </div>

          {/* Current Metrics */}
          <h3 className="text-sm font-medium text-zinc-300">Current Metrics</h3>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label
                htmlFor="current-users-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                <Users className="mr-1 inline h-3 w-3" />
                Users
              </label>
              <input
                id="current-users-input"
                type="number"
                min={0}
                value={currentUsers}
                onChange={(e) => setCurrentUsers(Math.max(0, parseInt(e.target.value) || 0))}
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
            <div>
              <label
                htmlFor="current-db-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                <Database className="mr-1 inline h-3 w-3" />
                DB Size (GB)
              </label>
              <input
                id="current-db-input"
                type="number"
                min={0}
                step={0.1}
                value={currentDbSizeGb}
                onChange={(e) =>
                  setCurrentDbSizeGb(Math.max(0, parseFloat(e.target.value) || 0))
                }
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
            <div>
              <label
                htmlFor="current-tokens-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                <Zap className="mr-1 inline h-3 w-3" />
                Daily Tokens
              </label>
              <input
                id="current-tokens-input"
                type="number"
                min={0}
                value={currentDailyTokens}
                onChange={(e) =>
                  setCurrentDailyTokens(Math.max(0, parseInt(e.target.value) || 0))
                }
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
            <div>
              <label
                htmlFor="current-cache-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                <HardDrive className="mr-1 inline h-3 w-3" />
                Cache (MB)
              </label>
              <input
                id="current-cache-input"
                type="number"
                min={0}
                value={currentCacheMb}
                onChange={(e) =>
                  setCurrentCacheMb(Math.max(0, parseFloat(e.target.value) || 0))
                }
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
            <div className="col-span-2">
              <label
                htmlFor="current-vectors-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                <Layers className="mr-1 inline h-3 w-3" />
                Vector Entries
              </label>
              <input
                id="current-vectors-input"
                type="number"
                min={0}
                value={currentVectorEntries}
                onChange={(e) =>
                  setCurrentVectorEntries(Math.max(0, parseInt(e.target.value) || 0))
                }
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
          </div>

          {/* Per-User Multipliers */}
          <h3 className="text-sm font-medium text-zinc-300">Per-User Resource Usage</h3>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label
                htmlFor="db-per-user-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                DB/User (MB)
              </label>
              <input
                id="db-per-user-input"
                type="number"
                min={0}
                step={0.1}
                value={dbPerUserMb}
                onChange={(e) => setDbPerUserMb(Math.max(0, parseFloat(e.target.value) || 0))}
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
            <div>
              <label
                htmlFor="tokens-per-user-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                Tokens/User/Day
              </label>
              <input
                id="tokens-per-user-input"
                type="number"
                min={0}
                value={tokensPerUserPerDay}
                onChange={(e) =>
                  setTokensPerUserPerDay(Math.max(0, parseInt(e.target.value) || 0))
                }
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
            <div>
              <label
                htmlFor="cache-per-user-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                Cache/User (MB)
              </label>
              <input
                id="cache-per-user-input"
                type="number"
                min={0}
                step={0.01}
                value={cachePerUserMb}
                onChange={(e) =>
                  setCachePerUserMb(Math.max(0, parseFloat(e.target.value) || 0))
                }
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
            <div>
              <label
                htmlFor="vectors-per-user-input"
                className="mb-1.5 block text-xs font-medium text-zinc-400"
              >
                Vectors/User
              </label>
              <input
                id="vectors-per-user-input"
                type="number"
                min={0}
                value={vectorsPerUser}
                onChange={(e) => setVectorsPerUser(Math.max(0, parseInt(e.target.value) || 0))}
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-teal-500 focus:outline-none"
              />
            </div>
          </div>

          {/* Forecast Button */}
          <button
            onClick={handleEstimate}
            disabled={loading}
            className="flex w-full items-center justify-center gap-2 rounded-lg bg-teal-600 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-teal-500 disabled:opacity-50"
          >
            <TrendingUp className="h-4 w-4" />
            {loading ? 'Computing...' : 'Run Forecast'}
          </button>
        </div>

        {/* Right: Results */}
        <div className="space-y-4 rounded-xl border border-zinc-700/50 bg-zinc-800/50 p-5">
          <h3 className="text-sm font-medium text-zinc-300">12-Month Projection</h3>

          {estimation ? (
            <>
              {/* Summary KPI Cards */}
              <div className="grid grid-cols-2 gap-3">
                <div className="rounded-lg border border-zinc-700/50 bg-zinc-900/50 p-3">
                  <div className="flex items-center gap-1.5 text-xs text-zinc-400">
                    <Users className="h-3 w-3" />
                    Users (Month 12)
                  </div>
                  <div className="mt-1 text-lg font-semibold text-white">
                    {estimation.projectedUsersMonth12.toLocaleString()}
                  </div>
                  <div className="text-xs text-zinc-500">
                    from {estimation.currentUsers.toLocaleString()}
                  </div>
                </div>
                <div className="rounded-lg border border-zinc-700/50 bg-zinc-900/50 p-3">
                  <div className="flex items-center gap-1.5 text-xs text-zinc-400">
                    <DollarSign className="h-3 w-3" />
                    Monthly Cost (M12)
                  </div>
                  <div
                    className={`mt-1 text-lg font-semibold ${
                      estimation.projectedMonthlyCostMonth12 > 100
                        ? 'text-amber-400'
                        : 'text-emerald-400'
                    }`}
                  >
                    $
                    {estimation.projectedMonthlyCostMonth12.toLocaleString(undefined, {
                      minimumFractionDigits: 2,
                      maximumFractionDigits: 2,
                    })}
                  </div>
                </div>
              </div>

              {/* Projection Timeline */}
              <div className="max-h-56 overflow-y-auto rounded-lg border border-zinc-700/50 bg-zinc-900/50">
                <table className="w-full text-left text-xs">
                  <thead className="sticky top-0 bg-zinc-900">
                    <tr className="border-b border-zinc-700/50 text-zinc-400">
                      <th className="px-2 py-1.5 font-medium">Mo</th>
                      <th className="px-2 py-1.5 font-medium">Users</th>
                      <th className="px-2 py-1.5 font-medium">DB (GB)</th>
                      <th className="px-2 py-1.5 font-medium">Tokens/d</th>
                      <th className="px-2 py-1.5 font-medium">Cache</th>
                      <th className="px-2 py-1.5 font-medium">Vectors</th>
                      <th className="px-2 py-1.5 text-right font-medium">$/mo</th>
                    </tr>
                  </thead>
                  <tbody>
                    {estimation.projections.map((p) => (
                      <tr key={p.month} className="border-b border-zinc-800/50 text-zinc-300">
                        <td className="px-2 py-1.5 font-mono">{p.month}</td>
                        <td className="px-2 py-1.5 font-mono">
                          {p.projectedUsers.toLocaleString()}
                        </td>
                        <td className="px-2 py-1.5 font-mono text-blue-400">
                          {p.projectedDbGb.toFixed(1)}
                        </td>
                        <td className="px-2 py-1.5 font-mono text-amber-400">
                          {(p.projectedDailyTokens / 1000).toFixed(0)}K
                        </td>
                        <td className="px-2 py-1.5 font-mono text-emerald-400">
                          {p.projectedCacheMb.toFixed(0)}
                        </td>
                        <td className="px-2 py-1.5 font-mono text-purple-400">
                          {(p.projectedVectorEntries / 1000).toFixed(0)}K
                        </td>
                        <td className="px-2 py-1.5 text-right font-mono">
                          ${p.estimatedMonthlyCostUsd.toFixed(2)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Recommendations */}
              {estimation.recommendations.length > 0 && (
                <div className="space-y-1.5">
                  <h4 className="flex items-center gap-1.5 text-xs font-medium text-zinc-400">
                    <Shield className="h-3 w-3" />
                    Action Recommendations
                  </h4>
                  {estimation.recommendations.map((rec, i) => (
                    <div
                      key={`rec-${i}`}
                      className={`rounded-lg border p-2.5 text-xs ${
                        SEVERITY_STYLES[rec.severity] ?? SEVERITY_STYLES.warning
                      }`}
                    >
                      <div className="flex items-center gap-2">
                        <span
                          className={`font-medium ${RESOURCE_COLORS[rec.resourceType] ?? 'text-zinc-300'}`}
                        >
                          {rec.resourceType}
                        </span>
                        <span className="text-zinc-500">Month {rec.triggerMonth}</span>
                      </div>
                      <div className="mt-0.5">{rec.message}</div>
                      <div className="mt-1 font-medium opacity-80">{rec.action}</div>
                    </div>
                  ))}
                </div>
              )}

              {/* Save Button */}
              {!showSaveDialog ? (
                <button
                  onClick={() => setShowSaveDialog(true)}
                  className="flex w-full items-center justify-center gap-2 rounded-lg border border-zinc-600 bg-zinc-700/50 px-4 py-2 text-sm text-zinc-300 transition hover:bg-zinc-700"
                >
                  <Save className="h-4 w-4" />
                  Save Scenario
                </button>
              ) : (
                <div className="flex gap-2">
                  <input
                    type="text"
                    value={saveName}
                    onChange={(e) => setSaveName(e.target.value)}
                    placeholder="Scenario name..."
                    maxLength={200}
                    className="flex-1 rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white placeholder:text-zinc-500 focus:border-teal-500 focus:outline-none"
                  />
                  <button
                    onClick={handleSave}
                    disabled={!saveName.trim()}
                    className="rounded-lg bg-teal-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-teal-500 disabled:opacity-50"
                  >
                    Save
                  </button>
                  <button
                    onClick={() => {
                      setShowSaveDialog(false);
                      setSaveName('');
                    }}
                    className="rounded-lg border border-zinc-600 px-3 py-2 text-sm text-zinc-400 transition hover:bg-zinc-700"
                  >
                    Cancel
                  </button>
                </div>
              )}
            </>
          ) : (
            <div className="flex h-48 items-center justify-center text-sm text-zinc-500">
              Configure parameters and click &quot;Run Forecast&quot; to see projections
            </div>
          )}
        </div>
      </div>

      {/* Saved Scenarios */}
      {scenarios.length > 0 && (
        <div className="rounded-xl border border-zinc-700/50 bg-zinc-800/50 p-5">
          <h3 className="mb-3 text-sm font-medium text-zinc-300">Saved Scenarios</h3>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-zinc-700/50 text-xs text-zinc-400">
                  <th className="pb-2 pr-4 font-medium">Name</th>
                  <th className="pb-2 pr-4 font-medium">Pattern</th>
                  <th className="pb-2 pr-4 text-right font-medium">Growth</th>
                  <th className="pb-2 pr-4 text-right font-medium">Users</th>
                  <th className="pb-2 pr-4 text-right font-medium">$/mo (M12)</th>
                  <th className="pb-2 font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {scenarios.map((scenario) => (
                  <tr key={scenario.id} className="border-b border-zinc-700/30">
                    <td className="py-2.5 pr-4 font-medium text-white">{scenario.name}</td>
                    <td className="py-2.5 pr-4 text-zinc-400">
                      {GROWTH_PATTERN_LABELS[scenario.growthPattern as GrowthPattern] ??
                        scenario.growthPattern}
                    </td>
                    <td className="py-2.5 pr-4 text-right font-mono text-zinc-400">
                      {scenario.monthlyGrowthRate}%
                    </td>
                    <td className="py-2.5 pr-4 text-right font-mono text-zinc-400">
                      {scenario.currentUsers.toLocaleString()}
                    </td>
                    <td
                      className={`py-2.5 pr-4 text-right font-mono ${
                        scenario.projectedMonthlyCost > 100 ? 'text-amber-400' : 'text-emerald-400'
                      }`}
                    >
                      $
                      {scenario.projectedMonthlyCost.toLocaleString(undefined, {
                        minimumFractionDigits: 2,
                        maximumFractionDigits: 2,
                      })}
                    </td>
                    <td className="py-2.5">
                      <div className="flex items-center gap-1">
                        <button
                          onClick={() => handleLoadScenario(scenario)}
                          className="rounded p-1.5 text-zinc-400 transition hover:bg-zinc-700 hover:text-white"
                          aria-label={`Load scenario ${scenario.name}`}
                        >
                          <Calculator className="h-3.5 w-3.5" />
                        </button>
                        <button
                          onClick={() => handleDelete(scenario.id)}
                          className="rounded p-1.5 text-zinc-400 transition hover:bg-red-900/30 hover:text-red-400"
                          aria-label={`Delete scenario ${scenario.name}`}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
