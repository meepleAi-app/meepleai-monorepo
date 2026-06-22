'use client';

/**
 * SearchPill — runtime backfill of nav-chrome primitive D5 search slot (Issue #2320).
 *
 * Renders the search-pill trigger in the AppTopBar. Clicking dispatches a
 * `meeple:command-palette:open` window event that the CommandPalette host
 * in `providers.tsx` listens for. Decouples the trigger from the modal
 * state without introducing a new React context layer.
 *
 * The keyboard shortcut (Cmd/Ctrl+K) lives in
 * `useGlobalKeyboardShortcuts` and is already wired into the same
 * provider state; this button is the *visual + click* alternative.
 *
 * Design source: `primitive-nav-topbar.html` (D5 search slot — collapsed
 * pill state). Once focused / clicked, the CommandPalette modal takes
 * over from the static pill.
 */

import { useCallback } from 'react';

import { Search } from 'lucide-react';

import { isMac, modKey } from '@/hooks/useKeyboardShortcuts';
import { cn } from '@/lib/utils';

interface SearchPillProps {
  className?: string;
}

export function SearchPill({ className }: SearchPillProps) {
  const handleClick = useCallback(() => {
    window.dispatchEvent(new CustomEvent('meeple:command-palette:open'));
  }, []);

  return (
    <button
      type="button"
      data-testid="topbar-search-pill"
      data-slot="topbar-search-pill"
      onClick={handleClick}
      aria-label={`Apri ricerca globale (${isMac ? '⌘K' : 'Ctrl+K'})`}
      className={cn(
        'group inline-flex shrink-0 items-center gap-2 rounded-full border border-border bg-muted/40 px-3 py-1.5',
        'text-[12px] font-medium text-muted-foreground transition-colors',
        'hover:bg-muted hover:text-foreground',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
        className
      )}
    >
      <Search aria-hidden="true" className="h-3.5 w-3.5 opacity-75" />
      <span className="hidden lg:inline">Cerca…</span>
      <span
        aria-hidden="true"
        className={cn(
          'hidden items-center gap-0.5 rounded border border-border bg-background px-1 py-0.5 font-mono text-[10px] font-semibold opacity-70 lg:inline-flex'
        )}
      >
        {modKey}K
      </span>
    </button>
  );
}
