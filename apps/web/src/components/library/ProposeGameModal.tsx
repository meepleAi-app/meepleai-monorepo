/**
 * Propose Game Modal Component
 * Issue #3669: Phase 8 - Frontend Integration (Task 8.4)
 *
 * Modal for proposing a private game to the shared catalog.
 * Includes notes textarea and document selector.
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
import type { PrivateGameDto } from '@/lib/api/schemas/private-games.schemas';

export interface ProposeGameModalProps {
  isOpen: boolean;
  onClose: () => void;
  game: PrivateGameDto | null;
  onPropose: (gameId: string, notes: string) => Promise<void>;
}

export function ProposeGameModal({ isOpen, onClose, game, onPropose }: ProposeGameModalProps) {
  const [notes, setNotes] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handlePropose = async () => {
    if (!game) return;

    setIsSubmitting(true);
    try {
      await onPropose(game.id, notes);
      setNotes('');
      onClose();
    } catch (error) {
      console.error('Propose error:', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Propose to Shared Catalog</DialogTitle>
          <DialogDescription>
            Propose "{game?.title}" to be added to the community catalog
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="notes">Notes for Reviewers (Optional)</Label>
            <Textarea
              id="notes"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Any additional information for the review team..."
              rows={4}
              disabled={isSubmitting}
            />
            <p className="text-sm text-muted-foreground">
              Provide context about why this game should be added to the catalog
            </p>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button onClick={handlePropose} disabled={isSubmitting || !game}>
            {isSubmitting ? 'Proposing...' : 'Submit Proposal'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
