import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { getLastUsedLang, setLastUsedLang, LAST_USED_LANG_KEY } from '../last-used-lang';

describe('last-used-lang', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('LAST_USED_LANG_KEY is namespaced', () => {
    expect(LAST_USED_LANG_KEY).toBe('gamebook.last_used_source_lang');
  });

  it('getLastUsedLang returns IT default when localStorage empty', () => {
    expect(getLastUsedLang()).toBe('IT');
  });

  it('getLastUsedLang returns stored valid lang', () => {
    localStorage.setItem(LAST_USED_LANG_KEY, 'FR');
    expect(getLastUsedLang()).toBe('FR');
  });

  it('getLastUsedLang returns IT default for invalid stored value', () => {
    localStorage.setItem(LAST_USED_LANG_KEY, 'XX');
    expect(getLastUsedLang()).toBe('IT');
  });

  it('setLastUsedLang persists valid lang', () => {
    setLastUsedLang('DE');
    expect(localStorage.getItem(LAST_USED_LANG_KEY)).toBe('DE');
  });

  it('setLastUsedLang ignores invalid lang code (defensive)', () => {
    // @ts-expect-error testing runtime defensive path
    setLastUsedLang('XX');
    expect(localStorage.getItem(LAST_USED_LANG_KEY)).toBeNull();
  });

  it('getLastUsedLang returns IT when localStorage unavailable (SSR/private mode simulation)', () => {
    const original = window.localStorage;
    Object.defineProperty(window, 'localStorage', { value: undefined, configurable: true });
    expect(getLastUsedLang()).toBe('IT');
    Object.defineProperty(window, 'localStorage', { value: original, configurable: true });
  });
});
