/**
 * KbEmptyState.tsx
 * Issue #1482 Task 3 — Empty state component for KB Globale
 *
 * Discriminated component supporting two variants:
 * - kind='no-query': Landing state when user arrives without search query
 *   → "Start searching across your library" with optional CTA
 * - kind='no-results': Post-search state when query returned 0 results
 *   → "No matches for «query»" with optional "Try again" CTA
 *
 * Features:
 * - Labels-injected (no hardcoded i18n strings — consumer wires i18n)
 * - CTA conditional: rendered ONLY when both labels.cta + onCtaClick provided
 * - Optional custom icon slot (or no icon if undefined)
 * - A11y: role="status" (no-results), role="region" + aria-label (no-query)
 * - DS-15 semantic tokens (bg-card, text-foreground, border-border, bg-entity-kb)
 * - jest-axe compliant
 *
 * @see Issue #1482 Phase 1 Foundation
 * @see admin-mockups/design_files/sp4-kb-globale.jsx (KbEmptyState pattern, lines 2437–2533)
 */

import { type JSX, type ReactNode } from 'react';

import { cn } from '@/lib/utils';

export type KbEmptyKind = 'no-query' | 'no-results';

export interface KbEmptyStateLabels {
  /** Main heading text (required) */
  title: string;
  /** Descriptive paragraph text (required) */
  description: string;
  /** CTA button label (optional — if undefined, no button rendered) */
  cta?: string;
}

export interface KbEmptyStateProps {
  /** Variant discriminator: 'no-query' (landing) or 'no-results' (search) */
  kind: KbEmptyKind;
  /** All UI labels (injected by consumer — no hardcoded i18n strings) */
  labels: KbEmptyStateLabels;
  /** Called when user clicks CTA button (optional) */
  onCtaClick?: () => void;
  /** Optional custom icon element to render above title (or undefined for no icon) */
  icon?: ReactNode;
  /** Additional CSS classes applied to root element */
  className?: string;
}

/**
 * KbEmptyState component — discriminated empty state for KB Globale.
 *
 * Variants:
 * - kind='no-query': landing CTA view with role="region"
 * - kind='no-results': search result view with role="status"
 *
 * CTA button rendered only when BOTH labels.cta and onCtaClick provided.
 *
 * @example
 * ```tsx
 * // Landing (no query)
 * <KbEmptyState
 *   kind="no-query"
 *   labels={{
 *     title: "Start searching",
 *     description: "Explore your KB...",
 *     cta: "Upload first document"
 *   }}
 *   onCtaClick={() => openUploadModal()}
 * />
 *
 * // No results (post-search)
 * <KbEmptyState
 *   kind="no-results"
 *   labels={{
 *     title: `No matches for «${query}»`,
 *     description: "Try different keywords...",
 *   }}
 * />
 * ```
 */
export function KbEmptyState({
  kind,
  labels,
  onCtaClick,
  icon,
  className,
}: KbEmptyStateProps): JSX.Element {
  const roleProps =
    kind === 'no-results' ? { role: 'status' } : { role: 'region', 'aria-label': labels.title };

  // CTA rendered only when BOTH labels.cta + onCtaClick present
  const showCta = !!labels.cta && !!onCtaClick;

  return (
    <div
      {...roleProps}
      className={cn(
        // Layout: centered flex column
        'flex flex-col items-center justify-center',
        'py-16 px-6 text-center',
        // Card: KB-themed border + background
        'rounded-xl',
        'bg-card border border-entity-kb/20',
        // Optional spacing wrapper (if parent layout needs margin)
        className
      )}
    >
      {/* Optional icon */}
      {icon && (
        <div className="mb-6 flex items-center justify-center text-entity-kb" aria-hidden="true">
          {icon}
        </div>
      )}

      {/* Title heading */}
      <h2 className={cn('text-2xl font-bold', 'text-foreground', 'mb-3', 'tracking-tight')}>
        {labels.title}
      </h2>

      {/* Description paragraph */}
      <p
        className={cn('text-base', 'text-muted-foreground', 'mb-6', 'max-w-md', 'leading-relaxed')}
      >
        {labels.description}
      </p>

      {/* CTA button (conditional) */}
      {showCta && (
        <button
          type="button"
          onClick={onCtaClick}
          className={cn(
            // Layout
            'inline-flex items-center justify-center',
            'px-6 py-2.5',
            // Typography
            'font-semibold text-sm',
            // Styling: KB entity color (text-white allowed: same string as bg-entity-kb per DS-15)
            'bg-entity-kb text-white hover:bg-entity-kb/90',
            // Border + Transitions
            'border border-entity-kb',
            'rounded-md',
            'transition-colors duration-200',
            // Focus state
            'focus:outline-none focus:ring-2 focus:ring-entity-kb/40 focus:ring-offset-2'
          )}
        >
          {labels.cta}
        </button>
      )}
    </div>
  );
}
