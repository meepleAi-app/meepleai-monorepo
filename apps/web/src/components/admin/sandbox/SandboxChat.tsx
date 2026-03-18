'use client';

import { useState, useRef, useEffect, useCallback, type KeyboardEvent } from 'react';

import { Send, MessageSquare, Clock, Layers, BarChart3 } from 'lucide-react';

import { usePipeline } from '@/components/admin/sandbox/contexts/PipelineContext';
import { useSandboxSession } from '@/components/admin/sandbox/contexts/SandboxSessionContext';
import { useSource } from '@/components/admin/sandbox/contexts/SourceContext';
import { Button } from '@/components/ui/button';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/data-display/tooltip';
import { useDebugChatStream, type DebugEvent } from '@/hooks/useDebugChatStream';

import type { ChatMessage, RetrievedChunk, PipelineTrace, PipelineTraceStep } from './types';

interface SandboxChatProps {
  selectedMessageId: string | null;
  onSelectMessage: (id: string) => void;
}

/**
 * Build RetrievedChunk[] from debug events.
 * The DebugRetrievalResults event contains chunk data.
 */
function extractChunksFromDebug(debugEvents: DebugEvent[]): RetrievedChunk[] {
  const retrievalEvent = debugEvents.find(
    e => e.type === 13 // DebugRetrievalResults
  );
  if (!retrievalEvent?.data) return [];

  const data = retrievalEvent.data as {
    results?: Array<{
      id?: string;
      score?: number;
      text?: string;
      page?: number;
      chunkIndex?: number;
      pdfName?: string;
      used?: boolean;
    }>;
  };

  return (data.results || []).map((r, i) => ({
    id: r.id || crypto.randomUUID(),
    score: r.score ?? 0,
    text: r.text || '',
    page: r.page ?? 0,
    chunkIndex: r.chunkIndex ?? i,
    pdfName: r.pdfName || 'unknown',
    used: r.used ?? true,
  }));
}

/**
 * Build PipelineTrace from debug events timing data.
 */
function buildTraceFromDebug(debugEvents: DebugEvent[]): PipelineTrace {
  const filtered = debugEvents.filter(e => e.type >= 10);
  const steps: PipelineTraceStep[] = filtered.map((e, i) => ({
    name: e.typeName,
    durationMs: i === 0 ? e.elapsedMs : e.elapsedMs - filtered[i - 1].elapsedMs,
    details:
      typeof e.data === 'object' && e.data !== null
        ? (e.data as Record<string, string | number>)
        : {},
  }));

  const totalDurationMs = filtered.length > 0 ? filtered[filtered.length - 1].elapsedMs : 0;

  return { steps, totalDurationMs };
}

