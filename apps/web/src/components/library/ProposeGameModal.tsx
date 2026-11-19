/**
 * Propose Game Modal Component
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.4)
 *
 * Modal for proposing a private game to the shared catalog.
 *
 * #5074: the shared catalog requires a description, so a proposal does too (decision of 2026-11-19). A game
 * without one is told so before anything is sent, with a way to edit it; the server answers 422 anyway.
 */

'use client';

import { useState } from 'react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { ApiError } from '@/lib/api/core/errors';
import type { PrivateGameDto } from '@/lib/api/schemas/private-games.schemas';
import { logger } from '@/lib/logger';

export interface ProposeGameModalProps {
  isOpen: boolean;
  onClose: () => void;
  game: PrivateGameDto | null;
  onPropose: (gameId: string, notes: string) => Promise<void>;
  /** Opens the game's edit form, offered when the game has no description (#5074). */
  onEditGame?: (game: PrivateGameDto) => void;
  /** Where the focus goes when the modal closes (#5081): it opens from a card menu, not from a trigger of its own. */
  onCloseAutoFocus?: (event: Event) => void;
}

/** What the user reads when the proposal is refused (#5074). */
function proposeErrorMessage(error: unknown): string {
  const status = error instanceof ApiError ? error.statusCode : undefined;
  if (status === 409) return 'A proposal for this game is already waiting for review.';
  if (status === 422) return 'Add a description to the game before proposing it to the catalog.';
  return 'The proposal could not be sent. Please try again.';
}

export function ProposeGameModal({
  isOpen,
  onClose,
  game,
  onPropose,
  onEditGame,
  onCloseAutoFocus,
}: ProposeGameModalProps) {
  const [notes, setNotes] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const missingDescription = !!game && !game.description?.trim();

  const handleClose = () => {
    setError(null);
    onClose();
  };

  const handlePropose = async () => {
    if (!game || missingDescription) return;

    setIsSubmitting(true);
    setError(null);
    try {
      await onPropose(game.id, notes);
      setNotes('');
      onClose();
    } catch (err) {
      logger.error('Propose error:', err);
      setError(proposeErrorMessage(err));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={open => !open && handleClose()}>
      <DialogContent onCloseAutoFocus={onCloseAutoFocus}>
        <DialogHeader>
          <DialogTitle>Propose to Shared Catalog</DialogTitle>
          <DialogDescription>
            Propose "{game?.title}" to be added to the community catalog
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {missingDescription && (
            <div
              role="alert"
              data-testid="propose-missing-description"
              className="space-y-2 rounded-md border border-border bg-muted p-3 text-sm text-foreground"
            >
              <p>
                This game has no description yet. The shared catalog needs one: add it before
                proposing the game.
              </p>
              {onEditGame && game && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    handleClose();
                    onEditGame(game);
                  }}
                >
                  Edit game
                </Button>
              )}
            </div>
          )}

          <div className="space-y-2">
            <Label htmlFor="notes">Notes for Reviewers (Optional)</Label>
            <Textarea
              id="notes"
              value={notes}
              onChange={e => setNotes(e.target.value)}
              placeholder="Any additional information for the review team..."
              rows={4}
              disabled={isSubmitting || missingDescription}
            />
            <p className="text-sm text-muted-foreground">
              Provide context about why this game should be added to the catalog
            </p>
          </div>

          {error && (
            <p role="alert" data-testid="propose-error" className="text-sm text-destructive">
              {error}
            </p>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button onClick={handlePropose} disabled={isSubmitting || !game || missingDescription}>
            {isSubmitting ? 'Proposing...' : 'Submit Proposal'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
