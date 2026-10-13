/**
 * #4168 (5g-b) — la domanda fermata dalla quota del piano.
 *
 * Il backend chiude lo stream con un evento Error di codice `TIER_LIMIT_EXCEEDED`, e gli stessi
 * campi del 403 `tier_limit_exceeded` che c'è già: `limitType`, `current`, `max`, `upgradeUrl`.
 * Questi test fissano come la chat li legge, prima che il backend cominci a emetterli.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import {
  quotaResetLocalTime,
  readTierLimit,
  tierLimitMessage,
  TierLimitNotice,
} from '../TierLimitNotice';

describe('readTierLimit', () => {
  it('reads the numbers and the link the backend sends', () => {
    expect(
      readTierLimit({
        errorCode: 'TIER_LIMIT_EXCEEDED',
        limitType: 'AgentQuery',
        current: 3,
        max: 3,
        upgradeUrl: '/pricing',
      })
    ).toEqual({ current: 3, max: 3, upgradeUrl: '/pricing' });
  });

  /**
   * Il link finisce in un `href`: deve essere un percorso di questo sito. Qualunque altra cosa
   * torna alla pagina dei piani.
   */
  it.each([
    ['https://evil.example/pricing'],
    ['//evil.example'],
    ['javascript:alert(1)'],
    [undefined],
    [42],
  ])('falls back to the plans page when the link is %s', upgradeUrl => {
    expect(readTierLimit({ current: 3, max: 3, upgradeUrl }).upgradeUrl).toBe('/pricing');
  });

  /** Un numero che manca non diventa uno zero: «0 su 0» sarebbe una bugia. */
  it.each([
    [{ max: 3 }],
    [{ current: 3 }],
    [{ current: '3', max: '3' }],
    [{ current: 3, max: 0 }],
    [{ current: -1, max: 3 }],
    [{ current: 1.5, max: 3 }],
    [{ current: 3, max: 1e21 }],
    [null],
    ['not an object'],
  ])('leaves the numbers out when they are not both there: %j', data => {
    const limit = readTierLimit(data);

    expect(limit.current).toBeUndefined();
    expect(limit.max).toBeUndefined();
    expect(limit.upgradeUrl).toBe('/pricing');
  });
});

/**
 * La quota conta per giorno UTC. Dire solo «mezzanotte UTC» lascia il conto a chi legge, che in
 * Italia la trova all'una o alle due di notte: la frase dà l'ora del posto in cui è l'utente e
 * tiene «UTC» accanto, perché è lì che il conteggio riparte davvero.
 */
describe('quotaResetLocalTime', () => {
  it.each([
    ['2026-01-15T10:00:00Z', 'Europe/Rome', '01:00'],
    ['2026-07-15T10:00:00Z', 'Europe/Rome', '02:00'],
    ['2026-07-15T10:00:00Z', 'UTC', '00:00'],
    ['2026-01-15T10:00:00Z', 'America/New_York', '19:00'],
    // A un minuto dalla mezzanotte UTC la prossima è fra un minuto, non fra un giorno e un minuto.
    ['2026-01-15T23:59:00Z', 'Europe/Rome', '01:00'],
  ])('at %s in %s the count starts again at %s', (now, timeZone, expected) => {
    expect(quotaResetLocalTime(new Date(now), timeZone)).toBe(expected);
  });
});

describe('tierLimitMessage', () => {
  it('says how many questions were used and when the count starts again', () => {
    expect(tierLimitMessage({ current: 3, max: 3, upgradeUrl: '/pricing' }, '02:00')).toBe(
      'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3). Il conteggio riparte alle 02:00 (mezzanotte UTC).'
    );
  });

  it('does without the numbers when the backend did not send them', () => {
    expect(tierLimitMessage({ upgradeUrl: '/pricing' }, '01:00')).toBe(
      'Hai usato tutte le domande di oggi previste dal tuo piano. Il conteggio riparte alle 01:00 (mezzanotte UTC).'
    );
  });
});

describe('TierLimitNotice', () => {
  it('explains the limit and links to the plans', () => {
    render(<TierLimitNotice limit={{ current: 3, max: 3, upgradeUrl: '/pricing' }} />);

    const notice = screen.getByTestId('tier-limit-exceeded');
    expect(notice).toHaveTextContent('3 su 3');
    expect(notice).toHaveTextContent(/riparte alle \d{2}:\d{2} \(mezzanotte UTC\)/);
    expect(screen.getByRole('link', { name: 'Vedi i piani' })).toHaveAttribute('href', '/pricing');
  });

  /** Compare dopo l'invio, senza che il fuoco si sposti: uno screen reader deve annunciarlo. */
  it('is announced as a status', () => {
    render(<TierLimitNotice limit={{ upgradeUrl: '/pricing' }} />);

    expect(screen.getByRole('status')).toBe(screen.getByTestId('tier-limit-exceeded'));
  });
});
