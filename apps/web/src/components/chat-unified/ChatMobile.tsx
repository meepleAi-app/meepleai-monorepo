/**
 * ChatMobile - Full mobile chat view with SSE streaming
 *
 * Props: threadId
 * Loads thread via api.chat.getThreadById, renders messages,
 * and streams responses via useAgentChatStream.
 */

'use client';

import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';

import { Send } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { useAgentChatStream } from '@/hooks/useAgentChatStream';
import { api } from '@/lib/api';
import { qaStream } from '@/lib/api/clients/chatClient';
import type { ChatThreadDto, ChatThreadMessageDto } from '@/lib/api/schemas/chat.schemas';
import { cn } from '@/lib/utils';

import { QuickPromptChips } from './QuickPromptChips';

// ─── Types ──────────────────────────────────────────────────────────────────

interface LocalMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: string;
  citations?: CitationItem[];
  followUpQuestions?: string[];
}

interface CitationItem {
  title: string;
  source?: string;
}

export interface ChatMobileProps {
  threadId: string;
}

// ─── Citation Chip ──────────────────────────────────────────────────────────

function CitationChip({ citation }: { citation: CitationItem }) {
  return (
    <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-300 text-[10px] font-nunito">
      {citation.title}
    </span>
  );
}

// ─── Message Bubble ─────────────────────────────────────────────────────────

