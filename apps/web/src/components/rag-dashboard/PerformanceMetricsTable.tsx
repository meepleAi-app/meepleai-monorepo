'use client';

/**
 * PerformanceMetricsTable Component
 *
 * Comprehensive performance metrics display for TOMAC-RAG system.
 * Shows latency, throughput, accuracy, and resource consumption
 * across strategies and query types.
 */

import React, { useState } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Timer,
  Zap,
  Target,
  TrendingUp,
  BarChart3,
  Server,
  Activity,
  Gauge,
  ChevronDown,
  ArrowUpRight,
  ArrowDownRight,
  Minus,
} from 'lucide-react';

import type { RagStrategy, QueryTemplate } from './types';

// =============================================================================
// Types
// =============================================================================

interface PerformanceMetric {
  id: string;
  name: string;
  description: string;
  unit: string;
  icon: React.ReactNode;
  strategies: {
    FAST: number;
    BALANCED: number;
    PRECISE: number;
    EXPERT: number;
    CONSENSUS: number;
    CUSTOM: number;
  };
  baseline: number;
  trend: 'up' | 'down' | 'stable';
  trendIsGood: boolean;
}

interface QueryTypeMetrics {
  template: QueryTemplate;
  label: string;
  avgLatencyMs: number;
  avgTokens: number;
  cacheHitRate: number;
  accuracy: number;
  volume: number; // percentage of total queries
}

interface SystemHealthMetric {
  name: string;
  value: number;
  max: number;
  unit: string;
  status: 'healthy' | 'warning' | 'critical';
}

// =============================================================================
// Mock Data
// =============================================================================

const PERFORMANCE_METRICS: PerformanceMetric[] = [
  {
    id: 'latency-p50',
    name: 'P50 Latency',
    description: 'Median response time',
    unit: 'ms',
    icon: <Timer className="h-4 w-4" />,
    strategies: { FAST: 180, BALANCED: 450, PRECISE: 1200, EXPERT: 2500, CONSENSUS: 3500, CUSTOM: 800 },
    baseline: 500,
    trend: 'down',
    trendIsGood: true,
  },
  {
    id: 'latency-p95',
    name: 'P95 Latency',
    description: '95th percentile response time',
    unit: 'ms',
    icon: <Timer className="h-4 w-4" />,
    strategies: { FAST: 350, BALANCED: 850, PRECISE: 2800, EXPERT: 5000, CONSENSUS: 7000, CUSTOM: 1500 },
    baseline: 1000,
    trend: 'down',
    trendIsGood: true,
  },
  {
    id: 'throughput',
    name: 'Throughput',
    description: 'Queries processed per second',
    unit: 'qps',
    icon: <Zap className="h-4 w-4" />,
    strategies: { FAST: 120, BALANCED: 45, PRECISE: 12, EXPERT: 8, CONSENSUS: 5, CUSTOM: 30 },
    baseline: 50,
    trend: 'up',
    trendIsGood: true,
  },
  {
    id: 'accuracy',
    name: 'Accuracy',
    description: 'Correct answer rate',
    unit: '%',
    icon: <Target className="h-4 w-4" />,
    strategies: { FAST: 78, BALANCED: 89, PRECISE: 96, EXPERT: 94, CONSENSUS: 98, CUSTOM: 85 },
    baseline: 85,
    trend: 'up',
    trendIsGood: true,
  },
  {
    id: 'citation-rate',
    name: 'Citation Rate',
    description: 'Responses with valid citations',
    unit: '%',
    icon: <Target className="h-4 w-4" />,
    strategies: { FAST: 65, BALANCED: 88, PRECISE: 98, EXPERT: 95, CONSENSUS: 99, CUSTOM: 80 },
    baseline: 80,
    trend: 'up',
    trendIsGood: true,
  },
  {
    id: 'token-efficiency',
    name: 'Token Efficiency',
    description: 'Tokens saved vs naive RAG',
    unit: '%',
    icon: <TrendingUp className="h-4 w-4" />,
    strategies: { FAST: 45, BALANCED: 35, PRECISE: 20, EXPERT: 15, CONSENSUS: 10, CUSTOM: 30 },
    baseline: 30,
    trend: 'up',
    trendIsGood: true,
  },
  {
    id: 'cache-hit',
    name: 'Cache Hit Rate',
    description: 'Queries served from cache',
    unit: '%',
    icon: <Server className="h-4 w-4" />,
    strategies: { FAST: 85, BALANCED: 75, PRECISE: 60, EXPERT: 40, CONSENSUS: 35, CUSTOM: 65 },
    baseline: 70,
    trend: 'up',
    trendIsGood: true,
  },
  {
    id: 'error-rate',
    name: 'Error Rate',
    description: 'Failed queries percentage',
    unit: '%',
    icon: <Activity className="h-4 w-4" />,
    strategies: { FAST: 2.5, BALANCED: 1.2, PRECISE: 0.5, EXPERT: 0.8, CONSENSUS: 0.3, CUSTOM: 1.5 },
    baseline: 2.0,
    trend: 'down',
    trendIsGood: true,
  },
];

