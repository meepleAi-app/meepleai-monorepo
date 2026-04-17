/**
 * OwnershipConfirmDialog - Copyright-aware ownership confirmation
 *
 * Displayed when a user clicks the "game" ManaPip on a catalog card.
 * Confirms that the user owns a physical copy before granting access
 * to the Knowledge Base (RAG rulebook content). Ownership is required
 * for copyright compliance.
 *
 * Uses Radix AlertDialog so the dialog cannot be dismissed by clicking
 * outside — the user must make an explicit choice.
 */

'use client';

import React from 'react';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';

// ============================================================================
// Types
// ============================================================================

export interface OwnershipConfirmDialogProps {
  /** Whether the dialog is open */
  open: boolean;
  /** Callback to control open state */
  onOpenChange: (open: boolean) => void;
  /** Title of the game being added to the library */
  gameTitle: string;
  /** Callback when user confirms ownership */
  onConfirm: () => void;
  /** Whether the add-to-library operation is in progress */
  confirming: boolean;
}

// ============================================================================
// Component
// ============================================================================

/**
 * Asks the user to confirm physical ownership before adding a game to their
 * library and enabling Knowledge Base access.
 *
 * @example
 * ```tsx
 * <OwnershipConfirmDialog
 *   open={isOpen}
 *   onOpenChange={setIsOpen}
 *   gameTitle="Wingspan"
 *   onConfirm={handleAddToLibrary}
 *   confirming={isAdding}
 * />
 * ```
 */
export function OwnershipConfirmDialog({
  open,
  onOpenChange,
  gameTitle,
  onConfirm,
  confirming,
}: OwnershipConfirmDialogProps) {
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent className="sm:max-w-md">
        <AlertDialogHeader>
          <AlertDialogTitle className="font-quicksand flex items-center gap-2 text-xl">
            <span aria-hidden="true">🎲</span>
            <span>{gameTitle}</span>
          </AlertDialogTitle>
          <AlertDialogDescription className="text-sm leading-relaxed">
            Per accedere alla Knowledge Base di questo gioco (regolamento, FAQ, strategie), conferma
            di possederne una copia fisica. Questo ci permette di offrirti il contenuto nel rispetto
            del copyright.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter className="flex-col gap-2 sm:flex-row sm:gap-2">
          <AlertDialogCancel disabled={confirming}>Annulla</AlertDialogCancel>
          <AlertDialogAction
            onClick={e => {
              // Prevent default close so the caller controls dialog state
              e.preventDefault();
              onConfirm();
            }}
            disabled={confirming}
            className="bg-amber-600 text-white hover:bg-amber-700 focus-visible:ring-amber-600"
          >
            {confirming ? 'Aggiunta in corso...' : 'Possiedo il gioco'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
