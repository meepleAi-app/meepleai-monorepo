'use client';

/**
 * #1859 Phase 10 — StepUpTwoFactorModal
 *
 * Modal that prompts the user for a TOTP code to re-verify 2FA on the current
 * session (without creating a new session). Used by admin commands gated by
 * `[RequireTwoFactor]` (e.g. rotate-key) when the BE returns 401 + subcode
 * `step_up_required`.
 *
 * Wire contract: docs/api/2fa-step-up-protocol.md
 *
 * **Usage**:
 *   const [stepUpOpen, setStepUpOpen] = useState(false);
 *   const handleStepUpSuccess = () => {
 *     setStepUpOpen(false);
 *     retryOriginalMutation();
 *   };
 *   <StepUpTwoFactorModal
 *     isOpen={stepUpOpen}
 *     onClose={() => setStepUpOpen(false)}
 *     onSuccess={handleStepUpSuccess}
 *     reason="Per ruotare la chiave del provider serve una verifica 2FA recente."
 *   />
 */

import { useState } from 'react';

import { toast } from 'sonner';

import { TwoFactorVerification } from '@/components/auth/TwoFactorVerification';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { StepUpTwoFactorError, useStepUpTwoFactor } from '@/hooks/queries/useStepUpTwoFactor';

export interface StepUpTwoFactorModalProps {
  /** Controls dialog open state. */
  readonly isOpen: boolean;
  /** Called when the user dismisses the dialog without succeeding. */
  readonly onClose: () => void;
  /** Called after a successful step-up verification. Caller typically retries the original request. */
  readonly onSuccess: () => void;
  /**
   * Optional contextual reason shown in the dialog description.
   * Defaults to a generic message.
   */
  readonly reason?: string;
}

const DEFAULT_REASON =
  "Per completare l'operazione richiesta serve una verifica 2FA recente sulla sessione corrente.";

export function StepUpTwoFactorModal({
  isOpen,
  onClose,
  onSuccess,
  reason,
}: StepUpTwoFactorModalProps): React.JSX.Element {
  const stepUp = useStepUpTwoFactor();
  const [errorMessage, setErrorMessage] = useState<string | undefined>(undefined);

  const handleVerify = async ({ code }: { code: string }): Promise<void> => {
    setErrorMessage(undefined);
    try {
      await stepUp.mutateAsync({ code });
      toast.success('Verifica 2FA completata');
      onSuccess();
    } catch (err) {
      if (err instanceof StepUpTwoFactorError) {
        if (err.kind === 'locked_out') {
          const secs = err.retryAfterSeconds ?? 900;
          const minutes = Math.ceil(secs / 60);
          toast.error('Troppi tentativi falliti', {
            description: `Riprova tra circa ${minutes} minut${minutes === 1 ? 'o' : 'i'}.`,
          });
          onClose();
          return;
        }
        if (err.kind === 'unavailable') {
          toast.error('Servizio 2FA temporaneamente non disponibile', {
            description: 'Riprova tra qualche istante.',
          });
          onClose();
          return;
        }
        // invalid_code or unknown — inline error in the form
        setErrorMessage(
          err.kind === 'invalid_code' ? 'Codice non valido o scaduto. Riprova.' : err.message
        );
        return;
      }
      const fallback = err instanceof Error ? err.message : 'Errore sconosciuto';
      setErrorMessage(fallback);
    }
  };

  const handleOpenChange = (open: boolean) => {
    if (!open && !stepUp.isPending) {
      setErrorMessage(undefined);
      onClose();
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={handleOpenChange}>
      <DialogContent data-testid="step-up-2fa-modal">
        <DialogHeader>
          <DialogTitle>Verifica 2FA richiesta</DialogTitle>
          <DialogDescription>{reason ?? DEFAULT_REASON}</DialogDescription>
        </DialogHeader>

        <div className="pt-2">
          <TwoFactorVerification
            onVerify={handleVerify}
            onCancel={() => {
              setErrorMessage(undefined);
              onClose();
            }}
            loading={stepUp.isPending}
            error={errorMessage}
            onErrorDismiss={() => setErrorMessage(undefined)}
            autoSubmit={true}
            showRememberDevice={false}
            title="Inserisci il codice 2FA"
            subtitle="Inserisci il codice a 6 cifre dalla tua app authenticator."
          />
        </div>
      </DialogContent>
    </Dialog>
  );
}
