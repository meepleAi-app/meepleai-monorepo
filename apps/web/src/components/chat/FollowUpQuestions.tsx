import { FC } from 'react';

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
      className="mt-3 pt-3 border-t border-border/50 dark:border-border/30"
    >
      <div className="text-xs font-medium mb-2 text-muted-foreground">Domande suggerite:</div>
      <div className="flex flex-wrap gap-2">
        {questions.map(question => (
          <button
            key={question}
            onClick={() => onQuestionClick(question)}
            disabled={disabled}
            aria-label={`Ask follow-up question: ${question}`}
            className={`px-3 py-1.5 rounded-full text-xs whitespace-nowrap max-w-full overflow-hidden text-ellipsis transition-all duration-200 ${
              disabled
                ? 'bg-muted text-muted-foreground/50 cursor-not-allowed'
                : 'bg-card/90 backdrop-blur-[8px] dark:bg-card dark:backdrop-blur-none text-blue-600 dark:text-blue-400 border border-border/50 dark:border-border/70 hover:bg-blue-50 dark:hover:bg-blue-500/10 hover:border-blue-600 cursor-pointer'
            }`}
          >
            {question}
          </button>
        ))}
      </div>
    </div>
  );
};
