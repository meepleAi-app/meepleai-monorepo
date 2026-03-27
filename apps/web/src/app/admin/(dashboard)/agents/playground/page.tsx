'use client';

/**
 * RAG Playground - Unified testing, debugging, and comparison page.
 *
 * Three tabs:
 *   1. Query Tester — interactive RAG query testing with parameter controls
 *   2. Chat Debug — streaming debug chat with pipeline events
 *   3. Compare — side-by-side execution comparison
 *
 * Supports deep linking via ?tab=chat | ?tab=compare.
 * Protected by admin layout (RequireRole(['Admin'])).
 */

import { useState, useCallback, useRef } from 'react';

import { PlayIcon, SendIcon } from 'lucide-react';
import { useSearchParams } from 'next/navigation';

import { DebugTimeline, StrategySelectorBar } from '@/components/admin/debug-chat';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { useDebugChatStream } from '@/hooks/useDebugChatStream';
import { useLocalStorage } from '@/hooks/useLocalStorage';
import { cn } from '@/lib/utils';

import { CompareTab } from './compare-tab';

// ─── Types ────────────────────────────────────────────────────────────────────

type RagStrategy = 'POC' | 'SingleModel' | 'MultiModelConsensus' | 'HybridRAG';

interface QueryResult {
  answer: string;
  chunks: { id: string; text: string; score: number }[];
  metrics: {
    latencyMs: number;
    tokens: number;
    cost: number;
    confidence: number;
  };
}

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

// ─── Constants ────────────────────────────────────────────────────────────────

const STRATEGIES: RagStrategy[] = ['POC', 'SingleModel', 'MultiModelConsensus', 'HybridRAG'];

const TAB_MAP: Record<string, string> = {
  chat: 'chat-debug',
  compare: 'compare',
};

// ─── Query Tester Tab ─────────────────────────────────────────────────────────

