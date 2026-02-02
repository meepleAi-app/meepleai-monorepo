'use client';

/**
 * ModelSelectionOptimizer Component
 *
 * Pricing comparison and recommendations for LLM model selection.
 * Shows cost/quality tradeoffs and token efficiency metrics.
 */

import React, { useState, useMemo } from 'react';

import { motion } from 'framer-motion';
import {
  Cpu,
  DollarSign,
  Zap,
  Target,
  Scale,
  TrendingDown,
  TrendingUp,
  Star,
  Info,
  ArrowRight,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import type { RagStrategy } from './types';

// =============================================================================
// Types
// =============================================================================

interface ModelInfo {
  id: string;
  name: string;
  provider: string;
  strategy: RagStrategy;
  inputCost: number; // per million tokens
  outputCost: number; // per million tokens
  maxContext: number; // in K tokens
  quality: number; // 1-10 score
  speed: number; // 1-10 score (higher = faster)
  recommended: boolean;
  features: string[];
  bestFor: string[];
}

interface UseCaseRecommendation {
  useCase: string;
  description: string;
  recommendedModel: string;
  alternativeModel: string;
  reasoning: string;
}

// =============================================================================
// Data
// =============================================================================

const MODELS: ModelInfo[] = [
  // FAST Strategy Models (Free)
  {
    id: 'llama-3.3-70b',
    name: 'Llama 3.3 70B',
    provider: 'OpenRouter',
    strategy: 'FAST',
    inputCost: 0,
    outputCost: 0,
    maxContext: 128,
    quality: 7,
    speed: 9,
    recommended: true,
    features: ['Free', 'Open source', 'Fast inference'],
    bestFor: ['Simple queries', 'High volume', 'Cost-sensitive'],
  },
  {
    id: 'gemini-2.0-flash',
    name: 'Gemini 2.0 Flash',
    provider: 'Google',
    strategy: 'FAST',
    inputCost: 0,
    outputCost: 0,
    maxContext: 32,
    quality: 7.5,
    speed: 10,
    recommended: true,
    features: ['Free', 'Very fast', 'Good for simple tasks'],
    bestFor: ['Speed-critical', 'Basic extraction', 'Quick lookups'],
  },
  // BALANCED Strategy Models
  {
    id: 'claude-3.5-sonnet',
    name: 'Claude 3.5 Sonnet',
    provider: 'Anthropic',
    strategy: 'BALANCED',
    inputCost: 3,
    outputCost: 15,
    maxContext: 200,
    quality: 9,
    speed: 7,
    recommended: true,
    features: ['Strong reasoning', 'Large context', 'Reliable'],
    bestFor: ['Complex rules', 'Nuanced queries', 'Quality-focused'],
  },
  {
    id: 'deepseek-chat',
    name: 'DeepSeek Chat',
    provider: 'DeepSeek',
    strategy: 'BALANCED',
    inputCost: 0.14,
    outputCost: 0.28,
    maxContext: 64,
    quality: 8,
    speed: 8,
    recommended: false,
    features: ['Very affordable', 'Good quality', 'Fast'],
    bestFor: ['Budget-conscious', 'Moderate complexity', 'Volume'],
  },
  {
    id: 'gpt-4o-mini',
    name: 'GPT-4o Mini',
    provider: 'OpenAI',
    strategy: 'BALANCED',
    inputCost: 0.15,
    outputCost: 0.6,
    maxContext: 128,
    quality: 8.5,
    speed: 8,
    recommended: false,
    features: ['Affordable', 'Good performance', 'Reliable'],
    bestFor: ['General use', 'Moderate budgets', 'Consistent results'],
  },
  // PRECISE Strategy Models
  {
    id: 'claude-3.5-opus',
    name: 'Claude 3.5 Opus',
    provider: 'Anthropic',
    strategy: 'PRECISE',
    inputCost: 15,
    outputCost: 75,
    maxContext: 200,
    quality: 10,
    speed: 5,
    recommended: true,
    features: ['Best reasoning', 'Highest quality', 'Multi-agent ready'],
    bestFor: ['Critical queries', 'Complex analysis', 'Premium users'],
  },
  {
    id: 'gpt-4o',
    name: 'GPT-4o',
    provider: 'OpenAI',
    strategy: 'PRECISE',
    inputCost: 5,
    outputCost: 15,
    maxContext: 128,
    quality: 9.5,
    speed: 6,
    recommended: false,
    features: ['Excellent quality', 'Good context', 'Reliable'],
    bestFor: ['High-stakes queries', 'Complex tasks', 'Validation'],
  },
  {
    id: 'gemini-1.5-pro',
    name: 'Gemini 1.5 Pro',
    provider: 'Google',
    strategy: 'PRECISE',
    inputCost: 3.5,
    outputCost: 10.5,
    maxContext: 1000,
    quality: 9,
    speed: 6,
    recommended: false,
    features: ['Huge context', 'Good reasoning', 'Multi-modal'],
    bestFor: ['Very long context', 'Document analysis', 'Cost-effective premium'],
  },
];

const USE_CASE_RECOMMENDATIONS: UseCaseRecommendation[] = [
  {
    useCase: 'Simple Rule Lookups',
    description: 'Basic rule clarification questions with clear answers',
    recommendedModel: 'Llama 3.3 70B',
    alternativeModel: 'Gemini 2.0 Flash',
    reasoning: 'Free models handle simple extractions well, no need for premium models',
  },
  {
    useCase: 'Complex Rule Interpretation',
    description: 'Edge cases, ambiguous situations, tournament rulings',
    recommendedModel: 'Claude 3.5 Sonnet',
    alternativeModel: 'GPT-4o Mini',
    reasoning: 'Strong reasoning capabilities needed for nuanced rule interpretation',
  },
  {
    useCase: 'Strategy Analysis',
    description: 'In-depth game state analysis and recommendations',
    recommendedModel: 'Claude 3.5 Opus',
    alternativeModel: 'GPT-4o',
    reasoning: 'Multi-agent pipeline requires best-in-class reasoning for strategy',
  },
  {
    useCase: 'High Volume Operations',
    description: '100K+ queries per month with cost constraints',
    recommendedModel: 'DeepSeek Chat',
    alternativeModel: 'Llama 3.3 70B',
    reasoning: 'Best cost/quality ratio for volume, 100x cheaper than premium',
  },
  {
    useCase: 'Premium User Experience',
    description: 'Best possible quality for paying customers',
    recommendedModel: 'Claude 3.5 Opus',
    alternativeModel: 'Claude 3.5 Sonnet',
    reasoning: 'Highest quality justifies cost for premium tier users',
  },
];

// =============================================================================
// Sub-Components
// =============================================================================

interface ModelCardProps {
  model: ModelInfo;
  isSelected: boolean;
  onClick: () => void;
}

function ModelCard({ model, isSelected, onClick }: ModelCardProps) {
  const strategyColors: Record<RagStrategy, string> = {
    FAST: 'hsl(142 76% 36%)',
    BALANCED: 'hsl(221 83% 53%)',
    PRECISE: 'hsl(262 83% 62%)',
    EXPERT: 'hsl(25 95% 53%)',
    CONSENSUS: 'hsl(330 80% 60%)',
    CUSTOM: 'hsl(0 0% 45%)',
  };

  const color = strategyColors[model.strategy];

  return (
    <motion.div
      className={cn(
        'p-4 rounded-xl border cursor-pointer transition-all',
        'hover:border-primary/50 hover:shadow-md',
        isSelected && 'ring-2 ring-primary border-primary'
      )}
      onClick={onClick}
      whileHover={{ scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
    >
      {/* Header */}
      <div className="flex items-start justify-between mb-3">
        <div>
          <div className="flex items-center gap-2">
            <span className="font-semibold">{model.name}</span>
            {model.recommended && (
              <Star className="h-4 w-4 text-yellow-500 fill-yellow-500" />
            )}
          </div>
          <span className="text-xs text-muted-foreground">{model.provider}</span>
        </div>
        <span
          className="text-xs font-medium px-2 py-1 rounded-full"
          style={{ backgroundColor: `${color}20`, color }}
        >
          {model.strategy}
        </span>
      </div>

      {/* Pricing */}
      <div className="grid grid-cols-2 gap-2 mb-3 text-sm">
        <div className="p-2 rounded-lg bg-muted/50">
          <div className="text-xs text-muted-foreground">Input</div>
          <div className="font-mono font-semibold">
            {model.inputCost === 0 ? (
              <span className="text-green-500">Free</span>
            ) : (
              `$${model.inputCost}/M`
            )}
          </div>
        </div>
        <div className="p-2 rounded-lg bg-muted/50">
          <div className="text-xs text-muted-foreground">Output</div>
          <div className="font-mono font-semibold">
            {model.outputCost === 0 ? (
              <span className="text-green-500">Free</span>
            ) : (
              `$${model.outputCost}/M`
            )}
          </div>
        </div>
      </div>

      {/* Quality/Speed Bars */}
      <div className="space-y-2 mb-3">
        <div className="flex items-center gap-2 text-xs">
          <span className="w-14 text-muted-foreground">Quality</span>
          <div className="flex-1 h-2 bg-muted rounded-full overflow-hidden">
            <div
              className="h-full bg-blue-500 rounded-full"
              style={{ width: `${model.quality * 10}%` }}
            />
          </div>
          <span className="w-8 font-mono">{model.quality}/10</span>
        </div>
        <div className="flex items-center gap-2 text-xs">
          <span className="w-14 text-muted-foreground">Speed</span>
          <div className="flex-1 h-2 bg-muted rounded-full overflow-hidden">
            <div
              className="h-full bg-green-500 rounded-full"
              style={{ width: `${model.speed * 10}%` }}
            />
          </div>
          <span className="w-8 font-mono">{model.speed}/10</span>
        </div>
      </div>

      {/* Features */}
      <div className="flex flex-wrap gap-1">
        {model.features.map(feature => (
          <span
            key={feature}
            className="text-[10px] px-1.5 py-0.5 rounded bg-muted text-muted-foreground"
          >
            {feature}
          </span>
        ))}
      </div>
    </motion.div>
  );
}

interface CostComparisonProps {
  models: ModelInfo[];
  tokensPerQuery: number;
  queriesPerMonth: number;
}

function CostComparison({ models, tokensPerQuery, queriesPerMonth }: CostComparisonProps) {
  const costs = useMemo(() => {
    const inputTokens = tokensPerQuery * 0.7;
    const outputTokens = tokensPerQuery * 0.3;

    return models.map(model => {
      const inputCost = (inputTokens * queriesPerMonth * model.inputCost) / 1_000_000;
      const outputCost = (outputTokens * queriesPerMonth * model.outputCost) / 1_000_000;
      return {
        model,
        monthlyCost: inputCost + outputCost,
      };
    });
  }, [models, tokensPerQuery, queriesPerMonth]);

  const maxCost = Math.max(...costs.map(c => c.monthlyCost));

  return (
    <div className="space-y-3">
      <div className="text-xs font-medium text-muted-foreground flex items-center justify-between">
        <span>Monthly Cost Comparison ({queriesPerMonth.toLocaleString()} queries)</span>
        <span className="font-mono">{tokensPerQuery.toLocaleString()} tokens/query</span>
      </div>
      <div className="space-y-2">
        {costs
          .sort((a, b) => a.monthlyCost - b.monthlyCost)
          .map(({ model, monthlyCost }) => (
            <div key={model.id} className="flex items-center gap-3">
              <span className="w-32 text-sm truncate">{model.name}</span>
              <div className="flex-1 h-6 bg-muted rounded-lg overflow-hidden relative">
                <motion.div
                  className="h-full rounded-lg"
                  style={{
                    backgroundColor:
                      model.strategy === 'FAST'
                        ? 'hsl(142 76% 36%)'
                        : model.strategy === 'BALANCED'
                          ? 'hsl(221 83% 53%)'
                          : model.strategy === 'PRECISE'
                            ? 'hsl(262 83% 62%)'
                            : model.strategy === 'EXPERT'
                              ? 'hsl(25 95% 53%)'
                              : model.strategy === 'CONSENSUS'
                                ? 'hsl(330 80% 60%)'
                                : 'hsl(0 0% 45%)',
                  }}
                  initial={{ width: 0 }}
                  animate={{
                    width: maxCost > 0 ? `${(monthlyCost / maxCost) * 100}%` : '0%',
                  }}
                  transition={{ duration: 0.5, delay: 0.1 }}
                />
                <span className="absolute inset-0 flex items-center justify-end pr-2 text-xs font-mono">
                  {monthlyCost === 0 ? (
                    <span className="text-green-500 font-semibold">Free</span>
                  ) : (
                    `$${monthlyCost.toFixed(2)}`
                  )}
                </span>
              </div>
            </div>
          ))}
      </div>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface ModelSelectionOptimizerProps {
  className?: string;
}

export function ModelSelectionOptimizer({ className }: ModelSelectionOptimizerProps) {
  const [selectedStrategy, setSelectedStrategy] = useState<RagStrategy | 'all'>('all');
  const [selectedModelId, setSelectedModelId] = useState<string | null>(null);
  const [queriesPerMonth, setQueriesPerMonth] = useState(100000);
  const [tokensPerQuery, setTokensPerQuery] = useState(2000);

  const filteredModels = useMemo(() => {
    if (selectedStrategy === 'all') return MODELS;
    return MODELS.filter(m => m.strategy === selectedStrategy);
  }, [selectedStrategy]);

  const selectedModel = MODELS.find(m => m.id === selectedModelId);

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Cpu className="h-5 w-5 text-primary" />
          Model Selection Optimizer
        </CardTitle>
        <p className="text-sm text-muted-foreground mt-1">
          Compare LLM pricing and find the best model for your use case
        </p>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Strategy Filter */}
        <div className="flex flex-wrap gap-2">
          {(['all', 'FAST', 'BALANCED', 'PRECISE'] as const).map(strategy => {
            const icons = {
              all: <Cpu className="h-4 w-4" />,
              FAST: <Zap className="h-4 w-4" />,
              BALANCED: <Scale className="h-4 w-4" />,
              PRECISE: <Target className="h-4 w-4" />,
            };
            return (
              <button
                key={strategy}
                className={cn(
                  'flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all',
                  'border hover:border-primary/50',
                  selectedStrategy === strategy
                    ? 'border-primary bg-primary/10 text-primary'
                    : 'border-border'
                )}
                onClick={() => setSelectedStrategy(strategy)}
              >
                {icons[strategy]}
                {strategy === 'all' ? 'All Models' : strategy}
              </button>
            );
          })}
        </div>

        {/* Model Grid */}
        <div className="grid sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
          {filteredModels.map(model => (
            <ModelCard
              key={model.id}
              model={model}
              isSelected={selectedModelId === model.id}
              onClick={() => setSelectedModelId(model.id === selectedModelId ? null : model.id)}
            />
          ))}
        </div>

        {/* Cost Parameters */}
        <div className="p-4 rounded-xl bg-muted/30 border space-y-4">
          <div className="flex items-center gap-2 text-sm font-medium">
            <DollarSign className="h-4 w-4" />
            Cost Calculator
          </div>
          <div className="grid sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs text-muted-foreground">Queries per Month</label>
              <input
                type="range"
                min={10000}
                max={1000000}
                step={10000}
                value={queriesPerMonth}
                onChange={e => setQueriesPerMonth(Number(e.target.value))}
                className="w-full mt-1"
              />
              <div className="text-right font-mono text-sm">{queriesPerMonth.toLocaleString()}</div>
            </div>
            <div>
              <label className="text-xs text-muted-foreground">Avg Tokens per Query</label>
              <input
                type="range"
                min={500}
                max={10000}
                step={100}
                value={tokensPerQuery}
                onChange={e => setTokensPerQuery(Number(e.target.value))}
                className="w-full mt-1"
              />
              <div className="text-right font-mono text-sm">{tokensPerQuery.toLocaleString()}</div>
            </div>
          </div>

          <CostComparison
            models={filteredModels}
            tokensPerQuery={tokensPerQuery}
            queriesPerMonth={queriesPerMonth}
          />
        </div>

        {/* Selected Model Details */}
        {selectedModel && (
          <motion.div
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            className="p-4 rounded-xl bg-primary/5 border border-primary/20"
          >
            <div className="flex items-start gap-3">
              <Info className="h-5 w-5 text-primary mt-0.5" />
              <div>
                <div className="font-semibold">{selectedModel.name}</div>
                <p className="text-sm text-muted-foreground mt-1">
                  Best for: {selectedModel.bestFor.join(', ')}
                </p>
                <div className="mt-2 text-sm">
                  <span className="font-medium">Context window:</span>{' '}
                  <span className="font-mono">{selectedModel.maxContext}K tokens</span>
                </div>
              </div>
            </div>
          </motion.div>
        )}

        {/* Use Case Recommendations */}
        <div className="space-y-3">
          <div className="text-sm font-medium flex items-center gap-2">
            <Target className="h-4 w-4" />
            Use Case Recommendations
          </div>
          <div className="space-y-2">
            {USE_CASE_RECOMMENDATIONS.map(rec => (
              <div key={rec.useCase} className="p-3 rounded-lg bg-muted/30 border">
                <div className="flex items-start justify-between">
                  <div>
                    <div className="font-medium text-sm">{rec.useCase}</div>
                    <p className="text-xs text-muted-foreground mt-0.5">{rec.description}</p>
                  </div>
                  <div className="text-right text-xs">
                    <div className="flex items-center gap-1">
                      <Star className="h-3 w-3 text-yellow-500" />
                      <span className="font-medium">{rec.recommendedModel}</span>
                    </div>
                    <div className="text-muted-foreground">Alt: {rec.alternativeModel}</div>
                  </div>
                </div>
                <p className="text-xs text-muted-foreground mt-2 italic">{rec.reasoning}</p>
              </div>
            ))}
          </div>
        </div>

        {/* Key Insights */}
        <div className="grid sm:grid-cols-3 gap-4">
          <div className="p-4 rounded-xl bg-green-500/10 border border-green-500/20">
            <div className="flex items-center gap-2 text-green-600 dark:text-green-400 mb-2">
              <TrendingDown className="h-4 w-4" />
              <span className="font-semibold text-sm">Cost Optimization</span>
            </div>
            <p className="text-xs text-muted-foreground">
              Use FAST strategy for 70% of queries. Free models handle simple lookups well.
            </p>
          </div>
          <div className="p-4 rounded-xl bg-blue-500/10 border border-blue-500/20">
            <div className="flex items-center gap-2 text-blue-600 dark:text-blue-400 mb-2">
              <Scale className="h-4 w-4" />
              <span className="font-semibold text-sm">Quality Balance</span>
            </div>
            <p className="text-xs text-muted-foreground">
              Claude 3.5 Sonnet offers best quality/cost ratio for complex queries.
            </p>
          </div>
          <div className="p-4 rounded-xl bg-purple-500/10 border border-purple-500/20">
            <div className="flex items-center gap-2 text-purple-600 dark:text-purple-400 mb-2">
              <TrendingUp className="h-4 w-4" />
              <span className="font-semibold text-sm">Premium Value</span>
            </div>
            <p className="text-xs text-muted-foreground">
              Reserve Opus for premium users and critical strategy analysis.
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
