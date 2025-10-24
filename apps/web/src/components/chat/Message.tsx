/**
 * Message - Individual message display component
 *
 * Displays a single chat message (user or assistant) with:
 * - Edit/delete actions for user messages
 * - Feedback buttons for assistant messages
 * - Inline message editing
 * - Message timestamps
 * - Edited badge
 */

import React from 'react';
import { Message as MessageType } from '@/types';
import { useChatContext } from './ChatProvider';
import { MessageActions } from './MessageActions';
import { MessageEditForm } from './MessageEditForm';

interface MessageProps {
  message: MessageType;
  isUser: boolean;
}

export function Message({ message, isUser }: MessageProps) {
  const {
    editingMessageId,
    startEditMessage,
    deleteMessage,
    setMessageFeedback,
    loading
  } = useChatContext();

  const isEditing = editingMessageId === message.id;
  const isDeleted = message.isDeleted;
  const isUpdating = loading.updating || loading.deleting;

  // Don't show actions for deleted messages
  const showActions = !isDeleted && !isEditing;

  return (
    <li
      aria-label={`${isUser ? 'Your message' : 'AI response'}`}
      style={{
        marginBottom: 24,
        display: 'flex',
        flexDirection: 'column',
        alignItems: isUser ? 'flex-end' : 'flex-start'
      }}
    >
      {/* Message Bubble */}
      <div
        className={isUser && showActions ? 'user-message-hoverable' : ''}
        style={{
          maxWidth: '75%',
          padding: 12,
          borderRadius: 8,
          background: isUser ? '#e3f2fd' : '#f1f3f4',
          fontSize: 14,
          lineHeight: 1.5,
          position: 'relative'
        }}
      >
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 4 }}>
          <div style={{ fontWeight: 500, fontSize: 12, color: '#64748b' }}>
            {isUser ? 'Tu' : 'MeepleAI'}
            {/* Edited badge */}
            {message.updatedAt && !isDeleted && (
              <span style={{ marginLeft: 6, fontSize: 11, color: '#94a3b8', fontStyle: 'italic' }}>
                (modificato)
              </span>
            )}
          </div>

          {/* User message actions (edit/delete) */}
          {isUser && showActions && (
            <MessageActions
              message={message}
              isUser={isUser}
              onEdit={startEditMessage}
              onDelete={deleteMessage}
              isEditing={isEditing}
              isUpdating={isUpdating}
            />
          )}
        </div>

        {/* Message Content */}
        {isDeleted ? (
          <div style={{ color: '#94a3b8', fontStyle: 'italic' }}>
            [Messaggio eliminato]
          </div>
        ) : isEditing ? (
          <MessageEditForm />
        ) : (
          <div style={{ whiteSpace: 'pre-wrap' }}>
            {message.content}
          </div>
        )}
      </div>

      {/* Assistant message feedback buttons */}
      {!isUser && showActions && (
        <MessageActions
          message={message}
          isUser={isUser}
          onFeedback={setMessageFeedback}
        />
      )}

      {/* Timestamp */}
      {!isDeleted && (
        <div style={{ fontSize: 11, color: '#64748b', marginTop: 4 }}>
          {message.timestamp.toLocaleTimeString()}
        </div>
      )}
    </li>
  );
}
