/* eslint-disable local/no-hardcoded-color-utility -- admin tools chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13d admin scope (--admin-* decision deferred to DS-15). */
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
import { APPROVE_CLAIM_NOTE_MAX_LENGTH } from '@/lib/api/schemas/mechanic-analyses.schemas';
import type {
  MechanicClaimDto,
  MechanicClaimStructureDto,
  MechanicClaimValidationDto,
} from '@/lib/api/schemas/mechanic-analyses.schemas';

import { ClaimStructureFields } from './ClaimStructureFields';

import type { ClaimStructureSibling } from './ClaimStructureFields';

interface ApproveClaimDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (note: string, structure?: MechanicClaimStructureDto) => void;
  isPending: boolean;
  /** Short claim preview (first ~80 chars) used in the dialog description. */
  claimPreview?: string;
  /** Per-claim guardrail outcomes; a `fail` surfaces an override warning (#2782 FU-1). */
  validations?: MechanicClaimValidationDto[];
  /** Claim being approved; when present, its proposed structure is editable before approval. */
  claim?: MechanicClaimDto;
  /** Other claims of the same analysis, candidates for the overrides list. */
  siblings?: ClaimStructureSibling[];
}

function structureOf(claim: MechanicClaimDto): MechanicClaimStructureDto {
  return {
    kind: claim.kind,
    priority: claim.priority,
    overrides: claim.overrides,
    trigger: claim.trigger,
  };
}

/**
 * Modal for approving a single mechanic claim with an OPTIONAL reviewer note
 * (#526 ME-M1.4 AC-6). Unlike {@link RejectClaimDialog}, the note here is not
 * required — the confirm action is always enabled.
 */
export function ApproveClaimDialog({
  open,
  onOpenChange,
  onConfirm,
  isPending,
  claimPreview,
  validations,
  claim,
  siblings = [],
}: ApproveClaimDialogProps): React.JSX.Element {
  const [note, setNote] = useState('');
  const [structure, setStructure] = useState<MechanicClaimStructureDto | null>(
    claim ? structureOf(claim) : null
  );
  const hasFail = (validations ?? []).some(v => v.outcome === 'fail');

  // Reset note when the dialog closes (Cancel or programmatic close).
  useEffect(() => {
    if (!open) setNote('');
  }, [open]);

  // Initialise the editable structure from the claim whenever a (new) claim is targeted.
  useEffect(() => {
    setStructure(claim ? structureOf(claim) : null);
  }, [claim]);

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Approve this claim?</AlertDialogTitle>
          <AlertDialogDescription>
            {claimPreview ? (
              <span className="block italic">&ldquo;{claimPreview}&rdquo;</span>
            ) : null}
            <span className="mt-2 block">
              Optionally add a reviewer note. The note is recorded with the claim and surfaced to
              other reviewers.
            </span>
          </AlertDialogDescription>
        </AlertDialogHeader>
        {hasFail && (
          <div
            className="rounded-md border border-amber-300 bg-amber-50 p-2 text-xs text-amber-900 dark:border-amber-800 dark:bg-amber-950/20 dark:text-amber-300"
            role="alert"
            data-testid="approve-fail-warning"
          >
            Questo claim ha fallito uno o più guardrail. Approvando confermi un override manuale.
          </div>
        )}
        {structure && (
          <ClaimStructureFields value={structure} onChange={setStructure} siblings={siblings} />
        )}
        <div className="space-y-2">
          <label className="block text-sm font-medium" htmlFor="approve-claim-note">
            Reviewer note (optional, up to {APPROVE_CLAIM_NOTE_MAX_LENGTH} chars)
          </label>
          <textarea
            id="approve-claim-note"
            value={note}
            onChange={e => setNote(e.target.value)}
            placeholder="e.g. matches p.4…"
            className="h-24 w-full resize-y rounded-md border border-border bg-card px-3 py-2 text-sm dark:border-zinc-700 dark:bg-zinc-900 focus:border-green-400 focus:outline-none focus:ring-1 focus:ring-green-400"
            data-testid="approve-claim-note-input"
            maxLength={APPROVE_CLAIM_NOTE_MAX_LENGTH}
          />
          <p className="text-xs text-muted-foreground">
            {note.length} / {APPROVE_CLAIM_NOTE_MAX_LENGTH} characters
          </p>
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={e => {
              e.preventDefault();
              onConfirm(note.trim(), structure ?? undefined);
            }}
            disabled={isPending}
            className="bg-green-600 hover:bg-green-700"
            data-testid="approve-claim-confirm"
          >
            {isPending ? <Loader2Icon className="mr-1 h-4 w-4 animate-spin" /> : null}
            Approve claim
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
