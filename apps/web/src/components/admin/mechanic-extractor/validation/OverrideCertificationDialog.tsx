/**
 * Mechanic Extractor — AI Comprehension Validation (ADR-051 Sprint 1 / Task 37)
 *
 * Controlled dialog that collects a free-form admin reason (20..500 chars)
 * and triggers `useOverrideCertification(analysisId)` on submit.
 *
 *  - Opens / closes via `open` + `onOpenChange` props (no built-in trigger).
 *  - Form validation enforced client-side via zod (mirrors backend constraint).
 *  - Stays open on error (so the curator can see the toast); closes on success.
 *  - Always resets the form when the dialog closes.
 *
 * Toasts are owned by `useOverrideCertification` to avoid double-toasting; this
 * component only orchestrates the form lifecycle.
 */

'use client';

import { useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2Icon } from 'lucide-react';
import { useForm, type Resolver } from 'react-hook-form';
import { z } from 'zod';

import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/forms/form';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useOverrideCertification } from '@/hooks/admin/useOverrideCertification';

// ──────────────────────────────────────────────────────────────────────────
// Form schema — mirrors backend `OverrideCertificationRequest.Reason` (20..500).
// ──────────────────────────────────────────────────────────────────────────

export const OverrideCertificationFormSchema = z.object({
  reason: z
    .string()
    .min(20, 'Reason must be at least 20 characters')
    .max(500, 'Reason must be at most 500 characters'),
});

export type OverrideCertificationFormData = z.infer<typeof OverrideCertificationFormSchema>;

// ──────────────────────────────────────────────────────────────────────────
// Component
// ──────────────────────────────────────────────────────────────────────────

export interface OverrideCertificationDialogProps {
  /** Mechanic analysis aggregate id whose certification to override. */
  analysisId: string;
  /** Controlled open state. */
  open: boolean;
  /** Controlled open-change handler — invoked on Cancel, X, and after success. */
  onOpenChange: (open: boolean) => void;
  /** Optional callback after a successful override. */
  onSuccess?: () => void;
}

export function OverrideCertificationDialog({
  analysisId,
  open,
  onOpenChange,
  onSuccess,
}: OverrideCertificationDialogProps) {
  const mutation = useOverrideCertification();

  const form = useForm<OverrideCertificationFormData>({
    resolver: zodResolver(
      OverrideCertificationFormSchema
    ) as Resolver<OverrideCertificationFormData>,
    mode: 'onChange',
    defaultValues: { reason: '' },
  });

  // Reset the form whenever the dialog closes (Cancel, X, programmatic).
  useEffect(() => {
    if (!open) {
      form.reset({ reason: '' });
    }
  }, [open, form]);

  const onSubmit = form.handleSubmit(data => {
    mutation.mutate(
      { analysisId, reason: data.reason },
      {
        onSuccess: () => {
          onSuccess?.();
          onOpenChange(false);
        },
        // On error: keep the dialog open. Toast is fired by the hook.
      }
    );
  });

  const reasonValue = form.watch('reason');
  const reasonLength = reasonValue?.length ?? 0;
  const counterClass =
    reasonLength < 20 || reasonLength > 500
      ? 'text-rose-600 dark:text-rose-400'
      : 'text-muted-foreground';

  const submitDisabled = mutation.isPending || !form.formState.isValid;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-w-lg"
        data-testid="override-certification-dialog"
        // Prevent closing while submitting.
        onPointerDownOutside={event => {
          if (mutation.isPending) event.preventDefault();
        }}
        onEscapeKeyDown={event => {
          if (mutation.isPending) event.preventDefault();
        }}
      >
        <DialogHeader>
          <DialogTitle>Override certification</DialogTitle>
          <DialogDescription>
            Manually set this analysis to a different certification status. Provide a reason (20–500
            characters) for the audit log.
          </DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={onSubmit} className="space-y-4" noValidate>
            <FormField
              control={form.control}
              name="reason"
              render={({ field }) => (
                <FormItem>
                  <FormLabel htmlFor="override-cert-reason">Reason *</FormLabel>
                  <FormControl>
                    <Textarea
                      id="override-cert-reason"
                      rows={5}
                      placeholder="Explain why this certification is being overridden…"
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                  <p className={`text-xs ${counterClass}`}>{reasonLength}/500</p>
                </FormItem>
              )}
            />

            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                onClick={() => onOpenChange(false)}
                disabled={mutation.isPending}
              >
                Cancel
              </Button>
              <Button type="submit" disabled={submitDisabled}>
                {mutation.isPending && <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />}
                {mutation.isPending ? 'Submitting…' : 'Confirm override'}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
