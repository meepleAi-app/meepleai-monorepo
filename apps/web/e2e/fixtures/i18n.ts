/**
 * i18n Test Helper for E2E Tests
 *
 * Purpose: Provides language-agnostic text matching for E2E tests
 * to handle both English and Italian (or any other language) UI text.
 *
 * Problem: Tests were written in English but UI is in Italian,
 * causing 60% of test failures due to text mismatches.
 *
 * Solution: Define text mappings for multiple languages and provide
 * flexible matchers that work with any configured language.
 *
 * Usage:
 * ```typescript
 * import { t, getTextMatcher } from './fixtures/i18n';
 *
 * // Option 1: Use translation keys
 * await page.getByRole('button', { name: t('auth.login') });
 *
 * // Option 2: Use flexible matchers (regex that matches any language)
 * const matcher = getTextMatcher('auth.login');
 * await page.getByRole('button', { name: matcher });
 * ```
 */

import enMessages from '../../src/locales/en.json';
import itMessages from '../../src/locales/it.json';

type NestedMessages = Record<string, string | Record<string, unknown>>;

const SUPPORTED_LANGUAGES = ['it', 'en'] as const;
type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];
const DEFAULT_LANGUAGE: SupportedLanguage = 'it';

function flattenMessages(nestedMessages: NestedMessages, prefix = ''): Record<string, string> {
  return Object.entries(nestedMessages).reduce(
    (messages, [key, value]) => {
      const prefixedKey = prefix ? `${prefix}.${key}` : key;

      if (typeof value === 'string') {
        messages[prefixedKey] = value;
      } else if (typeof value === 'object' && value !== null) {
        Object.assign(messages, flattenMessages(value as NestedMessages, prefixedKey));
      }

      return messages;
    },
    {} as Record<string, string>
  );
}

const catalog: Record<SupportedLanguage, Record<string, string>> = {
  it: flattenMessages(itMessages as NestedMessages),
  en: flattenMessages(enMessages as NestedMessages),
};

function getCatalogTranslation(key: string, lang: SupportedLanguage): string | undefined {
  return catalog[lang]?.[key];
}

function resolveTranslation(key: string, lang: SupportedLanguage): string | undefined {
  return getCatalogTranslation(key, lang) ?? getCatalogTranslation(key, DEFAULT_LANGUAGE);
}

function getTranslationVariants(key: string): string[] {
  const variants = new Set<string>();

  for (const lang of SUPPORTED_LANGUAGES) {
    const value = getCatalogTranslation(key, lang);
    if (value) {
      variants.add(value);
    }
  }

  if (variants.size === 0) {
    const fallback = getCatalogTranslation(key, DEFAULT_LANGUAGE);
    if (fallback) {
      variants.add(fallback);
    }
  }

  return Array.from(variants).filter(Boolean);
}

/**
 * Detect current UI language from environment or defaults to English
 * Can be overridden with TEST_LANG environment variable
 */
export function getCurrentLanguage(): SupportedLanguage {
  const envLang = process.env.TEST_LANG?.toLowerCase();
  if (envLang === 'it' || envLang === 'italian') return 'it';
  if (envLang === 'en' || envLang === 'english') return 'en';

  // Default to English for tests unless explicitly set
  return 'en';
}

/**
 * Translate a key to the current language
 * @param key Translation key (e.g., 'auth.login')
 * @param lang Optional language override
 * @returns Translated text
 *
 * @example
 * t('auth.login') // => 'Login' (en) or 'Accedi' (it)
 * t('auth.login', 'it') // => 'Accedi'
 */
export function t(key: string, lang?: SupportedLanguage): string {
  const targetLang = lang || getCurrentLanguage();
  const translation =
    resolveTranslation(key, targetLang) ?? resolveTranslation(key, DEFAULT_LANGUAGE);

  if (!translation) {
    console.warn(`⚠️  Missing translation for key: ${key}`);
    return key;
  }

  return translation;
}

/**
 * Create a regex matcher that matches text in ANY language
 * Useful for getByRole() and similar Playwright matchers that accept regex
 *
 * @param key Translation key
 * @returns RegExp that matches any language variant
 *
 * @example
 * const matcher = getTextMatcher('auth.login');
 * await page.getByRole('button', { name: matcher });
 * // Matches both "Login" and "Accedi"
 */
export function getTextMatcher(key: string): RegExp {
  const variants = getTranslationVariants(key);

  if (variants.length === 0) {
    console.warn(`⚠️  Missing translation for key: ${key}`);
    return new RegExp(key, 'i');
  }

  // Create regex that matches any language variant (case-insensitive)
  const escaped = variants.map(text =>
    // Escape regex special characters
    text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  );

  return new RegExp(`(${escaped.join('|')})`, 'i');
}

/**
 * Create a flexible text matcher that matches partial text in any language
 * Useful for text that might be embedded in longer strings
 *
 * @param key Translation key
 * @returns RegExp with flexible matching
 *
 * @example
 * const matcher = getFlexibleMatcher('auth.login');
 * await page.getByText(matcher);
 * // Matches "Click here to Login" or "Clicca qui per Accedi"
 */
export function getFlexibleMatcher(key: string): RegExp {
  const variants = getTranslationVariants(key);

  if (variants.length === 0) {
    console.warn(`⚠️  Missing translation for key: ${key}`);
    return new RegExp(key, 'i');
  }

  // Create regex with word boundaries for partial matching
  const escaped = variants.map(text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));

  return new RegExp(`.*?(${escaped.join('|')}).*?`, 'i');
}

/**
 * Check if a string matches a translation key in any language
 * @param text Text to check
 * @param key Translation key
 * @returns true if text matches any language variant
 */
export function matchesTranslation(text: string, key: string): boolean {
  const matcher = getTextMatcher(key);
  return matcher.test(text);
}

/**
 * Get all language variants for a key
 * @param key Translation key
 * @returns Object with all language variants
 */
export function getAllVariants(key: string): { en: string; it: string } | null {
  const enVariant = resolveTranslation(key, 'en') ?? resolveTranslation(key, DEFAULT_LANGUAGE);
  const itVariant = resolveTranslation(key, 'it') ?? resolveTranslation(key, DEFAULT_LANGUAGE);

  if (!enVariant && !itVariant) {
    return null;
  }

  return {
    en: enVariant ?? itVariant ?? key,
    it: itVariant ?? enVariant ?? key,
  };
}

/**
 * Add or update a translation dynamically (for test-specific text)
 * @param key Translation key
 * @param en English text
 * @param it Italian text
 */
export function addTranslation(key: string, en: string, it: string): void {
  catalog.en[key] = en;
  catalog.it[key] = it;
}
