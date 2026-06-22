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
      // DS-1 (2026-05-12 token canonicalization):
      // - attribute={['class', 'data-theme']} applies BOTH `class="dark"` and
      //   `data-theme="dark"` on <html>. This keeps backwards compatibility
      //   with the ~80 legacy `.dark .foo` selectors in globals.css /
      //   design-tokens.css while enabling the canonical mockup convention
      //   (admin-mockups/design_files/tokens.css uses [data-theme="light|dark"]).
      //   Bridge removed in DS-12 once all legacy `.dark` selectors migrate.
      // - defaultTheme="light" — mockup default warm cream (#f7f3ee).
      //   Previously dark-first via legacy gaming palette; now the canonical
      //   light theme is authoritative.
      attribute={['class', 'data-theme']}
      defaultTheme="light"
      enableSystem
      disableTransitionOnChange={false}
      {...props}
    >
      {children}
    </NextThemesProvider>
  );
}
