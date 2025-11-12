'use client';

import { useTheme } from 'next-themes';
import { useEffect, useState } from 'react';

/**
 * ThemeSwitcher Component
 *
 * Provides a toggle button to switch between light and dark themes.
 * Uses next-themes for theme management with CSS class strategy.
 *
 * Features:
 * - Animated icon transitions
 * - System theme detection
 * - Accessible keyboard navigation
 * - WCAG 2.1 AA compliant contrast
 */
export function ThemeSwitcher() {
  const { theme, setTheme, systemTheme } = useTheme();
  const [mounted, setMounted] = useState(false);

  // Prevent hydration mismatch
  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) {
    return (
      <div className="w-10 h-10 rounded-lg bg-muted animate-pulse" aria-hidden="true" />
    );
  }

  const currentTheme = theme === 'system' ? systemTheme : theme;
  const isDark = currentTheme === 'dark';

  const toggleTheme = () => {
    setTheme(isDark ? 'light' : 'dark');
  };

  return (
    <button
      type="button"
      onClick={toggleTheme}
      className="
        relative w-10 h-10 rounded-lg
        bg-secondary hover:bg-secondary/80
        border border-border
        transition-all duration-200
        hover:scale-105 active:scale-95
        focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2
        group
      "
      aria-label={`Switch to ${isDark ? 'light' : 'dark'} theme`}
      aria-pressed={isDark}
    >
      {/* Sun icon (light mode) */}
      <svg
        className={`
          absolute inset-0 m-auto w-5 h-5 text-foreground
          transition-all duration-300
          ${isDark ? 'opacity-0 rotate-90 scale-0' : 'opacity-100 rotate-0 scale-100'}
        `}
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
        strokeWidth={2}
        aria-hidden="true"
      >
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41" />
      </svg>

      {/* Moon icon (dark mode) */}
      <svg
        className={`
          absolute inset-0 m-auto w-5 h-5 text-foreground
          transition-all duration-300
          ${isDark ? 'opacity-100 rotate-0 scale-100' : 'opacity-0 -rotate-90 scale-0'}
        `}
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
        strokeWidth={2}
        aria-hidden="true"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"
        />
      </svg>

      {/* Tooltip */}
      <span className="
        absolute -bottom-10 left-1/2 -translate-x-1/2
        px-2 py-1 rounded text-xs font-medium
        bg-popover text-popover-foreground
        border border-border
        opacity-0 group-hover:opacity-100 group-focus-visible:opacity-100
        transition-opacity duration-200
        pointer-events-none whitespace-nowrap
        z-50
      ">
        {isDark ? 'Light mode' : 'Dark mode'}
      </span>
    </button>
  );
}

/**
 * ThemeProvider Component
 *
 * Wrapper component to provide theme context to the app.
 * Should be placed at the root level (_app.tsx or layout.tsx).
 *
 * @example
 * ```tsx
 * import { ThemeProvider } from '@/components/ThemeSwitcher';
 *
 * export default function App({ Component, pageProps }) {
 *   return (
 *     <ThemeProvider>
 *       <Component {...pageProps} />
 *     </ThemeProvider>
 *   );
 * }
 * ```
 */
export { ThemeProvider } from 'next-themes';
