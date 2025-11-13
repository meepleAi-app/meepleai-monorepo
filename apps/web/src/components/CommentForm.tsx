import { useState } from "react";

interface CommentFormProps {
  onSubmit: (commentText: string, atomId: string | null) => Promise<void>;
  atomId?: string | null;
  placeholder?: string;
  disabled?: boolean;
}

export function CommentForm({
  onSubmit,
  atomId = null,
  placeholder = "Scrivi un commento...",
  disabled = false
}: CommentFormProps) {
  const [commentText, setCommentText] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isDisabled = disabled || isSubmitting;

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
    <form onSubmit={handleSubmit} className="mb-4">
      <textarea
        value={commentText}
        onChange={(e) => setCommentText(e.target.value)}
        placeholder={placeholder}
        disabled={isDisabled}
        className="w-full min-h-20 p-3 border border-gray-300 rounded text-sm font-inherit mb-2 resize-vertical"
      />
      <button
        type="submit"
        disabled={isDisabled || !commentText.trim()}
        className={`px-4 py-2 rounded text-sm font-medium transition-colors ${
          isDisabled || !commentText.trim()
            ? "bg-gray-300 text-white cursor-not-allowed"
            : "bg-blue-600 text-white hover:bg-blue-700 cursor-pointer"
        }`}
      >
        {isSubmitting ? "Invio in corso..." : "Aggiungi Commento"}
      </button>
    </form>
  );
}
