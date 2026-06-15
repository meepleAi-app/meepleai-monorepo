/**
 * AnimatedUnderlineTabs — primitive extracted from features/game-detail
 * (`GameDetailTabsAnimated`) per #2311 DEC-D5. Hosts the shared tab visuals
 * + a11y wiring + reduced-motion underline animation, so the KB detail page
 * sub-tabs (Markdown / Raw), Reviews tab, Strategies tab, and game-detail
 * tabs share one source of truth instead of duplicating the pattern.
 *
 * A11y contract (mirrors WAI-ARIA APG tablist):
 *   - `role="tablist"` on container with `aria-label`
 *   - `role="tab"` + `aria-selected` + `aria-controls={panelId}` + `id={tabId}`
 *   - Keyboard nav via `useTablistKeyboardNav` (Arrow keys, Home, End)
 *
 * Reduced motion: underline transition collapses to instant under
 * `prefers-reduced-motion: reduce` (Tailwind `motion-safe:` modifier).
 *
 * Design decision: ID + data-slot prefixes are parameterised via
 * `slotPrefix` so consumers retain stable selectors (e.g. existing
 * `game-detail-tab-{key}` IDs in tests) while the primitive does not
 * assume any feature namespace.
 *
 * AC: T A M V
 */

'use client';

import {
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
  type ReactElement,
  type ReactNode,
} from 'react';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';

export interface AnimatedUnderlineTabConfig<TKey extends string = string> {
  readonly key: TKey;
  readonly label: string;
  readonly icon?: ReactNode;
  readonly count?: number;
  readonly locked?: boolean;
}

export interface AnimatedUnderlineTabsProps<TKey extends string = string> {
  readonly tabs: ReadonlyArray<AnimatedUnderlineTabConfig<TKey>>;
  readonly active: TKey;
  readonly onChange: (key: TKey) => void;
  readonly ariaLabel: string;
  readonly className?: string;
  /**
   * Prefix for the HTML `id` and `data-slot` attributes. Allows consumers
   * to keep stable selectors (e.g. `game-detail-tab-info`, `kb-preview-tab-raw`)
   * without colliding across surfaces. Defaults to `'tab'`.
   */
  readonly slotPrefix?: string;
  /**
   * Tailwind classes applied to the active tab label + underline indicator.
   * Defaults to the amber accent used by game-detail. KB consumer overrides
   * with `text-entity-kb` + `bg-entity-kb` per DEC-D5 token canonicalization.
   */
  readonly accentActiveClassName?: string;
  readonly accentIndicatorClassName?: string;
  readonly accentCountActiveClassName?: string;
}

/**
 * Derives the panel prefix from a tab slot prefix.
 *   - `'game-detail-tab'` → `'game-detail-panel'` (suffix swap)
 *   - `'tab'` → `'tab-panel'` (append fallback when no `-tab` suffix)
 *   - `'kb-preview-tab'` → `'kb-preview-panel'`
 * Internal helper exported for parity with `panelIdForPrefix`.
 */
function derivePanelPrefix(prefix: string): string {
  return prefix.endsWith('-tab')
    ? prefix.replace(/-tab$/, '-panel')
    : prefix === 'tab'
      ? 'tab-panel'
      : `${prefix}-panel`;
}

/** Returns the HTML `id` for a tab button given a slot prefix + key. */
export function tabIdForPrefix(prefix: string, key: string): string {
  return `${prefix}-${key}`;
}

/** Returns the HTML `id` for a tab panel given a slot prefix + key. */
export function panelIdForPrefix(prefix: string, key: string): string {
  return `${derivePanelPrefix(prefix)}-${key}`;
}

// SSR-safe useLayoutEffect (mirror common React pattern).
const useIsomorphicLayoutEffect = typeof window !== 'undefined' ? useLayoutEffect : useEffect;

