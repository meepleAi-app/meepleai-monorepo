import { isSourceLangCode, type SourceLangCode } from './lang-codes';

export const LAST_USED_LANG_KEY = 'gamebook.last_used_source_lang';
const DEFAULT_LANG: SourceLangCode = 'IT';

function safeLocalStorage(): Storage | null {
  try {
    if (typeof window === 'undefined') return null;
    return window.localStorage ?? null;
  } catch {
    return null; // SecurityError in private mode / disabled cookies
  }
}

export function getLastUsedLang(): SourceLangCode {
  const storage = safeLocalStorage();
  if (!storage) return DEFAULT_LANG;
  const raw = storage.getItem(LAST_USED_LANG_KEY);
  return isSourceLangCode(raw) ? raw : DEFAULT_LANG;
}

export function setLastUsedLang(code: SourceLangCode): void {
  if (!isSourceLangCode(code)) return;
  const storage = safeLocalStorage();
  if (!storage) return;
  try {
    storage.setItem(LAST_USED_LANG_KEY, code);
  } catch {
    // QuotaExceededError or similar — silently ignore
  }
}
