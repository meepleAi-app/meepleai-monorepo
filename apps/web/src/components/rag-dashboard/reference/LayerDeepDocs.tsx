'use client';

/**
 * LayerDeepDocs Component
 *
 * Detailed documentation for each of the 6 TOMAC-RAG layers.
 * Includes code examples, decision trees, and configuration options.
 */

import React, { useState } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Layers,
  Brain,
  Database,
  Search,
  CheckCircle2,
  Sparkles,
  Shield,
  Code2,
  GitBranch,
  Settings,
  Lightbulb,
  Copy,
  Check,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// =============================================================================
// Types
// =============================================================================

interface LayerDocumentation {
  id: string;
  name: string;
  shortName: string;
  icon: React.ReactNode;
  color: string;
  description: string;
  purpose: string;
  codeExample: string;
  decisionTree: DecisionNode[];
  configuration: ConfigOption[];
  useCases: UseCase[];
}

interface DecisionNode {
  condition: string;
  yes: string;
  no: string;
}

interface ConfigOption {
  name: string;
  type: string;
  defaultVal: string;
  description: string;
}

interface UseCase {
  scenario: string;
  behavior: string;
  tokens: string;
}

// =============================================================================
// Data - Layer Documentation
// =============================================================================

const LAYER_DOCS: LayerDocumentation[] = [
  {
    id: 'routing',
    name: 'Intelligent Routing',
    shortName: 'L1',
    icon: <Brain className="h-5 w-5" />,
    color: 'hsl(221 83% 53%)',
    description: 'Classifies incoming queries and routes them to the optimal processing pipeline.',
    purpose: 'Analyze query complexity, identify template type, select appropriate RAG strategy.',
    codeExample: `// Layer 1: Intelligent Routing
interface RoutingResult {
  template: QueryTemplate;
  complexity: number; // 0-5
  strategy: RagStrategy;
  tier: UserTier;
  confidence: number;
}

async function routeQuery(
  query: string,
  userTier: UserTier
): Promise<RoutingResult> {
  // 1. Template classification using keyword matching + LLM
  const template = await classifyTemplate(query);

  // 2. Complexity scoring (keywords, length, entities)
  const complexity = calculateComplexity(query);

  // 3. Strategy selection based on tier + complexity
  const strategy = selectStrategy(userTier, complexity, template);

  return {
    template,
    complexity,
    strategy,
    tier: userTier,
    confidence: 0.92
  };
}`,
    decisionTree: [
      { condition: 'Is it a simple rule question?', yes: 'FAST + rule_lookup', no: 'Continue...' },
      {
        condition: 'Does user have Editor+ tier?',
        yes: 'Consider BALANCED/PRECISE',
        no: 'Cap at FAST/BALANCED',
      },
      { condition: 'Complexity score > 3?', yes: 'BALANCED or PRECISE', no: 'FAST' },
      {
        condition: 'Is it strategy advice?',
        yes: 'PRECISE (if allowed)',
        no: 'Use template default',
      },
    ],
    configuration: [
      {
        name: 'maxComplexityScore',
        type: 'number',
        defaultVal: '5',
        description: 'Maximum complexity score (0-5)',
      },
      {
        name: 'templateConfidenceThreshold',
        type: 'number',
        defaultVal: '0.7',
        description: 'Min confidence for template classification',
      },
      {
        name: 'enableLlmClassification',
        type: 'boolean',
        defaultVal: 'true',
        description: 'Use LLM for ambiguous queries',
      },
      {
        name: 'defaultStrategy',
        type: 'RagStrategy',
        defaultVal: 'FAST',
        description: 'Fallback strategy when routing fails',
      },
    ],
    useCases: [
      {
        scenario: 'What are the rules for 7 in Catan?',
        behavior: 'rule_lookup, complexity=1, FAST',
        tokens: '280',
      },
      {
        scenario: 'Help me plan resources for a 4-player game',
        behavior: 'resource_planning, complexity=3, BALANCED',
        tokens: '320',
      },
      {
        scenario: 'Analyze this position and suggest moves',
        behavior: 'strategy_advice, complexity=5, PRECISE',
        tokens: '360',
      },
    ],
  },
  {
    id: 'cache',
    name: 'Semantic Cache',
    shortName: 'L2',
    icon: <Database className="h-5 w-5" />,
    color: 'hsl(262 83% 62%)',
    description: 'Two-tier caching system with exact match and semantic similarity lookup.',
    purpose: 'Reduce redundant LLM calls by returning cached responses for similar queries.',
    codeExample: `// Layer 2: Semantic Cache
interface CacheEntry {
  queryHash: string;
  queryEmbedding: number[];
  response: string;
  timestamp: Date;
  hitCount: number;
}

async function checkCache(
  query: string
): Promise<CacheHit | null> {
  // 1. Exact match check (fast, O(1))
  const exactMatch = await memoryCache.get(hashQuery(query));
  if (exactMatch) {
    return { type: 'exact', response: exactMatch, tokens: 0 };
  }

  // 2. Semantic similarity check (if BALANCED/PRECISE)
  const embedding = await getEmbedding(query);
  const similar = await vectorCache.findSimilar(embedding, {
    threshold: 0.85,
    topK: 1
  });

  if (similar && similar.score > 0.90) {
    return { type: 'semantic', response: similar.response, tokens: 310 };
  }

  return null; // Cache miss
}`,
    decisionTree: [
      {
        condition: 'Exact query match in memory?',
        yes: 'Return cached (0 tokens)',
        no: 'Continue...',
      },
      {
        condition: 'Strategy allows semantic cache?',
        yes: 'Check vector similarity',
        no: 'Cache miss',
      },
      {
        condition: 'Similarity score > 0.90?',
        yes: 'Return cached (310 tokens)',
        no: 'Cache miss',
      },
      {
        condition: 'Similarity score > 0.85?',
        yes: 'Use as context hint',
        no: 'Full retrieval needed',
      },
    ],
    configuration: [
      {
        name: 'exactMatchTtl',
        type: 'number',
        defaultVal: '3600',
        description: 'TTL for exact matches in seconds',
      },
      {
        name: 'semanticThreshold',
        type: 'number',
        defaultVal: '0.85',
        description: 'Minimum similarity for semantic match',
      },
      {
        name: 'maxCacheSize',
        type: 'number',
        defaultVal: '10000',
        description: 'Maximum entries in vector cache',
      },
      {
        name: 'embeddingModel',
        type: 'string',
        defaultVal: 'all-MiniLM-L6-v2',
        description: 'Model for query embeddings',
      },
    ],
    useCases: [
      { scenario: 'Repeated exact query', behavior: 'Memory cache hit', tokens: '0' },
      {
        scenario: 'Similar phrasing (Catan 7 rule vs 7 roll in Catan)',
        behavior: 'Semantic cache hit',
        tokens: '310',
      },
      { scenario: 'Novel query', behavior: 'Cache miss, proceed to L3', tokens: '50' },
    ],
  },
  {
    id: 'retrieval',
    name: 'Modular Retrieval',
    shortName: 'L3',
    icon: <Search className="h-5 w-5" />,
    color: 'hsl(142 76% 36%)',
    description: 'Hybrid search combining vector embeddings and BM25 keyword matching.',
    purpose: 'Find the most relevant documents from the knowledge base for query context.',
    codeExample: `// Layer 3: Modular Retrieval
interface RetrievalResult {
  documents: Document[];
  scores: number[];
  method: 'vector' | 'hybrid' | 'multihop';
}

async function retrieve(
  query: string,
  strategy: RagStrategy
): Promise<RetrievalResult> {
  // Strategy-specific retrieval
  switch (strategy) {
    case 'FAST':
      // Vector-only, top 3
      return vectorSearch(query, { topK: 3 });

    case 'BALANCED':
      // Hybrid: vector + BM25 with RRF fusion
      const vectorResults = await vectorSearch(query, { topK: 5 });
      const bm25Results = await bm25Search(query, { topK: 5 });
      return reciprocalRankFusion(vectorResults, bm25Results);

    case 'PRECISE':
      // Multi-hop retrieval
      return multiHopRetrieval(query, {
        maxHops: 3,
        expandEntities: true,
        webAugmentation: true
      });
  }
}`,
    decisionTree: [
      { condition: 'Strategy is FAST?', yes: 'Vector-only search, top 3', no: 'Continue...' },
      {
        condition: 'Strategy is BALANCED?',
        yes: 'Hybrid search with RRF fusion',
        no: 'PRECISE path',
      },
      {
        condition: 'Query has named entities?',
        yes: 'Multi-hop with entity expansion',
        no: 'Standard multi-hop',
      },
      {
        condition: 'Web augmentation enabled?',
        yes: 'Include web search results',
        no: 'Local KB only',
      },
    ],
    configuration: [
      {
        name: 'vectorTopK',
        type: 'number',
        defaultVal: '5',
        description: 'Number of vector search results',
      },
      {
        name: 'bm25TopK',
        type: 'number',
        defaultVal: '5',
        description: 'Number of BM25 search results',
      },
      { name: 'rrfK', type: 'number', defaultVal: '60', description: 'RRF constant for fusion' },
      {
        name: 'maxHops',
        type: 'number',
        defaultVal: '3',
        description: 'Maximum multi-hop iterations',
      },
      {
        name: 'chunkSize',
        type: 'number',
        defaultVal: '512',
        description: 'Document chunk size in tokens',
      },
    ],
    useCases: [
      { scenario: 'Simple rule query (FAST)', behavior: 'Vector search, 3 chunks', tokens: '1500' },
      {
        scenario: 'Complex setup question (BALANCED)',
        behavior: 'Hybrid search, 5 chunks + metadata',
        tokens: '3500',
      },
      {
        scenario: 'Strategy analysis (PRECISE)',
        behavior: 'Multi-hop, entity expansion, web',
        tokens: '8000',
      },
    ],
  },
  {
    id: 'crag',
    name: 'CRAG Evaluation',
    shortName: 'L4',
    icon: <CheckCircle2 className="h-5 w-5" />,
    color: 'hsl(45 93% 47%)',
    description: 'Corrective RAG evaluation to assess retrieval quality and trigger re-retrieval.',
    purpose: 'Quality gate that detects poor retrieval and can request additional context.',
    codeExample: `// Layer 4: CRAG Evaluation
interface CragResult {
  score: 'correct' | 'ambiguous' | 'incorrect';
  confidence: number;
  action: 'proceed' | 'recompose' | 'web_search';
}

async function evaluateRetrieval(
  query: string,
  documents: Document[]
): Promise<CragResult> {
  // Skip for FAST strategy
  if (strategy === 'FAST') {
    return { score: 'correct', confidence: 0.7, action: 'proceed' };
  }

  // T5-Large relevance evaluation
  const relevanceScore = await t5Evaluate(query, documents);

  if (relevanceScore > 0.8) {
    return { score: 'correct', confidence: relevanceScore, action: 'proceed' };
  }

  if (relevanceScore > 0.5) {
    // Decompose query, recompose retrieval
    return { score: 'ambiguous', confidence: relevanceScore, action: 'recompose' };
  }

  // Low relevance - trigger web augmentation
  return { score: 'incorrect', confidence: relevanceScore, action: 'web_search' };
}`,
    decisionTree: [
      { condition: 'Strategy is FAST?', yes: 'Skip CRAG (0 tokens)', no: 'Run evaluation' },
      { condition: 'T5 score > 0.8?', yes: 'Proceed to generation', no: 'Continue...' },
      { condition: 'T5 score > 0.5?', yes: 'Decompose-recompose', no: 'Trigger web search' },
      {
        condition: 'Web search successful?',
        yes: 'Merge and proceed',
        no: 'Generate with warning',
      },
    ],
    configuration: [
      {
        name: 'correctThreshold',
        type: 'number',
        defaultVal: '0.8',
        description: 'Min score for correct rating',
      },
      {
        name: 'ambiguousThreshold',
        type: 'number',
        defaultVal: '0.5',
        description: 'Min score for ambiguous rating',
      },
      {
        name: 'evaluationModel',
        type: 'string',
        defaultVal: 'T5-Large',
        description: 'Model for relevance evaluation',
      },
      {
        name: 'enableWebAugmentation',
        type: 'boolean',
        defaultVal: 'true',
        description: 'Allow web search for low scores',
      },
    ],
    useCases: [
      { scenario: 'High relevance retrieval', behavior: 'Proceed directly', tokens: '0-500' },
      { scenario: 'Partial match (ambiguous)', behavior: 'Query decomposition', tokens: '500' },
      {
        scenario: 'Poor retrieval (incorrect)',
        behavior: 'Web search augmentation',
        tokens: '500+',
      },
    ],
  },
  {
    id: 'generation',
    name: 'Adaptive Generation',
    shortName: 'L5',
    icon: <Sparkles className="h-5 w-5" />,
    color: 'hsl(25 95% 53%)',
    description: 'LLM response generation with template-specific prompts and model selection.',
    purpose: 'Generate high-quality responses using the appropriate model and prompt template.',
    codeExample: `// Layer 5: Adaptive Generation
interface GenerationResult {
  response: string;
  model: string;
  tokens: { input: number; output: number };
  confidence: number;
}

async function generate(
  query: string,
  context: Document[],
  template: QueryTemplate,
  strategy: RagStrategy
): Promise<GenerationResult> {
  // Select model based on strategy
  const model = selectModel(strategy);

  // Build template-specific prompt
  const prompt = buildPrompt({
    systemPrompt: getSystemPrompt(template),
    context: formatContext(context),
    query,
    outputFormat: getOutputFormat(template)
  });

  // For PRECISE: Multi-agent pipeline
  if (strategy === 'PRECISE') {
    return multiAgentGeneration(prompt, context);
  }

  // Single-pass generation
  return singlePassGeneration(prompt, model);
}`,
    decisionTree: [
      { condition: 'Strategy is FAST?', yes: 'Llama/Gemini (free)', no: 'Continue...' },
      { condition: 'Strategy is BALANCED?', yes: 'Claude Sonnet/DeepSeek', no: 'PRECISE path' },
      {
        condition: 'PRECISE strategy?',
        yes: 'Multi-agent: Analyzer-Synthesizer-Validator',
        no: 'Error',
      },
      {
        condition: 'Template requires citations?',
        yes: 'Add citation extraction step',
        no: 'Standard output',
      },
    ],
    configuration: [
      {
        name: 'maxOutputTokens',
        type: 'number',
        defaultVal: '2000',
        description: 'Maximum response tokens',
      },
      {
        name: 'temperature',
        type: 'number',
        defaultVal: '0.3',
        description: 'LLM temperature (lower = more focused)',
      },
      {
        name: 'enableStreaming',
        type: 'boolean',
        defaultVal: 'true',
        description: 'Stream response to user',
      },
      {
        name: 'citationRequired',
        type: 'boolean',
        defaultVal: 'true',
        description: 'Require citations in output',
      },
    ],
    useCases: [
      { scenario: 'Simple rule query', behavior: 'Single-pass with Llama', tokens: '1800-2000' },
      {
        scenario: 'Complex setup guide',
        behavior: 'Claude Sonnet with structured output',
        tokens: '3000-3500',
      },
      {
        scenario: 'Strategy analysis',
        behavior: '3-agent pipeline with Opus',
        tokens: '8000-12000',
      },
    ],
  },
  {
    id: 'validation',
    name: 'Self-Validation',
    shortName: 'L6',
    icon: <Shield className="h-5 w-5" />,
    color: 'hsl(0 72% 51%)',
    description: 'Citation verification and hallucination detection using Self-RAG reflection.',
    purpose: 'Ensure response accuracy through citation checking and confidence scoring.',
    codeExample: `// Layer 6: Self-Validation
interface ValidationResult {
  isValid: boolean;
  citationsVerified: number;
  citationsMissing: number;
  hallucinationRisk: number;
  confidenceScore: number;
  feedback?: string;
}

async function validateResponse(
  response: string,
  context: Document[],
  strategy: RagStrategy
): Promise<ValidationResult> {
  // FAST: Rule-based citation check only
  if (strategy === 'FAST') {
    return ruleBasedCitationCheck(response, context);
  }

  // BALANCED: Cross-encoder alignment
  if (strategy === 'BALANCED') {
    return crossEncoderValidation(response, context);
  }

  // PRECISE: Full Self-RAG reflection
  const reflection = await selfRagReflection(response, context);

  if (reflection.confidence < 0.7) {
    // Trigger regeneration with feedback
    return {
      isValid: false,
      ...reflection,
      feedback: 'Low confidence - regenerate with more context'
    };
  }

  return { isValid: true, ...reflection };
}`,
    decisionTree: [
      {
        condition: 'Strategy is FAST?',
        yes: 'Rule-based citation check (0 tokens)',
        no: 'Continue...',
      },
      {
        condition: 'Strategy is BALANCED?',
        yes: 'Cross-encoder alignment (0 tokens)',
        no: 'PRECISE path',
      },
      {
        condition: 'Self-RAG confidence > 0.7?',
        yes: 'Accept response',
        no: 'Regenerate with feedback',
      },
      { condition: 'Hallucination detected?', yes: 'Flag and regenerate', no: 'Pass validation' },
    ],
    configuration: [
      {
        name: 'confidenceThreshold',
        type: 'number',
        defaultVal: '0.7',
        description: 'Min confidence to accept response',
      },
      {
        name: 'maxReflectionIterations',
        type: 'number',
        defaultVal: '2',
        description: 'Max Self-RAG iterations',
      },
      {
        name: 'crossEncoderModel',
        type: 'string',
        defaultVal: 'ms-marco-MiniLM',
        description: 'Cross-encoder for alignment',
      },
      {
        name: 'enableHallucinationDetection',
        type: 'boolean',
        defaultVal: 'true',
        description: 'Check for unsupported claims',
      },
    ],
    useCases: [
      { scenario: 'FAST response', behavior: 'Regex citation check', tokens: '0' },
      { scenario: 'BALANCED response', behavior: 'Cross-encoder scoring', tokens: '0' },
      { scenario: 'PRECISE response', behavior: 'Self-RAG reflection loop', tokens: '3500-4400' },
    ],
  },
];

