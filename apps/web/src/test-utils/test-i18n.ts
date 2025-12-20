/**
 * i18n Test Helper for Unit Tests (React Testing Library)
 *
 * Purpose: Provides language-agnostic text matching for unit tests
 * to handle both English and Italian (or any other language) UI text.
 *
 * Problem: Tests with hardcoded Italian strings break when UI language changes
 * or when running tests in different locale environments.
 *
 * Solution: Define text mappings for multiple languages and provide
 * flexible matchers that work with any configured language.
 *
 * Usage:
 * ```typescript
 * import { t, getTextMatcher, getByTranslation } from '@/test-utils/test-i18n';
 *
 * // Option 1: Use translation keys for exact text
 * expect(screen.getByPlaceholderText(t('chat.placeholder'))).toBeInTheDocument();
 *
 * // Option 2: Use flexible matchers (regex that matches any language)
 * expect(screen.getByRole('button', { name: getTextMatcher('common.submit') })).toBeInTheDocument();
 *
 * // Option 3: Use custom query helper
 * expect(getByTranslation(container, 'chat.sendMessage')).toBeInTheDocument();
 * ```
 *
 * @module test-utils/test-i18n
 */

import { screen, within as rtlWithin } from '@testing-library/react';

import enMessages from '../locales/en.json';
import itMessages from '../locales/it.json';

// ============================================================================
// Types and Constants
// ============================================================================

type NestedMessages = Record<string, string | Record<string, unknown>>;

const SUPPORTED_LANGUAGES = ['it', 'en'] as const;
type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];

/**
 * Default language for tests.
 * Set to 'it' to match production UI default.
 */
const DEFAULT_LANGUAGE: SupportedLanguage = 'it';

// ============================================================================
// Internal Utilities
// ============================================================================

/**
 * Flatten nested message objects into dot-notation keys.
 * { "chat": { "placeholder": "Hello" } } => { "chat.placeholder": "Hello" }
 */
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

/**
 * Pre-flattened catalog for fast lookups.
 */
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

// ============================================================================
// Public API
// ============================================================================

/**
 * Detect current test language from environment or defaults to Italian.
 * Can be overridden with TEST_LANG environment variable.
 *
 * @returns Current test language
 *
 * @example
 * // In terminal: TEST_LANG=en pnpm test
 * getCurrentLanguage() // => 'en'
 */
export function getCurrentLanguage(): SupportedLanguage {
  const envLang = process.env.TEST_LANG?.toLowerCase();
  if (envLang === 'it' || envLang === 'italian') return 'it';
  if (envLang === 'en' || envLang === 'english') return 'en';

  // Default to Italian for tests to match production UI
  return DEFAULT_LANGUAGE;
}

/**
 * Translate a key to the current language.
 *
 * @param key Translation key (e.g., 'chat.placeholder')
 * @param lang Optional language override
 * @returns Translated text
 *
 * @example
 * t('chat.placeholder') // => 'Chiedi qualcosa sul regolamento...' (it)
 * t('chat.placeholder', 'en') // => 'Ask something about the rules...'
 *
 * // In test:
 * expect(screen.getByPlaceholderText(t('chat.placeholder'))).toBeInTheDocument();
 */
export function t(key: string, lang?: SupportedLanguage): string {
  const targetLang = lang || getCurrentLanguage();
  const translation =
    resolveTranslation(key, targetLang) ?? resolveTranslation(key, DEFAULT_LANGUAGE);

  if (!translation) {
    console.warn(`[test-i18n] Missing translation for key: ${key}`);
    return key;
  }

  return translation;
}

/**
 * Create a regex matcher that matches text in ANY language.
 * Useful for getByRole(), getByText() and similar RTL matchers that accept regex.
 *
 * @param key Translation key
 * @returns RegExp that matches any language variant
 *
 * @example
 * const matcher = getTextMatcher('common.submit');
 * expect(screen.getByRole('button', { name: matcher })).toBeInTheDocument();
 * // Matches both "Submit" and "Invia"
 */
