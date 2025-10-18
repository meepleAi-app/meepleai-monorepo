import { FC } from "react";

interface FollowUpQuestionsProps {
  questions: string[];
  onQuestionClick: (question: string) => void;
  disabled?: boolean;
}

/**
 * CHAT-02: Follow-up Questions Component
 *
 * Displays AI-generated follow-up questions as clickable pill-style buttons.
 * Clicking a question populates the chat input with the question text.
 */
export const FollowUpQuestions: FC<FollowUpQuestionsProps> = ({
  questions,
  onQuestionClick,
  disabled = false,
}) => {
  if (!questions || questions.length === 0) {
    return null;
  }

  return (
    <div
      role="region"
      aria-label="Suggested follow-up questions"
      style={{
        marginTop: 12,
        paddingTop: 12,
        borderTop: "1px solid #dadce0",
      }}
    >
      <div
        style={{
          fontSize: 12,
          fontWeight: 500,
          marginBottom: 8,
          color: "#64748b",
        }}
      >
        Domande suggerite:
      </div>
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: 8,
        }}
      >
        {questions.map((question, idx) => (
          <button
            key={idx}
            onClick={() => onQuestionClick(question)}
            disabled={disabled}
            aria-label={`Ask follow-up question: ${question}`}
            style={{
              padding: "6px 12px",
              background: disabled ? "#f1f3f4" : "#ffffff",
              color: disabled ? "#9ca3af" : "#1a73e8",
              border: "1px solid #dadce0",
              borderRadius: 16,
              fontSize: 13,
              cursor: disabled ? "not-allowed" : "pointer",
              transition: "all 0.2s",
              whiteSpace: "nowrap",
              maxWidth: "100%",
              overflow: "hidden",
              textOverflow: "ellipsis",
            }}
            onMouseEnter={(e) => {
              if (!disabled) {
                e.currentTarget.style.background = "#e8f0fe";
                e.currentTarget.style.borderColor = "#1a73e8";
              }
            }}
            onMouseLeave={(e) => {
              if (!disabled) {
                e.currentTarget.style.background = "#ffffff";
                e.currentTarget.style.borderColor = "#dadce0";
              }
            }}
          >
            {question}
          </button>
        ))}
      </div>
    </div>
  );
};
