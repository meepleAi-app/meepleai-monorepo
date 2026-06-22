'use client';

/**
 * #1859 — RotateKeyModal
 *
 * Modal per rotate API key di un provider LLM (mockup `sp5-admin-providers.html`).
 * Richiede ruolo superadmin + step-up 2FA strict cutover (#1597).
 *
 * Wire contract:
 *   POST /api/v1/admin/providers/{name}/rotate-key
 *   body: { newApiKey, confirmedProviderName }
 *   200:  { providerName, newKeyFingerprint, rotatedAt, previousKeyDisabledAt }
 *
 * Errors:
 *   401 + subcode=step_up_required → opens StepUpTwoFactorModal,
 *                                    on success automatically retries rotation
 *   401 + subcode=enroll_required  → toast "2FA non configurato"
 *   403  → toast "Solo superadmin può ruotare le chiavi"
 *   409  → toast "Rotation già effettuata nelle ultime 24 ore"
 *   502  → toast "La nuova key non funziona — verifica sul dashboard provider"
 *   400  → toast generic (provider_name_mismatch / invalid_key_format)
 *
 * Initial 56-line disabled stub kept its trigger semantics (button always
 * visible; disabled when non-superadmin). The full modal is only rendered
 * once the user is superadmin.
 */

import { useCallback, useState } from 'react';

import { toast } from 'sonner';

import { StepUpTwoFactorModal } from '@/components/auth/StepUpTwoFactorModal';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Label } from '@/components/ui/primitives/label';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { RotateProviderKeyError, useRotateProviderKey } from '@/hooks/queries/useRotateProviderKey';
import type { ProviderName, RotateProviderKeyResponse } from '@/lib/api/schemas/providers';
import { isSuperAdmin } from '@/types/auth';

export interface RotateKeyModalProps {
  readonly providerName: ProviderName;
}

const MIN_KEY_LENGTH = 10;
const MAX_KEY_LENGTH = 512;

