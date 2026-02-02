'use client';

/**
 * QuerySimulator Component
 *
 * Interactive query input that simulates TOMAC-RAG routing in real-time.
 * Shows: User Tier → Template Classification → Complexity Score → Strategy Selection
 */

import React, { useState, useCallback } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import { Search, Zap, Brain, BarChart3, Target, Cpu, Clock, DollarSign } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import { RAG_LAYERS, MODELS } from './types';
import { TIER_STRATEGY_ACCESS } from './rag-data';

import type { RagStrategy, UserTier, QueryTemplate, QueryAnalysis } from './types';

// =============================================================================
// Query Analysis Logic
// =============================================================================

const TEMPLATE_KEYWORDS: Record<QueryTemplate, string[]> = {
  rule_lookup: ['how many', 'what is', 'when can', 'rule', 'allowed', 'cost', 'requirement'],
  resource_planning: ['resource', 'food', 'wood', 'stone', 'money', 'budget', 'tokens'],
  setup_guide: ['setup', 'start', 'prepare', 'begin', 'initial', 'first'],
  strategy_advice: ['strategy', 'best', 'should i', 'optimal', 'recommend', 'win'],
  educational: ['explain', 'why', 'teach', 'understand', 'learn', 'example'],
};

function classifyTemplate(query: string): QueryTemplate {
  const lowerQuery = query.toLowerCase();

  for (const [template, keywords] of Object.entries(TEMPLATE_KEYWORDS)) {
    if (keywords.some(kw => lowerQuery.includes(kw))) {
      return template as QueryTemplate;
    }
  }

  return 'rule_lookup';
}

function calculateComplexity(query: string, template: QueryTemplate): number {
  let score = 0;

  // Length factor (0-1)
  if (query.length > 50) score += 1;
  if (query.length > 100) score += 1;

  // Multi-concept (keywords count)
  const conceptKeywords = ['and', 'or', 'but', 'also', 'then', 'if'];
  const conceptCount = conceptKeywords.filter(kw => query.toLowerCase().includes(kw)).length;
  score += Math.min(conceptCount, 2);

  // Template complexity bonus
  if (template === 'strategy_advice') score += 1;
  if (template === 'educational') score += 1;

  return Math.min(score, 5);
}

/**
 * Select strategy based on tier and complexity.
 * Uses TIER_STRATEGY_ACCESS (new architecture: Tier → Available Strategies).
 *
 * Architecture: Tier determines WHICH strategies are available.
 * Complexity determines WHICH of the available strategies to recommend.
 *
 * @see rag-data.ts TIER_STRATEGY_ACCESS
 */
function selectStrategy(tier: UserTier, complexity: number, availableStrategies: RagStrategy[]): RagStrategy {
  // Anonymous users cannot access the RAG system
  if (tier === 'Anonymous' || availableStrategies.length === 0) {
    throw new Error('AuthenticationRequired: Anonymous users cannot access RAG system');
  }

  // Select from AVAILABLE strategies only (tier-based access control)
  // Complexity determines recommendation within allowed strategies

  if (complexity <= 1 && availableStrategies.includes('FAST')) return 'FAST';
  if (complexity <= 3 && availableStrategies.includes('BALANCED')) return 'BALANCED';
  if (complexity <= 4 && availableStrategies.includes('PRECISE')) return 'PRECISE';
  if (complexity === 5 && availableStrategies.includes('EXPERT')) return 'EXPERT';

  // Fallback: highest complexity available strategy
  if (availableStrategies.includes('CONSENSUS')) return 'CONSENSUS';
  if (availableStrategies.includes('EXPERT')) return 'EXPERT';
  if (availableStrategies.includes('PRECISE')) return 'PRECISE';
  if (availableStrategies.includes('BALANCED')) return 'BALANCED';

  return availableStrategies[0]; // Fallback to first available
}

/**
 * Estimate tokens based on strategy.
 * Values from rag-data.ts (Single Source of Truth).
 */
function estimateTokens(strategy: RagStrategy, cacheHit: boolean): number {
  if (cacheHit) return 50 + Math.floor(Math.random() * 50);

  // Token values aligned with rag-data.ts
  const baseTokens: Record<RagStrategy, number> = {
    FAST: 2060,
    BALANCED: 2820,
    PRECISE: 22396,
    EXPERT: 15000,
    CONSENSUS: 18000,
    CUSTOM: 5000,
  };

  return baseTokens[strategy] + Math.floor(Math.random() * 500) - 250;
}

