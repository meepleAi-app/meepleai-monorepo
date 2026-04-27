'use client';

import React, { useEffect, useState } from 'react';

import { Loader2Icon } from 'lucide-react';

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
import {
  REJECT_CLAIM_NOTE_MAX_LENGTH,
  REJECT_CLAIM_NOTE_MIN_LENGTH,
} from '@/lib/api/schemas/mechanic-analyses.schemas';

interface RejectClaimDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (note: string) => void;
  isPending: boolean;
  /** Short claim preview (first ~80 chars) used in the dialog description. */
  claimPreview?: string;
}

/**
 * Modal for rejecting a single mechanic claim (ISSUE-584).
 *
 * Mirrors the backend validator bounds exposed via
 * `REJECT_CLAIM_NOTE_MIN_LENGTH` / `REJECT_CLAIM_NOTE_MAX_LENGTH`.
 */
export function RejectClaimDialog({
  open,
  onOpenChange,
  onConfirm,
  isPending,
  claimPreview,
}: RejectClaimDialogProps): React.JSX.Element {
  const [note, setNote] = useState('');

  // Reset note when the dialog closes (Cancel or programmatic close).
  useEffect(() => {
    if (!open) setNote('');
  }, [open]);

  const trimmedLength = note.trim().length;
  const isValid =
    trimmedLength >= REJECT_CLAIM_NOTE_MIN_LENGTH && note.length <= REJECT_CLAIM_NOTE_MAX_LENGTH;

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Reject this claim?</AlertDialogTitle>
          <AlertDialogDescription>
            {claimPreview ? (
              <span className="block italic">&ldquo;{claimPreview}&rdquo;</span>
            ) : null}
            <span className="mt-2 block">
              Provide a reviewer note explaining the rejection. The note is recorded with the claim
              and surfaced to other reviewers.
            </span>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <div className="space-y-2">
          <label className="block text-sm font-medium" htmlFor="reject-claim-note">
            Rejection note ({REJECT_CLAIM_NOTE_MIN_LENGTH}–{REJECT_CLAIM_NOTE_MAX_LENGTH} chars)
          </label>
          <textarea
            id="reject-claim-note"
            value={note}
            onChange={e => setNote(e.target.value)}
            placeholder="e.g. quote is from the optional expansion, not the base game…"
            className="h-24 w-full resize-y rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900 focus:border-rose-400 focus:outline-none focus:ring-1 focus:ring-rose-400"
            data-testid="reject-claim-note-input"
            maxLength={REJECT_CLAIM_NOTE_MAX_LENGTH + 50}
          />
          <p className="text-xs text-muted-foreground">
            {note.length} / {REJECT_CLAIM_NOTE_MAX_LENGTH} characters
            {trimmedLength === 0 && ' — note is required'}
          </p>
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={e => {
              e.preventDefault();
              if (!isValid) return;
              onConfirm(note.trim());
            }}
            disabled={!isValid || isPending}
            className="bg-rose-600 hover:bg-rose-700"
            data-testid="reject-claim-confirm-button"
          >
            {isPending ? <Loader2Icon className="mr-1 h-4 w-4 animate-spin" /> : null}
            Reject claim
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
