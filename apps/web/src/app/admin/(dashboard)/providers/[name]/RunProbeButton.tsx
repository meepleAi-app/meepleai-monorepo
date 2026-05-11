'use client';

import { useState } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { useProbeProviderMutation } from '@/hooks/queries/useProviders';
import { useAuthUser } from '@/hooks/useAuthUser';
import type { ProviderName, ProviderProbeResult } from '@/lib/api/schemas/providers';

/**
 * Issue #936 (G5) — Run Probe button. Visible only to SuperAdmin.
 * Result is shown inline next to the button.
 */
export function RunProbeButton({ name }: { name: ProviderName }) {
  const { user } = useAuthUser();
  const isSuperAdmin = user?.role.toLowerCase() === 'superadmin';
  const mutation = useProbeProviderMutation(name);
  const [lastResult, setLastResult] = useState<ProviderProbeResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!isSuperAdmin) {
    return (
      <span className="text-sm text-muted-foreground">Probe richiede privilegi SuperAdmin</span>
    );
  }

  const onClick = async () => {
    setError(null);
    try {
      const r = await mutation.mutateAsync();
      setLastResult(r);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Probe fallita');
    }
  };

  return (
    <div className="flex items-center gap-3">
      <Button size="sm" disabled={mutation.isPending} onClick={onClick}>
        {mutation.isPending ? 'Probing…' : 'Run probe'}
      </Button>
      {lastResult && !error && (
        <span className="text-sm text-muted-foreground">
          {lastResult.tokenAuthenticated ? '✓ autenticato' : '✗ fallita'} · {lastResult.latencyMs}ms
          {lastResult.errorCode && (
            <span className="ml-1 text-destructive">({lastResult.errorCode})</span>
          )}
        </span>
      )}
      {error && <span className="text-sm text-destructive">{error}</span>}
    </div>
  );
}
