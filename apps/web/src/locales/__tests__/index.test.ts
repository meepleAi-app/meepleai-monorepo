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

  it('should fallback to Italian messages when English locale is empty', () => {
    const enMessages = getMessages(LOCALES.EN);
    const itMessages = getMessages(LOCALES.IT);

    // English messages should fallback to Italian since EN is empty
    expect(enMessages).toBe(itMessages);
    expect(Object.keys(enMessages).length).toBeGreaterThan(0);
  });

  it('should fallback to default locale for empty messages', () => {
    const emptyLocaleMessages = getMessages(LOCALES.EN);
    const defaultMessages = messages[DEFAULT_LOCALE];

    expect(emptyLocaleMessages).toBe(defaultMessages);
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
