/**
 * EmbeddedChatView - Compact chat for ExtraMeepleCardDrawer (liveChat mode)
 *
 * Minimal version of ChatThreadView: messages + input only.
 * No side panel, no debug, no voice, no thread header.
 */

'use client';

import React, { useState, useCallback, useRef, useEffect } from 'react';

import { useAgentChatStream } from '@/hooks/useAgentChatStream';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp?: string;
}

export interface EmbeddedChatViewProps {
  /** Chat thread ID */
  threadId: string;
  /** Agent ID for SSE streaming */
  agentId: string;
  /** Game ID for context */
  gameId: string;
  /** Game name for ProxyGameContext */
  gameName?: string;
}

// ============================================================================
// Component
// ============================================================================

export function EmbeddedChatView({ threadId, agentId, gameId, gameName }: EmbeddedChatViewProps) {
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputValue, setInputValue] = useState('');

  // SSE streaming
  const {
    state: streamState,
    sendMessage: sendViaSSE,
    stopStreaming,
  } = useAgentChatStream({
    onComplete: (answer, metadata) => {
      const assistantMessage: ChatMessage = {
        id: `assistant-${crypto.randomUUID()}`,
        role: 'assistant',
        content: answer,
        timestamp: new Date().toISOString(),
      };
      setMessages(prev => [...prev, assistantMessage]);
    },
  });

  // Auto-scroll on new messages or streaming content
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, streamState.currentAnswer]);

  // Stop SSE streaming on unmount
  useEffect(() => {
    return () => {
      stopStreaming();
    };
  }, [stopStreaming]);

  // Send message handler
  const handleSend = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      const content = inputValue.trim();
      if (!content) return;

      // Optimistic UI: add user message
      const userMessage: ChatMessage = {
        id: `user-${crypto.randomUUID()}`,
        role: 'user',
        content,
        timestamp: new Date().toISOString(),
      };
      setMessages(prev => [...prev, userMessage]);
      setInputValue('');

      // Send via SSE
      sendViaSSE(
        agentId,
        content,
        threadId,
        { gameName: gameName ?? '', agentTypology: '' },
        undefined
      );
    },
    [inputValue, agentId, threadId, gameName, sendViaSSE]
  );

  return (
    <div className="flex flex-col h-full" data-testid="embedded-chat-view">
      {/* Error banner */}
      {streamState.error && (
        <div
          role="alert"
          className="mx-3 mt-2 p-2 bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-400 rounded-lg text-xs border border-red-200 dark:border-red-500/20"
        >
          {streamState.error}
        </div>
      )}

      {/* Messages area */}
      <div className="flex-1 overflow-y-auto p-4 space-y-3">
        {/* Empty state */}
        {messages.length === 0 && !streamState.isStreaming && (
          <div className="flex items-center justify-center h-full">
            <p className="text-sm text-muted-foreground font-nunito">
              Chiedi qualsiasi cosa sul gioco!
            </p>
          </div>
        )}

        {/* Message bubbles */}
        {messages.map(msg => (
          <div
            key={msg.id}
            className={cn(
              'max-w-[90%] rounded-2xl px-3 py-2',
              msg.role === 'user'
                ? 'ml-auto bg-amber-500 text-white'
                : 'mr-auto bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50'
            )}
            data-testid={`message-${msg.role}`}
          >
            <p className="text-sm whitespace-pre-wrap font-nunito">{msg.content}</p>
          </div>
        ))}

        {/* Streaming status */}
        {streamState.statusMessage && (
          <div
            className="flex items-center gap-2 text-xs text-muted-foreground font-nunito"
            data-testid="stream-status"
          >
            <div className="h-3 w-3 border-2 border-amber-500/30 border-t-amber-500 rounded-full animate-spin" />
            {streamState.statusMessage}
          </div>
        )}

        {/* Streaming response bubble */}
        {streamState.isStreaming && streamState.currentAnswer && (
          <div
            className="max-w-[90%] mr-auto rounded-2xl px-3 py-2 bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50"
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

      {/* Input area */}
      <form onSubmit={handleSend} className="p-3 border-t border-border">
        <div className="flex items-center gap-2">
          <input
            type="text"
            value={inputValue}
            onChange={e => setInputValue(e.target.value)}
            placeholder="Scrivi la tua domanda..."
            disabled={streamState.isStreaming}
            className={cn(
              'flex-1 rounded-xl border border-border/50 px-3 py-2',
              'bg-white/70 dark:bg-card/70 backdrop-blur-md text-sm font-nunito',
              'placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-500/40',
              'disabled:opacity-50 disabled:cursor-not-allowed'
            )}
            data-testid="embedded-chat-input"
          />
          <button
            type="submit"
            disabled={!inputValue.trim() || streamState.isStreaming}
            className={cn(
              'p-2 rounded-xl transition-all duration-200 flex-shrink-0',
              inputValue.trim() && !streamState.isStreaming
                ? 'bg-amber-500 hover:bg-amber-600 text-white cursor-pointer'
                : 'bg-muted text-muted-foreground cursor-not-allowed'
            )}
            aria-label="Invia messaggio"
            data-testid="embedded-send-btn"
          >
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8"
              />
            </svg>
          </button>
        </div>
      </form>
    </div>
  );
}
