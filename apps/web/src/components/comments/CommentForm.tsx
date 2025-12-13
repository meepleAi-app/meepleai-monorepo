import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

interface CommentFormProps {
  onSubmit: (commentText: string, atomId: string | null) => Promise<void>;
  atomId?: string | null;
  placeholder?: string;
  disabled?: boolean;
}

export function CommentForm({
  onSubmit,
  atomId = null,
  placeholder,
  disabled = false,
}: CommentFormProps) {
  const t = useTranslations('comments');
  const [commentText, setCommentText] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isDisabled = disabled || isSubmitting;

  // Use provided placeholder or i18n default
  const effectivePlaceholder = placeholder || t('placeholder');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!commentText.trim()) {
      return;
    }

    setIsSubmitting(true);
    try {
      await onSubmit(commentText, atomId);
      setCommentText('');
    } catch (error) {
      logger.error(
        'Failed to create comment',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('CommentForm', 'handleSubmit', { atomId })
      );
      alert(t('createError'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="mb-4">
      <textarea
        value={commentText}
        onChange={e => setCommentText(e.target.value)}
        placeholder={effectivePlaceholder}
        disabled={isDisabled}
        className="w-full min-h-20 p-3 border border-gray-300 rounded text-sm font-inherit mb-2 resize-vertical"
      />
      <button
        type="submit"
        disabled={isDisabled || !commentText.trim()}
        className={`px-4 py-2 rounded text-sm font-medium transition-colors ${
          isDisabled || !commentText.trim()
            ? 'bg-gray-300 text-white cursor-not-allowed'
            : 'bg-blue-600 text-white hover:bg-blue-700 cursor-pointer'
        }`}
      >
        {isSubmitting ? t('submitting') : t('addComment')}
      </button>
    </form>
  );
}
