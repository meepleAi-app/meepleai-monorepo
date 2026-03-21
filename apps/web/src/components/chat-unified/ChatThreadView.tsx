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

import { Wifi, WifiOff } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { AgentSelector, type AgentType, AGENT_NAMES } from '@/components/agent/AgentSelector';
import { AgentSettingsDrawer } from '@/components/agent/settings';
import { useAuth } from '@/components/auth/AuthProvider';
import { buildWelcomeMessage, getWelcomeFollowUpQuestions } from '@/config/agent-welcome';
import { useAgentChatStream, type ProxyGameContext } from '@/hooks/useAgentChatStream';
import { useVoiceInput } from '@/hooks/useVoiceInput';
import { useVoiceOutput } from '@/hooks/useVoiceOutput';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import { useVoicePreferencesStore } from '@/store/voice/store';
import { isAdminOrAbove, isEditorOrAbove } from '@/types/auth';

import { AgentSwitchDialog } from './AgentSwitchDialog';
import { ChatInfoPanel } from './ChatInfoPanel';
import { ChatInputArea } from './ChatInputArea';
import { ChatMessageList, type ChatMessageItem } from './ChatMessageList';
import { ChatThreadHeader } from './ChatThreadHeader';
import { DebugStepCard } from './DebugStepCard';
import { DebugSummaryBar } from './DebugSummaryBar';
import { VoiceTranscriptOverlay } from './VoiceTranscriptOverlay';

// ============================================================================
// Types
// ============================================================================

interface ThreadData {
  id: string;
  title: string;
  gameId?: string | null;
  agentId?: string | null;
  agentTypology?: string | null;
  status: string;
  messages: ChatMessageItem[];
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
  const isEditor = isEditorOrAbove(user);

  // State
  const [thread, setThread] = useState<ThreadData | null>(null);
  const [messages, setMessages] = useState<ChatMessageItem[]>([]);
  const [inputValue, setInputValue] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'chat' | 'debug'>('chat');

  const [settingsOpen, setSettingsOpen] = useState(false);
  const [showAgentConfirm, setShowAgentConfirm] = useState(false);
  const [pendingAgent, setPendingAgent] = useState<AgentType | null>(null);

  // Derived state for info panel
  const [game, setGame] = useState<{ id: string; title: string } | null>(null);

  // Voice input/output
  const voicePrefs = useVoicePreferencesStore();
  const lastMessageWasVoiceRef = useRef(false);
  const handleSendRef = useRef<((content?: string) => void) | undefined>(undefined);

  const {
    state: voiceState,
    interimText,
    finalText,
    startListening,
    stopListening,
    cancelListening,
    isSupported: isVoiceSupported,
  } = useVoiceInput({
    language: voicePrefs.language,
    autoSend: voicePrefs.autoSend,
    onTranscript: text => {
      if (voicePrefs.autoSend && text.trim()) {
        lastMessageWasVoiceRef.current = true;
        handleSendRef.current?.(text);
      }
    },
  });

  const {
    speak,
    stop: stopSpeaking,
    isSpeaking,
    isSupported: isTtsSupported,
    availableVoices,
  } = useVoiceOutput({
    language: voicePrefs.language,
    preferredVoiceURI: voicePrefs.voiceURI ?? undefined,
    rate: voicePrefs.rate,
    autoDetectLanguage: true,
  });

  const handleVoiceTap = useCallback(() => {
    if (voiceState === 'listening') {
      stopListening();
    } else if (voiceState === 'idle' || voiceState === 'error') {
      if (isSpeaking) stopSpeaking();
      startListening();
    }
  }, [voiceState, isSpeaking, stopListening, startListening, stopSpeaking]);

