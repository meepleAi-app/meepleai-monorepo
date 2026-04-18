/**
 * ChatMessageList - Message rendering for ChatThreadView
 *
 * Renders the list of chat messages including:
 * - User and assistant message bubbles
 * - TTS speaker buttons on assistant messages
 * - Rule source cards (citations)
 * - Response meta badge (strategy tier)
 * - Technical details panel (debug info for editors)
 * - Streaming status, model downgrade banner, streaming bubble
 * - KB-07: Feedback buttons wired to POST /games/{id}/knowledge-base/feedback
 */

'use client';

import React, { useCallback, useState } from 'react';

import { ChatMessage } from '@/components/ui/meeple/chat-message';
import type { FeedbackValue } from '@/components/ui/meeple/feedback-buttons';
import type { AgentChatStreamState } from '@/hooks/useAgentChatStream';
import { api } from '@/lib/api';

import { CitationBlock } from './CitationBlock';
import { ContinueButton } from './ContinueButton';
import { InlineCitationText } from './InlineCitationText';
import { ResponseMetaBadge } from './ResponseMetaBadge';
import { RuleSourceCard } from './RuleSourceCard';
import { TechnicalDetailsPanel } from './TechnicalDetailsPanel';
import { TtsSpeakerButton } from './TtsSpeakerButton';
import { isLastAssistantMessage } from './utils/isLastAssistantMessage';
import { toChatMessageProps } from './utils/toChatMessageProps';

// ============================================================================
// Types
// ============================================================================

export interface ChatMessageItem {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp?: string;
  citations?: import('@/types').Citation[];
  followUpQuestions?: string[];
  inlineCitations?: import('@/lib/api/clients/chatClient').InlineCitationMatch[];
  snippets?: Array<{ text: string; source: string; page: number; line: number; score: number }>;
  continuationToken?: string;
}

export type StreamStateForMessages = Pick<
  AgentChatStreamState,
  | 'isStreaming'
  | 'currentAnswer'
  | 'statusMessage'
  | 'strategyTier'
  | 'executionId'
  | 'debugSteps'
  | 'modelDowngrade'
>;

export interface ChatMessageListProps {
  messages: ChatMessageItem[];
  streamState: StreamStateForMessages;
  isEditor: boolean;
  isAdmin: boolean;
  gameTitle?: string;
  /** KB-07: Game ID used to submit feedback — pass null/undefined to hide feedback buttons */
  gameId?: string | null;
  /** KB-07: Thread ID used as chatSessionId in feedback payload */
  threadId?: string;
  /** TTS state */
  isTtsSupported: boolean;
  ttsEnabled: boolean;
  isSpeaking: boolean;
  onSpeak: (text: string) => void;
  onStopSpeaking: () => void;
  /** Continuation handler for "continue" button */
  onContinue?: (continuationToken: string) => void;
  /** Whether a send/continuation is in progress */
  isSending?: boolean;
  /** Ref for auto-scroll */
  messagesEndRef: React.RefObject<HTMLDivElement | null>;
}

// ============================================================================
// Constants
// ============================================================================

/** Windowed slice: max DOM nodes rendered at once for performance */
const WINDOW_SIZE = 50;

// ============================================================================
// Component
// ============================================================================

