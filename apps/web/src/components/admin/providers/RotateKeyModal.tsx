'use client';

/**
 * #1834 SP5 F4-C3 — RotateKeyModal
 *
 * Modale per rotate API key di un provider LLM (mockup `sp5-admin-providers.html` row actions).
 * Richiede ruolo superadmin + step-up 2FA (S3 strict cutover #1597).
 *
 * **Stato attuale**: BE endpoint `POST /api/v1/admin/providers/{name}/rotate-key` NON ancora
 * implementato. Questa UI è pre-built per quando il BE arriverà. Comportamento corrente:
 * - Button "Rotate" sempre disabled
 * - Tooltip "BE endpoint pending — track ⊥ Sicurezza S3 followup"
 * - Modale NON apre (button disabled)
 *
 * Quando il BE arriverà (issue follow-up da aprire):
 * - Wire mutation POST /providers/{name}/rotate-key con header X-StepUp-Token
 * - Typed-confirm input ("type the provider name to confirm")
 * - Display nuova key UNA volta + checkbox "Ho copiato la key"
 * - Audit Level=3 emesso BE
 */

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import type { ProviderName } from '@/lib/api/schemas/providers';
import { isSuperAdmin } from '@/types/auth';

export interface RotateKeyModalProps {
  readonly providerName: ProviderName;
}

export function RotateKeyModal({ providerName }: RotateKeyModalProps) {
  const currentUser = useCurrentUser();
  const isSuper = isSuperAdmin(currentUser.data ?? null);

  // BE endpoint not yet implemented — button is always disabled with explanatory tooltip.
  // When BE lands, wire mutation + step-up check + typed-confirm flow here.
  const beAvailable = false;
  const disabled = !isSuper || !beAvailable;

  const title = !isSuper
    ? 'Richiede ruolo superadmin'
    : 'BE endpoint pending — track ⊥ Sicurezza S3 followup';

  return (
    <button
      type="button"
      disabled={disabled}
      title={title}
      aria-label={`Rotate key ${providerName} (BE pending)`}
      data-testid={`rotate-key-button-${providerName}`}
      data-be-available={beAvailable ? 'true' : 'false'}
      className="inline-flex items-center gap-1 rounded-md border border-rose-500/40 bg-rose-500/5 px-2 py-1 text-[11px] font-medium text-rose-700 dark:text-rose-300 hover:bg-rose-500/10 disabled:opacity-50 disabled:cursor-not-allowed"
    >
      🔒 Rotate
    </button>
  );
}
