// apps/web/src/lib/i18n/__tests__/pick-best-translation.test.ts
import { describe, expect, it } from 'vitest';

import { pickBestTranslation } from '@/lib/i18n/pick-best-translation';
import type { SharedGameTranslationDto } from '@/lib/api/schemas/shared-games.schemas';

const TR = (overrides: Partial<SharedGameTranslationDto>): SharedGameTranslationDto => ({
  locale: 'it',
  title: 'Titolo',
  description: null,
  source: 'manual',
  ...overrides,
});

describe('pickBestTranslation', () => {
  it('returns null when no translation matches locale', () => {
    expect(pickBestTranslation([TR({ locale: 'it' })], 'fr')).toBeNull();
  });

  it('prefers manual over auto-openrouter for same locale', () => {
    const result = pickBestTranslation(
      [
        TR({ locale: 'it', title: 'Auto', source: 'auto-openrouter' }),
        TR({ locale: 'it', title: 'Manual', source: 'manual' }),
      ],
      'it'
    );
    expect(result?.title).toBe('Manual');
    expect(result?.source).toBe('manual');
  });

  it('prefers auto-openrouter over community for same locale', () => {
    const result = pickBestTranslation(
      [
        TR({ locale: 'it', title: 'Community', source: 'community' }),
        TR({ locale: 'it', title: 'Auto', source: 'auto-openrouter' }),
      ],
      'it'
    );
    expect(result?.title).toBe('Auto');
    expect(result?.source).toBe('auto-openrouter');
  });

  it('returns single match when only one source available', () => {
    const result = pickBestTranslation(
      [TR({ locale: 'it', title: 'Only', source: 'community' })],
      'it'
    );
    expect(result?.title).toBe('Only');
    expect(result?.source).toBe('community');
  });

  it('returns null for empty translations list', () => {
    expect(pickBestTranslation([], 'it')).toBeNull();
  });

  it('matches exact locale only (no BCP-47 fallback here — that is resolveLocale)', () => {
    expect(pickBestTranslation([TR({ locale: 'it' })], 'it-IT')).toBeNull();
  });
});