// =============================================================================
// Sub-Components
// =============================================================================

interface LayerTabProps {
  layer: LayerDocumentation;
  isActive: boolean;
  onClick: () => void;
}

function LayerTab({ layer, isActive, onClick }: LayerTabProps) {
  return (
    <button
      className={cn(
        'flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-all',
        'border hover:border-primary/50',
        isActive ? 'border-primary bg-primary/10' : 'border-border bg-muted/30'
      )}
      onClick={onClick}
      style={
        isActive
          ? { borderColor: layer.color, backgroundColor: `${layer.color}15`, color: layer.color }
          : undefined
      }
    >
      <span style={{ color: isActive ? layer.color : undefined }}>{layer.icon}</span>
      <span className="hidden sm:inline">{layer.shortName}:</span>
      <span>{layer.name}</span>
    </button>
  );
}

interface CodeBlockProps {
  code: string;
  title?: string;
}

function CodeBlock({ code, title }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    navigator.clipboard.writeText(code);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="relative">
      {title && (
        <div className="flex items-center justify-between mb-2">
          <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
            <Code2 className="h-3 w-3" />
            {title}
          </div>
          <Button variant="ghost" size="sm" onClick={handleCopy} className="h-7 px-2">
            {copied ? <Check className="h-3 w-3 text-green-500" /> : <Copy className="h-3 w-3" />}
          </Button>
        </div>
      )}
      <pre className="text-xs bg-muted/50 p-4 rounded-xl overflow-x-auto font-mono border">
        <code>{code}</code>
      </pre>
    </div>
  );
}