const QUERY_TYPE_METRICS: QueryTypeMetrics[] = [
  {
    template: 'rule_lookup',
    label: 'Rule Lookup',
    avgLatencyMs: 280,
    avgTokens: 850,
    cacheHitRate: 92,
    accuracy: 95,
    volume: 45,
  },
  {
    template: 'setup_guide',
    label: 'Setup Guide',
    avgLatencyMs: 420,
    avgTokens: 1100,
    cacheHitRate: 78,
    accuracy: 91,
    volume: 20,
  },
  {
    template: 'strategy_advice',
    label: 'Strategy Advice',
    avgLatencyMs: 1100,
    avgTokens: 1800,
    cacheHitRate: 45,
    accuracy: 88,
    volume: 15,
  },
  {
    template: 'resource_planning',
    label: 'Resource Planning',
    avgLatencyMs: 650,
    avgTokens: 1400,
    cacheHitRate: 65,
    accuracy: 93,
    volume: 12,
  },
  {
    template: 'educational',
    label: 'Educational',
    avgLatencyMs: 850,
    avgTokens: 2100,
    cacheHitRate: 55,
    accuracy: 90,
    volume: 8,
  },
];

const SYSTEM_HEALTH: SystemHealthMetric[] = [
  { name: 'Vector DB Load', value: 42, max: 100, unit: '%', status: 'healthy' },
  { name: 'Cache Memory', value: 2.8, max: 4, unit: 'GB', status: 'healthy' },
  { name: 'API Rate Limit', value: 65, max: 100, unit: '%', status: 'warning' },
  { name: 'Embedding Queue', value: 12, max: 100, unit: 'items', status: 'healthy' },
];

// =============================================================================
// Helper Components
// =============================================================================

function TrendIndicator({
  trend,
  isGood,
}: {
  trend: 'up' | 'down' | 'stable';
  isGood: boolean;
}) {
  const color = isGood ? 'text-green-500' : 'text-red-500';

  if (trend === 'up') {
    return <ArrowUpRight className={`h-4 w-4 ${color}`} />;
  }
  if (trend === 'down') {
    return <ArrowDownRight className={`h-4 w-4 ${color}`} />;
  }
  return <Minus className="h-4 w-4 text-muted-foreground" />;
}

function MetricBar({
  value,
  max,
  color = 'primary',
}: {
  value: number;
  max: number;
  color?: 'primary' | 'green' | 'yellow' | 'red';
}) {
  const percentage = Math.min((value / max) * 100, 100);
  const colorClasses = {
    primary: 'bg-primary',
    green: 'bg-green-500',
    yellow: 'bg-yellow-500',
    red: 'bg-red-500',
  };

  return (
    <div className="h-2 bg-muted rounded-full overflow-hidden">
      <motion.div
        className={`h-full ${colorClasses[color]}`}
        initial={{ width: 0 }}
        animate={{ width: `${percentage}%` }}
        transition={{ duration: 0.5, ease: 'easeOut' }}
      />
    </div>
  );
}

