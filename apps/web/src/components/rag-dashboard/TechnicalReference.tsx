'use client';

/**
 * TechnicalReference Component
 *
 * Deep technical reference showing:
 * - Concrete LLM call examples from RagService
 * - Agent classification and query routing
 * - Strategy technical details with model mapping
 * - Infrastructure services (Docker)
 * - Cosine similarity and tsvector explanations
 *
 * @see docs/03-api/rag/15-technical-reference.md
 */

import React, { useState } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Code2,
  Database,
  Server,
  Cpu,
  Network,
  FileCode,
  Search,
  Bot,
  Zap,
  Scale,
  Target,
  Brain,
  Users,
  Settings,
  ChevronDown,
  ChevronRight,
  Copy,
  Check,
  ExternalLink,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import {
  STRATEGIES,
  AGENT_CLASSIFICATION,
  STRATEGY_MODEL_MAPPING,
  MODEL_PRICING,
} from './rag-data';

import type { RagStrategy } from './types';

// =============================================================================
// Types
// =============================================================================

interface InfraService {
  name: string;
  container: string;
  port: string;
  description: string;
  icon: React.ReactNode;
  color: string;
}

interface CodeExample {
  title: string;
  language: string;
  code: string;
  description: string;
  file?: string;
  lineNumbers?: string;
}

// =============================================================================
// Data
// =============================================================================

const INFRASTRUCTURE_SERVICES: InfraService[] = [
  {
    name: 'PostgreSQL + FTS',
    container: 'meepleai-postgres',
    port: '5432',
    description: 'Database principale con tsvector per full-text search',
    icon: <Database className="h-5 w-5" />,
    color: 'hsl(221 83% 53%)',
  },
  {
    name: 'pgvector (PostgreSQL)',
    container: 'meepleai-postgres',
    port: '5432',
    description: 'Vector database per semantic search con pgvector HNSW',
    icon: <Search className="h-5 w-5" />,
    color: 'hsl(262 83% 62%)',
  },
  {
    name: 'Redis Cache',
    container: 'meepleai-redis',
    port: '6379',
    description: 'Cache per risposte AI e session state',
    icon: <Zap className="h-5 w-5" />,
    color: 'hsl(0 72% 51%)',
  },
  {
    name: 'Ollama LLM',
    container: 'meepleai-ollama',
    port: '11434',
    description: 'LLM locale (Llama 3.3 70B) per strategia FAST',
    icon: <Brain className="h-5 w-5" />,
    color: 'hsl(142 76% 36%)',
  },
  {
    name: 'Embedding Service',
    container: 'meepleai-embedding',
    port: '8000',
    description: 'Generazione embeddings text-embedding-3-large (3072 dim)',
    icon: <Cpu className="h-5 w-5" />,
    color: 'hsl(25 95% 53%)',
  },
  {
    name: 'SmolDocling',
    container: 'meepleai-smoldocling',
    port: '8002',
    description: 'PDF extraction Stage 2 con Vision Language Model',
    icon: <FileCode className="h-5 w-5" />,
    color: 'hsl(280 83% 62%)',
  },
  {
    name: 'Unstructured',
    container: 'meepleai-unstructured',
    port: '8001',
    description: 'PDF extraction Stage 1 con analisi semantica',
    icon: <FileCode className="h-5 w-5" />,
    color: 'hsl(180 76% 40%)',
  },
  {
    name: 'Reranker Service',
    container: 'meepleai-reranker',
    port: '8003',
    description: 'Cross-encoder reranking (BAAI/bge-reranker-v2-m3)',
    icon: <Scale className="h-5 w-5" />,
    color: 'hsl(45 93% 47%)',
  },
  {
    name: 'Orchestrator (LangGraph)',
    container: 'meepleai-orchestrator',
    port: '8004',
    description: 'Multi-agent coordination: Tutor, Arbitro, Stratega, Narratore',
    icon: <Network className="h-5 w-5" />,
    color: 'hsl(320 76% 50%)',
  },
  {
    name: 'Prometheus',
    container: 'meepleai-prometheus',
    port: '9090',
    description: 'Metrics collection e alerting',
    icon: <Server className="h-5 w-5" />,
    color: 'hsl(15 85% 55%)',
  },
];

