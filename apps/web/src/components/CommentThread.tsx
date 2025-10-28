import { useCallback, useEffect, useState } from "react";
import { api, type RuleSpecComment, type RuleSpecCommentsResponse } from "../lib/api";
import { CommentItem } from "./CommentItem";
import { CommentForm } from "./CommentForm";

interface CommentThreadProps {
  gameId: string;
  version: string;
  currentUserId: string;
  currentUserRole: string;
  lineNumber?: number | null; // Optional line filtering for inline annotations
}

export function CommentThread({
  gameId,
  version,
  currentUserId,
  currentUserRole,
  lineNumber = null
}: CommentThreadProps) {
  const [comments, setComments] = useState<RuleSpecComment[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string>("");
  const [includeResolved, setIncludeResolved] = useState(true);

  const loadComments = useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const response = await api.ruleSpecComments.getComments(gameId, version, includeResolved);
      if (response) {
        // Filter by line number if specified
        const filteredComments = lineNumber !== null
          ? response.comments.filter(c => c.lineNumber === lineNumber)
          : response.comments;
        setComments(filteredComments);
      }
    } catch (err: any) {
      console.error("Failed to load comments:", err);
      setError(err?.message || "Impossibile caricare i commenti");
    } finally {
      setIsLoading(false);
    }
  }, [gameId, version, includeResolved, lineNumber]);

  useEffect(() => {
    void loadComments();
  }, [loadComments]);

  const handleCreateComment = async (commentText: string, atomId: string | null) => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError("");
    try {
      await api.ruleSpecComments.createComment(gameId, version, {
        atomId,
        lineNumber: lineNumber,
        commentText
      });
      await loadComments();
    } catch (err: any) {
      console.error("Failed to create comment:", err);
      setError(err?.message || "Impossibile creare il commento");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleEditComment = async (commentId: string, newText: string) => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError("");
    try {
      await api.ruleSpecComments.updateComment(gameId, commentId, {
        commentText: newText
      });
      await loadComments();
    } catch (err: any) {
      console.error("Failed to update comment:", err);
      setError(err?.message || "Impossibile modificare il commento");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteComment = async (commentId: string) => {
    if (isSubmitting) return;
    if (!confirm("Sei sicuro di voler eliminare questo commento?")) {
      return;
    }
    setIsSubmitting(true);
    setError("");
    try {
      await api.ruleSpecComments.deleteComment(gameId, commentId);
      await loadComments();
    } catch (err: any) {
      console.error("Failed to delete comment:", err);
      setError(err?.message || "Impossibile eliminare il commento");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleReply = async (parentCommentId: string, replyText: string) => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError("");
    try {
      await api.ruleSpecComments.createReply(parentCommentId, {
        commentText: replyText
      });
      await loadComments();
    } catch (err: any) {
      console.error("Failed to create reply:", err);
      setError(err?.message || "Impossibile creare la risposta");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResolve = async (commentId: string) => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError("");
    try {
      await api.ruleSpecComments.resolveComment(commentId);
      await loadComments();
    } catch (err: any) {
      console.error("Failed to resolve comment:", err);
      setError(err?.message || "Impossibile risolvere il commento");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleUnresolve = async (commentId: string) => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError("");
    try {
      await api.ruleSpecComments.unresolveComment(commentId);
      await loadComments();
    } catch (err: any) {
      console.error("Failed to unresolve comment:", err);
      setError(err?.message || "Impossibile riaprire il commento");
    } finally {
      setIsSubmitting(false);
    }
  };

  // Only editors and admins can create comments
  const canComment = currentUserRole === "Admin" || currentUserRole === "Editor";

  return (
    <div style={{ marginTop: 24 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
        <h3 style={{ margin: 0 }}>
          Commenti ({comments.length})
        </h3>

        <label style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 14, cursor: "pointer" }}>
          <input
            type="checkbox"
            checked={includeResolved}
            onChange={(e) => setIncludeResolved(e.target.checked)}
            style={{ cursor: "pointer" }}
          />
          <span>Mostra commenti risolti</span>
        </label>
      </div>

      {lineNumber !== null && (
        <div style={{
          marginBottom: 16,
          padding: 12,
          background: "#fff3cd",
          border: "1px solid #ffc107",
          borderRadius: 4,
          fontSize: 14
        }}>
          Commenti sulla riga {lineNumber}
        </div>
      )}

      {error && (
        <div style={{ padding: 12, background: "#fce4e4", border: "1px solid #d93025", borderRadius: 4, marginBottom: 16 }}>
          {error}
        </div>
      )}

      {canComment && (
        <CommentForm
          onSubmit={handleCreateComment}
          placeholder={
            lineNumber !== null
              ? `Aggiungi un commento sulla riga ${lineNumber}...`
              : "Aggiungi un commento su questa versione..."
          }
          disabled={isSubmitting}
        />
      )}

      {isLoading ? (
        <p style={{ color: "#666", fontSize: 14 }}>Caricamento commenti...</p>
      ) : comments.length === 0 ? (
        <p style={{ color: "#999", fontSize: 14, fontStyle: "italic" }}>
          {lineNumber !== null
            ? `Nessun commento su questa riga. ${canComment ? "Sii il primo a commentare!" : ""}`
            : `Nessun commento ancora. ${canComment ? "Sii il primo a commentare!" : ""}`
          }
        </p>
      ) : (
        <div>
          {comments.map((comment) => (
            <CommentItem
              key={comment.id}
              comment={comment}
              currentUserId={currentUserId}
              currentUserRole={currentUserRole}
              onEdit={handleEditComment}
              onDelete={handleDeleteComment}
              onReply={handleReply}
              onResolve={handleResolve}
              onUnresolve={handleUnresolve}
              disabled={isSubmitting}
            />
          ))}
        </div>
      )}
    </div>
  );
}
