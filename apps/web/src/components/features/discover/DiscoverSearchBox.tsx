'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import { Search } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface DiscoverSearchBoxProps {
  /** Current committed value (URL-bound). Empty string for no filter. */
  readonly value: string;
  /** Called when user commits (Enter or 300ms idle). */
  readonly onCommit: (value: string) => void;
  /** Localised placeholder. */
  readonly placeholder: string;
  /** When true, input is read-only + focus emits telemetry event. */
  readonly disabled?: boolean;
  /** Tooltip text when disabled. */
  readonly disabledTooltip?: string;
  /** Called when disabled input receives focus (telemetry hook). */
  readonly onDisabledFocus?: () => void;
}

const DEBOUNCE_MS = 300;

/**
 * DiscoverSearchBox — controlled search input with 300ms debounce.
 *
 * Commits to parent on Enter or after 300ms idle. Supports `disabled` shell
 * variant: when backend search endpoint is unavailable, input is read-only,
 * shows a tooltip, and emits a telemetry event on focus.
 */
export function DiscoverSearchBox({
  value,
  onCommit,
  placeholder,
  disabled = false,
  disabledTooltip,
  onDisabledFocus,
}: DiscoverSearchBoxProps) {
  const [draft, setDraft] = useState(value);
  const idleTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Sync from URL when external value changes (e.g. back/forward navigation)
  useEffect(() => {
    setDraft(value);
  }, [value]);

  // Cleanup pending debounce on unmount
  useEffect(
    () => () => {
      if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
    },
    []
  );

  const scheduleCommit = useCallback(
    (next: string) => {
      if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
      idleTimerRef.current = setTimeout(() => {
        onCommit(next);
      }, DEBOUNCE_MS);
    },
    [onCommit]
  );

  const handleChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const next = event.target.value;
      setDraft(next);
      if (!disabled) scheduleCommit(next);
    },
    [disabled, scheduleCommit]
  );

  const handleKeyDown = useCallback(
    (event: React.KeyboardEvent<HTMLInputElement>) => {
      if (event.key === 'Enter') {
        event.preventDefault();
        if (idleTimerRef.current) clearTimeout(idleTimerRef.current);
        if (!disabled) onCommit(draft);
      }
    },
    [disabled, draft, onCommit]
  );

  const handleFocus = useCallback(() => {
    if (disabled) onDisabledFocus?.();
  }, [disabled, onDisabledFocus]);

  // #3848 — da spento il campo mostrava comunque il placeholder che invita a cercare, e l'unico
  // segnale della sua indisponibilita' era `aria-disabled` (che raggiunge solo chi usa uno screen
  // reader) e un `title` (che richiede di passarci sopra e aspettare). Chi guarda lo schermo ci
  // clicca, non succede nulla e nessun messaggio spiega perche'.
  //
  // Il testo che promette lascia il posto a quello che dichiara. Lo stato spento resta voluto —
  // la ricerca lato server non esiste ancora (#728) — ma smette di essere invisibile.
  const testoVisibile = disabled && disabledTooltip ? disabledTooltip : placeholder;

  return (
    <div className="relative">
      <Search
        size={16}
        className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground"
        aria-hidden="true"
      />
      <input
        type="search"
        data-slot="discover-search-box"
        value={draft}
        onChange={handleChange}
        onKeyDown={handleKeyDown}
        onFocus={handleFocus}
        placeholder={testoVisibile}
        aria-label={testoVisibile}
        aria-disabled={disabled || undefined}
        title={disabled ? disabledTooltip : undefined}
        readOnly={disabled}
        className={cn(
          'h-10 w-full rounded-2xl border border-border bg-card pl-10 pr-4 text-sm',
          'outline-none transition-shadow focus-visible:ring-2 focus-visible:ring-foreground/20',
          'placeholder:text-muted-foreground',
          disabled && 'cursor-not-allowed bg-muted text-muted-foreground'
        )}
      />
    </div>
  );
}