const CODE_EXAMPLES: Record<string, CodeExample> = {
  llmCall: {
    title: 'LLM Call with Context',
    language: 'csharp',
    file: 'RagService.cs',
    lineNumbers: '752-837',
    description: "Come RagService costruisce il contesto e chiama l'LLM",
    code: `// Step 1: Converti i risultati hybrid in Snippet
var snippets = topResults.Select(r => new Snippet(
    r.Content,
    $"PDF:{r.PdfDocumentId}",
    r.PageNumber ?? 0,
    0,
    r.HybridScore
)).ToList();

// Step 2: Costruisci il CONTESTO dai chunk
var context = string.Join("\\n\\n---\\n\\n", topResults.Select(r =>
    $"[Page {r.PageNumber ?? 0}]\\n{r.Content}"));

// Step 3: Classifica la domanda per scegliere il template
var questionType = _promptTemplateService.ClassifyQuestion(query);

// Step 4: Recupera il template specifico
var template = await _promptTemplateService.GetTemplateAsync(gameGuid, questionType);

// Step 5: Renderizza i prompt
var systemPrompt = _promptTemplateService.RenderSystemPrompt(template);
var userPrompt = _promptTemplateService.RenderUserPrompt(template, context, query);

// Step 6: CHIAMATA LLM
var llmResult = await _llmService.GenerateCompletionAsync(
    systemPrompt,    // Istruzioni sistema con anti-hallucination
    userPrompt,      // Contesto + domanda utente
    cancellationToken
);

// Step 7: Calcola confidence dal miglior score
var confidence = topResults.Count > 0
    ? (double?)topResults.Max(r => r.HybridScore)
    : null;`,
  },
  cosineSimilarity: {
    title: 'Cosine Similarity',
    language: 'text',
    description: 'Formula matematica per la similarità semantica',
    code: `COSINE SIMILARITY - Ricerca Semantica
═══════════════════════════════════════

Formula:
  cos(θ) = (A · B) / (||A|| × ||B||)

dove:
  A · B    = prodotto scalare dei vettori
  ||A||    = norma (lunghezza) del vettore A
  ||B||    = norma del vettore B

Range valori: -1 a 1 (per vettori normalizzati: 0 a 1)
  1.0  = vettori identici (stessa direzione)
  0.0  = vettori ortogonali (nessuna relazione)
  -1.0 = vettori opposti

Configurazione pgvector:
  VectorDimensions = 768  // e5-base
  DistanceMetric = Cosine (<=>)
  HNSW: m=16, ef_construction=200`,
  },
  tsvector: {
    title: 'PostgreSQL tsvector',
    language: 'sql',
    file: 'KeywordSearchService.cs',
    description: 'Full-text search con ts_rank_cd',
    code: `-- PostgreSQL Full-Text Search Query
SELECT
    "Id",
    "Content",
    "PdfDocumentId",
    "GameId",
    "ChunkIndex",
    "PageNumber",
    ts_rank_cd(
        search_vector,
        to_tsquery(@textSearchConfig, @tsQuery),
        @normalization
    ) AS "RelevanceScore"
FROM text_chunks
WHERE
    "GameId" = @gameId::uuid
    AND search_vector @@ to_tsquery(@textSearchConfig, @tsQuery)
ORDER BY "RelevanceScore" DESC
LIMIT @limit

-- Operatori chiave:
-- @@           = match operator
-- to_tsquery() = converte query in formato tsquery
-- ts_rank_cd() = cover density ranking

-- Esempio tsvector:
SELECT to_tsvector('italian', 'Il Re si muove di una casella');
-- Risultato: 'casel':6 'muov':4 're':2`,
  },
  rrfFusion: {
    title: 'RRF Fusion',
    language: 'text',
    description: 'Reciprocal Rank Fusion per hybrid search',
    code: `RECIPROCAL RANK FUSION (RRF)
═══════════════════════════════════════

Formula:
  RRF_score = Σ (1 / (k + rank_i)) × weight_i

Parametri MeepleAI:
  k = 60 (costante standard da Cormack et al. 2009)
  vector_weight = 0.7 (70% semantic)
  keyword_weight = 0.3 (30% keyword)

Esempio calcolo:
  Documento X:
  - Vector search rank: 2
  - Keyword search rank: 5

  RRF_score = (1/(60+2)) × 0.7 + (1/(60+5)) × 0.3
            = 0.0113 × 0.7 + 0.0154 × 0.3
            = 0.0079 + 0.0046
            = 0.0125`,
  },
};