function HealthIndicator({ status }: { status: 'healthy' | 'warning' | 'critical' }) {
  const colors = {
    healthy: 'bg-green-500',
    warning: 'bg-yellow-500',
    critical: 'bg-red-500',
  };

  return (
    <span className="relative flex h-2 w-2">
      <span
        className={`animate-ping absolute inline-flex h-full w-full rounded-full ${colors[status]} opacity-75`}
      />
      <span className={`relative inline-flex rounded-full h-2 w-2 ${colors[status]}`} />
    </span>
  );
}

// =============================================================================
// Strategy Comparison Table
// =============================================================================

interface StrategyTableProps {
  metrics: PerformanceMetric[];
  selectedStrategy: RagStrategy | 'all';
  onStrategyChange: (strategy: RagStrategy | 'all') => void;
}

function StrategyComparisonTable({
  metrics,
  selectedStrategy,
  onStrategyChange,
}: StrategyTableProps) {
  const strategies: (RagStrategy | 'all')[] = ['all', 'FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'];

  return (
    <div className="space-y-4">
      {/* Strategy Filter */}
      <div className="flex flex-wrap gap-2">
        {strategies.map(strategy => (
          <button
            key={strategy}
            onClick={() => onStrategyChange(strategy)}
            className={`px-3 py-1.5 rounded-lg text-sm font-medium transition-all ${
              selectedStrategy === strategy
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted hover:bg-muted/80 text-muted-foreground'
            }`}
          >
            {strategy === 'all' ? 'All Strategies' : strategy}
          </button>
        ))}
      </div>

      {/* Table */}
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="border-b border-border">
              <th className="text-left py-3 px-4 text-sm font-medium text-muted-foreground">
                Metric
              </th>
              {(selectedStrategy === 'all'
                ? (['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'] as RagStrategy[])
                : [selectedStrategy as RagStrategy]
              ).map(s => (
                <th
                  key={s}
                  className="text-right py-3 px-4 text-sm font-medium text-muted-foreground"
                >
                  {s}
                </th>
              ))}
              <th className="text-right py-3 px-4 text-sm font-medium text-muted-foreground">
                Baseline
              </th>
              <th className="text-center py-3 px-4 text-sm font-medium text-muted-foreground">
                Trend
              </th>
            </tr>
          </thead>
          <tbody>
            {metrics.map((metric, idx) => (
              <motion.tr
                key={metric.id}
                initial={{ opacity: 0, x: -10 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: idx * 0.05 }}
                className="border-b border-border/50 hover:bg-muted/30"
              >
                <td className="py-3 px-4">
                  <div className="flex items-center gap-2">
                    <span className="text-primary">{metric.icon}</span>
                    <div>
                      <div className="text-sm font-medium">{metric.name}</div>
                      <div className="text-xs text-muted-foreground">
                        {metric.description}
                      </div>
                    </div>
                  </div>
                </td>
                {(selectedStrategy === 'all'
                  ? (['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'] as RagStrategy[])
                  : [selectedStrategy as RagStrategy]
                ).map(s => (
                  <td key={s} className="text-right py-3 px-4 font-mono text-sm">
                    {metric.strategies[s]}
                    <span className="text-muted-foreground ml-1">{metric.unit}</span>
                  </td>
                ))}
                <td className="text-right py-3 px-4 font-mono text-sm text-muted-foreground">
                  {metric.baseline}
                  <span className="ml-1">{metric.unit}</span>
                </td>
                <td className="text-center py-3 px-4">
                  <div className="flex justify-center">
                    <TrendIndicator trend={metric.trend} isGood={metric.trendIsGood} />
                  </div>
                </td>
              </motion.tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

// =============================================================================
// Query Type Performance
// =============================================================================

function QueryTypePerformance({ metrics }: { metrics: QueryTypeMetrics[] }) {
  const [expanded, setExpanded] = useState<string | null>(null);

  return (
    <div className="space-y-3">
      {metrics.map((metric, idx) => (
        <motion.div
          key={metric.template}
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: idx * 0.1 }}
          className="border border-border rounded-lg overflow-hidden"
        >
          {/* Header */}
          <button
            onClick={() => setExpanded(expanded === metric.template ? null : metric.template)}
            className="w-full flex items-center justify-between p-4 hover:bg-muted/30 transition-colors"
          >
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center">
                <BarChart3 className="h-5 w-5 text-primary" />
              </div>
              <div className="text-left">
                <div className="font-medium">{metric.label}</div>
                <div className="text-xs text-muted-foreground">
                  {metric.volume}% of queries
                </div>
              </div>
            </div>

            <div className="flex items-center gap-6">
              <div className="text-right hidden sm:block">
                <div className="text-sm font-mono">{metric.avgLatencyMs}ms</div>
                <div className="text-xs text-muted-foreground">avg latency</div>
              </div>
              <div className="text-right hidden sm:block">
                <div className="text-sm font-mono">{metric.accuracy}%</div>
                <div className="text-xs text-muted-foreground">accuracy</div>
              </div>
              <ChevronDown
                className={`h-5 w-5 text-muted-foreground transition-transform ${
                  expanded === metric.template ? 'rotate-180' : ''
                }`}
              />
            </div>
          </button>

          {/* Expanded Details */}
          <AnimatePresence>
            {expanded === metric.template && (
              <motion.div
                initial={{ height: 0, opacity: 0 }}
                animate={{ height: 'auto', opacity: 1 }}
                exit={{ height: 0, opacity: 0 }}
                transition={{ duration: 0.2 }}
                className="border-t border-border bg-muted/20"
              >
                <div className="p-4 grid grid-cols-2 md:grid-cols-4 gap-4">
                  <div>
                    <div className="text-xs text-muted-foreground mb-1">Avg Latency</div>
                    <div className="font-mono text-lg">{metric.avgLatencyMs}ms</div>
                    <MetricBar value={metric.avgLatencyMs} max={2000} color="primary" />
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground mb-1">Avg Tokens</div>
                    <div className="font-mono text-lg">{metric.avgTokens}</div>
                    <MetricBar value={metric.avgTokens} max={3000} color="yellow" />
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground mb-1">Cache Hit Rate</div>
                    <div className="font-mono text-lg">{metric.cacheHitRate}%</div>
                    <MetricBar value={metric.cacheHitRate} max={100} color="green" />
                  </div>
                  <div>
                    <div className="text-xs text-muted-foreground mb-1">Accuracy</div>
                    <div className="font-mono text-lg">{metric.accuracy}%</div>
                    <MetricBar value={metric.accuracy} max={100} color="green" />
                  </div>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </motion.div>
      ))}
    </div>
  );
}

// =============================================================================
// System Health Dashboard
// =============================================================================

function SystemHealth({ metrics }: { metrics: SystemHealthMetric[] }) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
      {metrics.map((metric, idx) => (
        <motion.div
          key={metric.name}
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ delay: idx * 0.1 }}
          className="p-4 rounded-xl border border-border bg-card"
        >
          <div className="flex items-center justify-between mb-2">
            <span className="text-sm text-muted-foreground">{metric.name}</span>
            <HealthIndicator status={metric.status} />
          </div>
          <div className="flex items-baseline gap-1 mb-2">
            <span className="text-2xl font-bold font-mono">{metric.value}</span>
            <span className="text-sm text-muted-foreground">
              / {metric.max} {metric.unit}
            </span>
          </div>
          <MetricBar
            value={metric.value}
            max={metric.max}
            color={
              metric.status === 'healthy'
                ? 'green'
                : metric.status === 'warning'
                  ? 'yellow'
                  : 'red'
            }
          />
        </motion.div>
      ))}
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

export function PerformanceMetricsTable() {
  const [selectedStrategy, setSelectedStrategy] = useState<RagStrategy | 'all'>('all');
  const [activeTab, setActiveTab] = useState<'strategy' | 'query' | 'health'>('strategy');

  const tabs = [
    { id: 'strategy', label: 'Strategy Comparison', icon: <BarChart3 className="h-4 w-4" /> },
    { id: 'query', label: 'Query Types', icon: <Activity className="h-4 w-4" /> },
    { id: 'health', label: 'System Health', icon: <Gauge className="h-4 w-4" /> },
  ] as const;

  return (
    <div className="space-y-6">
      {/* Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          className="p-4 rounded-xl border border-green-500/20 bg-gradient-to-br from-green-500/10 to-green-500/5"
        >
          <div className="flex items-center gap-2 text-green-600 dark:text-green-400 mb-1">
            <Timer className="h-4 w-4" />
            <span className="text-sm font-medium">Avg Latency</span>
          </div>
          <div className="text-2xl font-bold font-mono">450ms</div>
          <div className="text-xs text-muted-foreground">-18% vs last week</div>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="p-4 rounded-xl border border-blue-500/20 bg-gradient-to-br from-blue-500/10 to-blue-500/5"
        >
          <div className="flex items-center gap-2 text-blue-600 dark:text-blue-400 mb-1">
            <Target className="h-4 w-4" />
            <span className="text-sm font-medium">Accuracy</span>
          </div>
          <div className="text-2xl font-bold font-mono">89%</div>
          <div className="text-xs text-muted-foreground">+3% vs last week</div>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="p-4 rounded-xl border border-purple-500/20 bg-gradient-to-br from-purple-500/10 to-purple-500/5"
        >
          <div className="flex items-center gap-2 text-purple-600 dark:text-purple-400 mb-1">
            <Zap className="h-4 w-4" />
            <span className="text-sm font-medium">Throughput</span>
          </div>
          <div className="text-2xl font-bold font-mono">45 qps</div>
          <div className="text-xs text-muted-foreground">+12% vs last week</div>
        </motion.div>

        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3 }}
          className="p-4 rounded-xl border border-orange-500/20 bg-gradient-to-br from-orange-500/10 to-orange-500/5"
        >
          <div className="flex items-center gap-2 text-orange-600 dark:text-orange-400 mb-1">
            <Server className="h-4 w-4" />
            <span className="text-sm font-medium">Cache Hit</span>
          </div>
          <div className="text-2xl font-bold font-mono">80%</div>
          <div className="text-xs text-muted-foreground">+5% vs last week</div>
        </motion.div>
      </div>

      {/* Tabs */}
      <div className="flex gap-2 border-b border-border pb-2">
        {tabs.map(tab => (
          <button
            key={tab.id}
            onClick={() => setActiveTab(tab.id)}
            className={`flex items-center gap-2 px-4 py-2 rounded-t-lg text-sm font-medium transition-all ${
              activeTab === tab.id
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:text-foreground hover:bg-muted'
            }`}
          >
            {tab.icon}
            <span className="hidden sm:inline">{tab.label}</span>
          </button>
        ))}
      </div>

      {/* Tab Content */}
      <AnimatePresence mode="wait">
        {activeTab === 'strategy' && (
          <motion.div
            key="strategy"
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
          >
            <StrategyComparisonTable
              metrics={PERFORMANCE_METRICS}
              selectedStrategy={selectedStrategy}
              onStrategyChange={setSelectedStrategy}
            />
          </motion.div>
        )}

        {activeTab === 'query' && (
          <motion.div
            key="query"
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
          >
            <QueryTypePerformance metrics={QUERY_TYPE_METRICS} />
          </motion.div>
        )}

        {activeTab === 'health' && (
          <motion.div
            key="health"
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
          >
            <SystemHealth metrics={SYSTEM_HEALTH} />
          </motion.div>
        )}
      </AnimatePresence>

      {/* Legend */}
      <div className="flex flex-wrap gap-4 text-xs text-muted-foreground pt-4 border-t border-border">
        <div className="flex items-center gap-2">
          <ArrowUpRight className="h-3 w-3 text-green-500" />
          <span>Improving (good)</span>
        </div>
        <div className="flex items-center gap-2">
          <ArrowDownRight className="h-3 w-3 text-green-500" />
          <span>Decreasing (good for errors/latency)</span>
        </div>
        <div className="flex items-center gap-2">
          <Minus className="h-3 w-3" />
          <span>Stable</span>
        </div>
        <div className="flex items-center gap-2">
          <HealthIndicator status="healthy" />
          <span>Healthy</span>
        </div>
        <div className="flex items-center gap-2">
          <HealthIndicator status="warning" />
          <span>Warning</span>
        </div>
      </div>
    </div>
  );
}
