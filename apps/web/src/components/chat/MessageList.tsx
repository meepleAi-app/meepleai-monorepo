/**
 * MessageList - Scrollable list of chat messages
 *
 * Displays messages with loading and empty states.
 * Integrates with ChatProvider for message state.
 *
 * Updated for Issue #1840:
 * - VirtualizedMessageList integration (react-window)
 * - ChatMessage component (Issue #1831)
 * - Auto-scroll to bottom on new messages
 * - Efficient rendering for large message lists (>50 messages)
 *
 * Updated for BGAI-074:
 * - Citation click handler for jump-to-PDF-page functionality
 */

import React from 'react';
import { useChatContext } from '@/hooks/useChatContext';
import { VirtualizedMessageList } from './VirtualizedMessageList';
import { SkeletonLoader } from '../loading/SkeletonLoader';

export interface MessageListProps {
  /** Citation click handler for PDF page jump (BGAI-074) */
  onCitationClick?: (citationId: string) => void;
}

export function MessageList({ onCitationClick }: MessageListProps) {
  const {
    messages,
    activeChatId,
    loading,
    // Streaming state (Issue #1007)
    isStreaming,
    streamingAnswer,
    streamingState: streamingStateMessage,
  } = useChatContext();

  // Loading state
  if (loading.messages) {
    return (
      <div role="region" aria-label="Chat messages" className="flex-1 overflow-y-auto p-6 bg-white">
        <div role="status" aria-live="polite" className="text-center">
          <div className="mb-3 text-sm text-[#64748b]">Caricamento messaggi...</div>
          <SkeletonLoader variant="message" count={3} ariaLabel="Caricamento messaggi" />
        </div>
      </div>
    );
  }

  // Empty state
  if (messages.length === 0) {
    return (
      <div role="region" aria-label="Chat messages" className="flex-1 overflow-y-auto p-6 bg-white">
        <div className="text-center p-12 text-[#64748b]">
          <p className="text-base mb-2">Nessun messaggio ancora.</p>
          <p className="text-sm">
            {activeChatId
              ? 'Inizia facendo una domanda!'
              : 'Seleziona una chat esistente o creane una nuova per iniziare.'}
          </p>
        </div>
      </div>
    );
  }

  // Streaming message data
  const streamingMessage =
    isStreaming && (streamingAnswer || streamingStateMessage)
      ? {
          content: streamingAnswer || '',
          stateMessage: streamingStateMessage,
        }
      : undefined;

  // Messages list with virtualization (Issue #1840)
  return (
    <div role="region" aria-label="Chat messages" className="flex-1 overflow-y-auto p-6 bg-white">
      <VirtualizedMessageList
        messages={messages}
        streamingMessage={streamingMessage}
        isStreaming={isStreaming}
        onCitationClick={onCitationClick}
        className="h-full"
      />
    </div>
  );
}
