'use client';

/**
 * AITab - Chat interface with RAG agent for ExtraMeepleCard
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 */

import React, { useState, useRef, useEffect } from 'react';

import { Bot, Send, Sparkles, BookOpen, Loader2, Quote } from 'lucide-react';

import { cn } from '@/lib/utils';

import type { AITabData, AIChatMessage, AIQuickAction, AISource } from '../types';

interface AITabProps {
  data?: AITabData;
  onSendMessage?: (message: string) => void;
}

/** Chat message bubble */
function ChatBubble({ message }: { message: AIChatMessage }) {
  const isUser = message.role === 'user';

  return (
    <div
      className={cn('flex gap-2', isUser ? 'flex-row-reverse' : 'flex-row')}
      data-testid={`ai-message-${message.id}`}
    >
      {/* Avatar */}
      <div
        className={cn(
          'flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full',
          isUser ? 'bg-indigo-100' : 'bg-amber-100'
        )}
      >
        {isUser ? (
          <span className="text-xs font-bold text-indigo-600">U</span>
        ) : (
          <Bot className="h-3.5 w-3.5 text-amber-600" />
        )}
      </div>

      {/* Message content */}
      <div
        className={cn(
          'max-w-[80%] rounded-xl px-3 py-2',
          isUser
            ? 'bg-indigo-50 text-slate-800'
            : 'bg-white/80 border border-slate-200/60 text-slate-700'
        )}
      >
        <p className="font-nunito text-xs leading-relaxed whitespace-pre-wrap">{message.content}</p>

        {/* Source citations */}
        {message.sources && message.sources.length > 0 && (
          <div className="mt-2 space-y-1 border-t border-slate-200/40 pt-1.5">
            {message.sources.map(source => (
              <SourceCitation key={source.title} source={source} />
            ))}
          </div>
        )}

        <span className="mt-1 block text-[10px] text-slate-400 font-nunito">
          {new Date(message.timestamp).toLocaleTimeString([], {
            hour: '2-digit',
            minute: '2-digit',
          })}
        </span>
      </div>
    </div>
  );
}

/** Source citation badge */
function SourceCitation({ source }: { source: AISource }) {
  return (
    <div className="flex items-start gap-1.5 rounded bg-slate-50 px-2 py-1">
      <Quote className="h-3 w-3 flex-shrink-0 text-amber-500 mt-0.5" />
      <div>
        <p className="font-nunito text-[10px] font-semibold text-slate-600">{source.title}</p>
        <p className="font-nunito text-[10px] text-slate-400 line-clamp-1">{source.snippet}</p>
      </div>
    </div>
  );
}

/** Quick action button */
function QuickActionButton({ action, onClick }: { action: AIQuickAction; onClick: () => void }) {
  const Icon = action.icon || Sparkles;

  return (
    <button
      onClick={onClick}
      className={cn(
        'flex items-center gap-1.5 rounded-full border border-amber-200/60',
        'bg-amber-50/50 px-3 py-1.5',
        'font-nunito text-xs text-amber-800',
        'hover:bg-amber-100/60 transition-colors'
      )}
      data-testid={`ai-quick-action`}
    >
      <Icon className="h-3 w-3" />
      <span>{action.label}</span>
    </button>
  );
}

export function AITab({ data, onSendMessage }: AITabProps) {
  const [inputValue, setInputValue] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Auto-scroll on new messages
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [data?.messages?.length]);

  const handleSend = () => {
    const trimmed = inputValue.trim();
    if (!trimmed || !onSendMessage) return;
    onSendMessage(trimmed);
    setInputValue('');
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  if (!data) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-slate-400">
        <Bot className="h-10 w-10 mb-2 opacity-50" />
        <p className="font-nunito text-sm">AI Assistant</p>
        <p className="font-nunito text-xs mt-1">Connect a RAG agent to get started</p>
      </div>
    );
  }

  const hasMessages = data.messages.length > 0;

  return (
    <div className="flex flex-col h-[520px]" data-testid="ai-tab">
      {/* Agent context header */}
      {(data.agentName || data.sessionContext) && (
        <div className="flex items-center gap-2 rounded-lg bg-amber-50/50 border border-amber-200/40 px-3 py-2 mb-2">
          <BookOpen className="h-3.5 w-3.5 text-amber-600 flex-shrink-0" />
          <div className="min-w-0">
            {data.agentName && (
              <p className="font-nunito text-xs font-semibold text-amber-800">{data.agentName}</p>
            )}
            {data.sessionContext && (
              <p className="font-nunito text-[10px] text-amber-600 truncate">
                {data.sessionContext}
              </p>
            )}
          </div>
        </div>
      )}

      {/* Chat messages */}
      <div className="flex-1 overflow-y-auto space-y-3 pr-1">
        {!hasMessages && (
          <div className="flex flex-col items-center justify-center py-8 text-slate-400">
            <Sparkles className="h-8 w-8 mb-2 text-amber-300" />
            <p className="font-nunito text-xs">Ask anything about this game session</p>
          </div>
        )}

        {data.messages.map(msg => (
          <ChatBubble key={msg.id} message={msg} />
        ))}

        {data.isLoading && (
          <div className="flex items-center gap-2 text-slate-400" data-testid="ai-loading">
            <div className="flex h-7 w-7 items-center justify-center rounded-full bg-amber-100">
              <Loader2 className="h-3.5 w-3.5 animate-spin text-amber-600" />
            </div>
            <span className="font-nunito text-xs">Thinking...</span>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Quick actions */}
      {data.quickActions.length > 0 && !hasMessages && (
        <div className="flex flex-wrap gap-1.5 py-2" data-testid="ai-quick-actions">
          {data.quickActions.map(action => (
            <QuickActionButton
              key={action.label}
              action={action}
              onClick={() => onSendMessage?.(action.prompt)}
            />
          ))}
        </div>
      )}

      {/* Input area */}
      <div className="flex items-end gap-2 border-t border-slate-200/40 pt-2 mt-auto">
        <textarea
          value={inputValue}
          onChange={e => setInputValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Ask about rules, scores, strategy..."
          rows={1}
          className={cn(
            'flex-1 resize-none rounded-lg border border-slate-200/60',
            'bg-white/60 px-3 py-2 font-nunito text-xs text-slate-700',
            'placeholder:text-slate-400',
            'focus:outline-none focus:ring-1 focus:ring-indigo-300'
          )}
          data-testid="ai-input"
        />
        <button
          onClick={handleSend}
          disabled={!inputValue.trim() || !!data.isLoading || !onSendMessage}
          className={cn(
            'flex h-8 w-8 items-center justify-center rounded-lg',
            'bg-indigo-500 text-white',
            'hover:bg-indigo-600 transition-colors',
            'disabled:opacity-40 disabled:cursor-not-allowed'
          )}
          aria-label="Send message"
          data-testid="ai-send-button"
        >
          <Send className="h-3.5 w-3.5" aria-hidden="true" />
        </button>
      </div>
    </div>
  );
}
