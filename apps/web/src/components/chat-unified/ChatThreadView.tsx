/**
 * ChatThreadView - Split view layout for active chat (Issue #4364)
 *
 * Composes the split layout:
 * - Header with title, agent, actions
 * - Left: Chat messages + input (60-65%)
 * - Right: ChatInfoPanel (35-40%)
 *
 * Responsive:
 * - Desktop (lg+): Full split view
 * - Tablet/Mobile: Chat full width, info as drawer
 */

'use client';

import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';

import { useRouter } from 'next/navigation';

import { AgentSelector, type AgentType, AGENT_NAMES } from '@/components/agent/AgentSelector';
import { buildWelcomeMessage, getWelcomeFollowUpQuestions } from '@/config/agent-welcome';
import { useAgentChatStream, type ProxyGameContext } from '@/hooks/useAgentChatStream';
import { useAuth } from '@/components/auth/AuthProvider';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import { isAdminOrAbove } from '@/types/auth';
import type { Citation } from '@/types';

import { ChatThreadHeader } from './ChatThreadHeader';
import { DebugStepCard } from './DebugStepCard';
import { DebugSummaryBar } from './DebugSummaryBar';

// ============================================================================
// Types
// ============================================================================

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp?: string;
  citations?: Citation[];
  followUpQuestions?: string[];
}

interface ThreadData {
  id: string;
  title: string;
  gameId?: string | null;
  agentId?: string | null;
  agentTypology?: string | null;
  status: string;
  messages: ChatMessage[];
}

export interface ChatThreadViewProps {
  /** Thread ID from URL params */
  threadId: string;
}

// ============================================================================
// Component
// ============================================================================