interface DecisionTreeDisplayProps {
  nodes: DecisionNode[];
}

function DecisionTreeDisplay({ nodes }: DecisionTreeDisplayProps) {
  return (
    <div className="space-y-3">
      <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
        <GitBranch className="h-3 w-3" />
        Decision Tree
      </div>
      <div className="space-y-2">
        {nodes.map((node, index) => (
          <div key={index} className="p-3 rounded-lg bg-muted/30 border text-sm">
            <div className="font-medium mb-2">{node.condition}</div>
            <div className="grid grid-cols-2 gap-2 text-xs">
              <div className="p-2 rounded bg-green-500/10 text-green-600 dark:text-green-400">
                <span className="font-medium">Yes:</span> {node.yes}
              </div>
              <div className="p-2 rounded bg-red-500/10 text-red-600 dark:text-red-400">
                <span className="font-medium">No:</span> {node.no}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

interface ConfigTableProps {
  options: ConfigOption[];
}

function ConfigTable({ options }: ConfigTableProps) {
  return (
    <div className="space-y-3">
      <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
        <Settings className="h-3 w-3" />
        Configuration Options
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border">
              <th className="text-left py-2 px-2 font-medium text-muted-foreground">Option</th>
              <th className="text-left py-2 px-2 font-medium text-muted-foreground">Type</th>
              <th className="text-left py-2 px-2 font-medium text-muted-foreground">Default</th>
              <th className="text-left py-2 px-2 font-medium text-muted-foreground">Description</th>
            </tr>
          </thead>
          <tbody>
            {options.map(option => (
              <tr key={option.name} className="border-b border-border/50">
                <td className="py-2 px-2 font-mono text-xs">{option.name}</td>
                <td className="py-2 px-2 text-xs text-muted-foreground">{option.type}</td>
                <td className="py-2 px-2 font-mono text-xs">{option.defaultVal}</td>
                <td className="py-2 px-2 text-xs">{option.description}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

interface UseCaseListProps {
  useCases: UseCase[];
  color: string;
}

function UseCaseList({ useCases, color }: UseCaseListProps) {
  return (
    <div className="space-y-3">
      <div className="text-xs font-medium text-muted-foreground flex items-center gap-2">
        <Lightbulb className="h-3 w-3" />
        Usage Examples
      </div>
      <div className="space-y-2">
        {useCases.map((useCase, index) => (
          <div key={index} className="p-3 rounded-lg bg-muted/30 border">
            <div className="text-sm font-medium mb-1">&ldquo;{useCase.scenario}&rdquo;</div>
            <div className="flex items-center justify-between text-xs">
              <span className="text-muted-foreground">{useCase.behavior}</span>
              <span
                className="px-2 py-0.5 rounded-full font-mono"
                style={{ backgroundColor: `${color}20`, color }}
              >
                ~{useCase.tokens} tokens
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

interface LayerDeepDocsProps {
  className?: string;
}

export function LayerDeepDocs({ className }: LayerDeepDocsProps) {
  const [activeLayerId, setActiveLayerId] = useState<string>('routing');
  const [activeTab, setActiveTab] = useState<'code' | 'tree' | 'config' | 'cases'>('code');

  const activeLayer = LAYER_DOCS.find(l => l.id === activeLayerId);

  if (!activeLayer) return null;

  return (
    <Card className={cn('rag-card', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Layers className="h-5 w-5 text-primary" />
          Layer Deep Documentation
        </CardTitle>
        <p className="text-sm text-muted-foreground mt-1">
          Detailed technical documentation for each TOMAC-RAG layer
        </p>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Layer Tabs */}
        <div className="flex flex-wrap gap-2 pb-4 border-b border-border">
          {LAYER_DOCS.map(layer => (
            <LayerTab
              key={layer.id}
              layer={layer}
              isActive={activeLayerId === layer.id}
              onClick={() => setActiveLayerId(layer.id)}
            />
          ))}
        </div>

        {/* Layer Overview */}
        <AnimatePresence mode="wait">
          <motion.div
            key={activeLayer.id}
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -10 }}
            transition={{ duration: 0.2 }}
            className="space-y-6"
          >
            {/* Header */}
            <div className="flex items-start gap-4">
              <div className="p-3 rounded-xl" style={{ backgroundColor: `${activeLayer.color}20` }}>
                <span style={{ color: activeLayer.color }}>{activeLayer.icon}</span>
              </div>
              <div>
                <h3 className="text-lg font-semibold" style={{ color: activeLayer.color }}>
                  {activeLayer.shortName}: {activeLayer.name}
                </h3>
                <p className="text-sm text-muted-foreground mt-1">{activeLayer.description}</p>
                <p className="text-sm mt-2">
                  <span className="font-medium">Purpose:</span> {activeLayer.purpose}
                </p>
              </div>
            </div>

            {/* Content Tabs */}
            <div className="flex gap-2 border-b border-border pb-2">
              {[
                { id: 'code', label: 'Code Example', icon: <Code2 className="h-3 w-3" /> },
                { id: 'tree', label: 'Decision Tree', icon: <GitBranch className="h-3 w-3" /> },
                { id: 'config', label: 'Configuration', icon: <Settings className="h-3 w-3" /> },
                { id: 'cases', label: 'Use Cases', icon: <Lightbulb className="h-3 w-3" /> },
              ].map(tab => (
                <button
                  key={tab.id}
                  className={cn(
                    'flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium transition-all',
                    activeTab === tab.id
                      ? 'bg-primary/10 text-primary'
                      : 'text-muted-foreground hover:text-foreground hover:bg-muted/50'
                  )}
                  onClick={() => setActiveTab(tab.id as typeof activeTab)}
                >
                  {tab.icon}
                  {tab.label}
                </button>
              ))}
            </div>

            {/* Content Area */}
            <div className="min-h-[300px]">
              {activeTab === 'code' && (
                <CodeBlock code={activeLayer.codeExample} title="Implementation Example" />
              )}
              {activeTab === 'tree' && <DecisionTreeDisplay nodes={activeLayer.decisionTree} />}
              {activeTab === 'config' && <ConfigTable options={activeLayer.configuration} />}
              {activeTab === 'cases' && (
                <UseCaseList useCases={activeLayer.useCases} color={activeLayer.color} />
              )}
            </div>
          </motion.div>
        </AnimatePresence>
      </CardContent>
    </Card>
  );
}
