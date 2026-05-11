/**
 * FAQSearchBar — large pill input with clear button.
 *
 * Wave A.1 Phase 4.5 (Issue #583). Mirrors mockup
 * admin-mockups/design_files/sp3-faq-enhanced.jsx lines 198-266.
 */

'use client';

import { type JSX, useRef } from 'react';

import clsx from 'clsx';
import { Search, X } from 'lucide-react';

import { entityHsl } from '@/lib/color-utils';

export interface FAQSearchBarProps {
  readonly value: string;
  readonly onChange: (next: string) => void;
  readonly placeholder?: string;
  readonly ariaLabel?: string;
  readonly clearLabel?: string;
  readonly disabled?: boolean;
  readonly className?: string;
}

export function FAQSearchBar({
  value,
  onChange,
  placeholder = 'Search FAQs...',
  ariaLabel = 'Search FAQs',
  clearLabel = 'Clear search',
  disabled = false,
  className,
}: FAQSearchBarProps): JSX.Element {
  const inputRef = useRef<HTMLInputElement>(null);
  const focusRingColor = entityHsl('game', 0.4);

  return (
    <div
      data-slot="faq-search-bar"
      className={clsx(
        'relative flex items-center bg-card border-[1.5px] border-border rounded-full',
        'shadow-sm transition-[border-color,box-shadow] duration-150',
        'focus-within:border-[hsl(var(--c-game)/0.55)]',
        className
      )}
      style={{
        // CSS custom property used in focus-within to bind ring color.
        // Tailwind arbitrary won't work for var-only refs.
        ['--faq-search-focus' as string]: focusRingColor,
      }}
    >
      <span
        aria-hidden="true"
        className="pointer-events-none absolute left-[18px] flex h-4 w-4 items-center justify-center text-[hsl(var(--text-muted))]"
      >
        <Search size={16} strokeWidth={2} />
      </span>
      <input
        ref={inputRef}
        type="search"
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        aria-label={ariaLabel}
        disabled={disabled}
        data-dynamic="search-input"
        className={clsx(
          'flex-1 min-w-0 bg-transparent border-none outline-none',
          'py-[14px] pl-[46px] pr-[18px] rounded-full',
          'text-[15px] text-foreground font-body',
          'placeholder:text-[hsl(var(--text-muted))]',
          'disabled:cursor-not-allowed disabled:opacity-60',
          'focus-visible:shadow-[0_0_0_4px_hsl(var(--c-game)/0.12)]'
        )}
      />
      {value.length > 0 && !disabled && (
        <button
          type="button"
          onClick={() => {
            onChange('');
            inputRef.current?.focus();
          }}
          aria-label={clearLabel}
          className={clsx(
            'mr-2 inline-flex h-[30px] w-[30px] flex-shrink-0 items-center justify-center',
            'rounded-full bg-[hsl(var(--bg-muted))] text-[hsl(var(--text-sec))]',
            'border-0 cursor-pointer text-[13px]',
            'hover:bg-[hsl(var(--bg-hover))] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))]'
          )}
        >
          <X size={14} strokeWidth={2.5} aria-hidden="true" />
        </button>
      )}
    </div>
  );
}
