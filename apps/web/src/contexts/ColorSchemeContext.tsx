/**
 * ColorSchemeContext - Theme Management Provider (Issue #1102)
 *
 * Manages color scheme state across the application:
 * - Loads theme from localStorage on mount
 * - Applies CSS variables to :root element
 * - Persists theme changes to localStorage
 * - Supports 18 preset themes + custom themes
 * - SSR-safe with hydration check
 *
 * Usage:
 * ```tsx
 * import { useColorScheme } from '@/contexts/ColorSchemeContext';
 *
 * function MyComponent() {
 *   const { currentTheme, setTheme, themes } = useColorScheme();
 *   return <button onClick={() => setTheme('ocean-dark')}>Ocean Dark</button>;
 * }
 * ```
 */

'use client';

import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  type ReactNode,
} from 'react';

import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';
import { THEMES, getCurrentTheme, saveTheme, applyTheme, type Theme } from '@/lib/themes';

interface ColorSchemeContextValue {
  /** Current active theme */
  currentTheme: Theme;

  /** All available themes (18 presets + custom) */
  themes: Theme[];

  /** Change the active theme */
  setTheme: (themeId: string) => void;

  /** Add a custom theme to the list */
  addCustomTheme: (theme: Theme) => void;

  /** Remove a custom theme */
  removeCustomTheme: (themeId: string) => void;

  /** Preview a theme without persisting (for preview UI) */
  previewTheme: (themeId: string) => void;

  /** Cancel preview and restore current theme */
  cancelPreview: () => void;

  /** Whether a preview is active */
  isPreviewing: boolean;
}

const ColorSchemeContext = createContext<ColorSchemeContextValue | undefined>(undefined);

interface ColorSchemeProviderProps {
  children: ReactNode;
}

export function ColorSchemeProvider({ children }: ColorSchemeProviderProps) {
  const [currentTheme, setCurrentTheme] = useState<Theme>(() => THEMES[0]);
  const [themes, setThemes] = useState<Theme[]>(THEMES);
  const [mounted, setMounted] = useState(false);
  const [isPreviewing, setIsPreviewing] = useState(false);
  const [previewThemeId, setPreviewThemeId] = useState<string | null>(null);

  // Load theme from localStorage on mount (client-side only)
  useEffect(() => {
    const loadedTheme = getCurrentTheme();
    setCurrentTheme(loadedTheme);
    applyTheme(loadedTheme);
    setMounted(true);
  }, []);

  // Load custom themes from localStorage
  useEffect(() => {
    if (!mounted) return;

    try {
      const customThemesJSON = localStorage.getItem('meepleai-custom-themes');
      if (customThemesJSON) {
        const customThemes: Theme[] = JSON.parse(customThemesJSON);
        setThemes([...THEMES, ...customThemes]);
      }
    } catch (err) {
      logger.error(
        'Failed to load custom themes',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('ColorSchemeProvider', 'loadCustomThemes')
      );
    }
  }, [mounted]);

  // Set theme and persist
  const setTheme = useCallback(
    (themeId: string) => {
      const theme = themes.find(t => t.id === themeId);
      if (!theme) {
        console.warn(`Theme not found: ${themeId}`);
        return;
      }

      setCurrentTheme(theme);
      saveTheme(themeId);
      applyTheme(theme);
      setIsPreviewing(false);
      setPreviewThemeId(null);
    },
    [themes]
  );

  // Preview theme without persisting
  const previewTheme = useCallback(
    (themeId: string) => {
      const theme = themes.find(t => t.id === themeId);
      if (!theme) return;

      setIsPreviewing(true);
      setPreviewThemeId(themeId);
      applyTheme(theme);
    },
    [themes]
  );

  // Cancel preview and restore current theme
  const cancelPreview = useCallback(() => {
    setIsPreviewing(false);
    setPreviewThemeId(null);
    applyTheme(currentTheme);
  }, [currentTheme]);

  // Add custom theme
  const addCustomTheme = useCallback(
    (theme: Theme) => {
      const newThemes = [...themes, theme];
      setThemes(newThemes);

      // Persist custom themes to localStorage
      const customThemes = newThemes.filter(t => t.isCustom);
      try {
        localStorage.setItem('meepleai-custom-themes', JSON.stringify(customThemes));
      } catch (err) {
        logger.error(
          'Failed to save custom theme',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('ColorSchemeProvider', 'addCustomTheme', { themeId: theme.id })
        );
      }
    },
    [themes]
  );

  // Remove custom theme
  const removeCustomTheme = useCallback(
    (themeId: string) => {
      const theme = themes.find(t => t.id === themeId);
      if (!theme || !theme.isCustom) {
        console.warn('Cannot remove non-custom theme');
        return;
      }

      const newThemes = themes.filter(t => t.id !== themeId);
      setThemes(newThemes);

      // Update localStorage
      const customThemes = newThemes.filter(t => t.isCustom);
      try {
        localStorage.setItem('meepleai-custom-themes', JSON.stringify(customThemes));
      } catch (err) {
        logger.error(
          'Failed to update custom themes',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('ColorSchemeProvider', 'removeCustomTheme', { themeId })
        );
      }

      // If removing current theme, switch to default
      if (currentTheme.id === themeId) {
        setTheme(THEMES[0].id);
      }
    },
    [themes, currentTheme, setTheme]
  );

  const value: ColorSchemeContextValue = {
    currentTheme:
      isPreviewing && previewThemeId
        ? themes.find(t => t.id === previewThemeId) || currentTheme
        : currentTheme,
    themes,
    setTheme,
    addCustomTheme,
    removeCustomTheme,
    previewTheme,
    cancelPreview,
    isPreviewing,
  };

  // Prevent flash of unstyled content during SSR
  if (!mounted) {
    return <div style={{ visibility: 'hidden' }}>{children}</div>;
  }

  return <ColorSchemeContext.Provider value={value}>{children}</ColorSchemeContext.Provider>;
}

/**
 * Hook to access color scheme context
 *
 * @throws Error if used outside ColorSchemeProvider
 */
export function useColorScheme(): ColorSchemeContextValue {
  const context = useContext(ColorSchemeContext);
  if (!context) {
    throw new Error('useColorScheme must be used within ColorSchemeProvider');
  }
  return context;
}
