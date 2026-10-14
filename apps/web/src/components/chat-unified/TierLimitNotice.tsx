/**
 * TierLimitNotice — #4168 (5g-b).
 *
 * Il backend ha fermato la domanda con `TIER_LIMIT_EXCEEDED`: il piano dell'utente prevede un
 * numero di domande al giorno, e per oggi sono finite. Non è un guasto, quindi non usa il banner
 * d'errore: dice quante ne sono state usate, a che ora riparte il conteggio, e porta ai piani.
 *
 * I campi sono gli stessi del 403 `tier_limit_exceeded` (`limitType`, `current`, `max`,
 * `upgradeUrl`), che qui arrivano dentro l'evento Error dello stream.
 */

'use client';

import React from 'react';

import Link from 'next/link';

import { tierLimitMessage, type TierLimit } from '@/lib/tier-limit';

// Chi leggeva la quota da qui continua a trovarla: la lettura sta in lib, la usano anche gli hook.
export {
  quotaResetLocalTime,
  readTierLimit,
  TIER_LIMIT_EXCEEDED,
  tierLimitMessage,
  type TierLimit,
} from '@/lib/tier-limit';

export interface TierLimitNoticeProps {
  limit: TierLimit;
}

export function TierLimitNotice({ limit }: TierLimitNoticeProps): React.JSX.Element {
  return (
    <div
      data-testid="tier-limit-exceeded"
      className="rounded-lg border border-border bg-muted p-3 text-sm text-foreground"
    >
      <p>{tierLimitMessage(limit)}</p>
      <Link
        href={limit.upgradeUrl}
        className="mt-2 inline-block font-medium text-foreground underline underline-offset-2"
      >
        Vedi i piani
      </Link>
    </div>
  );
}

export interface TierLimitRegionProps {
  /** Il limite da spiegare, o `null` finché non c'è niente da dire. */
  limit: TierLimit | null;
  /** I margini che la vista dà all'avviso. Valgono solo quando l'avviso c'è. */
  className?: string;
}

/**
 * Il posto in cui l'avviso compare. È nella pagina da subito, vuoto: l'avviso arriva dopo l'invio
 * e senza che il fuoco si sposti, e una regione viva montata già piena non viene annunciata da
 * tutti gli screen reader. Una che c'è da prima e poi si riempie, sì.
 */
export function TierLimitRegion({ limit, className }: TierLimitRegionProps): React.JSX.Element {
  return (
    <div role="status" data-testid="tier-limit-region" className={limit ? className : undefined}>
      {limit && <TierLimitNotice limit={limit} />}
    </div>
  );
}