export function ChatThreadView({ threadId }: ChatThreadViewProps) {
  const router = useRouter();
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const { user } = useAuth();
  const isAdmin = isAdminOrAbove(user);

  // State
  const [thread, setThread] = useState<ThreadData | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'chat' | 'debug'>('chat');

  const [showAgentConfirm, setShowAgentConfirm] = useState(false);
  const [pendingAgent, setPendingAgent] = useState<AgentType | null>(null);

  // Derived state for info panel
  const [game, setGame] = useState<{ id: string; title: string } | null>(null);

  // SSE Streaming (Issue #4364)
  const { state: streamState, sendMessage: sendViaSSE } = useAgentChatStream({
    onComplete: (answer, metadata) => {
      const assistantMessage: ChatMessage = {
        id: `assistant-${Date.now()}`,
        role: 'assistant',
        content: answer,
        timestamp: new Date().toISOString(),
        followUpQuestions: metadata.followUpQuestions,
      };
      setMessages(prev => [...prev, assistantMessage]);
      setIsSending(false);
    },
    onError: (errorMsg) => {
      setError(errorMsg);
      setIsSending(false);
    },
  });

  // Extract citations from all assistant messages
  const allCitations = useMemo(
    () => messages.flatMap(m => m.citations ?? []),
    [messages]
  );

  // Extract last suggested questions
  const suggestedQuestions = useMemo(() => {
    const lastAssistant = [...messages].reverse().find(m => m.role === 'assistant');
    return lastAssistant?.followUpQuestions ?? [];
  }, [messages]);

  // Auto-scroll to bottom
  const scrollToBottom = useCallback(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, []);

  useEffect(() => {
    scrollToBottom();
  }, [messages, streamState.currentAnswer, scrollToBottom]);

  // Load thread data
  useEffect(() => {
    async function loadThread() {
      setIsLoading(true);
      setError(null);
      try {
        const threadData = await api.chat.getThreadById(threadId);
        if (!threadData) {
          setError('Thread non trovato');
          return;
        }

        const mappedMessages: ChatMessage[] = (threadData.messages ?? []).map((m) => ({
          id: m.backendMessageId ?? `msg-${Date.now()}-${Math.random()}`,
          role: m.role as 'user' | 'assistant',
          content: m.content,
          timestamp: m.timestamp,
        }));

        const threadRecord = threadData as Record<string, unknown>;
        const agentId = (threadRecord.agentId as string | null) ?? null;
        const agentTypology = (threadRecord.agentTypology as string | null) ?? null;

        setThread({
          id: threadData.id,
          title: threadData.title ?? 'Chat',
          gameId: threadData.gameId,
          agentId,
          agentTypology,
          status: 'Active',
          messages: mappedMessages,
        });

        // Load game info if available
        let gameName: string | null = null;
        if (threadData.gameId) {
          try {
            const games = await api.games.getAll();
            const found = (games.games ?? []).find(
              (g: { id: string }) => g.id === threadData.gameId
            );
            if (found) {
              gameName = found.title;
              setGame({
                id: found.id,
                title: found.title,
              });
            }
          } catch {
            // Non-critical: game info not found
          }
        }

        // Issue #4780: Inject welcome message when thread is new (empty) and has an agent
        if (mappedMessages.length === 0 && agentId && gameName) {
          const welcomeContent = buildWelcomeMessage(agentTypology, gameName);
          const followUps = getWelcomeFollowUpQuestions(agentTypology, gameName);
          const welcomeMessage: ChatMessage = {
            id: 'welcome-message',
            role: 'assistant',
            content: welcomeContent,
            timestamp: new Date().toISOString(),
            followUpQuestions: followUps,
          };
          setMessages([welcomeMessage]);
        } else {
          setMessages(mappedMessages);
        }
      } catch {
        setError('Errore nel caricamento della conversazione');
      } finally {
        setIsLoading(false);
      }
    }

    void loadThread();
  }, [threadId]);

  // Send message - SSE streaming when agentId available, REST fallback otherwise
  const handleSendMessage = useCallback(
    async (content?: string) => {
      const messageContent = content || inputValue.trim();
      if (!messageContent || isSending) return;

      setIsSending(true);
      setInputValue('');
      setError(null);

      // Optimistic UI: add user message
      const userMessage: ChatMessage = {
        id: `user-${Date.now()}`,
        role: 'user',
        content: messageContent,
        timestamp: new Date().toISOString(),
      };
      setMessages(prev => [...prev, userMessage]);

      // SSE path: stream via agent endpoint when agentId is available
      if (thread?.agentId) {
        // Issue #4780: Build proxy game context for OpenRouter proxy (if enabled)
        const proxyCtx: ProxyGameContext | undefined =
          game && thread.agentTypology
            ? { gameName: game.title, agentTypology: thread.agentTypology }
            : undefined;
        sendViaSSE(thread.agentId, messageContent, threadId, proxyCtx);
        return; // onComplete/onError callbacks handle the rest
      }

      // REST fallback: standard API call
      try {
        const response = await api.chat.addMessage(threadId, {
          content: messageContent,
          role: 'user',
        });

        if (response?.messages) {
          setMessages(
            response.messages.map((m): ChatMessage => ({
              id: m.backendMessageId ?? `msg-${Date.now()}-${Math.random()}`,
              role: m.role as 'user' | 'assistant',
              content: m.content,
              timestamp: m.timestamp,
            }))
          );
        }
      } catch {
        setError('Errore nell\'invio del messaggio');
        // Remove optimistic message on error
        setMessages(prev => prev.filter(m => m.id !== userMessage.id));
      } finally {
        setIsSending(false);
      }
    },
    [inputValue, isSending, threadId, thread?.agentId, thread?.agentTypology, game, sendViaSSE]
  );

  // Title change
  const handleTitleChange = useCallback(
    async (newTitle: string) => {
      try {
        await api.chat.updateThreadTitle(threadId, newTitle);
        setThread(prev => prev ? { ...prev, title: newTitle } : prev);
      } catch {
        // Silent fail
      }
    },
    [threadId]
  );

  // Delete thread
  const handleDelete = useCallback(async () => {
    try {
      await api.chat.deleteThread(threadId);
      router.push('/chat/new');
    } catch {
      setError('Errore nell\'eliminazione della conversazione');
    }
  }, [threadId, router]);

  // Agent switching with confirmation (Issue #4465)
  const handleAgentChangeRequest = useCallback((newAgent: AgentType) => {
    if (newAgent === (thread?.agentId as AgentType ?? 'auto')) return;
    setPendingAgent(newAgent);
    setShowAgentConfirm(true);
  }, [thread?.agentId]);

  const handleAgentChangeConfirm = useCallback(async () => {
    if (!pendingAgent || !thread) return;
    setShowAgentConfirm(false);
    try {
      await api.chat.switchThreadAgent(thread.id, pendingAgent);
      setThread(prev => prev ? { ...prev, agentId: pendingAgent } : prev);
    } catch {
      setError('Errore nel cambio agente');
    }
    setPendingAgent(null);
  }, [pendingAgent, thread]);

  const handleAgentChangeCancel = useCallback(() => {
    setShowAgentConfirm(false);
    setPendingAgent(null);
  }, []);

  // Handle question click
  const handleQuestionClick = useCallback(
    (question: string) => {
      void handleSendMessage(question);
    },
    [handleSendMessage]
  );

  // Handle key press in input
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        void handleSendMessage();
      }
    },
    [handleSendMessage]
  );

  // Loading state
  if (isLoading) {
    return (
      <div className="flex h-dvh items-center justify-center" data-testid="chat-loading">
        <div className="text-center">
          <div className="animate-spin rounded-full h-10 w-10 border-b-2 border-amber-500 mx-auto mb-3" />
          <p className="text-muted-foreground font-nunito text-sm">Caricamento chat...</p>
        </div>
      </div>
    );
  }

  // Error state
  if (error && !thread) {
    return (
      <div className="flex h-dvh items-center justify-center" data-testid="chat-error">
        <div className="text-center max-w-md">
          <p className="text-red-600 dark:text-red-400 mb-3">{error}</p>
          <a
            href="/chat/new"
            className="text-amber-600 hover:text-amber-700 underline text-sm"
          >
            Torna alla selezione
          </a>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-dvh bg-background" data-testid="chat-thread-view">
      {/* Header */}
      <ChatThreadHeader
        title={thread?.title ?? 'Chat'}
        gameName={game?.title}
        agentName={thread?.agentId ? AGENT_NAMES[(thread.agentId as AgentType) ?? 'auto'] : undefined}
        onTitleChange={handleTitleChange}
        onDelete={handleDelete}
      />

      {/* Agent Selector */}
      <div className="flex items-center gap-2 px-4 py-2 border-b border-border/30 bg-background/80">
        <AgentSelector
          value={(thread?.agentId as AgentType) ?? 'auto'}
          onChange={handleAgentChangeRequest}
          disabled={isSending || streamState.isStreaming}
          className="flex-1"
        />
      </div>

      {/* Agent switch confirmation dialog */}
      {showAgentConfirm && pendingAgent && (
        <div
          className="mx-4 mt-2 p-3 bg-amber-50 dark:bg-amber-500/10 rounded-lg border border-amber-200 dark:border-amber-500/20 flex items-center justify-between gap-3"
          role="alertdialog"
          data-testid="agent-switch-confirm"
        >
          <p className="text-sm font-nunito text-amber-900 dark:text-amber-200">
            Vuoi cambiare agente a <strong>{AGENT_NAMES[pendingAgent]}</strong>? La cronologia viene mantenuta.
          </p>
          <div className="flex gap-2 flex-shrink-0">
            <button
              onClick={() => void handleAgentChangeConfirm()}
              className="px-3 py-1.5 text-xs font-medium bg-amber-500 text-white rounded-md hover:bg-amber-600 transition-colors"
              data-testid="agent-switch-confirm-btn"
            >
              Conferma
            </button>
            <button
              onClick={handleAgentChangeCancel}
              className="px-3 py-1.5 text-xs font-medium bg-white dark:bg-card text-muted-foreground rounded-md border hover:bg-muted/50 transition-colors"
              data-testid="agent-switch-cancel-btn"
            >
              Annulla
            </button>
          </div>
        </div>
      )}

      {/* Error banner */}
      {error && (
        <div
          role="alert"
          className="mx-4 mt-2 p-2 bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-400 rounded-lg text-xs border border-red-200 dark:border-red-500/20"
        >
          {error}
        </div>
      )}

      {/* Split view */}
      <div className="flex flex-1 min-h-0 relative">
        {/* Chat Area (left) */}
        <div className="flex-1 flex flex-col min-w-0">
          {/* Chat / Debug tab bar (Issue #4916) */}
          <div role="tablist" aria-label="Chat panels" className="flex items-center gap-1 px-4 pt-2 pb-0 border-b border-border/30">
            <button
              type="button"
              role="tab"
              aria-selected={activeTab === 'chat'}
              aria-controls="panel-chat"
              onClick={() => setActiveTab('chat')}
              className={cn(
                'px-3 py-1.5 text-xs font-medium rounded-t-md transition-colors',
                activeTab === 'chat'
                  ? 'bg-background border border-b-background border-border/50 text-foreground -mb-px z-10'
                  : 'text-muted-foreground hover:text-foreground'
              )}
              data-testid="tab-chat"
            >
              Chat
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={activeTab === 'debug'}
              aria-controls="panel-debug"
              onClick={() => setActiveTab('debug')}
              className={cn(
                'px-3 py-1.5 text-xs font-medium rounded-t-md transition-colors',
                activeTab === 'debug'
                  ? 'bg-background border border-b-background border-border/50 text-foreground -mb-px z-10'
                  : 'text-muted-foreground hover:text-foreground'
              )}
              data-testid="tab-debug"
            >
              Debug
              {streamState.debugSteps.length > 0 && (
                <span className="ml-1 text-[10px] bg-amber-500/20 text-amber-700 dark:text-amber-400 px-1 rounded">
                  {streamState.debugSteps.length}
                </span>
              )}
            </button>
          </div>

          {/* Debug panel (Issue #4916) */}
          {activeTab === 'debug' && (
            <div id="panel-debug" role="tabpanel" className="flex-1 overflow-y-auto px-4 py-4 space-y-3" data-testid="debug-panel">
              {streamState.debugSteps.length === 0 ? (
                <div className="flex items-center justify-center h-32">
                  <p className="text-sm text-muted-foreground font-nunito">
                    Invia un messaggio per visualizzare il debug del pipeline RAG.
                  </p>
                </div>
              ) : (
                <>
                  <DebugSummaryBar steps={streamState.debugSteps} />
                  <div className="space-y-2">
                    {streamState.debugSteps.map((step, i) => (
                      <DebugStepCard
                        key={`${step.type}-${i}`}
                        step={step}
                        index={i}
                        showSystemPrompt={isAdmin}
                      />
                    ))}
                  </div>
                </>
              )}
            </div>
          )}

          {/* Messages (only when chat tab is active) */}
          {activeTab === 'chat' && (
          <div
            id="panel-chat"
            role="tabpanel"
            className="flex-1 overflow-y-auto px-4 py-4 space-y-4"
            data-testid="messages-area"
          >
            {messages.length === 0 ? (
              <div className="flex items-center justify-center h-full">
                <div className="text-center text-muted-foreground">
                  <p className="text-lg font-quicksand mb-2">Inizia la conversazione</p>
                  <p className="text-sm font-nunito">Scrivi un messaggio per cominciare.</p>
                </div>
              </div>
            ) : (
              messages.map(msg => (
                <div
                  key={msg.id}
                  className={cn(
                    'max-w-[85%] rounded-2xl px-4 py-3',
                    msg.role === 'user'
                      ? 'ml-auto bg-amber-500 text-white'
                      : 'mr-auto bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50'
                  )}
                  data-testid={`message-${msg.role}`}
                >
                  <p className="text-sm whitespace-pre-wrap font-nunito">{msg.content}</p>
                  {msg.citations && msg.citations.length > 0 && (
                    <div className="mt-2 flex flex-wrap gap-1">
                      {msg.citations.map((c, i) => (
                        <span
                          key={`${c.documentId}-${c.pageNumber}-${i}`}
                          className="text-[10px] px-1.5 py-0.5 bg-orange-500/20 text-orange-700 dark:text-orange-300 rounded"
                        >
                          p.{c.pageNumber}
                        </span>
                      ))}
                    </div>
                  )}
                </div>
              ))
            )}
            {/* Streaming status message */}
            {streamState.statusMessage && (
              <div className="flex items-center gap-2 text-xs text-muted-foreground font-nunito" data-testid="stream-status">
                <div className="h-3 w-3 border-2 border-amber-500/30 border-t-amber-500 rounded-full animate-spin" />
                {streamState.statusMessage}
              </div>
            )}

            {/* Streaming response bubble */}
            {streamState.isStreaming && streamState.currentAnswer && (
              <div
                className="max-w-[85%] mr-auto rounded-2xl px-4 py-3 bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50"
                data-testid="message-streaming"
              >
                <p className="text-sm whitespace-pre-wrap font-nunito">{streamState.currentAnswer}</p>
                <div className="mt-1 flex items-center gap-1">
                  <div className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-pulse" />
                  <span className="text-[10px] text-muted-foreground">In scrittura...</span>
                </div>
              </div>
            )}

            <div ref={messagesEndRef} />
          </div>
          )}

          {/* Input */}
          <div
            className="border-t border-border/50 dark:border-border/30 p-4 bg-background/95 backdrop-blur-[12px] dark:bg-card dark:backdrop-blur-none"
            data-testid="chat-input-area"
          >
            <div className="flex items-end gap-2 max-w-3xl mx-auto">
              <textarea
                value={inputValue}
                onChange={e => setInputValue(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Scrivi un messaggio..."
                rows={1}
                className={cn(
                  'flex-1 resize-none rounded-xl border border-border/50 px-4 py-3',
                  'bg-white/70 dark:bg-card/70 backdrop-blur-md text-sm font-nunito',
                  'placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-500/40',
                  'max-h-32'
                )}
                disabled={isSending || streamState.isStreaming}
                data-testid="message-input"
              />
              <button
                onClick={() => void handleSendMessage()}
                disabled={!inputValue.trim() || isSending || streamState.isStreaming}
                className={cn(
                  'p-3 rounded-xl transition-all duration-200 flex-shrink-0',
                  inputValue.trim() && !isSending && !streamState.isStreaming
                    ? 'bg-amber-500 hover:bg-amber-600 text-white cursor-pointer'
                    : 'bg-muted text-muted-foreground cursor-not-allowed'
                )}
                aria-label="Invia messaggio"
                data-testid="send-btn"
              >
                {isSending ? (
                  <div className="h-5 w-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                ) : (
                  <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
                  </svg>
                )}
              </button>
            </div>
          </div>
        </div>

        {/* Info Panel (right) - Hidden on mobile */}
        <div className="hidden lg:flex w-[340px] flex-shrink-0 flex-col border-l border-border/50 bg-card/50 p-4 overflow-y-auto">
          {game && (
            <div className="mb-4">
              <h4 className="text-sm font-semibold font-quicksand mb-1">{game.title}</h4>
              <p className="text-xs text-muted-foreground">Gioco collegato</p>
            </div>
          )}
          {allCitations.length > 0 && (
            <div className="mb-4">
              <h4 className="text-sm font-semibold font-quicksand mb-2">Citazioni ({allCitations.length})</h4>
              <div className="space-y-1">
                {allCitations.slice(0, 10).map((c, i) => (
                  <div key={`${c.documentId}-${c.pageNumber}-${i}`} className="text-xs px-2 py-1 bg-amber-50 dark:bg-amber-500/10 rounded">
                    p.{c.pageNumber} - {c.documentId?.slice(0, 8)}
                  </div>
                ))}
              </div>
            </div>
          )}
          {suggestedQuestions.length > 0 && (
            <div>
              <h4 className="text-sm font-semibold font-quicksand mb-2">Domande suggerite</h4>
              <div className="space-y-1">
                {suggestedQuestions.map((q, i) => (
                  <button
                    key={i}
                    onClick={() => handleQuestionClick(q)}
                    className="block w-full text-left text-xs px-2 py-1.5 rounded hover:bg-muted/50 transition-colors text-muted-foreground hover:text-foreground"
                  >
                    {q}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
