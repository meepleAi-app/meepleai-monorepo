'use client';

import * as React from 'react';

import { showSuccessToast } from '@/lib/toastUtils';
import { useChatStore } from '@/store/chat/store';

import { useConfirmDialog } from './useConfirmDialog';

/**
 * useThreadDeletion - Shared hook for thread deletion with confirmation
 *
 * Provides consistent thread deletion behavior across the application.
 * Handles confirmation dialog, Zustand store integration, and success notifications.
 *
 * Features:
 * - Promise-based confirmation dialog
 * - Automatic Zustand store integration
 * - Success toast notification
 * - Error handling via store error state
 *
 * @example
 * ```tsx
 * function ChatPage() {
 *   const { handleThreadDelete, ConfirmDialogComponent } = useThreadDeletion()
 *
 *   const onDelete = () => {
 *     void handleThreadDelete(activeChatId)
 *   }
 *
 *   return (
 *     <>
 *       <button onClick={onDelete}>Delete</button>
 *       <ConfirmDialogComponent />
 *     </>
 *   )
 * }
 * ```
 *
 * @returns Object containing:
 * - handleThreadDelete: Async function that shows confirmation and deletes thread
 * - ConfirmDialogComponent: React component to render in JSX
 *
 * @see Issue #2258 - Thread deletion with confirmation dialog
 */
export function useThreadDeletion() {
  const { confirm, ConfirmDialogComponent } = useConfirmDialog();
  const deleteChat = useChatStore(state => state.deleteChat);

  /**
   * Handle thread deletion with confirmation
   * @param chatId - ID of the chat thread to delete
   */
  const handleThreadDelete = React.useCallback(
    async (chatId: string): Promise<void> => {
      if (!chatId) {
        return;
      }

      // Show confirmation dialog
      const confirmed = await confirm({
        title: 'Elimina thread?',
        message:
          'Questa azione non può essere annullata. Tutti i messaggi in questo thread verranno eliminati permanentemente.',
        variant: 'destructive',
        confirmText: 'Elimina',
        cancelText: 'Annulla',
      });

      if (!confirmed) {
        return;
      }

      // Delete thread via Zustand store
      // Note: deleteChat handles errors internally and sets error state
      await deleteChat(chatId);

      // Show success notification
      // If there was an error, the store's error state will be set
      // and can be handled by error boundaries or UI error displays
      showSuccessToast('Thread eliminato con successo');
    },
    [confirm, deleteChat]
  );

  return {
    handleThreadDelete,
    ConfirmDialogComponent,
  };
}
