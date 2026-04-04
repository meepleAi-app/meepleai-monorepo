'use client';

/**
 * StrategyFlowVisualizer Component
 *
 * Interactive visualization of RAG strategy execution flows.
 * Shows block-by-block breakdown with click-to-explain modals.
 *
 * Features:
 * - Click block → explanation modal
 * - View examples button
 * - Live test for admin
 * - MeepleAI design system integration
 */

import React, { useState } from 'react';

import { motion } from 'framer-motion';
import { Info, Play, ChevronRight } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

import { BlockExplanationModal } from './BlockExplanationModal';
import { ExampleIOModal } from './ExampleIOModal';
import { STRATEGIES } from './rag-data';

import type { RagStrategy } from './types';

// =============================================================================
// Types
// =============================================================================

interface FlowBlock {
  id: string;
  type: 'input' | 'agent' | 'model' | 'layer' | 'output';
  title: string;
  icon: string;
  params: Record<string, string>;
  explanation: string;
  technicalDetails: string;
  codeReference?: string;
}

// Unused interface - keeping for potential future use
// interface StrategyFlow {
//   strategyId: RagStrategy;
//   blocks: FlowBlock[];
// }

// =============================================================================
// Block Data (from research)
// =============================================================================

const BLOCK_EXPLANATIONS: Record<
  string,
  { explanation: string; technical: string; code?: string }
