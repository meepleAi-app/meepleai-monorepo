/**
 * #4168 (5g-b) — la domanda fermata dalla quota del piano.
 *
 * Il backend chiude lo stream con un evento Error di codice `TIER_LIMIT_EXCEEDED`, e gli stessi
 * campi del 403 `tier_limit_exceeded` che c'è già: `limitType`, `current`, `max`, `upgradeUrl`.
 * Questi test fissano come la chat li legge, prima che il backend cominci a emetterli.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { readTierLimit, tierLimitMessage, TierLimitNotice } from '../TierLimitNotice';

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
    [null],
    ['not an object'],
  ])('leaves the numbers out when they are not both there: %j', data => {
    const limit = readTierLimit(data);

    expect(limit.current).toBeUndefined();
    expect(limit.max).toBeUndefined();
    expect(limit.upgradeUrl).toBe('/pricing');
  });
});

describe('tierLimitMessage', () => {
  it('says how many questions were used and when the count starts again', () => {
    expect(tierLimitMessage({ current: 3, max: 3, upgradeUrl: '/pricing' })).toBe(
      'Hai usato tutte le domande di oggi previste dal tuo piano (3 su 3). Il conteggio riparte a mezzanotte UTC.'
    );
  });

  it('does without the numbers when the backend did not send them', () => {
    expect(tierLimitMessage({ upgradeUrl: '/pricing' })).toBe(
      'Hai usato tutte le domande di oggi previste dal tuo piano. Il conteggio riparte a mezzanotte UTC.'
    );
  });
});

describe('TierLimitNotice', () => {
  it('explains the limit and links to the plans', () => {
    render(<TierLimitNotice limit={{ current: 3, max: 3, upgradeUrl: '/pricing' }} />);

    expect(screen.getByTestId('tier-limit-exceeded')).toHaveTextContent('3 su 3');
    expect(screen.getByRole('link', { name: 'Vedi i piani' })).toHaveAttribute('href', '/pricing');
  });
});
