/**
 * TierLimitNotice — #4168 (5g-b).
 *
 * Il backend ha fermato la domanda con `TIER_LIMIT_EXCEEDED`: il piano dell'utente prevede un
 * numero di domande al giorno, e per oggi sono finite. Non è un guasto, quindi non usa il banner
 * d'errore: dice quante ne sono state usate, quando riparte il conteggio, e porta ai piani.
 *
 * I campi sono gli stessi del 403 `tier_limit_exceeded` (`limitType`, `current`, `max`,
 * `upgradeUrl`), che qui arrivano dentro l'evento Error dello stream.
 */

'use client';

import React from 'react';

import Link from 'next/link';

import { assertSafeRelativeOrFallback } from '@/lib/url-safety';

/** Il codice dell'evento Error con cui il backend ferma una domanda oltre la quota del piano. */
export const TIER_LIMIT_EXCEEDED = 'TIER_LIMIT_EXCEEDED';

/** Dove si vedono i piani, se il backend non lo dice o dice qualcosa che non è un percorso del sito. */
const PLANS_PAGE = '/pricing';

export interface TierLimit {
  /** Le domande usate oggi. Assente se il backend non l'ha mandato insieme al massimo. */
  current?: number;
  /** Le domande al giorno del piano. */
  max?: number;
  /** Un percorso di questo sito: finisce in un `href`. */
  upgradeUrl: string;
}

function isCount(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0;
}

/**
 * Legge il limite dai dati dell'evento. I due numeri valgono solo insieme e solo se sono numeri
 * veri: un «0 su 0» costruito su un campo mancante sarebbe una bugia.
 */
export function readTierLimit(data: unknown): TierLimit {
  const fields = (typeof data === 'object' && data !== null ? data : {}) as {
    current?: unknown;
    max?: unknown;
    upgradeUrl?: unknown;
  };
  const upgradeUrl = assertSafeRelativeOrFallback(
    typeof fields.upgradeUrl === 'string' ? fields.upgradeUrl : null,
    PLANS_PAGE
  );

  if (isCount(fields.current) && isCount(fields.max) && fields.max > 0) {
    return { current: fields.current, max: fields.max, upgradeUrl };
  }

  return { upgradeUrl };
}

/** La spiegazione in una frase, per chi non può mostrare il link (la bolla della chat mobile). */
export function tierLimitMessage(limit: TierLimit): string {
  const used =
    limit.current !== undefined && limit.max !== undefined
      ? ` (${limit.current} su ${limit.max})`
      : '';

  return `Hai usato tutte le domande di oggi previste dal tuo piano${used}. Il conteggio riparte a mezzanotte UTC.`;
}

export interface TierLimitNoticeProps {
  limit: TierLimit;
}

export function TierLimitNotice({ limit }: TierLimitNoticeProps): React.JSX.Element {
  return (
    <div
      data-testid="tier-limit-exceeded"
      className="mx-4 mt-2 rounded-lg border border-border bg-muted p-3 text-sm text-foreground"
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