const STRATEGY_ICONS: Record<RagStrategy, React.ReactNode> = {
  FAST: <Zap className="h-4 w-4" />,
  BALANCED: <Scale className="h-4 w-4" />,
  PRECISE: <Target className="h-4 w-4" />,
  EXPERT: <Brain className="h-4 w-4" />,
  CONSENSUS: <Users className="h-4 w-4" />,
  CUSTOM: <Settings className="h-4 w-4" />,
};

// =============================================================================
// Sub-Components
// =============================================================================

interface CodeBlockProps {
  example: CodeExample;
}

function CodeBlock({ example }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);

  const copyToClipboard = async () => {
    await navigator.clipboard.writeText(example.code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="rounded-xl border bg-muted/30 overflow-hidden">
      <div className="flex items-center justify-between px-4 py-2 bg-muted/50 border-b">
        <div className="flex items-center gap-2">
          <Code2 className="h-4 w-4 text-primary" />
          <span className="font-medium text-sm">{example.title}</span>
          {example.file && (
            <Badge variant="outline" className="text-xs">
              {example.file}
              {example.lineNumbers && `:${example.lineNumbers}`}
            </Badge>
          )}
        </div>
        <Button variant="ghost" size="sm" onClick={copyToClipboard} className="h-8 px-2">
          {copied ? <Check className="h-4 w-4 text-green-500" /> : <Copy className="h-4 w-4" />}
        </Button>
      </div>
      <div className="p-4 overflow-x-auto">
        <pre className="text-xs font-mono whitespace-pre-wrap">{example.code}</pre>
      </div>
      <div className="px-4 py-2 bg-muted/30 border-t">
        <p className="text-xs text-muted-foreground">{example.description}</p>
      </div>
    </div>
  );
}

interface ServiceCardProps {
  service: InfraService;
}

function ServiceCard({ service }: ServiceCardProps) {
  return (
    <div className="p-4 rounded-xl border bg-muted/20 hover:bg-muted/30 transition-colors">
      <div className="flex items-start gap-3">
        <div
          className="p-2 rounded-lg"
          style={{ backgroundColor: `${service.color}20`, color: service.color }}
        >
          {service.icon}
        </div>
        <div className="flex-1 min-w-0">
          <div className="font-semibold text-sm">{service.name}</div>
          <div className="text-xs text-muted-foreground mt-1">{service.description}</div>
          <div className="flex items-center gap-2 mt-2">
            <Badge variant="outline" className="text-xs font-mono">
              {service.container}
            </Badge>
            <Badge variant="secondary" className="text-xs font-mono">
              :{service.port}
            </Badge>
          </div>
        </div>
      </div>
    </div>
  );
}

interface AgentClassificationCardProps {
  agent: (typeof AGENT_CLASSIFICATION)[number];
  isExpanded: boolean;
  onToggle: () => void;
}