function MessageBubble({ message }: { message: LocalMessage }) {
  const isUser = message.role === 'user';

  return (
    <div className={cn('flex', isUser ? 'justify-end' : 'justify-start')}>
      <div
        className={cn(
          'max-w-[85%] rounded-2xl px-4 py-2.5 text-sm font-nunito',
          isUser
            ? 'bg-[var(--gaming-bg-elevated)] text-white rounded-br-md'
            : 'bg-[var(--gaming-bg-glass)] backdrop-blur-sm border border-[var(--gaming-border-glass)] text-[var(--gaming-text-primary)] rounded-bl-md'
        )}
      >
        <p className="whitespace-pre-wrap break-words">{message.content}</p>
        {/* Citations */}
        {message.citations && message.citations.length > 0 && (
          <div className="flex flex-wrap gap-1 mt-2">
            {message.citations.map((c, i) => (
              <CitationChip key={i} citation={c} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

// ─── Streaming Bubble ───────────────────────────────────────────────────────

function StreamingBubble({ content }: { content: string }) {
  return (
    <div className="flex justify-start">
      <div
        className={cn(
          'max-w-[85%] rounded-2xl rounded-bl-md px-4 py-2.5 text-sm font-nunito',
          'bg-[var(--gaming-bg-glass)] backdrop-blur-sm border border-[var(--gaming-border-glass)] text-[var(--gaming-text-primary)]'
        )}
      >
        <p className="whitespace-pre-wrap break-words">{content}</p>
        <span className="inline-block w-2 h-4 ml-0.5 bg-amber-500 animate-pulse rounded-sm" />
      </div>
    </div>
  );
}

// ─── Streaming Loading Dots ─────────────────────────────────────────────────

function StreamingLoadingDots({ message }: { message: string }) {
  return (
    <div className="flex justify-start">
      <div className="max-w-[85%] rounded-2xl rounded-bl-md px-4 py-3 bg-[var(--gaming-bg-glass)] border border-[var(--gaming-border-glass)]">
        <div className="flex items-center gap-2">
          <div className="flex gap-1">
            <span className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-bounce" />
            <span className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-bounce [animation-delay:0.15s]" />
            <span className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-bounce [animation-delay:0.3s]" />
          </div>
          <span className="text-xs text-[var(--gaming-text-secondary)] font-nunito">{message}</span>
        </div>
      </div>
    </div>
  );
}

// ─── Main Component ─────────────────────────────────────────────────────────

export function ChatMobile({ threadId }: ChatMobileProps) {
  const router = useRouter();
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // State
  const [thread, setThread] = useState<ChatThreadDto | null>(null);
  const [messages, setMessages] = useState<LocalMessage[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [agentId, setAgentId] = useState<string | null>(null);
  const [isSending, setIsSending] = useState(false);
  const qaAbortRef = useRef<AbortController | null>(null);

  // SSE Streaming
  const { state: streamState, sendMessage: sendViaSSE } = useAgentChatStream({
    onComplete: (answer, metadata) => {
      const assistantMessage: LocalMessage = {
        id: `assistant-${Date.now()}`,
        role: 'assistant',
        content: answer,
        timestamp: new Date().toISOString(),
        followUpQuestions: metadata.followUpQuestions,
      };
      setMessages(prev => [...prev, assistantMessage]);
    },
    onError: () => {
      // Error state handled via streamState.error
    },
  });

  // Follow-up questions from the last assistant message
  const followUpQuestions = useMemo(() => {
    const lastAssistant = [...messages].reverse().find(m => m.role === 'assistant');
    return lastAssistant?.followUpQuestions ?? [];
  }, [messages]);

  // Also include streaming follow-ups
  const activeFollowUps = useMemo(() => {
    if (streamState.followUpQuestions.length > 0) return streamState.followUpQuestions;
    return followUpQuestions;
  }, [streamState.followUpQuestions, followUpQuestions]);

  // Auto-scroll
  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, streamState.currentAnswer, scrollToBottom]);

  // Abort QA stream on unmount
  useEffect(() => {
    return () => {
      qaAbortRef.current?.abort();
    };
  }, []);

  // Load thread
  useEffect(() => {
    async function loadThread() {
      setIsLoading(true);
      setLoadError(null);
      try {
        const threadData = await api.chat.getThreadById(threadId);
        if (!threadData) {
          setLoadError('Thread non trovato');
          return;
        }

        setThread(threadData);
        setAgentId(threadData.agentId ?? null);

        const mapped: LocalMessage[] = (threadData.messages ?? []).map(
          (m: ChatThreadMessageDto) => ({
            id: m.backendMessageId ?? `msg-${Date.now()}-${Math.random()}`,
            role: m.role as 'user' | 'assistant',
            content: m.content,
            timestamp: m.timestamp,
          })
        );
        setMessages(mapped);
      } catch {
        setLoadError('Errore nel caricamento della conversazione');
      } finally {
        setIsLoading(false);
      }
    }

    void loadThread();
  }, [threadId]);

  // Send message — SSE streaming when agentId or gameId available, REST fallback otherwise
  const handleSend = useCallback(
    (content?: string) => {
      const text = (content || inputValue).trim();
      if (!text || streamState.isStreaming || isSending) return;

      setInputValue('');

      // Optimistic: add user message
      const userMessage: LocalMessage = {
        id: `user-${Date.now()}`,
        role: 'user',
        content: text,
        timestamp: new Date().toISOString(),
      };
      setMessages(prev => [...prev, userMessage]);

      // SSE path: agent-specific streaming
      if (agentId) {
        sendViaSSE(agentId, text, threadId);
        return;
      }

      // QA stream path: game context available → use RAG QA streaming
      if (thread?.gameId) {
        setIsSending(true);
        const assistantMsgId = `assistant-${Date.now()}`;
        setMessages(prev => [
          ...prev,
          {
            id: assistantMsgId,
            role: 'assistant',
            content: '',
            timestamp: new Date().toISOString(),
          },
        ]);

        const abortController = new AbortController();
        qaAbortRef.current = abortController;

        void (async () => {
          let finalAnswer = '';
          let followUps: string[] = [];
          try {
            await api.chat.addMessage(threadId, { content: text, role: 'user' });

            for await (const event of qaStream(
              { gameId: thread.gameId!, query: text, chatId: threadId },
              abortController.signal
            )) {
              switch (event.type) {
                case 7: {
                  const token =
                    typeof event.data === 'string'
                      ? event.data
                      : ((event.data as { token?: string })?.token ?? '');
                  if (token) {
                    finalAnswer += token;
                    const currentContent = finalAnswer;
                    setMessages(prev =>
                      prev.map(m =>
                        m.id === assistantMsgId ? { ...m, content: currentContent } : m
                      )
                    );
                  }
                  break;
                }
                case 4: {
                  const data = event.data as {
                    answer?: string;
                    followUpQuestions?: string[];
                  };
                  if (data.answer) finalAnswer = data.answer;
                  if (data.followUpQuestions) followUps = data.followUpQuestions;
                  break;
                }
                case 6: {
                  const citations = (event.data as { snippets?: CitationItem[] })?.snippets;
                  if (citations) {
                    setMessages(prev =>
                      prev.map(m => (m.id === assistantMsgId ? { ...m, citations } : m))
                    );
                  }
                  break;
                }
                case 9: {
                  const errData = event.data as { message?: string };
                  setMessages(prev =>
                    prev.map(m =>
                      m.id === assistantMsgId
                        ? { ...m, content: errData?.message ?? 'Errore nella risposta' }
                        : m
                    )
                  );
                  break;
                }
              }
            }

            // Finalize
            setMessages(prev =>
              prev.map(m =>
                m.id === assistantMsgId
                  ? {
                      ...m,
                      content: finalAnswer,
                      followUpQuestions: followUps.length > 0 ? followUps : undefined,
                    }
                  : m
              )
            );
          } catch {
            if (!abortController.signal.aborted) {
              setMessages(prev =>
                prev.map(m =>
                  m.id === assistantMsgId
                    ? { ...m, content: finalAnswer || 'Errore nella generazione della risposta' }
                    : m
                )
              );
            }
          } finally {
            setIsSending(false);
            qaAbortRef.current = null;
          }
        })();
        return;
      }

      // REST fallback (no game, no agent)
      void (async () => {
        try {
          const response = await api.chat.addMessage(threadId, {
            content: text,
            role: 'user',
          });
          if (response?.messages) {
            setMessages(
              response.messages.map(m => ({
                id: m.backendMessageId ?? `msg-${Date.now()}-${Math.random()}`,
                role: m.role as 'user' | 'assistant',
                content: m.content,
                timestamp: m.timestamp,
              }))
            );
          }
        } catch {
          setMessages(prev => prev.filter(m => m.id !== userMessage.id));
        }
      })();
    },
    [inputValue, streamState.isStreaming, isSending, agentId, thread?.gameId, threadId, sendViaSSE]
  );

  // Key handler
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend]
  );

  // Header title
  const headerTitle = thread?.title ?? 'Chat';

  // Loading
  if (isLoading) {
    return (
      <div className="flex flex-col h-dvh bg-[var(--gaming-bg-base)]">
        <MobileHeader title="Caricamento..." onBack={() => router.push('/chat')} />
        <div className="flex-1 flex items-center justify-center">
          <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-amber-500" />
        </div>
      </div>
    );
  }

  // Load error
  if (loadError) {
    return (
      <div className="flex flex-col h-dvh bg-[var(--gaming-bg-base)]">
        <MobileHeader title="Errore" onBack={() => router.push('/chat')} />
        <div className="flex-1 flex items-center justify-center px-6">
          <div className="text-center">
            <p className="text-red-500 font-nunito mb-3">{loadError}</p>
            <button
              onClick={() => router.push('/chat')}
              className="text-amber-600 hover:text-amber-700 underline text-sm font-nunito"
            >
              Torna alla lista chat
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-dvh bg-[var(--gaming-bg-base)]">
      {/* Header */}
      <MobileHeader title={headerTitle} onBack={() => router.push('/chat')} />

      {/* Messages area */}
      <div className="flex-1 overflow-y-auto px-3 py-4 space-y-3">
        {messages.length === 0 && !streamState.isStreaming && (
          <div className="text-center py-12 text-[var(--gaming-text-secondary)] font-nunito">
            <p className="text-sm">Inizia la conversazione inviando un messaggio.</p>
          </div>
        )}

        {messages.map(msg => (
          <MessageBubble key={msg.id} message={msg} />
        ))}

        {/* Streaming states */}
        {streamState.isStreaming && streamState.currentAnswer && (
          <StreamingBubble content={streamState.currentAnswer} />
        )}

        {streamState.isStreaming && !streamState.currentAnswer && streamState.statusMessage && (
          <StreamingLoadingDots message={streamState.statusMessage} />
        )}

        {/* Stream error */}
        {streamState.error && (
          <div className="mx-2 p-2 bg-red-500/10 text-red-400 rounded-lg text-xs font-nunito border border-red-500/20">
            {streamState.error}
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Quick prompts (hidden while streaming) */}
      {!streamState.isStreaming && activeFollowUps.length > 0 && (
        <QuickPromptChips
          prompts={activeFollowUps}
          onSelect={q => handleSend(q)}
          className="px-4 py-2"
        />
      )}

      {/* Input area */}
      <div className="border-t border-[var(--gaming-border-glass)] px-3 py-3 bg-[var(--gaming-bg-elevated)] backdrop-blur-sm">
        <div className="flex items-end gap-2">
          <textarea
            value={inputValue}
            onChange={e => setInputValue(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Scrivi un messaggio..."
            rows={1}
            className={cn(
              'flex-1 resize-none rounded-xl border border-[var(--gaming-border-glass)] px-4 py-2.5',
              'bg-[var(--gaming-bg-glass)] text-sm font-nunito',
              'placeholder:text-[var(--gaming-text-secondary)] focus:outline-none focus:ring-2 focus:ring-amber-500/40',
              'max-h-24'
            )}
            disabled={streamState.isStreaming}
          />
          <button
            onClick={() => handleSend()}
            disabled={!inputValue.trim() || streamState.isStreaming}
            className={cn(
              'p-2.5 rounded-xl transition-all duration-200 shrink-0',
              inputValue.trim() && !streamState.isStreaming
                ? 'bg-amber-500 hover:bg-amber-600 text-white'
                : 'bg-[var(--gaming-bg-glass)] text-[var(--gaming-text-secondary)]'
            )}
            aria-label="Invia messaggio"
          >
            {streamState.isStreaming ? (
              <div className="h-5 w-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            ) : (
              <Send className="h-5 w-5" />
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