export function getTextMatcher(key: string): RegExp {
  const variants = getTranslationVariants(key);

  if (variants.length === 0) {
    console.warn(`[test-i18n] Missing translation for key: ${key}`);
    return new RegExp(key, 'i');
  }

  // Create regex that matches any language variant (case-insensitive)
  const escaped = variants.map(text =>
    // Escape regex special characters
    text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  );

  return new RegExp(`^(${escaped.join('|')})$`, 'i');
}

/**
 * Create a flexible text matcher that matches partial text in any language.
 * Useful for text that might be embedded in longer strings.
 *
 * @param key Translation key
 * @returns RegExp with flexible matching
 *
 * @example
 * const matcher = getFlexibleMatcher('common.loading');
 * expect(screen.getByText(matcher)).toBeInTheDocument();
 * // Matches "Loading..." embedded in "Please wait, Loading..."
 */
export function getFlexibleMatcher(key: string): RegExp {
  const variants = getTranslationVariants(key);

  if (variants.length === 0) {
    console.warn(`[test-i18n] Missing translation for key: ${key}`);
    return new RegExp(key, 'i');
  }

  // Create regex without anchors for partial matching
  const escaped = variants.map(text => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));

  return new RegExp(`(${escaped.join('|')})`, 'i');
}

/**
 * Check if a string matches a translation key in any language.
 *
 * @param text Text to check
 * @param key Translation key
 * @returns true if text matches any language variant
 *
 * @example
 * matchesTranslation('Invia', 'common.submit') // => true
 * matchesTranslation('Submit', 'common.submit') // => true
 * matchesTranslation('Save', 'common.submit') // => false
 */
export function matchesTranslation(text: string, key: string): boolean {
  const variants = getTranslationVariants(key);
  return variants.some(variant => variant.toLowerCase() === text.toLowerCase());
}

/**
 * Get all language variants for a key.
 *
 * @param key Translation key
 * @returns Object with all language variants or null if key not found
 *
 * @example
 * getAllVariants('common.submit')
 * // => { en: 'Submit', it: 'Invia' }
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

// ============================================================================
// React Testing Library Helpers
// ============================================================================

/**
 * Query for an element containing translated text using translation key.
 * Searches for text in ANY language variant.
 *
 * @param container Container element to search within
 * @param key Translation key
 * @returns Element containing the translated text
 * @throws If element not found
 *
 * @example
 * const { container } = render(<MyComponent />);
 * expect(getByTranslation(container, 'common.submit')).toBeInTheDocument();
 */
export function getByTranslation(container: HTMLElement, key: string): HTMLElement {
  const matcher = getFlexibleMatcher(key);
  return rtlWithin(container).getByText(matcher);
}

/**
 * Query for an element containing translated text using translation key (screen).
 * Searches the entire document.
 *
 * @param key Translation key
 * @returns Element containing the translated text
 * @throws If element not found
 *
 * @example
 * render(<MyComponent />);
 * expect(getByTranslationScreen('common.submit')).toBeInTheDocument();
 */
export function getByTranslationScreen(key: string): HTMLElement {
  const matcher = getFlexibleMatcher(key);
  return screen.getByText(matcher);
}

/**
 * Query for an element containing translated text (non-throwing version).
 *
 * @param container Container element to search within
 * @param key Translation key
 * @returns Element or null if not found
 *
 * @example
 * const { container } = render(<MyComponent />);
 * const element = queryByTranslation(container, 'errors.notFound');
 * expect(element).toBeNull(); // Element should not exist
 */
export function queryByTranslation(container: HTMLElement, key: string): HTMLElement | null {
  const matcher = getFlexibleMatcher(key);
  return rtlWithin(container).queryByText(matcher);
}