function AgentClassificationCard({ agent, isExpanded, onToggle }: AgentClassificationCardProps) {
  return (
    <div className="border rounded-xl overflow-hidden">
      <button
        className="w-full p-4 flex items-center justify-between hover:bg-muted/30 transition-colors"
        onClick={onToggle}
      >
        <div className="flex items-center gap-3">
          <div className="p-2 rounded-lg bg-primary/10">
            <Bot className="h-5 w-5 text-primary" />
          </div>
          <div className="text-left">
            <div className="font-semibold text-sm">{agent.agentType}</div>
            <div className="text-xs text-muted-foreground">{agent.queryType}</div>
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
            <div className="px-4 pb-4 space-y-3">
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Description</div>
                <p className="text-sm">{agent.description}</p>
              </div>
              <div>
                <div className="text-xs font-medium text-muted-foreground mb-1">Example Query</div>
                <div className="p-2 rounded bg-muted/50 text-sm font-mono">
                  &quot;{agent.example}&quot;
                </div>
              </div>
              {agent.keywords.length > 0 && (
                <div>
                  <div className="text-xs font-medium text-muted-foreground mb-1">Keywords</div>
                  <div className="flex flex-wrap gap-1">
                    {agent.keywords.map(kw => (
                      <Badge key={kw} variant="secondary" className="text-xs">
                        {kw}
                      </Badge>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

interface StrategyTechnicalCardProps {
  strategyId: RagStrategy;
}

function StrategyTechnicalCard({ strategyId }: StrategyTechnicalCardProps) {
  const strategy = STRATEGIES[strategyId];
  const modelMapping = STRATEGY_MODEL_MAPPING.find(m => m.strategy === strategyId);
  const primaryModel = MODEL_PRICING.find(m => m.id === modelMapping?.primaryModel);

  return (
    <div className="p-4 rounded-xl border bg-muted/20">
      <div className="flex items-center gap-2 mb-3">
        <div className="p-2 rounded-lg" style={{ backgroundColor: `${strategy.color}20` }}>
          <span style={{ color: strategy.color }}>{STRATEGY_ICONS[strategyId]}</span>
        </div>
        <div>
          <div className="font-semibold text-sm flex items-center gap-2">
            {strategy.icon} {strategy.name}
          </div>
          <div className="text-xs text-muted-foreground">{strategy.description}</div>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2 mb-3">
        <div className="p-2 rounded bg-muted/50 text-center">
          <div className="text-xs text-muted-foreground">Tokens</div>
          <div className="font-mono text-sm">{strategy.tokens.toLocaleString()}</div>
        </div>
        <div className="p-2 rounded bg-muted/50 text-center">
          <div className="text-xs text-muted-foreground">Latency</div>
          <div className="font-mono text-sm">{strategy.latency.display}</div>
        </div>
        <div className="p-2 rounded bg-muted/50 text-center">
          <div className="text-xs text-muted-foreground">Accuracy</div>
          <div className="font-mono text-sm">{strategy.accuracy.display}</div>
        </div>
        <div className="p-2 rounded bg-muted/50 text-center">
          <div className="text-xs text-muted-foreground">Cost</div>
          <div className="font-mono text-sm">${strategy.cost.toFixed(4)}</div>
        </div>
      </div>

      {modelMapping && (
        <div className="space-y-2">
          <div className="text-xs font-medium text-muted-foreground">Model Routing</div>
          <div className="flex items-center gap-2">
            <Badge variant="default" className="text-xs">
              Primary: {modelMapping.primaryModel}
            </Badge>
            <Badge variant="outline" className="text-xs">
              {modelMapping.provider}
            </Badge>
          </div>
          {modelMapping.fallbackModels.length > 0 && (
            <div className="flex flex-wrap gap-1">
              <span className="text-xs text-muted-foreground">Fallback:</span>
              {modelMapping.fallbackModels.map(model => (
                <Badge key={model} variant="secondary" className="text-xs">
                  {model}
                </Badge>
              ))}
            </div>
          )}
          {primaryModel && !primaryModel.isFree && (
            <div className="text-xs text-muted-foreground">
              Input: ${primaryModel.inputCost}/1M tokens | Output: ${primaryModel.outputCost}/1M
              tokens
            </div>
          )}
          {primaryModel?.isFree && (
            <Badge variant="outline" className="text-xs text-green-600 border-green-600">
              Free Model
            </Badge>
          )}
        </div>
      )}

      <div className="mt-3 pt-3 border-t">
        <div className="text-xs font-medium text-muted-foreground mb-2">Phases</div>
        <div className="flex flex-wrap gap-1">
          {strategy.phases.map(phase => (
            <Badge key={phase} variant="outline" className="text-xs">
              {phase}
            </Badge>
          ))}
        </div>
      </div>
    </div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface TechnicalReferenceProps {
  className?: string;
}

export function TechnicalReference({ className }: TechnicalReferenceProps) {
  const [expandedAgent, setExpandedAgent] = useState<string | null>('CitationVerification');
  const [activeTab, setActiveTab] = useState('llm-flow');

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Code2 className="h-5 w-5 text-primary" />
          Technical Reference
        </CardTitle>
        <p className="text-sm text-muted-foreground mt-1">
          Deep dive into RAG implementation: code examples, agent classification, and infrastructure
        </p>
      </CardHeader>

      <CardContent>
        <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
          <TabsList className="grid w-full grid-cols-4 mb-6">
            <TabsTrigger value="llm-flow">LLM Flow</TabsTrigger>
            <TabsTrigger value="agents">Agents</TabsTrigger>
            <TabsTrigger value="strategies">Strategies</TabsTrigger>
            <TabsTrigger value="infra">Infrastructure</TabsTrigger>
          </TabsList>

          {/* LLM Flow Tab */}
          <TabsContent value="llm-flow" className="space-y-6">
            <div className="space-y-4">
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <span className="w-6 h-6 rounded-full bg-primary/20 text-primary text-xs flex items-center justify-center">
                  1
                </span>
                RagService → LLM Call
              </h3>
              <CodeBlock example={CODE_EXAMPLES.llmCall} />
            </div>

            <div className="grid lg:grid-cols-2 gap-4">
              <div className="space-y-4">
                <h3 className="text-sm font-semibold flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-primary/20 text-primary text-xs flex items-center justify-center">
                    2
                  </span>
                  Cosine Similarity (Vector Search)
                </h3>
                <CodeBlock example={CODE_EXAMPLES.cosineSimilarity} />
              </div>

              <div className="space-y-4">
                <h3 className="text-sm font-semibold flex items-center gap-2">
                  <span className="w-6 h-6 rounded-full bg-primary/20 text-primary text-xs flex items-center justify-center">
                    3
                  </span>
                  RRF Fusion (Hybrid Search)
                </h3>
                <CodeBlock example={CODE_EXAMPLES.rrfFusion} />
              </div>
            </div>

            <div className="space-y-4">
              <h3 className="text-sm font-semibold flex items-center gap-2">
                <span className="w-6 h-6 rounded-full bg-primary/20 text-primary text-xs flex items-center justify-center">
                  4
                </span>
                PostgreSQL tsvector (Keyword Search)
              </h3>
              <CodeBlock example={CODE_EXAMPLES.tsvector} />
            </div>

            <div className="p-4 rounded-xl bg-muted/30 border">
              <div className="flex items-center gap-2 mb-3">
                <ExternalLink className="h-4 w-4 text-primary" />
                <span className="font-medium text-sm">Full Documentation</span>
              </div>
              <p className="text-xs text-muted-foreground mb-2">
                Per la documentazione completa con diagrammi di flusso e reference dei file:
              </p>
              <a
                href="/docs/03-api/rag/15-technical-reference.md"
                target="_blank"
                className="text-xs text-primary hover:underline"
              >
                docs/03-api/rag/15-technical-reference.md →
              </a>
            </div>
          </TabsContent>

          {/* Agents Tab */}
          <TabsContent value="agents" className="space-y-6">
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold">Agent Classification</h3>
                <Badge variant="outline">{AGENT_CLASSIFICATION.length} Agent Types</Badge>
              </div>
              <p className="text-xs text-muted-foreground">
                Query classification basata su keyword pattern matching. Il primo match vince,
                default: GeneralQuestion.
              </p>

              <div className="space-y-2">
                {AGENT_CLASSIFICATION.map(agent => (
                  <AgentClassificationCard
                    key={agent.queryType}
                    agent={agent}
                    isExpanded={expandedAgent === agent.queryType}
                    onToggle={() =>
                      setExpandedAgent(expandedAgent === agent.queryType ? null : agent.queryType)
                    }
                  />
                ))}
              </div>
            </div>

            <div className="p-4 rounded-xl bg-blue-500/10 border border-blue-500/20">
              <div className="font-semibold text-sm text-blue-600 dark:text-blue-400 mb-2">
                Classification Flow
              </div>
              <div className="text-xs space-y-1">
                <p>1. Query lowercase → pattern matching sui keywords</p>
                <p>2. First match wins (ordine di priorità)</p>
                <p>3. Se nessun match → GeneralAgent (default)</p>
                <p>4. Agent type determina system prompt e template</p>
              </div>
            </div>
          </TabsContent>

          {/* Strategies Tab */}
          <TabsContent value="strategies" className="space-y-6">
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold">Strategy Technical Details</h3>
                <Badge variant="outline">6 Strategies</Badge>
              </div>
              <p className="text-xs text-muted-foreground">
                Ogni strategia ha un primary model e fallback. Il routing è basato su user tier
                (accesso) + strategy (modello).
              </p>

              <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-4">
                {(
                  ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'] as RagStrategy[]
                ).map(strategyId => (
                  <StrategyTechnicalCard key={strategyId} strategyId={strategyId} />
                ))}
              </div>
            </div>

            <div className="p-4 rounded-xl bg-purple-500/10 border border-purple-500/20">
              <div className="font-semibold text-sm text-purple-600 dark:text-purple-400 mb-2">
                Architecture Principle
              </div>
              <div className="text-xs space-y-1">
                <p>
                  <strong>User Tier → Available Strategies</strong>: Il tier determina quali
                  strategie sono accessibili
                </p>
                <p>
                  <strong>Strategy → Model</strong>: La strategia determina quale LLM usare (NOT il
                  tier!)
                </p>
                <p>
                  <strong>Admin Override</strong>: Gli admin possono customizzare il model mapping
                </p>
              </div>
            </div>
          </TabsContent>

          {/* Infrastructure Tab */}
          <TabsContent value="infra" className="space-y-6">
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <h3 className="text-sm font-semibold">Docker Infrastructure</h3>
                <Badge variant="outline">{INFRASTRUCTURE_SERVICES.length} Services</Badge>
              </div>
              <p className="text-xs text-muted-foreground">
                Tutti i servizi sono definiti in{' '}
                <code className="font-mono bg-muted px-1 rounded">infra/docker-compose.yml</code>
              </p>

              <div className="grid md:grid-cols-2 gap-4">
                {INFRASTRUCTURE_SERVICES.map(service => (
                  <ServiceCard key={service.container} service={service} />
                ))}
              </div>
            </div>

            <div className="p-4 rounded-xl bg-orange-500/10 border border-orange-500/20">
              <div className="font-semibold text-sm text-orange-600 dark:text-orange-400 mb-2">
                PDF Extraction Pipeline (ADR-003)
              </div>
              <div className="text-xs space-y-2">
                <div className="flex items-center gap-2">
                  <Badge variant="outline">Stage 1</Badge>
                  <span>Unstructured (Quality ≥ 0.80) → Success ~80%</span>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="outline">Stage 2</Badge>
                  <span>SmolDocling VLM (Quality ≥ 0.70) → Success ~15%</span>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="outline">Stage 3</Badge>
                  <span>Docnet (Best Effort) → Success ~5%</span>
                </div>
              </div>
            </div>
          </TabsContent>
        </Tabs>
      </CardContent>
    </Card>
  );
}
