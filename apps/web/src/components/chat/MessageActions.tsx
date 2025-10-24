/**
 * MessageActions - Message action buttons (edit, delete, feedback)
 *
 * Displays appropriate action buttons based on message role:
 * - User messages: Edit and delete buttons (shown on hover)
 * - Assistant messages: Helpful/Not-helpful feedback buttons
 *
 * Simplified version for Phase 3 - will be enhanced in Phase 4 with:
 * - Hover state management
 * - Loading states during operations
 * - Confirmation dialogs
 */

import React from 'react';
import { Message } from '@/types';

interface MessageActionsProps {
  message: Message;
  isUser: boolean;
  onEdit?: (messageId: string, content: string) => void;
  onDelete?: (messageId: string) => void;
  onFeedback?: (messageId: string, feedback: 'helpful' | 'not-helpful') => void;
  isEditing?: boolean;
  isUpdating?: boolean;
}

export function MessageActions({
  message,
  isUser,
  onEdit,
  onDelete,
  onFeedback,
  isEditing = false,
  isUpdating = false
}: MessageActionsProps) {
  // User message actions (edit/delete)
  if (isUser && !isEditing) {
    return (
      <div
        className="message-actions"
        style={{
          display: 'flex',
          gap: 4,
          opacity: 0,
          transition: 'opacity 0.2s'
        }}
      >
        <button
          onClick={() => onEdit?.(message.id, message.content)}
          disabled={isUpdating}
          aria-label="Edit message"
          style={{
            padding: '2px 6px',
            background: '#94a3b8',
            color: 'white',
            border: 'none',
            borderRadius: 3,
            fontSize: 10,
            cursor: isUpdating ? 'not-allowed' : 'pointer',
            opacity: isUpdating ? 0.5 : 1
          }}
          title="Modifica messaggio"
        >
          ✏️
        </button>
        <button
          onClick={() => onDelete?.(message.id)}
          disabled={isUpdating}
          aria-label="Delete message"
          style={{
            padding: '2px 6px',
            background: '#94a3b8',
            color: 'white',
            border: 'none',
            borderRadius: 3,
            fontSize: 10,
            cursor: isUpdating ? 'not-allowed' : 'pointer',
            opacity: isUpdating ? 0.5 : 1
          }}
          title="Elimina messaggio"
        >
          🗑️
        </button>
      </div>
    );
  }

  // Assistant message actions (feedback)
  if (!isUser) {
    return (
      <div
        role="group"
        aria-label="Message feedback"
        style={{
          display: 'flex',
          gap: 8,
          marginTop: 8
        }}
      >
        <button
          onClick={() => onFeedback?.(message.id, 'helpful')}
          aria-label="Mark as helpful"
          aria-pressed={message.feedback === 'helpful'}
          style={{
            padding: '4px 8px',
            background: message.feedback === 'helpful' ? '#34a853' : '#f1f3f4',
            color: message.feedback === 'helpful' ? 'white' : '#64748b',
            border: 'none',
            borderRadius: 4,
            fontSize: 12,
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 4
          }}
        >
          <span>👍</span>
          <span>Utile</span>
        </button>
        <button
          onClick={() => onFeedback?.(message.id, 'not-helpful')}
          aria-label="Mark as not helpful"
          aria-pressed={message.feedback === 'not-helpful'}
          style={{
            padding: '4px 8px',
            background: message.feedback === 'not-helpful' ? '#ea4335' : '#f1f3f4',
            color: message.feedback === 'not-helpful' ? 'white' : '#64748b',
            border: 'none',
            borderRadius: 4,
            fontSize: 12,
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            gap: 4
          }}
        >
          <span>👎</span>
          <span>Non utile</span>
        </button>
      </div>
    );
  }

  return null;
}
