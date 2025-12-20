'use client';

import { useEffect, useState } from 'react';

import { Moon, Sun, Monitor } from 'lucide-react';
import { useTheme } from 'next-themes';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

/**
 * ThemeSwitcher Component (FRONTEND-4)
 *
 * Provides a dropdown menu to switch between light, dark, and system themes.
 * Uses next-themes for theme management with CSS class strategy.
 *
 * Features:
 * - Three modes: Light, Dark, System
 * - Animated icon transitions
 * - System theme detection
 * - localStorage persistence (automatic via next-themes)
 * - Accessible keyboard navigation (WCAG 2.1 AA)
 * - No FOUC (Flash of Unstyled Content)
 *
 * @example
 * ```tsx
 * import { ThemeSwitcher } from '@/components/layout';
 *
 * export default function Navigation() {
 *   return (
 *     <nav>
 *       <ThemeSwitcher />
 *     </nav>
 *   );
 * }
 * ```
 */
export function ThemeSwitcher() {
  const { theme, setTheme, systemTheme } = useTheme();
  const [mounted, setMounted] = useState(false);

  // Prevent hydration mismatch
  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) {
    return <div className="w-10 h-10 rounded-lg bg-muted animate-pulse" aria-hidden="true" />;
  }

  const currentTheme = theme === 'system' ? systemTheme : theme;
  const isDark = currentTheme === 'dark';

  const getIcon = () => {
    if (theme === 'system') return <Monitor className="h-5 w-5" />;
    if (isDark) return <Moon className="h-5 w-5" />;
    return <Sun className="h-5 w-5" />;
  };

  const getLabel = () => {
    if (theme === 'system') return 'System';
    if (isDark) return 'Dark';
    return 'Light';
  };

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="
            relative inline-flex items-center justify-center gap-2
            w-auto h-10 px-3 rounded-lg
            bg-secondary text-secondary-foreground hover:bg-secondary/80
            border border-border
            transition-all duration-200
            hover:scale-105 active:scale-95
            focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2
          "
          aria-label="Theme switcher"
        >
          <span className="transition-transform duration-300">{getIcon()}</span>
          <span className="text-sm font-medium hidden sm:inline-block">{getLabel()}</span>
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-36">
        <DropdownMenuItem
          onClick={() => setTheme('light')}
          className="cursor-pointer"
          aria-label="Switch to light theme"
        >
          <Sun className="mr-2 h-4 w-4" />
          <span>Light</span>
          {theme === 'light' && (
            <span className="ml-auto text-primary" aria-label="Current theme">
              ✓
            </span>
          )}
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={() => setTheme('dark')}
          className="cursor-pointer"
          aria-label="Switch to dark theme"
        >
          <Moon className="mr-2 h-4 w-4" />
          <span>Dark</span>
          {theme === 'dark' && (
            <span className="ml-auto text-primary" aria-label="Current theme">
              ✓
            </span>
          )}
        </DropdownMenuItem>
        <DropdownMenuItem
          onClick={() => setTheme('system')}
          className="cursor-pointer"
          aria-label="Use system theme preference"
        >
          <Monitor className="mr-2 h-4 w-4" />
          <span>System</span>
          {theme === 'system' && (
            <span className="ml-auto text-primary" aria-label="Current theme">
              ✓
            </span>
          )}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
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
 * import { ThemeProvider } from '@/components/layout';
 *
 * export default function App({ Component, pageProps }) {
 *   return (
 *     <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
 *       <Component {...pageProps} />
 *     </ThemeProvider>
 *   );
 * }
 * ```
 */
export { ThemeProvider } from 'next-themes';
