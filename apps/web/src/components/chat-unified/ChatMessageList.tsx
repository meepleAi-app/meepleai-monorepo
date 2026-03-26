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
 */

'use client';

import React from 'react';

import type { AgentChatStreamState } from '@/hooks/useAgentChatStream';
import { cn } from '@/lib/utils';

import { ResponseMetaBadge } from './ResponseMetaBadge';
import { RuleSourceCard } from './RuleSourceCard';
import { TechnicalDetailsPanel } from './TechnicalDetailsPanel';
import { TtsSpeakerButton } from './TtsSpeakerButton';

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
  /** TTS state */
  isTtsSupported: boolean;
  ttsEnabled: boolean;
  isSpeaking: boolean;
  onSpeak: (text: string) => void;
  onStopSpeaking: () => void;
  /** Ref for auto-scroll */
  messagesEndRef: React.RefObject<HTMLDivElement | null>;
}

// ============================================================================
// Component
// ============================================================================

export function ChatMessageList({
  messages,
  streamState,
  isEditor,
  isAdmin,
  gameTitle,
  isTtsSupported,
  ttsEnabled,
  isSpeaking,
  onSpeak,
  onStopSpeaking,
  messagesEndRef,
}: ChatMessageListProps) {
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
        messages.map((msg, msgIndex) => {
          // Show strategy badge on the last assistant message when available
          const isLastAssistant =
            msg.role === 'assistant' &&
            !streamState.isStreaming &&
            msgIndex === messages.length - 1;

          return (
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
              {msg.role === 'assistant' && isTtsSupported && ttsEnabled && (
                <TtsSpeakerButton
                  text={msg.content}
                  isSpeaking={isSpeaking}
                  onSpeak={onSpeak}
                  onStop={onStopSpeaking}
                />
              )}
              {msg.citations && msg.citations.length > 0 && (
                <RuleSourceCard citations={msg.citations} gameTitle={gameTitle} />
              )}
              {isLastAssistant && streamState.strategyTier && (
                <div className="mt-2">
                  <ResponseMetaBadge strategyTier={streamState.strategyTier} />
                </div>
              )}
              {isLastAssistant && isEditor && streamState.debugSteps.length > 0 && (
                <TechnicalDetailsPanel
                  debugSteps={streamState.debugSteps}
                  executionId={streamState.executionId}
                  showDebugLink={isAdmin}
                />
              )}
            </div>
          );
        })
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
