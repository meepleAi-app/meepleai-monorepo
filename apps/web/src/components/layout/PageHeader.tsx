/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or decorative inline gradient; mockup .e-bg pattern. Will be re-evaluated in DS-15 finalization audit. */
'use client';

import { useEffect, useLayoutEffect, useRef, useState } from 'react';

import Link from 'next/link';

import { cn } from '@/lib/utils';

// useLayoutEffect on the client, useEffect on SSR so React doesn't warn during
// hydration. The underline measurement only matters once the DOM is ready.
const useIsoLayoutEffect = typeof window === 'undefined' ? useEffect : useLayoutEffect;

export interface PageHeaderTab {
  id: string;
  label: string;
  href: string;
  count?: number;
}

export interface PageHeaderAction {
  label: string;
  onClick: () => void;
  icon?: React.ReactNode;
}

export interface PageHeaderProps {
  title: string;
  subtitle?: string;
  parentHref?: string;
  parentLabel?: string;
  primaryAction?: PageHeaderAction;
  tabs?: PageHeaderTab[];
  activeTabId?: string;
  className?: string;
}

/**
 * @deprecated Issue #2158 (Fix #2 bis). PageHeader is the pre-Asse-B header
 * (h1 + tabs + primaryAction). It was superseded by `useMiniNavConfig` for
 * the breadcrumb+tabs strip on `MiniNavSlot` and by inline page-specific
 * headers for CTAs. The 8 in-tree consumers were migrated in #2158; new
 * imports are blocked by `no-restricted-imports` in `eslint.config.mjs`.
 *
 * The component is retained (rather than deleted) only to preserve the
 * public type surface (`PageHeaderProps`, `PageHeaderTab`, `PageHeaderAction`)
 * for any in-flight branch that still references it. Delete-on-sight once
 * all branches have rebased on top of #2158.
 */
export function PageHeader({
  title,
  subtitle,
  parentHref,
  parentLabel,
  primaryAction,
  tabs,
  activeTabId,
  className,
}: PageHeaderProps) {
  // Mockup parity (sp3-shared-game-detail.jsx Tabs): the active tab gets an
  // animated underline that SLIDES (left + width) when the user switches tab,
  // instead of the previous static `border-b-2` which only fades color.
  // Pattern: track the active tab's anchor ref, measure offsetLeft/Width,
  // then animate a single absolute-positioned `<span>` underneath the tab row.
  const tabRefs = useRef<Record<string, HTMLAnchorElement | null>>({});
  const [underline, setUnderline] = useState<{ left: number; width: number }>({
    left: 0,
    width: 0,
  });

  useIsoLayoutEffect(() => {
    if (!tabs || !activeTabId) return;
    const el = tabRefs.current[activeTabId];
    if (el) {
      setUnderline({ left: el.offsetLeft, width: el.offsetWidth });
    }
  }, [activeTabId, tabs?.length]);

  return (
    <div className={cn('w-full', className)}>
      {/* Back link — both parentHref and parentLabel must be present */}
      {parentHref && parentLabel && (
        <div className="mb-1">
          <Link
            href={parentHref}
            className="inline-flex items-center gap-1 text-sm font-medium"
            style={{ color: 'hsl(var(--c-kb-text))' }}
          >
            <span aria-hidden="true">←</span>
            <span>{parentLabel}</span>
          </Link>
        </div>
      )}

      {/* Title row */}
      <div className="flex items-center justify-between gap-4">
        <div className="min-w-0">
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground truncate">
            {title}
          </h1>
          {subtitle && <p className="mt-0.5 text-sm text-muted-foreground">{subtitle}</p>}
        </div>

        {primaryAction && (
          <button
            type="button"
            onClick={primaryAction.onClick}
            className="inline-flex shrink-0 items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2"
            style={{ backgroundColor: 'hsl(25, 95%, 38%)' }}
          >
            {primaryAction.icon && <span className="shrink-0">{primaryAction.icon}</span>}
            {primaryAction.label}
          </button>
        )}
      </div>

      {/* Tabs */}
      {tabs && tabs.length > 0 && (
        <nav className="relative mt-4 flex gap-0 border-b border-border" aria-label="Page tabs">
          {tabs.map(tab => {
            const isActive = tab.id === activeTabId;
            return (
              <Link
                key={tab.id}
                href={tab.href}
                ref={(el: HTMLAnchorElement | null) => {
                  tabRefs.current[tab.id] = el;
                }}
                aria-current={isActive ? 'page' : undefined}
                className={cn(
                  'inline-flex items-center gap-1.5 px-3 pb-3 pt-1 text-sm font-medium transition-colors',
                  isActive
                    ? 'text-[hsl(var(--c-game-text))]'
                    : 'text-muted-foreground hover:text-foreground'
                )}
              >
                {tab.label}
                {tab.count !== undefined && (
                  <span
                    className={cn(
                      'rounded-full px-1.5 py-0.5 text-xs font-semibold tabular-nums',
                      isActive
                        ? 'bg-[hsl(25,95%,45%)] text-white'
                        : 'bg-muted text-muted-foreground'
                    )}
                  >
                    {tab.count}
                  </span>
                )}
              </Link>
            );
          })}
          {/* Animated underline — slides + grows between tabs (300ms ease-out).
              motion-reduce variant kills the transition so users with vestibular
              sensitivity see an instant jump instead of a slide. */}
          <span
            aria-hidden="true"
            data-testid="page-header-tab-underline"
            className="pointer-events-none absolute bottom-[-1px] h-0.5 rounded-t bg-[hsl(25,95%,45%)] transition-[left,width] duration-300 ease-out motion-reduce:transition-none"
            style={{ left: underline.left, width: underline.width }}
          />
        </nav>
      )}
    </div>
  );
}