  // SSE Streaming (Issue #4364)
  const { state: streamState, sendMessage: sendViaSSE } = useAgentChatStream({
    onComplete: (answer, metadata) => {
      const assistantMessage: ChatMessageItem = {
        id: `assistant-${Date.now()}`,
        role: 'assistant',
        content: answer,
        timestamp: new Date().toISOString(),
        followUpQuestions: metadata.followUpQuestions,
      };
      setMessages(prev => [...prev, assistantMessage]);
      // TTS: auto-speak response when last message was voice-initiated
      if (voicePrefs.ttsEnabled && lastMessageWasVoiceRef.current) {
        speak(answer);
        lastMessageWasVoiceRef.current = false;
      }
      setIsSending(false);
    },
    onError: errorMsg => {
      setError(errorMsg);
      setIsSending(false);
    },
  });

  // Extract citations from all assistant messages
  const allCitations = useMemo(() => messages.flatMap(m => m.citations ?? []), [messages]);

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

        const mappedMessages: ChatMessageItem[] = (threadData.messages ?? []).map(m => ({
          id: m.backendMessageId ?? `msg-${Date.now()}-${Math.random()}`,
          role: m.role as 'user' | 'assistant',
          content: m.content,
          timestamp: m.timestamp,
        }));

        const agentId = threadData.agentId ?? null;
        const agentTypology = threadData.agentType ?? null;

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
          const welcomeMessage: ChatMessageItem = {
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
      const userMessage: ChatMessageItem = {
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
            response.messages.map(
              (m): ChatMessageItem => ({
                id: m.backendMessageId ?? `msg-${Date.now()}-${Math.random()}`,
                role: m.role as 'user' | 'assistant',
                content: m.content,
                timestamp: m.timestamp,
              })
            )
          );
        }
      } catch {
        setError("Errore nell'invio del messaggio");
        // Remove optimistic message on error
        setMessages(prev => prev.filter(m => m.id !== userMessage.id));
      } finally {
        setIsSending(false);
      }
    },
    [inputValue, isSending, threadId, thread?.agentId, thread?.agentTypology, game, sendViaSSE]
  );

  // Keep ref in sync for voice onTranscript callback
  handleSendRef.current = handleSendMessage;

  // Title change
  const handleTitleChange = useCallback(
    async (newTitle: string) => {
      try {
        await api.chat.updateThreadTitle(threadId, newTitle);
        setThread(prev => (prev ? { ...prev, title: newTitle } : prev));
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
      setError("Errore nell'eliminazione della conversazione");
    }
  }, [threadId, router]);

  // Agent switching with confirmation (Issue #4465)
  const handleAgentChangeRequest = useCallback(
    (newAgent: AgentType) => {
      if (newAgent === ((thread?.agentTypology as AgentType) ?? 'auto')) return;
      setPendingAgent(newAgent);
      setShowAgentConfirm(true);
    },
    [thread?.agentTypology]
  );

  const handleAgentChangeConfirm = useCallback(async () => {
    if (!pendingAgent || !thread) return;
    setShowAgentConfirm(false);
    try {
      await api.chat.switchThreadAgent(thread.id, pendingAgent);
      setThread(prev => (prev ? { ...prev, agentTypology: pendingAgent } : prev));
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
          <a href="/chat/new" className="text-amber-600 hover:text-amber-700 underline text-sm">
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
        agentName={
          thread?.agentTypology
            ? AGENT_NAMES[(thread.agentTypology as AgentType) ?? 'auto']
            : undefined
        }
        onTitleChange={handleTitleChange}
        onSettings={thread?.agentId ? () => setSettingsOpen(true) : undefined}
        onDelete={handleDelete}
      />

      {/* Agent Selector + Connection Status */}
      <div className="flex items-center gap-2 px-4 py-2 border-b border-border/30 bg-background/80">
        <AgentSelector
          value={(thread?.agentTypology as AgentType) ?? 'auto'}
          onChange={handleAgentChangeRequest}
          disabled={isSending || streamState.isStreaming}
          className="flex-1"
        />
        {streamState.connectionStatus !== 'idle' && (
          <div
            role="status"
            aria-live="polite"
            className={cn(
              'inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-[10px] font-medium transition-opacity',
              streamState.connectionStatus === 'connected' &&
                'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-300',
              streamState.connectionStatus === 'connecting' &&
                'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-300',
              streamState.connectionStatus === 'reconnecting' &&
                'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-300',
              streamState.connectionStatus === 'disconnected' &&
                'border-gray-200 bg-gray-50 text-gray-500 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-400',
              streamState.connectionStatus === 'error' &&
                'border-red-200 bg-red-50 text-red-700 dark:border-red-800 dark:bg-red-950 dark:text-red-300'
            )}
          >
            {streamState.connectionStatus === 'error' ||
            streamState.connectionStatus === 'disconnected' ? (
              <WifiOff className="h-2.5 w-2.5" />
            ) : (
              <Wifi
                className={cn(
                  'h-2.5 w-2.5',
                  (streamState.connectionStatus === 'connecting' ||
                    streamState.connectionStatus === 'reconnecting') &&
                    'animate-pulse'
                )}
              />
            )}
            <span>
              {streamState.connectionStatus === 'connected' && 'Connesso'}
              {streamState.connectionStatus === 'connecting' && 'Connessione...'}
              {streamState.connectionStatus === 'reconnecting' && 'Riconnessione...'}
              {streamState.connectionStatus === 'disconnected' && 'Offline'}
              {streamState.connectionStatus === 'error' && 'Errore connessione'}
            </span>
          </div>
        )}
      </div>

      {/* Agent switch confirmation dialog */}
      {showAgentConfirm && pendingAgent && (
        <AgentSwitchDialog
          pendingAgentName={AGENT_NAMES[pendingAgent]}
          onConfirm={() => void handleAgentChangeConfirm()}
          onCancel={handleAgentChangeCancel}
        />
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
          <div
            role="tablist"
            aria-label="Chat panels"
            className="flex items-center gap-1 px-4 pt-2 pb-0 border-b border-border/30"
          >
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
            <div
              id="panel-debug"
              role="tabpanel"
              className="flex-1 overflow-y-auto px-4 py-4 space-y-3"
              data-testid="debug-panel"
            >
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
              <ChatMessageList
                messages={messages}
                streamState={streamState}
                isEditor={isEditor}
                isAdmin={isAdmin}
                gameTitle={game?.title}
                isTtsSupported={isTtsSupported}
                ttsEnabled={voicePrefs.ttsEnabled}
                isSpeaking={isSpeaking}
                onSpeak={speak}
                onStopSpeaking={stopSpeaking}
                messagesEndRef={messagesEndRef}
              />
            </div>
          )}

          {/* Voice transcript overlay */}
          {voiceState !== 'idle' && (
            <VoiceTranscriptOverlay
              interimText={interimText}
              finalText={finalText}
              state={voiceState}
              onEdit={text => setInputValue(text)}
              onSend={() => {
                lastMessageWasVoiceRef.current = true;
                handleSendMessage(finalText);
              }}
              onCancel={cancelListening}
              autoSend={voicePrefs.autoSend}
            />
          )}

          {/* Input */}
          <ChatInputArea
            inputValue={inputValue}
            onInputChange={setInputValue}
            onSend={() => void handleSendMessage()}
            onKeyDown={handleKeyDown}
            isSending={isSending}
            isStreaming={streamState.isStreaming}
            voiceState={voiceState}
            onVoiceTap={handleVoiceTap}
            isVoiceSupported={isVoiceSupported}
            voicePrefs={voicePrefs}
            availableVoices={availableVoices}
          />
        </div>

        {/* Agent Settings Drawer */}
        {thread?.agentId && (
          <AgentSettingsDrawer
            isOpen={settingsOpen}
            onClose={() => setSettingsOpen(false)}
            agentId={thread.agentId}
            agentName={
              thread.agentTypology
                ? AGENT_NAMES[(thread.agentTypology as AgentType) ?? 'auto']
                : undefined
            }
            userTier={user ? 'premium' : 'free'}
          />
        )}

        {/* Info Panel (right) - Hidden on mobile */}
        <ChatInfoPanel
          game={game}
          citations={allCitations}
          suggestedQuestions={suggestedQuestions}
          onQuestionClick={handleQuestionClick}
        />
      </div>
    </div>
  );
}
