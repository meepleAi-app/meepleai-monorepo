'use client';

/**
 * ThemeToggle Component - Issue #2965 Wave 1
 *
 * Theme toggle button for switching between light and dark modes.
 * Uses next-themes for theme management with icons for visual feedback.
 *
 * Features:
 * - Visual indicators (Sun/Moon icons)
 * - Smooth transitions
 * - Accessible (keyboard + screen reader)
 * - Mobile-friendly
 *
 * Usage:
 * - TopNav user dropdown
 * - Mobile settings menu
 * - Admin dashboard header
 */

import { useEffect, useState } from 'react';

import { Moon, Sun } from 'lucide-react';
import { useTheme } from 'next-themes';

import { Button } from '@/components/ui/primitives/button';

export interface ThemeToggleProps {
  /** Show as icon button (default) or with label */
  showLabel?: boolean;
  /** Size variant */
  size?: 'sm' | 'md' | 'lg';
  /** Custom className */
  className?: string;
}

export function ThemeToggle({ showLabel = false, size = 'md', className }: ThemeToggleProps) {
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);

  // Avoid hydration mismatch - theme is only available on client
  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) {
    // Return placeholder button during SSR to prevent layout shift
    return (
      <Button
        variant="ghost"
        size={size === 'sm' ? 'sm' : size === 'lg' ? 'lg' : 'icon'}
        className={className}
        aria-label="Toggle theme"
        disabled
      >
        <Sun className="h-5 w-5" />
        {showLabel && <span className="ml-2">Tema</span>}
      </Button>
    );
  }

  const isDark = theme === 'dark';

  const toggleTheme = () => {
    setTheme(isDark ? 'light' : 'dark');
  };

  return (
    <Button
      variant="ghost"
      size={size === 'sm' ? 'sm' : size === 'lg' ? 'lg' : 'icon'}
      onClick={toggleTheme}
      className={className}
      aria-label={isDark ? 'Attiva tema chiaro' : 'Attiva tema scuro'}
      title={isDark ? 'Tema Chiaro' : 'Tema Scuro'}
    >
      {isDark ? (
        <Sun className="h-5 w-5 text-amber-400" />
      ) : (
        <Moon className="h-5 w-5 text-slate-700" />
      )}
      {showLabel && (
        <span className="ml-2 text-sm font-medium">
          {isDark ? 'Chiaro' : 'Scuro'}
        </span>
      )}
    </Button>
  );
}
