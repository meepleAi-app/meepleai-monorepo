'use client';
import { useEffect, useState } from 'react';
import type { JSX } from 'react';

const STORAGE_KEY = 'mai-theme';

export function ThemeToggle(): JSX.Element {
  const [isDark, setIsDark] = useState(false);

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    const shouldBeDark = stored === 'dark';
    setIsDark(shouldBeDark);
    document.documentElement.classList.toggle('dark', shouldBeDark);
  }, []);

  const handleToggle = (): void => {
    const next = !isDark;
    setIsDark(next);
    document.documentElement.classList.toggle('dark', next);
    localStorage.setItem(STORAGE_KEY, next ? 'dark' : 'light');
  };

  return (
    <button
      type="button"
      onClick={handleToggle}
      aria-label={isDark ? 'Passa a tema chiaro' : 'Passa a tema scuro'}
      className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-border bg-background text-foreground hover:bg-accent"
    >
      <span aria-hidden="true">{isDark ? '☀️' : '🌙'}</span>
    </button>
  );
}