function analyzeQuery(query: string, tier: UserTier): QueryAnalysis {
  const template = classifyTemplate(query);
  const complexity = calculateComplexity(query, template);

  // NEW ARCHITECTURE: Tier → Available Strategies (access control only)
  const tierAccess = TIER_STRATEGY_ACCESS.find(t => t.tier === tier);

  if (!tierAccess || !tierAccess.hasRagAccess) {
    throw new Error('AuthenticationRequired: This tier has no access to RAG system');
  }

  const availableStrategies = tierAccess.availableStrategies;
  const strategy = selectStrategy(tier, complexity, availableStrategies);

  // Strategy determines model (NOT tier!)
  const cacheHit = Math.random() < 0.8; // 80% cache hit simulation
  const estimatedTokens = estimateTokens(strategy, cacheHit);
  const model = MODELS[strategy][0];

  const inputCost = (estimatedTokens * 0.7 * model.inputCostPerMillion) / 1000000;
  const outputCost = (estimatedTokens * 0.3 * model.outputCostPerMillion) / 1000000;

  return {
    query,
    tier,
    template,
    complexity,
    strategy,
    model: model.name,
    estimatedTokens,
    estimatedCost: inputCost + outputCost,
    cacheHit,
    layers: RAG_LAYERS.map((layer) => ({
      layerId: layer.id,
      status: 'complete' as const,
      tokensUsed: Math.floor(
        layer.tokenRange.min + Math.random() * (layer.tokenRange.max - layer.tokenRange.min)
      ),
      latencyMs: 50 + Math.floor(Math.random() * 200),
    })),
  };
}

// =============================================================================
// Component
// =============================================================================

interface QuerySimulatorProps {
  className?: string;
}

