import { useState } from "react";

interface CommentFormProps {
  onSubmit: (commentText: string, atomId: string | null) => Promise<void>;
  atomId?: string | null;
  placeholder?: string;
}

export function CommentForm({ onSubmit, atomId = null, placeholder = "Scrivi un commento..." }: CommentFormProps) {
  const [commentText, setCommentText] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!commentText.trim()) {
      return;
    }

    setIsSubmitting(true);
    try {
      await onSubmit(commentText, atomId);
      setCommentText("");
    } catch (error) {
      console.error("Failed to create comment:", error);
      alert("Impossibile creare il commento");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} style={{ marginBottom: 16 }}>
      <textarea
        value={commentText}
        onChange={(e) => setCommentText(e.target.value)}
        placeholder={placeholder}
        disabled={isSubmitting}
        style={{
          width: "100%",
          minHeight: 80,
          padding: 12,
          border: "1px solid #ccc",
          borderRadius: 4,
          fontSize: 14,
          fontFamily: "inherit",
          marginBottom: 8,
          resize: "vertical"
        }}
      />
      <button
        type="submit"
        disabled={isSubmitting || !commentText.trim()}
        style={{
          padding: "8px 16px",
          background: isSubmitting || !commentText.trim() ? "#ccc" : "#0070f3",
          color: "white",
          border: "none",
          borderRadius: 4,
          cursor: isSubmitting || !commentText.trim() ? "not-allowed" : "pointer",
          fontSize: 14
        }}
      >
        {isSubmitting ? "Invio in corso..." : "Aggiungi Commento"}
      </button>
    </form>
  );
}
