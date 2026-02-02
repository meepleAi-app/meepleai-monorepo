'use client';

/**
 * BlockExplanationModal Component
 *
 * Detailed explanation modal for RAG flow blocks.
 * Opens when user clicks on a block in strategy flow visualization.
 *
 * Features:
 * - 4 tabs: Description, Technical, Examples, Research
 * - Code references with file:line links
 * - Parameter documentation
 * - Impact metrics
 */

import React, { useState } from 'react';
import { X, Code2, BookOpen, Beaker, GraduationCap, ExternalLink, FileCode } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Badge } from '@/components/ui/data-display/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

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

interface BlockExplanationModalProps {
  block: FlowBlock | null;
  isOpen: boolean;
  onClose: () => void;
}

// =============================================================================
// Block Type Metadata
// =============================================================================

const BLOCK_TYPE_INFO = {
  input: {
    name: 'Input Query',
    description: 'Punto di ingresso per le query utente nel sistema RAG',
    color: 'hsl(221, 83%, 53%)',
    category: 'Flow Control'
  },
  agent: {
    name: 'Agent Selector',
    description: 'Seleziona l\'agente specializzato basato sul tipo di query',
    color: 'hsl(262, 83%, 62%)',
    category: 'Orchestration'
  },
  model: {
    name: 'LLM Model',
    description: 'Modello di linguaggio utilizzato per la generazione',
    color: 'hsl(25, 85%, 45%)',
    category: 'Generation'
  },
  layer: {
    name: 'RAG Layer',
    description: 'Layer di processing del pipeline TOMAC-RAG',
    color: 'hsl(142, 76%, 36%)',
    category: 'Processing'
  },
  output: {
    name: 'Output Response',
    description: 'Risposta finale restituita all\'utente',
    color: 'hsl(25, 95%, 53%)',
    category: 'Flow Control'
  }
};

// =============================================================================
// Component
// =============================================================================

