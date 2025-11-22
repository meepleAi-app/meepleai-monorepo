const STORAGE_KEY = 'meepleai:apiKey';

let memoryApiKey: string | null = null;

const isBrowser = () => typeof window !== 'undefined' && typeof window.sessionStorage !== 'undefined';

export function setStoredApiKey(apiKey: string): void {
  memoryApiKey = apiKey;
  if (isBrowser()) {
    window.sessionStorage.setItem(STORAGE_KEY, apiKey);
  }
}

export function getStoredApiKey(): string | null {
  if (isBrowser()) {
    const value = window.sessionStorage.getItem(STORAGE_KEY);
    memoryApiKey = value;
    return value;
  }

  return memoryApiKey;
}

export function clearStoredApiKey(): void {
  memoryApiKey = null;
  if (isBrowser()) {
    window.sessionStorage.removeItem(STORAGE_KEY);
  }
}

export function hasStoredApiKey(): boolean {
  return Boolean(getStoredApiKey());
}
