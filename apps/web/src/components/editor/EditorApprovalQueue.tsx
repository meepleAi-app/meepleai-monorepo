/**
 * Editor Approval Queue Component (Issue #2896)
 *
 * Container for approval queue with bulk selection and actions.
 * Features:
 * - Individual item checkboxes
 * - Floating action bar when items selected
 * - Bulk approve/reject with confirmation
 * - Progress tracking during operations
 */

'use client';

import React, { useState, useCallback } from 'react';

import { Check, X } from 'lucide-react';
import { toast } from 'sonner';

import { BulkActionBar, type BulkAction } from '@/components/admin/BulkActionBar';
import { api } from '@/lib/api';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

import { BulkRejectDialog } from './BulkRejectDialog';
import { EditorApprovalQueueItem } from './EditorApprovalQueueItem';

export interface EditorApprovalQueueProps {
  /**
   * List of games pending approval
   */
  games: SharedGame[];

  /**
   * Callback when Review button is clicked
   */
  onReview: (gameId: string) => void;

  /**
   * Callback when individual Approve is clicked
   */
  onApprove: (gameId: string) => void;

  /**
   * Callback when individual Reject is clicked
   */
  onReject: (gameId: string) => void;

  /**
   * Callback after bulk operations complete (for refetching data)
   */
  onBulkComplete?: () => void;

  /**
   * Optional CSS class name
   */
  className?: string;

  /**
   * Test ID for automated testing
   */
  'data-testid'?: string;
}

/**
 * EditorApprovalQueue Component
 *
 * Manages approval queue with bulk selection and operations.
 * Integrates EditorApprovalQueueItem with BulkActionBar.
 */
export function EditorApprovalQueue({
  games,
  onReview,
  onApprove,
  onReject,
  onBulkComplete,
  className,
  'data-testid': testId = 'editor-approval-queue',
}: EditorApprovalQueueProps) {
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [isRejectDialogOpen, setIsRejectDialogOpen] = useState(false);
  const [isBulkApproving, setIsBulkApproving] = useState(false);
  const [isBulkRejecting, setIsBulkRejecting] = useState(false);

  // Selection handlers
  const toggleSelection = useCallback((gameId: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(gameId)) {
        next.delete(gameId);
      } else {
        next.add(gameId);
      }
      return next;
    });
  }, []);

  const clearSelection = useCallback(() => {
    setSelectedIds(new Set());
  }, []);

  // Bulk approve handler
  const handleBulkApprove = useCallback(async () => {
    if (selectedIds.size === 0) return;

    setIsBulkApproving(true);
    const selectedGames = Array.from(selectedIds);

    try {
      // Execute approvals in parallel
      const results = await Promise.allSettled(
        selectedGames.map((gameId) => api.sharedGames.approvePublication(gameId))
      );

      // Count successes and failures
      const successCount = results.filter((r) => r.status === 'fulfilled').length;
      const failureCount = results.filter((r) => r.status === 'rejected').length;

      // Show notifications
      if (successCount > 0) {
        toast.success(
          `${successCount} ${successCount === 1 ? 'gioco approvato' : 'giochi approvati'} con successo`
        );
      }

      if (failureCount > 0) {
        toast.error(
          `${failureCount} ${failureCount === 1 ? 'gioco non è stato approvato' : 'giochi non sono stati approvati'}`
        );
      }

      // Clear selection and trigger refresh
      clearSelection();
      onBulkComplete?.();
    } catch (error) {
      toast.error('Errore durante l\'approvazione multipla');
      console.error('Bulk approve error:', error);
    } finally {
      setIsBulkApproving(false);
    }
  }, [selectedIds, clearSelection, onBulkComplete]);

  // Bulk reject handler (triggered after dialog confirmation)
  const handleBulkRejectConfirm = useCallback(
    async (reason: string) => {
      if (selectedIds.size === 0) return;

      setIsBulkRejecting(true);
      const selectedGames = Array.from(selectedIds);

      try {
        // Execute rejections in parallel
        const results = await Promise.allSettled(
          selectedGames.map((gameId) => api.sharedGames.rejectPublication(gameId, reason))
        );

        // Count successes and failures
        const successCount = results.filter((r) => r.status === 'fulfilled').length;
        const failureCount = results.filter((r) => r.status === 'rejected').length;

        // Show notifications
        if (successCount > 0) {
          toast.success(
            `${successCount} ${successCount === 1 ? 'gioco rifiutato' : 'giochi rifiutati'} con successo`
          );
        }

        if (failureCount > 0) {
          toast.error(
            `${failureCount} ${failureCount === 1 ? 'gioco non è stato rifiutato' : 'giochi non sono stati rifiutati'}`
          );
        }

        // Clear selection, close dialog, trigger refresh
        clearSelection();
        setIsRejectDialogOpen(false);
        onBulkComplete?.();
      } catch (error) {
        toast.error('Errore durante il rifiuto multiplo');
        console.error('Bulk reject error:', error);
      } finally {
        setIsBulkRejecting(false);
      }
    },
    [selectedIds, clearSelection, onBulkComplete]
  );

  // Configure bulk actions
  const bulkActions: BulkAction[] = [
    {
      id: 'bulk-approve',
      label: 'Approva',
      icon: Check,
      variant: 'default',
      onClick: handleBulkApprove,
      disabled: isBulkApproving || isBulkRejecting,
      className: 'border-green-200 text-green-700 hover:bg-green-50 hover:border-green-300',
    },
    {
      id: 'bulk-reject',
      label: 'Rifiuta',
      icon: X,
      variant: 'outline',
      onClick: () => setIsRejectDialogOpen(true),
      disabled: isBulkApproving || isBulkRejecting,
      className: 'border-red-200 text-red-700 hover:bg-red-50 hover:border-red-300',
    },
  ];

  return (
    <>
      <div className={className} data-testid={testId}>
        {/* Queue Items */}
        <div className="space-y-3">
          {games.map((game) => (
            <EditorApprovalQueueItem
              key={game.id}
              game={game}
              selected={selectedIds.has(game.id)}
              onToggleSelect={toggleSelection}
              onReview={onReview}
              onApprove={onApprove}
              onReject={onReject}
            />
          ))}
        </div>

        {/* Floating Action Bar - Shows when items selected */}
        {selectedIds.size > 0 && (
          <div className="fixed bottom-4 left-1/2 -translate-x-1/2 z-50">
            <BulkActionBar
              selectedCount={selectedIds.size}
              totalCount={games.length}
              actions={bulkActions}
              onClearSelection={clearSelection}
              variant="floating"
              itemLabel="giochi"
              itemLabelSingular="gioco"
              testId={`${testId}-bulk-action-bar`}
            />
          </div>
        )}
      </div>

      {/* Bulk Reject Confirmation Dialog */}
      <BulkRejectDialog
        open={isRejectDialogOpen}
        onOpenChange={setIsRejectDialogOpen}
        selectedCount={selectedIds.size}
        onConfirm={handleBulkRejectConfirm}
        isLoading={isBulkRejecting}
      />
    </>
  );
}

EditorApprovalQueue.displayName = 'EditorApprovalQueue';
