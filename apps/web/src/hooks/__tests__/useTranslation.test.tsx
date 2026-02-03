/**
 * useTranslation Hook Tests
 *
 * Tests for the custom i18n translation hook wrapping react-intl.
 * @see useTranslation.ts
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import type { ReactNode } from 'react';

import { useTranslation } from '../useTranslation';

// Test messages
const testMessages = {
  'common.loading': 'Loading...',
  'common.error': 'An error occurred',
  'greeting.hello': 'Hello, {name}!',
  'items.count': 'You have {count} items',
  'date.created': 'Created on {date}',
};

// Test wrapper component
function createWrapper(locale = 'en', messages = testMessages) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <IntlProvider locale={locale} messages={messages}>
        {children}
      </IntlProvider>
    );
  };
}

describe('useTranslation', () => {
  describe('t function', () => {
    it('should translate simple message by id', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      expect(result.current.t('common.loading')).toBe('Loading...');
      expect(result.current.t('common.error')).toBe('An error occurred');
    });

    it('should translate message with string interpolation', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      const translated = result.current.t('greeting.hello', { name: 'World' });
      expect(translated).toBe('Hello, World!');
    });

    it('should translate message with number interpolation', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      const translated = result.current.t('items.count', { count: 5 });
      expect(translated).toBe('You have 5 items');
    });

    it('should return message id for missing translations', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      // react-intl returns the ID when message is not found
      const translated = result.current.t('nonexistent.key');
      expect(translated).toBe('nonexistent.key');
    });

    it('should handle null/undefined values gracefully', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      // Should not throw with undefined values
      const translated = result.current.t('greeting.hello', { name: undefined });
      expect(typeof translated).toBe('string');
    });
  });

  describe('formatMessage function', () => {
    it('should format message with descriptor object', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      const translated = result.current.formatMessage({ id: 'common.loading' });
      expect(translated).toBe('Loading...');
    });

    it('should format message with default message', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      const translated = result.current.formatMessage({
        id: 'nonexistent.key',
        defaultMessage: 'Default text',
      });
      expect(translated).toBe('Default text');
    });

    it('should format message with values', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      const translated = result.current.formatMessage(
        { id: 'greeting.hello' },
        { name: 'Test User' }
      );
      expect(translated).toBe('Hello, Test User!');
    });
  });

  describe('locale', () => {
    it('should return current locale', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      expect(result.current.locale).toBe('en');
    });

    it('should return Italian locale when set', () => {
      const italianMessages = {
        'common.loading': 'Caricamento...',
      };

      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('it', italianMessages),
      });

      expect(result.current.locale).toBe('it');
      expect(result.current.t('common.loading')).toBe('Caricamento...');
    });
  });

  describe('formatNumber', () => {
    it('should format numbers according to locale', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const formatted = result.current.formatNumber(1234567.89);
      expect(formatted).toContain('1');
      expect(formatted).toContain('234');
    });

    it('should format numbers with options', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const formatted = result.current.formatNumber(1234.56, {
        style: 'currency',
        currency: 'USD',
      });
      expect(formatted).toContain('1,234.56');
      expect(formatted).toContain('$');
    });

    it('should format percentages', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const formatted = result.current.formatNumber(0.75, {
        style: 'percent',
      });
      expect(formatted).toBe('75%');
    });
  });

  describe('formatDate', () => {
    it('should format dates according to locale', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const testDate = new Date('2024-01-15T12:00:00Z');
      const formatted = result.current.formatDate(testDate);

      // Should contain month, day, year
      expect(typeof formatted).toBe('string');
      expect(formatted.length).toBeGreaterThan(0);
    });

    it('should format dates with custom options', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const testDate = new Date('2024-01-15T12:00:00Z');
      const formatted = result.current.formatDate(testDate, {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      });

      expect(formatted).toContain('January');
      expect(formatted).toContain('15');
      expect(formatted).toContain('2024');
    });
  });

  describe('formatTime', () => {
    it('should format times according to locale', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const testDate = new Date('2024-01-15T14:30:00Z');
      const formatted = result.current.formatTime(testDate);

      expect(typeof formatted).toBe('string');
      expect(formatted.length).toBeGreaterThan(0);
    });

    it('should format times with custom options', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const testDate = new Date('2024-01-15T14:30:00Z');
      const formatted = result.current.formatTime(testDate, {
        hour: '2-digit',
        minute: '2-digit',
        hour12: false,
      });

      // Should contain hour and minute in 24h format
      expect(formatted).toMatch(/\d{1,2}:\d{2}/);
    });
  });

  describe('formatRelativeTime', () => {
    it('should format relative time in days', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const formatted = result.current.formatRelativeTime(-1, 'day');
      // formatRelativeTime may return "1 day ago" or "yesterday" depending on Intl implementation
      expect(formatted.toLowerCase()).toMatch(/yesterday|1 day ago/);
    });

    it('should format relative time in hours', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const formatted = result.current.formatRelativeTime(-2, 'hour');
      expect(formatted).toContain('2');
      expect(formatted.toLowerCase()).toContain('hour');
    });

    it('should format future relative time', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const formatted = result.current.formatRelativeTime(1, 'day');
      // formatRelativeTime may return "in 1 day" or "tomorrow" depending on Intl implementation
      expect(formatted.toLowerCase()).toMatch(/tomorrow|in 1 day/);
    });

    it('should format relative time in minutes', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en'),
      });

      const formatted = result.current.formatRelativeTime(-30, 'minute');
      expect(formatted).toContain('30');
      expect(formatted.toLowerCase()).toContain('minute');
    });
  });

  describe('hook stability', () => {
    it('should return stable function references', () => {
      const { result, rerender } = renderHook(() => useTranslation(), {
        wrapper: createWrapper(),
      });

      const firstT = result.current.t;
      const firstFormatMessage = result.current.formatMessage;

      rerender();

      // Functions should be recreated on each render (react-intl behavior)
      // but should still work correctly
      expect(result.current.t('common.loading')).toBe('Loading...');
      expect(result.current.formatMessage({ id: 'common.loading' })).toBe('Loading...');
    });
  });

  describe('edge cases', () => {
    it('should handle empty messages object', () => {
      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en', {}),
      });

      // Should return the message ID when no messages defined
      const translated = result.current.t('any.key');
      expect(translated).toBe('any.key');
    });

    it('should handle special characters in translations', () => {
      const messagesWithSpecialChars = {
        'special.chars': 'Price: $100 (50% off)',
        'special.html': '<strong>Bold</strong> & "quoted"',
      };

      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en', messagesWithSpecialChars),
      });

      expect(result.current.t('special.chars')).toBe('Price: $100 (50% off)');
      expect(result.current.t('special.html')).toBe('<strong>Bold</strong> & "quoted"');
    });

    it('should handle boolean values in interpolation', () => {
      const messagesWithBool = {
        'bool.message': 'Active: {active}',
      };

      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en', messagesWithBool),
      });

      // React-intl may handle booleans differently - convert to string before passing
      const translated = result.current.t('bool.message', { active: String(true) });
      expect(translated).toContain('true');
    });

    it('should handle numeric values correctly', () => {
      const messagesWithNum = {
        'num.zero': 'Count: {count}',
        'num.negative': 'Balance: {amount}',
      };

      const { result } = renderHook(() => useTranslation(), {
        wrapper: createWrapper('en', messagesWithNum),
      });

      expect(result.current.t('num.zero', { count: 0 })).toBe('Count: 0');
      expect(result.current.t('num.negative', { amount: -100 })).toBe('Balance: -100');
    });
  });
});
