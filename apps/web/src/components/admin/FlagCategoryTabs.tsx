/**
 * FlagCategoryTabs (Issue #1836)
 *
 * Second-level navigation inside `/admin/config?tab=flags`. Filters feature
 * flags by inferred category (AI, Integrations, Security, Features…) plus an
 * "All" facet. Pure client-side: the active category is mirrored to the URL
 * hash (e.g. `#category=ai`) so deep links and history work without a server
 * roundtrip.
 *
 * Category inference is best-effort because the backend does not yet expose a
 * dedicated `subcategory` field — see {@link detectFlagCategory}.
 */

'use client';

import type { ReactNode } from 'react';

import { Brain, Flag, Layers, Plug, ShieldCheck } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { useTablistKeyboardNav } from '@/hooks/useTablistKeyboardNav';
import { cn } from '@/lib/utils';

export type FlagCategory = 'all' | 'features' | 'ai' | 'integrations' | 'security';

export const FLAG_CATEGORIES: readonly FlagCategory[] = [
  'all',
  'features',
  'ai',
  'integrations',
  'security',
] as const;

interface CategoryMeta {
  label: string;
  icon: ReactNode;
}

const CATEGORY_META: Record<FlagCategory, CategoryMeta> = {
  all: { label: 'All', icon: <Layers className="h-3.5 w-3.5" /> },
  features: { label: 'Features', icon: <Flag className="h-3.5 w-3.5" /> },
  ai: { label: 'AI', icon: <Brain className="h-3.5 w-3.5" /> },
  integrations: { label: 'Integrations', icon: <Plug className="h-3.5 w-3.5" /> },
  security: { label: 'Security', icon: <ShieldCheck className="h-3.5 w-3.5" /> },
};

/**
 * Infer the sub-category of a feature flag from its key.
 *
 * The backend exposes a single `category` ("FeatureFlag") so we lean on the
 * convention that the right-hand side of `Features:Xxx` is descriptive. The
 * rules are ordered most-specific → least-specific.
 */
export function detectFlagCategory(key: string): Exclude<FlagCategory, 'all'> {
  const lower = key.toLowerCase();
  // Order matters: integrations must run before security because "oauth"
  // contains the substring "auth". A bare-word boundary on "auth" inside
  // the security regex would still fire on "oauth" without lookbehind.
  if (/oauth|webhook|integration|n8n|external|connector|sso|saml/.test(lower)) {
    return 'integrations';
  }
  if (
    /security|mfa|2fa|totp|csrf|password|encryption|rate.?limit|firewall|(^|[^a-z])auth(?![a-z])/.test(
      lower
    )
  ) {
    return 'security';
  }
  if (
    /(^|[^a-z])(ai|llm|rag|embed|stream|vector|prompt|model|deepseek|openai|anthropic|ollama)/.test(
      lower
    )
  ) {
    return 'ai';
  }
  return 'features';
}

export interface FlagCategoryTabsProps {
  /**
   * All known feature flag keys, used to compute per-category counts.
   */
  flagKeys: readonly string[];

  /**
   * Currently active category.
   */
  activeCategory: FlagCategory;

  /**
   * Called when the user picks a different category.
   */
  onCategoryChange: (next: FlagCategory) => void;

  className?: string;
}

export function FlagCategoryTabs({
  flagKeys,
  activeCategory,
  onCategoryChange,
  className,
}: FlagCategoryTabsProps) {
  const counts = computeCategoryCounts(flagKeys);
  // Issue #1836 review fix: wire WAI-ARIA APG roving-tabindex + Arrow/Home/End
  // keyboard navigation per the project's `useTablistKeyboardNav` pattern.
  const { tabRefs, handleKeyDown } = useTablistKeyboardNav<FlagCategory>({
    orderedKeys: FLAG_CATEGORIES,
    onChange: onCategoryChange,
    orientation: 'horizontal',
  });

  return (
    <div
      role="tablist"
      aria-label="Feature flag categories"
      className={cn('flex gap-1 flex-wrap', 'border-b border-border/40 pb-px -mb-px', className)}
      data-testid="flag-category-tabs"
    >
      {FLAG_CATEGORIES.map(cat => {
        const meta = CATEGORY_META[cat];
        const isActive = cat === activeCategory;
        const count = counts[cat];

        return (
          <button
            key={cat}
            type="button"
            role="tab"
            aria-selected={isActive}
            data-active={isActive}
            data-testid={`flag-category-tab-${cat}`}
            tabIndex={isActive ? 0 : -1}
            ref={node => {
              if (node) tabRefs.current.set(cat, node);
              else tabRefs.current.delete(cat);
            }}
            onClick={() => onCategoryChange(cat)}
            onKeyDown={e => handleKeyDown(e, cat)}
            className={cn(
              'inline-flex items-center gap-1.5 rounded-lg px-3 py-1.5 text-sm font-medium',
              'transition-all duration-200 shrink-0',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 focus-visible:ring-offset-1',
              isActive
                ? 'bg-card text-foreground shadow-sm border border-border'
                : 'text-muted-foreground hover:text-foreground hover:bg-muted/50 border border-transparent'
            )}
          >
            <span
              className={cn('shrink-0', isActive ? 'text-primary' : 'text-muted-foreground/70')}
              aria-hidden="true"
            >
              {meta.icon}
            </span>
            <span>{meta.label}</span>
            <Badge
              variant="outline"
              className={cn(
                'h-5 px-1.5 text-[10px] tabular-nums',
                isActive && 'border-primary/30 text-foreground'
              )}
              data-testid={`flag-category-tab-${cat}-count`}
            >
              {count}
            </Badge>
          </button>
        );
      })}
    </div>
  );
}

FlagCategoryTabs.displayName = 'FlagCategoryTabs';

/**
 * Compute the per-category counts for a list of flag keys.
 * `all` always equals the total length.
 */
export function computeCategoryCounts(flagKeys: readonly string[]): Record<FlagCategory, number> {
  const counts: Record<FlagCategory, number> = {
    all: flagKeys.length,
    features: 0,
    ai: 0,
    integrations: 0,
    security: 0,
  };
  for (const key of flagKeys) {
    counts[detectFlagCategory(key)]++;
  }
  return counts;
}

/**
 * Read the requested category from the URL hash (`#category=ai`).
 * Returns `'all'` if the hash is missing or invalid. Safe to call during SSR.
 */
export function readCategoryFromHash(): FlagCategory {
  if (typeof window === 'undefined') return 'all';
  const match = window.location.hash.match(/category=([a-z]+)/i);
  if (!match) return 'all';
  const candidate = match[1].toLowerCase() as FlagCategory;
  return FLAG_CATEGORIES.includes(candidate) ? candidate : 'all';
}

/**
 * Mirror the active category to the URL hash. Calling this with `'all'`
 * removes the hash entirely to keep canonical URLs clean.
 */
export function writeCategoryToHash(category: FlagCategory): void {
  if (typeof window === 'undefined') return;
  const url = new URL(window.location.href);
  if (category === 'all') {
    url.hash = '';
  } else {
    url.hash = `category=${category}`;
  }
  // Replace history entry to avoid filling the back-stack on every tab click.
  window.history.replaceState(null, '', url.toString());
}

export default FlagCategoryTabs;
