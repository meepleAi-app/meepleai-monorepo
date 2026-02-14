/**
 * Agent Cost Calculator Tab
 * Issue #3725: Agent Cost Calculator (Epic #3688)
 *
 * Interactive calculator with strategy/model selection, usage parameters,
 * cost projections, saved scenarios, and warnings.
 */

'use client';

import React, { useCallback, useEffect, useState } from 'react';

import {
  Calculator,
  DollarSign,
  Save,
  Trash2,
  AlertTriangle,
  TrendingUp,
  Users,
  MessageSquare,
  Zap,
  Clock,
} from 'lucide-react';

import { api } from '@/lib/api';
import {
  RAG_STRATEGIES,
  RAG_STRATEGY_LABELS,
  type AgentCostEstimationResult,
  type CostScenarioDto,
  type RagStrategy,
} from '@/lib/api/schemas/cost-calculator.schemas';

// ========== Deterministic Mock Data (SSR-safe) ==========

const MOCK_ESTIMATION: AgentCostEstimationResult = {
  strategy: 'Balanced',
  modelId: 'deepseek/deepseek-chat',
  provider: 'DeepSeek',
  inputCostPer1MTokens: 0.27,
  outputCostPer1MTokens: 1.1,
  costPerRequest: 0.00054,
  dailyProjection: 54.0,
  monthlyProjection: 1620.0,
  totalDailyRequests: 100000,
  avgTokensPerRequest: 1000,
  warnings: ['Monthly projection exceeds $1,000. Review usage parameters for optimization opportunities.'],
};

const MOCK_SCENARIOS: CostScenarioDto[] = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    name: 'Production Baseline',
    strategy: 'Balanced',
    modelId: 'deepseek/deepseek-chat',
    messagesPerDay: 1000,
    activeUsers: 100,
    avgTokensPerRequest: 1000,
    costPerRequest: 0.00054,
    dailyProjection: 54.0,
    monthlyProjection: 1620.0,
    warnings: [],
    createdByUserId: '00000000-0000-0000-0000-000000000099',
    createdAt: '2026-02-10T10:00:00Z',
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    name: 'High Traffic Scenario',
    strategy: 'Fast',
    modelId: 'meta-llama/llama-3.3-70b-instruct:free',
    messagesPerDay: 5000,
    activeUsers: 500,
    avgTokensPerRequest: 800,
    costPerRequest: 0,
    dailyProjection: 0,
    monthlyProjection: 0,
    warnings: ['This model is free tier. Costs may change if free tier limits are exceeded.'],
    createdByUserId: '00000000-0000-0000-0000-000000000099',
    createdAt: '2026-02-09T15:30:00Z',
  },
];

// ========== Available Models ==========

const AVAILABLE_MODELS = [
  { id: 'meta-llama/llama-3.3-70b-instruct:free', label: 'Llama 3.3 70B (Free)', provider: 'OpenRouter' },
  { id: 'meta-llama/llama-3.1-70b-instruct:free', label: 'Llama 3.1 70B (Free)', provider: 'OpenRouter' },
  { id: 'meta-llama/llama-3.3-70b-instruct', label: 'Llama 3.3 70B (Paid)', provider: 'OpenRouter' },
  { id: 'google/gemini-pro', label: 'Gemini Pro', provider: 'Google' },
  { id: 'deepseek/deepseek-chat', label: 'DeepSeek Chat', provider: 'DeepSeek' },
  { id: 'llama3:8b', label: 'Llama 3 8B (Local)', provider: 'Ollama' },
  { id: 'llama3:70b', label: 'Llama 3 70B (Local)', provider: 'Ollama' },
  { id: 'mistral', label: 'Mistral (Local)', provider: 'Ollama' },
];

// ========== Component ==========