export function ChatMessageList({
  messages,
  streamState,
  isEditor,
  isAdmin,
  gameTitle,
  gameId,
  threadId,
  isTtsSupported,
  ttsEnabled,
  isSpeaking,
  onContinue,
  isSending,
  onSpeak,
  onStopSpeaking,
  messagesEndRef,
}: ChatMessageListProps) {
  const [windowStart, setWindowStart] = React.useState(() =>
    Math.max(0, messages.length - WINDOW_SIZE)
  );

  // KB-07: Per-message feedback state (messageId → FeedbackValue)
  const [feedbackMap, setFeedbackMap] = useState<Map<string, FeedbackValue>>(new Map());
  const [feedbackLoadingMap, setFeedbackLoadingMap] = useState<Map<string, boolean>>(new Map());

  // KB-07: Submit feedback handler — wires FeedbackButtons to POST /games/{id}/knowledge-base/feedback
  const handleFeedback = useCallback(
    async (messageId: string, feedbackValue: FeedbackValue, comment?: string) => {
      if (!gameId || !threadId) return;

      setFeedbackLoadingMap(prev => new Map(prev).set(messageId, true));
      try {
        if (feedbackValue !== null) {
          // FeedbackValue uses 'not-helpful'; API requires 'not_helpful'
          const outcome = feedbackValue === 'not-helpful' ? 'not_helpful' : 'helpful';
          await api.knowledgeBase.submitKbFeedback(gameId, {
            chatSessionId: threadId,
            messageId,
            outcome,
            comment,
          });
        }
        setFeedbackMap(prev => new Map(prev).set(messageId, feedbackValue));
      } catch {
        // Silent fail: feedback is non-critical, do not surface errors to user
      } finally {
        setFeedbackLoadingMap(prev => new Map(prev).set(messageId, false));
      }
    },
    [gameId, threadId]
  );

  // Advance window when new messages arrive so the latest stays visible
  React.useEffect(() => {
    setWindowStart(Math.max(0, messages.length - WINDOW_SIZE));
  }, [messages.length]);

  const visibleMessages = messages.slice(windowStart);
  const hiddenCount = windowStart;

  return (
    <>
      {messages.length === 0 ? (
        <div className="flex items-center justify-center h-full">
          <div className="text-center text-muted-foreground">
            <p className="text-lg font-quicksand mb-2">Inizia la conversazione</p>
            <p className="text-sm font-nunito">Scrivi un messaggio per cominciare.</p>
          </div>
        </div>
      ) : (
        <>
          {hiddenCount > 0 && (
            <div className="flex justify-center mb-2">
              <button
                type="button"
                className="text-xs text-muted-foreground hover:text-foreground underline underline-offset-2 transition-colors"
                onClick={() => setWindowStart(prev => Math.max(0, prev - WINDOW_SIZE))}
              >
                {hiddenCount} messaggi precedenti
              </button>
            </div>
          )}

          {visibleMessages.map((msg, visibleIndex) => {
            const msgIndex = windowStart + visibleIndex;
            // Show strategy badge on the last assistant message when available.
            // isLastAssistantMessage checks list structure only; we combine
            // with !streamState.isStreaming to gate on the final response.
            const isLastAssistant =
              !streamState.isStreaming && isLastAssistantMessage(messages, msgIndex);
            const showFeedback = msg.role === 'assistant' && !!gameId && !!threadId;

            return (
              <div
                key={msg.id}
                data-testid={`message-${msg.role}`}
                className={msg.role === 'user' ? 'ml-auto' : 'mr-auto'}
              >
                {/* Core message bubble (role/content/timestamp/avatar/feedback) */}
                <ChatMessage
                  {...toChatMessageProps(msg, {
                    feedback: feedbackMap.get(msg.id) ?? null,
                    isFeedbackLoading: feedbackLoadingMap.get(msg.id) ?? false,
                    showFeedback,
                    onFeedbackChange: async (value, comment) => {
                      await handleFeedback(msg.id, value, comment);
                    },
                  })}
                />

                {/* TTS speaker button — kept outside <ChatMessage> (atom doesn't own TTS wiring) */}
                {msg.role === 'assistant' && isTtsSupported && ttsEnabled && (
                  <TtsSpeakerButton
                    text={msg.content}
                    isSpeaking={isSpeaking}
                    onSpeak={onSpeak}
                    onStop={onStopSpeaking}
                  />
                )}

                {/* Citations — kept outside <ChatMessage> to preserve RuleSourceCard
                    (RAG copyright-tier handling; @/types.Citation is incompatible with
                    ChatMessage's local Citation type) */}
                {msg.citations && msg.citations.length > 0 && (
                  <RuleSourceCard citations={msg.citations} gameTitle={gameTitle} />
                )}

                {/* Inline citation text overlay — replaces plain text when inline citations exist */}
                {msg.role === 'assistant' && msg.inlineCitations && msg.inlineCitations.length > 0 && (
                  <div className="mt-1">
                    <InlineCitationText
                      text={msg.content}
                      citations={msg.inlineCitations}
                      snippets={msg.snippets ?? []}
                    />
                  </div>
                )}

                {/* Citation block for non-inline snippets */}
                {msg.role === 'assistant' && msg.snippets && msg.snippets.length > 0 && (
                  <CitationBlock
                    snippets={msg.snippets}
                    excludeIndices={new Set(msg.inlineCitations?.map(c => c.snippetIndex) ?? [])}
                  />
                )}

                {/* Continue button — when backend signals truncated response */}
                {msg.role === 'assistant' && msg.continuationToken && onContinue && (
                  <ContinueButton
                    onContinue={() => onContinue(msg.continuationToken!)}
                    isLoading={isSending ?? false}
                  />
                )}

                {/* Strategy tier badge — only on last assistant message, not streaming */}
                {isLastAssistant && streamState.strategyTier && (
                  <div className="mt-2">
                    <ResponseMetaBadge strategyTier={streamState.strategyTier} />
                  </div>
                )}

                {/* Technical details panel — only on last assistant when editor + debugSteps */}
                {isLastAssistant && isEditor && streamState.debugSteps.length > 0 && (
                  <TechnicalDetailsPanel
                    debugSteps={streamState.debugSteps}
                    executionId={streamState.executionId}
                    showDebugLink={isAdmin}
                  />
                )}
              </div>
            );
          })}
        </>
      )}

      {/* Streaming status message */}
      {streamState.statusMessage && (
        <div
          className="flex items-center gap-2 text-xs text-muted-foreground font-nunito"
          data-testid="stream-status"
          role="status"
          aria-live="polite"
          aria-label={streamState.statusMessage || undefined}
        >
          <div
            className="h-3 w-3 border-2 border-amber-500/30 border-t-amber-500 rounded-full animate-spin"
            aria-hidden="true"
          />
          {streamState.statusMessage}
        </div>
      )}

      {/* Model downgrade banner */}
      {streamState.modelDowngrade && (
        <div
          className="mx-0 mb-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-200"
          data-testid="model-downgrade-banner"
          role="alert"
          aria-live="assertive"
        >
          <div className="flex items-center gap-2">
            <svg
              className="h-4 w-4 shrink-0"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth={2}
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
              />
            </svg>
            <span>
              {streamState.modelDowngrade.isLocalFallback
                ? `Modello ${streamState.modelDowngrade.originalModel} non disponibile. Risposta generata con modello locale (${streamState.modelDowngrade.fallbackModel}).`
                : `Modello cambiato: ${streamState.modelDowngrade.originalModel} → ${streamState.modelDowngrade.fallbackModel}`}
            </span>
          </div>
          {streamState.modelDowngrade.upgradeMessage && (
            <div className="mt-1.5 flex items-center gap-2">
              <a
                href="/pricing"
                className="font-medium text-amber-900 underline dark:text-amber-100"
              >
                Passa a Premium
              </a>
              <span className="text-amber-600 dark:text-amber-400">
                per modelli più veloci e affidabili
              </span>
            </div>
          )}
        </div>
      )}

      {/* Streaming response bubble */}
      {streamState.isStreaming && streamState.currentAnswer && (
        <div
          className="max-w-[85%] mr-auto rounded-2xl px-4 py-3 bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50"
          data-testid="message-streaming"
          role="status"
          aria-live="polite"
          aria-label="Risposta dell'assistente in scrittura"
        >
          <p className="text-sm whitespace-pre-wrap font-nunito">{streamState.currentAnswer}</p>
          <div className="mt-1 flex items-center gap-1">
            <div className="h-1.5 w-1.5 rounded-full bg-amber-500 animate-pulse" />
            <span className="text-[10px] text-muted-foreground" aria-hidden="true">
              In scrittura...
            </span>
          </div>
        </div>
      )}

      <div ref={messagesEndRef} />
    </>
  );
}
