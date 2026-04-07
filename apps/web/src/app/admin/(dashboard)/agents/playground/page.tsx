'use client';

/**
 * RAG Playground - Debug chat with RAG agents.
 *
 * Single-view debug chat with streaming, pipeline debug events,
 * and parameter overrides (model, temperature, top-K).
 *
 * Protected by admin layout (RequireRole(['Admin'])).
 */

import { useState, useCallback, useRef, useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';
import { SendIcon } from 'lucide-react';

import { DebugTimeline, StrategySelectorBar } from '@/components/admin/debug-chat';
import type { DebugChatConfigOverride } from '@/hooks/useDebugChatStream';
import { useDebugChatStream } from '@/hooks/useDebugChatStream';
import { useLocalStorage } from '@/hooks/useLocalStorage';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

// ─── Chat Debug Tab ───────────────────────────────────────────────────────────

function ChatDebugTab() {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [selectedGameId, setSelectedGameId] = useState('');
  const [selectedStrategy, setSelectedStrategy] = useState('');
  const [selectedModel, setSelectedModel] = useState('');
  const [temperature, setTemperature] = useState(0.7);
  const [topK, setTopK] = useState(5);
  const lastQueryRef = useRef<{ gameId: string; query: string } | null>(null);
  const chatEndRef = useRef<HTMLDivElement>(null);
  const messageIdRef = useRef(0);
  const [showDebug, setShowDebug] = useLocalStorage('playground-debug-panel-visible', true);

  const adminClient = useMemo(() => {
    const httpClient = new HttpClient();
    return createAdminClient({ httpClient });
  }, []);

  const { data: aiModels, isLoading: modelsLoading } = useQuery({
    queryKey: ['admin', 'ai-models', 'active'],
    queryFn: () => adminClient.getAiModels({ status: 'active' }),
    staleTime: 300_000,
  });

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

    const configOverride: DebugChatConfigOverride | undefined =
      (selectedModel && selectedModel !== '__default__') || temperature !== 0.7 || topK !== 5
        ? {
            model: selectedModel && selectedModel !== '__default__' ? selectedModel : undefined,
            temperature,
            topK,
          }
        : undefined;

    sendMessage(selectedGameId, query, strategy, state.chatThreadId ?? undefined, configOverride);
    setTimeout(() => chatEndRef.current?.scrollIntoView({ behavior: 'smooth' }), 100);
  }, [
    inputValue,
    selectedGameId,
    selectedStrategy,
    selectedModel,
    temperature,
    topK,
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

    const configOverride: DebugChatConfigOverride | undefined =
      (selectedModel && selectedModel !== '__default__') || temperature !== 0.7 || topK !== 5
        ? {
            model: selectedModel && selectedModel !== '__default__' ? selectedModel : undefined,
            temperature,
            topK,
          }
        : undefined;

    sendMessage(gameId, query, strategy, undefined, configOverride);
  }, [state.isStreaming, selectedStrategy, selectedModel, temperature, topK, sendMessage, reset]);

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
        selectedModel={selectedModel}
        onModelChange={setSelectedModel}
        temperature={temperature}
        onTemperatureChange={setTemperature}
        topK={topK}
        onTopKChange={setTopK}
        availableModels={(aiModels?.items ?? []).map((m: any) => ({
          id: m.id,
          displayName: m.displayName,
          modelIdentifier: m.modelIdentifier,
        }))}
        modelsLoading={modelsLoading}
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
  return (
    <div className="space-y-6 p-4 lg:p-6">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          RAG Playground
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Debug chat con gli agenti RAG — streaming, pipeline debug e override parametri
        </p>
      </div>
      <ChatDebugTab />
    </div>
  );
}
