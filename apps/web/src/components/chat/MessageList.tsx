/**
 * MessageList - Scrollable list of chat messages
 *
 * Displays messages with loading and empty states.
 * Integrates with ChatProvider for message state.
 *
 * Simplified version for Phase 3 - will be enhanced in Phase 4 with:
 * - Message animations (AnimatePresence)
 * - Auto-scroll to bottom on new messages
 * - Scroll-to-top button for long conversations
 * - Message grouping by date
 */

import React from 'react';
import { useChatContext } from './ChatProvider';
import { Message } from './Message';
import { SkeletonLoader } from '../loading/SkeletonLoader';
import { Message as MessageType } from '@/types';

export function MessageList() {
  const { messages, activeChatId, loading } = useChatContext();

  // Loading state
  if (loading.messages) {
    return (
      <div
        role="region"
        aria-label="Chat messages"
        className="flex-1 overflow-y-auto p-6 bg-white"
      >
        <div role="status" aria-live="polite" className="text-center">
          <div className="mb-3 text-sm text-[#64748b]">
            Caricamento messaggi...
          </div>
          <SkeletonLoader variant="message" count={3} ariaLabel="Caricamento messaggi" />
        </div>
      </div>
    );
  }

  // Empty state
  if (messages.length === 0) {
    return (
      <div
        role="region"
        aria-label="Chat messages"
        className="flex-1 overflow-y-auto p-6 bg-white"
      >
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

  // Messages list
  return (
    <div
      role="region"
      aria-label="Chat messages"
      className="flex-1 overflow-y-auto p-6 bg-white"
    >
      <ul
        role="log"
        aria-live="polite"
        aria-atomic="false"
        className="list-none m-0 p-0"
      >
        {messages.map((msg: MessageType) => (
          <Message
            key={msg.id}
            message={msg}
            isUser={msg.role === 'user'}
          />
        ))}
      </ul>
    </div>
  );
}
