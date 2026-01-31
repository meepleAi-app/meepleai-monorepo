/**
 * Tests for i18n locales configuration and fallback logic
 */

import { getMessages, messages, LOCALES, DEFAULT_LOCALE } from '../index';

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
});
