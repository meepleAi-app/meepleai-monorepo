// apps/web/src/lib/i18n/__tests__/use-game-title.test.tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, renderHook } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { IntlProvider } from 'react-intl';

import { useGameTitle } from '@/lib/i18n/use-game-title';
import * as useUserLocaleModule from '@/hooks/useUserLocale';
import { useTranslation } from '@/hooks/useTranslation';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

import enMessages from '@/locales/en.json';
import itMessages from '@/locales/it.json';

expect.extend(toHaveNoViolations);

const BASE_GAME: Pick<SharedGame, 'id' | 'title' | 'translations'> = {
  id: '00000000-0000-0000-0000-000000000001' as never,
  title: 'Catan',
  translations: [],
};

function mockUserLocale(locale: 'it' | 'en' | 'es' | 'fr' | 'de') {
  vi.spyOn(useUserLocaleModule, 'useUserLocale').mockReturnValue(locale);
}

describe('useGameTitle (matrix T1-T6)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('T1: en user, no translations → canonical EN', () => {
    mockUserLocale('en');
    const { result } = renderHook(() => useGameTitle(BASE_GAME));
    expect(result.current).toEqual({
      value: 'Catan',
      source: 'canonical',
      locale: 'en',
    });
  });

  it('T2: it user, [it manual] → IT manual translation', () => {
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [
        { locale: 'it', title: 'I Coloni di Catan', description: null, source: 'manual' as const },
      ],
    };
    const { result } = renderHook(() => useGameTitle(game));
    expect(result.current).toEqual({
      value: 'I Coloni di Catan',
      source: 'translation',
      locale: 'it',
      provider: 'manual',
    });
  });

  it('T3: it-IT user, [it manual] → IT manual (BCP-47 fallback)', () => {
    // The user-locale hook returns 'it' for it-IT (drops region). To simulate
    // it-IT we override via the options arg.
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [
        { locale: 'it', title: 'I Coloni di Catan', description: null, source: 'manual' as const },
      ],
    };
    const { result } = renderHook(() => useGameTitle(game, { locale: 'it-IT' }));
    expect(result.current).toEqual({
      value: 'I Coloni di Catan',
      source: 'translation',
      locale: 'it', // resolved via fallback, not it-IT
      provider: 'manual',
    });
  });

  it('T4: de user, [it manual, fr community] → canonical EN', () => {
    mockUserLocale('de');
    const game = {
      ...BASE_GAME,
      translations: [
        { locale: 'it', title: 'IT', description: null, source: 'manual' as const },
        { locale: 'fr', title: 'FR', description: null, source: 'community' as const },
      ],
    };
    const { result } = renderHook(() => useGameTitle(game));
    expect(result.current.value).toBe('Catan');
    expect(result.current.source).toBe('canonical');
  });

  it('T5: explicit override en, [it manual] → canonical EN (override wins)', () => {
    // Even with browser/profile it, an explicit options.locale='en' overrides.
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [{ locale: 'it', title: 'IT', description: null, source: 'manual' as const }],
    };
    const { result } = renderHook(() => useGameTitle(game, { locale: 'en' }));
    expect(result.current).toEqual({
      value: 'Catan',
      source: 'canonical',
      locale: 'en',
    });
  });

  it('T6: it user, [it manual, it auto-openrouter] → manual wins (source priority)', () => {
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [
        { locale: 'it', title: 'Auto MT', description: null, source: 'auto-openrouter' as const },
        { locale: 'it', title: 'I Coloni di Catan', description: null, source: 'manual' as const },
      ],
    };
    const { result } = renderHook(() => useGameTitle(game));
    expect(result.current).toEqual({
      value: 'I Coloni di Catan',
      source: 'translation',
      locale: 'it',
      provider: 'manual',
    });
  });

  it('handles null translations payload defensively (backward compat)', () => {
    mockUserLocale('it');
    const game = { ...BASE_GAME, translations: null as never };
    const { result } = renderHook(() => useGameTitle(game));
    expect(result.current.value).toBe('Catan');
    expect(result.current.source).toBe('canonical');
  });

  it('memoizes: re-render with same inputs returns same object reference', () => {
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [{ locale: 'it', title: 'IT', description: null, source: 'manual' as const }],
    };
    const { result, rerender } = renderHook(() => useGameTitle(game));
    const first = result.current;
    rerender();
    expect(result.current).toBe(first);
  });
});

