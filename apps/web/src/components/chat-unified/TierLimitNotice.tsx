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
  return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;
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

/**
 * L'ora, nel fuso di chi legge, in cui riparte il conteggio: la prossima mezzanotte UTC. La quota
 * conta per giorno UTC, e «mezzanotte UTC» da sola lascia il conto a chi legge, che in Italia la
 * trova all'una o alle due di notte.
 *
 * @param timeZone Il fuso in cui dire l'ora. Per difetto quello del browser.
 */
export function quotaResetLocalTime(now: Date = new Date(), timeZone?: string): string {
  const nextUtcMidnight = new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1)
  );

  return new Intl.DateTimeFormat('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
    timeZone,
  }).format(nextUtcMidnight);
}

/** La spiegazione in una frase. */
export function tierLimitMessage(
  limit: TierLimit,
  resetsAt: string = quotaResetLocalTime()
): string {
  const used =
    limit.current !== undefined && limit.max !== undefined
      ? ` (${limit.current} su ${limit.max})`
      : '';

  return `Hai usato tutte le domande di oggi previste dal tuo piano${used}. Il conteggio riparte alle ${resetsAt} (mezzanotte UTC).`;
}

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
