'use client';

/**
 * PocStatus Component
 *
 * Shows the current POC implementation status vs planned features.
 * Distinguishes between what's actually working in code vs documentation/plans.
 */

import React, { useState } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  CheckCircle2,
  Circle,
  Clock,
  Database,
  Search,
  Bot,
  Zap,
  Server,
  Shield,
  ChevronDown,
  ChevronRight,
  AlertCircle,
  Sparkles,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

// =============================================================================
// Types
// =============================================================================

type FeatureStatus = 'implemented' | 'partial' | 'planned';

interface Feature {
  name: string;
  description: string;
  status: FeatureStatus;
  details?: string;
  file?: string;
}

interface FeatureCategory {
  id: string;
  name: string;
  icon: React.ReactNode;
  color: string;
  features: Feature[];
}

// =============================================================================
// POC Feature Data
// =============================================================================

const POC_FEATURES: FeatureCategory[] = [
  {
    id: 'search',
    name: 'Search & Retrieval (Layer 3)',
    icon: <Search className="h-5 w-5" />,
    color: 'hsl(142 76% 36%)',
    features: [
      {
        name: 'Semantic Search (Vector)',
        description: 'Qdrant vector database con cosine similarity',
        status: 'implemented',
        details: 'text-embedding-3-large, 3072 dimensioni, HNSW index',
        file: 'QdrantVectorSearcher.cs',
      },
      {
        name: 'Keyword Search (FTS)',
        description: 'PostgreSQL tsvector con ts_rank_cd',
        status: 'implemented',
        details: 'Supporto italiano/inglese, phrase search, boost terms',
        file: 'KeywordSearchService.cs',
      },
      {
        name: 'Hybrid Search (RRF)',
        description: 'Reciprocal Rank Fusion con pesi configurabili',
        status: 'implemented',
        details: 'k=60, vector_weight=0.7, keyword_weight=0.3',
        file: 'HybridSearchService.cs',
      },
      {
        name: 'Query Expansion',
        description: 'Espansione query con sinonimi e varianti',
        status: 'partial',
        details: 'Implementazione base, senza LLM expansion',
        file: 'IQueryExpansionService.cs',
      },
      {
        name: 'Cross-Encoder Reranking',
        description: 'Reranking con BAAI/bge-reranker-v2-m3',
        status: 'planned',
        details: 'Docker service pronto, integrazione pending',
      },
    ],
  },
  {
    id: 'llm',
    name: 'LLM Generation (Layer 5)',
    icon: <Bot className="h-5 w-5" />,
    color: 'hsl(262 83% 62%)',
    features: [
      {
        name: 'Ollama Provider',
        description: 'LLM locale con Llama 3.3 70B (free)',
        status: 'implemented',
        details: 'Streaming, circuit breaker, health check',
        file: 'OllamaLlmClient.cs',
      },
      {
        name: 'OpenRouter Provider',
        description: 'GPT-4o-mini, Claude via API',
        status: 'implemented',
        details: 'Fallback provider, cost tracking',
        file: 'OpenRouterLlmClient.cs',
      },
      {
        name: 'Tier-Based Routing',
        description: 'User tier → Provider selection',
        status: 'implemented',
        details: 'User→Ollama, Editor→Balanced, Admin→Premium',
        file: 'HybridAdaptiveRoutingStrategy.cs',
      },
      {
        name: 'Circuit Breaker',
        description: 'Failover automatico tra provider',
        status: 'implemented',
        details: '5 failures → open 30s, health monitoring',
        file: 'HybridLlmService.cs',
      },
      {
        name: 'Cost Tracking',
        description: 'Logging costi per query/user',
        status: 'implemented',
        details: 'Token usage, cost per model',
        file: 'LlmCostLogEntity.cs',
      },
    ],
  },
  {
    id: 'strategies',
    name: 'RAG Strategies',
    icon: <Zap className="h-5 w-5" />,
    color: 'hsl(25 95% 53%)',
    features: [
      {
        name: 'FAST Strategy',
        description: 'Cache + Single-pass generation',
        status: 'partial',
        details: 'Implementato come default flow, senza routing esplicito',
      },
      {
        name: 'BALANCED Strategy',
        description: 'CRAG evaluation + re-retrieval',
        status: 'planned',
        details: 'Richiede T5-Large evaluator',
      },
      {
        name: 'PRECISE Strategy',
        description: 'Multi-agent pipeline con Self-RAG',
        status: 'planned',
        details: 'Richiede LangGraph orchestrator',
      },
      {
        name: 'EXPERT Strategy',
        description: 'Web search + multi-hop reasoning',
        status: 'planned',
        details: 'Richiede Tavily/Serper integration',
      },
      {
        name: 'CONSENSUS Strategy',
        description: 'Multi-LLM voting (3 voters)',
        status: 'planned',
        details: 'Richiede 3 LLM calls paralleli',
      },
      {
        name: 'Strategy Router (L1)',
        description: 'Query classification → Strategy selection',
        status: 'planned',
        details: 'Richiede LLM classifier',
      },
    ],
  },
  {
    id: 'cache',
    name: 'Caching (Layer 2)',
    icon: <Database className="h-5 w-5" />,
    color: 'hsl(221 83% 53%)',
    features: [
      {
        name: 'Response Cache',
        description: 'Redis cache per risposte AI',
        status: 'implemented',
        details: 'TTL configurabile per tier',
        file: 'IAiResponseCacheService.cs',
      },
      {
        name: 'Semantic Cache',
        description: 'Cache basata su embedding similarity',
        status: 'planned',
        details: 'Target 80% hit rate',
      },
    ],
  },
  {
    id: 'validation',
    name: 'Validation (Layer 6)',
    icon: <Shield className="h-5 w-5" />,
    color: 'hsl(0 72% 51%)',
    features: [
      {
        name: 'Citation Extraction',
        description: 'Estrazione riferimenti pagina',
        status: 'implemented',
        details: 'Regex-based, page number extraction',
      },
      {
        name: 'Self-RAG Validation',
        description: 'Auto-verifica con reflection',
        status: 'planned',
        details: 'Richiede LLM evaluation loop',
      },
      {
        name: 'Hallucination Detection',
        description: 'Verifica claim vs context',
        status: 'planned',
        details: 'Richiede NLI model',
      },
    ],
  },
  {
    id: 'infra',
    name: 'Infrastructure',
    icon: <Server className="h-5 w-5" />,
    color: 'hsl(45 93% 47%)',
    features: [
      {
        name: 'PostgreSQL + tsvector',
        description: 'Database con FTS triggers',
        status: 'implemented',
        file: 'docker-compose.yml',
      },
      {
        name: 'Qdrant Vector DB',
        description: 'Vector storage con HNSW',
        status: 'implemented',
        file: 'docker-compose.yml',
      },
      {
        name: 'Redis Cache',
        description: 'Caching layer',
        status: 'implemented',
        file: 'docker-compose.yml',
      },
      {
        name: 'Ollama Container',
        description: 'Local LLM hosting',
        status: 'implemented',
        file: 'docker-compose.yml',
      },
      {
        name: 'Embedding Service',
        description: 'Python embedding server',
        status: 'implemented',
        file: 'apps/embedding-service/',
      },
      {
        name: 'SmolDocling Service',
        description: 'VLM PDF extraction',
        status: 'implemented',
        details: 'Stage 2 fallback',
        file: 'apps/smoldocling-service/',
      },
      {
        name: 'Reranker Service',
        description: 'Cross-encoder reranking',
        status: 'implemented',
        details: 'Container ready, API pending',
        file: 'apps/reranker-service/',
      },
      {
        name: 'Orchestrator Service',
        description: 'LangGraph multi-agent',
        status: 'partial',
        details: 'Container ready, workflow pending',
        file: 'apps/orchestration-service/',
      },
    ],
  },
];

