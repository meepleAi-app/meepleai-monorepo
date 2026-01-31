/**
 * Tests for useUserLocale hook
 *
 * Issue #2254: Get locale from user preferences instead of hardcoding
 *
 * Test Coverage:
 * - User preference locale (from profile)
 * - Browser language fallback
 * - Default locale fallback
 * - Locale normalization
 * - Error handling
 */

import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useUserLocale, getBrowserLocale, type SupportedLocale } from '../useUserLocale';
import { api } from '@/lib/api';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getProfile: vi.fn(),
    },
  },
}));

const mockGetProfile = api.auth.getProfile as ReturnType<typeof vi.fn>;

// Mock navigator.language
const mockNavigatorLanguage = (language: string | null, languages?: string[]) => {
  Object.defineProperty(window.navigator, 'language', {
    writable: true,
    configurable: true,
    value: language,
  });

  if (languages) {
    Object.defineProperty(window.navigator, 'languages', {
      writable: true,
      configurable: true,
      value: languages,
    });
  }
};

describe('useUserLocale', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset navigator mocks
    mockNavigatorLanguage('en-US', ['en-US']);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('User Preference (Priority 1)', () => {
    it('should return user preference locale from profile', async () => {
      // Mock API response with Italian preference
      mockGetProfile.mockResolvedValueOnce({
        language: 'it',
        email: 'test@example.com',
        displayName: 'Test User',
        id: 'test-id',
        role: 'User',
        createdAt: new Date().toISOString(),
        isTwoFactorEnabled: false,
        twoFactorEnabledAt: null,
        theme: 'system',
        emailNotifications: true,
        dataRetentionDays: 90,
      });

      const { result } = renderHook(() => useUserLocale());

      // Initially returns default
      expect(result.current).toBe('it');

      // Wait for async effect to complete
      await waitFor(() => {
        expect(result.current).toBe('it');
      });

      expect(mockGetProfile).toHaveBeenCalled();
    });

    it('should return English when user prefers English', async () => {
      mockGetProfile.mockResolvedValueOnce({
        language: 'en',
        email: 'test@example.com',
        displayName: 'Test User',
        id: 'test-id',
        role: 'User',
        createdAt: new Date().toISOString(),
        isTwoFactorEnabled: false,
        twoFactorEnabledAt: null,
        theme: 'system',
        emailNotifications: true,
        dataRetentionDays: 90,
      });

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('en');
      });
    });

    it('should support all available locales', async () => {
      const locales: SupportedLocale[] = ['it', 'en', 'es', 'fr', 'de'];

      for (const locale of locales) {
        mockGetProfile.mockResolvedValueOnce({
          language: locale,
          email: 'test@example.com',
          displayName: 'Test User',
          id: 'test-id',
          role: 'User',
          createdAt: new Date().toISOString(),
          isTwoFactorEnabled: false,
          twoFactorEnabledAt: null,
          theme: 'system',
          emailNotifications: true,
          dataRetentionDays: 90,
        });

        const { result } = renderHook(() => useUserLocale());

        await waitFor(() => {
          expect(result.current).toBe(locale);
        });

        vi.clearAllMocks();
      }
    });
  });

  describe('Browser Language Fallback (Priority 2)', () => {
    it('should fallback to browser language when API fails', async () => {
      mockGetProfile.mockRejectedValueOnce(new Error('Network error'));
      mockNavigatorLanguage('fr-FR', ['fr-FR']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('fr');
      });
    });

    it('should fallback to browser language when user not logged in', async () => {
      mockGetProfile.mockResolvedValueOnce(null);
      mockNavigatorLanguage('de-DE', ['de-DE']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('de');
      });
    });

    it('should normalize browser language code (en-US → en)', async () => {
      mockGetProfile.mockResolvedValueOnce(null);
      mockNavigatorLanguage('en-US', ['en-US']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('en');
      });
    });

    it('should normalize browser language code (es-ES → es)', async () => {
      mockGetProfile.mockResolvedValueOnce(null);
      mockNavigatorLanguage('es-ES', ['es-ES']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('es');
      });
    });

    it('should use navigator.languages[0] when available', async () => {
      mockGetProfile.mockResolvedValueOnce(null);
      mockNavigatorLanguage('en-US', ['it-IT', 'en-US', 'fr-FR']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('it');
      });
    });
  });

  describe('Default Locale Fallback (Priority 3)', () => {
    it('should fallback to default locale when API fails and browser language unsupported', async () => {
      mockGetProfile.mockRejectedValueOnce(new Error('Network error'));
      mockNavigatorLanguage('zh-CN', ['zh-CN']); // Chinese not supported

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('it'); // Default Italian
      });
    });

    it('should fallback to default when user preference is invalid', async () => {
      mockGetProfile.mockResolvedValueOnce({
        language: 'invalid-locale',
        email: 'test@example.com',
        displayName: 'Test User',
        id: 'test-id',
        role: 'User',
        createdAt: new Date().toISOString(),
        isTwoFactorEnabled: false,
        twoFactorEnabledAt: null,
        theme: 'system',
        emailNotifications: true,
        dataRetentionDays: 90,
      } as any); // Type assertion needed for invalid locale test
      mockNavigatorLanguage('zh-CN', ['zh-CN']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('it');
      });
    });

    it('should fallback to default when navigator.language is null', async () => {
      mockGetProfile.mockResolvedValueOnce(null);
      mockNavigatorLanguage(null, []);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('it');
      });
    });
  });

  describe('getBrowserLocale (Synchronous Helper)', () => {
    it('should return browser locale when supported', () => {
      mockNavigatorLanguage('en-GB', ['en-GB']);
      expect(getBrowserLocale()).toBe('en');
    });

    it('should return default locale when browser language unsupported', () => {
      mockNavigatorLanguage('zh-CN', ['zh-CN']);
      expect(getBrowserLocale()).toBe('it');
    });

    it('should return default locale when navigator is unavailable', () => {
      mockNavigatorLanguage(null, []);
      expect(getBrowserLocale()).toBe('it');
    });

    it('should normalize language codes correctly', () => {
      const testCases: Array<[string, SupportedLocale]> = [
        ['it-IT', 'it'],
        ['en-US', 'en'],
        ['en-GB', 'en'],
        ['es-ES', 'es'],
        ['fr-FR', 'fr'],
        ['de-DE', 'de'],
      ];

      for (const [input, expected] of testCases) {
        mockNavigatorLanguage(input, [input]);
        expect(getBrowserLocale()).toBe(expected);
      }
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty user response', async () => {
      mockGetProfile.mockResolvedValueOnce(null);
      mockNavigatorLanguage('en-US', ['en-US']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('en');
      });
    });

    it('should handle malformed API response', async () => {
      mockGetProfile.mockRejectedValueOnce(new Error('Invalid response'));
      mockNavigatorLanguage('fr-FR', ['fr-FR']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('fr');
      });
    });

    it('should handle network timeout', async () => {
      mockGetProfile.mockRejectedValueOnce(new Error('Timeout'));
      mockNavigatorLanguage('de-DE', ['de-DE']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('de');
      });
    });
  });

  describe('Integration Scenarios', () => {
    it('should prioritize user preference over browser language', async () => {
      mockGetProfile.mockResolvedValueOnce({
        language: 'it',
        email: 'test@example.com',
        displayName: 'Test User',
        id: 'test-id',
        role: 'User',
        createdAt: new Date().toISOString(),
        isTwoFactorEnabled: false,
        twoFactorEnabledAt: null,
        theme: 'system',
        emailNotifications: true,
        dataRetentionDays: 90,
      });
      mockNavigatorLanguage('en-US', ['en-US']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('it'); // User preference wins
      });
    });

    it('should use browser language when user has no preference', async () => {
      mockGetProfile.mockResolvedValueOnce({
        language: null,
        email: 'test@example.com',
        displayName: 'Test User',
        id: 'test-id',
        role: 'User',
        createdAt: new Date().toISOString(),
        isTwoFactorEnabled: false,
        twoFactorEnabledAt: null,
        theme: 'system',
        emailNotifications: true,
        dataRetentionDays: 90,
      } as any);
      mockNavigatorLanguage('es-MX', ['es-MX']);

      const { result } = renderHook(() => useUserLocale());

      await waitFor(() => {
        expect(result.current).toBe('es');
      });
    });
  });
});