// ─── A11y tests (A1/A2/A3) ──────────────────────────────────────────────────
// DEC-FE-9: aria-label uses i18n key `common.localizedFromEnglish` via
// react-intl, NOT a hardcoded EN string. Localized for both UI=en and UI=it.

/**
 * Flatten nested locale JSON to dot-notation keys for react-intl messages.
 * `common.localizedFromEnglish` → flat["common.localizedFromEnglish"] = "..."
 */
function flatten(obj: Record<string, unknown>, prefix = ''): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, value] of Object.entries(obj)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') {
      result[path] = value;
    } else if (value && typeof value === 'object') {
      Object.assign(result, flatten(value as Record<string, unknown>, path));
    }
  }
  return result;
}

const EN_FLAT = flatten(enMessages as Record<string, unknown>);
const IT_FLAT = flatten(itMessages as Record<string, unknown>);

function renderWithIntl(locale: 'en' | 'it', component: React.ReactElement) {
  const messages = locale === 'en' ? EN_FLAT : IT_FLAT;
  return render(
    <IntlProvider locale={locale} messages={messages}>
      {component}
    </IntlProvider>
  );
}

describe('useGameTitle a11y (axe AA + DEC-FE-9 i18n)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  function CardWithAriaLabel({ game }: { game: typeof BASE_GAME }) {
    const { value, source } = useGameTitle(game);
    const { t } = useTranslation();
    const ariaLabel =
      source === 'translation'
        ? t('common.localizedFromEnglish', {
            localizedTitle: value,
            originalTitle: game.title,
          })
        : undefined;
    return <h3 aria-label={ariaLabel}>{value}</h3>;
  }

  it('A1: localized title aria-label uses common.localizedFromEnglish key (UI=en)', async () => {
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [
        {
          locale: 'it',
          title: 'I Coloni di Catan',
          description: null,
          source: 'manual' as const,
        },
      ],
    };

    const { container } = renderWithIntl('en', <CardWithAriaLabel game={game} />);
    const heading = container.querySelector('h3')!;
    expect(heading.getAttribute('aria-label')).toContain('localized from English');
    expect(heading.getAttribute('aria-label')).toContain('Catan');
    expect(heading.getAttribute('aria-label')).toContain('I Coloni di Catan');

    const axeResults = await axe(container);
    expect(axeResults).toHaveNoViolations();
  });

  it('A2: canonical title rendered with no aria-label augmentation', async () => {
    mockUserLocale('en');

    const { container } = renderWithIntl('en', <CardWithAriaLabel game={BASE_GAME} />);
    const heading = container.querySelector('h3')!;
    expect(heading.getAttribute('aria-label')).toBeNull();

    const axeResults = await axe(container);
    expect(axeResults).toHaveNoViolations();
  });

  it('A3: localized title aria-label uses IT translation when UI locale=it', async () => {
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [
        {
          locale: 'it',
          title: 'I Coloni di Catan',
          description: null,
          source: 'manual' as const,
        },
      ],
    };

    const { container } = renderWithIntl('it', <CardWithAriaLabel game={game} />);
    const heading = container.querySelector('h3')!;
    expect(heading.getAttribute('aria-label')).toContain('tradotto da');
    expect(heading.getAttribute('aria-label')).toContain('Catan');
    expect(heading.getAttribute('aria-label')).toContain('I Coloni di Catan');

    const axeResults = await axe(container);
    expect(axeResults).toHaveNoViolations();
  });
});
