/**
 * GameDetailTabsAnimated - v2 Wave C.1 (Issue #581)
 *
 * Mapped from `admin-mockups/design_files/sp4-game-detail.jsx` (GameTabs).
 * Spec: docs/superpowers/specs/2026-04-26-v2-design-migration.md (Phase 1+2)
 * Tracking: docs/frontend/v2-migration-matrix.md (Issue #573)
 *
 * Animated underline tab navigation. Reuses `useTablistKeyboardNav` hook
 * (Wave A.6 PR #623, Issue #622) for WAI-ARIA APG keyboard contract:
 *   - ArrowLeft/Right wrap, Home/End jump, automatic activation.
 *
 * Reduced motion: animation gated via Tailwind `motion-safe:` modifier so
 * the underline transition collapses to instant under `prefers-reduced-motion: reduce`.
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

export interface GameDetailTabConfig<TKey extends string = string> {
  readonly key: TKey;
  readonly label: string;
  readonly icon?: ReactNode;
  readonly count?: number;
  readonly locked?: boolean;
}

export interface GameDetailTabsAnimatedProps<TKey extends string = string> {
  readonly tabs: ReadonlyArray<GameDetailTabConfig<TKey>>;
  readonly active: TKey;
  readonly onChange: (key: TKey) => void;
  readonly ariaLabel: string;
  readonly className?: string;
}

// SSR-safe useLayoutEffect (mirror common React pattern).
const useIsomorphicLayoutEffect = typeof window !== 'undefined' ? useLayoutEffect : useEffect;

export function GameDetailTabsAnimated<TKey extends string = string>(
  props: GameDetailTabsAnimatedProps<TKey>
): ReactElement {
  const { tabs, active, onChange, ariaLabel, className } = props;

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

  // Reposition the underline indicator whenever active tab or layout changes.
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

  return (
    <div
      ref={containerRef}
      role="tablist"
      aria-label={ariaLabel}
      data-slot="game-detail-tabs"
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
            aria-controls={`game-detail-panel-${tab.key}`}
            id={`game-detail-tab-${tab.key}`}
            tabIndex={isActive ? 0 : -1}
            disabled={isLocked}
            data-slot="game-detail-tab"
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
                  ? 'font-extrabold text-amber-700 dark:text-amber-400'
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
                  isActive
                    ? 'bg-amber-700 text-white dark:bg-amber-500'
                    : 'bg-muted text-muted-foreground'
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
        data-slot="game-detail-tab-indicator"
        className="absolute bottom-[-1px] h-0.5 rounded-t-[2px] bg-amber-700 transition-all duration-300 ease-[cubic-bezier(0.4,0,0.2,1)] motion-reduce:transition-none dark:bg-amber-500"
        style={{ left: indicator.left, width: indicator.width }}
      />
    </div>
  );
}
