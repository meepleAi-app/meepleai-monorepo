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

export function MessageList() {
  const { messages, activeChatId, loading } = useChatContext();

  // Loading state
  if (loading.messages) {
    return (
      <div
        role="region"
        aria-label="Chat messages"
        style={{
          flex: 1,
          overflowY: 'auto',
          padding: 24,
          background: '#ffffff'
        }}
      >
        <div role="status" aria-live="polite" style={{ textAlign: 'center' }}>
          <div style={{ marginBottom: 12, fontSize: 14, color: '#64748b' }}>
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
        style={{
          flex: 1,
          overflowY: 'auto',
          padding: 24,
          background: '#ffffff'
        }}
      >
        <div style={{ textAlign: 'center', padding: 48, color: '#64748b' }}>
          <p style={{ fontSize: 16, marginBottom: 8 }}>Nessun messaggio ancora.</p>
          <p style={{ fontSize: 14 }}>
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
      style={{
        flex: 1,
        overflowY: 'auto',
        padding: 24,
        background: '#ffffff'
      }}
    >
      <ul
        role="log"
        aria-live="polite"
        aria-atomic="false"
        style={{
          listStyle: 'none',
          margin: 0,
          padding: 0
        }}
      >
        {messages.map((msg) => (
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
