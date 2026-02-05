'use client';

/**
 * ParameterGuide Component
 *
 * Comprehensive guide to RAG system parameters:
 * - Current POC parameters
 * - Future TOMAC-RAG parameters
 * - Strategy comparison grid
 * - Effects and trade-offs
 */

import React, { useState } from 'react';

import {
  CheckCircle2,
  Circle,
  Zap,
  Database,
  Brain,
  Search,
  Shield,
  Settings,
  ChevronDown,
  ChevronRight,
  TrendingUp,
  TrendingDown,
  Minus,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

import { STRATEGIES, LAYERS, MODEL_PRICING } from './rag-data';

// =============================================================================
// Types
// =============================================================================

type ImplementationStatus = 'implemented' | 'partial' | 'planned';

interface Parameter {
  name: string;
  currentValue: string;
  range?: string;
  effect: string;
  tradeoff?: string;
  status: ImplementationStatus;
  category: 'search' | 'embedding' | 'llm' | 'cache' | 'routing' | 'validation';
}

// =============================================================================
// Parameter Data
// =============================================================================

const PARAMETERS: Parameter[] = [
  // SEARCH PARAMETERS
  {
    name: 'RRF k',
    currentValue: '60',
    range: '1-100',
    effect: 'Smoothing factor: k basso favorisce top positions, k alto distribuisce score',
    tradeoff: 'k basso = più selettivo | k alto = più diversità',
    status: 'implemented',
    category: 'search',
  },
  {
    name: 'vector_weight',
    currentValue: '0.7',
    range: '0-1',
    effect: 'Peso ricerca semantica nel fusion. Alto = privilegia similarità concettuale',
    tradeoff: 'Alto = migliore per query concettuali | Basso = meglio per termini tecnici',
    status: 'implemented',
    category: 'search',
  },
  {
    name: 'keyword_weight',
    currentValue: '0.3',
    range: '0-1',
    effect: 'Peso ricerca keyword nel fusion. Alto = privilegia match esatto',
    tradeoff: 'Alto = meglio per nomi/termini | Basso = meglio per concetti',
    status: 'implemented',
    category: 'search',
  },
  {
    name: 'top_k',
    currentValue: '10',
    range: '5-50',
    effect: 'Numero chunk recuperati. Più = più contesto, ma più noise e token',
    tradeoff: 'Alto = più recall | Basso = più precision, meno costo',
    status: 'implemented',
    category: 'search',
  },
  {
    name: 'similarity_threshold',
    currentValue: '0.7',
    range: '0-1',
    effect: 'Soglia minima cosine similarity per includere chunk',
    tradeoff: 'Alto = solo chunk rilevanti | Basso = più risultati, possibile noise',
    status: 'partial',
    category: 'search',
  },
  {
    name: 'reranker_top_k',
    currentValue: '5',
    range: '3-20',
    effect: 'Chunk da passare al reranker dopo retrieval iniziale',
    tradeoff: 'Alto = migliore qualità | Basso = più veloce',
    status: 'planned',
    category: 'search',
  },

  // EMBEDDING PARAMETERS
  {
    name: 'embedding_model',
    currentValue: 'text-embedding-3-large',
    effect: 'Modello per convertire testo in vettori. Determina qualità rappresentazione',
    tradeoff: 'Large = migliore qualità, più costo | Small = più veloce, meno preciso',
    status: 'implemented',
    category: 'embedding',
  },
  {
    name: 'vector_dimensions',
    currentValue: '3072',
    range: '256-3072',
    effect: 'Dimensioni vettore. Più = migliore cattura semantica, più storage',
    tradeoff: 'Alto = migliore qualità | Basso = meno storage, più veloce',
    status: 'implemented',
    category: 'embedding',
  },
  {
    name: 'similarity_metric',
    currentValue: 'cosine',
    effect: 'Metrica distanza: cosine (normalizzato), dot_product (veloce), euclidean',
    tradeoff: 'Cosine = robusto | Dot = veloce | Euclidean = distanza assoluta',
    status: 'implemented',
    category: 'embedding',
  },

  // LLM PARAMETERS
  {
    name: 'temperature',
    currentValue: '0.7',
    range: '0-2',
    effect: 'Creatività/randomness. Basso = deterministico, Alto = creativo',
    tradeoff: '0-0.3 = preciso (rules) | 0.5-0.7 = bilanciato | 0.8+ = creativo (strategy)',
    status: 'implemented',
    category: 'llm',
  },
  {
    name: 'max_tokens',
    currentValue: '2048',
    range: '256-8192',
    effect: 'Limite lunghezza risposta. Più = risposte complete, più costo',
    tradeoff: 'Alto = complete | Basso = concise, economiche',
    status: 'implemented',
    category: 'llm',
  },
  {
    name: 'primary_model',
    currentValue: 'llama3.3:70b (Ollama)',
    effect: 'Modello LLM principale. Determina qualità, costo, velocità',
    status: 'implemented',
    category: 'llm',
  },
  {
    name: 'fallback_model',
    currentValue: 'gpt-4o-mini (OpenRouter)',
    effect: 'Modello backup quando primary fallisce (circuit breaker)',
    status: 'implemented',
    category: 'llm',
  },
  {
    name: 'circuit_breaker_threshold',
    currentValue: '5',
    range: '1-10',
    effect: 'Failures prima di aprire circuit breaker e passare a fallback',
    tradeoff: 'Basso = failover rapido | Alto = più tollerante a errori transitori',
    status: 'implemented',
    category: 'llm',
  },
  {
    name: 'circuit_breaker_duration',
    currentValue: '30s',
    range: '10s-5min',
    effect: 'Tempo in stato "open" prima di riprovare primary',
    tradeoff: 'Breve = riprova presto | Lungo = più stabile',
    status: 'implemented',
    category: 'llm',
  },

  // CACHE PARAMETERS
  {
    name: 'cache_ttl_user',
    currentValue: '48h',
    range: '1h-168h',
    effect: 'Durata cache per tier User',
    tradeoff: 'Lungo = meno costi | Breve = risposte fresche',
    status: 'implemented',
    category: 'cache',
  },
  {
    name: 'cache_ttl_admin',
    currentValue: '168h',
    range: '24h-336h',
    effect: 'Durata cache per tier Admin',
    status: 'implemented',
    category: 'cache',
  },
  {
    name: 'semantic_cache_threshold',
    currentValue: '0.95',
    range: '0.85-0.99',
    effect: 'Similarità minima per cache hit semantico (query simili, non identiche)',
    tradeoff: 'Alto = solo query quasi uguali | Basso = più hit, rischio risposte inappropriate',
    status: 'planned',
    category: 'cache',
  },
  {
    name: 'semantic_cache_size',
    currentValue: '10000',
    range: '1000-100000',
    effect: 'Numero massimo entry in semantic cache',
    status: 'planned',
    category: 'cache',
  },

  // ROUTING PARAMETERS (FUTURE)
  {
    name: 'routing_classifier_model',
    currentValue: 'N/A',
    effect: 'LLM per classificare query e selezionare strategia (L1)',
    status: 'planned',
    category: 'routing',
  },
  {
    name: 'complexity_threshold_fast',
    currentValue: '0.3',
    range: '0-1',
    effect: 'Soglia complessità sotto cui usare FAST strategy',
    status: 'planned',
    category: 'routing',
  },
  {
    name: 'complexity_threshold_balanced',
    currentValue: '0.7',
    range: '0-1',
    effect: 'Soglia complessità per BALANCED (tra FAST e PRECISE)',
    status: 'planned',
    category: 'routing',
  },

  // VALIDATION PARAMETERS (FUTURE)
  {
    name: 'crag_evaluator_model',
    currentValue: 'T5-Large',
    effect: 'Modello per valutare qualità retrieval (L4 CRAG)',
    status: 'planned',
    category: 'validation',
  },
  {
    name: 'crag_relevance_threshold',
    currentValue: '0.7',
    range: '0.5-0.9',
    effect: 'Soglia rilevanza CRAG. Sotto = trigger re-retrieval o web search',
    status: 'planned',
    category: 'validation',
  },
  {
    name: 'self_rag_enabled',
    currentValue: 'false',
    effect: 'Abilita Self-RAG validation (L6) con reflection loop',
    status: 'planned',
    category: 'validation',
  },
  {
    name: 'hallucination_threshold',
    currentValue: '0.8',
    range: '0.6-0.95',
    effect: 'Soglia NLI per rilevare hallucination',
    status: 'planned',
    category: 'validation',
  },
];

const CATEGORY_INFO = {
  search: { label: 'Hybrid Search', icon: Search, color: 'text-green-600' },
  embedding: { label: 'Embedding', icon: Brain, color: 'text-purple-600' },
  llm: { label: 'LLM Generation', icon: Zap, color: 'text-blue-600' },
  cache: { label: 'Cache', icon: Database, color: 'text-orange-600' },
  routing: { label: 'Routing (L1)', icon: Settings, color: 'text-cyan-600' },
  validation: { label: 'Validation (L4-L6)', icon: Shield, color: 'text-red-600' },
};

// =============================================================================
// Helper Components
// =============================================================================

function StatusBadge({ status }: { status: ImplementationStatus }) {
  const config = {
    implemented: { label: 'POC', className: 'bg-green-100 text-green-700 border-green-300' },
    partial: { label: 'Partial', className: 'bg-yellow-100 text-yellow-700 border-yellow-300' },
    planned: { label: 'Planned', className: 'bg-gray-100 text-gray-600 border-gray-300' },
  };
  const { label, className } = config[status];
  return <Badge className={cn('text-xs', className)}>{label}</Badge>;
}

function _EffectIndicator({ effect }: { effect: 'positive' | 'negative' | 'neutral' }) {
  if (effect === 'positive') return <TrendingUp className="h-4 w-4 text-green-500" />;
  if (effect === 'negative') return <TrendingDown className="h-4 w-4 text-red-500" />;
  return <Minus className="h-4 w-4 text-gray-400" />;
}

// =============================================================================
// Parameter Section Component
// =============================================================================

interface ParameterSectionProps {
  category: keyof typeof CATEGORY_INFO;
  parameters: Parameter[];
}

function ParameterSection({ category, parameters }: ParameterSectionProps) {
  const [isExpanded, setIsExpanded] = useState(true);
  const info = CATEGORY_INFO[category];
  const Icon = info.icon;

  const implemented = parameters.filter(p => p.status === 'implemented').length;
  const total = parameters.length;

  return (
    <div className="border rounded-lg overflow-hidden">
      <button
        className="w-full p-4 flex items-center justify-between hover:bg-muted/30 transition-colors"
        onClick={() => setIsExpanded(!isExpanded)}
      >
        <div className="flex items-center gap-3">
          <Icon className={cn('h-5 w-5', info.color)} />
          <span className="font-semibold">{info.label}</span>
          <span className="text-xs text-muted-foreground">
            ({implemented}/{total} in POC)
          </span>
        </div>
        {isExpanded ? (
          <ChevronDown className="h-4 w-4 text-muted-foreground" />
        ) : (
          <ChevronRight className="h-4 w-4 text-muted-foreground" />
        )}
      </button>

      {isExpanded && (
        <div className="border-t">
          <table className="w-full text-sm">
            <thead className="bg-muted/30">
              <tr>
                <th className="text-left p-3 font-medium">Parametro</th>
                <th className="text-left p-3 font-medium">Valore</th>
                <th className="text-left p-3 font-medium hidden md:table-cell">Effetto</th>
                <th className="text-center p-3 font-medium w-20">Status</th>
              </tr>
            </thead>
            <tbody>
              {parameters.map(param => (
                <tr key={param.name} className="border-t hover:bg-muted/20">
                  <td className="p-3">
                    <code className="text-xs bg-muted px-1.5 py-0.5 rounded">{param.name}</code>
                    {param.range && (
                      <span className="text-xs text-muted-foreground ml-2">({param.range})</span>
                    )}
                  </td>
                  <td className="p-3 font-mono text-xs">{param.currentValue}</td>
                  <td className="p-3 text-xs text-muted-foreground hidden md:table-cell">
                    {param.effect}
                    {param.tradeoff && (
                      <div className="mt-1 text-orange-600 dark:text-orange-400">
                        ⚖️ {param.tradeoff}
                      </div>
                    )}
                  </td>
                  <td className="p-3 text-center">
                    <StatusBadge status={param.status} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// =============================================================================
// Strategy Comparison Grid
// =============================================================================

function StrategyComparisonGrid() {
  const strategies = Object.values(STRATEGIES);

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm border-collapse">
        <thead>
          <tr className="bg-muted/50">
            <th className="text-left p-3 font-semibold border-b sticky left-0 bg-muted/50">
              Strategy
            </th>
            <th className="text-center p-3 font-semibold border-b">Tokens</th>
            <th className="text-center p-3 font-semibold border-b">Costo</th>
            <th className="text-center p-3 font-semibold border-b">Latenza</th>
            <th className="text-center p-3 font-semibold border-b">Accuracy</th>
            <th className="text-center p-3 font-semibold border-b">Usage</th>
            <th className="text-left p-3 font-semibold border-b hidden lg:table-cell">Modelli</th>
            <th className="text-left p-3 font-semibold border-b hidden xl:table-cell">Use Cases</th>
          </tr>
        </thead>
        <tbody>
          {strategies.map(strategy => (
            <tr
              key={strategy.id}
              className="border-b hover:bg-muted/20 transition-colors"
              style={{ borderLeftColor: strategy.color, borderLeftWidth: '4px' }}
            >
              <td className="p-3 sticky left-0 bg-background">
                <div className="flex items-center gap-2">
                  <span className="text-lg">{strategy.icon}</span>
                  <div>
                    <div className="font-semibold">{strategy.name}</div>
                    <div className="text-xs text-muted-foreground">{strategy.description}</div>
                  </div>
                </div>
              </td>
              <td className="p-3 text-center font-mono text-xs">
                {strategy.tokens.toLocaleString()}
              </td>
              <td className="p-3 text-center">
                <span
                  className={cn(
                    'font-mono text-xs px-2 py-1 rounded',
                    strategy.cost === 0
                      ? 'bg-green-100 text-green-700'
                      : strategy.cost < 0.05
                        ? 'bg-blue-100 text-blue-700'
                        : strategy.cost < 0.1
                          ? 'bg-orange-100 text-orange-700'
                          : 'bg-red-100 text-red-700'
                  )}
                >
                  ${strategy.cost.toFixed(4)}
                </span>
              </td>
              <td className="p-3 text-center text-xs">{strategy.latency.display}</td>
              <td className="p-3 text-center">
                <div
                  className="w-full h-2 bg-muted rounded-full overflow-hidden"
                  title={strategy.accuracy.display}
                >
                  <div
                    className="h-full bg-gradient-to-r from-green-400 to-green-600"
                    style={{ width: `${strategy.accuracy.min}%` }}
                  />
                </div>
                <span className="text-xs text-muted-foreground">{strategy.accuracy.display}</span>
              </td>
              <td className="p-3 text-center text-xs text-muted-foreground">
                {strategy.usage.display}
              </td>
              <td className="p-3 hidden lg:table-cell">
                <div className="flex flex-wrap gap-1">
                  {strategy.models.slice(0, 2).map(model => (
                    <Badge key={model} variant="outline" className="text-xs">
                      {model.split('-').slice(0, 2).join('-')}
                    </Badge>
                  ))}
                </div>
              </td>
              <td className="p-3 text-xs text-muted-foreground hidden xl:table-cell">
                {strategy.useCases.slice(0, 2).join(', ')}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// =============================================================================
// Strategy Detail Cards
// =============================================================================

function StrategyDetailCards() {
  const strategies = Object.values(STRATEGIES);

  return (
    <div className="grid md:grid-cols-2 xl:grid-cols-3 gap-4">
      {strategies.map(strategy => (
        <Card
          key={strategy.id}
          className="overflow-hidden"
          style={{ borderTopColor: strategy.color, borderTopWidth: '3px' }}
        >
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-lg">
              <span className="text-xl">{strategy.icon}</span>
              {strategy.name}
            </CardTitle>
            <p className="text-sm text-muted-foreground">{strategy.descriptionEn}</p>
          </CardHeader>
          <CardContent className="space-y-3">
            {/* Metrics */}
            <div className="grid grid-cols-2 gap-2 text-sm">
              <div className="bg-muted/30 p-2 rounded">
                <div className="text-xs text-muted-foreground">Tokens</div>
                <div className="font-mono font-semibold">{strategy.tokens.toLocaleString()}</div>
              </div>
              <div className="bg-muted/30 p-2 rounded">
                <div className="text-xs text-muted-foreground">Cost/query</div>
                <div className="font-mono font-semibold">${strategy.cost.toFixed(4)}</div>
              </div>
              <div className="bg-muted/30 p-2 rounded">
                <div className="text-xs text-muted-foreground">Latency</div>
                <div className="font-semibold">{strategy.latency.display}</div>
              </div>
              <div className="bg-muted/30 p-2 rounded">
                <div className="text-xs text-muted-foreground">Accuracy</div>
                <div className="font-semibold">{strategy.accuracy.display}</div>
              </div>
            </div>

            {/* Phases */}
            <div>
              <div className="text-xs font-medium text-muted-foreground mb-1">Pipeline Layers</div>
              <div className="flex flex-wrap gap-1">
                {strategy.phases.map(phase => (
                  <Badge key={phase} variant="outline" className="text-xs">
                    {phase}
                  </Badge>
                ))}
              </div>
            </div>

            {/* Models */}
            <div>
              <div className="text-xs font-medium text-muted-foreground mb-1">Models</div>
              <div className="flex flex-wrap gap-1">
                {strategy.models.map(model => {
                  const pricing = MODEL_PRICING.find(m => m.id === model);
                  return (
                    <Badge
                      key={model}
                      className={cn(
                        'text-xs',
                        pricing?.isFree
                          ? 'bg-green-100 text-green-700 border-green-300'
                          : 'bg-blue-100 text-blue-700 border-blue-300'
                      )}
                    >
                      {model}
                      {pricing?.isFree && ' (free)'}
                    </Badge>
                  );
                })}
              </div>
            </div>

            {/* Use Cases */}
            <div>
              <div className="text-xs font-medium text-muted-foreground mb-1">Use Cases</div>
              <ul className="text-xs text-muted-foreground space-y-0.5">
                {strategy.useCases.map(useCase => (
                  <li key={useCase}>• {useCase}</li>
                ))}
              </ul>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}

// =============================================================================
// Layer Pipeline Visualization
// =============================================================================

function LayerPipelineVisualization() {
  return (
    <div className="space-y-4">
      <div className="text-sm text-muted-foreground">
        Ogni strategia attraversa un sottoinsieme dei 6 layer TOMAC-RAG:
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-sm border-collapse">
          <thead>
            <tr className="bg-muted/30">
              <th className="text-left p-2 font-medium">Strategy</th>
              {LAYERS.map(layer => (
                <th key={layer.id} className="text-center p-2 font-medium text-xs">
                  <div className="flex flex-col items-center">
                    <span>{layer.icon}</span>
                    <span>{layer.shortName}</span>
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Object.values(STRATEGIES).map(strategy => (
              <tr key={strategy.id} className="border-t">
                <td className="p-2 font-medium">
                  {strategy.icon} {strategy.name}
                </td>
                {LAYERS.map(layer => {
                  const isUsed = strategy.phases.some(
                    p =>
                      p.toLowerCase().includes(layer.shortName.toLowerCase()) ||
                      p.toLowerCase().includes(layer.id.toLowerCase())
                  );
                  return (
                    <td key={layer.id} className="text-center p-2">
                      {isUsed ? (
                        <CheckCircle2 className="h-4 w-4 text-green-500 mx-auto" />
                      ) : (
                        <Circle className="h-4 w-4 text-gray-300 mx-auto" />
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Layer descriptions */}
      <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-3 mt-4">
        {LAYERS.map(layer => (
          <div
            key={layer.id}
            className="p-3 rounded-lg border"
            style={{ borderLeftColor: layer.color, borderLeftWidth: '3px' }}
          >
            <div className="flex items-center gap-2 mb-1">
              <span>{layer.icon}</span>
              <span className="font-medium text-sm">
                {layer.shortName}: {layer.name}
              </span>
            </div>
            <p className="text-xs text-muted-foreground">{layer.description}</p>
            <div className="mt-2 flex gap-2 text-xs">
              <span className="text-muted-foreground">
                {layer.tokenRange.min}-{layer.tokenRange.max} tok
              </span>
              <span className="text-muted-foreground">
                {layer.latencyRange.min}-{layer.latencyRange.max}ms
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

interface ParameterGuideProps {
  className?: string;
}

export function ParameterGuide({ className }: ParameterGuideProps) {
  const categories = ['search', 'embedding', 'llm', 'cache', 'routing', 'validation'] as const;

  return (
    <Card className={cn('', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Settings className="h-5 w-5 text-primary" />
          Parametri & Strategie RAG
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          Guida completa ai parametri configurabili e confronto strategie TOMAC-RAG
        </p>
      </CardHeader>

      <CardContent>
        <Tabs defaultValue="strategies" className="w-full">
          <TabsList className="grid w-full grid-cols-3 mb-6">
            <TabsTrigger value="strategies">Strategie</TabsTrigger>
            <TabsTrigger value="parameters">Parametri</TabsTrigger>
            <TabsTrigger value="layers">Layers</TabsTrigger>
          </TabsList>

          {/* STRATEGIES TAB */}
          <TabsContent value="strategies" className="space-y-6">
            <div>
              <h3 className="text-lg font-semibold mb-3">Confronto Strategie</h3>
              <StrategyComparisonGrid />
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-3">Dettaglio Strategie</h3>
              <StrategyDetailCards />
            </div>
          </TabsContent>

          {/* PARAMETERS TAB */}
          <TabsContent value="parameters" className="space-y-4">
            <div className="flex items-center gap-4 p-3 bg-muted/30 rounded-lg text-sm">
              <div className="flex items-center gap-2">
                <StatusBadge status="implemented" />
                <span>Implementato nel POC</span>
              </div>
              <div className="flex items-center gap-2">
                <StatusBadge status="partial" />
                <span>Parziale</span>
              </div>
              <div className="flex items-center gap-2">
                <StatusBadge status="planned" />
                <span>TOMAC-RAG futuro</span>
              </div>
            </div>

            {categories.map(category => (
              <ParameterSection
                key={category}
                category={category}
                parameters={PARAMETERS.filter(p => p.category === category)}
              />
            ))}
          </TabsContent>

          {/* LAYERS TAB */}
          <TabsContent value="layers" className="space-y-6">
            <LayerPipelineVisualization />
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  );
}
