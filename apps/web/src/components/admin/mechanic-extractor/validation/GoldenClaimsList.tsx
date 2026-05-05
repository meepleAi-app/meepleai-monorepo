/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 36)
 *
 * Tabular display of curated golden claims grouped by section, with inline edit
 * (dialog hosting `GoldenClaimForm`) and deactivate (AlertDialog confirmation
 * → `useDeactivateGoldenClaim`).
 */

'use client';

import { useMemo, useState } from 'react';

import { PencilIcon, Trash2Icon } from 'lucide-react';

import { GoldenClaimForm } from '@/components/admin/mechanic-extractor/validation/GoldenClaimForm';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
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
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { useDeactivateGoldenClaim } from '@/hooks/admin/useDeactivateGoldenClaim';
import type {
  MechanicGoldenClaimDto,
  MechanicSection,
} from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

const SECTION_ORDER: ReadonlyArray<MechanicSection> = [
  'Summary',
  'Mechanics',
  'Victory',
  'Resources',
  'Phases',
  'Faq',
];

const SECTION_LABEL: Record<MechanicSection, string> = {
  Summary: 'Summary',
  Mechanics: 'Mechanics',
  Victory: 'Victory',
  Resources: 'Resources',
  Phases: 'Phases',
  Faq: 'FAQ',
};

function truncate(text: string, max: number): string {
  if (text.length <= max) return text;
  return `${text.slice(0, max)}…`;
}

export interface GoldenClaimsListProps {
  /** Shared game owning these claims. */
  sharedGameId: string;
  /** Claims to render — typically `useGoldenForGame(sharedGameId).data?.claims`. */
  claims: ReadonlyArray<MechanicGoldenClaimDto>;
}

export function GoldenClaimsList({ sharedGameId, claims }: GoldenClaimsListProps) {
  const [editingClaim, setEditingClaim] = useState<MechanicGoldenClaimDto | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const deactivateMutation = useDeactivateGoldenClaim();

  const grouped = useMemo(() => {
    const map = new Map<MechanicSection, MechanicGoldenClaimDto[]>();
    for (const c of claims) {
      const arr = map.get(c.section) ?? [];
      arr.push(c);
      map.set(c.section, arr);
    }
    return SECTION_ORDER.filter(s => map.has(s)).map(section => ({
      section,
      claims: (map.get(section) ?? []).slice().sort((a, b) => a.expectedPage - b.expectedPage),
    }));
  }, [claims]);

  const handleConfirmDeactivate = () => {
    if (!deactivatingId) return;
    deactivateMutation.mutate(
      { sharedGameId, claimId: deactivatingId },
      {
        onSettled: () => setDeactivatingId(null),
      }
    );
  };

  if (claims.length === 0) {
    return (
      <div
        data-testid="golden-claims-list"
        className="rounded-md border border-dashed border-slate-300 bg-white/40 p-6 text-center text-sm text-muted-foreground dark:border-zinc-700 dark:bg-zinc-900/40"
      >
        No golden claims yet. Use the &quot;New claim&quot; button to add the first one.
      </div>
    );
  }

  return (
    <div data-testid="golden-claims-list" className="space-y-6">
      {grouped.map(group => (
        <section key={group.section} className="space-y-2">
          <h3 className="font-quicksand text-sm font-semibold tracking-wide text-foreground">
            {SECTION_LABEL[group.section]}{' '}
            <span className="text-muted-foreground">({group.claims.length})</span>
          </h3>
          <div className="rounded-md border border-slate-200 bg-white/70 dark:border-zinc-700 dark:bg-zinc-900/40">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[120px]">Section</TableHead>
                  <TableHead>Statement</TableHead>
                  <TableHead className="w-[110px]">Expected page</TableHead>
                  <TableHead className="w-[110px] text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {group.claims.map(claim => (
                  <TableRow key={claim.id}>
                    <TableCell className="font-medium">{SECTION_LABEL[claim.section]}</TableCell>
                    <TableCell>
                      <TooltipProvider delayDuration={250}>
                        <Tooltip>
                          <TooltipTrigger asChild>
                            <span className="cursor-help">{truncate(claim.statement, 80)}</span>
                          </TooltipTrigger>
                          <TooltipContent className="max-w-md whitespace-pre-wrap">
                            {claim.statement}
                          </TooltipContent>
                        </Tooltip>
                      </TooltipProvider>
                    </TableCell>
                    <TableCell>{claim.expectedPage}</TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          aria-label={`Edit claim ${claim.id}`}
                          onClick={() => setEditingClaim(claim)}
                        >
                          <PencilIcon className="h-4 w-4" />
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          aria-label={`Deactivate claim ${claim.id}`}
                          onClick={() => setDeactivatingId(claim.id)}
                        >
                          <Trash2Icon className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </section>
      ))}

      {/* Edit dialog */}
      <Dialog
        open={editingClaim !== null}
        onOpenChange={open => {
          if (!open) setEditingClaim(null);
        }}
      >
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Edit golden claim</DialogTitle>
          </DialogHeader>
          {editingClaim && (
            <GoldenClaimForm
              sharedGameId={sharedGameId}
              mode="edit"
              initialClaim={editingClaim}
              claimId={editingClaim.id}
              onClose={() => setEditingClaim(null)}
            />
          )}
        </DialogContent>
      </Dialog>

      {/* Deactivate confirmation */}
      <AlertDialog
        open={deactivatingId !== null}
        onOpenChange={open => {
          if (!open) setDeactivatingId(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Deactivate this golden claim?</AlertDialogTitle>
            <AlertDialogDescription>
              The claim will be soft-deleted. It can be restored later, but the version hash for
              this golden set will change.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deactivateMutation.isPending}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={e => {
                // Prevent the default Radix close so we can wait for the mutation
                e.preventDefault();
                handleConfirmDeactivate();
              }}
              disabled={deactivateMutation.isPending}
            >
              {deactivateMutation.isPending ? 'Deactivating…' : 'Deactivate'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
