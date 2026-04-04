import { useCallback, useEffect, useState } from 'react';

import { api, type RuleSpecComment } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';
import { getErrorMessage } from '@/lib/utils/errorHandler';

import { CommentForm } from './CommentForm';
import { CommentItem } from './CommentItem';

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
  lineNumber = null,
}: CommentThreadProps) {
  const [comments, setComments] = useState<RuleSpecComment[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string>('');
  const [includeResolved, setIncludeResolved] = useState(true);

  const loadComments = useCallback(async () => {
    setIsLoading(true);
    setError('');
    try {
      const response = await api.chat.getRuleSpecComments(gameId, version, includeResolved);
      if (response) {
        // Filter by line number if specified
        const filteredComments =
          lineNumber !== null
            ? response.comments.filter(c => c.lineNumber === lineNumber)
            : response.comments;
        setComments(filteredComments);
      }
    } catch (err) {
      logger.error(
        'Failed to load comments',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('CommentThread', 'loadComments', { gameId, version, lineNumber })
      );
      setError(getErrorMessage(err, 'Impossibile caricare i commenti'));
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
    setError('');
    try {
      await api.chat.createRuleSpecComment(gameId, version, {
        atomId,
        lineNumber: lineNumber,
        commentText,
      });
      await loadComments();
    } catch (err) {
      logger.error(
        'Failed to create comment',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('CommentThread', 'handleCreateComment', { gameId, version, lineNumber })
      );
      setError(getErrorMessage(err, 'Impossibile creare il commento'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleEditComment = async (commentId: string, newText: string) => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError('');
    try {
      await api.chat.updateRuleSpecComment(gameId, commentId, {
        commentText: newText,
      });
      await loadComments();
    } catch (err) {
      logger.error(
        'Failed to update comment',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('CommentThread', 'handleEditComment', { gameId, commentId })
      );
      setError(getErrorMessage(err, 'Impossibile modificare il commento'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteComment = async (commentId: string) => {
    if (isSubmitting) return;
    if (!confirm('Sei sicuro di voler eliminare questo commento?')) {
      return;
    }
    setIsSubmitting(true);
    setError('');
    try {
      await api.chat.deleteRuleSpecComment(gameId, commentId);
      await loadComments();
    } catch (err) {
      logger.error(
        'Failed to delete comment',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('CommentThread', 'handleDeleteComment', { gameId, commentId })
      );
      setError(getErrorMessage(err, 'Impossibile eliminare il commento'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleReply = async (parentCommentId: string, replyText: string) => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError('');
    try {
      await api.chat.createCommentReply(parentCommentId, {
        commentText: replyText,
      });
      await loadComments();
    } catch (err) {
      logger.error(
        'Failed to create reply',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('CommentThread', 'handleReply', { parentCommentId })
      );
      setError(getErrorMessage(err, 'Impossibile creare la risposta'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResolve = async (commentId: string) => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError('');
    try {
      await api.chat.resolveComment(commentId);
      await loadComments();
    } catch (err) {
      logger.error(
        'Failed to resolve comment',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('CommentThread', 'handleResolve', { commentId })
      );
      setError(getErrorMessage(err, 'Impossibile risolvere il commento'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleUnresolve = async (commentId: string) => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setError('');
    try {
      await api.chat.unresolveComment(commentId);
      await loadComments();
    } catch (err) {
      logger.error(
        'Failed to unresolve comment',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('CommentThread', 'handleUnresolve', { commentId })
      );
      setError(getErrorMessage(err, 'Impossibile riaprire il commento'));
    } finally {
      setIsSubmitting(false);
    }
  };

  // Only editors and admins can create comments (normalize for case-insensitive comparison)
  const normalizedRole = currentUserRole?.toLowerCase();
  const canComment =
    normalizedRole === 'admin' || normalizedRole === 'superadmin' || normalizedRole === 'editor';

  return (
    <div className="mt-6" data-testid="comment-thread">
      {/* Hidden test data elements for test compatibility */}
      <div data-testid="comment-game-id" style={{ display: 'none' }}>
        {gameId}
      </div>
      <div data-testid="comment-version" style={{ display: 'none' }}>
        {version}
      </div>
      <div data-testid="comment-user-id" style={{ display: 'none' }}>
        {currentUserId}
      </div>
      <div data-testid="comment-user-role" style={{ display: 'none' }}>
        {currentUserRole}
      </div>

      <div className="flex justify-between items-center mb-4">
        <h3 className="m-0">Commenti ({comments.length})</h3>

        <label className="flex items-center gap-2 text-sm cursor-pointer">
          <input
            type="checkbox"
            checked={includeResolved}
            onChange={e => setIncludeResolved(e.target.checked)}
            className="cursor-pointer"
          />
          <span>Mostra commenti risolti</span>
        </label>
      </div>

      {lineNumber !== null && (
        <div className="mb-4 p-3 bg-amber-50 border border-amber-400 rounded text-sm">
          Commenti sulla riga {lineNumber}
        </div>
      )}

      {error && <div className="p-3 bg-red-50 border border-red-600 rounded mb-4">{error}</div>}

      {canComment && (
        <CommentForm
          onSubmit={handleCreateComment}
          placeholder={
            lineNumber !== null
              ? `Aggiungi un commento sulla riga ${lineNumber}...`
              : 'Aggiungi un commento su questa versione...'
          }
          disabled={isSubmitting}
        />
      )}

      {isLoading ? (
        <p className="text-gray-600 text-sm">Caricamento commenti...</p>
      ) : comments.length === 0 ? (
        <p className="text-gray-600 text-sm italic">
          {lineNumber !== null
            ? `Nessun commento su questa riga. ${canComment ? 'Sii il primo a commentare!' : ''}`
            : `Nessun commento ancora. ${canComment ? 'Sii il primo a commentare!' : ''}`}
        </p>
      ) : (
        <div>
          {comments.map(comment => (
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
