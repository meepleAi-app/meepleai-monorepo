/**
 * LedgerTimelineRollback Hook
 * Issue #2422: Ledger Mode History Timeline
 *
 * Rollback logic with confirmation dialog for restoring previous states.
 */

'use client';

import { useState } from 'react';

import { ConfirmationDialog } from '@/components/ui/overlays/confirmation-dialog';
import type { GameStateSnapshot } from '@/types/game-state';

interface UseRollbackOptions {
  onConfirm: (snapshotId: string) => void;
}

export function useRollback({ onConfirm }: UseRollbackOptions) {
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [selectedSnapshot, setSelectedSnapshot] = useState<GameStateSnapshot | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const initiateRollback = (snapshot: GameStateSnapshot) => {
    setSelectedSnapshot(snapshot);
    setIsDialogOpen(true);
  };

  const handleConfirm = async () => {
    if (!selectedSnapshot) return;

    setIsLoading(true);
    try {
      await onConfirm(selectedSnapshot.id);
      setIsDialogOpen(false);
      setSelectedSnapshot(null);
    } catch (error) {
      console.error('Rollback failed:', error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleClose = () => {
    if (!isLoading) {
      setIsDialogOpen(false);
      setSelectedSnapshot(null);
    }
  };

  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  };

  const RollbackDialog = () => (
    <ConfirmationDialog
      isOpen={isDialogOpen}
      onClose={handleClose}
      onConfirm={handleConfirm}
      title="Restore Previous State?"
      message={
        selectedSnapshot
          ? `This will restore the game state from ${formatTimestamp(selectedSnapshot.timestamp)}. Current unsaved changes will be lost.`
          : ''
      }
      confirmText="Restore"
      cancelText="Cancel"
      variant="warning"
      isLoading={isLoading}
    />
  );

  return {
    initiateRollback,
    RollbackDialog,
    isDialogOpen,
    isLoading,
  };
}
