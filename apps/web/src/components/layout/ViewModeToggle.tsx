/**
 * ViewModeToggle — admin↔user view switcher for the top navbar.
 *
 * Icon pill switch (44×26) that flips between admin and user shells.
 * Only rendered when the current user has an admin role — the parent
 * component is responsible for gating visibility.
 *
 * Accessibility: role="switch" with aria-checked, keyboard-operable.
 */
'use client';

import { useCallback, type KeyboardEvent } from 'react';

import { useViewMode } from '@/hooks/useViewMode';
import { cn } from '@/lib/utils';

export function ViewModeToggle() {
  const { viewMode, toggle } = useViewMode();
  const isAdmin = viewMode === 'admin';

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLButtonElement>) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        toggle();
      }
    },
    [toggle]
  );

  return (
    <button
      type="button"
      role="switch"
      aria-checked={isAdmin}
      aria-label="Cambia vista tra admin e utente"
      onClick={toggle}
      onKeyDown={handleKeyDown}
      data-testid="view-mode-toggle"
      className={cn(
        'relative inline-flex h-[26px] w-[44px] shrink-0 items-center rounded-full',
        'bg-gradient-to-br from-purple-500 to-orange-500',
        'border-2 border-white shadow-sm',
        'cursor-pointer transition-colors',
        'focus:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2'
      )}
    >
      {/* Left indicator: user icon */}
      <span
        className={cn(
          'absolute left-1 text-[9px] transition-opacity',
          isAdmin ? 'opacity-70' : 'opacity-30'
        )}
        aria-hidden="true"
      >
        👤
      </span>
      {/* Right indicator: admin gear */}
      <span
        className={cn(
          'absolute right-1 text-[9px] transition-opacity',
          isAdmin ? 'opacity-30' : 'opacity-70'
        )}
        aria-hidden="true"
      >
        ⚙️
      </span>
      {/* Knob */}
      <span
        className={cn(
          'pointer-events-none absolute top-[1px] h-5 w-5 rounded-full bg-white shadow-sm',
          'flex items-center justify-center text-[11px]',
          'transition-transform duration-200',
          isAdmin ? 'translate-x-[18px]' : 'translate-x-[1px]'
        )}
        aria-hidden="true"
      >
        {isAdmin ? '⚙️' : '👤'}
      </span>
    </button>
  );
}
