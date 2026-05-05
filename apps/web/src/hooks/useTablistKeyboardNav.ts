/**
 * useTablistKeyboardNav — WAI-ARIA APG tablist keyboard contract.
 *
 * Wave A.6 Tier 1 (Issue #622) — initial horizontal-only extraction.
 * Wave A.6 Tier 1.5 (Issue #625) — orientation extension (vertical / both).
 *
 * Extracted from inline implementations in:
 *   - components/ui/v2/faq/category-tabs.tsx (Issue #588 hotfix, PR #620)
 *   - components/ui/v2/shared-game-detail/tabs.tsx (Wave A.4)
 *   - components/session/ToolRail.tsx (Tier 1.5, orientation: 'both')
 *
 * Provides a reusable hook so future tablist surfaces don't reinvent the
 * keyboard contract. Closes the governance gap where multiple sites maintain
 * near-identical inline switch handlers.
 *
 * Keyboard contract (WAI-ARIA APG tablist):
 *   - ArrowLeft  → previous key (wraps first → last) — horizontal | both
 *   - ArrowRight → next key (wraps last → first)     — horizontal | both
 *   - ArrowUp    → previous key (wraps first → last) — vertical   | both
 *   - ArrowDown  → next key (wraps last → first)     — vertical   | both
 *   - Home       → first key (all orientations)
 *   - End        → last key  (all orientations)
 *   - Activation is automatic: focus = onChange same tick. Consumers that
 *     need lazy-mount panel guards must manage that themselves; this hook
 *     assumes statically-rendered panels (FAQ-style) or amortized cost.
 *   - Off-axis arrow keys (e.g. ArrowUp in horizontal mode) and other keys
 *     (character keys, Enter/Space/Escape/Tab) are intentional no-ops —
 *     preventDefault is NOT called so default browser behavior is preserved.
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

/**
 * Tablist orientation following the WAI-ARIA APG model.
 *
 * - `'horizontal'` (default): ArrowLeft/Right navigate; ArrowUp/Down ignored.
 *   Mirrors `aria-orientation="horizontal"` (the ARIA default for tablists).
 * - `'vertical'`: ArrowUp/Down navigate; ArrowLeft/Right ignored. Pair with
 *   `aria-orientation="vertical"` on the tablist container.
 * - `'both'`: all four arrow keys map to the same linear order (Up≡Left,
 *   Down≡Right). Useful when a tablist visually wraps to two axes (e.g.
 *   rail/list components) and consumers want either axis to advance.
 */
export type TablistOrientation = 'horizontal' | 'vertical' | 'both';

export interface UseTablistKeyboardNavArgs<T extends string> {
  /**
   * Ordered list of tab keys defining navigation order. Arrow keys wrap
   * around the bounds of this array; Home/End jump to first/last.
   */
  readonly orderedKeys: ReadonlyArray<T>;
  /**
   * Activation callback fired synchronously with focus movement. Consumers
   * should set state here; the hook assumes automatic activation per
   * WAI-ARIA APG (focus = activation same tick).
   */
  readonly onChange: (key: T) => void;
  /**
   * Which arrow keys participate in navigation. Defaults to `'horizontal'`
   * to preserve the original Wave A.6 Tier 1 contract for existing consumers.
   */
  readonly orientation?: TablistOrientation;
}

export interface UseTablistKeyboardNavReturn<T extends string> {
  /**
   * Mutable Map ref keyed by tab key. Consumer must populate via React ref
   * callback on each tab button. Hook reads this map to call .focus() on
   * the new active tab after onChange.
   */
  readonly tabRefs: MutableRefObject<Map<T, HTMLButtonElement>>;
  /**
   * Stable keydown handler. Bind per-button as
   * `onKeyDown={e => handleKeyDown(e, thisTabKey)}` (preferred), or on the
   * tablist container as `onKeyDown={e => handleKeyDown(e, activeKey)}` for
   * surfaces that prefer container-level event delegation. The event type is
   * widened to `HTMLElement` so both patterns satisfy TypeScript.
   */
  readonly handleKeyDown: (e: KeyboardEvent<HTMLElement>, currentKey: T) => void;
}

export function useTablistKeyboardNav<T extends string>({
  orderedKeys,
  onChange,
  orientation = 'horizontal',
}: UseTablistKeyboardNavArgs<T>): UseTablistKeyboardNavReturn<T> {
  const tabRefs = useRef<Map<T, HTMLButtonElement>>(new Map());

  const handleKeyDown = useCallback(
    (e: KeyboardEvent<HTMLElement>, currentKey: T) => {
      const idx = orderedKeys.indexOf(currentKey);
      if (idx === -1) return;

      const horizontalActive = orientation === 'horizontal' || orientation === 'both';
      const verticalActive = orientation === 'vertical' || orientation === 'both';

      let nextIdx: number | null = null;
      switch (e.key) {
        case 'ArrowLeft':
          if (!horizontalActive) return;
          nextIdx = (idx - 1 + orderedKeys.length) % orderedKeys.length;
          break;
        case 'ArrowRight':
          if (!horizontalActive) return;
          nextIdx = (idx + 1) % orderedKeys.length;
          break;
        case 'ArrowUp':
          if (!verticalActive) return;
          nextIdx = (idx - 1 + orderedKeys.length) % orderedKeys.length;
          break;
        case 'ArrowDown':
          if (!verticalActive) return;
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
    [orderedKeys, onChange, orientation]
  );

  return { tabRefs, handleKeyDown };
}