// =============================================================================
// Helper Functions
// =============================================================================

function getStatusIcon(status: FeatureStatus) {
  switch (status) {
    case 'implemented':
      return <CheckCircle2 className="h-4 w-4 text-green-500" />;
    case 'partial':
      return <AlertCircle className="h-4 w-4 text-yellow-500" />;
    case 'planned':
      return <Circle className="h-4 w-4 text-muted-foreground" />;
  }
}

function getStatusBadge(status: FeatureStatus) {
  switch (status) {
    case 'implemented':
      return (
        <Badge className="bg-green-500/20 text-green-600 border-green-500/30">Implemented</Badge>
      );
    case 'partial':
      return (
        <Badge className="bg-yellow-500/20 text-yellow-600 border-yellow-500/30">Partial</Badge>
      );
    case 'planned':
      return (
        <Badge variant="outline" className="text-muted-foreground">
          Planned
        </Badge>
      );
  }
}

function calculateProgress(features: Feature[]): {
  implemented: number;
  partial: number;
  total: number;
} {
  const implemented = features.filter(f => f.status === 'implemented').length;
  const partial = features.filter(f => f.status === 'partial').length;
  return { implemented, partial, total: features.length };
}

// =============================================================================
// Sub-Components
// =============================================================================

interface CategoryCardProps {
  category: FeatureCategory;
  isExpanded: boolean;
  onToggle: () => void;
}

