'use client';

/**
 * SessionChatWidget — Collapsible inline chat for rule questions during a live session.
 *
 * Provides a minimal chat interface embedded in the play view. Expands on tap
 * and collapses to a single-line summary. Uses a simple message list with an
 * input form (not the full ChatThreadView to keep the play page lightweight).
 *
 * Issue #5587 — Live Game Session UI
 */

import { useState, useRef, useEffect, useCallback, type FormEvent } from 'react';

import { ChevronDown, ChevronUp, MessageCircle, Send, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { cn } from '@/lib/utils';

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

export interface SessionChatWidgetProps {
  /** Messages to display */
  messages: ChatMessage[];
  /** Whether the AI is currently responding */
  isStreaming?: boolean;
  /** Called when user sends a message */
  onSend: (message: string) => void;
  /** Default collapsed state */
  defaultExpanded?: boolean;
  className?: string;
}

export function SessionChatWidget({
  messages,
  isStreaming = false,
  onSend,
  defaultExpanded = false,
  className,
}: SessionChatWidgetProps) {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const [input, setInput] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Auto-scroll to bottom on new messages
  useEffect(() => {
    if (expanded) {
      messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages.length, expanded]);

  // Focus input when expanded
  useEffect(() => {
    if (expanded) {
      inputRef.current?.focus();
    }
  }, [expanded]);

  const handleSubmit = useCallback(
    (e: FormEvent) => {
      e.preventDefault();
      const trimmed = input.trim();
      if (!trimmed || isStreaming) return;
      onSend(trimmed);
      setInput('');
    },
    [input, isStreaming, onSend]
  );

  const lastAssistantMsg = [...messages].reverse().find(m => m.role === 'assistant');

  return (
    <section
      className={cn(
        'rounded-xl border border-slate-200 dark:border-slate-700 overflow-hidden',
        'bg-white/80 dark:bg-slate-800/60 backdrop-blur-sm',
        className
      )}
      data-testid="session-chat-widget"
    >
      {/* Collapse / Expand header */}
      <button
        type="button"
        onClick={() => setExpanded(v => !v)}
        className={cn(
          'w-full flex items-center gap-2 px-3 py-2.5 text-left',
          'hover:bg-slate-50 dark:hover:bg-slate-700/40 transition-colors'
        )}
        aria-expanded={expanded}
        data-testid="chat-toggle"
      >
        <MessageCircle
          className="h-4 w-4 text-amber-600 dark:text-amber-400 flex-shrink-0"
          aria-hidden="true"
        />
        <span className="text-sm font-semibold text-slate-900 dark:text-slate-100 flex-1 truncate">
          Chat regole
        </span>

        {!expanded && lastAssistantMsg && (
          <span className="text-xs text-slate-500 dark:text-slate-400 truncate max-w-[50%]">
            {lastAssistantMsg.content.slice(0, 60)}
            {lastAssistantMsg.content.length > 60 ? '...' : ''}
          </span>
        )}

        {expanded ? (
          <ChevronUp className="h-4 w-4 text-slate-400 flex-shrink-0" aria-hidden="true" />
        ) : (
          <ChevronDown className="h-4 w-4 text-slate-400 flex-shrink-0" aria-hidden="true" />
        )}
      </button>

      {/* Expanded body */}
      {expanded && (
        <div className="border-t border-slate-200 dark:border-slate-700">
          {/* Messages list */}
          <div className="max-h-60 overflow-y-auto px-3 py-2 space-y-2" data-testid="chat-messages">
            {messages.length === 0 && (
              <p className="text-xs text-slate-400 dark:text-slate-500 text-center py-4">
                Chiedi una regola per iniziare...
              </p>
            )}

            {messages.map(msg => (
              <div
                key={msg.id}
                className={cn(
                  'text-sm rounded-lg px-3 py-2 max-w-[85%]',
                  msg.role === 'user'
                    ? 'ml-auto bg-amber-600 text-white'
                    : 'mr-auto bg-slate-100 dark:bg-slate-700 text-slate-900 dark:text-slate-100'
                )}
                data-testid={`chat-message-${msg.role}`}
              >
                {msg.content}
              </div>
            ))}

            {isStreaming && (
              <div className="flex items-center gap-1.5 text-xs text-slate-400 mr-auto">
                <Loader2 className="h-3 w-3 animate-spin" aria-hidden="true" />
                <span>Sta rispondendo...</span>
              </div>
            )}

            <div ref={messagesEndRef} />
          </div>

          {/* Input form */}
          <form
            onSubmit={handleSubmit}
            className="flex items-center gap-2 border-t border-slate-200 dark:border-slate-700 px-3 py-2"
          >
            <Input
              ref={inputRef}
              value={input}
              onChange={e => setInput(e.target.value)}
              placeholder="Chiedi una regola..."
              disabled={isStreaming}
              className="flex-1 h-9 text-sm"
              data-testid="chat-input"
            />
            <Button
              type="submit"
              size="icon"
              disabled={isStreaming || !input.trim()}
              className={cn(
                'h-9 w-9 flex-shrink-0',
                'bg-amber-600 hover:bg-amber-700 text-white',
                'active:scale-95 transition-all'
              )}
              data-testid="chat-send"
            >
              <Send className="h-4 w-4" aria-hidden="true" />
              <span className="sr-only">Invia</span>
            </Button>
          </form>
        </div>
      )}
    </section>
  );
}