export function RotateKeyModal({ providerName }: RotateKeyModalProps): React.JSX.Element {
  const currentUser = useCurrentUser();
  const isSuper = isSuperAdmin(currentUser.data ?? null);

  // Local UI state
  const [open, setOpen] = useState(false);
  const [confirmName, setConfirmName] = useState('');
  const [newApiKey, setNewApiKey] = useState('');
  const [stepUpOpen, setStepUpOpen] = useState(false);
  const [result, setResult] = useState<RotateProviderKeyResponse | null>(null);

  const rotate = useRotateProviderKey(providerName);

  const resetForm = useCallback(() => {
    setConfirmName('');
    setNewApiKey('');
    setResult(null);
  }, []);

  const handleClose = useCallback(() => {
    if (rotate.isPending) return;
    setOpen(false);
    resetForm();
  }, [rotate.isPending, resetForm]);

  const handleOpenChange = useCallback(
    (next: boolean) => {
      if (next) {
        setOpen(true);
      } else {
        handleClose();
      }
    },
    [handleClose]
  );

  const handleApiError = useCallback((err: unknown): void => {
    if (!(err instanceof RotateProviderKeyError)) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error('Rotate key fallita', { description: message });
      return;
    }
    switch (err.kind) {
      case 'step_up_required':
        // Open step-up modal; success handler will auto-retry rotation.
        setStepUpOpen(true);
        return;
      case 'enroll_required':
        toast.error('2FA non configurato', {
          description: 'Configura il 2FA in /settings prima di ruotare le chiavi.',
        });
        return;
      case 'forbidden':
        toast.error('Solo superadmin può ruotare le chiavi');
        return;
      case 'rate_limit_exceeded':
        toast.error('Rotation già effettuata nelle ultime 24 ore', {
          description: 'Attendi prima di ruotare di nuovo questa chiave.',
        });
        return;
      case 'provider_probe_failed':
        toast.error('La nuova key non funziona', {
          description: 'Verifica la key sul dashboard del provider e riprova.',
        });
        return;
      case 'provider_name_mismatch':
        toast.error('Nome provider non corrisponde', {
          description: 'Il nome di conferma deve coincidere esattamente con il provider.',
        });
        return;
      case 'invalid_key_format':
        toast.error('Formato chiave non valido', {
          description: err.message,
        });
        return;
      case 'invalid_provider':
        toast.error('Provider sconosciuto', { description: err.message });
        return;
      case 'bad_request':
      case 'unknown':
      default:
        toast.error('Rotate key fallita', { description: err.message });
        return;
    }
  }, []);

  const submitRotation = useCallback(async (): Promise<void> => {
    try {
      const response = await rotate.mutateAsync({
        newApiKey,
        confirmedProviderName: confirmName,
      });
      setResult(response);
      toast.success('Chiave ruotata correttamente', {
        description: `Nuovo fingerprint: ${response.newKeyFingerprint}`,
      });
    } catch (err) {
      handleApiError(err);
    }
  }, [confirmName, newApiKey, rotate, handleApiError]);

  const handleSubmit = useCallback(
    async (e: React.FormEvent) => {
      e.preventDefault();
      if (rotate.isPending) return;
      await submitRotation();
    },
    [rotate.isPending, submitRotation]
  );

  const handleStepUpSuccess = useCallback(() => {
    setStepUpOpen(false);
    // Automatically retry the rotation now that the session is freshly step-upped.
    void submitRotation();
  }, [submitRotation]);

  const typedConfirmMatches = confirmName.trim() === providerName;
  const keyLengthValid = newApiKey.length >= MIN_KEY_LENGTH && newApiKey.length <= MAX_KEY_LENGTH;
  const canSubmit = typedConfirmMatches && keyLengthValid && !rotate.isPending && result === null;

  // Trigger button — preserves original disabled semantics for non-superadmin.
  const triggerDisabled = !isSuper;
  const triggerTitle = !isSuper ? 'Richiede ruolo superadmin' : 'Ruota la chiave del provider';

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        disabled={triggerDisabled}
        title={triggerTitle}
        aria-label={`Rotate key ${providerName}`}
        data-testid={`rotate-key-button-${providerName}`}
        data-be-available="true"
        className="inline-flex items-center gap-1 rounded-md border border-rose-500/40 bg-rose-500/5 px-2 py-1 text-[11px] font-medium text-rose-700 dark:text-rose-300 hover:bg-rose-500/10 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        <span aria-hidden="true">🔒</span> Rotate
      </button>

      <Dialog open={open && isSuper} onOpenChange={handleOpenChange}>
        <DialogContent data-testid={`rotate-key-modal-${providerName}`}>
          <DialogHeader>
            <DialogTitle>Ruota chiave API — {providerName}</DialogTitle>
            <DialogDescription>
              Questa operazione richiede ruolo superadmin e una verifica 2FA recente. La nuova
              chiave sarà validata contro il provider prima di essere persistita.
            </DialogDescription>
          </DialogHeader>

          {result ? (
            <div className="space-y-4 pt-2">
              <div
                className="rounded-md border border-emerald-500/40 bg-emerald-500/5 p-3"
                data-testid={`rotate-key-success-${providerName}`}
              >
                <p className="font-quicksand text-sm font-bold text-emerald-700 dark:text-emerald-300">
                  Chiave ruotata
                </p>
                <dl className="mt-2 space-y-1 font-mono text-[11px] text-foreground">
                  <div className="flex justify-between gap-3">
                    <dt className="text-muted-foreground">Fingerprint</dt>
                    <dd data-testid={`rotate-key-fingerprint-${providerName}`}>
                      {result.newKeyFingerprint}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-3">
                    <dt className="text-muted-foreground">Ruotata il</dt>
                    <dd>{new Date(result.rotatedAt).toLocaleString('it-IT')}</dd>
                  </div>
                  <div className="flex justify-between gap-3">
                    <dt className="text-muted-foreground">Vecchia chiave disabilitata</dt>
                    <dd>{new Date(result.previousKeyDisabledAt).toLocaleString('it-IT')}</dd>
                  </div>
                </dl>
              </div>
              <DialogFooter>
                <button
                  type="button"
                  onClick={handleClose}
                  className="rounded border border-border px-4 py-2 text-sm hover:bg-muted"
                  data-testid={`rotate-key-close-${providerName}`}
                >
                  Chiudi
                </button>
              </DialogFooter>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              <div className="space-y-1">
                <Label htmlFor={`rotate-key-confirm-${providerName}`}>Conferma nome provider</Label>
                <input
                  id={`rotate-key-confirm-${providerName}`}
                  type="text"
                  value={confirmName}
                  onChange={e => setConfirmName(e.target.value)}
                  placeholder={`Digita "${providerName}" per confermare`}
                  className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                  autoComplete="off"
                  data-testid={`rotate-key-confirm-input-${providerName}`}
                />
                {confirmName.length > 0 && !typedConfirmMatches && (
                  <p className="text-[11px] text-rose-600 dark:text-rose-400">
                    Deve corrispondere esattamente a &quot;{providerName}&quot;.
                  </p>
                )}
              </div>

              <div className="space-y-1">
                <Label htmlFor={`rotate-key-new-${providerName}`}>Nuova chiave API</Label>
                <input
                  id={`rotate-key-new-${providerName}`}
                  type="password"
                  value={newApiKey}
                  onChange={e => setNewApiKey(e.target.value)}
                  minLength={MIN_KEY_LENGTH}
                  maxLength={MAX_KEY_LENGTH}
                  className="w-full rounded border border-border bg-background px-3 py-2 font-mono text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                  autoComplete="off"
                  required
                  data-testid={`rotate-key-new-input-${providerName}`}
                />
                <p className="text-[11px] text-muted-foreground">
                  {MIN_KEY_LENGTH}-{MAX_KEY_LENGTH} caratteri. La chiave sarà validata contro il
                  provider prima della persistenza.
                </p>
              </div>

              <DialogFooter>
                <button
                  type="button"
                  onClick={handleClose}
                  disabled={rotate.isPending}
                  className="rounded border border-border px-4 py-2 text-sm hover:bg-muted disabled:opacity-50"
                  data-testid={`rotate-key-cancel-${providerName}`}
                >
                  Annulla
                </button>
                <button
                  type="submit"
                  disabled={!canSubmit}
                  className="rounded bg-rose-600 px-4 py-2 text-sm text-white hover:bg-rose-700 disabled:opacity-50"
                  data-testid={`rotate-key-submit-${providerName}`}
                >
                  {rotate.isPending ? 'Ruotando…' : 'Ruota chiave'}
                </button>
              </DialogFooter>
            </form>
          )}
        </DialogContent>
      </Dialog>

      <StepUpTwoFactorModal
        isOpen={stepUpOpen}
        onClose={() => setStepUpOpen(false)}
        onSuccess={handleStepUpSuccess}
        reason={`Per ruotare la chiave del provider "${providerName}" serve una verifica 2FA recente.`}
      />
    </>
  );
}
