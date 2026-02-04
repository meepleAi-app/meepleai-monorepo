'use client';

/**
 * CostCalculator Component
 *
 * Advanced cost projection tool with interactive sliders.
 * Features: queries/month, cache rate, tier distribution, comparison charts.
 */

import React, { useState, useMemo, useCallback } from 'react';

import { motion } from 'framer-motion';
import { Calculator, TrendingDown, Download, PieChart, BarChart3 } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import { MODEL_PRICING, STRATEGIES } from './rag-data';

import type { CostProjection, RagStrategy } from './types';

// =============================================================================
// Cost Constants - Derived from rag-data.ts (Single Source of Truth)
// See: docs/03-api/rag/appendix/E-model-pricing-2026.md
// =============================================================================

/**
 * NOTE: User tier does NOT affect cost calculations.
 * Cost is determined ONLY by strategy and model selection.
 * Tier affects: access control, cache TTL, max strategy level.
 * @see docs/03-api/rag/02-layer1-routing.md
 */

/**
 * Get model pricing from Single Source of Truth
 */
function getModelPricing(modelId: string): { input: number; output: number } {
  const model = MODEL_PRICING.find((m) => m.id === modelId);
  if (!model) return { input: 0, output: 0 };
  return { input: model.inputCost, output: model.outputCost };
}

/**
 * Strategy cost derived from rag-data.ts MODEL_PRICING
 * Uses primary model for each strategy
 */
const STRATEGY_COSTS: Record<RagStrategy, { input: number; output: number }> = {
  FAST: getModelPricing('llama-3.3-70b'), // Free via OpenRouter
  BALANCED: getModelPricing('deepseek-chat'), // DeepSeek Chat
  PRECISE: { input: 3, output: 15 }, // Weighted avg (Haiku + Sonnet + Opus)
  EXPERT: getModelPricing('claude-sonnet-4.5'), // Claude Sonnet 4.5
  CONSENSUS: { input: 2.5, output: 10 }, // Weighted avg (Sonnet + GPT-4o + DeepSeek)
  CUSTOM: getModelPricing('claude-haiku-4.5'), // Claude Haiku 4.5
};

/**
 * Base tokens per strategy - derived from STRATEGIES in rag-data.ts
 */
const BASE_TOKENS: Record<RagStrategy, number> = {
  FAST: STRATEGIES.FAST.tokens,
  BALANCED: STRATEGIES.BALANCED.tokens,
  PRECISE: STRATEGIES.PRECISE.tokens,
  EXPERT: STRATEGIES.EXPERT.tokens,
  CONSENSUS: STRATEGIES.CONSENSUS.tokens,
  CUSTOM: STRATEGIES.CUSTOM.tokens,
};

// =============================================================================
// Slider Component
// =============================================================================

interface SliderProps {
  label: string;
  value: number;
  min: number;
  max: number;
  step: number;
  format?: (value: number) => string;
  onChange: (value: number) => void;
}

function Slider({ label, value, min, max, step, format, onChange }: SliderProps) {
  return (
    <div className="rag-slider-container">
      <div className="rag-slider-header">
        <span className="rag-slider-label">{label}</span>
        <span className="rag-slider-value">{format ? format(value) : value}</span>
      </div>
      <input
        type="range"
        className="rag-slider"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={e => onChange(Number(e.target.value))}
      />
    </div>
  );
}

// =============================================================================
// Cost Breakdown Bar
// =============================================================================

interface CostBarProps {
  label: string;
  value: number;
  total: number;
  color: string;
}

