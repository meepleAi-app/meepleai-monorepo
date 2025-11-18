/**
 * Message - Individual message display component
 *
 * Displays a single chat message (user or assistant) with:
 * - Edit/delete actions for user messages
 * - Feedback buttons for assistant messages
 * - Inline message editing
 * - Message timestamps
 * - Edited badge
 * - Citations with PDF viewer (BGAI-074)
 */

import React, { useState } from 'react';
import { cn } from '@/lib/utils';
import { Message as MessageType, Citation } from '@/types';
import { useChatStore } from '@/store/chat/store';
import { MessageActions } from './MessageActions';
import { MessageEditForm } from './MessageEditForm';
import { FollowUpQuestions } from './FollowUpQuestions';
import { CitationList } from '../citations'; // #859
import { PdfViewerModal } from '../pdf/PdfViewerModal'; // BGAI-074
import { api } from '@/lib/api';

interface MessageProps {
  message: MessageType;
  isUser: boolean;
}

export const Message = React.memo(function Message({ message, isUser }: MessageProps) {
  const {
    editingMessageId,
    startEdit: startEditMessage,
    deleteMessage,
    setMessageFeedback,
    loading,
    setInputValue
  } = useChatStore();

  const isEditing = editingMessageId === message.id;
  const isDeleted = message.isDeleted;
  const isUpdating = loading.updating || loading.deleting;
  const isOptimistic = message.isOptimistic ?? false; // #1167

  // Don't show actions for deleted or optimistic messages
  const showActions = !isDeleted && !isEditing && !isOptimistic;

  // BGAI-074: PDF viewer state
  const [pdfViewerOpen, setPdfViewerOpen] = useState(false);
  const [selectedCitation, setSelectedCitation] = useState<Citation | null>(null);

  // CHAT-02: Handle follow-up question click
  const handleFollowUpClick = (question: string) => {
    setInputValue(question);
    // Focus the input field
    const inputElement = document.querySelector<HTMLTextAreaElement>('textarea[placeholder*="Fai una domanda"]');
    if (inputElement) {
      inputElement.focus();
    }
  };

  // BGAI-074: Handle citation click to open PDF viewer
  const handleCitationClick = (citation: Citation) => {
    setSelectedCitation(citation);
    setPdfViewerOpen(true);
  };

  return (
    <li
      aria-label={`${isUser ? 'Your message' : 'AI response'}`}
      className={cn(
        "mb-6 flex flex-col",
        isUser ? "items-end" : "items-start"
      )}
    >
      {/* Message Bubble */}
      <div
        className={cn(
          "max-w-[75%] p-3 rounded-lg text-sm leading-relaxed relative",
          isUser ? "bg-[#e3f2fd]" : "bg-[#f1f3f4]",
          isUser && showActions && "user-message-hoverable",
          isOptimistic && "opacity-60 animate-pulse" // #1167: Visual feedback for optimistic messages
        )}
        aria-busy={isOptimistic}
        aria-label={isOptimistic ? "Sending message..." : undefined}
      >
        <div className="flex justify-between items-center mb-1">
          <div className="font-medium text-xs text-[#64748b]">
            {isUser ? 'Tu' : 'MeepleAI'}
            {/* #1167: Sending indicator for optimistic messages */}
            {isOptimistic && (
              <span className="ml-1.5 text-[11px] text-[#64748b] italic">
                (invio...)
              </span>
            )}
            {/* Edited badge */}
            {message.updatedAt && !isDeleted && !isOptimistic && (
              <span className="ml-1.5 text-[11px] text-[#94a3b8] italic">
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
          <div className="text-[#94a3b8] italic">
            [Messaggio eliminato]
          </div>
        ) : isEditing ? (
          <MessageEditForm />
        ) : (
          <div className="whitespace-pre-wrap">
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

      {/* CHAT-02: Follow-up questions for assistant messages */}
      {!isUser && !isDeleted && message.followUpQuestions && message.followUpQuestions.length > 0 && (
        <div className="max-w-[75%]" data-testid="follow-up-questions">
          <FollowUpQuestions
            questions={message.followUpQuestions}
            onQuestionClick={handleFollowUpClick}
            disabled={loading.sending}
          />
        </div>
      )}

      {/* #859: Citations for assistant messages */}
      {!isUser && !isDeleted && message.citations && message.citations.length > 0 && (
        <div className="max-w-[75%]" data-testid="message-citations">
          <CitationList
            citations={message.citations}
            showRelevanceScores={false}
            collapsible={true}
            onCitationClick={handleCitationClick}
          />
        </div>
      )}

      {/* BGAI-074: PDF Viewer Modal */}
      {selectedCitation && (
        <PdfViewerModal
          open={pdfViewerOpen}
          onOpenChange={setPdfViewerOpen}
          pdfUrl={api.pdf.getPdfDownloadUrl(selectedCitation.documentId)}
          initialPage={selectedCitation.pageNumber}
          documentName={`PDF - Page ${selectedCitation.pageNumber}`}
        />
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
