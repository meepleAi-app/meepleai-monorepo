/**
 * Unit tests for i18n locales configuration and utilities
 *
 * Issue #990: BGAI-049 - i18n setup (React Intl, it.json)
 */

import {
  LOCALES,
  DEFAULT_LOCALE,
  messages,
  getMessages,
  flattenMessages,
  type Locale,
} from '@/locales';

describe('Locales Configuration', () => {
  describe('LOCALES constant', () => {
    it('should define Italian locale', () => {
      expect(LOCALES.IT).toBe('it');
    });

    it('should define English locale', () => {
      expect(LOCALES.EN).toBe('en');
    });
  });

  describe('DEFAULT_LOCALE', () => {
    it('should be Italian', () => {
      expect(DEFAULT_LOCALE).toBe('it');
    });
  });

  describe('messages object', () => {
    it('should have Italian messages', () => {
      expect(messages[LOCALES.IT]).toBeDefined();
      expect(messages[LOCALES.IT]).not.toEqual({});
    });

    it('should have common translations in Italian', () => {
      const itMessages = messages[LOCALES.IT] as Record<string, any>;
      expect(itMessages.common).toBeDefined();
      expect(itMessages.common.loading).toBeDefined();
      expect(typeof itMessages.common.loading).toBe('string');
    });

    it('should have auth translations in Italian', () => {
      const itMessages = messages[LOCALES.IT] as Record<string, any>;
      expect(itMessages.auth).toBeDefined();
      expect(itMessages.auth.login).toBeDefined();
      expect(itMessages.auth.login.title).toBeDefined();
    });

    it('should have English placeholder', () => {
      expect(messages[LOCALES.EN]).toBeDefined();
      expect(messages[LOCALES.EN]).toEqual({});
    });
  });

  describe('getMessages function', () => {
    it('should return Italian messages for "it" locale', () => {
      const msgs = getMessages('it' as Locale);
      expect(msgs).toBeDefined();
      expect(msgs).toEqual(messages[LOCALES.IT]);
    });

    it('should return English messages for "en" locale', () => {
      const msgs = getMessages('en' as Locale);
      expect(msgs).toBeDefined();
      expect(msgs).toEqual(messages[LOCALES.EN]);
    });

    it('should return default locale messages for undefined', () => {
      const msgs = getMessages(undefined as any);
      expect(msgs).toBeDefined();
    });
  });

  describe('flattenMessages function', () => {
    it('should flatten nested messages', () => {
      const nested = {
        common: {
          loading: 'Loading...',
          error: 'Error',
        },
        auth: {
          login: {
            title: 'Login',
          },
        },
      };

      const flattened = flattenMessages(nested);

      expect(flattened).toEqual({
        'common.loading': 'Loading...',
        'common.error': 'Error',
        'auth.login.title': 'Login',
      });
    });

    it('should handle single-level messages', () => {
      const messages = {
        hello: 'Hello',
        world: 'World',
      };

      const flattened = flattenMessages(messages);

      expect(flattened).toEqual({
        hello: 'Hello',
        world: 'World',
      });
    });

    it('should handle empty object', () => {
      const flattened = flattenMessages({});
      expect(flattened).toEqual({});
    });

    it('should skip non-string values at leaf level', () => {
      const messages = {
        valid: 'Valid string',
        invalid: null,
        nested: {
          value: 'Nested value',
        },
      };

      const flattened = flattenMessages(messages as any);

      expect(flattened).toEqual({
        valid: 'Valid string',
        'nested.value': 'Nested value',
      });
    });

    it('should handle deeply nested messages', () => {
      const nested = {
        level1: {
          level2: {
            level3: {
              level4: 'Deep value',
            },
          },
        },
      };

      const flattened = flattenMessages(nested);

      expect(flattened).toEqual({
        'level1.level2.level3.level4': 'Deep value',
      });
    });
  });

  describe('Italian translations structure', () => {
    const itMessages = messages[LOCALES.IT] as Record<string, any>;

    it('should have all required categories', () => {
      const requiredCategories = [
        'common',
        'navigation',
        'app',
        'auth',
        'games',
        'chat',
        'upload',
        'settings',
        'admin',
        'errors',
        'validation',
        'dates',
        'accessibility',
      ];

      requiredCategories.forEach((category) => {
        expect(itMessages[category]).toBeDefined();
      });
    });

    it('should have common button labels', () => {
      expect(itMessages.common.save).toBeDefined();
      expect(itMessages.common.cancel).toBeDefined();
      expect(itMessages.common.delete).toBeDefined();
      expect(itMessages.common.edit).toBeDefined();
      expect(itMessages.common.close).toBeDefined();
    });

    it('should have navigation items', () => {
      expect(itMessages.navigation.home).toBeDefined();
      expect(itMessages.navigation.games).toBeDefined();
      expect(itMessages.navigation.chat).toBeDefined();
      expect(itMessages.navigation.settings).toBeDefined();
    });

    it('should have auth login translations', () => {
      expect(itMessages.auth.login.title).toBeDefined();
      expect(itMessages.auth.login.email).toBeDefined();
      expect(itMessages.auth.login.password).toBeDefined();
      expect(itMessages.auth.login.submit).toBeDefined();
    });

    it('should have validation messages', () => {
      expect(itMessages.validation.required).toBeDefined();
      expect(itMessages.validation.email).toBeDefined();
      expect(itMessages.validation.minLength).toBeDefined();
      expect(itMessages.validation.maxLength).toBeDefined();
    });

    it('should have accessibility labels', () => {
      expect(itMessages.accessibility.skipToMain).toBeDefined();
      expect(itMessages.accessibility.menu).toBeDefined();
      expect(itMessages.accessibility.loading).toBeDefined();
    });
  });

  describe('Type safety', () => {
    it('should accept valid Locale type', () => {
      const locale: Locale = 'it';
      expect(locale).toBe('it');
    });

    it('should provide correct types for Messages', () => {
      const itMessages = messages[LOCALES.IT] as Record<string, any>;
      expect(typeof itMessages).toBe('object');
    });
  });
});