export function AgentCostCalculatorTab() {
  // Form state
  const [strategy, setStrategy] = useState<RagStrategy>('Balanced');
  const [modelId, setModelId] = useState('deepseek/deepseek-chat');
  const [messagesPerDay, setMessagesPerDay] = useState(1000);
  const [activeUsers, setActiveUsers] = useState(100);
  const [avgTokensPerRequest, setAvgTokensPerRequest] = useState(1000);

  // Results state
  const [estimation, setEstimation] = useState<AgentCostEstimationResult | null>(null);
  const [scenarios, setScenarios] = useState<CostScenarioDto[]>(MOCK_SCENARIOS);
  const [loading, setLoading] = useState(false);
  const [saveName, setSaveName] = useState('');
  const [showSaveDialog, setShowSaveDialog] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Fetch saved scenarios on mount
  const fetchScenarios = useCallback(async () => {
    try {
      const result = await api.admin.getCostScenarios({ page: 1, pageSize: 50 });
      setScenarios(result.items);
    } catch {
      // Keep mock data on error
    }
  }, []);

  useEffect(() => {
    fetchScenarios();
  }, [fetchScenarios]);

  // Estimate cost
  const handleEstimate = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const result = await api.admin.estimateAgentCost({
        strategy,
        modelId,
        messagesPerDay,
        activeUsers,
        avgTokensPerRequest,
      });
      setEstimation(result);
    } catch {
      setEstimation(MOCK_ESTIMATION);
      setError('Using estimated data. Connect to API for live calculations.');
    } finally {
      setLoading(false);
    }
  }, [strategy, modelId, messagesPerDay, activeUsers, avgTokensPerRequest]);

  // Save scenario
  const handleSave = useCallback(async () => {
    if (!estimation || !saveName.trim()) return;
    try {
      await api.admin.saveCostScenario({
        name: saveName.trim(),
        strategy: estimation.strategy,
        modelId: estimation.modelId,
        messagesPerDay,
        activeUsers,
        avgTokensPerRequest,
        costPerRequest: estimation.costPerRequest,
        dailyProjection: estimation.dailyProjection,
        monthlyProjection: estimation.monthlyProjection,
        warnings: estimation.warnings,
      });
      setSaveName('');
      setShowSaveDialog(false);
      await fetchScenarios();
    } catch {
      setError('Failed to save scenario.');
    }
  }, [estimation, saveName, messagesPerDay, activeUsers, avgTokensPerRequest, fetchScenarios]);

  // Delete scenario
  const handleDelete = useCallback(
    async (id: string) => {
      try {
        await api.admin.deleteCostScenario(id);
        await fetchScenarios();
      } catch {
        setError('Failed to delete scenario.');
      }
    },
    [fetchScenarios],
  );

  // Load scenario into form
  const handleLoadScenario = useCallback((scenario: CostScenarioDto) => {
    setStrategy(scenario.strategy as RagStrategy);
    setModelId(scenario.modelId);
    setMessagesPerDay(scenario.messagesPerDay);
    setActiveUsers(scenario.activeUsers);
    setAvgTokensPerRequest(scenario.avgTokensPerRequest);
    setEstimation({
      strategy: scenario.strategy,
      modelId: scenario.modelId,
      provider: '',
      inputCostPer1MTokens: 0,
      outputCostPer1MTokens: 0,
      costPerRequest: scenario.costPerRequest,
      dailyProjection: scenario.dailyProjection,
      monthlyProjection: scenario.monthlyProjection,
      totalDailyRequests: scenario.messagesPerDay * scenario.activeUsers,
      avgTokensPerRequest: scenario.avgTokensPerRequest,
      warnings: scenario.warnings,
    });
  }, []);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Calculator className="h-6 w-6 text-indigo-400" />
        <div>
          <h2 className="text-lg font-semibold text-white">Agent Cost Calculator</h2>
          <p className="text-sm text-zinc-400">
            Estimate and compare costs for different AI agent configurations
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
          <h3 className="text-sm font-medium text-zinc-300">Configuration</h3>

          {/* Strategy Selector */}
          <div>
            <label htmlFor="strategy-select" className="mb-1.5 block text-xs font-medium text-zinc-400">
              RAG Strategy
            </label>
            <select
              id="strategy-select"
              value={strategy}
              onChange={(e) => setStrategy(e.target.value as RagStrategy)}
              className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-indigo-500 focus:outline-none"
            >
              {RAG_STRATEGIES.map((s) => (
                <option key={s} value={s}>
                  {RAG_STRATEGY_LABELS[s]}
                </option>
              ))}
            </select>
          </div>

          {/* Model Selector */}
          <div>
            <label htmlFor="model-select" className="mb-1.5 block text-xs font-medium text-zinc-400">
              LLM Model
            </label>
            <select
              id="model-select"
              value={modelId}
              onChange={(e) => setModelId(e.target.value)}
              className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-indigo-500 focus:outline-none"
            >
              {AVAILABLE_MODELS.map((m) => (
                <option key={m.id} value={m.id}>
                  {m.label} ({m.provider})
                </option>
              ))}
            </select>
          </div>

          {/* Numeric Inputs */}
          <div className="grid grid-cols-3 gap-3">
            <div>
              <label htmlFor="messages-input" className="mb-1.5 block text-xs font-medium text-zinc-400">
                <MessageSquare className="mr-1 inline h-3 w-3" />
                Msgs/Day
              </label>
              <input
                id="messages-input"
                type="number"
                min={0}
                max={1000000}
                value={messagesPerDay}
                onChange={(e) => setMessagesPerDay(Math.max(0, parseInt(e.target.value) || 0))}
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-indigo-500 focus:outline-none"
              />
            </div>
            <div>
              <label htmlFor="users-input" className="mb-1.5 block text-xs font-medium text-zinc-400">
                <Users className="mr-1 inline h-3 w-3" />
                Active Users
              </label>
              <input
                id="users-input"
                type="number"
                min={0}
                max={10000000}
                value={activeUsers}
                onChange={(e) => setActiveUsers(Math.max(0, parseInt(e.target.value) || 0))}
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-indigo-500 focus:outline-none"
              />
            </div>
            <div>
              <label htmlFor="tokens-input" className="mb-1.5 block text-xs font-medium text-zinc-400">
                <Zap className="mr-1 inline h-3 w-3" />
                Avg Tokens
              </label>
              <input
                id="tokens-input"
                type="number"
                min={1}
                max={100000}
                value={avgTokensPerRequest}
                onChange={(e) => setAvgTokensPerRequest(Math.max(1, parseInt(e.target.value) || 1))}
                className="w-full rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white focus:border-indigo-500 focus:outline-none"
              />
            </div>
          </div>

          {/* Estimate Button */}
          <button
            onClick={handleEstimate}
            disabled={loading}
            className="flex w-full items-center justify-center gap-2 rounded-lg bg-indigo-600 px-4 py-2.5 text-sm font-medium text-white transition hover:bg-indigo-500 disabled:opacity-50"
          >
            <Calculator className="h-4 w-4" />
            {loading ? 'Calculating...' : 'Estimate Cost'}
          </button>
        </div>

        {/* Right: Results */}
        <div className="space-y-4 rounded-xl border border-zinc-700/50 bg-zinc-800/50 p-5">
          <h3 className="text-sm font-medium text-zinc-300">Cost Projection</h3>

          {estimation ? (
            <>
              {/* KPI Cards */}
              <div className="grid grid-cols-2 gap-3">
                <div className="rounded-lg border border-zinc-700/50 bg-zinc-900/50 p-3">
                  <div className="flex items-center gap-1.5 text-xs text-zinc-400">
                    <DollarSign className="h-3 w-3" />
                    Cost / Request
                  </div>
                  <div className="mt-1 text-lg font-semibold text-white">
                    ${estimation.costPerRequest < 0.01
                      ? estimation.costPerRequest.toFixed(6)
                      : estimation.costPerRequest.toFixed(4)}
                  </div>
                </div>
                <div className="rounded-lg border border-zinc-700/50 bg-zinc-900/50 p-3">
                  <div className="flex items-center gap-1.5 text-xs text-zinc-400">
                    <Clock className="h-3 w-3" />
                    Daily Requests
                  </div>
                  <div className="mt-1 text-lg font-semibold text-white">
                    {estimation.totalDailyRequests.toLocaleString()}
                  </div>
                </div>
                <div className="rounded-lg border border-zinc-700/50 bg-zinc-900/50 p-3">
                  <div className="flex items-center gap-1.5 text-xs text-zinc-400">
                    <TrendingUp className="h-3 w-3" />
                    Daily Cost
                  </div>
                  <div className="mt-1 text-lg font-semibold text-emerald-400">
                    ${estimation.dailyProjection.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </div>
                </div>
                <div className="rounded-lg border border-zinc-700/50 bg-zinc-900/50 p-3">
                  <div className="flex items-center gap-1.5 text-xs text-zinc-400">
                    <DollarSign className="h-3 w-3" />
                    Monthly Cost
                  </div>
                  <div
                    className={`mt-1 text-lg font-semibold ${
                      estimation.monthlyProjection > 1000 ? 'text-amber-400' : 'text-emerald-400'
                    }`}
                  >
                    ${estimation.monthlyProjection.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </div>
                </div>
              </div>

              {/* Model Info */}
              <div className="rounded-lg border border-zinc-700/50 bg-zinc-900/50 p-3 text-xs text-zinc-400">
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <span className="text-zinc-500">Provider:</span> {estimation.provider}
                  </div>
                  <div>
                    <span className="text-zinc-500">Model:</span> {estimation.modelId}
                  </div>
                  <div>
                    <span className="text-zinc-500">Input/1M:</span> ${estimation.inputCostPer1MTokens.toFixed(3)}
                  </div>
                  <div>
                    <span className="text-zinc-500">Output/1M:</span> ${estimation.outputCostPer1MTokens.toFixed(3)}
                  </div>
                </div>
              </div>

              {/* Warnings */}
              {estimation.warnings.length > 0 && (
                <div className="space-y-1.5">
                  {estimation.warnings.map((warning, i) => (
                    <div
                      key={`warning-${i}`}
                      className="flex items-start gap-2 rounded-lg border border-amber-500/20 bg-amber-500/10 p-2.5 text-xs text-amber-300"
                    >
                      <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" />
                      {warning}
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
                    className="flex-1 rounded-lg border border-zinc-600 bg-zinc-700/50 px-3 py-2 text-sm text-white placeholder:text-zinc-500 focus:border-indigo-500 focus:outline-none"
                  />
                  <button
                    onClick={handleSave}
                    disabled={!saveName.trim()}
                    className="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-indigo-500 disabled:opacity-50"
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
              Configure parameters and click &quot;Estimate Cost&quot; to see projections
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
                  <th className="pb-2 pr-4 font-medium">Strategy</th>
                  <th className="pb-2 pr-4 font-medium">Model</th>
                  <th className="pb-2 pr-4 text-right font-medium">Monthly</th>
                  <th className="pb-2 pr-4 text-right font-medium">Daily Req</th>
                  <th className="pb-2 font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {scenarios.map((scenario) => (
                  <tr key={scenario.id} className="border-b border-zinc-700/30">
                    <td className="py-2.5 pr-4 font-medium text-white">{scenario.name}</td>
                    <td className="py-2.5 pr-4 text-zinc-400">
                      {RAG_STRATEGY_LABELS[scenario.strategy as RagStrategy] ?? scenario.strategy}
                    </td>
                    <td className="py-2.5 pr-4 text-zinc-400">
                      {AVAILABLE_MODELS.find((m) => m.id === scenario.modelId)?.label ?? scenario.modelId}
                    </td>
                    <td
                      className={`py-2.5 pr-4 text-right font-mono ${
                        scenario.monthlyProjection > 1000 ? 'text-amber-400' : 'text-emerald-400'
                      }`}
                    >
                      ${scenario.monthlyProjection.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                    </td>
                    <td className="py-2.5 pr-4 text-right font-mono text-zinc-400">
                      {(scenario.messagesPerDay * scenario.activeUsers).toLocaleString()}
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
