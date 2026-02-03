'use client';

/**
 * TokenFlowVisualizer Component
 *
 * Animated diagram showing token flow through the 6 TOMAC-RAG layers.
 * Features: click-to-expand modal, strategy toggle, animated token bars,
 * detailed flow visualization per layer.
 */

import React, { useState, useMemo } from 'react';

import { motion, AnimatePresence } from 'framer-motion';
import {
  Sparkles,
  ChevronDown,
  ChevronRight,
  X,
  ArrowRight,
  Zap,
  Database,
  Search,
  CheckCircle,
  MessageSquare,
  Shield,
  Globe,
  Users,
  Settings,
  Brain,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import { RAG_LAYERS, STRATEGY_CONFIGS } from './types';

import type { RagStrategy, RagLayer } from './types';

// =============================================================================
// Token Estimates by Strategy (Extended to 6 strategies)
// =============================================================================

const STRATEGY_TOKENS: Record<RagStrategy, Record<string, { input: number; output: number }>> = {
  FAST: {
    routing: { input: 280, output: 40 },
    cache: { input: 50, output: 0 },
    retrieval: { input: 1500, output: 0 },
    crag: { input: 0, output: 0 },
    generation: { input: 1800, output: 200 },
    validation: { input: 0, output: 0 },
  },
  BALANCED: {
    routing: { input: 320, output: 40 },
    cache: { input: 310, output: 0 },
    retrieval: { input: 3500, output: 0 },
    crag: { input: 500, output: 0 },
    generation: { input: 3000, output: 350 },
    validation: { input: 0, output: 0 },
  },
  PRECISE: {
    routing: { input: 360, output: 50 },
    cache: { input: 310, output: 0 },
    retrieval: { input: 8000, output: 0 },
    crag: { input: 500, output: 0 },
    generation: { input: 8000, output: 500 },
    validation: { input: 3500, output: 900 },
  },
  EXPERT: {
    routing: { input: 400, output: 60 },
    cache: { input: 310, output: 0 },
    retrieval: { input: 5000, output: 0 },
    crag: { input: 500, output: 0 },
    generation: { input: 6000, output: 600 },
    validation: { input: 2000, output: 400 },
  },
  CONSENSUS: {
    routing: { input: 450, output: 70 },
    cache: { input: 310, output: 0 },
    retrieval: { input: 4000, output: 0 },
    crag: { input: 500, output: 0 },
    generation: { input: 10000, output: 1200 },
    validation: { input: 1500, output: 300 },
  },
  // CUSTOM removed: Token flow depends on Admin-defined pipeline configuration
  // Will be calculated dynamically from strategy definition in future (Epic #3413)
  CUSTOM: {
    routing: { input: 0, output: 0 },
    cache: { input: 0, output: 0 },
    retrieval: { input: 0, output: 0 },
    crag: { input: 0, output: 0 },
    generation: { input: 0, output: 0 },
    validation: { input: 0, output: 0 },
  },
};

// =============================================================================
// Layer Flow Data - Detailed explanations and internal flow
// =============================================================================

interface LayerFlowStep {
  id: string;
  name: string;
  description: string;
  icon: React.ReactNode;
  tokens?: number;
}

interface LayerDetailData {
  title: string;
  fullDescription: string;
  technicalDetails: string[];
  flowSteps: LayerFlowStep[];
  models: Record<RagStrategy, string>;
  optimizationTips: string[];
}

const LAYER_FLOW_DATA: Record<string, LayerDetailData> = {
  routing: {
    title: 'Layer 1: Intelligent Routing',
    fullDescription:
      'Il routing layer analizza ogni query in ingresso per determinare la strategia RAG ottimale. Utilizza un classificatore che valuta: complessità della query, template richiesto, tier dell\'utente e contesto storico.',
    technicalDetails: [
      'Classificazione 3D: User Tier × Query Template × Complexity Score',
      'Complexity scoring: 0-5 basato su lunghezza, entità, operatori logici',
      '5 Template types: rule_lookup, setup_guide, strategy_advice, resource_planning, educational',
      'Routing decision in <50ms per queries semplici',
    ],
    flowSteps: [
      { id: 'input', name: 'Query Input', description: 'Riceve la query utente', icon: <MessageSquare className="h-4 w-4" /> },
      { id: 'classify', name: 'Template Classification', description: 'Identifica il tipo di query', icon: <Brain className="h-4 w-4" />, tokens: 150 },
      { id: 'score', name: 'Complexity Scoring', description: 'Calcola punteggio 0-5', icon: <Zap className="h-4 w-4" />, tokens: 80 },
      { id: 'select', name: 'Strategy Selection', description: 'Sceglie FAST/BALANCED/PRECISE/...', icon: <Settings className="h-4 w-4" />, tokens: 50 },
      { id: 'output', name: 'Route to Layer 2', description: 'Passa al cache layer', icon: <ArrowRight className="h-4 w-4" /> },
    ],
    models: {
      FAST: 'Keyword matching (no LLM)',
      BALANCED: 'Claude 3.5 Haiku',
      PRECISE: 'Claude 3.5 Sonnet',
      EXPERT: 'Claude 3.5 Sonnet',
      CONSENSUS: 'Claude 3.5 Sonnet',
      CUSTOM: 'Configurable',
    },
    optimizationTips: [
      'Cache dei pattern di routing più frequenti',
      'Usa keyword matching per queries FAQ note',
      'Pre-classifica queries per game specifico',
    ],
  },
  cache: {
    title: 'Layer 2: Semantic Cache',
    fullDescription:
      'Il semantic cache intercetta queries simili già processate, riducendo fino all\'80% dei costi LLM. Combina cache esatto (Redis) con similarity matching semantico per catturare variazioni nella formulazione.',
    technicalDetails: [
      'Two-tier: Memory Cache (exact) + Semantic Cache (similarity)',
      'Soglia similarità: 0.85 per hit semantico',
      'TTL configurabile per tipo di contenuto (rules: 24h, strategy: 1h)',
      'Invalidazione automatica su aggiornamento rulebook',
    ],
    flowSteps: [
      { id: 'hash', name: 'Query Hash', description: 'Genera hash per exact match', icon: <Database className="h-4 w-4" />, tokens: 0 },
      { id: 'exact', name: 'Exact Match Check', description: 'Cerca in Redis cache', icon: <Search className="h-4 w-4" />, tokens: 0 },
      { id: 'embed', name: 'Query Embedding', description: 'Genera embedding per similarity', icon: <Brain className="h-4 w-4" />, tokens: 50 },
      { id: 'similar', name: 'Similarity Search', description: 'Cerca queries simili in Qdrant', icon: <Search className="h-4 w-4" />, tokens: 0 },
      { id: 'decide', name: 'Hit/Miss Decision', description: 'Ritorna cached o procedi', icon: <CheckCircle className="h-4 w-4" /> },
    ],
    models: {
      FAST: 'Exact match only (50 tokens)',
      BALANCED: 'Semantic similarity check',
      PRECISE: 'Full semantic with 0.85 threshold',
      EXPERT: 'Semantic + context check',
      CONSENSUS: 'Semantic with voting history',
      CUSTOM: 'Configurable threshold',
    },
    optimizationTips: [
      '80% hit rate target = 80% cost reduction',
      'Pre-warm cache con FAQ più frequenti',
      'Separate cache per game per evitare collisioni',
    ],
  },
  retrieval: {
    title: 'Layer 3: Modular Retrieval',
    fullDescription:
      'Il retrieval layer recupera i documenti rilevanti usando una strategia ibrida: vector search per similarità semantica + BM25 per keyword matching esatto. Supporta metadata filtering per game, section, version.',
    technicalDetails: [
      'Hybrid search: Vector (Qdrant) + BM25 (keyword)',
      'Reciprocal Rank Fusion per combinare risultati',
      'Metadata filtering: game_id, section, rule_type, version',
      'Chunk size: 512 tokens con 50 token overlap',
    ],
    flowSteps: [
      { id: 'rewrite', name: 'Query Rewriting', description: 'Espande/riformula query', icon: <MessageSquare className="h-4 w-4" />, tokens: 200 },
      { id: 'vector', name: 'Vector Search', description: 'Similarità semantica in Qdrant', icon: <Database className="h-4 w-4" />, tokens: 0 },
      { id: 'bm25', name: 'BM25 Search', description: 'Keyword matching esatto', icon: <Search className="h-4 w-4" />, tokens: 0 },
      { id: 'fusion', name: 'RRF Fusion', description: 'Combina e rerank risultati', icon: <Zap className="h-4 w-4" />, tokens: 0 },
      { id: 'filter', name: 'Metadata Filter', description: 'Filtra per game/section', icon: <Settings className="h-4 w-4" />, tokens: 0 },
    ],
    models: {
      FAST: 'Vector only, top-3',
      BALANCED: 'Hybrid, top-5 with reranking',
      PRECISE: 'Multi-hop, top-10 with cross-encoder',
      EXPERT: 'Hybrid + web search fallback',
      CONSENSUS: 'Hybrid, top-7 per voter',
      CUSTOM: 'Configurable depth',
    },
    optimizationTips: [
      'Pre-filter per game riduce search space 90%',
      'Cross-encoder reranking: +8% accuracy, 0 LLM tokens',
      'Sentence window retrieval per contesto granulare',
    ],
  },
  crag: {
    title: 'Layer 4: CRAG Evaluation',
    fullDescription:
      'CRAG (Corrective RAG) valuta la qualità dei documenti recuperati prima della generazione. Un modello T5-Large classificatore decide se i documenti sono: Correct, Ambiguous, o Incorrect, attivando azioni correttive.',
    technicalDetails: [
      'T5-Large fine-tuned per relevance evaluation',
      '3 classi: Correct (>0.7), Ambiguous (0.4-0.7), Incorrect (<0.4)',
      'Decompose-recompose: estrae key sentences dai chunks',
      'Web augmentation per risultati Ambiguous/Incorrect',
    ],
    flowSteps: [
      { id: 'eval', name: 'Relevance Evaluation', description: 'T5 classifica documenti', icon: <CheckCircle className="h-4 w-4" />, tokens: 0 },
      { id: 'correct', name: 'If Correct', description: 'Procedi a generation', icon: <ArrowRight className="h-4 w-4" /> },
      { id: 'ambiguous', name: 'If Ambiguous', description: 'Decompose + web search', icon: <Globe className="h-4 w-4" />, tokens: 300 },
      { id: 'incorrect', name: 'If Incorrect', description: 'Web-only fallback', icon: <Globe className="h-4 w-4" />, tokens: 500 },
      { id: 'recompose', name: 'Recompose', description: 'Filtra a key sentences', icon: <Zap className="h-4 w-4" />, tokens: 200 },
    ],
    models: {
      FAST: 'Skipped (0 tokens)',
      BALANCED: 'T5-Large (no LLM tokens)',
      PRECISE: 'T5-Large + LLM verification',
      EXPERT: 'T5 + web augmentation',
      CONSENSUS: 'T5 per ogni voter',
      CUSTOM: 'Configurable threshold',
    },
    optimizationTips: [
      'T5-Large: 0 LLM tokens, ma +12% accuracy',
      'Decompose-recompose riduce context 60%',
      'Cache CRAG decisions per queries simili',
    ],
  },
  generation: {
    title: 'Layer 5: Adaptive Generation',
    fullDescription:
      'Il generation layer produce la risposta finale usando template-specific prompts ottimizzati per ogni tipo di query. Supporta single-agent (FAST), CRAG-enhanced (BALANCED), e multi-agent pipelines (PRECISE).',
    technicalDetails: [
      '5 Template prompts ottimizzati per use case',
      'Token budget per tier: User 3K, Editor 5K, Admin 15K, Premium 20K+ (Anonymous: NO ACCESS)',
      'Citation enforcement: ogni claim deve avere source',
      'Multi-agent: Analyzer → Strategist → Validator',
    ],
    flowSteps: [
      { id: 'template', name: 'Template Selection', description: 'Sceglie prompt template', icon: <MessageSquare className="h-4 w-4" />, tokens: 0 },
      { id: 'context', name: 'Context Assembly', description: 'Assembla system + context + query', icon: <Database className="h-4 w-4" />, tokens: 500 },
      { id: 'generate', name: 'LLM Generation', description: 'Genera risposta con citations', icon: <Brain className="h-4 w-4" />, tokens: 2000 },
      { id: 'format', name: 'Output Formatting', description: 'Struttura risposta finale', icon: <Settings className="h-4 w-4" />, tokens: 100 },
      { id: 'cite', name: 'Citation Injection', description: 'Aggiunge link ai sources', icon: <CheckCircle className="h-4 w-4" />, tokens: 0 },
    ],
    models: {
      FAST: 'Llama 3.3 70B / Gemini Flash (free)',
      BALANCED: 'Claude 3.5 Sonnet',
      PRECISE: 'Claude 3.5 Opus (multi-agent)',
      EXPERT: 'Claude 3.5 Sonnet + web synthesis',
      CONSENSUS: '3x voters + aggregator',
      CUSTOM: 'Admin-defined model chain',
    },
    optimizationTips: [
      'Free models per FAST: $0 cost, 78% accuracy',
      'Prompt caching: -90% su system prompt tokens',
      'Streaming: UX migliore senza token overhead',
    ],
  },
  validation: {
    title: 'Layer 6: Self-Validation',
    fullDescription:
      'Il validation layer verifica la qualità della risposta generata. Include citation check (ogni claim ha source?), hallucination detection, e Self-RAG reflection per queries PRECISE.',
    technicalDetails: [
      'Rule-based citation check: 0 LLM tokens',
      'Cross-encoder alignment: generated vs retrieved',
      'Self-RAG reflection: confidence scoring 0-1',
      'Auto-escalation: se confidence < 0.7, retry con PRECISE',
    ],
    flowSteps: [
      { id: 'cite-check', name: 'Citation Check', description: 'Verifica ogni claim ha source', icon: <CheckCircle className="h-4 w-4" />, tokens: 0 },
      { id: 'align', name: 'Alignment Check', description: 'Cross-encoder: risposta vs docs', icon: <Shield className="h-4 w-4" />, tokens: 0 },
      { id: 'reflect', name: 'Self-Reflection', description: 'LLM valuta propria risposta', icon: <Brain className="h-4 w-4" />, tokens: 800 },
      { id: 'score', name: 'Confidence Score', description: 'Calcola score 0-1', icon: <Zap className="h-4 w-4" />, tokens: 100 },
      { id: 'escalate', name: 'Auto-Escalation', description: 'Retry se score < 0.7', icon: <ArrowRight className="h-4 w-4" /> },
    ],
    models: {
      FAST: 'Rule-based only (0 tokens)',
      BALANCED: 'Cross-encoder (0 LLM tokens)',
      PRECISE: 'Self-RAG with Sonnet',
      EXPERT: 'Cross-encoder + confidence',
      CONSENSUS: 'Vote aggregation check',
      CUSTOM: 'Configurable validation',
    },
    optimizationTips: [
      'Citation check: 100% coverage con 0 tokens',
      'Self-RAG cattura 15% errori automaticamente',
      'Auto-escalation previene risposte low-quality',
    ],
  },
};

// =============================================================================
// Layer Detail Modal
// =============================================================================

interface LayerModalProps {
  layer: RagLayer;
  strategy: RagStrategy;
  isOpen: boolean;
  onClose: () => void;
}

function LayerModal({ layer, strategy, isOpen, onClose }: LayerModalProps) {
  const data = LAYER_FLOW_DATA[layer.id];
  const tokens = STRATEGY_TOKENS[strategy][layer.id];
  const totalTokens = tokens.input + tokens.output;

  if (!isOpen) return null;

  return (
    <AnimatePresence>
      {isOpen && (
        <>
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50"
            onClick={onClose}
          />

          {/* Modal */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ type: 'spring', damping: 25, stiffness: 300 }}
            className="fixed inset-4 md:inset-auto md:left-1/2 md:top-1/2 md:-translate-x-1/2 md:-translate-y-1/2 md:w-[800px] md:max-h-[85vh] bg-card border border-border rounded-2xl shadow-2xl z-50 overflow-hidden flex flex-col"
          >
            {/* Header */}
            <div
              className="p-6 border-b border-border"
              style={{ background: `linear-gradient(135deg, ${layer.color}15, transparent)` }}
            >
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-4">
                  <div
                    className="w-14 h-14 rounded-xl flex items-center justify-center text-2xl"
                    style={{ background: `${layer.color}20`, border: `2px solid ${layer.color}` }}
                  >
                    {layer.icon}
                  </div>
                  <div>
                    <h2 className="text-xl font-bold">{data.title}</h2>
                    <p className="text-sm text-muted-foreground">{layer.description}</p>
                  </div>
                </div>
                <button
                  onClick={onClose}
                  className="p-2 hover:bg-muted rounded-lg transition-colors"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              {/* Strategy Badge & Tokens */}
              <div className="flex items-center gap-4 mt-4">
                <span
                  className="px-3 py-1 rounded-full text-sm font-medium"
                  style={{ background: `${layer.color}20`, color: layer.color }}
                >
                  {strategy}
                </span>
                <span className="text-sm text-muted-foreground">
                  Model: <span className="font-medium text-foreground">{data.models[strategy]}</span>
                </span>
                <span className="text-sm font-mono" style={{ color: layer.color }}>
                  {totalTokens.toLocaleString()} tokens
                </span>
              </div>
            </div>

            {/* Content */}
            <div className="flex-1 overflow-y-auto p-6 space-y-6">
              {/* Description */}
              <div>
                <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  Descrizione
                </h3>
                <p className="text-sm leading-relaxed">{data.fullDescription}</p>
              </div>

              {/* Flow Visualization */}
              <div>
                <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider mb-4">
                  Flusso Interno
                </h3>
                <div className="relative">
                  <div className="flex flex-wrap gap-3">
                    {data.flowSteps.map((step, index) => (
                      <motion.div
                        key={step.id}
                        initial={{ opacity: 0, y: 10 }}
                        animate={{ opacity: 1, y: 0 }}
                        transition={{ delay: index * 0.1 }}
                        className="flex items-center gap-2"
                      >
                        <div
                          className="flex items-center gap-3 px-4 py-3 rounded-xl border bg-card hover:bg-muted/50 transition-colors"
                          style={{ borderColor: `${layer.color}40` }}
                        >
                          <div
                            className="w-8 h-8 rounded-lg flex items-center justify-center"
                            style={{ background: `${layer.color}20`, color: layer.color }}
                          >
                            {step.icon}
                          </div>
                          <div>
                            <div className="text-sm font-medium">{step.name}</div>
                            <div className="text-xs text-muted-foreground">{step.description}</div>
                            {step.tokens !== undefined && step.tokens > 0 && (
                              <div className="text-xs font-mono mt-1" style={{ color: layer.color }}>
                                ~{step.tokens} tokens
                              </div>
                            )}
                          </div>
                        </div>
                        {index < data.flowSteps.length - 1 && (
                          <ArrowRight className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                        )}
                      </motion.div>
                    ))}
                  </div>
                </div>
              </div>

              {/* Technical Details */}
              <div>
                <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  Dettagli Tecnici
                </h3>
                <ul className="space-y-2">
                  {data.technicalDetails.map((detail, idx) => (
                    <li key={idx} className="flex items-start gap-2 text-sm">
                      <span style={{ color: layer.color }}>•</span>
                      <span>{detail}</span>
                    </li>
                  ))}
                </ul>
              </div>

              {/* Token Breakdown */}
              <div>
                <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  Token Breakdown ({strategy})
                </h3>
                <div className="grid grid-cols-3 gap-4">
                  <div className="p-4 rounded-xl bg-muted/50">
                    <div className="text-xs text-muted-foreground">Input Tokens</div>
                    <div className="text-2xl font-bold font-mono">{tokens.input.toLocaleString()}</div>
                  </div>
                  <div className="p-4 rounded-xl bg-muted/50">
                    <div className="text-xs text-muted-foreground">Output Tokens</div>
                    <div className="text-2xl font-bold font-mono">{tokens.output.toLocaleString()}</div>
                  </div>
                  <div className="p-4 rounded-xl" style={{ background: `${layer.color}15` }}>
                    <div className="text-xs text-muted-foreground">Total</div>
                    <div className="text-2xl font-bold font-mono" style={{ color: layer.color }}>
                      {totalTokens.toLocaleString()}
                    </div>
                  </div>
                </div>
              </div>

              {/* Optimization Tips */}
              <div>
                <h3 className="text-sm font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                  💡 Optimization Tips
                </h3>
                <div className="space-y-2">
                  {data.optimizationTips.map((tip, idx) => (
                    <div
                      key={idx}
                      className="flex items-start gap-2 p-3 rounded-lg bg-green-500/10 border border-green-500/20 text-sm"
                    >
                      <Zap className="h-4 w-4 text-green-500 flex-shrink-0 mt-0.5" />
                      <span>{tip}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}

// =============================================================================
// Flow Layer Component
// =============================================================================

interface FlowLayerProps {
  layer: RagLayer;
  strategy: RagStrategy;
  maxTokens: number;
  index: number;
  isExpanded: boolean;
  onToggle: () => void;
  onOpenModal: () => void;
}

function FlowLayer({
  layer,
  strategy,
  maxTokens,
  index,
  isExpanded,
  onToggle,
  onOpenModal,
}: FlowLayerProps) {
  const tokens = STRATEGY_TOKENS[strategy][layer.id];
  const totalTokens = tokens.input + tokens.output;
  const percentage = (totalTokens / maxTokens) * 100;

  return (
    <motion.div
      initial={{ opacity: 0, x: -20 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.1, duration: 0.3 }}
      className="relative"
    >
      {/* Connector line */}
      {index > 0 && (
        <div
          className="rag-flow-connector"
          style={{
            top: -8,
            background: `linear-gradient(to bottom, ${RAG_LAYERS[index - 1].color}, ${layer.color})`,
          }}
        />
      )}

      <div
        className="rag-flow-layer group"
        style={{ '--layer-color': layer.color } as React.CSSProperties}
        data-active={isExpanded}
      >
        {/* Icon */}
        <div className="rag-layer-icon">{layer.icon}</div>

        {/* Layer Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-xs font-mono text-muted-foreground">{layer.shortName}</span>
            <span className="font-semibold">{layer.name}</span>
          </div>
          <div className="text-xs text-muted-foreground truncate">{layer.description}</div>
        </div>

        {/* Token Bar */}
        <div className="w-32 md:w-48 flex items-center gap-2">
          <div className="rag-token-bar flex-1">
            <motion.div
              className="rag-token-bar-fill"
              initial={{ width: 0 }}
              animate={{ width: `${percentage}%` }}
              transition={{ delay: index * 0.1 + 0.3, duration: 0.5 }}
              style={{ background: layer.color }}
            />
          </div>
          <span className="font-mono text-xs w-12 text-right" style={{ color: layer.color }}>
            {totalTokens > 0 ? `${(totalTokens / 1000).toFixed(1)}K` : '0'}
          </span>
        </div>

        {/* Actions */}
        <div className="flex items-center gap-1">
          <button
            onClick={onOpenModal}
            className="p-2 hover:bg-muted rounded-lg transition-colors opacity-0 group-hover:opacity-100"
            title="Vedi dettagli"
          >
            <Sparkles className="h-4 w-4 text-primary" />
          </button>
          <button
            onClick={onToggle}
            className="p-2 hover:bg-muted rounded-lg transition-colors"
            aria-expanded={isExpanded}
          >
            {isExpanded ? (
              <ChevronDown className="h-4 w-4 text-muted-foreground" />
            ) : (
              <ChevronRight className="h-4 w-4 text-muted-foreground" />
            )}
          </button>
        </div>
      </div>

      {/* Quick Expand Panel */}
      <AnimatePresence>
        {isExpanded && (
          <motion.div
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={{ duration: 0.2 }}
            className="overflow-hidden"
          >
            <div className="pt-4 pl-16 pr-4 pb-2 space-y-3">
              <p className="text-sm text-muted-foreground">
                {LAYER_FLOW_DATA[layer.id].fullDescription.slice(0, 150)}...
              </p>
              <button
                onClick={onOpenModal}
                className="text-sm text-primary hover:underline flex items-center gap-1"
              >
                <Sparkles className="h-3 w-3" />
                Vedi flusso completo e dettagli
              </button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  );
}

// =============================================================================
// Main Component
// =============================================================================

interface TokenFlowVisualizerProps {
  className?: string;
}

export function TokenFlowVisualizer({ className }: TokenFlowVisualizerProps) {
  const [strategy, setStrategy] = useState<RagStrategy>('BALANCED');
  const [expandedLayer, setExpandedLayer] = useState<string | null>(null);
  const [modalLayer, setModalLayer] = useState<RagLayer | null>(null);

  const maxTokens = useMemo(() => {
    return Math.max(
      ...Object.values(STRATEGY_TOKENS[strategy]).map(t => t.input + t.output)
    );
  }, [strategy]);

  const totalTokens = useMemo(() => {
    return Object.values(STRATEGY_TOKENS[strategy]).reduce(
      (sum, t) => sum + t.input + t.output,
      0
    );
  }, [strategy]);

  const strategies: RagStrategy[] = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'];

  return (
    <>
      <Card className={cn('rag-card', className)}>
        <CardHeader>
          <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
            <CardTitle className="flex items-center gap-2">
              <Sparkles className="h-5 w-5 text-primary" />
              Token Flow Visualizer
            </CardTitle>

            {/* Strategy Toggle */}
            <div className="flex flex-wrap gap-2">
              {strategies.map(s => (
                <button
                  key={s}
                  onClick={() => setStrategy(s)}
                  className="rag-strategy-btn"
                  data-strategy={s}
                  data-active={strategy === s}
                >
                  {s}
                </button>
              ))}
            </div>
          </div>

          {/* Total Summary */}
          <div className="flex flex-wrap items-center gap-4 pt-2 text-sm">
            <div>
              <span className="text-muted-foreground">Total Tokens:</span>{' '}
              <span className="font-mono font-semibold text-primary">
                {totalTokens.toLocaleString()}
              </span>
            </div>
            <div className="text-xs text-muted-foreground">
              {STRATEGY_CONFIGS[strategy]?.latencyMs} | {STRATEGY_CONFIGS[strategy]?.accuracyRange}
            </div>
          </div>
        </CardHeader>

        <CardContent>
          <div className="rag-flow-container">
            {RAG_LAYERS.map((layer, index) => (
              <FlowLayer
                key={layer.id}
                layer={layer}
                strategy={strategy}
                maxTokens={maxTokens}
                index={index}
                isExpanded={expandedLayer === layer.id}
                onToggle={() =>
                  setExpandedLayer(expandedLayer === layer.id ? null : layer.id)
                }
                onOpenModal={() => setModalLayer(layer)}
              />
            ))}
          </div>

          {/* Strategy Info */}
          <div className="mt-6 p-4 rounded-xl bg-muted/50 border border-border">
            <div className="flex items-start gap-3">
              <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center">
                {strategy === 'FAST' && <Zap className="h-5 w-5 text-primary" />}
                {strategy === 'BALANCED' && <CheckCircle className="h-5 w-5 text-primary" />}
                {strategy === 'PRECISE' && <Shield className="h-5 w-5 text-primary" />}
                {strategy === 'EXPERT' && <Globe className="h-5 w-5 text-primary" />}
                {strategy === 'CONSENSUS' && <Users className="h-5 w-5 text-primary" />}
                {strategy === 'CUSTOM' && <Settings className="h-5 w-5 text-primary" />}
              </div>
              <div>
                <div className="font-semibold">{strategy} Strategy</div>
                <div className="text-sm text-muted-foreground">
                  {STRATEGY_CONFIGS[strategy]?.description}
                </div>
                <div className="text-xs text-muted-foreground mt-1">
                  Use case: {STRATEGY_CONFIGS[strategy]?.useCase}
                </div>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Layer Detail Modal */}
      {modalLayer && (
        <LayerModal
          layer={modalLayer}
          strategy={strategy}
          isOpen={!!modalLayer}
          onClose={() => setModalLayer(null)}
        />
      )}
    </>
  );
}
