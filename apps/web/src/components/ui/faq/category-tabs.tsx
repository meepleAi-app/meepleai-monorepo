/**
 * CategoryTabs — sticky horizontal scrollable tab list with count badges.
 *
 * Wave A.1 Phase 4.5 (Issue #583). Mirrors mockup lines 324-388.
 *
 * Spec §3.2: `role="tablist"` + per-button `role="tab"` + `aria-selected`.
 * Sticky default ON; opt-out via `sticky={false}` when used inside scroll
 * container that already handles stickiness.
 *
 * Closes Issue #588 (a11y: WAI-ARIA tablist Arrow-key navigation).
 * Wave A.4 absorbed #588 in `shared-game-detail/tabs.tsx` but the original
 * FAQ CategoryTabs site was missed — this restores the contract here.
 *
 * Keyboard contract (mirrors `shared-game-detail/tabs.tsx`):
 *   - ArrowLeft  → previous tab (wraps first → last)
 *   - ArrowRight → next tab (wraps last → first)
 *   - Home       → first tab
 *   - End        → last tab
 *   - Activation is automatic (focus = onChange same tick) — FAQ panels are
 *     statically rendered, no lazy-mount cost to amortize.
 */

'use client';

import type { JSX } from 'react';
import { useMemo } from 'react';

import clsx from 'clsx';

import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';
import type { CategoryId, FAQCategory } from '@/lib/faq/data';

export interface CategoryTabsProps {
  readonly categories: ReadonlyArray<FAQCategory>;
  readonly active: CategoryId;
  readonly onChange: (id: CategoryId) => void;
  readonly counts: Record<CategoryId, number>;
  readonly resolveLabel: (cat: FAQCategory) => string;
  readonly sticky?: boolean;
  readonly ariaLabel?: string;
  readonly className?: string;
}

export function CategoryTabs({
  categories,
  active,
  onChange,
  counts,
  resolveLabel,
  sticky = true,
  ariaLabel = 'FAQ categories',
  className,
}: CategoryTabsProps): JSX.Element {
  const orderedIds = useMemo(() => categories.map(c => c.id), [categories]);
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<CategoryId>({
    orderedKeys: orderedIds,
    onChange,
  });

  return (
    <div
      data-slot="category-tabs"
      className={clsx(
        'border-b border-border bg-[hsl(var(--glass-bg))] backdrop-blur-md',
        sticky && 'sticky top-0 z-[3]',
        className
      )}
    >
      <div
        role="tablist"
        aria-label={ariaLabel}
        className="relative flex items-center gap-1 overflow-x-auto py-2.5 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
      >
        {categories.map(cat => {
          const isActive = cat.id === active;
          const count = counts[cat.id] ?? 0;
          return (
            <button
              key={cat.id}
              ref={node => {
                if (node) tabRefs.current.set(cat.id, node);
                else tabRefs.current.delete(cat.id);
              }}
              type="button"
              role="tab"
              aria-selected={isActive}
              aria-controls={`faq-panel-${cat.id}`}
              id={`faq-tab-${cat.id}`}
              tabIndex={isActive ? 0 : -1}
              onClick={() => onChange(cat.id)}
              onKeyDown={e => handleKeyDown(e, cat.id)}
              className={clsx(
                'inline-flex flex-shrink-0 items-center gap-1.5 whitespace-nowrap',
                'px-[13px] py-2 rounded-full cursor-pointer',
                'font-display text-[12.5px]',
                'transition-[background-color,border-color,color] duration-150 ease-out',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-game))] focus-visible:ring-offset-2',
                isActive
                  ? 'border border-[hsl(var(--c-game)/0.3)] bg-[hsl(var(--c-game)/0.12)] text-[hsl(var(--c-game))] font-bold'
                  : 'border border-border bg-transparent text-[hsl(var(--text-sec))] font-semibold hover:bg-[hsl(var(--bg-hover))]'
              )}
            >
              <span aria-hidden="true">{cat.icon}</span>
              <span>{resolveLabel(cat)}</span>
              <span
                className={clsx(
                  'min-w-4 text-center font-mono text-[9px] font-extrabold tabular-nums',
                  'rounded-full px-1.5 py-px',
                  isActive
                    ? 'bg-[hsl(var(--c-game)/0.25)] text-[hsl(var(--c-game))] dark:bg-[hsl(var(--c-game)/0.4)] dark:text-foreground'
                    : 'bg-[hsl(var(--bg-muted))] text-[hsl(var(--text-muted))]'
                )}
              >
                {count}
              </span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
