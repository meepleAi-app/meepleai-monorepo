'use client';

import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useProviderQuota } from '@/hooks/queries/useProviders';
import type { ProviderName } from '@/lib/api/schemas/providers';

import { RunProbeButton } from './RunProbeButton';

/**
 * Issue #936 (G5) — Provider detail page (drill-down from /admin/providers).
 */
export function ProviderDetail({ name }: { name: ProviderName }) {
  const { data, isLoading, isError } = useProviderQuota(name);

  return (
    <div className="space-y-6">
      <Link href="/admin/providers" className="text-sm text-muted-foreground hover:underline">
        ← Torna alla lista
      </Link>

      <div className="flex items-center justify-between">
        <h1 className="font-quicksand text-2xl font-semibold tracking-tight text-foreground">
          {name}
        </h1>
        <RunProbeButton name={name} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Quota</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {isLoading && <span>Caricamento…</span>}
          {isError && <span className="text-destructive">Errore caricamento</span>}
          {data && !data.quotaSupported && (
            <div className="text-muted-foreground">
              {data.errorMessage ?? 'Quota tracking non supportato dal provider'}
            </div>
          )}
          {data?.quotaSupported && !data.tokenConfigured && (
            <Badge variant="destructive">Token non configurato</Badge>
          )}
          {data?.quotaSupported && data.tokenConfigured && (
            <dl className="grid grid-cols-2 gap-3">
              {data.usedUsd !== null && (
                <div>
                  <dt className="text-muted-foreground">Utilizzato</dt>
                  <dd className="text-lg font-medium">${data.usedUsd.toFixed(4)}</dd>
                </div>
              )}
              {data.limitUsd !== null && (
                <div>
                  <dt className="text-muted-foreground">Limite</dt>
                  <dd className="text-lg font-medium">${data.limitUsd.toFixed(2)}</dd>
                </div>
              )}
              {data.remainingUsd !== null && (
                <div>
                  <dt className="text-muted-foreground">Rimanente</dt>
                  <dd className="text-lg font-medium">${data.remainingUsd.toFixed(2)}</dd>
                </div>
              )}
              {data.resetAt && (
                <div>
                  <dt className="text-muted-foreground">Reset</dt>
                  <dd>{new Date(data.resetAt).toLocaleString()}</dd>
                </div>
              )}
              <div className="col-span-2 pt-2 text-xs text-muted-foreground">
                Aggiornato: {new Date(data.fetchedAt).toLocaleString()} (cache{' '}
                {data.cacheTtlSeconds}s)
              </div>
            </dl>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
