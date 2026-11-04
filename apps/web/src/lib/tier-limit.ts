/**
 * La quota del piano — #4168 (5g-b).
 *
 * Il backend ferma una domanda oltre la quota con il codice `TIER_LIMIT_EXCEEDED`: nello stream è
 * un evento Error, fuori dallo stream il 403 `tier_limit_exceeded`. I campi sono gli stessi
 * (`limitType`, `current`, `max`, `upgradeUrl`). Qui sta come si leggono e come si dice in una
 * frase; lo usano tutti i punti del frontend che ricevono quello stream.
 */

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
