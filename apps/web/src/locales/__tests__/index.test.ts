/**
 * Tests for i18n locales configuration and fallback logic
 */

import { getMessages, messages, flattenMessages, LOCALES, DEFAULT_LOCALE } from '../index';

describe('i18n locales', () => {
  it('should return Italian messages for Italian locale', () => {
    const itMessages = getMessages(LOCALES.IT);
    expect(itMessages).toBeDefined();
    expect(Object.keys(itMessages).length).toBeGreaterThan(0);
  });

  it('should return English messages for English locale', () => {
    const enMessages = getMessages(LOCALES.EN);
    expect(enMessages).toBeDefined();
    expect(Object.keys(enMessages).length).toBeGreaterThan(0);
  });

  it('should have separate message catalogs for Italian and English', () => {
    const enMessages = getMessages(LOCALES.EN);
    const itMessages = getMessages(LOCALES.IT);

    // English and Italian should have their own catalogs
    expect(enMessages).not.toBe(itMessages);
    expect(Object.keys(enMessages).length).toBeGreaterThan(0);
    expect(Object.keys(itMessages).length).toBeGreaterThan(0);
  });

  it('should have Italian as default locale', () => {
    expect(DEFAULT_LOCALE).toBe(LOCALES.IT);
  });

  it('should handle undefined locale gracefully', () => {
    // @ts-expect-error Testing invalid locale
    const invalidMessages = getMessages('fr');
    const defaultMessages = messages[DEFAULT_LOCALE];

    expect(invalidMessages).toBe(defaultMessages);
  });

  // Issue #3130: guard against it/en drift for the sections that were missing
  // from en.json (KbStatusBadge / PdfProcessingStatus / NotificationCenter use
  // a non-scoped `t`, so a missing en section leaves EN users without translations).
  it.each(['pdfIndexing', 'kbStatus', 'notificationCenter'])(
    'should have matching "%s" keys in the it and en catalogs',
    section => {
      const itCatalog = getMessages(LOCALES.IT) as Record<string, unknown>;
      const enCatalog = getMessages(LOCALES.EN) as Record<string, unknown>;

      expect(itCatalog[section], `it.json is missing "${section}"`).toBeDefined();
      expect(enCatalog[section], `en.json is missing "${section}"`).toBeDefined();

      const itKeys = Object.keys(
        flattenMessages(itCatalog[section] as Record<string, unknown>)
      ).sort();
      const enKeys = Object.keys(
        flattenMessages(enCatalog[section] as Record<string, unknown>)
      ).sort();

      expect(enKeys).toEqual(itKeys);
    }
  );

  // Issue #3166: PlayerDetailView builds trendLabels via a non-scoped
  // t('pages.playerDetail.sections.trend.*') with no defaultMessage, so a missing section
  // makes PlayerTrendCard render raw ids (title, deltas, aria-labels, 12 month labels).
  describe('pages.playerDetail.sections.trend (Issue #3166)', () => {
    const EXPECTED_KEYS = [
      'title',
      'deltaUp',
      'deltaDown',
      'deltaFlat',
      'deltaUpAriaLabel',
      'deltaDownAriaLabel',
      'deltaFlatAriaLabel',
      'empty',
      'trendSummaryAriaLabel',
      'monthsShort.jan',
      'monthsShort.feb',
      'monthsShort.mar',
      'monthsShort.apr',
      'monthsShort.may',
      'monthsShort.jun',
      'monthsShort.jul',
      'monthsShort.aug',
      'monthsShort.sep',
      'monthsShort.oct',
      'monthsShort.nov',
      'monthsShort.dec',
    ].sort();

    const navigateToTrend = (catalog: Record<string, unknown>): unknown =>
      ['pages', 'playerDetail', 'sections', 'trend'].reduce<unknown>(
        (acc, key) => (acc as Record<string, unknown> | undefined)?.[key],
        catalog
      );

    it.each([
      ['it', LOCALES.IT],
      ['en', LOCALES.EN],
    ])('has the full trend section in the %s catalog', (name, locale) => {
      const section = navigateToTrend(getMessages(locale) as Record<string, unknown>);
      expect(section, `${name}.json is missing pages.playerDetail.sections.trend`).toBeDefined();

      const keys = Object.keys(flattenMessages(section as Record<string, unknown>)).sort();
      expect(keys).toEqual(EXPECTED_KEYS);
    });
  });

  // Issue #3168: /play-records/[id]/edit reuses SessionCreateForm with the scoped translator
  // ns='playRecords.edit'. Every wizard-chrome key the form resolves in create mode
  // (playRecords.new.*) must therefore also exist under playRecords.edit, otherwise edit mode
  // renders raw ids (step1.title, step2.dateLabel, actions.next, aria-labels, ...).
  describe('playRecords.edit ⊇ playRecords.new (Issue #3168)', () => {
    it.each([
      ['it', LOCALES.IT],
      ['en', LOCALES.EN],
    ])('playRecords.edit contains every playRecords.new key in the %s catalog', (name, locale) => {
      const catalog = getMessages(locale) as Record<string, unknown>;
      const playRecords = (catalog.playRecords ?? {}) as Record<string, unknown>;

      const newKeys = Object.keys(
        flattenMessages((playRecords.new ?? {}) as Record<string, unknown>)
      );
      const editKeys = new Set(
        Object.keys(flattenMessages((playRecords.edit ?? {}) as Record<string, unknown>))
      );

      const missing = newKeys.filter(key => !editKeys.has(key)).sort();
      expect(missing, `${name}.json playRecords.edit is missing wizard-chrome keys`).toEqual([]);
    });
  });
});
