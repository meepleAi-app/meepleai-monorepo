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
import { cn } from '@/lib/utils';
import { Message as MessageType } from '@/types';
import { useChatStore } from '@/store/chat/store';
import { MessageActions } from './MessageActions';
import { MessageEditForm } from './MessageEditForm';
import { FollowUpQuestions } from './FollowUpQuestions';

interface MessageProps {
  message: MessageType;
  isUser: boolean;
}

export const Message = React.memo(function Message({ message, isUser }: MessageProps) {
  // Issue #1676: Migrated from useChatContext to direct Zustand store
  const {
    editingMessageId,
    startEditMessage,
    deleteMessage,
    setMessageFeedback,
    loading,
    setInputValue,
  } = useChatStore(state => ({
    editingMessageId: state.editingMessageId,
    startEditMessage: state.startEdit,
    deleteMessage: state.deleteMessage,
    setMessageFeedback: state.setMessageFeedback,
    loading: state.loading,
    setInputValue: state.setInputValue,
  }));

  const isEditing = editingMessageId === message.id;
  const isDeleted = message.isDeleted;
  const isUpdating = loading.updating || loading.deleting;

  // Don't show actions for deleted messages
  const showActions = !isDeleted && !isEditing;

  // CHAT-02: Handle follow-up question click
  const handleFollowUpClick = (question: string) => {
    setInputValue(question);
    // Focus the input field
    const inputElement = document.querySelector<HTMLTextAreaElement>(
      'textarea[placeholder*="Fai una domanda"]'
    );
    if (inputElement) {
      inputElement.focus();
    }
  };

  return (
    <li
      aria-label={`${isUser ? 'Your message' : 'AI response'}`}
      className={cn('mb-6 flex flex-col', isUser ? 'items-end' : 'items-start')}
    >
      {/* Message Bubble */}
      <div
        className={cn(
          'max-w-[75%] p-3 rounded-lg text-sm leading-relaxed relative',
          isUser ? 'bg-[#e3f2fd]' : 'bg-[#f1f3f4]',
          isUser && showActions && 'user-message-hoverable'
        )}
      >
        <div className="flex justify-between items-center mb-1">
          <div className="font-medium text-xs text-[#64748b]">
            {isUser ? 'Tu' : 'MeepleAI'}
            {/* Edited badge */}
            {message.updatedAt && !isDeleted && (
              <span className="ml-1.5 text-[11px] text-[#94a3b8] italic">(modificato)</span>
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
          <div className="text-[#94a3b8] italic">[Messaggio eliminato]</div>
        ) : isEditing ? (
          <MessageEditForm />
        ) : (
          <div className="whitespace-pre-wrap">{message.content}</div>
        )}
      </div>

      {/* Assistant message feedback buttons */}
      {!isUser && showActions && (
        <MessageActions message={message} isUser={isUser} onFeedback={setMessageFeedback} />
      )}

      {/* CHAT-02: Follow-up questions for assistant messages */}
      {!isUser &&
        !isDeleted &&
        message.followUpQuestions &&
        message.followUpQuestions.length > 0 && (
          <div className="max-w-[75%]" data-testid="follow-up-questions">
            <FollowUpQuestions
              questions={message.followUpQuestions}
              onQuestionClick={handleFollowUpClick}
              disabled={loading.sending}
            />
          </div>
        )}

      {/* Timestamp */}
      {!isDeleted && (
        <div className="text-[11px] text-[#64748b] mt-1">
          {message.timestamp.toLocaleTimeString()}
        </div>
      )}
    </li>
  );
});