function CostBar({ label, value, total, color }: CostBarProps) {
  const percentage = (value / total) * 100;

  return (
    <div className="space-y-1">
      <div className="flex justify-between text-sm">
        <span className="text-muted-foreground">{label}</span>
        <span className="font-mono">${value.toFixed(2)}</span>
      </div>
      <div className="h-3 bg-muted rounded-full overflow-hidden">
        <motion.div
          className="h-full rounded-full"
          style={{ backgroundColor: color }}
          initial={{ width: 0 }}
          animate={{ width: `${percentage}%` }}
          transition={{ duration: 0.5, ease: 'easeOut' }}
        />
      </div>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface CostCalculatorProps {
  className?: string;
}

export function CostCalculator({ className }: CostCalculatorProps) {
  // State for sliders
  const [queriesPerMonth, setQueriesPerMonth] = useState(100000);
  const [cacheHitRate, setCacheHitRate] = useState(80);

  // Strategy distribution (must sum to 100)
  const [fastPercent, setFastPercent] = useState(70);
  const [balancedPercent, setBalancedPercent] = useState(25);
  const precisePercent = 100 - fastPercent - balancedPercent;

  // Calculate costs
  const projection = useMemo<CostProjection>(() => {
    const cacheMissRate = 1 - cacheHitRate / 100;

    const strategyDist: Record<RagStrategy, number> = {
      FAST: fastPercent / 100,
      BALANCED: balancedPercent / 100,
      PRECISE: Math.max(0, precisePercent) / 100,
      EXPERT: 0.02, // 2% estimated
      CONSENSUS: 0.01, // 1% estimated
      CUSTOM: 0.005, // 0.5% estimated
    };

    let totalLlmCost = 0;

    // Calculate LLM costs per strategy
    for (const [strategy, dist] of Object.entries(strategyDist) as [RagStrategy, number][]) {
      const queries = queriesPerMonth * dist * cacheMissRate;
      const tokens = BASE_TOKENS[strategy];
      const inputTokens = tokens * 0.7;
      const outputTokens = tokens * 0.3;

      const inputCost = (inputTokens * queries * STRATEGY_COSTS[strategy].input) / 1_000_000;
      const outputCost = (outputTokens * queries * STRATEGY_COSTS[strategy].output) / 1_000_000;

      totalLlmCost += inputCost + outputCost;
    }

    // Embeddings cost (approximately $0.0001 per query for embedding generation)
    const embeddingCost = queriesPerMonth * cacheMissRate * 0.0005;

    // Infrastructure (fixed + variable)
    const infraCost = 50 + queriesPerMonth * 0.00025;

    // Cache savings calculation
    const naiveCost = queriesPerMonth * 2000 * 0.000003; // Naive: 2000 tokens at $3/M
    const cacheSavings = (cacheHitRate / 100) * naiveCost;
    const optimizationSavings = naiveCost * 0.35; // 35% token reduction

    return {
      queriesPerMonth,
      cacheHitRate,
      // NOTE: Tier distribution is for access control info only.
      // Cost is determined by STRATEGY, not by user tier.
      tierDistribution: {
        Anonymous: 0, // NO ACCESS
        User: 0.55,
        Editor: 0.25,
        Admin: 0.12,
        Premium: 0.08,
      },
      strategyDistribution: strategyDist,
      costs: {
        llm: totalLlmCost,
        embeddings: embeddingCost,
        infrastructure: infraCost,
        total: totalLlmCost + embeddingCost + infraCost,
      },
      savings: {
        fromCache: cacheSavings,
        fromOptimization: optimizationSavings,
        total: cacheSavings + optimizationSavings,
      },
      perQueryCost: (totalLlmCost + embeddingCost + infraCost) / queriesPerMonth,
    };
  }, [queriesPerMonth, cacheHitRate, fastPercent, balancedPercent, precisePercent]);

  const handleExport = useCallback(() => {
    const report = {
      generatedAt: new Date().toISOString(),
      parameters: {
        queriesPerMonth,
        cacheHitRate,
        strategyDistribution: {
          FAST: `${fastPercent}%`,
          BALANCED: `${balancedPercent}%`,
          PRECISE: `${precisePercent}%`,
          EXPERT: '2%',
          CONSENSUS: '1%',
          CUSTOM: '0.5%',
        },
      },
      costs: {
        llm: `$${projection.costs.llm.toFixed(2)}`,
        embeddings: `$${projection.costs.embeddings.toFixed(2)}`,
        infrastructure: `$${projection.costs.infrastructure.toFixed(2)}`,
        total: `$${projection.costs.total.toFixed(2)}`,
      },
      savings: {
        fromCache: `$${projection.savings.fromCache.toFixed(2)}`,
        fromOptimization: `$${projection.savings.fromOptimization.toFixed(2)}`,
        total: `$${projection.savings.total.toFixed(2)}`,
      },
      perQueryCost: `$${projection.perQueryCost.toFixed(6)}`,
    };

    const blob = new Blob([JSON.stringify(report, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `rag-cost-report-${new Date().toISOString().split('T')[0]}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }, [queriesPerMonth, cacheHitRate, fastPercent, balancedPercent, precisePercent, projection]);

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="flex items-center gap-2">
            <Calculator className="h-5 w-5 text-primary" />
            Cost Calculator
          </CardTitle>
          <Button variant="outline" size="sm" onClick={handleExport}>
            <Download className="h-4 w-4 mr-2" />
            Export
          </Button>
        </div>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Sliders Section */}
        <div className="grid md:grid-cols-2 gap-6">
          {/* Left Column - Input Parameters */}
          <div className="space-y-6">
            <Slider
              label="Queries per Month"
              value={queriesPerMonth}
              min={1000}
              max={1000000}
              step={1000}
              format={v => v.toLocaleString()}
              onChange={setQueriesPerMonth}
            />

            <Slider
              label="Cache Hit Rate"
              value={cacheHitRate}
              min={0}
              max={100}
              step={5}
              format={v => `${v}%`}
              onChange={setCacheHitRate}
            />

            <div className="pt-4 border-t border-border">
              <div className="text-sm font-medium mb-4 flex items-center gap-2">
                <PieChart className="h-4 w-4" />
                Strategy Distribution
              </div>

              <Slider
                label="FAST Strategy"
                value={fastPercent}
                min={0}
                max={100 - balancedPercent}
                step={5}
                format={v => `${v}%`}
                onChange={setFastPercent}
              />

              <div className="mt-4">
                <Slider
                  label="BALANCED Strategy"
                  value={balancedPercent}
                  min={0}
                  max={100 - fastPercent}
                  step={5}
                  format={v => `${v}%`}
                  onChange={setBalancedPercent}
                />
              </div>

              <div className="mt-4 flex items-center justify-between text-sm">
                <span className="text-muted-foreground">PRECISE Strategy</span>
                <span className="font-mono text-purple-500">{Math.max(0, precisePercent)}%</span>
              </div>
            </div>
          </div>

          {/* Right Column - Results */}
          <div className="space-y-6">
            {/* Total Cost Card */}
            <div className="p-6 rounded-xl bg-gradient-to-br from-primary/10 to-primary/5 border border-primary/20">
              <div className="text-sm text-muted-foreground mb-1">Estimated Monthly Cost</div>
              <div className="text-4xl font-bold font-quicksand text-primary">
                ${projection.costs.total.toFixed(2)}
              </div>
              <div className="text-sm text-muted-foreground mt-2">
                ${(projection.perQueryCost * 1000).toFixed(4)} per 1K queries
              </div>
            </div>

            {/* Cost Breakdown */}
            <div className="space-y-4">
              <div className="text-sm font-medium flex items-center gap-2">
                <BarChart3 className="h-4 w-4" />
                Cost Breakdown
              </div>

              <CostBar
                label="LLM Inference"
                value={projection.costs.llm}
                total={projection.costs.total}
                color="hsl(221, 83%, 53%)"
              />

              <CostBar
                label="Embeddings"
                value={projection.costs.embeddings}
                total={projection.costs.total}
                color="hsl(262, 83%, 62%)"
              />

              <CostBar
                label="Infrastructure"
                value={projection.costs.infrastructure}
                total={projection.costs.total}
                color="hsl(25, 95%, 53%)"
              />
            </div>

            {/* Savings */}
            <div className="p-4 rounded-lg bg-green-500/10 border border-green-500/20">
              <div className="flex items-center gap-2 text-green-600 dark:text-green-400 mb-2">
                <TrendingDown className="h-4 w-4" />
                <span className="font-semibold">Estimated Savings</span>
              </div>
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <div className="text-muted-foreground">From Cache</div>
                  <div className="font-mono font-semibold text-green-600 dark:text-green-400">
                    ${projection.savings.fromCache.toFixed(2)}
                  </div>
                </div>
                <div>
                  <div className="text-muted-foreground">From Optimization</div>
                  <div className="font-mono font-semibold text-green-600 dark:text-green-400">
                    ${projection.savings.fromOptimization.toFixed(2)}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
