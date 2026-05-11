'use client';

import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useProviderQuota } from '@/hooks/queries/useProviders';
import type { ProviderName } from '@/lib/api/schemas/providers';

/**
 * Issue #936 (G5) — Compact provider card for /admin/providers index.
 * Click → drill-down /admin/providers/[name].
 */
export function ProviderCard({ name }: { name: ProviderName }) {
  const { data, isLoading, isError } = useProviderQuota(name);

  return (
    <Link
      href={`/admin/providers/${encodeURIComponent(name)}`}
      className="block rounded-xl outline-none focus-visible:ring-2 focus-visible:ring-ring"
      aria-label={`Apri dettaglio provider ${name}`}
    >
      <Card className="h-full transition hover:bg-accent/30">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-base font-medium">{name}</CardTitle>
          {data && !data.quotaSupported && (
            <Badge variant="secondary" title={data.errorMessage ?? undefined}>
              Quota n/d
            </Badge>
          )}
          {data?.quotaSupported && data.tokenConfigured && <Badge variant="default">OK</Badge>}
          {data?.quotaSupported && !data.tokenConfigured && (
            <Badge variant="destructive">No token</Badge>
          )}
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          {isLoading && <span>Caricamento…</span>}
          {isError && <span>Errore caricamento quota</span>}
          {data?.quotaSupported && data.tokenConfigured && (
            <div className="space-y-1">
              {data.remainingUsd !== null && (
                <div>
                  <span className="font-medium text-foreground">
                    ${data.remainingUsd.toFixed(2)}
                  </span>{' '}
                  rimanenti
                </div>
              )}
              {data.usedUsd !== null && data.remainingUsd === null && (
                <div>
                  <span className="font-medium text-foreground">${data.usedUsd.toFixed(2)}</span>{' '}
                  utilizzati
                </div>
              )}
            </div>
          )}
          {data && !data.quotaSupported && <span>Quota tracking non supportato dal provider</span>}
        </CardContent>
      </Card>
    </Link>
  );
}
