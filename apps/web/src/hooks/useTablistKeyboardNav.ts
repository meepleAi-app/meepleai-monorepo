/**
 * useTablistKeyboardNav — WAI-ARIA APG horizontal tablist keyboard contract.
 *
 * Wave A.6 Tier 1 (Issue #622). Extracted from inline implementations in:
 *   - components/ui/v2/faq/category-tabs.tsx (Issue #588 hotfix, PR #620)
 *   - components/ui/v2/shared-game-detail/tabs.tsx (Wave A.4)
 *
 * Provides a reusable hook so future tablist surfaces don't reinvent the
 * keyboard contract. Closes the governance gap where 5 sites maintain near-
 * identical inline switch handlers.
 *
 * Keyboard contract (WAI-ARIA APG horizontal tablist):
 *   - ArrowLeft  → previous key (wraps first → last)
 *   - ArrowRight → next key (wraps last → first)
 *   - Home       → first key
 *   - End        → last key
 *   - Activation is automatic: focus = onChange same tick. Consumers that
 *     need lazy-mount panel guards must manage that themselves; this hook
 *     assumes statically-rendered panels (FAQ-style) or amortized cost.
 *   - Other keys (ArrowUp/Down, character keys, Enter/Space/Escape/Tab) are
 *     intentional no-ops — preventDefault is NOT called so default browser
 *     behavior (e.g. Tab moves focus out of the tablist) is preserved.
 *
 * Defensive behavior:
 *   - Unknown `currentKey` (not in `orderedKeys`) → no-op, no preventDefault
 *   - Missing ref (key in orderedKeys but no button registered) → onChange
 *     still fires; focus call is a silent no-op (matches existing behavior
 *     in extracted sites where focusTab gracefully handles undefined).
 *
 * @example
 * ```tsx
 * type TabKey = 'all' | 'games' | 'ai';
 * const ORDER: ReadonlyArray<TabKey> = ['all', 'games', 'ai'];
 *
 * const { tabRefs, handleKeyDown } = useTablistKeyboardNav<TabKey>({
 *   orderedKeys: ORDER,
 *   onChange: setActive,
 * });
 *
 * return (
 *   <div role="tablist">
 *     {ORDER.map(key => (
 *       <button
 *         key={key}
 *         ref={node => {
 *           if (node) tabRefs.current.set(key, node);
 *           else tabRefs.current.delete(key);
 *         }}
 *         role="tab"
 *         tabIndex={key === active ? 0 : -1}
 *         onKeyDown={e => handleKeyDown(e, key)}
 *       >
 *         {labels[key]}
 *       </button>
 *     ))}
 *   </div>
 * );
 * ```
 */

'use client';

import { useCallback, useRef, type KeyboardEvent, type MutableRefObject } from 'react';

export interface UseTablistKeyboardNavArgs<T extends string> {
  /**
   * Ordered list of tab keys defining navigation order. ArrowLeft/Right wrap
   * around the bounds of this array; Home/End jump to first/last.
   */
  readonly orderedKeys: ReadonlyArray<T>;
  /**
   * Activation callback fired synchronously with focus movement. Consumers
   * should set state here; the hook assumes automatic activation per
   * WAI-ARIA APG (focus = activation same tick).
   */
  readonly onChange: (key: T) => void;
}

export interface UseTablistKeyboardNavReturn<T extends string> {
  /**
   * Mutable Map ref keyed by tab key. Consumer must populate via React ref
   * callback on each tab button. Hook reads this map to call .focus() on
   * the new active tab after onChange.
   */
  readonly tabRefs: MutableRefObject<Map<T, HTMLButtonElement>>;
  /**
   * Stable keydown handler. Bind to each tab button as
   * `onKeyDown={e => handleKeyDown(e, thisTabKey)}`.
   */
  readonly handleKeyDown: (e: KeyboardEvent<HTMLButtonElement>, currentKey: T) => void;
}

export function useTablistKeyboardNav<T extends string>({
  orderedKeys,
  onChange,
}: UseTablistKeyboardNavArgs<T>): UseTablistKeyboardNavReturn<T> {
  const tabRefs = useRef<Map<T, HTMLButtonElement>>(new Map());

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLButtonElement>, currentKey: T) => {
      const idx = orderedKeys.indexOf(currentKey);
      if (idx === -1) return;

      let nextIdx: number | null = null;
      switch (e.key) {
        case 'ArrowLeft':
          nextIdx = (idx - 1 + orderedKeys.length) % orderedKeys.length;
          break;
        case 'ArrowRight':
          nextIdx = (idx + 1) % orderedKeys.length;
          break;
        case 'Home':
          nextIdx = 0;
          break;
        case 'End':
          nextIdx = orderedKeys.length - 1;
          break;
        default:
          return;
      }

      e.preventDefault();
      const nextKey = orderedKeys[nextIdx];
      onChange(nextKey);
      tabRefs.current.get(nextKey)?.focus();
    },
    [orderedKeys, onChange]
  );

  return { tabRefs, handleKeyDown };
}
