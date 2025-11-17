/**
 * Unit tests for useTranslation hook
 *
 * Issue #990: BGAI-049 - i18n setup (React Intl, it.json)
 */

import { renderHook } from '@testing-library/react';
import { ReactNode } from 'react';
import { useTranslation } from '@/hooks/useTranslation';
import { IntlProvider } from '@/components/providers/IntlProvider';
import { LOCALES } from '@/locales';

// Wrapper component for tests
function createWrapper(locale?: string) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <IntlProvider locale={locale as any}>{children}</IntlProvider>;
  };
}

describe('useTranslation hook', () => {
  describe('Basic functionality', () => {
    it('should return translation function', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      expect(result.current.t).toBeDefined();
      expect(typeof result.current.t).toBe('function');
    });

    it('should return formatMessage function', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      expect(result.current.formatMessage).toBeDefined();
      expect(typeof result.current.formatMessage).toBe('function');
    });

    it('should return current locale', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      expect(result.current.locale).toBe('it');
    });

    it('should return formatting utilities', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      expect(result.current.formatNumber).toBeDefined();
      expect(result.current.formatDate).toBeDefined();
      expect(result.current.formatTime).toBeDefined();
      expect(result.current.formatRelativeTime).toBeDefined();
    });
  });

  describe('Translation function (t)', () => {
    it('should translate simple keys', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      expect(result.current.t('common.loading')).toBe('Caricamento...');
      expect(result.current.t('common.error')).toBe('Errore');
      expect(result.current.t('common.success')).toBe('Successo');
    });

    it('should translate nested keys', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      expect(result.current.t('auth.login.title')).toBe('Accedi');
      expect(result.current.t('auth.login.email')).toBe('Email');
      expect(result.current.t('auth.register.title')).toBe('Registrati');
    });

    it('should support variable interpolation', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const translated = result.current.t('auth.session.expiringSoon', { minutes: 5 });
      expect(translated).toBe('La tua sessione scadrà tra 5 minuti.');
    });

    it('should handle multiple variables', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const translated = result.current.t('validation.minLength', { min: 8 });
      expect(translated).toBe('Deve contenere almeno 8 caratteri');
    });
  });

  describe('formatMessage function', () => {
    it('should format messages with descriptor', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const message = result.current.formatMessage({
        id: 'common.loading',
        defaultMessage: 'Loading...',
      });

      expect(message).toBe('Caricamento...');
    });

    it('should support values in descriptor', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const message = result.current.formatMessage(
        {
          id: 'validation.maxLength',
          defaultMessage: 'Cannot exceed {max} characters',
        },
        { max: 100 }
      );

      expect(message).toBe('Non può superare 100 caratteri');
    });
  });

  describe('Number formatting', () => {
    it('should format integers', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const formatted = result.current.formatNumber(1234);
      expect(formatted).toBeTruthy();
      expect(typeof formatted).toBe('string');
    });

    it('should format decimals', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const formatted = result.current.formatNumber(1234.56);
      expect(formatted).toBeTruthy();
      expect(typeof formatted).toBe('string');
    });

    it('should accept formatting options', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const formatted = result.current.formatNumber(0.75, {
        style: 'percent',
      });

      expect(formatted).toBeTruthy();
      expect(formatted).toContain('75');
    });
  });

  describe('Date formatting', () => {
    it('should format dates', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const date = new Date('2025-11-17T12:00:00Z');
      const formatted = result.current.formatDate(date);

      expect(formatted).toBeTruthy();
      expect(typeof formatted).toBe('string');
    });

    it('should accept date formatting options', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const date = new Date('2025-11-17T12:00:00Z');
      const formatted = result.current.formatDate(date, {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      });

      expect(formatted).toBeTruthy();
      expect(formatted).toContain('2025');
    });
  });

  describe('Time formatting', () => {
    it('should format time', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const date = new Date('2025-11-17T14:30:00Z');
      const formatted = result.current.formatTime(date);

      expect(formatted).toBeTruthy();
      expect(typeof formatted).toBe('string');
    });

    it('should accept time formatting options', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const date = new Date('2025-11-17T14:30:00Z');
      const formatted = result.current.formatTime(date, {
        hour: '2-digit',
        minute: '2-digit',
      });

      expect(formatted).toBeTruthy();
    });
  });

  describe('Relative time formatting', () => {
    it('should format relative time', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const formatted = result.current.formatRelativeTime(-2, 'hours');

      expect(formatted).toBeTruthy();
      expect(typeof formatted).toBe('string');
    });

    it('should handle positive values', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const formatted = result.current.formatRelativeTime(3, 'days');

      expect(formatted).toBeTruthy();
      expect(typeof formatted).toBe('string');
    });

    it('should handle different time units', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const hours = result.current.formatRelativeTime(-2, 'hours');
      const days = result.current.formatRelativeTime(5, 'days');
      const months = result.current.formatRelativeTime(-1, 'months');

      expect(hours).toBeTruthy();
      expect(days).toBeTruthy();
      expect(months).toBeTruthy();
    });
  });

  describe('Different locales', () => {
    it('should use Italian locale', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      expect(result.current.locale).toBe('it');
      expect(result.current.t('common.loading')).toBe('Caricamento...');
    });

    it('should use English locale', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.EN),
      });

      expect(result.current.locale).toBe('en');
    });
  });

  describe('Edge cases', () => {
    it('should handle empty values object', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const translated = result.current.t('common.loading', {});
      expect(translated).toBe('Caricamento...');
    });

    it('should handle undefined values', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const translated = result.current.t('common.loading', undefined);
      expect(translated).toBe('Caricamento...');
    });

    it('should handle null values in interpolation', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(LOCALES.IT),
      });

      const translated = result.current.t('auth.session.expiringSoon', {
        minutes: null as any,
      });
      expect(translated).toBeTruthy();
    });
  });

  describe('Type safety', () => {
    it('should provide correct TypeScript types', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      // Type checks - these should compile without errors
      const _t: typeof result.current.t = result.current.t;
      const _formatMessage: typeof result.current.formatMessage = result.current.formatMessage;
      const _locale: string = result.current.locale;
      const _formatNumber: typeof result.current.formatNumber = result.current.formatNumber;

      expect(_t).toBeDefined();
      expect(_formatMessage).toBeDefined();
      expect(_locale).toBeDefined();
      expect(_formatNumber).toBeDefined();
    });
  });
});
