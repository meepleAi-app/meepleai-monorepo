import { useState, useCallback } from "react";
import type { RuleSpecComment } from "@/lib/api";
import { MentionInput } from "../chat/MentionInput";
import { cn } from "@/lib/utils";
import { useConfirmDialog } from "@/hooks/useConfirmDialog";
import { useAlertDialog } from "@/hooks/useAlertDialog";
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

interface CommentItemProps {
  comment: RuleSpecComment;
  currentUserId: string;
  currentUserRole: string;
  depth?: number; // Thread depth for indentation (default: 0)
  maxDepth?: number; // Max depth to render (default: 5)
  disabled?: boolean; // Disable all actions (default: false)
  onEdit: (commentId: string, newText: string) => Promise<void>;
  onDelete: (commentId: string) => Promise<void>;
  onReply: (parentCommentId: string, replyText: string) => Promise<void>;
  onResolve: (commentId: string) => Promise<void>;
  onUnresolve: (commentId: string) => Promise<void>;
}

export function CommentItem({
  comment,
  currentUserId,
  currentUserRole,
  depth = 0,
  maxDepth = 5,
  disabled = false,
  onEdit,
  onDelete,
  onReply,
  onResolve,
  onUnresolve
}: CommentItemProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editText, setEditText] = useState(comment.commentText);
  const [isReplying, setIsReplying] = useState(false);
  const [replyText, setReplyText] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const { confirm, ConfirmDialogComponent } = useConfirmDialog();
  const { alert: showAlert, AlertDialogComponent } = useAlertDialog();

  const canEdit = comment.userId === currentUserId && !disabled;
  const canDelete = (comment.userId === currentUserId || currentUserRole === "Admin") && !disabled;
  const canResolve = (currentUserRole === "Admin" || currentUserRole === "Editor") && !disabled;
  const isDisabled = disabled || isSubmitting;

  const handleSaveEdit = async () => {
    if (!editText.trim()) {
      return;
    }

    setIsSubmitting(true);
    try {
      await onEdit(comment.id, editText);
      setIsEditing(false);
    } catch (error) {
      logger.error(
        'Failed to edit comment',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('CommentItem', 'handleSaveEdit', { commentId: comment.id })
      );
      await showAlert({
        title: "Errore",
        message: "Impossibile modificare il commento",
        variant: "error",
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCancelEdit = () => {
    setEditText(comment.commentText);
    setIsEditing(false);
  };

  const handleDelete = async () => {
    const confirmed = await confirm({
      title: "Elimina commento",
      message: "Sei sicuro di voler eliminare questo commento? Questa azione non può essere annullata.",
      variant: "destructive",
      confirmText: "Elimina",
      cancelText: "Annulla",
    });

    if (!confirmed) {
      return;
    }

    setIsSubmitting(true);
    try {
      await onDelete(comment.id);
    } catch (error) {
      logger.error(
        'Failed to delete comment',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('CommentItem', 'handleDelete', { commentId: comment.id })
      );
      await showAlert({
        title: "Errore",
        message: "Impossibile eliminare il commento",
        variant: "error",
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleReply = useCallback(async () => {
    if (!replyText.trim()) {
      return;
    }

    setIsSubmitting(true);
    try {
      await onReply(comment.id, replyText);
      setReplyText("");
      setIsReplying(false);
    } catch (error) {
      logger.error(
        'Failed to reply',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('CommentItem', 'handleReply', { commentId: comment.id })
      );
      await showAlert({
        title: "Errore",
        message: "Impossibile aggiungere la risposta",
        variant: "error",
      });
    } finally {
      setIsSubmitting(false);
    }
  }, [comment.id, replyText, onReply, showAlert]);

  const handleCancelReply = useCallback(() => {
    setReplyText("");
    setIsReplying(false);
  }, []);

  const handleResolve = async () => {
    setIsSubmitting(true);
    try {
      await onResolve(comment.id);
    } catch (error) {
      logger.error(
        'Failed to resolve comment',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('CommentItem', 'handleResolve', { commentId: comment.id })
      );
      await showAlert({
        title: "Errore",
        message: "Impossibile contrassegnare come risolto",
        variant: "error",
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleUnresolve = async () => {
    setIsSubmitting(true);
    try {
      await onUnresolve(comment.id);
    } catch (error) {
      logger.error(
        'Failed to unresolve comment',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('CommentItem', 'handleUnresolve', { commentId: comment.id })
      );
      await showAlert({
        title: "Errore",
        message: "Impossibile riaprire il commento",
        variant: "error",
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  // Render text with @mentions as styled spans
  const renderTextWithMentions = (text: string) => {
    const mentionRegex = /@(\w+)/g;
    const parts = text.split(mentionRegex);

    return parts.map((part, i) => {
      if (i % 2 === 1) { // Odd indices are captured groups (usernames)
        return (
          <span
            key={i}
            className="text-blue-500 font-bold cursor-pointer"
            title={`Mentioned user: ${part}`}
          >
            @{part}
          </span>
        );
      }
      return <span key={i}>{part}</span>;
    });
  };

  return (
    <div
      className={cn(
        "p-3 border border-gray-300 rounded mb-3",
        comment.isResolved ? "bg-gray-100 opacity-70" : "bg-gray-50"
      )}
    >
      <div className="flex justify-between items-start mb-2">
        <div>
          <strong className="text-sm">{comment.userDisplayName}</strong>
          {comment.atomId && (
            <span className="ml-2 text-xs text-gray-600 bg-blue-50 px-1.5 py-0.5 rounded-sm">
              Regola: {comment.atomId}
            </span>
          )}
          {comment.lineNumber !== null && (
            <span className="ml-2 text-xs text-gray-600 bg-orange-50 px-1.5 py-0.5 rounded-sm">
              Line {comment.lineNumber}
            </span>
          )}
          {comment.isResolved && (
            <span
              className="ml-2 px-2 py-0.5 bg-green-50 text-green-800 rounded text-xs"
              title={`Resolved by ${comment.resolvedByDisplayName || "Unknown"} on ${comment.resolvedAt ? new Date(comment.resolvedAt).toLocaleString() : "Unknown"}`}
            >
              ✓ Resolved
            </span>
          )}
        </div>
        <div className="text-xs text-gray-600">
          {new Date(comment.createdAt).toLocaleString()}
          {comment.updatedAt && " (modificato)"}
        </div>
      </div>

      {isEditing ? (
        <div>
          <textarea
            value={editText}
            onChange={(e) => setEditText(e.target.value)}
            disabled={isDisabled}
            className="w-full min-h-[80px] p-2 border border-gray-400 rounded text-sm font-[inherit] mb-2 resize-y"
          />
          <div className="flex gap-2">
            <button
              onClick={handleSaveEdit}
              disabled={isDisabled || !editText.trim()}
              className={cn(
                "px-3 py-1.5 text-white border-0 rounded text-sm",
                isDisabled || !editText.trim()
                  ? "bg-gray-400 cursor-not-allowed"
                  : "bg-green-600 cursor-pointer hover:bg-green-700"
              )}
            >
              {isSubmitting ? "Salvataggio..." : "Salva"}
            </button>
            <button
              onClick={handleCancelEdit}
              disabled={isDisabled}
              className={cn(
                "px-3 py-1.5 bg-gray-600 text-white border-0 rounded text-sm",
                isDisabled ? "cursor-not-allowed" : "cursor-pointer hover:bg-gray-700"
              )}
            >
              Annulla
            </button>
          </div>
        </div>
      ) : (
        <div>
          {/* Line context snippet if available */}
          {comment.lineContext && (
            <div className="p-2 bg-white border border-gray-300 rounded mb-2 text-sm font-mono text-gray-600">
              {comment.lineContext}
            </div>
          )}

          {/* Comment text with mention rendering */}
          <p
            className={cn(
              "m-0 mb-2 text-sm leading-normal",
              comment.isResolved && "line-through"
            )}
          >
            {renderTextWithMentions(comment.commentText)}
          </p>

          {/* Mentioned users display */}
          {comment.mentionedUserIds && comment.mentionedUserIds.length > 0 && (
            <div className="text-xs text-gray-600 mb-2">
              <span className="italic">
                Mentioned: {comment.mentionedUserIds.length} user(s)
              </span>
            </div>
          )}

          {/* Action buttons */}
          <div className="flex gap-2 flex-wrap mt-2">
            {canEdit && (
              <button
                onClick={() => setIsEditing(true)}
                disabled={isDisabled}
                className={cn(
                  "px-2.5 py-1 text-white border-0 rounded text-xs",
                  isDisabled ? "bg-gray-400 cursor-not-allowed" : "bg-blue-500 cursor-pointer hover:bg-blue-600"
                )}
                aria-label="Edit comment"
              >
                Modifica
              </button>
            )}
            {canDelete && (
              <button
                onClick={handleDelete}
                disabled={isDisabled}
                className={cn(
                  "px-2.5 py-1 text-white border-0 rounded text-xs",
                  isDisabled ? "bg-gray-400 cursor-not-allowed" : "bg-red-600 cursor-pointer hover:bg-red-700"
                )}
                aria-label="Delete comment"
              >
                {isSubmitting ? "Eliminazione..." : "Elimina"}
              </button>
            )}
            {/* Reply button */}
            {depth < maxDepth && !disabled && (
              <button
                onClick={() => setIsReplying(!isReplying)}
                disabled={isDisabled}
                className={cn(
                  "px-2.5 py-1 text-white border-0 rounded text-xs",
                  isDisabled ? "bg-gray-400 cursor-not-allowed" : "bg-blue-500 cursor-pointer hover:bg-blue-600"
                )}
                aria-label="Reply to comment"
              >
                {isReplying ? "Annulla" : "Rispondi"}
              </button>
            )}
            {/* Resolve/Unresolve button */}
            {canResolve && (
              <button
                onClick={comment.isResolved ? handleUnresolve : handleResolve}
                disabled={isDisabled}
                className={cn(
                  "px-2.5 py-1 text-white border-0 rounded text-xs",
                  isDisabled
                    ? "bg-gray-400 cursor-not-allowed"
                    : comment.isResolved
                      ? "bg-gray-600 cursor-pointer hover:bg-gray-700"
                      : "bg-green-600 cursor-pointer hover:bg-green-700"
                )}
                aria-label={comment.isResolved ? "Reopen comment" : "Mark as resolved"}
              >
                {isSubmitting
                  ? "..."
                  : comment.isResolved
                  ? "Riapri"
                  : "Risolvi"}
              </button>
            )}
          </div>
        </div>
      )}

      {/* Reply form */}
      {isReplying && (
        <div className="mt-3 pt-3 border-t border-gray-300">
          <MentionInput
            value={replyText}
            onChange={setReplyText}
            placeholder="Scrivi una risposta..."
            disabled={isDisabled}
          />
          <div className="flex gap-2 mt-2">
            <button
              onClick={handleReply}
              disabled={isDisabled || !replyText.trim()}
              className={cn(
                "px-3 py-1.5 text-white border-0 rounded text-sm",
                isDisabled || !replyText.trim()
                  ? "bg-gray-400 cursor-not-allowed"
                  : "bg-green-600 cursor-pointer hover:bg-green-700"
              )}
            >
              {isSubmitting ? "Invio..." : "Invia risposta"}
            </button>
            <button
              onClick={handleCancelReply}
              disabled={isDisabled}
              className={cn(
                "px-3 py-1.5 bg-gray-600 text-white border-0 rounded text-sm",
                isDisabled ? "cursor-not-allowed" : "cursor-pointer hover:bg-gray-700"
              )}
            >
              Annulla
            </button>
          </div>
        </div>
      )}

      {/* Threaded replies (recursive rendering) */}
      {comment.replies && comment.replies.length > 0 && depth < maxDepth && (
        <div className="ml-5 mt-3 border-l-2 border-gray-300 pl-3">
          {comment.replies.map((reply) => (
            <CommentItem
              key={reply.id}
              comment={reply}
              currentUserId={currentUserId}
              currentUserRole={currentUserRole}
              depth={depth + 1}
              maxDepth={maxDepth}
              disabled={disabled}
              onEdit={onEdit}
              onDelete={onDelete}
              onReply={onReply}
              onResolve={onResolve}
              onUnresolve={onUnresolve}
            />
          ))}
        </div>
      )}

      {/* Confirm dialog */}
      <ConfirmDialogComponent />

      {/* Alert dialog */}
      <AlertDialogComponent />
    </div>
  );
}