/**
 * WizardModal — multi-step centered modal primitive (Issue #1897, Asse B WP4 T4).
 *
 * Pattern: Radix Dialog (centered modal, NOT slide-over like Drawer).
 * Z-index: z-[60] / z-[61] — sits above Drawer (z-50).
 *
 * Features:
 * - Sync/async validate (normalized via normalizeValidate helper, MAJ-1)
 * - Skip button on optional steps
 * - Cancel confirmation flow (nested Dialog z-[70])
 * - Errors render with role="alert" for a11y
 * - Focus trap handled by Radix Dialog
 */

'use client';

import { useState, type ReactElement } from 'react';

import * as RadixDialog from '@radix-ui/react-dialog';

import { useWizardStep } from './use-wizard-step';

import type { WizardModalProps } from './wizard-modal-types';

export function WizardModal({
  steps,
  onComplete,
  onCancel,
  open,
  onOpenChange,
}: WizardModalProps): ReactElement {
  const wizard = useWizardStep(steps);
  const [confirmingCancel, setConfirmingCancel] = useState(false);

  const handleNext = async (): Promise<void> => {
    const wasLast = wizard.isLast;
    const advanced = await wizard.goNext();
    if (advanced && wasLast) {
      await onComplete({});
      onOpenChange(false);
    }
  };

  const handleCancel = (): void => {
    setConfirmingCancel(true);
  };

  const handleConfirmCancel = (): void => {
    setConfirmingCancel(false);
    onCancel();
    onOpenChange(false);
  };

  const handleAbortCancel = (): void => {
    setConfirmingCancel(false);
  };

  return (
    <>
      <RadixDialog.Root open={open} onOpenChange={onOpenChange}>
        <RadixDialog.Portal>
          <RadixDialog.Overlay
            className="fixed inset-0 z-[60] bg-[hsl(var(--c-overlay-scrim))]"
            data-testid="wizard-modal-overlay"
          />
          <RadixDialog.Content
            className="fixed left-1/2 top-1/2 z-[61] flex w-full max-w-[720px] -translate-x-1/2 -translate-y-1/2 flex-col rounded-lg bg-background shadow-2xl"
            data-slot="wizard-modal"
            aria-labelledby="wizard-title"
          >
            <header className="sticky top-0 border-b border-border p-4">
              <RadixDialog.Title id="wizard-title" className="text-lg font-semibold">
                {wizard.currentStep.title}
              </RadixDialog.Title>
              <div data-testid="wizard-step-indicator" className="text-sm text-muted-foreground">
                Step {wizard.currentIndex + 1} of {steps.length}
              </div>
            </header>

            <div className="max-h-[60vh] overflow-y-auto p-6" data-testid="wizard-step-content">
              {wizard.currentStep.content}
              {wizard.errors.length > 0 && (
                <ul
                  role="alert"
                  className="mt-4 list-disc pl-5 text-[hsl(var(--c-danger))]"
                  data-testid="wizard-error-list"
                >
                  {wizard.errors.map((e, i) => (
                    <li key={`${e.field ?? 'err'}-${i}`}>{e.message}</li>
                  ))}
                </ul>
              )}
            </div>

            <footer className="sticky bottom-0 flex justify-between border-t border-border p-4">
              {!wizard.isFirst ? (
                <button
                  onClick={wizard.goBack}
                  type="button"
                  data-slot="wizard-back"
                  className="rounded px-3 py-1.5 text-sm hover:bg-muted"
                >
                  Back
                </button>
              ) : (
                <span />
              )}
              <div className="flex gap-2">
                <button
                  onClick={handleCancel}
                  type="button"
                  data-slot="wizard-cancel"
                  className="rounded px-3 py-1.5 text-sm hover:bg-muted"
                >
                  Cancel
                </button>
                {wizard.currentStep.optional && !wizard.isLast && (
                  <button
                    onClick={wizard.skip}
                    type="button"
                    data-slot="wizard-skip"
                    className="rounded px-3 py-1.5 text-sm hover:bg-muted"
                  >
                    Skip
                  </button>
                )}
                <button
                  onClick={handleNext}
                  disabled={wizard.isValidating}
                  type="button"
                  data-slot="wizard-next"
                  className="rounded bg-primary px-3 py-1.5 text-sm text-primary-foreground hover:bg-primary/90 disabled:opacity-50"
                >
                  {wizard.isLast ? 'Complete' : 'Next'}
                </button>
              </div>
            </footer>
          </RadixDialog.Content>
        </RadixDialog.Portal>
      </RadixDialog.Root>

      {/* Cancel confirmation — nested Dialog (z-[70] above wizard) */}
      {confirmingCancel && (
        <RadixDialog.Root open={confirmingCancel} onOpenChange={setConfirmingCancel}>
          <RadixDialog.Portal>
            <RadixDialog.Overlay
              className="fixed inset-0 z-[70] bg-[hsl(var(--c-overlay-scrim))]"
              data-testid="wizard-cancel-overlay"
            />
            <RadixDialog.Content
              className="fixed left-1/2 top-1/2 z-[71] w-full max-w-[400px] -translate-x-1/2 -translate-y-1/2 rounded-lg bg-background p-6 shadow-2xl"
              data-slot="wizard-cancel-modal"
            >
              <RadixDialog.Title className="text-lg font-semibold">
                Confirm Cancellation
              </RadixDialog.Title>
              <RadixDialog.Description className="mt-2 text-sm text-muted-foreground">
                Are you sure you want to cancel? All entered data will be lost.
              </RadixDialog.Description>
              <div className="mt-4 flex justify-end gap-2">
                <button
                  onClick={handleAbortCancel}
                  type="button"
                  data-slot="wizard-cancel-abort"
                  className="rounded px-3 py-1.5 text-sm hover:bg-muted"
                >
                  Continue
                </button>
                <button
                  onClick={handleConfirmCancel}
                  type="button"
                  data-slot="wizard-cancel-confirm"
                  className="rounded bg-[hsl(var(--c-danger))] px-3 py-1.5 text-sm text-white hover:opacity-90"
                >
                  Cancel Wizard
                </button>
              </div>
            </RadixDialog.Content>
          </RadixDialog.Portal>
        </RadixDialog.Root>
      )}
    </>
  );
}
