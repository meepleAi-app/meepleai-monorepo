'use client';

/**
 * Duplicate Warning Dialog
 * Issue #4167: Duplicate warning modal
 *
 * Shows a warning when importing a game that already exists in the catalog.
 * Allows user to choose: Cancel, Replace Existing, or Create Anyway.
 */

import type { JSX } from 'react';

import { AlertTriangle } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import type { SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

export interface DuplicateWarningDialogProps {
  /** Whether the dialog is open */
  open: boolean;
  /** Existing game in catalog */
  existingGame: SharedGameDetail | null;
  /** New game title being imported */
  newGameTitle: string;
  /** BGG ID */
  bggId: number;
  /** Callback when user cancels */
  onCancel: () => void;
  /** Callback when user chooses to replace existing */
  onReplace: () => void;
  /** Callback when user chooses to create anyway */
  onCreateAnyway: () => void;
}

export function DuplicateWarningDialog({
  open,
  existingGame,
  newGameTitle,
  bggId,
  onCancel,
  onReplace,
  onCreateAnyway,
}: DuplicateWarningDialogProps): JSX.Element {
  return (
    <Dialog open={open} onOpenChange={(isOpen: boolean) => !isOpen && onCancel()}>
      <DialogContent className="sm:max-w-[600px]">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-amber-100 dark:bg-amber-950">
              <AlertTriangle className="h-5 w-5 text-amber-600 dark:text-amber-400" />
            </div>
            <div>
              <DialogTitle>Duplicate Game Detected</DialogTitle>
              <DialogDescription>
                Un gioco con ID {bggId} esiste già nel catalogo.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        {/* Comparison View */}
        <div className="grid grid-cols-2 gap-4">
          {/* Existing Game */}
          <div className="rounded-md border bg-muted/50 p-4">
            <h4 className="mb-2 text-sm font-semibold text-muted-foreground">Existing Game</h4>
            <div className="space-y-1">
              <p className="text-sm font-medium">{existingGame?.title || 'Unknown'}</p>
              {existingGame?.yearPublished && (
                <p className="text-xs text-muted-foreground">Year: {existingGame.yearPublished}</p>
              )}
              {existingGame?.minPlayers && existingGame?.maxPlayers && (
                <p className="text-xs text-muted-foreground">
                  Players: {existingGame.minPlayers}-{existingGame.maxPlayers}
                </p>
              )}
              {existingGame?.minAge && (
                <p className="text-xs text-muted-foreground">Age: {existingGame.minAge}+</p>
              )}
            </div>
          </div>

          {/* New Game */}
          <div className="rounded-md border bg-primary/5 p-4">
            <h4 className="mb-2 text-sm font-semibold text-muted-foreground">New Import</h4>
            <div className="space-y-1">
              <p className="text-sm font-medium">{newGameTitle}</p>
              <p className="text-xs text-muted-foreground">ID: {bggId}</p>
            </div>
          </div>
        </div>

        {/* Warning Message */}
        <div className="rounded-md bg-amber-50 p-3 text-sm dark:bg-amber-950/30">
          <p className="text-amber-900 dark:text-amber-100">
            <strong>What would you like to do?</strong>
          </p>
          <ul className="mt-2 space-y-1 text-xs text-amber-800 dark:text-amber-200">
            <li>
              • <strong>Cancel</strong>: Go back and change your selection
            </li>
            <li>
              • <strong>Replace</strong>: Update the existing game with new data
            </li>
            <li>
              • <strong>Create Anyway</strong>: Create as a separate entry (not recommended)
            </li>
          </ul>
        </div>

        <DialogFooter className="flex-col gap-2 sm:flex-row sm:justify-between">
          <Button variant="outline" onClick={onCancel} className="w-full sm:w-auto">
            Cancel
          </Button>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Button variant="default" onClick={onReplace} className="w-full sm:w-auto">
              Replace Existing
            </Button>
            <Button variant="secondary" onClick={onCreateAnyway} className="w-full sm:w-auto">
              Create Anyway
            </Button>
          </div>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
