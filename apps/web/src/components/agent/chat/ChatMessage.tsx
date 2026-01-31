/**
 * ChatMessage - Enhanced agent chat message display (Issue #3243)
 *
 * Features:
 * - Type-based alignment (user: right/cyan, agent: left/dark)
 * - Message grouping (hide header for consecutive same-sender)
 * - Markdown rendering for agent messages
 * - Citation badges (clickable)
 * - Copy button per message (on hover)
 * - Relative timestamp display ("2m ago")
 */

import React, { useState } from 'react';

import { Copy, Check } from 'lucide-react';
import ReactMarkdown from 'react-markdown';

import type { Citation } from '@/lib/api/schemas/streaming.schemas';
import { cn } from '@/lib/utils';
import { formatRelativeTime } from '@/lib/utils/timeUtils';
import { AgentMessage as AgentMessageType } from '@/types/agent';

import { CitationBadge } from './CitationBadge';
import { ConfidenceBar } from './ConfidenceBar';

interface ChatMessageProps {
  message: AgentMessageType;
  /** Whether to hide header (for grouped messages) */
  isGrouped?: boolean;
}

export const ChatMessage = React.memo(function ChatMessage({
  message,
  isGrouped = false,
}: ChatMessageProps) {
  const [copied, setCopied] = useState(false);

  const isUser = message.type === 'user';
  const isSystem = message.type === 'system';
  const isAgent = message.type === 'agent';

  // Copy message to clipboard
  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(message.content);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (error) {
      console.error('Failed to copy message:', error);
    }
  };

  // Citation click handler (MVP: logs, future: PDF viewer)
  const handleCitationClick = (_citation: Citation) => {
    // TODO: Future integration with PDF viewer scroll
  };

  // System message (center-aligned)
  if (isSystem) {
    return (
      <div className="flex justify-center my-2">
        <div className="bg-gray-100 px-3 py-1.5 rounded text-xs text-gray-600">
          {message.content}
        </div>
      </div>
    );
  }

  // User or Agent message
  return (
    <div
      className={cn(
        'flex flex-col group',
        isUser ? 'items-end' : 'items-start',
        isGrouped ? 'mt-1' : 'mt-4'
      )}
      aria-label={isUser ? 'Your message' : 'Agent response'}
    >
      {/* Message Bubble */}
      <div
        className={cn(
          'max-w-[85%] p-3 rounded-lg text-sm leading-relaxed relative',
          isUser
            ? 'bg-cyan-50 text-slate-900' // User: cyan background (right-aligned)
            : 'bg-gray-800 text-gray-100' // Agent: dark background (left-aligned)
        )}
      >
        {/* Header: Role + Timestamp (hidden when grouped) */}
        {!isGrouped && (
          <div className="flex items-center justify-between mb-1.5 gap-2">
            <span
              className={cn(
                'font-medium text-xs',
                isUser ? 'text-cyan-700' : 'text-gray-400'
              )}
            >
              {isUser ? 'Tu' : 'Agent'}
            </span>
            <span
              className={cn('text-[10px]', isUser ? 'text-cyan-600' : 'text-gray-500')}
              title={message.timestamp.toLocaleString('it-IT')}
            >
              {formatRelativeTime(message.timestamp)}
            </span>
          </div>
        )}

        {/* Content */}
        <div className="text-sm">
          {isAgent ? (
            <ReactMarkdown
              className={cn('prose prose-sm max-w-none', 'prose-invert')}
              components={{
                p: ({ children }: { children?: React.ReactNode }) => (
                  <p className="mb-2 last:mb-0 text-gray-100">{children}</p>
                ),
                ul: ({ children }: { children?: React.ReactNode }) => (
                  <ul className="list-disc pl-4 mb-2 text-gray-100">{children}</ul>
                ),
                ol: ({ children }: { children?: React.ReactNode }) => (
                  <ol className="list-decimal pl-4 mb-2 text-gray-100">{children}</ol>
                ),
                code: ({
                  children,
                  className,
                }: {
                  children?: React.ReactNode;
                  className?: string;
                }) => {
                  const isInline = !className;
                  return isInline ? (
                    <code className="bg-gray-700 text-cyan-300 px-1 py-0.5 rounded text-xs font-mono">
                      {children}
                    </code>
                  ) : (
                    <code className="block bg-slate-900 text-gray-100 p-2 rounded text-xs font-mono overflow-x-auto">
                      {children}
                    </code>
                  );
                },
              }}
            >
              {message.content}
            </ReactMarkdown>
          ) : (
            <p className="whitespace-pre-wrap">{message.content}</p>
          )}
        </div>

        {/* Confidence Bar (agent only) - Issue #3244 */}
        {isAgent && message.confidence !== undefined && (
          <div className="mt-3">
            <ConfidenceBar confidence={message.confidence} />
          </div>
        )}

        {/* Citations (agent only) - Issue #3244 */}
        {isAgent && message.citations && message.citations.length > 0 && (
          <div className="mt-3 pt-3 border-t border-gray-700">
            <div className="text-xs text-gray-400 mb-1.5">Fonti:</div>
            <div className="flex flex-wrap gap-1.5">
              {message.citations.map((citation, index) => (
                <CitationBadge
                  key={index}
                  citation={citation}
                  onClick={handleCitationClick}
                />
              ))}
            </div>
          </div>
        )}

        {/* Copy Button (on hover) */}
        <button
          onClick={handleCopy}
          className={cn(
            'absolute -top-2 -right-2 opacity-0 group-hover:opacity-100',
            'p-1.5 bg-white border border-gray-300 rounded shadow-sm',
            'hover:bg-gray-50 transition-opacity',
            'dark:bg-gray-700 dark:border-gray-600 dark:hover:bg-gray-600'
          )}
          aria-label="Copy message"
          title="Copy message"
        >
          {copied ? (
            <Check className="h-3 w-3 text-green-600" />
          ) : (
            <Copy className="h-3 w-3 text-gray-600 dark:text-gray-300" />
          )}
        </button>
      </div>
    </div>
  );
});