> = {
  input: {
    explanation:
      "Punto di ingresso per la query dell'utente. Il sistema riceve la domanda e la classifica per complessità e tipo.",
    technical:
      'La query viene preprocessata (lowercase, tokenizzazione) e viene estratto il contesto (GameId, ThreadId se conversazione esistente).',
    code: 'AskQuestionQueryHandler.cs:30-45',
  },
  agent_general: {
    explanation:
      "GeneralAgent è l'agente predefinito per domande semplici. Fornisce risposte concise e dirette.",
    technical:
      'BasePrompt: "You are a helpful assistant. Provide concise answers." DefaultStrategy: FAST. Ottimizzato per FAQ e lookup diretti.',
    code: 'AgentOrchestrationService.cs:150-160',
  },
  agent_rules: {
    explanation:
      "RulesInterpreter è specializzato nell'interpretazione autoritativa delle regole di gioco. Fornisce citazioni precise dal regolamento.",
    technical:
      'BasePrompt: "You are a rules expert. Provide authoritative interpretations with page references." DefaultStrategy: BALANCED. Triggered by keywords: rule, can i, is it legal.',
    code: 'AgentOrchestrationService.cs:140-149',
  },
  model_llama: {
    explanation:
      'Llama 3.3 70B è un modello open-source gratuito, ottimo per query semplici con zero costi.',
    technical:
      'Provider: OpenRouter (free tier). Context: 4K tokens. Speed: ~50 tokens/sec. Accuracy: buona per FAQ, non ideale per ragionamento complesso.',
    code: 'HybridAdaptiveRoutingStrategy.cs:200-215',
  },
  model_deepseek: {
    explanation: 'DeepSeek Chat offre un ottimo rapporto qualità/prezzo per query standard.',
    technical:
      'Provider: DeepSeek. Cost: $0.28/M input, $0.42/M output. Context: 16K tokens. Speed: ~80 tokens/sec. Accuracy: competitiva con modelli premium.',
    code: 'HybridAdaptiveRoutingStrategy.cs:220-235',
  },
  model_sonnet: {
    explanation:
      'Claude Sonnet 4.5 è il modello premium per reasoning complesso e decisioni critiche.',
    technical:
      'Provider: Anthropic. Cost: $3/M input, $15/M output. Context: 200K tokens. Speed: ~100 tokens/sec. Accuracy: state-of-the-art per ragionamento multi-step.',
    code: 'HybridAdaptiveRoutingStrategy.cs:250-265',
  },
  layer_routing: {
    explanation:
      "L1 Routing classifica la query e valida che il tier dell'utente abbia accesso alla strategia selezionata.",
    technical:
      'Usa LLM lightweight per classificare QueryType (RulesInterpretation, Citation, Strategy, General). Verifica TierStrategyAccess. Tokens: 280-360. Latency: 20-50ms.',
    code: 'AgentOrchestrationService.cs:23-69',
  },
  layer_cache: {
    explanation:
      'L2 Cache controlla se esiste una risposta simile già calcolata, evitando retrieval e generation non necessari.',
    technical:
      'Semantic similarity search in Redis. Threshold: cosine > 0.95. Hit rate target: 80%. Se hit, risparmia ~2000 tokens. TTL varia per tier (48h User, 168h Admin).',
    code: 'SemanticCacheDomainService.cs:40-85',
  },
  layer_retrieval: {
    explanation:
      'L3 Retrieval cerca nel database vettoriale i chunk più rilevanti del regolamento del gioco.',
    technical:
      'Vector search in pgvector con cosine similarity. TopK varia (3 per FAST, 5 per BALANCED). MinScore: 0.55. Genera embedding della query e cerca i chunk più simili.',
    code: 'VectorSearchDomainService.cs:20-65',
  },
  layer_crag: {
    explanation:
      'L4 CRAG valuta la qualità dei documenti recuperati e decide se sono sufficienti o se serve ricerca aggiuntiva.',
    technical:
      'CRAG Evaluator (T5-Large) assegna score: Correct (>0.8), Ambiguous (0.5-0.8), Incorrect (<0.5). Se Incorrect → trigger web search o query rewriting.',
    code: 'CragEvaluationService.cs:55-120',
  },
  layer_generation: {
    explanation:
      'L5 Generation sintetizza la risposta finale usando il modello LLM selezionato e il contesto recuperato.',
    technical:
      'Costruisce prompt: SystemPrompt (agent BasePrompt) + UserPrompt (query + retrieved chunks). Chiama LLM con temperature 0.7, max_tokens 1000. Traccia token usage con OpenTelemetry.',
    code: 'AskQuestionQueryHandler.cs:94-120',
  },
  layer_validation: {
    explanation:
      'L6 Validation esegue 5 layer di controllo qualità: confidence, multi-model, citation, hallucination, accuracy tracking.',
    technical:
      'Pipeline: V1 (confidence ≥0.70) → V2 (multi-model similarity ≥0.90) + V3 (citation in PDF) → V4 (no forbidden words) → V5 (accuracy tracking). Parallel execution di V2+V3.',
    code: 'RagValidationPipelineService.cs:55-259',
  },
  layer_multihop: {
    explanation:
      'Multi-Hop Retrieval esegue retrieval iterativo: usa risultati del hop precedente per raffinare la ricerca successiva.',
    technical:
      'Strategia: 1) Initial retrieval (TopK=5), 2) Extract entities from chunks, 3) Search entities (TopK=3), 4) Merge results with RRF fusion. Tipicamente 2-3 hops.',
    code: 'MultiHopRetrievalService.cs:30-95 (future implementation)',
  },
  layer_multiagent: {
    explanation:
      'Multi-Agent Pipeline coordina 3 agenti specializzati: Analyzer, Validator, Synthesizer lavorano in sequenza.',
    technical:
      'LangGraph orchestration: Agent1 analizza query → Agent2 valida sources → Agent3 sintetizza risposta finale. Ogni agente usa diverso modello (Haiku, Sonnet, Opus) progressivamente.',
    code: 'MultiAgentPipelineService.cs:45-180 (future implementation)',
  },
  voting: {
    explanation:
      'Sistema di voting con 3 LLM diversi che generano risposte indipendenti, poi un aggregator sceglie il consenso.',
    technical:
      'Parallel execution: Sonnet 4.5 + GPT-4o + DeepSeek generano simultaneamente. Aggregator (Sonnet) analizza le 3 risposte e sceglie quella con maggior consenso o merge insights.',
    code: 'ConsensusVotingService.cs:60-145 (future implementation)',
  },
};

