/**
 * EditNotesModal Component (Issue #2464)
 *
 * Modal dialog for editing library entry notes.
 * Features:
 * - Textarea with 500 char limit
 * - Character counter
 * - Validation with Zod
 * - Optimistic UI update
 */

'use client';

import React, { useState, useEffect } from 'react';

import { Check, Loader2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { useUpdateLibraryEntry } from '@/hooks/queries';
import { cn } from '@/lib/utils';

export interface EditNotesModalProps {
  /** Modal open state */
  isOpen: boolean;
  /** Callback to close modal */
  onClose: () => void;
  /** Game ID to edit notes for */
  gameId: string;
  /** Game title for display */
  gameTitle: string;
  /** Current notes value */
  currentNotes?: string | null;
  /** Callback when notes are updated */
  onNotesUpdated?: (notes: string) => void;
}

export function EditNotesModal({
  isOpen,
  onClose,
  gameId,
  gameTitle,
  currentNotes,
  onNotesUpdated,
}: EditNotesModalProps) {
  const [notes, setNotes] = useState(currentNotes || '');
  const updateMutation = useUpdateLibraryEntry();

  const isLoading = updateMutation.isPending;
  const maxLength = 500;
  const charsRemaining = maxLength - notes.length;

  // Sync with external notes changes
  useEffect(() => {
    setNotes(currentNotes || '');
  }, [currentNotes]);

  const handleSave = async () => {
    if (isLoading) return;

    try {
      await updateMutation.mutateAsync({
        gameId,
        request: { notes: notes.trim() || null },
      });

      toast.success('Note aggiornate con successo.');
      onNotesUpdated?.(notes);
      onClose();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Impossibile aggiornare le note.');
    }
  };

  const handleCancel = () => {
    setNotes(currentNotes || '');
    onClose();
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Modifica Note</DialogTitle>
          <DialogDescription>
            Aggiungi o modifica le tue note personali per <strong>{gameTitle}</strong>.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-2">
          <Label htmlFor="notes-textarea">Note Personali</Label>
          <Textarea
            id="notes-textarea"
            placeholder="Inserisci le tue note qui..."
            value={notes}
            onChange={e => setNotes(e.target.value)}
            maxLength={maxLength}
            rows={6}
            className="resize-none"
            aria-describedby="notes-counter"
          />
          <div
            id="notes-counter"
            className={cn(
              'text-xs text-right',
              charsRemaining < 50 ? 'text-destructive' : 'text-muted-foreground'
            )}
          >
            {charsRemaining} caratteri rimanenti
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={handleCancel} disabled={isLoading}>
            Annulla
          </Button>
          <Button onClick={handleSave} disabled={isLoading}>
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Salvataggio...
              </>
            ) : (
              <>
                <Check className="mr-2 h-4 w-4" />
                Salva
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