export function AnimatedUnderlineTabs<TKey extends string = string>(
  props: AnimatedUnderlineTabsProps<TKey>
): ReactElement {
  const {
    tabs,
    active,
    onChange,
    ariaLabel,
    className,
    slotPrefix = 'tab',
    accentActiveClassName = 'font-extrabold text-amber-700 dark:text-amber-400',
    accentIndicatorClassName = 'bg-amber-700 dark:bg-amber-500',
    accentCountActiveClassName = 'bg-amber-700 text-white dark:bg-amber-500',
  } = props;

  const containerRef = useRef<HTMLDivElement | null>(null);
  const [indicator, setIndicator] = useState<{ left: number; width: number }>({
    left: 0,
    width: 0,
  });

  const orderedKeys = tabs.filter(t => !t.locked).map(t => t.key);
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<TKey>({
    orderedKeys,
    onChange,
  });

  useIsomorphicLayoutEffect(() => {
    const button = tabRefs.current.get(active);
    if (!button || !containerRef.current) {
      setIndicator({ left: 0, width: 0 });
      return;
    }
    const containerRect = containerRef.current.getBoundingClientRect();
    const buttonRect = button.getBoundingClientRect();
    setIndicator({
      left: buttonRect.left - containerRect.left + containerRef.current.scrollLeft,
      width: buttonRect.width,
    });
  }, [active, tabs.length, tabRefs]);

  const panelPrefix = derivePanelPrefix(slotPrefix);

  return (
    <div
      ref={containerRef}
      role="tablist"
      aria-label={ariaLabel}
      data-slot={`${slotPrefix}s`}
      className={clsx(
        'relative flex items-center gap-0.5 overflow-x-auto border-b border-border bg-background px-4 sm:px-8',
        '[scrollbar-width:none] [&::-webkit-scrollbar]:hidden',
        className
      )}
    >
      {tabs.map(tab => {
        const isActive = tab.key === active;
        const isLocked = tab.locked === true;
        return (
          <button
            key={tab.key}
            type="button"
            ref={node => {
              if (node) tabRefs.current.set(tab.key, node);
              else tabRefs.current.delete(tab.key);
            }}
            role="tab"
            aria-selected={isActive}
            aria-controls={`${panelPrefix}-${tab.key}`}
            id={`${slotPrefix}-${tab.key}`}
            tabIndex={isActive ? 0 : -1}
            disabled={isLocked}
            data-slot={slotPrefix}
            data-active={isActive}
            data-locked={isLocked}
            data-tab-key={tab.key}
            onClick={() => !isLocked && onChange(tab.key)}
            onKeyDown={e => handleKeyDown(e, tab.key)}
            className={clsx(
              'inline-flex flex-shrink-0 items-center gap-1.5 whitespace-nowrap border-none bg-transparent px-3.5 py-3 font-display text-[13px] transition-colors motion-reduce:transition-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              isLocked && 'cursor-not-allowed opacity-55 text-muted-foreground',
              !isLocked &&
                (isActive
                  ? accentActiveClassName
                  : 'cursor-pointer font-bold text-muted-foreground hover:text-foreground')
            )}
          >
            {tab.icon ? (
              <span aria-hidden="true" className="text-base">
                {tab.icon}
              </span>
            ) : null}
            <span>{tab.label}</span>
            {tab.count !== undefined ? (
              <span
                className={clsx(
                  'rounded-full px-1.5 py-0.5 font-mono text-[9px] font-extrabold tabular-nums',
                  isActive ? accentCountActiveClassName : 'bg-muted text-muted-foreground'
                )}
              >
                {tab.count}
              </span>
            ) : null}
            {isLocked ? (
              <span aria-hidden="true" className="text-[10px]">
                🔒
              </span>
            ) : null}
          </button>
        );
      })}
      <span
        aria-hidden="true"
        data-slot={`${slotPrefix}-indicator`}
        className={clsx(
          'absolute bottom-[-1px] h-0.5 rounded-t-[2px] motion-safe:transition-all motion-safe:duration-300 motion-safe:ease-[cubic-bezier(0.4,0,0.2,1)]',
          accentIndicatorClassName
        )}
        style={{ left: indicator.left, width: indicator.width }}
      />
    </div>
  );
}