export function BlockExplanationModal({ block, isOpen, onClose }: BlockExplanationModalProps) {
  const [activeTab, setActiveTab] = useState('description');

  if (!block) return null;

  const blockTypeInfo = BLOCK_TYPE_INFO[block.type];

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center gap-4 mb-4">
            {/* Icon */}
            <div
              className="w-16 h-16 rounded-xl flex items-center justify-center text-3xl"
              style={{
                background: `linear-gradient(135deg, ${blockTypeInfo.color}15, ${blockTypeInfo.color}05)`
              }}
            >
              {block.icon}
            </div>

            {/* Title */}
            <div className="flex-1">
              <DialogTitle className="text-2xl font-quicksand font-bold mb-1">
                {block.title}
              </DialogTitle>
              <div className="flex items-center gap-2">
                <Badge variant="outline" style={{ borderColor: blockTypeInfo.color }}>
                  {blockTypeInfo.name}
                </Badge>
                <Badge variant="secondary">{blockTypeInfo.category}</Badge>
              </div>
            </div>

            {/* Close button */}
            <Button
              variant="ghost"
              size="icon"
              onClick={onClose}
              className="absolute top-4 right-4"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
        </DialogHeader>

        {/* Tabs */}
        <Tabs value={activeTab} onValueChange={setActiveTab} className="mt-6">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="description">
              <BookOpen className="h-4 w-4 mr-2" />
              Description
            </TabsTrigger>
            <TabsTrigger value="technical">
              <Code2 className="h-4 w-4 mr-2" />
              Technical
            </TabsTrigger>
            <TabsTrigger value="examples">
              <Beaker className="h-4 w-4 mr-2" />
              Examples
            </TabsTrigger>
            <TabsTrigger value="research">
              <GraduationCap className="h-4 w-4 mr-2" />
              Research
            </TabsTrigger>
          </TabsList>

          {/* Tab 1: Description */}
          <TabsContent value="description" className="space-y-6 mt-6">
            <section>
              <h3 className="text-lg font-semibold mb-3 flex items-center gap-2">
                <span className="text-2xl">{block.icon}</span>
                Cosa fa questo blocco
              </h3>
              <p className="text-muted-foreground leading-relaxed">
                {block.explanation}
              </p>
            </section>

            <section>
              <h4 className="font-semibold mb-3">Parametri Configurati</h4>
              <div className="grid grid-cols-2 gap-3">
                {Object.entries(block.params).map(([key, value]) => (
                  <div
                    key={key}
                    className="p-3 rounded-lg bg-muted/50 border border-border"
                  >
                    <div className="text-xs text-muted-foreground uppercase tracking-wide mb-1">
                      {key}
                    </div>
                    <div className="font-semibold text-foreground">{value}</div>
                  </div>
                ))}
              </div>
            </section>

            {block.type === 'layer' && (
              <section>
                <h4 className="font-semibold mb-3">📊 Impact Metrics</h4>
                <div className="bg-gradient-to-br from-primary/5 to-purple-500/5 rounded-lg p-4 border border-primary/20">
                  <p className="text-sm text-muted-foreground">
                    Questo layer contribuisce ai tempi di risposta e ai costi complessivi della strategia.
                    Rimuoverlo può ridurre latency ma impattare l'accuracy.
                  </p>
                </div>
              </section>
            )}
          </TabsContent>

          {/* Tab 2: Technical */}
          <TabsContent value="technical" className="space-y-6 mt-6">
            <section>
              <h3 className="text-lg font-semibold mb-3">Come Funziona</h3>
              <div className="bg-muted/30 rounded-lg p-4 border border-border">
                <p className="text-sm leading-relaxed whitespace-pre-line font-mono">
                  {block.technicalDetails}
                </p>
              </div>
            </section>

            {block.codeReference && (
              <section>
                <h4 className="font-semibold mb-3 flex items-center gap-2">
                  <FileCode className="h-5 w-5" />
                  Code Reference
                </h4>
                <div className="flex items-center gap-2 p-3 rounded-lg bg-muted/50 border border-border font-mono text-sm">
                  <Code2 className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                  <span className="text-foreground">{block.codeReference}</span>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="ml-auto"
                    onClick={() => {
                      // Open in VS Code (future enhancement)
                      console.log('Open:', block.codeReference);
                    }}
                  >
                    <ExternalLink className="h-3 w-3" />
                  </Button>
                </div>
              </section>
            )}

            {block.type === 'model' && (
              <section>
                <h4 className="font-semibold mb-3">Model Specifications</h4>
                <div className="space-y-2 text-sm">
                  <div className="flex justify-between p-2 rounded bg-muted/30">
                    <span className="text-muted-foreground">Context Window:</span>
                    <span className="font-semibold">
                      {block.title.includes('Llama') ? '4K tokens' :
                       block.title.includes('DeepSeek') ? '16K tokens' :
                       block.title.includes('Sonnet') ? '200K tokens' : 'Variable'}
                    </span>
                  </div>
                  <div className="flex justify-between p-2 rounded bg-muted/30">
                    <span className="text-muted-foreground">Speed:</span>
                    <span className="font-semibold">
                      {block.title.includes('Llama') ? '~50 tokens/sec' :
                       block.title.includes('DeepSeek') ? '~80 tokens/sec' :
                       block.title.includes('Sonnet') ? '~100 tokens/sec' : 'Variable'}
                    </span>
                  </div>
                  <div className="flex justify-between p-2 rounded bg-muted/30">
                    <span className="text-muted-foreground">Fallback:</span>
                    <span className="font-semibold">
                      {block.title.includes('Llama') ? 'Gemini 2.0 Flash' :
                       block.title.includes('DeepSeek') ? 'Claude Haiku 4.5' :
                       block.title.includes('Sonnet') ? 'Haiku 4.5, GPT-4o-mini' : 'None'}
                    </span>
                  </div>
                </div>
              </section>
            )}
          </TabsContent>

          {/* Tab 3: Examples */}
          <TabsContent value="examples" className="space-y-6 mt-6">
            <section>
              <h3 className="text-lg font-semibold mb-3">Esempio di Utilizzo</h3>

              {block.type === 'layer' && block.title.includes('Retrieval') && (
                <div className="space-y-4">
                  <div>
                    <h4 className="text-sm font-semibold mb-2 text-muted-foreground">Prima del Retrieval:</h4>
                    <div className="bg-muted/30 p-3 rounded-lg border border-border">
                      <p className="text-sm font-mono">Query: "Posso muovere in diagonale?"</p>
                      <p className="text-sm font-mono text-muted-foreground mt-1">
                        Embedding generato: [0.23, -0.15, 0.67, ...]
                      </p>
                    </div>
                  </div>

                  <div>
                    <h4 className="text-sm font-semibold mb-2 text-muted-foreground">Dopo il Retrieval:</h4>
                    <div className="bg-muted/30 p-3 rounded-lg border border-border">
                      <div className="space-y-2 text-sm font-mono">
                        <div className="flex items-start gap-2">
                          <Badge variant="outline" className="text-xs">0.89</Badge>
                          <span>Chunk 1: "Movimento pezzi - sezione 3.2, pag. 12"</span>
                        </div>
                        <div className="flex items-start gap-2">
                          <Badge variant="outline" className="text-xs">0.82</Badge>
                          <span>Chunk 2: "Regole movimento diagonale - pag. 13"</span>
                        </div>
                        <div className="flex items-start gap-2">
                          <Badge variant="outline" className="text-xs">0.75</Badge>
                          <span>Chunk 3: "Eccezioni movimento - pag. 45"</span>
                        </div>
                      </div>
                    </div>
                  </div>

                  <div className="bg-primary/5 border border-primary/20 rounded-lg p-3">
                    <p className="text-sm text-foreground">
                      <strong>Impact:</strong> Recuperati 3 chunk rilevanti in ~120ms con ~900 tokens.
                      Questi chunk forniscono il contesto per la generation.
                    </p>
                  </div>
                </div>
              )}

              {block.type === 'model' && (
                <div className="space-y-4">
                  <div>
                    <h4 className="text-sm font-semibold mb-2 text-muted-foreground">Input al Modello:</h4>
                    <div className="bg-muted/30 p-3 rounded-lg border border-border font-mono text-xs">
                      <div className="text-muted-foreground mb-2">System Prompt:</div>
                      <p className="mb-3">"You are a rules expert for board games..."</p>

                      <div className="text-muted-foreground mb-2">User Prompt:</div>
                      <p className="mb-2">"Query: Posso muovere in diagonale?"</p>
                      <p className="text-muted-foreground">"Context: [3 chunks from retrieval]"</p>
                    </div>
                  </div>

                  <div>
                    <h4 className="text-sm font-semibold mb-2 text-muted-foreground">Output del Modello:</h4>
                    <div className="bg-muted/30 p-3 rounded-lg border border-border text-sm">
                      <p>"Sì, secondo la sezione 3.2 pagina 12, il movimento in diagonale è consentito..."</p>
                      <div className="mt-2 text-xs text-muted-foreground">
                        Tokens generati: ~200 | Latency: ~800ms
                      </div>
                    </div>
                  </div>
                </div>
              )}

              {(block.type === 'input' || block.type === 'output' || block.type === 'agent') && (
                <div className="bg-muted/30 p-4 rounded-lg border border-border">
                  <p className="text-sm text-muted-foreground text-center">
                    Esempi dettagliati disponibili nel tab "View Examples" della strategia.
                  </p>
                </div>
              )}
            </section>
          </TabsContent>

          {/* Tab 4: Research */}
          <TabsContent value="research" className="space-y-6 mt-6">
            <section>
              <h3 className="text-lg font-semibold mb-3">Academic Sources</h3>

              {block.title.includes('CRAG') && (
                <div className="space-y-3">
                  <a
                    href="https://arxiv.org/abs/2401.15884"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="block p-3 rounded-lg border border-border hover:border-primary/50 transition-colors"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <h4 className="font-semibold text-sm mb-1">
                          Corrective Retrieval Augmented Generation
                        </h4>
                        <p className="text-xs text-muted-foreground">
                          Yan et al. (2024) - ICLR 2025
                        </p>
                        <p className="text-xs mt-2">
                          Introduces retrieval evaluator for assessing document quality,
                          triggering correction actions (web search, query rewriting).
                          Achieves +5-15% accuracy improvement.
                        </p>
                      </div>
                      <ExternalLink className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                    </div>
                  </a>

                  <div className="p-3 rounded-lg bg-muted/50">
                    <h5 className="text-sm font-semibold mb-2">Key Findings:</h5>
                    <ul className="text-xs space-y-1 text-muted-foreground list-disc list-inside">
                      <li>Lightweight evaluator (T5-Large, 0.77B params) vs full LLM</li>
                      <li>Three actions: Correct, Ambiguous, Incorrect</li>
                      <li>Web search augmentation for static corpus limitations</li>
                      <li>Tested on PopQA, Biography, PubHealth datasets</li>
                    </ul>
                  </div>
                </div>
              )}

              {block.title.includes('Multi-Agent') && (
                <div className="space-y-3">
                  <a
                    href="https://docs.langchain.com/langgraph"
                    target="_blank"
                    rel="noopener noreferrer"
                    className="block p-3 rounded-lg border border-border hover:border-primary/50 transition-colors"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <h4 className="font-semibold text-sm mb-1">
                          LangGraph: Multi-Agent Workflows
                        </h4>
                        <p className="text-xs text-muted-foreground">
                          LangChain Documentation (2024-2025)
                        </p>
                        <p className="text-xs mt-2">
                          Framework for building stateful multi-agent applications with graph-based orchestration.
                          Supports supervisor-worker, sequential, and parallel patterns.
                        </p>
                      </div>
                      <ExternalLink className="h-4 w-4 text-muted-foreground flex-shrink-0" />
                    </div>
                  </a>

                  <div className="p-3 rounded-lg bg-muted/50">
                    <h5 className="text-sm font-semibold mb-2">Production Usage:</h5>
                    <ul className="text-xs space-y-1 text-muted-foreground list-disc list-inside">
                      <li>+25-40% task completion rate vs single agent</li>
                      <li>120K+ GitHub stars, 9.8M PyPI downloads/month (2025)</li>
                      <li>Used by: Databricks, Anthropic, OpenAI, Microsoft</li>
                    </ul>
                  </div>
                </div>
              )}

              {block.title.includes('Hybrid') && (
                <div className="space-y-3">
                  <div className="p-3 rounded-lg border border-border">
                    <h4 className="font-semibold text-sm mb-1">
                      Hybrid Search: Vector + Keyword Fusion
                    </h4>
                    <p className="text-xs text-muted-foreground mb-2">
                      Morphik AI, Databricks, LinkedIn (2024-2025)
                    </p>
                    <p className="text-xs">
                      Combines dense retrieval (semantic) with sparse retrieval (keyword matching) using
                      Reciprocal Rank Fusion (RRF) or weighted fusion. Achieves +35-48% improvement in
                      retrieval precision vs pure vector search.
                    </p>
                  </div>

                  <div className="p-3 rounded-lg bg-muted/50">
                    <h5 className="text-sm font-semibold mb-2">Why Hybrid {'>'} Vector Only:</h5>
                    <ul className="text-xs space-y-1 text-muted-foreground list-disc list-inside">
                      <li>Vector search: excellent for concepts, struggles with exact terms</li>
                      <li>Keyword search: perfect for product codes, names, specific phrases</li>
                      <li>Hybrid: best of both worlds, production standard in 2024+</li>
                    </ul>
                  </div>
                </div>
              )}

              {/* Generic fallback */}
              {!block.title.includes('CRAG') && !block.title.includes('Multi-Agent') && !block.title.includes('Hybrid') && (
                <div className="p-4 rounded-lg bg-muted/30 border border-border text-center">
                  <p className="text-sm text-muted-foreground">
                    Academic research per questo blocco disponibile nella documentazione completa.
                  </p>
                  <Button variant="link" size="sm" className="mt-2">
                    <ExternalLink className="h-3 w-3 mr-2" />
                    View Full Research Report
                  </Button>
                </div>
              )}
            </section>
          </TabsContent>
        </Tabs>

        {/* Footer Actions */}
        <div className="flex items-center justify-between mt-6 pt-4 border-t border-border">
          <Button variant="outline" size="sm" onClick={onClose}>
            Close (ESC)
          </Button>

          {block.codeReference && (
            <Button variant="link" size="sm" className="text-xs">
              <FileCode className="h-3 w-3 mr-2" />
              Open in VS Code
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
