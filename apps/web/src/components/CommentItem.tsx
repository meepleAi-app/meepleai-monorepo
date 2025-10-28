import { useState, useCallback } from "react";
import type { RuleSpecComment } from "../lib/api";
import { MentionInput } from "./MentionInput";

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
      console.error("Failed to edit comment:", error);
      alert("Impossibile modificare il commento");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCancelEdit = () => {
    setEditText(comment.commentText);
    setIsEditing(false);
  };

  const handleDelete = async () => {
    const confirmed = confirm("Sei sicuro di voler eliminare questo commento?");
    if (!confirmed) {
      return;
    }

    setIsSubmitting(true);
    try {
      await onDelete(comment.id);
    } catch (error) {
      console.error("Failed to delete comment:", error);
      alert("Impossibile eliminare il commento");
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
      console.error("Failed to reply:", error);
      alert("Impossibile aggiungere la risposta");
    } finally {
      setIsSubmitting(false);
    }
  }, [comment.id, replyText, onReply]);

  const handleCancelReply = useCallback(() => {
    setReplyText("");
    setIsReplying(false);
  }, []);

  const handleResolve = async () => {
    setIsSubmitting(true);
    try {
      await onResolve(comment.id);
    } catch (error) {
      console.error("Failed to resolve comment:", error);
      alert("Impossibile contrassegnare come risolto");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleUnresolve = async () => {
    setIsSubmitting(true);
    try {
      await onUnresolve(comment.id);
    } catch (error) {
      console.error("Failed to unresolve comment:", error);
      alert("Impossibile riaprire il commento");
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
            style={{
              color: "#0070f3",
              fontWeight: "bold",
              cursor: "pointer"
            }}
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
      style={{
        padding: 12,
        background: comment.isResolved ? "#f5f5f5" : "#f9f9f9",
        border: "1px solid #ddd",
        borderRadius: 4,
        marginBottom: 12,
        opacity: comment.isResolved ? 0.7 : 1
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 8 }}>
        <div>
          <strong style={{ fontSize: 14 }}>{comment.userDisplayName}</strong>
          {comment.atomId && (
            <span style={{ marginLeft: 8, fontSize: 12, color: "#666", background: "#e3f2fd", padding: "2px 6px", borderRadius: 3 }}>
              Regola: {comment.atomId}
            </span>
          )}
          {comment.lineNumber !== null && (
            <span style={{ marginLeft: 8, fontSize: 12, color: "#666", background: "#fff3e0", padding: "2px 6px", borderRadius: 3 }}>
              Line {comment.lineNumber}
            </span>
          )}
          {comment.isResolved && (
            <span
              style={{
                marginLeft: 8,
                padding: "2px 8px",
                background: "#e8f5e9",
                color: "#2e7d32",
                borderRadius: 4,
                fontSize: 12
              }}
              title={`Resolved by ${comment.resolvedByDisplayName || "Unknown"} on ${comment.resolvedAt ? new Date(comment.resolvedAt).toLocaleString() : "Unknown"}`}
            >
              ✓ Resolved
            </span>
          )}
        </div>
        <div style={{ fontSize: 12, color: "#999" }}>
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
            style={{
              width: "100%",
              minHeight: 80,
              padding: 8,
              border: "1px solid #ccc",
              borderRadius: 4,
              fontSize: 14,
              fontFamily: "inherit",
              marginBottom: 8,
              resize: "vertical"
            }}
          />
          <div style={{ display: "flex", gap: 8 }}>
            <button
              onClick={handleSaveEdit}
              disabled={isDisabled || !editText.trim()}
              style={{
                padding: "6px 12px",
                background: isDisabled || !editText.trim() ? "#ccc" : "#4caf50",
                color: "white",
                border: "none",
                borderRadius: 4,
                cursor: isDisabled || !editText.trim() ? "not-allowed" : "pointer",
                fontSize: 13
              }}
            >
              {isSubmitting ? "Salvataggio..." : "Salva"}
            </button>
            <button
              onClick={handleCancelEdit}
              disabled={isDisabled}
              style={{
                padding: "6px 12px",
                background: "#666",
                color: "white",
                border: "none",
                borderRadius: 4,
                cursor: isDisabled ? "not-allowed" : "pointer",
                fontSize: 13
              }}
            >
              Annulla
            </button>
          </div>
        </div>
      ) : (
        <div>
          {/* Line context snippet if available */}
          {comment.lineContext && (
            <div
              style={{
                padding: 8,
                background: "#fff",
                border: "1px solid #e0e0e0",
                borderRadius: 4,
                marginBottom: 8,
                fontSize: 13,
                fontFamily: "monospace",
                color: "#666"
              }}
            >
              {comment.lineContext}
            </div>
          )}

          {/* Comment text with mention rendering */}
          <p
            style={{
              margin: "0 0 8px 0",
              fontSize: 14,
              lineHeight: 1.5,
              textDecoration: comment.isResolved ? "line-through" : "none"
            }}
          >
            {renderTextWithMentions(comment.commentText)}
          </p>

          {/* Mentioned users display */}
          {comment.mentionedUserIds && comment.mentionedUserIds.length > 0 && (
            <div style={{ fontSize: 12, color: "#666", marginBottom: 8 }}>
              <span style={{ fontStyle: "italic" }}>
                Mentioned: {comment.mentionedUserIds.length} user(s)
              </span>
            </div>
          )}

          {/* Action buttons */}
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap", marginTop: 8 }}>
            {canEdit && (
              <button
                onClick={() => setIsEditing(true)}
                disabled={isDisabled}
                style={{
                  padding: "4px 10px",
                  background: isDisabled ? "#ccc" : "#0070f3",
                  color: "white",
                  border: "none",
                  borderRadius: 4,
                  cursor: isDisabled ? "not-allowed" : "pointer",
                  fontSize: 12
                }}
                aria-label="Edit comment"
              >
                Modifica
              </button>
            )}
            {canDelete && (
              <button
                onClick={handleDelete}
                disabled={isDisabled}
                style={{
                  padding: "4px 10px",
                  background: isDisabled ? "#ccc" : "#d93025",
                  color: "white",
                  border: "none",
                  borderRadius: 4,
                  cursor: isDisabled ? "not-allowed" : "pointer",
                  fontSize: 12
                }}
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
                style={{
                  padding: "4px 10px",
                  background: isDisabled ? "#ccc" : "#0070f3",
                  color: "white",
                  border: "none",
                  borderRadius: 4,
                  cursor: isDisabled ? "not-allowed" : "pointer",
                  fontSize: 12
                }}
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
                style={{
                  padding: "4px 10px",
                  background: isDisabled ? "#ccc" : comment.isResolved ? "#666" : "#4caf50",
                  color: "white",
                  border: "none",
                  borderRadius: 4,
                  cursor: isDisabled ? "not-allowed" : "pointer",
                  fontSize: 12
                }}
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
        <div style={{ marginTop: 12, paddingTop: 12, borderTop: "1px solid #ddd" }}>
          <MentionInput
            value={replyText}
            onChange={setReplyText}
            placeholder="Scrivi una risposta..."
            disabled={isDisabled}
          />
          <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
            <button
              onClick={handleReply}
              disabled={isDisabled || !replyText.trim()}
              style={{
                padding: "6px 12px",
                background: isDisabled || !replyText.trim() ? "#ccc" : "#4caf50",
                color: "white",
                border: "none",
                borderRadius: 4,
                cursor: isDisabled || !replyText.trim() ? "not-allowed" : "pointer",
                fontSize: 13
              }}
            >
              {isSubmitting ? "Invio..." : "Invia risposta"}
            </button>
            <button
              onClick={handleCancelReply}
              disabled={isDisabled}
              style={{
                padding: "6px 12px",
                background: "#666",
                color: "white",
                border: "none",
                borderRadius: 4,
                cursor: isDisabled ? "not-allowed" : "pointer",
                fontSize: 13
              }}
            >
              Annulla
            </button>
          </div>
        </div>
      )}

      {/* Threaded replies (recursive rendering) */}
      {comment.replies && comment.replies.length > 0 && depth < maxDepth && (
        <div
          style={{
            marginLeft: 20,
            marginTop: 12,
            borderLeft: "2px solid #ddd",
            paddingLeft: 12
          }}
        >
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
    </div>
  );
}
