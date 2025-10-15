import { useCallback, useEffect, useState } from "react";
import { api, type RuleSpecComment, type RuleSpecCommentsResponse } from "../lib/api";
import { CommentItem } from "./CommentItem";
import { CommentForm } from "./CommentForm";

interface CommentThreadProps {
  gameId: string;
  version: string;
  currentUserId: string;
  currentUserRole: string;
}

export function CommentThread({ gameId, version, currentUserId, currentUserRole }: CommentThreadProps) {
  const [comments, setComments] = useState<RuleSpecComment[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string>("");

  const loadComments = useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const response = await api.ruleSpecComments.getComments(gameId, version);
      if (response) {
        setComments(response.comments);
      }
    } catch (err: any) {
      console.error("Failed to load comments:", err);
      setError(err?.message || "Impossibile caricare i commenti");
    } finally {
      setIsLoading(false);
    }
  }, [gameId, version]);

  useEffect(() => {
    void loadComments();
  }, [loadComments]);

  const handleCreateComment = async (commentText: string, atomId: string | null) => {
    await api.ruleSpecComments.createComment(gameId, version, {
      atomId,
      commentText
    });
    await loadComments();
  };

  const handleEditComment = async (commentId: string, newText: string) => {
    await api.ruleSpecComments.updateComment(gameId, commentId, {
      commentText: newText
    });
    await loadComments();
  };

  const handleDeleteComment = async (commentId: string) => {
    await api.ruleSpecComments.deleteComment(gameId, commentId);
    await loadComments();
  };

  // Only editors and admins can create comments
  const canComment = currentUserRole === "Admin" || currentUserRole === "Editor";

  return (
    <div style={{ marginTop: 24 }}>
      <h3 style={{ marginBottom: 16 }}>
        Commenti ({comments.length})
      </h3>

      {error && (
        <div style={{ padding: 12, background: "#fce4e4", border: "1px solid #d93025", borderRadius: 4, marginBottom: 16 }}>
          {error}
        </div>
      )}

      {canComment && (
        <CommentForm
          onSubmit={handleCreateComment}
          placeholder="Aggiungi un commento su questa versione..."
        />
      )}

      {isLoading ? (
        <p style={{ color: "#666", fontSize: 14 }}>Caricamento commenti...</p>
      ) : comments.length === 0 ? (
        <p style={{ color: "#999", fontSize: 14, fontStyle: "italic" }}>
          Nessun commento ancora. {canComment && "Sii il primo a commentare!"}
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
            />
          ))}
        </div>
      )}
    </div>
  );
}
