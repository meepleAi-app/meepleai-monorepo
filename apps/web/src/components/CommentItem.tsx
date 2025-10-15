import { useState } from "react";
import type { RuleSpecComment } from "../lib/api";

interface CommentItemProps {
  comment: RuleSpecComment;
  currentUserId: string;
  currentUserRole: string;
  onEdit: (commentId: string, newText: string) => Promise<void>;
  onDelete: (commentId: string) => Promise<void>;
}

export function CommentItem({
  comment,
  currentUserId,
  currentUserRole,
  onEdit,
  onDelete
}: CommentItemProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [editText, setEditText] = useState(comment.commentText);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const canEdit = comment.userId === currentUserId;
  const canDelete = comment.userId === currentUserId || currentUserRole === "Admin";

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

  return (
    <div
      style={{
        padding: 12,
        background: "#f9f9f9",
        border: "1px solid #ddd",
        borderRadius: 4,
        marginBottom: 12
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
            disabled={isSubmitting}
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
              disabled={isSubmitting || !editText.trim()}
              style={{
                padding: "6px 12px",
                background: isSubmitting || !editText.trim() ? "#ccc" : "#4caf50",
                color: "white",
                border: "none",
                borderRadius: 4,
                cursor: isSubmitting || !editText.trim() ? "not-allowed" : "pointer",
                fontSize: 13
              }}
            >
              {isSubmitting ? "Salvataggio..." : "Salva"}
            </button>
            <button
              onClick={handleCancelEdit}
              disabled={isSubmitting}
              style={{
                padding: "6px 12px",
                background: "#666",
                color: "white",
                border: "none",
                borderRadius: 4,
                cursor: isSubmitting ? "not-allowed" : "pointer",
                fontSize: 13
              }}
            >
              Annulla
            </button>
          </div>
        </div>
      ) : (
        <div>
          <p style={{ margin: "0 0 8px 0", fontSize: 14, lineHeight: 1.5 }}>{comment.commentText}</p>
          {(canEdit || canDelete) && (
            <div style={{ display: "flex", gap: 8, marginTop: 8 }}>
              {canEdit && (
                <button
                  onClick={() => setIsEditing(true)}
                  disabled={isSubmitting}
                  style={{
                    padding: "4px 10px",
                    background: "#0070f3",
                    color: "white",
                    border: "none",
                    borderRadius: 4,
                    cursor: "pointer",
                    fontSize: 12
                  }}
                >
                  Modifica
                </button>
              )}
              {canDelete && (
                <button
                  onClick={handleDelete}
                  disabled={isSubmitting}
                  style={{
                    padding: "4px 10px",
                    background: "#d93025",
                    color: "white",
                    border: "none",
                    borderRadius: 4,
                    cursor: isSubmitting ? "not-allowed" : "pointer",
                    fontSize: 12
                  }}
                >
                  {isSubmitting ? "Eliminazione..." : "Elimina"}
                </button>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
