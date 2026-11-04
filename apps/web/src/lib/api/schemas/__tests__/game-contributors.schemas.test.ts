/**
 * game-contributors — BE-aligned contract tests (#3853)
 *
 * `GetGameContributorsQueryHandler` proietta i badge cosi':
 *
 *   new BadgeSummaryDto { Code = ub.Badge.Code, Name = ub.Badge.Name,
 *                         IconUrl = ub.Badge.IconUrl ?? string.Empty,
 *                         Tier = ub.Badge.Tier }
 *
 * Lo schema pretendeva un `id` uuid che non esiste nel contratto, e ignorava `code` —
 * l'identificatore naturale del badge. Ogni contributore con almeno un badge faceva
 * quindi fallire la validazione dell'intera risposta; con `topBadges: []` non si vedeva,
 * ed e' la stessa dinamica del difetto batch-jobs (vedi
 * admin-system.batch-jobs.schemas.test.ts).
 */
import { describe, it, expect } from 'vitest';

import { BadgeSummaryDtoSchema } from '../game-contributors.schemas';

const backendBadge = {
  code: 'TOP_CONTRIBUTOR',
  name: 'Top Contributor',
  iconUrl: 'https://cdn.example.com/badges/top.png',
  tier: 'Gold',
};

describe('BadgeSummaryDtoSchema — allineato alla proiezione del handler (#3853)', () => {
  it('accetta un badge come il backend lo serializza', () => {
    const parsed = BadgeSummaryDtoSchema.parse(backendBadge);

    expect(parsed.code).toBe('TOP_CONTRIBUTOR');
    expect(parsed.tier).toBe('Gold');
  });

  it('accetta iconUrl vuota — il handler fa `?? string.Empty`, non manda null', () => {
    const parsed = BadgeSummaryDtoSchema.parse({ ...backendBadge, iconUrl: '' });
    expect(parsed.iconUrl).toBe('');
  });

  it('rifiuta la forma con `id` e senza `code`', () => {
    // La forma che lo schema pretendeva prima della correzione: nessuna risposta reale
    // l'ha mai avuta, e il mock di BadgeIcon la riproduceva con un `id` non-uuid.
    expect(() =>
      BadgeSummaryDtoSchema.parse({
        id: '0f8fad5b-d9cb-469f-a165-70867728950e',
        name: 'Top Contributor',
        iconUrl: null,
        tier: 'Gold',
      })
    ).toThrow();
  });

  it('accetta tutti e cinque i tier dichiarati da BadgeTier', () => {
    for (const tier of ['Bronze', 'Silver', 'Gold', 'Platinum', 'Diamond']) {
      expect(() => BadgeSummaryDtoSchema.parse({ ...backendBadge, tier })).not.toThrow();
    }
  });
});