// Flow definitions for each strategy
const STRATEGY_FLOWS: Record<RagStrategy, FlowBlock[]> = {
  FAST: [
    {
      id: 'fast-input',
      type: 'input',
      title: 'Input Query',
      icon: '📥',
      params: { Tipo: 'Simple FAQ', Complessità: 'Bassa' },
      explanation: BLOCK_EXPLANATIONS['input'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['input'].technical,
      codeReference: BLOCK_EXPLANATIONS['input'].code,
    },
    {
      id: 'fast-agent',
      type: 'agent',
      title: 'GeneralAgent',
      icon: '🤖',
      params: { Prompt: 'Concise', Strategy: 'FAST' },
      explanation: BLOCK_EXPLANATIONS['agent_general'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['agent_general'].technical,
      codeReference: BLOCK_EXPLANATIONS['agent_general'].code,
    },
    {
      id: 'fast-model',
      type: 'model',
      title: 'Llama 3.3 70B',
      icon: '🦙',
      params: { Provider: 'OpenRouter', Cost: 'FREE' },
      explanation: BLOCK_EXPLANATIONS['model_llama'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['model_llama'].technical,
      codeReference: BLOCK_EXPLANATIONS['model_llama'].code,
    },
    {
      id: 'fast-l1',
      type: 'layer',
      title: 'L1: Routing',
      icon: '🧠',
      params: { Tokens: '280-360', Latency: '20-50ms' },
      explanation: BLOCK_EXPLANATIONS['layer_routing'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_routing'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_routing'].code,
    },
    {
      id: 'fast-l2',
      type: 'layer',
      title: 'L2: Cache',
      icon: '💾',
      params: { 'Hit Rate': '80%', Tokens: '~50' },
      explanation: BLOCK_EXPLANATIONS['layer_cache'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_cache'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_cache'].code,
    },
    {
      id: 'fast-l3',
      type: 'layer',
      title: 'L3: Retrieval',
      icon: '📚',
      params: { TopK: '3', Tokens: '~900' },
      explanation: BLOCK_EXPLANATIONS['layer_retrieval'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_retrieval'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_retrieval'].code,
    },
    {
      id: 'fast-l5',
      type: 'layer',
      title: 'L5: Generation',
      icon: '✨',
      params: { Tokens: '~800', Latency: '~100ms' },
      explanation: BLOCK_EXPLANATIONS['layer_generation'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_generation'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_generation'].code,
    },
    {
      id: 'fast-output',
      type: 'output',
      title: 'Response Output',
      icon: '📤',
      params: { Accuracy: '78-85%', Total: '2060 tok' },
      explanation: "Risposta finale all'utente con citazioni dalle fonti recuperate.",
      technicalDetails:
        'QaResponseDto con: Answer, Sources[], Confidence, ValidationResults. Serializzato come JSON e restituito via API.',
      codeReference: 'AskQuestionQueryHandler.cs:180-200',
    },
  ],

  BALANCED: [
    {
      id: 'balanced-input',
      type: 'input',
      title: 'Input Query',
      icon: '📥',
      params: { Tipo: 'Standard', Complessità: 'Media' },
      explanation: BLOCK_EXPLANATIONS['input'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['input'].technical,
      codeReference: BLOCK_EXPLANATIONS['input'].code,
    },
    {
      id: 'balanced-agent',
      type: 'agent',
      title: 'RulesInterpreter',
      icon: '⚖️',
      params: { Prompt: 'Authoritative', Strategy: 'BALANCED' },
      explanation: BLOCK_EXPLANATIONS['agent_rules'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['agent_rules'].technical,
      codeReference: BLOCK_EXPLANATIONS['agent_rules'].code,
    },
    {
      id: 'balanced-model',
      type: 'model',
      title: 'DeepSeek Chat',
      icon: '🤖',
      params: { Provider: 'DeepSeek', Cost: '$0.01' },
      explanation: BLOCK_EXPLANATIONS['model_deepseek'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['model_deepseek'].technical,
      codeReference: BLOCK_EXPLANATIONS['model_deepseek'].code,
    },
    {
      id: 'balanced-l1',
      type: 'layer',
      title: 'L1: Routing',
      icon: '🧠',
      params: { Tokens: '280', Latency: '30ms' },
      explanation: BLOCK_EXPLANATIONS['layer_routing'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_routing'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_routing'].code,
    },
    {
      id: 'balanced-l2',
      type: 'layer',
      title: 'L2: Cache',
      icon: '💾',
      params: { 'Hit Rate': '80%', Tokens: '~50' },
      explanation: BLOCK_EXPLANATIONS['layer_cache'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_cache'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_cache'].code,
    },
    {
      id: 'balanced-l3',
      type: 'layer',
      title: 'L3: Retrieval',
      icon: '📚',
      params: { TopK: '5', Tokens: '~1200' },
      explanation: BLOCK_EXPLANATIONS['layer_retrieval'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_retrieval'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_retrieval'].code,
    },
    {
      id: 'balanced-l4',
      type: 'layer',
      title: 'L4: CRAG Evaluation',
      icon: '✅',
      params: { Model: 'T5-Large', Tokens: '~300' },
      explanation: BLOCK_EXPLANATIONS['layer_crag'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_crag'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_crag'].code,
    },
    {
      id: 'balanced-l5',
      type: 'layer',
      title: 'L5: Generation',
      icon: '✨',
      params: { Tokens: '~1000', Latency: '1-2s' },
      explanation: BLOCK_EXPLANATIONS['layer_generation'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_generation'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_generation'].code,
    },
    {
      id: 'balanced-output',
      type: 'output',
      title: 'Response Output',
      icon: '📤',
      params: { Accuracy: '85-92%', Total: '2820 tok' },
      explanation: 'Risposta validata con CRAG quality gate.',
      technicalDetails: 'Include: Answer, Sources[], Confidence, CragScore, ValidationResults.',
      codeReference: 'AskQuestionQueryHandler.cs:180-200',
    },
  ],

  PRECISE: [
    {
      id: 'precise-input',
      type: 'input',
      title: 'Critical Query',
      icon: '📥',
      params: { Tipo: 'Critical', Complessità: 'Alta' },
      explanation: BLOCK_EXPLANATIONS['input'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['input'].technical,
      codeReference: BLOCK_EXPLANATIONS['input'].code,
    },
    {
      id: 'precise-agent',
      type: 'agent',
      title: 'StrategyAgent',
      icon: '🎯',
      params: { Prompt: 'Strategic', 'Multi-Agent': '3x' },
      explanation: 'StrategyAgent coordina 3 agenti specializzati per analisi critica.',
      technicalDetails:
        'Multi-Agent Pipeline: Analyzer + Validator + Synthesizer. Reasoning sequenziale con handoff.',
      codeReference: 'StrategyAgentService.cs:50-150 (future)',
    },
    {
      id: 'precise-model',
      type: 'model',
      title: 'Claude Sonnet 4.5',
      icon: '✨',
      params: { Provider: 'Anthropic', Cost: '$0.132' },
      explanation: BLOCK_EXPLANATIONS['model_sonnet'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['model_sonnet'].technical,
      codeReference: BLOCK_EXPLANATIONS['model_sonnet'].code,
    },
    {
      id: 'precise-multihop',
      type: 'layer',
      title: 'L3: Multi-Hop (3 hops)',
      icon: '🔄',
      params: { Hops: '3', Tokens: '~5000' },
      explanation: BLOCK_EXPLANATIONS['layer_multihop'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_multihop'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_multihop'].code,
    },
    {
      id: 'precise-grading',
      type: 'layer',
      title: 'L4: LLM Grading',
      icon: '⚖️',
      params: { Model: 'Haiku 4.5', Tokens: '~2000' },
      explanation: 'LLM Grading filtra chunk irrilevanti prima della generation.',
      technicalDetails:
        'Usa Haiku 4.5 per valutare relevance di ogni chunk (0-1 score). Scarta chunk con score < 0.7. Risparmia tokens nella generation.',
      codeReference: 'LlmGradingService.cs:40-90 (future)',
    },
    {
      id: 'precise-multiagent',
      type: 'layer',
      title: 'L5: Multi-Agent (3x)',
      icon: '🤖',
      params: { Agents: '3', Tokens: '~12000' },
      explanation: BLOCK_EXPLANATIONS['layer_multiagent'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_multiagent'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_multiagent'].code,
    },
    {
      id: 'precise-validation',
      type: 'layer',
      title: 'L6: 5-Layer Validation',
      icon: '🔍',
      params: { Layers: '5', Tokens: '~3000' },
      explanation: BLOCK_EXPLANATIONS['layer_validation'].explanation,
      technicalDetails: BLOCK_EXPLANATIONS['layer_validation'].technical,
      codeReference: BLOCK_EXPLANATIONS['layer_validation'].code,
    },
    {
      id: 'precise-output',
      type: 'output',
      title: 'Validated Response',
      icon: '📤',
      params: { Accuracy: '95-98%', Total: '22396 tok' },
      explanation: 'Risposta validata con massima qualità e citazioni multiple.',
      technicalDetails:
        'Include: Answer, Sources[], Confidence, MultiModelAgreement, CitationCoverage, HallucinationScore, AccuracyScore.',
      codeReference: 'AskQuestionQueryHandler.cs:180-200',
    },
  ],

  EXPERT: [],
  CONSENSUS: [],
  CUSTOM: [],
};

// =============================================================================
// Block Component
// =============================================================================

interface FlowBlockProps {
  block: FlowBlock;
  strategyColor: string;
  onClick: () => void;
}

function FlowBlockComponent({ block, strategyColor: _strategyColor, onClick }: FlowBlockProps) {
  const blockColorMap = {
    input: 'hsl(221, 83%, 53%)',
    agent: 'hsl(262, 83%, 62%)',
    model: 'hsl(25, 85%, 45%)',
    layer: 'hsl(142, 76%, 36%)',
    output: 'hsl(25, 95%, 53%)',
  };

  return (
    <motion.div className="relative group cursor-pointer" whileHover={{ x: 8 }} onClick={onClick}>
      <div
        className="p-4 rounded-xl border-l-4 bg-gradient-to-br from-white to-muted/20 hover:to-muted/40 transition-all duration-300"
        style={{ borderLeftColor: blockColorMap[block.type] }}
      >
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-2">
            <span className="text-2xl">{block.icon}</span>
            <span className="font-semibold text-foreground">{block.title}</span>
          </div>
          <Badge variant="outline" className="text-xs uppercase">
            {block.type}
          </Badge>
        </div>

        <div className="grid grid-cols-2 gap-2 text-sm">
          {Object.entries(block.params).map(([key, value]) => (
            <div key={key} className="flex justify-between">
              <span className="text-muted-foreground">{key}:</span>
              <span className="font-semibold text-foreground">{value}</span>
            </div>
          ))}
        </div>

        {/* Click hint */}
        <div className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 transition-opacity">
          <Info className="h-4 w-4 text-muted-foreground" />
        </div>
      </div>
    </motion.div>
  );
}

// =============================================================================
// Strategy Flow Component
// =============================================================================

interface StrategyFlowVisualizerProps {
  strategy: RagStrategy;
  onViewExamples?: () => void;
}

export function StrategyFlowVisualizer({
  strategy,
  onViewExamples: _onViewExamples,
}: StrategyFlowVisualizerProps) {
  const [selectedBlock, setSelectedBlock] = useState<FlowBlock | null>(null);
  const [showExamples, setShowExamples] = useState(false);

  const strategyData = STRATEGIES[strategy];
  const flowBlocks = STRATEGY_FLOWS[strategy] || [];

  if (flowBlocks.length === 0) {
    return (
      <Card className="p-6">
        <p className="text-muted-foreground text-center">
          Flow visualization per {strategy} in arrivo...
        </p>
      </Card>
    );
  }

  return (
    <>
      <Card className="p-6 space-y-4">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border pb-4">
          <div className="flex items-center gap-3">
            <div className="text-4xl">{strategyData.icon}</div>
            <div>
              <h3 className="text-2xl font-quicksand font-bold text-foreground">
                {strategyData.name}
              </h3>
              <p className="text-muted-foreground">{strategyData.description}</p>
            </div>
          </div>

          <Button variant="outline" size="sm" onClick={() => setShowExamples(true)}>
            <Play className="h-4 w-4 mr-2" />
            View Examples
          </Button>
        </div>

        {/* Flow blocks */}
        <div className="space-y-3">
          {flowBlocks.map((block, index) => (
            <React.Fragment key={block.id}>
              <FlowBlockComponent
                block={block}
                strategyColor={strategyData.color}
                onClick={() => setSelectedBlock(block)}
              />

              {index < flowBlocks.length - 1 && (
                <div className="flex justify-center">
                  <ChevronRight
                    className="h-6 w-6 text-muted-foreground rotate-90 opacity-40"
                    style={{ color: strategyData.color }}
                  />
                </div>
              )}
            </React.Fragment>
          ))}
        </div>

        {/* Metrics footer */}
        <div className="grid grid-cols-4 gap-3 pt-4 border-t border-border">
          <div className="text-center p-3 rounded-lg bg-gradient-to-br from-purple-500/10 to-transparent">
            <div className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Tokens</div>
            <div className="text-xl font-bold font-quicksand text-purple-600">
              {strategyData.tokens}
            </div>
          </div>

          <div className="text-center p-3 rounded-lg bg-gradient-to-br from-green-500/10 to-transparent">
            <div className="text-xs text-muted-foreground uppercase tracking-wide mb-1">Cost</div>
            <div className="text-xl font-bold font-quicksand text-green-600">
              ${strategyData.cost}
            </div>
          </div>

          <div className="text-center p-3 rounded-lg bg-gradient-to-br from-orange-500/10 to-transparent">
            <div className="text-xs text-muted-foreground uppercase tracking-wide mb-1">
              Latency
            </div>
            <div className="text-xl font-bold font-quicksand text-orange-600">
              {strategyData.latency.display}
            </div>
          </div>

          <div className="text-center p-3 rounded-lg bg-gradient-to-br from-blue-500/10 to-transparent">
            <div className="text-xs text-muted-foreground uppercase tracking-wide mb-1">
              Accuracy
            </div>
            <div className="text-xl font-bold font-quicksand text-blue-600">
              {strategyData.accuracy.display}
            </div>
          </div>
        </div>
      </Card>

      {/* Modals */}
      <BlockExplanationModal
        block={selectedBlock}
        isOpen={selectedBlock !== null}
        onClose={() => setSelectedBlock(null)}
      />

      <ExampleIOModal
        strategy={strategy}
        isOpen={showExamples}
        onClose={() => setShowExamples(false)}
      />
    </>
  );
}

// =============================================================================
// All Strategies View Component
// =============================================================================

export function AllStrategiesFlowView() {
  return (
    <div className="space-y-8">
      <div className="text-center mb-8">
        <h2 className="text-3xl font-quicksand font-bold mb-2">
          <span className="bg-gradient-to-r from-primary via-purple-500 to-orange-500 bg-clip-text text-transparent">
            Strategy Execution Flows
          </span>
        </h2>
        <p className="text-muted-foreground">
          Esplora come ogni strategia processa la tua query step-by-step
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <StrategyFlowVisualizer strategy="FAST" />
        <StrategyFlowVisualizer strategy="BALANCED" />
        <StrategyFlowVisualizer strategy="PRECISE" />
      </div>
    </div>
  );
}
