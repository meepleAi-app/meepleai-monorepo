/**
 * Test Utilities for Theme Testing (Issue #2965 Wave 9)
 *
 * Provides helper functions to render components with ThemeProvider
 * for testing theme-dependent components in both light and dark modes.
 */

import { ReactElement } from 'react';

import { render, RenderOptions } from '@testing-library/react';
import { ThemeProvider } from 'next-themes';

/**
 * Custom render function that wraps components with ThemeProvider
 *
 * @param ui - Component to render
 * @param theme - Theme to use ('light' or 'dark')
 * @param options - Additional render options
 */
export function renderWithTheme(
  ui: ReactElement,
  theme: 'light' | 'dark' = 'light',
  options?: Omit<RenderOptions, 'wrapper'>
) {
  return render(ui, {
    wrapper: ({ children }) => (
      <ThemeProvider attribute="class" defaultTheme={theme} forcedTheme={theme}>
        {children}
      </ThemeProvider>
    ),
    ...options,
  });
}

/**
 * Custom render function that wraps with ThemeProvider for both themes
 * Returns both light and dark renders for comparison testing
 */
export function renderBothThemes(ui: ReactElement) {
  const lightRender = renderWithTheme(ui, 'light');
  const darkRender = renderWithTheme(ui, 'dark');

  return {
    light: lightRender,
    dark: darkRender,
  };
}

/**
 * Test helper to verify component renders in both themes without errors
 */
export function testBothThemes(componentName: string, ui: ReactElement) {
  describe(`${componentName} - Theme Support`, () => {
    it('renders in light mode', () => {
      const { container } = renderWithTheme(ui, 'light');
      expect(container.firstChild).toBeTruthy();
    });

    it('renders in dark mode', () => {
      const { container } = renderWithTheme(ui, 'dark');
      expect(container.firstChild).toBeTruthy();
    });
  });
}

/**
 * Mock next-themes for tests that don't need actual theme switching
 * Use this in vi.mock() calls
 */
export const mockNextThemes = () => ({
  useTheme: vi.fn(() => ({
    theme: 'light',
    setTheme: vi.fn(),
    systemTheme: 'light',
  })),
  ThemeProvider: ({ children }: { children: React.ReactNode }) => children,
});
