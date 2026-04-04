/**
 * AgentMessage - Agent chat message display (Issue #3187)
 *
 * Displays individual agent chat messages with:
 * - Type-based alignment (user: right, agent: left, system: center)
 * - Markdown rendering for agent messages
 * - Citation badges (clickable)
 * - Copy button per message
 * - Timestamp display
 */

import React, { useState } from 'react';

import { Copy, Check } from 'lucide-react';
import ReactMarkdown from 'react-markdown';

import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';
import { AgentMessage as AgentMessageType } from '@/types/agent';

interface AgentMessageProps {
  message: AgentMessageType;
}

export const AgentMessage = React.memo(function AgentMessage({ message }: AgentMessageProps) {
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
      logger.error('Failed to copy message:', error);
    }
  };

  // Format timestamp
  const formatTime = (date: Date) => {
    return new Intl.DateTimeFormat('it-IT', {
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  };

  // System message (center-aligned)
  if (isSystem) {
    return (
      <div className="flex justify-center my-2">
        <div className="bg-[#f1f3f4] px-3 py-1.5 rounded text-xs text-[#5f6368]">
          {message.content}
        </div>
      </div>
    );
  }

  // User or Agent message
  return (
    <div
      className={cn('flex flex-col group', isUser ? 'items-end' : 'items-start')}
      aria-label={isUser ? 'Your message' : 'Agent response'}
    >
      {/* Message Bubble */}
      <div
        className={cn(
          'max-w-[85%] p-3 rounded-lg text-sm leading-relaxed relative',
          isUser ? 'bg-[#e3f2fd] text-[#1e293b]' : 'bg-[#f1f3f4] text-[#334155]'
        )}
      >
        {/* Header: Role + Timestamp */}
        <div className="flex items-center justify-between mb-1.5 gap-2">
          <span className="font-medium text-xs text-[#64748b]">{isUser ? 'Tu' : 'Agent'}</span>
          <span className="text-[10px] text-[#94a3b8]">{formatTime(message.timestamp)}</span>
        </div>

        {/* Content */}
        <div className="text-sm">
          {isAgent ? (
            <div className="prose prose-sm max-w-none">
              <ReactMarkdown
                components={{
                  p: ({ children }: { children?: React.ReactNode }) => (
                    <p className="mb-2 last:mb-0">{children}</p>
                  ),
                  ul: ({ children }: { children?: React.ReactNode }) => (
                    <ul className="list-disc pl-4 mb-2">{children}</ul>
                  ),
                  ol: ({ children }: { children?: React.ReactNode }) => (
                    <ol className="list-decimal pl-4 mb-2">{children}</ol>
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
                      <code className="bg-[#e5e7eb] px-1 py-0.5 rounded text-xs font-mono">
                        {children}
                      </code>
                    ) : (
                      <code className="block bg-[#1e293b] text-[#e2e8f0] p-2 rounded text-xs font-mono overflow-x-auto">
                        {children}
                      </code>
                    );
                  },
                }}
              >
                {message.content}
              </ReactMarkdown>
            </div>
          ) : (
            <p className="whitespace-pre-wrap">{message.content}</p>
          )}
        </div>

        {/* Citations */}
        {isAgent && message.citations && message.citations.length > 0 && (
          <div className="mt-3 pt-3 border-t border-[#dadce0]">
            <div className="text-xs text-[#5f6368] mb-1.5">Fonti:</div>
            <div className="flex flex-wrap gap-1.5">
              {message.citations.map((citation, index) => (
                <button
                  key={index}
                  className="inline-flex items-center gap-1 px-2 py-1 bg-white border border-[#dadce0] rounded text-xs hover:bg-[#f8f9fa] transition-colors"
                  title={citation.snippet ?? undefined}
                  onClick={() => {
                    // TODO Issue #4130: PDF viewer integration
                    // Requires useAgentStore integration in parent component
                    // Citation click handler will be implemented
                  }}
                >
                  <span className="text-[#1a73e8]">{citation.source || 'Source'}</span>
                  {citation.pageNumber && (
                    <span className="text-[#5f6368]">p.{citation.pageNumber}</span>
                  )}
                  {citation.score && (
                    <span className="text-[#94a3b8] text-[10px]">
                      ({(citation.score * 100).toFixed(0)}%)
                    </span>
                  )}
                </button>
              ))}
            </div>
          </div>
        )}

        {/* Copy Button (on hover) */}
        <button
          onClick={handleCopy}
          className="absolute -top-2 -right-2 opacity-0 group-hover:opacity-100 p-1.5 bg-white border border-[#dadce0] rounded shadow-sm hover:bg-[#f8f9fa] transition-opacity"
          aria-label="Copy message"
          title="Copy message"
        >
          {copied ? (
            <Check className="h-3 w-3 text-green-600" />
          ) : (
            <Copy className="h-3 w-3 text-[#5f6368]" />
          )}
        </button>
      </div>
    </div>
  );
});