export function SandboxChat({ selectedMessageId, onSelectMessage }: SandboxChatProps) {
  const { isAllReady } = usePipeline();
  const { selectedGame } = useSource();
  const { appliedConfig } = useSandboxSession();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const {
    state: streamState,
    sendMessage: sendStreamMessage,
    stopStreaming: _stopStreaming,
  } = useDebugChatStream({
    onComplete: (answer, metadata) => {
      const chunks = extractChunksFromDebug(metadata.debugEvents);
      const trace = buildTraceFromDebug(metadata.debugEvents);
      const avgConfidence =
        chunks.length > 0 ? chunks.reduce((sum, c) => sum + c.score, 0) / chunks.length : 0;

      const assistantMessage: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        content: answer,
        metadata: {
          latencyMs: trace.totalDurationMs,
          chunkCount: chunks.length,
          avgConfidence: Math.round(avgConfidence * 100) / 100,
          chunks,
          trace,
        },
      };

      setMessages(prev => [...prev, assistantMessage]);
    },
    onError: errorMsg => {
      const errorMessage: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        content: `Errore: ${errorMsg}`,
      };
      setMessages(prev => [...prev, errorMessage]);
    },
  });

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, streamState.currentAnswer, scrollToBottom]);

  // Auto-resize textarea
  useEffect(() => {
    const textarea = textareaRef.current;
    if (textarea) {
      textarea.style.height = 'auto';
      textarea.style.height = `${Math.min(textarea.scrollHeight, 120)}px`;
    }
  }, [input]);

  const handleSend = useCallback(() => {
    const trimmed = input.trim();
    if (!trimmed || streamState.isStreaming || !isAllReady || !selectedGame) return;

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: trimmed,
    };

    setMessages(prev => [...prev, userMessage]);
    setInput('');

    // Build configOverride from applied config
    const configOverride = {
      denseWeight: appliedConfig.denseWeight,
      topK: appliedConfig.topK,
      rerankingEnabled: appliedConfig.reranking,
      temperature: appliedConfig.temperature,
      maxTokens: appliedConfig.maxTokens,
      model: appliedConfig.model || undefined,
    };

    sendStreamMessage(
      selectedGame.id,
      trimmed,
      appliedConfig.strategy || undefined,
      streamState.chatThreadId || undefined,
      configOverride,
      undefined, // documentIds
      true // includePrompts for debug
    );
  }, [
    input,
    streamState.isStreaming,
    streamState.chatThreadId,
    isAllReady,
    selectedGame,
    appliedConfig,
    sendStreamMessage,
  ]);

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLTextAreaElement>) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend]
  );

  const isDisabled = !isAllReady || !selectedGame;

  return (
    <div className="flex h-full flex-col" data-testid="sandbox-chat">
      {/* Messages area */}
      <div className="flex-1 overflow-y-auto p-4 space-y-3" data-testid="messages-area">
        {messages.length === 0 && !streamState.currentAnswer && (
          <div className="flex flex-col items-center justify-center h-full gap-3 text-muted-foreground">
            <MessageSquare className="h-10 w-10 opacity-30" />
            <p className="font-nunito text-sm" data-testid="welcome-message">
              Invia un messaggio per testare il RAG agent
            </p>
          </div>
        )}

        {messages.map(msg => (
          <div
            key={msg.id}
            className={`flex ${msg.role === 'user' ? 'justify-end' : 'justify-start'}`}
          >
            <div
              data-testid={`message-${msg.role}`}
              className={`max-w-[85%] rounded-lg px-3 py-2 font-nunito text-sm cursor-pointer transition-all ${
                msg.role === 'user'
                  ? 'bg-blue-50 border border-blue-200 text-blue-900'
                  : `bg-white border text-foreground ${
                      selectedMessageId === msg.id
                        ? 'ring-2 ring-amber-400 border-amber-300'
                        : 'border-gray-200'
                    }`
              }`}
              onClick={() => msg.role === 'assistant' && onSelectMessage(msg.id)}
            >
              <p className="whitespace-pre-wrap">{msg.content}</p>

              {/* Metadata footer for assistant messages */}
              {msg.role === 'assistant' && msg.metadata && (
                <div className="mt-2 flex items-center gap-3 text-xs text-muted-foreground border-t pt-1.5">
                  <span className="flex items-center gap-1" data-testid="msg-latency">
                    <Clock className="h-3 w-3" />
                    {msg.metadata.latencyMs}ms
                  </span>
                  <span className="flex items-center gap-1" data-testid="msg-chunks">
                    <Layers className="h-3 w-3" />
                    {msg.metadata.chunkCount} chunk
                  </span>
                  <span className="flex items-center gap-1" data-testid="msg-confidence">
                    <BarChart3 className="h-3 w-3" />
                    {(msg.metadata.avgConfidence * 100).toFixed(0)}%
                  </span>
                </div>
              )}
            </div>
          </div>
        ))}

        {/* Streaming response in-progress */}
        {streamState.isStreaming && streamState.currentAnswer && (
          <div className="flex justify-start">
            <div className="max-w-[85%] rounded-lg px-3 py-2 bg-white border border-gray-200 font-nunito text-sm">
              <p className="whitespace-pre-wrap">{streamState.currentAnswer}</p>
              <div className="mt-1 text-xs text-muted-foreground animate-pulse">
                {streamState.statusMessage || 'Generazione in corso...'}
              </div>
            </div>
          </div>
        )}

        {/* Typing indicator */}
        {streamState.isStreaming && !streamState.currentAnswer && (
          <div className="flex justify-start">
            <div className="rounded-lg border border-gray-200 bg-white px-3 py-2">
              <div className="flex items-center gap-1">
                <span className="h-2 w-2 rounded-full bg-gray-400 animate-bounce [animation-delay:0ms]" />
                <span className="h-2 w-2 rounded-full bg-gray-400 animate-bounce [animation-delay:150ms]" />
                <span className="h-2 w-2 rounded-full bg-gray-400 animate-bounce [animation-delay:300ms]" />
              </div>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input area */}
      <div className="border-t bg-white/50 p-3">
        <div className="flex items-end gap-2">
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <div className="flex-1">
                  <textarea
                    ref={textareaRef}
                    data-testid="chat-input"
                    className="w-full resize-none rounded-lg border bg-white px-3 py-2 font-nunito text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-400/50 disabled:cursor-not-allowed disabled:opacity-50"
                    placeholder={isDisabled ? 'Pipeline non pronta' : 'Scrivi un messaggio...'}
                    value={input}
                    onChange={e => setInput(e.target.value)}
                    onKeyDown={handleKeyDown}
                    disabled={isDisabled}
                    rows={1}
                  />
                </div>
              </TooltipTrigger>
              {isDisabled && (
                <TooltipContent>
                  <p>Pipeline non pronta</p>
                </TooltipContent>
              )}
            </Tooltip>
          </TooltipProvider>
          <Button
            data-testid="send-button"
            size="icon"
            className="shrink-0 bg-amber-500 hover:bg-amber-600 text-white"
            onClick={handleSend}
            disabled={isDisabled || !input.trim() || streamState.isStreaming}
          >
            <Send className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
