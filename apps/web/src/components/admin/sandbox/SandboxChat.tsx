'use client';

import { useState, useRef, useEffect, useCallback, type KeyboardEvent } from 'react';

import { Send, MessageSquare, Clock, Layers, BarChart3 } from 'lucide-react';

import { usePipeline } from '@/components/admin/sandbox/contexts/PipelineContext';
import { Button } from '@/components/ui/button';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/data-display/tooltip';

import type { ChatMessage, RetrievedChunk, PipelineTrace } from './types';

interface SandboxChatProps {
  selectedMessageId: string | null;
  onSelectMessage: (id: string) => void;
}

function generateMockChunks(): RetrievedChunk[] {
  return [
    {
      id: crypto.randomUUID(),
      score: 0.85,
      text: 'Il gioco si prepara distribuendo le carte iniziali a ciascun giocatore e posizionando il tabellone al centro del tavolo.',
      page: 3,
      chunkIndex: 0,
      pdfName: 'regolamento.pdf',
      used: true,
    },
    {
      id: crypto.randomUUID(),
      score: 0.72,
      text: 'Ogni giocatore riceve 5 risorse iniziali e un segnalino del colore scelto.',
      page: 4,
      chunkIndex: 1,
      pdfName: 'regolamento.pdf',
      used: true,
    },
    {
      id: crypto.randomUUID(),
      score: 0.45,
      text: 'Le carte bonus vengono mescolate e poste coperte accanto al tabellone principale.',
      page: 5,
      chunkIndex: 2,
      pdfName: 'regolamento.pdf',
      used: false,
    },
  ];
}

function generateMockTrace(): PipelineTrace {
  return {
    steps: [
      {
        name: 'Query Analysis',
        durationMs: 12,
        details: { language: 'it', intent: 'setup_rules', entities: 2 },
      },
      {
        name: 'Dense Search',
        durationMs: 45,
        details: { collection: 'game_docs', candidates: 50, topScore: 0.85 },
      },
      { name: 'Sparse Search', durationMs: 28, details: { bm25Matches: 12, topScore: 0.72 } },
      {
        name: 'Hybrid Merge',
        durationMs: 5,
        details: { denseWeight: 0.7, sparseWeight: 0.3, uniqueResults: 15 },
      },
      {
        name: 'Reranking',
        durationMs: 120,
        details: { model: 'cross-encoder/ms-marco', inputCount: 15, outputCount: 5 },
      },
      {
        name: 'LLM Generation',
        durationMs: 890,
        details: {
          model: 'openrouter/gpt-4',
          tokensIn: 1200,
          tokensOut: 350,
          temperature: 0.3,
          cost: 0.004,
        },
      },
    ],
    totalDurationMs: 1100,
  };
}

export function SandboxChat({ selectedMessageId, onSelectMessage }: SandboxChatProps) {
  const { isAllReady } = usePipeline();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [isSending, setIsSending] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, scrollToBottom]);

  // Auto-resize textarea
  useEffect(() => {
    const textarea = textareaRef.current;
    if (textarea) {
      textarea.style.height = 'auto';
      textarea.style.height = `${Math.min(textarea.scrollHeight, 120)}px`;
    }
  }, [input]);

  const handleSend = useCallback(async () => {
    const trimmed = input.trim();
    if (!trimmed || isSending || !isAllReady) return;

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: trimmed,
    };

    setMessages(prev => [...prev, userMessage]);
    setInput('');
    setIsSending(true);

    // Mock assistant response after delay
    setTimeout(() => {
      const chunks = generateMockChunks();
      const trace = generateMockTrace();
      const avgConfidence = chunks.reduce((sum, c) => sum + c.score, 0) / chunks.length;

      const assistantMessage: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        content: `Basandomi sul regolamento, ecco la risposta alla tua domanda: "${trimmed}". Il gioco prevede una fase di preparazione in cui ogni giocatore riceve le carte iniziali e le risorse di partenza.`,
        metadata: {
          latencyMs: trace.totalDurationMs,
          chunkCount: chunks.length,
          avgConfidence: Math.round(avgConfidence * 100) / 100,
          chunks,
          trace,
        },
      };

      setMessages(prev => [...prev, assistantMessage]);
      setIsSending(false);
    }, 500);
  }, [input, isSending, isAllReady]);

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLTextAreaElement>) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend]
  );

  const isDisabled = !isAllReady;

  return (
    <div className="flex h-full flex-col" data-testid="sandbox-chat">
      {/* Messages area */}
      <div className="flex-1 overflow-y-auto p-4 space-y-3" data-testid="messages-area">
        {messages.length === 0 && (
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

        {isSending && (
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
            disabled={isDisabled || !input.trim() || isSending}
          >
            <Send className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
