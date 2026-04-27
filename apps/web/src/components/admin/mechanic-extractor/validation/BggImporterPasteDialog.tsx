/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 2 / Task 18).
 *
 * Operator-facing dialog that bulk-imports BGG mechanic / theme tags into the
 * golden set for a shared game. The body is a single textarea: the operator
 * pastes the `Category<TAB>Name` block straight off a BoardGameGeek game page
 * (Edit → tag table). Below the textarea a live preview re-renders on every
 * keystroke with two panels:
 *
 *  - A red error list — one chip per malformed line (TAB missing, empty
 *    category, empty name).
 *  - A small table of the parsed rows the importer is about to send.
 *
 * Submitting fires `useImportBggTags(...)` with the parsed rows. The hook
 * owns the success / error toasts (so the dialog can stay focused on form
 * lifecycle): on success we close the dialog, on error we keep it open so
 * the operator can fix the paste and retry without losing their work.
 *
 *  - Closes via `open` + `onOpenChange` props (no built-in trigger button —
 *    callers mount the trigger so the importer can sit next to the existing
 *    claim list on the golden detail page, Task 19).
 *  - The submit button is disabled until at least one well-formed row is
 *    parsed AND while the mutation is in flight, so a double-click can't
 *    fire two imports in a row.
 *  - Closing the dialog (via `onOpenChange(false)`, Esc, or outside-click)
 *    resets the textarea so the next open starts blank.
 */

'use client';

import { useEffect, useMemo, useState } from 'react';

import { Loader2Icon } from 'lucide-react';

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
import { useImportBggTags } from '@/hooks/admin/useImportBggTags';
import { parseBggTsv } from '@/lib/parsers/bgg-tsv';

export interface BggImporterPasteDialogProps {
  /** Shared-game aggregate id whose golden BGG tag set we're appending to. */
  sharedGameId: string;
  /** Controlled open state. */
  open: boolean;
  /** Controlled open-change handler — called on Cancel, X, success, and Esc. */
  onOpenChange: (open: boolean) => void;
}

const TEXTAREA_ID = 'bgg-importer-paste-textarea';

export function BggImporterPasteDialog({
  sharedGameId,
  open,
  onOpenChange,
}: BggImporterPasteDialogProps) {
  const [pasted, setPasted] = useState('');
  const mutation = useImportBggTags();

  // Reset the textarea whenever the dialog closes (Cancel, X, Esc, success).
  useEffect(() => {
    if (!open) {
      setPasted('');
    }
  }, [open]);

  // Re-parse on every keystroke. Cheap (no async, no network) and the parser
  // is deliberately permissive — never throws, always returns { rows, errors }.
  const { rows, errors } = useMemo(() => parseBggTsv(pasted), [pasted]);

  const submitDisabled = mutation.isPending || rows.length === 0;

  const handleSubmit = () => {
    if (rows.length === 0) return;
    mutation.mutate(
      { sharedGameId, tags: rows.map(row => ({ category: row.category, name: row.name })) },
      {
        onSuccess: () => {
          onOpenChange(false);
        },
        // On error: keep the dialog open. Toast is fired by the hook.
      }
    );
  };

  return (
    <Dialog
      open={open}
      onOpenChange={next => {
        if (!next && mutation.isPending) return;
        onOpenChange(next);
      }}
    >
      <DialogContent
        className="max-w-2xl"
        data-testid="bgg-importer-paste-dialog"
        onPointerDownOutside={event => {
          if (mutation.isPending) event.preventDefault();
        }}
        onEscapeKeyDown={event => {
          if (mutation.isPending) event.preventDefault();
        }}
      >
        <DialogHeader>
          <DialogTitle>Import BGG tags</DialogTitle>
          <DialogDescription>
            Paste the mechanic / theme tag block from BoardGameGeek. Each line should be{' '}
            <code>Category</code>
            <span aria-hidden="true">&lt;TAB&gt;</span>
            <code>Name</code>. Duplicates within the paste are folded silently; duplicates against
            existing tags are reported in the success toast.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor={TEXTAREA_ID}>Paste BGG tags</Label>
            <Textarea
              id={TEXTAREA_ID}
              rows={8}
              spellCheck={false}
              placeholder={
                'Mechanism\tRole Selection\nMechanism\tVariable Phase Order\nTheme\tEconomic'
              }
              value={pasted}
              onChange={event => setPasted(event.target.value)}
              data-testid="bgg-importer-paste-textarea"
            />
          </div>

          {errors.length > 0 && (
            <div
              className="rounded-md border border-rose-200 bg-rose-50 p-3 text-sm text-rose-900 dark:border-rose-900/50 dark:bg-rose-950/40 dark:text-rose-200"
              data-testid="bgg-importer-error-list"
            >
              <p className="mb-1 font-medium">{errors.length} line(s) skipped</p>
              <ul className="list-disc space-y-0.5 pl-5">
                {errors.map(message => (
                  <li key={message}>{message}</li>
                ))}
              </ul>
            </div>
          )}

          {rows.length > 0 && (
            <div className="space-y-2">
              <p className="text-sm text-muted-foreground">{rows.length} row(s) ready to import</p>
              <div className="max-h-56 overflow-y-auto rounded-md border">
                <table className="w-full text-sm" data-testid="bgg-importer-preview-table">
                  <thead className="sticky top-0 border-b bg-muted/50">
                    <tr>
                      <th className="px-3 py-2 text-left font-medium">Category</th>
                      <th className="px-3 py-2 text-left font-medium">Name</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row, idx) => (
                      <tr
                        key={`${row.category}\u0000${row.name}`}
                        className={idx % 2 === 0 ? 'bg-background' : 'bg-muted/20'}
                      >
                        <td className="px-3 py-1.5">{row.category}</td>
                        <td className="px-3 py-1.5">{row.name}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={mutation.isPending}
          >
            Cancel
          </Button>
          <Button type="button" onClick={handleSubmit} disabled={submitDisabled}>
            {mutation.isPending && <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />}
            {mutation.isPending
              ? 'Importing…'
              : rows.length > 0
                ? `Insert ${rows.length} tag(s)`
                : 'Insert tags'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