function CategoryCard({ category, isExpanded, onToggle }: CategoryCardProps) {
  const progress = calculateProgress(category.features);
  const progressPercent = Math.round(
    ((progress.implemented + progress.partial * 0.5) / progress.total) * 100
  );

  return (
    <div className="border rounded-xl overflow-hidden">
      <button
        className="w-full p-4 flex items-center justify-between hover:bg-muted/30 transition-colors"
        onClick={onToggle}
      >
        <div className="flex items-center gap-3">
          <div
            className="p-2 rounded-lg"
            style={{ backgroundColor: `${category.color}20`, color: category.color }}
          >
            {category.icon}
          </div>
          <div className="text-left">
            <div className="font-semibold text-sm">{category.name}</div>
            <div className="flex items-center gap-2 mt-1">
              <div className="w-24 h-1.5 bg-muted rounded-full overflow-hidden">
                <div
                  className="h-full bg-green-500 transition-all"
                  style={{ width: `${progressPercent}%` }}
                />
              </div>
              <span className="text-xs text-muted-foreground">
                {progress.implemented}/{progress.total}
              </span>
            </div>
          </div>
        </div>
        {isExpanded ? (
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        ) : (
          <ChevronRight className="h-4 w-4 text-muted-foreground" />
        )}
      </button>

      <AnimatePresence>
        {isExpanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            <div className="px-4 pb-4 space-y-2">
              {category.features.map(feature => (
                <div
                  key={feature.name}
                  className={cn(
                    'p-3 rounded-lg border',
                    feature.status === 'implemented' && 'bg-green-500/5 border-green-500/20',
                    feature.status === 'partial' && 'bg-yellow-500/5 border-yellow-500/20',
                    feature.status === 'planned' && 'bg-muted/30 border-border'
                  )}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex items-start gap-2">
                      {getStatusIcon(feature.status)}
                      <div>
                        <div className="font-medium text-sm">{feature.name}</div>
                        <div className="text-xs text-muted-foreground">{feature.description}</div>
                        {feature.details && (
                          <div className="text-xs text-muted-foreground mt-1 italic">
                            {feature.details}
                          </div>
                        )}
                        {feature.file && (
                          <code className="text-xs bg-muted px-1 rounded mt-1 inline-block">
                            {feature.file}
                          </code>
                        )}
                      </div>
                    </div>
                    {getStatusBadge(feature.status)}
                  </div>
                </div>
              ))}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface PocStatusProps {
  className?: string;
}

export function PocStatus({ className }: PocStatusProps) {
  const [expandedCategory, setExpandedCategory] = useState<string | null>('search');

  // Calculate overall progress
  const allFeatures = POC_FEATURES.flatMap(c => c.features);
  const overallProgress = calculateProgress(allFeatures);
  const overallPercent = Math.round(
    ((overallProgress.implemented + overallProgress.partial * 0.5) / overallProgress.total) * 100
  );

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-primary" />
          POC Implementation Status
        </CardTitle>
        <p className="text-sm text-muted-foreground mt-1">
          Stato attuale dell&apos;implementazione vs piano TOMAC-RAG
        </p>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Overall Progress */}
        <div className="p-4 rounded-xl bg-gradient-to-r from-primary/10 to-purple-500/10 border border-primary/20">
          <div className="flex items-center justify-between mb-3">
            <div className="font-semibold">Overall Progress</div>
            <div className="text-2xl font-bold text-primary">{overallPercent}%</div>
          </div>
          <div className="w-full h-3 bg-muted rounded-full overflow-hidden">
            <div
              className="h-full bg-gradient-to-r from-primary to-purple-500 transition-all"
              style={{ width: `${overallPercent}%` }}
            />
          </div>
          <div className="flex items-center justify-between mt-2 text-xs text-muted-foreground">
            <span className="flex items-center gap-1">
              <CheckCircle2 className="h-3 w-3 text-green-500" />
              {overallProgress.implemented} implemented
            </span>
            <span className="flex items-center gap-1">
              <AlertCircle className="h-3 w-3 text-yellow-500" />
              {overallProgress.partial} partial
            </span>
            <span className="flex items-center gap-1">
              <Circle className="h-3 w-3" />
              {overallProgress.total - overallProgress.implemented - overallProgress.partial}{' '}
              planned
            </span>
          </div>
        </div>

        {/* Current POC Summary */}
        <div className="grid md:grid-cols-2 gap-4">
          <div className="p-4 rounded-xl bg-green-500/10 border border-green-500/20">
            <div className="font-semibold text-green-600 dark:text-green-400 mb-2 flex items-center gap-2">
              <CheckCircle2 className="h-4 w-4" />
              Funzionante nel POC
            </div>
            <ul className="space-y-1 text-xs text-muted-foreground">
              <li>
                • <strong>Hybrid Search</strong>: Vector + Keyword + RRF
              </li>
              <li>
                • <strong>LLM Providers</strong>: Ollama (free) + OpenRouter
              </li>
              <li>
                • <strong>Tier Routing</strong>: User → Ollama, Admin → Premium
              </li>
              <li>
                • <strong>Circuit Breaker</strong>: Auto-failover tra provider
              </li>
              <li>
                • <strong>PDF Pipeline</strong>: Unstructured + SmolDocling
              </li>
            </ul>
          </div>

          <div className="p-4 rounded-xl bg-muted/30 border border-border">
            <div className="font-semibold text-muted-foreground mb-2 flex items-center gap-2">
              <Clock className="h-4 w-4" />
              Prossimi Step
            </div>
            <ul className="space-y-1 text-xs text-muted-foreground">
              <li>
                • <strong>Strategy Router (L1)</strong>: Query → Strategy
              </li>
              <li>
                • <strong>CRAG Evaluation (L4)</strong>: Quality gate
              </li>
              <li>
                • <strong>Semantic Cache (L2)</strong>: 80% hit target
              </li>
              <li>
                • <strong>Self-RAG (L6)</strong>: Auto-validation
              </li>
              <li>
                • <strong>Reranker Integration</strong>: Cross-encoder
              </li>
            </ul>
          </div>
        </div>

        {/* Feature Categories */}
        <div className="space-y-2">
          {POC_FEATURES.map(category => (
            <CategoryCard
              key={category.id}
              category={category}
              isExpanded={expandedCategory === category.id}
              onToggle={() =>
                setExpandedCategory(expandedCategory === category.id ? null : category.id)
              }
            />
          ))}
        </div>
      </CardContent>
    </Card>
  );
}
