/**
 * HeroSearch.tsx
 * Issue #1482 Task 2 — Hero search component for KB Globale
 *
 * A large, prominent search input with SearchMode segmented control.
 * Features:
 * - Controlled input with character limit enforcement (min 2 chars for submit)
 * - Segmented control for SearchMode selection (Semantic in v1)
 * - Submit via Enter key or click button (only when query >= 2 chars)
 * - Clear button (×) surfaces only when query non-empty
 * - Labels-injected (no hardcoded strings — consumer wires i18n)
 * - DS-15 semantic tokens
 * - jest-axe compliant
 *
 * @see Issue #1482 Phase 1 Foundation
 * @see admin-mockups/design_files/sp4-kb-globale.jsx (HeroSearch pattern, lines 175–212)
 */

import { type JSX, useState } from 'react';

import type { SearchMode } from '@/lib/api/schemas/kb-globale.schemas';

export interface HeroSearchLabels {
  /** Placeholder text for the search input */
  placeholder: string;
  /** Label for the submit button */
  submit: string;
  /** Label for the clear button */
  clear: string;
  /** Aria-label for the mode segmented control group */
  modeLabel: string;
  /** Labels for each SearchMode option */
  modeOptions: Record<SearchMode, string>;
}

export interface HeroSearchProps {
  /** Initial search query (defaults to '') */
  initialQuery?: string;
  /** Initial search mode (defaults to 'Semantic') */
  initialMode?: SearchMode;
  /** Called when user submits a query >= 2 chars via Enter or button click */
  onSubmit: (query: string, mode: SearchMode) => void;
  /** Called when user clears the input via the clear button */
  onClear?: () => void;
  /** All UI labels (injected by consumer — no hardcoded i18n strings) */
  labels: HeroSearchLabels;
  /** Optional wrapper class name */
  className?: string;
}

/**
 * HeroSearch component — large, prominent search input with mode toggle.
 *
 * Local state: query (string), mode (SearchMode).
 * Submit only fires when `query.trim().length >= 2`.
 * Clear button surfaces only when `query.length > 0`.
 * Aria-pressed on mode buttons reflects current selection.
 *
 * @example
 * ```tsx
 * <HeroSearch
 *   initialQuery="azul"
 *   initialMode="Semantic"
 *   onSubmit={(q, m) => router.push(`/kb?q=${q}&mode=${m}`)}
 *   onClear={() => router.push('/kb')}
 *   labels={labels}
 * />
 * ```
 */
export function HeroSearch(props: HeroSearchProps): JSX.Element {
  const {
    initialQuery = '',
    initialMode = 'Semantic',
    onSubmit,
    onClear,
    labels,
    className,
  } = props;

  const [query, setQuery] = useState<string>(initialQuery);
  const [mode, setMode] = useState<SearchMode>(initialMode);

  const trimmedQuery = query.trim();
  const canSubmit = trimmedQuery.length >= 2;
  const showClear = query.length > 0;

  const handleSubmit = () => {
    if (canSubmit) {
      onSubmit(trimmedQuery, mode);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      handleSubmit();
    }
  };

  const handleClear = () => {
    setQuery('');
    onClear?.();
  };

  // For v1, only Semantic mode is available; structure allows easy expansion
  const modeEntries = Object.entries(labels.modeOptions) as [SearchMode, string][];

  return (
    <div className={className}>
      {/* Hero search input container */}
      <div
        className="relative bg-card border-2 rounded-2xl transition-all duration-150"
        style={{
          borderColor: 'var(--border)',
          boxShadow: 'var(--shadow-md)',
        }}
      >
        {/* Search icon (left) */}
        <div
          className="absolute left-4 top-1/2 flex items-center justify-center -translate-y-1/2 w-7 h-7 rounded-sm bg-entity-kb/10 text-entity-kb text-sm font-bold"
          aria-hidden="true"
        >
          🔎
        </div>

        {/* Input */}
        <input
          type="text"
          value={query}
          onChange={e => setQuery(e.currentTarget.value)}
          onKeyDown={handleKeyDown}
          placeholder={labels.placeholder}
          className="w-full border-0 outline-none bg-transparent text-foreground pl-14 pr-16 py-3 font-body text-base font-medium placeholder-muted-foreground"
          aria-label={labels.placeholder}
        />

        {/* Clear button (right, only when query non-empty) */}
        {showClear && (
          <button
            onClick={handleClear}
            type="button"
            className="absolute right-4 top-1/2 -translate-y-1/2 h-7 w-7 flex items-center justify-center rounded-sm text-muted-foreground hover:bg-muted hover:text-foreground transition-colors"
            aria-label={labels.clear}
            title={labels.clear}
          >
            ×
          </button>
        )}
      </div>

      {/* Search mode segmented control */}
      <div className="mt-4 flex items-center gap-4">
        <label
          className="text-xs font-bold uppercase tracking-wider text-muted-foreground whitespace-nowrap"
          htmlFor="search-mode-group"
        >
          {labels.modeLabel}
        </label>
        <div
          id="search-mode-group"
          role="group"
          aria-label={labels.modeLabel}
          className="flex gap-2"
        >
          {modeEntries.map(([modeValue, modeLabel]) => (
            <button
              key={modeValue}
              type="button"
              onClick={() => setMode(modeValue)}
              aria-pressed={mode === modeValue}
              className={`px-4 py-2 rounded-md text-sm font-semibold transition-colors ${
                mode === modeValue
                  ? 'bg-entity-kb text-white'
                  : 'bg-entity-kb/10 text-entity-kb border border-entity-kb/20 hover:bg-entity-kb/20'
              }`}
            >
              {modeLabel}
            </button>
          ))}
        </div>
      </div>

      {/* Submit button */}
      <div className="mt-4 flex justify-end">
        <button
          onClick={handleSubmit}
          disabled={!canSubmit}
          type="button"
          className={`px-6 py-2.5 rounded-md font-bold transition-colors ${
            canSubmit
              ? 'bg-entity-kb text-white hover:bg-entity-kb/90 cursor-pointer'
              : 'bg-muted text-muted-foreground opacity-50 cursor-not-allowed'
          }`}
          aria-label={labels.submit}
        >
          {labels.submit}
        </button>
      </div>
    </div>
  );
}