function QueryTesterTab() {
  const [query, setQuery] = useState('');
  const [strategy, setStrategy] = useState<RagStrategy>('HybridRAG');
  const [model, setModel] = useState('');
  const [temperature, setTemperature] = useState(0.7);
  const [topK, setTopK] = useState(5);
  const [gameScope, setGameScope] = useState('');
  const [agent, setAgent] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [result, setResult] = useState<QueryResult | null>(null);

  const handleExecute = useCallback(async () => {
    if (!query.trim()) return;
    setIsLoading(true);
    setResult(null);

    try {
      // TODO: Wire to real API — POST /api/v1/admin/rag-pipeline/test
      // In a real implementation, this would call:
      // const client = createAdminClient({ httpClient: new HttpClient() });
      // const res = await client.testRagPipeline({ query, strategy, model, temperature, topK, gameScope, agent });
      // For now, simulate a response after a short delay
      await new Promise(resolve => setTimeout(resolve, 500));
      setResult({
        answer: `[Simulazione] Risposta per: "${query}" con strategia ${strategy}`,
        chunks: [
          { id: '1', text: 'Chunk di esempio 1...', score: 0.95 },
          { id: '2', text: 'Chunk di esempio 2...', score: 0.88 },
          { id: '3', text: 'Chunk di esempio 3...', score: 0.72 },
        ],
        metrics: {
          latencyMs: Math.round(300 + Math.random() * 700),
          tokens: Math.round(100 + Math.random() * 400),
          cost: +(Math.random() * 0.01).toFixed(4),
          confidence: +(0.7 + Math.random() * 0.25).toFixed(2),
        },
      });
    } catch {
      setResult(null);
    } finally {
      setIsLoading(false);
    }
  }, [query, strategy]);

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
      {/* Left Panel: Query + Parameters */}
      <div className="space-y-4">
        <div className="space-y-2">
          <label className="text-sm font-medium text-foreground" htmlFor="query-input">
            Query
          </label>
          <textarea
            id="query-input"
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Scrivi una domanda da testare..."
            className="w-full resize-none rounded-md border bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring min-h-[100px]"
            rows={4}
          />
        </div>

        {/* Parameter Controls */}
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground" htmlFor="strategy-select">
              Strategy
            </label>
            <select
              id="strategy-select"
              value={strategy}
              onChange={e => setStrategy(e.target.value as RagStrategy)}
              className="w-full rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            >
              {STRATEGIES.map(s => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </div>

          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground" htmlFor="model-input">
              Model
            </label>
            <input
              id="model-input"
              type="text"
              value={model}
              onChange={e => setModel(e.target.value)}
              placeholder="gpt-4o-mini"
              className="w-full rounded-md border bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>

          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground" htmlFor="temp-input">
              Temperature
            </label>
            <input
              id="temp-input"
              type="number"
              value={temperature}
              onChange={e => setTemperature(Number(e.target.value))}
              min={0}
              max={1}
              step={0.1}
              className="w-full rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>

          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground" htmlFor="topk-input">
              Top-K Chunks
            </label>
            <input
              id="topk-input"
              type="number"
              value={topK}
              onChange={e => setTopK(Number(e.target.value))}
              min={1}
              max={20}
              className="w-full rounded-md border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>

          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground" htmlFor="game-scope-input">
              Game Scope
            </label>
            <input
              id="game-scope-input"
              type="text"
              value={gameScope}
              onChange={e => setGameScope(e.target.value)}
              placeholder="ID o nome gioco"
              className="w-full rounded-md border bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>

          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground" htmlFor="agent-input">
              Agent
            </label>
            <input
              id="agent-input"
              type="text"
              value={agent}
              onChange={e => setAgent(e.target.value)}
              placeholder="Agent ID"
              className="w-full rounded-md border bg-background px-3 py-2 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>
        </div>

        <button
          onClick={handleExecute}
          disabled={!query.trim() || isLoading}
          className={cn(
            'flex w-full items-center justify-center gap-2 rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90',
            'disabled:opacity-50 disabled:cursor-not-allowed'
          )}
          type="button"
        >
          <PlayIcon className="h-4 w-4" />
          {isLoading ? 'Esecuzione in corso...' : 'Esegui Query'}
        </button>
      </div>

      {/* Right Panel: Results */}
      <div className="space-y-4">
        {result ? (
          <>
            {/* Metrics */}
            <div className="grid grid-cols-4 gap-2">
              {[
                { label: 'Latenza', value: `${result.metrics.latencyMs}ms` },
                { label: 'Token', value: result.metrics.tokens },
                { label: 'Costo', value: `$${result.metrics.cost}` },
                { label: 'Confidence', value: `${(result.metrics.confidence * 100).toFixed(0)}%` },
              ].map(m => (
                <div key={m.label} className="rounded-md border bg-muted/30 px-3 py-2 text-center">
                  <div className="text-xs text-muted-foreground">{m.label}</div>
                  <div className="text-sm font-semibold font-mono">{m.value}</div>
                </div>
              ))}
            </div>

            {/* Answer */}
            <div className="space-y-1">
              <h3 className="text-sm font-medium text-foreground">Risposta</h3>
              <div className="rounded-md border bg-background p-3 text-sm whitespace-pre-wrap">
                {result.answer}
              </div>
            </div>

            {/* Chunks */}
            <div className="space-y-1">
              <h3 className="text-sm font-medium text-foreground">
                Chunks Recuperati ({result.chunks.length})
              </h3>
              <div className="space-y-2">
                {result.chunks.map(chunk => (
                  <div key={chunk.id} className="rounded-md border p-3">
                    <div className="flex items-center justify-between mb-1">
                      <span className="text-xs font-mono text-muted-foreground">#{chunk.id}</span>
                      <span
                        className={cn(
                          'rounded-full px-2 py-0.5 text-xs font-medium',
                          chunk.score >= 0.9
                            ? 'bg-green-100 text-green-700'
                            : chunk.score >= 0.75
                              ? 'bg-yellow-100 text-yellow-700'
                              : 'bg-red-100 text-red-700'
                        )}
                      >
                        {chunk.score.toFixed(2)}
                      </span>
                    </div>
                    <p className="text-sm text-foreground">{chunk.text}</p>
                  </div>
                ))}
              </div>
            </div>
          </>
        ) : (
          <div className="flex h-full min-h-[300px] items-center justify-center rounded-lg border border-dashed">
            <p className="text-sm text-muted-foreground">
              Esegui una query per visualizzare i risultati
            </p>
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Chat Debug Tab ───────────────────────────────────────────────────────────

function ChatDebugTab() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [selectedGameId, setSelectedGameId] = useState('');
  const [selectedStrategy, setSelectedStrategy] = useState('');
  const lastQueryRef = useRef<{ gameId: string; query: string } | null>(null);
  const chatEndRef = useRef<HTMLDivElement>(null);
  const messageIdRef = useRef(0);
  const [showDebug, setShowDebug] = useLocalStorage('playground-debug-panel-visible', true);

  const handleToggleDebug = useCallback(() => {
    setShowDebug(prev => !prev);
  }, [setShowDebug]);

  const { state, sendMessage, stopStreaming, reset } = useDebugChatStream({
    onComplete: answer => {
      setMessages(prev => {
        const updated = [...prev];
        const lastIdx = updated.findLastIndex(m => m.role === 'assistant');
        if (lastIdx >= 0) {
          updated[lastIdx] = { ...updated[lastIdx], content: answer };
        }
        return updated;
      });
    },
    onError: error => {
      setMessages(prev => [
        ...prev,
        {
          id: `error-${Date.now()}`,
          role: 'assistant',
          content: `Error: ${error}`,
          timestamp: new Date(),
        },
      ]);
    },
  });

  const handleSend = useCallback(() => {
    const query = inputValue.trim();
    if (!query || !selectedGameId || state.isStreaming) return;

    const userMsgId = ++messageIdRef.current;
    const assistantMsgId = ++messageIdRef.current;
    setMessages(prev => [
      ...prev,
      { id: `user-${userMsgId}`, role: 'user', content: query, timestamp: new Date() },
      { id: `assistant-${assistantMsgId}`, role: 'assistant', content: '', timestamp: new Date() },
    ]);

    lastQueryRef.current = { gameId: selectedGameId, query };
    setInputValue('');

    const strategy = selectedStrategy === '__default__' ? undefined : selectedStrategy || undefined;
    sendMessage(selectedGameId, query, strategy, state.chatThreadId ?? undefined);
    setTimeout(() => chatEndRef.current?.scrollIntoView({ behavior: 'smooth' }), 100);
  }, [
    inputValue,
    selectedGameId,
    selectedStrategy,
    state.isStreaming,
    state.chatThreadId,
    sendMessage,
  ]);

  const handleReExecute = useCallback(() => {
    if (!lastQueryRef.current || state.isStreaming) return;
    const { gameId, query } = lastQueryRef.current;
    reset();
    const reUserMsgId = ++messageIdRef.current;
    const reAssistantMsgId = ++messageIdRef.current;
    setMessages(prev => [
      ...prev,
      {
        id: `user-${reUserMsgId}`,
        role: 'user',
        content: `[Re-execute] ${query}`,
        timestamp: new Date(),
      },
      {
        id: `assistant-${reAssistantMsgId}`,
        role: 'assistant',
        content: '',
        timestamp: new Date(),
      },
    ]);
    const strategy = selectedStrategy === '__default__' ? undefined : selectedStrategy || undefined;
    sendMessage(gameId, query, strategy);
  }, [state.isStreaming, selectedStrategy, sendMessage, reset]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend]
  );

  const displayMessages = messages.map((msg, i) => {
    if (
      msg.role === 'assistant' &&
      i === messages.length - 1 &&
      state.isStreaming &&
      state.currentAnswer
    ) {
      return { ...msg, content: state.currentAnswer };
    }
    return msg;
  });

  return (
    <div className="flex flex-col h-[calc(100vh-14rem)]">
      {/* Strategy selector bar */}
      <StrategySelectorBar
        selectedGameId={selectedGameId}
        onGameChange={setSelectedGameId}
        selectedStrategy={selectedStrategy}
        onStrategyChange={setSelectedStrategy}
        onReExecute={handleReExecute}
        isStreaming={state.isStreaming}
        hasLastQuery={!!lastQueryRef.current}
        showDebug={showDebug}
        onToggleDebug={handleToggleDebug}
      />

      {/* Split view: chat + debug */}
      <div
        className={cn(
          'flex-1 grid min-h-0',
          showDebug ? 'grid-cols-1 lg:grid-cols-[55%_45%]' : 'grid-cols-1'
        )}
      >
        {/* Chat Panel */}
        <div className="flex flex-col border-r min-h-0">
          <div className="flex-1 overflow-y-auto px-4 py-4 space-y-4">
            {displayMessages.length === 0 ? (
              <div className="flex h-full items-center justify-center">
                <p className="text-sm text-muted-foreground text-center max-w-xs">
                  Seleziona un gioco e fai una domanda per iniziare una sessione di debug chat.
                </p>
              </div>
            ) : (
              displayMessages.map(msg => (
                <div
                  key={msg.id}
                  className={cn('flex', msg.role === 'user' ? 'justify-end' : 'justify-start')}
                >
                  <div
                    className={cn(
                      'max-w-[80%] rounded-lg px-3.5 py-2.5 text-sm',
                      msg.role === 'user'
                        ? 'bg-primary text-primary-foreground'
                        : 'bg-muted text-foreground'
                    )}
                  >
                    {msg.content ||
                      (state.isStreaming ? (
                        <span className="inline-flex items-center gap-1 text-muted-foreground">
                          <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse" />
                          <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse [animation-delay:0.2s]" />
                          <span className="h-1.5 w-1.5 rounded-full bg-current animate-pulse [animation-delay:0.4s]" />
                        </span>
                      ) : null)}
                  </div>
                </div>
              ))
            )}
            <div ref={chatEndRef} />
          </div>

          {state.statusMessage && (
            <div className="border-t px-4 py-1.5 text-xs text-muted-foreground bg-muted/30">
              {state.statusMessage}
            </div>
          )}

          {/* Input */}
          <div className="border-t px-4 py-3">
            <div className="flex items-end gap-2">
              <textarea
                value={inputValue}
                onChange={e => setInputValue(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder={selectedGameId ? 'Fai una domanda...' : 'Seleziona prima un gioco'}
                disabled={!selectedGameId || state.isStreaming}
                className={cn(
                  'flex-1 resize-none rounded-md border bg-background px-3 py-2 text-sm',
                  'placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring',
                  'disabled:cursor-not-allowed disabled:opacity-50',
                  'min-h-[40px] max-h-[120px]'
                )}
                rows={1}
              />
              {state.isStreaming ? (
                <button
                  onClick={stopStreaming}
                  className="shrink-0 rounded-md bg-destructive px-3 py-2 text-sm font-medium text-destructive-foreground hover:bg-destructive/90"
                  type="button"
                >
                  Stop
                </button>
              ) : (
                <button
                  onClick={handleSend}
                  disabled={!inputValue.trim() || !selectedGameId}
                  className={cn(
                    'shrink-0 rounded-md bg-primary p-2 text-primary-foreground hover:bg-primary/90',
                    'disabled:opacity-50 disabled:cursor-not-allowed'
                  )}
                  type="button"
                >
                  <SendIcon className="h-4 w-4" />
                </button>
              )}
            </div>
          </div>

          {state.error && (
            <div className="border-t border-destructive/20 bg-destructive/5 px-4 py-2 text-xs text-destructive">
              {state.error}
            </div>
          )}
        </div>

        {/* Debug Timeline Panel */}
        {showDebug && (
          <div className="min-h-0 hidden lg:flex lg:flex-col">
            <DebugTimeline events={state.debugEvents} isStreaming={state.isStreaming} />
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function PlaygroundPage() {
  const searchParams = useSearchParams();
  const tabParam = searchParams.get('tab');
  const defaultTab = (tabParam && TAB_MAP[tabParam]) || 'query-tester';

  return (
    <div className="space-y-6 p-4 lg:p-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          RAG Playground
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Testa query RAG, conversa con gli agenti e confronta esecuzioni
        </p>
      </div>

      {/* Tabs */}
      <Tabs defaultValue={defaultTab}>
        <TabsList>
          <TabsTrigger value="query-tester">Query Tester</TabsTrigger>
          <TabsTrigger value="chat-debug">Chat Debug</TabsTrigger>
          <TabsTrigger value="compare">Compare</TabsTrigger>
        </TabsList>

        <TabsContent value="query-tester">
          <QueryTesterTab />
        </TabsContent>

        <TabsContent value="chat-debug">
          <ChatDebugTab />
        </TabsContent>

        <TabsContent value="compare">
          <CompareTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}
