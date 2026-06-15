/**
 * ChunkSearchBox — debounced search input for the KB chunks list (#2311 FE-3).
 *
 * The component owns the input state + a single debounced timer so consumers
 * receive a stable, already-debounced value via `onCommit`. The 250 ms
 * default lines up with the SEARCH_STALE_MS in `useSearchKbChunks` to avoid
 * thrashing the TanStack Query cache. Empty / whitespace input commits as ''
 * which the search hook interprets as "disabled".
 */

'use client';

import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type ChangeEvent,
  type ReactElement,
} from 'react';

import clsx from 'clsx';

export interface ChunkSearchBoxProps {
  readonly onCommit: (query: string) => void;
  readonly placeholder?: string;
  readonly debounceMs?: number;
  readonly className?: string;
  readonly initialValue?: string;
}

export function ChunkSearchBox({
  onCommit,
  placeholder = 'Cerca nei chunk…',
  debounceMs = 250,
  className,
  initialValue = '',
}: ChunkSearchBoxProps): ReactElement {
  const [value, setValue] = useState(initialValue);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const onCommitRef = useRef(onCommit);
  // Keep the latest callback without re-arming the timer when the parent rerenders.
  useEffect(() => {
    onCommitRef.current = onCommit;
  }, [onCommit]);

  const handleChange = useCallback(
    (e: ChangeEvent<HTMLInputElement>) => {
      const next = e.target.value;
      setValue(next);
      if (timerRef.current) clearTimeout(timerRef.current);
      timerRef.current = setTimeout(() => {
        onCommitRef.current(next.trim());
      }, debounceMs);
    },
    [debounceMs]
  );

  // Cleanup pending timer on unmount.
  useEffect(() => {
    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
    };
  }, []);

  return (
    <div data-slot="kb-chunk-search-box" className={clsx('relative', className)}>
      <label className="sr-only" htmlFor="kb-chunk-search-input">
        Cerca nei chunk
      </label>
      <input
        id="kb-chunk-search-input"
        type="search"
        value={value}
        onChange={handleChange}
        placeholder={placeholder}
        className={clsx(
          'w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground',
          'placeholder:text-muted-foreground',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
        )}
      />
    </div>
  );
}
