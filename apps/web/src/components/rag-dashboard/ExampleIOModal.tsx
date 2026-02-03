'use client';

/**
 * ExampleIOModal Component
 *
 * Shows static examples + live testing capability for RAG strategies.
 *
 * Features:
 * - 3-5 curated static examples per strategy
 * - "Test Live" button for Admin tier
 * - SSE streaming for live test progress
 * - Metrics comparison (actual vs estimated)
 */

import React, { useState } from 'react';

import { X, Play, Loader2, AlertCircle } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

import { STRATEGIES } from './rag-data';

import type { RagStrategy, UserTier } from './types';

// =============================================================================
// Types
// =============================================================================

interface StrategyExample {
  id: string;
  title: string;
  category: 'simple_faq' | 'rules_lookup' | 'strategy' | 'edge_case';
  input: {
    query: string;
    gameContext: string;
  };
  output: {
    answer: string;
    sources: Array<{ page: number; text: string }>;
    confidence: number;
  };
  metrics: {
    tokensUsed: number;
    latencyMs: number;
    cost: number;
    cacheHit: boolean;
  };
}

interface ExampleIOModalProps {
  strategy: RagStrategy;
  isOpen: boolean;
  onClose: () => void;
  userTier?: UserTier;
}

// =============================================================================
// Static Examples Data
// =============================================================================

const STRATEGY_EXAMPLES: Record<RagStrategy, StrategyExample[]> = {
  FAST: [
    {
      id: 'fast-1',
      title: 'Simple FAQ - Player Count',
      category: 'simple_faq',
      input: {
        query: 'Quanti giocatori supporta?',
        gameContext: 'Catan'
      },
      output: {
        answer: 'Il gioco Catan supporta 2-4 giocatori nella versione base (o 5-6 con l\'espansione).',
        sources: [{ page: 3, text: 'Numero giocatori: 2-4 (base), 5-6 (con espansione)' }],
        confidence: 0.92
      },
      metrics: {
        tokensUsed: 1850,
        latencyMs: 145,
        cost: 0,
        cacheHit: false
      }
    },
    {
      id: 'fast-2',
      title: 'Rules Lookup - Setup',
      category: 'rules_lookup',
      input: {
        query: 'Come si fa il setup iniziale?',
        gameContext: 'Catan'
      },
      output: {
        answer: 'Setup iniziale: 1) Assembla il tabellone esagonale, 2) Posiziona i numeri casuali, 3) Ogni giocatore sceglie 2 insediamenti e 2 strade iniziali.',
        sources: [
          { page: 4, text: 'Setup iniziale - sezione 2.1' },
          { page: 5, text: 'Posizionamento insediamenti iniziali' }
        ],
        confidence: 0.88
      },
      metrics: {
        tokensUsed: 2100,
        latencyMs: 180,
        cost: 0,
        cacheHit: false
      }
    },
    {
      id: 'fast-3',
      title: 'Cached Response',
      category: 'simple_faq',
      input: {
        query: 'Durata partita?',
        gameContext: 'Catan'
      },
      output: {
        answer: 'Una partita di Catan dura tipicamente 60-90 minuti.',
        sources: [{ page: 2, text: 'Durata: 60-90 minuti' }],
        confidence: 0.95
      },
      metrics: {
        tokensUsed: 50,
        latencyMs: 25,
        cost: 0,
        cacheHit: true
      }
    }
  ],

  BALANCED: [
    {
      id: 'balanced-1',
      title: 'Complex Rules Question',
      category: 'rules_lookup',
      input: {
        query: 'Posso costruire una strada se il mio avversario ha già costruito lì?',
        gameContext: 'Catan'
      },
      output: {
        answer: 'No, secondo le regole di costruzione (sezione 3.4, pagina 15), non puoi costruire una strada su uno spazio già occupato da un altro giocatore. Ogni spazio può contenere solo una strada.',
        sources: [
          { page: 15, text: 'Regole di costruzione - una strada per spazio' },
          { page: 16, text: 'Eccezioni costruzione - nessuna per strade' }
        ],
        confidence: 0.91
      },
      metrics: {
        tokensUsed: 2650,
        latencyMs: 1200,
        cost: 0.01,
        cacheHit: false
      }
    },
    {
      id: 'balanced-2',
      title: 'Strategy Question with Validation',
      category: 'strategy',
      input: {
        query: 'Qual è la strategia migliore per il primo turno?',
        gameContext: 'Catan'
      },
      output: {
        answer: 'La strategia più comune per il primo turno è posizionare il primo insediamento su un esagono con numero alto (6 o 8) e risorse diverse (grano + minerale è forte). Il secondo insediamento dovrebbe diversificare le risorse o garantire accesso al porto.',
        sources: [
          { page: 45, text: 'Guide strategiche - posizionamento iniziale' },
          { page: 46, text: 'Numeri produttivi - probabilità' }
        ],
        confidence: 0.87
      },
      metrics: {
        tokensUsed: 2900,
        latencyMs: 1450,
        cost: 0.01,
        cacheHit: false
      }
    }
  ],

  PRECISE: [
    {
      id: 'precise-1',
      title: 'Tournament Edge Case',
      category: 'edge_case',
      input: {
        query: 'Se due giocatori pareggiano al tie-breaker, cosa succede secondo le regole ufficiali del torneo?',
        gameContext: 'Catan Championship Rules'
      },
      output: {
        answer: 'Secondo le regole ufficiali del torneo (Tournament Rulebook, sezione 7.3, pagina 89), in caso di pareggio al tie-breaker si applica il criterio secondario: vince chi ha costruito più città. Se anche questo pareggia, si procede con il maggior numero di carte sviluppo giocate. Se ancora in pareggio, si gioca un round aggiuntivo decisivo.',
        sources: [
          { page: 89, text: 'Tie-breaker secondario - conteggio città' },
          { page: 90, text: 'Tie-breaker terziario - carte sviluppo' },
          { page: 91, text: 'Round decisivo - procedura' }
        ],
        confidence: 0.97
      },
      metrics: {
        tokensUsed: 21800,
        latencyMs: 8500,
        cost: 0.128,
        cacheHit: false
      }
    }
  ],

  EXPERT: [],
  CONSENSUS: [],
  CUSTOM: []
};

