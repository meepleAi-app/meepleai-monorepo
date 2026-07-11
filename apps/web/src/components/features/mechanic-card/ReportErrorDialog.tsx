'use client';

/**
 * ReportErrorDialog — the "Report error" modal opened by a 👎 on a claim
 * (ME-M3.1, Issue #533).
 *
 * Collects the required error **type** (factual | ambiguous | contradicts_rule),
 * an optional free-text **description**, and an optional **suggested citation**
 * ("citation to the correct rule"), then posts a 👎 feedback payload.
 *
 * The dialog is a controlled component: the parent owns `open` + the target
 * `claimId`, and the parent runs the actual mutation via `onSubmit`. On a failed
 * submit the parent keeps `open` true so the user can retry (see MechanicCardView);
 * on success the parent closes it. Focus-trap + Esc + labelled controls come from
 * the shared Radix Dialog primitive.
 */

import { useEffect, useState, type ReactElement } from 'react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  MECHANIC_CARD_ERROR_TYPES,
  MECHANIC_CARD_ERROR_TYPE_LABELS,
  type MechanicCardErrorType,
} from '@/lib/api/schemas/mechanic-card-feedback.schemas';

export interface ReportErrorSubmission {
  errorType: MechanicCardErrorType;
  description: string | null;
  suggestedCitation: string | null;
}

export interface ReportErrorDialogProps {
  /** Whether the dialog is open (parent-controlled). */
  readonly open: boolean;
  /** Called when the dialog requests to close (Esc, overlay, ✕, or Cancel). */
  readonly onClose: () => void;
  /** Runs the 👎 submission. Resolves on success (parent then closes the dialog). */
  readonly onSubmit: (submission: ReportErrorSubmission) => Promise<void>;
  /** True while the submit mutation is in flight — disables the controls. */
  readonly isSubmitting: boolean;
}

const DEFAULT_ERROR_TYPE: MechanicCardErrorType = 'factual';

export function ReportErrorDialog({
  open,
  onClose,
  onSubmit,
  isSubmitting,
}: ReportErrorDialogProps): ReactElement {
  const [errorType, setErrorType] = useState<MechanicCardErrorType>(DEFAULT_ERROR_TYPE);
  const [description, setDescription] = useState('');
  const [suggestedCitation, setSuggestedCitation] = useState('');

  // Reset the form whenever the dialog (re)opens so a previous claim's text never
  // bleeds into the next report.
  useEffect(() => {
    if (open) {
      setErrorType(DEFAULT_ERROR_TYPE);
      setDescription('');
      setSuggestedCitation('');
    }
  }, [open]);

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    void onSubmit({
      errorType,
      description: description.trim() === '' ? null : description.trim(),
      suggestedCitation: suggestedCitation.trim() === '' ? null : suggestedCitation.trim(),
    });
  };

  return (
    <Dialog open={open} onOpenChange={next => !next && onClose()}>
      <DialogContent
        data-slot="mechanic-card-report-dialog"
        data-testid="mechanic-card-report-dialog"
        className="max-w-md"
      >
        <DialogHeader>
          <DialogTitle className="font-display text-lg font-extrabold text-foreground">
            Report an error
          </DialogTitle>
          <DialogDescription>
            Tell us what looks wrong with this claim. Your report helps us correct the card.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="mechanic-card-report-type">Type of problem</Label>
            <Select
              value={errorType}
              onValueChange={value => setErrorType(value as MechanicCardErrorType)}
              disabled={isSubmitting}
            >
              <SelectTrigger
                id="mechanic-card-report-type"
                data-testid="mechanic-card-report-type"
                aria-label="Type of problem"
              >
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {MECHANIC_CARD_ERROR_TYPES.map(type => (
                  <SelectItem key={type} value={type}>
                    {MECHANIC_CARD_ERROR_TYPE_LABELS[type]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="mechanic-card-report-description">Description</Label>
            <Textarea
              id="mechanic-card-report-description"
              data-testid="mechanic-card-report-description"
              value={description}
              onChange={event => setDescription(event.target.value)}
              disabled={isSubmitting}
              rows={4}
              placeholder="What's wrong, and what should it say instead?"
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <Label htmlFor="mechanic-card-report-citation">
              Citation to the correct rule{' '}
              <span className="font-normal text-muted-foreground">(optional)</span>
            </Label>
            <Input
              id="mechanic-card-report-citation"
              data-testid="mechanic-card-report-citation"
              value={suggestedCitation}
              onChange={event => setSuggestedCitation(event.target.value)}
              disabled={isSubmitting}
              placeholder="e.g. Rulebook p. 12, “Trading”"
            />
          </div>

          <DialogFooter className="gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={onClose}
              disabled={isSubmitting}
              data-testid="mechanic-card-report-cancel"
            >
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} data-testid="mechanic-card-report-submit">
              {isSubmitting ? 'Sending…' : 'Send report'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
