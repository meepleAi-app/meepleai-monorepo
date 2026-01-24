'use client';

/**
 * ThemeProvider Component - Issue #2965 Wave 1
 *
 * Wrapper for next-themes to enable dark mode throughout the application.
 * Provides theme switching functionality with persistence in localStorage.
 *
 * Features:
 * - Theme persistence (localStorage)
 * - System preference detection
 * - Smooth theme transitions
 * - SSR-safe hydration
 */

import { ThemeProvider as NextThemesProvider } from 'next-themes';
import { type ThemeProviderProps as NextThemesProviderProps } from 'next-themes';

export type ThemeProviderProps = NextThemesProviderProps;

export function ThemeProvider({ children, ...props }: ThemeProviderProps) {
  return (
    <NextThemesProvider
      attribute="class"
      defaultTheme="light"
      enableSystem
      disableTransitionOnChange={false}
      {...props}
    >
      {children}
    </NextThemesProvider>
  );
}