// =============================================================================
// Component
// =============================================================================

export function ExampleIOModal({ strategy, isOpen, onClose, userTier = 'User' }: ExampleIOModalProps) {
  const [isLiveTesting, setIsLiveTesting] = useState(false);
  const [activeTab, setActiveTab] = useState('examples');

  const strategyData = STRATEGIES[strategy];
  const examples = STRATEGY_EXAMPLES[strategy] || [];
  const isAdmin = userTier === 'Admin' || userTier === 'Premium';

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center gap-4">
            <div className="text-4xl">{strategyData.icon}</div>
            <div className="flex-1">
              <DialogTitle className="text-2xl font-quicksand font-bold">
                {strategyData.name} - Examples & Testing
              </DialogTitle>
              <p className="text-sm text-muted-foreground mt-1">
                {strategyData.description}
              </p>
            </div>

            <Button variant="ghost" size="icon" onClick={onClose}>
              <X className="h-4 w-4" />
            </Button>
          </div>
        </DialogHeader>

        <Tabs value={activeTab} onValueChange={setActiveTab} className="mt-6">
          <TabsList className={isAdmin ? 'grid w-full grid-cols-2' : 'w-full'}>
            <TabsTrigger value="examples">
              📝 Static Examples ({examples.length})
            </TabsTrigger>
            {isAdmin && (
              <TabsTrigger value="live">
                🧪 Live Test (Admin)
              </TabsTrigger>
            )}
          </TabsList>

          {/* Static Examples Tab */}
          <TabsContent value="examples" className="space-y-4 mt-6">
            {examples.length === 0 ? (
              <div className="text-center py-12 text-muted-foreground">
                <p>Esempi per {strategy} in arrivo...</p>
              </div>
            ) : (
              examples.map((example, index) => (
                <div
                  key={example.id}
                  className="p-4 rounded-lg border border-border bg-card hover:border-primary/50 transition-colors"
                >
                  {/* Example header */}
                  <div className="flex items-start justify-between mb-3">
                    <div>
                      <h4 className="font-semibold text-foreground mb-1">
                        {index + 1}. {example.title}
                      </h4>
                      <Badge variant="secondary" className="text-xs">
                        {example.category.replace('_', ' ')}
                      </Badge>
                    </div>
                    {example.metrics.cacheHit && (
                      <Badge variant="outline" className="text-xs">
                        💾 Cache Hit
                      </Badge>
                    )}
                  </div>

                  {/* Input */}
                  <div className="mb-3">
                    <div className="text-xs text-muted-foreground uppercase tracking-wide mb-1">
                      Input Query
                    </div>
                    <div className="p-3 rounded-md bg-muted/50 border border-border">
                      <p className="text-sm font-medium">"{example.input.query}"</p>
                      <p className="text-xs text-muted-foreground mt-1">
                        Game: {example.input.gameContext}
                      </p>
                    </div>
                  </div>

                  {/* Output */}
                  <div className="mb-3">
                    <div className="text-xs text-muted-foreground uppercase tracking-wide mb-1">
                      Output Response
                    </div>
                    <div className="p-3 rounded-md bg-primary/5 border border-primary/20">
                      <p className="text-sm leading-relaxed">{example.output.answer}</p>

                      {example.output.sources.length > 0 && (
                        <div className="mt-3 space-y-1">
                          <div className="text-xs font-semibold text-muted-foreground">
                            Sources ({example.output.sources.length}):
                          </div>
                          {example.output.sources.map((source, i) => (
                            <div key={i} className="text-xs text-muted-foreground">
                              • Page {source.page}: {source.text}
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Metrics */}
                  <div className="grid grid-cols-4 gap-2">
                    <div className="text-center p-2 rounded bg-muted/30">
                      <div className="text-xs text-muted-foreground">Tokens</div>
                      <div className="text-sm font-bold text-purple-600">
                        {example.metrics.tokensUsed.toLocaleString()}
                      </div>
                    </div>
                    <div className="text-center p-2 rounded bg-muted/30">
                      <div className="text-xs text-muted-foreground">Cost</div>
                      <div className="text-sm font-bold text-green-600">
                        ${example.metrics.cost.toFixed(4)}
                      </div>
                    </div>
                    <div className="text-center p-2 rounded bg-muted/30">
                      <div className="text-xs text-muted-foreground">Latency</div>
                      <div className="text-sm font-bold text-orange-600">
                        {example.metrics.latencyMs}ms
                      </div>
                    </div>
                    <div className="text-center p-2 rounded bg-muted/30">
                      <div className="text-xs text-muted-foreground">Confidence</div>
                      <div className="text-sm font-bold text-blue-600">
                        {(example.output.confidence * 100).toFixed(0)}%
                      </div>
                    </div>
                  </div>
                </div>
              ))
            )}
          </TabsContent>

          {/* Live Test Tab (Admin Only) */}
          {isAdmin && (
            <TabsContent value="live" className="space-y-4 mt-6">
              <div className="bg-orange-500/10 border border-orange-500/30 rounded-lg p-4 mb-4">
                <div className="flex items-start gap-3">
                  <AlertCircle className="h-5 w-5 text-orange-600 flex-shrink-0 mt-0.5" />
                  <div>
                    <h4 className="font-semibold text-sm mb-1">Admin Feature</h4>
                    <p className="text-xs text-muted-foreground">
                      Il test live chiama il backend RAG reale e mostra l'esecuzione in tempo reale.
                      Questa funzionalità sarà disponibile dopo l'implementazione dell'Epic #3434.
                    </p>
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <div>
                  <label className="text-sm font-medium mb-2 block">Test Query</label>
                  <Input
                    placeholder="Es: Quanti giocatori supporta?"
                    disabled={isLiveTesting}
                  />
                </div>

                <div>
                  <label className="text-sm font-medium mb-2 block">Game Context</label>
                  <select
                    className="w-full p-2 rounded-md border border-border bg-background"
                    disabled={isLiveTesting}
                  >
                    <option>Catan</option>
                    <option>Ticket to Ride</option>
                    <option>7 Wonders</option>
                  </select>
                </div>

                <Button
                  className="w-full"
                  disabled={isLiveTesting}
                  onClick={() => {
                    setIsLiveTesting(true);
                    // TODO: Implement SSE streaming
                    setTimeout(() => setIsLiveTesting(false), 3000);
                  }}
                >
                  {isLiveTesting ? (
                    <>
                      <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                      Testing in Progress...
                    </>
                  ) : (
                    <>
                      <Play className="h-4 w-4 mr-2" />
                      Run Live Test
                    </>
                  )}
                </Button>

                {/* Live Progress (Future Implementation) */}
                {isLiveTesting && (
                  <div className="space-y-2 p-4 rounded-lg bg-muted/30 border border-border">
                    <div className="flex items-center gap-2 text-sm">
                      <Loader2 className="h-4 w-4 animate-spin text-primary" />
                      <span>L1: Routing...</span>
                      <span className="ml-auto text-muted-foreground">45ms</span>
                    </div>
                    <div className="flex items-center gap-2 text-sm text-muted-foreground">
                      <div className="h-4 w-4" />
                      <span>L2: Cache check...</span>
                    </div>
                    <div className="flex items-center gap-2 text-sm text-muted-foreground">
                      <div className="h-4 w-4" />
                      <span>L3: Retrieval...</span>
                    </div>
                  </div>
                )}

                {/* Placeholder for future results */}
                <div className="text-center text-sm text-muted-foreground">
                  <p>⚙️ Live testing API will be available after Epic #3434 backend implementation</p>
                </div>
              </div>
            </TabsContent>
          )}
        </Tabs>
      </DialogContent>
    </Dialog>
  );
}