export function QuerySimulator({ className }: QuerySimulatorProps) {
  const [query, setQuery] = useState('');
  const [tier, setTier] = useState<UserTier>('User');
  const [analysis, setAnalysis] = useState<QueryAnalysis | null>(null);
  const [isAnalyzing, setIsAnalyzing] = useState(false);

  const handleAnalyze = useCallback(() => {
    if (!query.trim()) return;
    if (tier === 'Anonymous') return; // Anonymous has NO ACCESS

    setIsAnalyzing(true);
    setAnalysis(null);

    // Simulate processing delay
    setTimeout(() => {
      setAnalysis(analyzeQuery(query, tier));
      setIsAnalyzing(false);
    }, 800);
  }, [query, tier]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter') {
        handleAnalyze();
      }
    },
    [handleAnalyze]
  );

  // NOTE: Anonymous shown as disabled - NO ACCESS to RAG system
  const tiers: UserTier[] = ['Anonymous', 'User', 'Editor', 'Admin', 'Premium'];
  const isAnonymous = tier === 'Anonymous';

  const strategyColors: Record<RagStrategy, string> = {
    FAST: 'bg-green-500/10 text-green-600 dark:text-green-400 border-green-500/30',
    BALANCED: 'bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/30',
    PRECISE: 'bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/30',
    EXPERT: 'bg-orange-500/10 text-orange-600 dark:text-orange-400 border-orange-500/30',
    CONSENSUS: 'bg-pink-500/10 text-pink-600 dark:text-pink-400 border-pink-500/30',
    CUSTOM: 'bg-gray-500/10 text-gray-600 dark:text-gray-400 border-gray-500/30',
  };

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Search className="h-5 w-5 text-primary" />
          Query Simulator
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-6">
        {/* Tier Selector */}
        <div className="flex flex-wrap gap-2">
          <span className="text-sm text-muted-foreground mr-2">User Tier:</span>
          {tiers.map(t => (
            <button
              key={t}
              onClick={() => setTier(t)}
              className={cn(
                'px-3 py-1 text-sm rounded-full border transition-all',
                t === 'Anonymous'
                  ? 'bg-red-500/10 text-red-500 border-red-500/30 cursor-pointer'
                  : tier === t
                    ? 'bg-primary text-primary-foreground border-primary'
                    : 'bg-muted/50 text-muted-foreground border-transparent hover:border-primary/50'
              )}
            >
              {t === 'Anonymous' ? '🚫 Anonymous' : t}
            </button>
          ))}
        </div>

        {/* Anonymous Warning */}
        {isAnonymous && (
          <div className="p-4 rounded-lg bg-red-500/10 border border-red-500/30 text-red-600 dark:text-red-400">
            <strong>⚠️ NO ACCESS:</strong> Anonymous users cannot access the RAG system. Authentication is required.
          </div>
        )}

        {/* Query Input */}
        <div className="rag-query-input">
          <Search className="search-icon h-5 w-5" aria-hidden="true" />
          <input
            type="text"
            value={query}
            onChange={e => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Try: How many food tokens in Agricola for 3 players?"
            className="w-full"
          />
          <button
            onClick={handleAnalyze}
            disabled={!query.trim() || isAnalyzing || isAnonymous}
            className={cn(
              'absolute right-2 top-1/2 -translate-y-1/2',
              'px-4 py-2 rounded-lg font-medium text-sm',
              'bg-primary text-primary-foreground',
              'hover:opacity-90 transition-opacity',
              'disabled:opacity-50 disabled:cursor-not-allowed'
            )}
          >
            {isAnonymous ? 'No Access' : isAnalyzing ? 'Analyzing...' : 'Analyze'}
          </button>
        </div>

        {/* Analysis Results */}
        <AnimatePresence mode="wait">
          {analysis && (
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              className="space-y-4"
            >
              {/* Routing Decision Flow */}
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                {/* Template */}
                <div className="text-center p-4 rounded-lg bg-muted/30 border border-border">
                  <Brain className="h-6 w-6 mx-auto mb-2 text-purple-500" />
                  <div className="text-xs text-muted-foreground mb-1">Template</div>
                  <div className="font-semibold text-sm">{analysis.template.replace('_', ' ')}</div>
                </div>

                {/* Complexity */}
                <div className="text-center p-4 rounded-lg bg-muted/30 border border-border">
                  <BarChart3 className="h-6 w-6 mx-auto mb-2 text-orange-500" />
                  <div className="text-xs text-muted-foreground mb-1">Complexity</div>
                  <div className="font-semibold text-sm">{analysis.complexity}/5</div>
                </div>

                {/* Strategy */}
                <div className="text-center p-4 rounded-lg bg-muted/30 border border-border">
                  <Target className="h-6 w-6 mx-auto mb-2 text-blue-500" />
                  <div className="text-xs text-muted-foreground mb-1">Strategy</div>
                  <Badge className={cn('font-semibold', strategyColors[analysis.strategy])}>
                    {analysis.strategy}
                  </Badge>
                </div>

                {/* Cache Status */}
                <div className="text-center p-4 rounded-lg bg-muted/30 border border-border">
                  <Zap className={cn('h-6 w-6 mx-auto mb-2', analysis.cacheHit ? 'text-green-500' : 'text-gray-400')} />
                  <div className="text-xs text-muted-foreground mb-1">Cache</div>
                  <div className={cn('font-semibold text-sm', analysis.cacheHit ? 'text-green-600' : 'text-gray-500')}>
                    {analysis.cacheHit ? 'HIT!' : 'MISS'}
                  </div>
                </div>
              </div>

              {/* Performance Metrics */}
              <div className="grid grid-cols-3 gap-4 pt-4 border-t border-border">
                <div className="flex items-center gap-3">
                  <Cpu className="h-5 w-5 text-muted-foreground" />
                  <div>
                    <div className="text-xs text-muted-foreground">Model</div>
                    <div className="font-medium text-sm">{analysis.model}</div>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <Clock className="h-5 w-5 text-muted-foreground" />
                  <div>
                    <div className="text-xs text-muted-foreground">Est. Tokens</div>
                    <div className="font-medium text-sm">{analysis.estimatedTokens.toLocaleString()}</div>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <DollarSign className="h-5 w-5 text-muted-foreground" />
                  <div>
                    <div className="text-xs text-muted-foreground">Est. Cost</div>
                    <div className="font-medium text-sm">
                      ${analysis.estimatedCost < 0.001 ? '<0.001' : analysis.estimatedCost.toFixed(4)}
                    </div>
                  </div>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        {/* Example Queries */}
        {!analysis && (
          <div className="pt-4 border-t border-border">
            <div className="text-xs text-muted-foreground mb-2">Try these examples:</div>
            <div className="flex flex-wrap gap-2">
              {[
                'How many players in Wingspan?',
                'Best strategy for Catan with 3 ore?',
                'Explain worker placement mechanics',
                'Setup guide for Terraforming Mars',
              ].map(example => (
                <button
                  key={example}
                  onClick={() => setQuery(example)}
                  className="text-xs px-3 py-1.5 rounded-full bg-muted/50 hover:bg-muted text-muted-foreground hover:text-foreground transition-colors"
                >
                  {example}
                </button>
              ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