/**
 * Query for an element containing translated text (screen, non-throwing).
 *
 * @param key Translation key
 * @returns Element or null if not found
 *
 * @example
 * render(<MyComponent />);
 * expect(queryByTranslationScreen('errors.notFound')).toBeNull();
 */
export function queryByTranslationScreen(key: string): HTMLElement | null {
  const matcher = getFlexibleMatcher(key);
  return screen.queryByText(matcher);
}

/**
 * Query for a button with translated accessible name.
 *
 * @param container Container element to search within
 * @param key Translation key for button name
 * @returns Button element
 * @throws If button not found
 *
 * @example
 * const { container } = render(<Form />);
 * const submitBtn = getButtonByTranslation(container, 'common.submit');
 * await userEvent.click(submitBtn);
 */
export function getButtonByTranslation(container: HTMLElement, key: string): HTMLElement {
  const matcher = getTextMatcher(key);
  return rtlWithin(container).getByRole('button', { name: matcher });
}

/**
 * Query for an input with translated placeholder.
 *
 * @param container Container element to search within
 * @param key Translation key for placeholder
 * @returns Input element
 * @throws If input not found
 *
 * @example
 * const { container } = render(<SearchForm />);
 * const input = getInputByPlaceholder(container, 'search.placeholder');
 * await userEvent.type(input, 'test query');
 */
export function getInputByPlaceholder(container: HTMLElement, key: string): HTMLElement {
  const matcher = getFlexibleMatcher(key);
  return rtlWithin(container).getByPlaceholderText(matcher);
}

/**
 * Query for a heading with translated text.
 *
 * @param container Container element to search within
 * @param key Translation key for heading
 * @param level Optional heading level (1-6)
 * @returns Heading element
 * @throws If heading not found
 *
 * @example
 * const { container } = render(<Page />);
 * expect(getHeadingByTranslation(container, 'page.title')).toBeInTheDocument();
 */
export function getHeadingByTranslation(
  container: HTMLElement,
  key: string,
  level?: 1 | 2 | 3 | 4 | 5 | 6
): HTMLElement {
  const matcher = getTextMatcher(key);
  return rtlWithin(container).getByRole('heading', { name: matcher, level });
}

// ============================================================================
// Test Setup Utilities
// ============================================================================

/**
 * Set the test language for the current test suite.
 * Useful for testing specific language behavior.
 *
 * @param lang Language to set
 *
 * @example
 * beforeAll(() => {
 *   setTestLanguage('en');
 * });
 *
 * afterAll(() => {
 *   resetTestLanguage();
 * });
 */
export function setTestLanguage(lang: SupportedLanguage): void {
  process.env.TEST_LANG = lang;
}

/**
 * Reset test language to default.
 */
export function resetTestLanguage(): void {
  delete process.env.TEST_LANG;
}

/**
 * List all available translation keys for debugging.
 *
 * @param pattern Optional filter pattern (regex)
 * @returns Array of matching keys
 *
 * @example
 * listTranslationKeys(/^chat\./) // => ['chat.placeholder', 'chat.send', ...]
 */
export function listTranslationKeys(pattern?: RegExp): string[] {
  const keys = Object.keys(catalog[DEFAULT_LANGUAGE]);
  if (!pattern) return keys;
  return keys.filter(key => pattern.test(key));
}

/**
 * Debug helper: print translation for a key in all languages.
 *
 * @param key Translation key
 *
 * @example
 * debugTranslation('chat.placeholder');
 * // Console output:
 * // [test-i18n] chat.placeholder:
 * //   it: "Chiedi qualcosa sul regolamento..."
 * //   en: "Ask something about the rules..."
 */
export function debugTranslation(key: string): void {
  const variants = getAllVariants(key);
  if (!variants) {
    console.log(`[test-i18n] Key not found: ${key}`);
    return;
  }
  console.log(`[test-i18n] ${key}:`);
  console.log(`  it: "${variants.it}"`);
  console.log(`  en: "${variants.en}"`);
}
