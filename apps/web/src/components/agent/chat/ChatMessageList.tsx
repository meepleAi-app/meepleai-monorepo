/**
 * ChatMessageList - Scrollable message container (Issue #3243)
 *
 * Features:
 * - Scrollable container with auto-scroll to bottom
 * - Message grouping (consecutive same-sender)
 * - Scroll-to-bottom button (when not at bottom)
 * - Progressive text reveal during streaming
 * - Empty state
 */

import React, { useRef, useEffect, useState, useCallback } from 'react';

import { ArrowDown } from 'lucide-react';

import { cn } from '@/lib/utils';
import type { AgentMessage } from '@/types/agent';

import { ChatMessage } from './ChatMessage';
import { TypingIndicator } from './TypingIndicator';

interface ChatMessageListProps {
  /** Messages to display */
  messages: AgentMessage[];

  /** Whether streaming is active */
  isStreaming?: boolean;

  /** Current streaming chunk (for progressive reveal) */
  currentChunk?: string;

  /** Custom class name */
  className?: string;
}

export function ChatMessageList({
  messages,
  isStreaming = false,
  currentChunk = '',
  className,
}: ChatMessageListProps) {
  const scrollContainerRef = useRef<HTMLDivElement>(null);
  const [showScrollButton, setShowScrollButton] = useState(false);
  const [isNearBottom, setIsNearBottom] = useState(true);

  /**
   * Check if user is near bottom of scroll container
   */
  const checkScrollPosition = useCallback(() => {
    const container = scrollContainerRef.current;
    if (!container) return;

    const { scrollTop, scrollHeight, clientHeight } = container;
    const distanceFromBottom = scrollHeight - scrollTop - clientHeight;

    // Consider "near bottom" if within 100px
    const nearBottom = distanceFromBottom < 100;
    setIsNearBottom(nearBottom);
    setShowScrollButton(!nearBottom);
  }, []);

  /**
   * Scroll to bottom of container
   */
  const scrollToBottom = useCallback((behavior: ScrollBehavior = 'smooth') => {
    const container = scrollContainerRef.current;
    if (!container) return;

    container.scrollTo({
      top: container.scrollHeight,
      behavior,
    });
  }, []);

  /**
   * Auto-scroll to bottom when new messages arrive (if user is near bottom)
   */
  useEffect(() => {
    if (isNearBottom) {
      // Use instant scroll for new messages to avoid lag
      scrollToBottom('instant');
    }
  }, [messages.length, currentChunk, isStreaming, isNearBottom, scrollToBottom]);

  /**
   * Attach scroll listener
   */
  useEffect(() => {
    const container = scrollContainerRef.current;
    if (!container) return;

    container.addEventListener('scroll', checkScrollPosition);
    checkScrollPosition(); // Initial check

    return () => {
      container.removeEventListener('scroll', checkScrollPosition);
    };
  }, [checkScrollPosition]);

  /**
   * Determine if messages should be grouped
   * Group if consecutive messages from same sender and < 2 minutes apart
   */
  const shouldGroupMessage = useCallback(
    (index: number): boolean => {
      if (index === 0) return false; // Never group first message

      // eslint-disable-next-line security/detect-object-injection -- index is validated loop parameter from messages array
      const current = messages[index];
      const previous = messages[index - 1];

      // Don't group system messages
      if (current.type === 'system' || previous.type === 'system') return false;

      // Same sender?
      if (current.type !== previous.type) return false;

      // Within 2 minutes?
      const timeDiff = current.timestamp.getTime() - previous.timestamp.getTime();
      const twoMinutes = 2 * 60 * 1000;

      return timeDiff < twoMinutes;
    },
    [messages]
  );

  // Empty state
  if (messages.length === 0 && !isStreaming) {
    return (
      <div
        className={cn(
          'flex items-center justify-center h-full text-gray-400 text-sm',
          className
        )}
      >
        <p>Nessun messaggio. Inizia una conversazione!</p>
      </div>
    );
  }

  return (
    <div className={cn('relative h-full', className)}>
      {/* Scrollable container */}
      <div
        ref={scrollContainerRef}
        className="h-full overflow-y-auto px-4 py-4 scroll-smooth"
        role="log"
        aria-live="polite"
        aria-label="Chat messages"
      >
        {/* Messages */}
        {messages.map((message, index) => (
          <ChatMessage
            key={`${message.timestamp.getTime()}-${index}`}
            message={message}
            isGrouped={shouldGroupMessage(index)}
          />
        ))}

        {/* Progressive reveal during streaming */}
        {isStreaming && currentChunk && (
          <div className="flex flex-col items-start mt-4">
            <div className="max-w-[85%] p-3 rounded-lg text-sm leading-relaxed bg-gray-800 text-gray-100">
              <div className="flex items-center justify-between mb-1.5 gap-2">
                <span className="font-medium text-xs text-gray-400">Agent</span>
              </div>
              <div className="text-sm whitespace-pre-wrap">{currentChunk}</div>
            </div>
          </div>
        )}

        {/* Typing indicator */}
        {isStreaming && !currentChunk && (
          <div className="mt-4">
            <TypingIndicator />
          </div>
        )}
      </div>

      {/* Scroll to bottom button */}
      {showScrollButton && (
        <button
          onClick={() => scrollToBottom('smooth')}
          className={cn(
            'absolute bottom-4 right-4',
            'p-2 bg-cyan-500 text-white rounded-full shadow-lg',
            'hover:bg-cyan-600 transition-colors',
            'focus:outline-none focus:ring-2 focus:ring-cyan-400 focus:ring-offset-2'
          )}
          aria-label="Scroll to bottom"
          title="Scroll to bottom"
        >
          <ArrowDown className="h-5 w-5" />
        </button>
      )}
    </div>
  );
}
