/**
 * KbHomeDesktop.tsx
 * Issue #1482 Task 4 — KB Globale landing component (recent docs grid)
 *
 * Pure presentational component shown when the KB page has no active search
 * query (?q= absent). Receives `recentDocs / isLoading / error` as props from
 * the orchestrator (Task 6) — does NOT call any hooks directly.
 *
 * States:
 *  - Loading:  12 skeleton cards in a 3-col desktop grid
 *  - Error:    inline alert banner with optional retry button
 *  - Empty:    delegates to KbEmptyState kind="no-query"
 *  - Loaded:   responsive grid of doc cards (fileName + gameName + updatedAt)
 *
 * Design tokens: DS-15 semantic tokens (bg-card, text-foreground,
 *   text-muted-foreground, border-border, bg-entity-kb)
 *
 * @see Issue #1482 Phase 1 Foundation
 * @see admin-mockups/design_files/sp4-kb-globale.jsx (Screen 1: "Centro: documenti grid")
 */

import { type JSX } from 'react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { KbDoc } from '@/lib/library/hybrid-hub.mappers';
import { cn } from '@/lib/utils';

import { KbEmptyState } from './KbEmptyState';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface KbHomeDesktopLabels {
  /** Section heading text (h2) */
  heading: string;
  /** Labels forwarded to KbEmptyState when list is empty */
  empty: {
    title: string;
    description: string;
    cta?: string;
  };
  /** Error banner title */
  errorTitle: string;
  /** Error banner description */
  errorDescription: string;
  /** Optional retry button label; if absent no retry button is rendered */
  retry?: string;
  /** Aria-label factory for each doc card (used for a11y) */
  docCardAriaLabel: (doc: KbDoc) => string;
  /** Phase 3 (#1737): label for the per-card edit button (optional; if absent no edit button) */
  editLabel?: string;
}

export interface KbHomeDesktopProps {
  /** Recent KB docs from the orchestrator — NOT fetched inside this component */
  recentDocs: readonly KbDoc[];
  /** True while the orchestrator is fetching */
  isLoading: boolean;
  /** Non-null when the orchestrator query errored */
  error: Error | null;
  /** All UI labels (consumers wire i18n externally) */
  labels: KbHomeDesktopLabels;
  /** Called with doc.id when a doc card is clicked (Phase 2 viewer, optional) */
  onDocClick?: (docId: string) => void;
  /** Called when the KbEmptyState CTA is clicked */
  onEmptyCtaClick?: () => void;
  /** Called when the error banner retry button is clicked */
  onRetry?: () => void;
  /**
   * Phase 3 (#1737): called with the doc when the per-card Edit button is clicked.
   * DEC-3: only provide this from KbGlobaleView home branch (owned docs only).
   * When absent, no Edit button is rendered.
   */
  onEditClick?: (doc: KbDoc) => void;
  /** Extra CSS classes on the root element */
  className?: string;
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

/** 12-card skeleton grid — same columns as the loaded doc grid to prevent layout shift */
function KbHomeSkeletons(): JSX.Element {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
      {Array.from({ length: 12 }).map((_, i) => (
        <div key={`skeleton-${i}`} data-testid="kb-home-skeleton" className="flex flex-col gap-2">
          <Skeleton className="h-16 w-full rounded-md" />
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-3 w-1/2" />
        </div>
      ))}
    </div>
  );
}

interface DocCardProps {
  doc: KbDoc;
  ariaLabel: string;
  onDocClick?: (docId: string) => void;
  /** Phase 3 (#1737): when provided, an Edit button is rendered (DEC-3 owner-only) */
  onEditClick?: (doc: KbDoc) => void;
  editLabel?: string;
}

/** Single doc card — rendered as <button> when onDocClick provided, <div> otherwise */
function DocCard({
  doc,
  ariaLabel,
  onDocClick,
  onEditClick,
  editLabel,
}: DocCardProps): JSX.Element {
  const gameName = doc.gameName ?? '(senza gioco)';

  // Format date — simple locale date, no extra library required
  const dateLabel = doc.updatedAt
    ? new Date(doc.updatedAt).toLocaleDateString('it-IT', {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
      })
    : '—';

  const cardContent = (
    <>
      {/* KB accent bar at top */}
      <div className="h-1.5 w-full rounded-t-md bg-entity-kb/60" aria-hidden="true" />

      {/* Card body */}
      <div className="p-3">
        {/* File name */}
        <p className={cn('truncate text-sm font-semibold text-foreground', 'mb-1 leading-snug')}>
          {doc.fileName}
        </p>

        {/* Game name */}
        <p className="truncate text-xs text-muted-foreground">{gameName}</p>

        {/* Footer: date + optional page count */}
        <div className="mt-2 flex items-center gap-2 font-mono text-[10px] text-muted-foreground">
          <span>{dateLabel}</span>
          {doc.pageCount != null && (
            <>
              <span aria-hidden="true">·</span>
              <span>{doc.pageCount} pag.</span>
            </>
          )}
        </div>

        {/* Phase 3 (#1737): edit affordance — owner-only (DEC-3) */}
        {onEditClick && editLabel && (
          <div className="mt-2">
            <button
              type="button"
              onClick={e => {
                e.stopPropagation();
                onEditClick(doc);
              }}
              className={cn(
                'inline-flex items-center rounded px-2 py-0.5 text-xs font-medium',
                'border border-border text-muted-foreground hover:bg-muted',
                'focus:outline-none focus:ring-2 focus:ring-entity-kb/40 focus:ring-offset-1',
                'transition-colors duration-150'
              )}
            >
              {editLabel}
            </button>
          </div>
        )}
      </div>
    </>
  );

  const sharedClasses = cn(
    'overflow-hidden rounded-md border border-border bg-card',
    'flex flex-col',
    'transition-shadow duration-150'
  );

  if (onDocClick) {
    return (
      <button
        type="button"
        aria-label={ariaLabel}
        onClick={() => onDocClick(doc.id)}
        className={cn(
          sharedClasses,
          'w-full text-left',
          'hover:shadow-md hover:border-entity-kb/40',
          'focus:outline-none focus:ring-2 focus:ring-entity-kb/40 focus:ring-offset-2'
        )}
      >
        {cardContent}
      </button>
    );
  }

  return <div className={sharedClasses}>{cardContent}</div>;
}

interface ErrorBannerProps {
  errorTitle: string;
  errorDescription: string;
  retry?: string;
  onRetry?: () => void;
}

function ErrorBanner({
  errorTitle,
  errorDescription,
  retry,
  onRetry,
}: ErrorBannerProps): JSX.Element {
  const showRetry = Boolean(onRetry && retry);

  return (
    <div
      role="alert"
      className={cn(
        'rounded-md border border-destructive/30 bg-destructive/10',
        'px-4 py-3 text-sm text-foreground'
      )}
    >
      <p className="font-semibold">{errorTitle}</p>
      <p className="mt-0.5 text-muted-foreground">{errorDescription}</p>
      {showRetry && (
        <button
          type="button"
          onClick={onRetry}
          className={cn(
            'mt-2 inline-flex items-center justify-center',
            'rounded-md border border-border px-3 py-1',
            'text-xs font-medium text-foreground',
            'hover:bg-muted',
            'focus:outline-none focus:ring-2 focus:ring-entity-kb/40 focus:ring-offset-1',
            'transition-colors duration-150'
          )}
        >
          {retry}
        </button>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function KbHomeDesktop({
  recentDocs,
  isLoading,
  error,
  labels,
  onDocClick,
  onEmptyCtaClick,
  onRetry,
  onEditClick,
  className,
}: KbHomeDesktopProps): JSX.Element {
  const isEmpty = recentDocs.length === 0 && !isLoading && !error;

  return (
    <section className={cn('flex flex-col gap-4', className)}>
      {/* Section heading — always visible (even in error / loading state) */}
      <h2 className="text-xl font-bold text-foreground">{labels.heading}</h2>

      {/* Error state */}
      {error && !isLoading && (
        <ErrorBanner
          errorTitle={labels.errorTitle}
          errorDescription={labels.errorDescription}
          retry={labels.retry}
          onRetry={onRetry}
        />
      )}

      {/* Loading state */}
      {isLoading && <KbHomeSkeletons />}

      {/* Empty state */}
      {isEmpty && (
        <KbEmptyState kind="no-query" labels={labels.empty} onCtaClick={onEmptyCtaClick} />
      )}

      {/* Loaded state */}
      {!isLoading && !error && recentDocs.length > 0 && (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
          {recentDocs.map(doc => (
            <DocCard
              key={doc.id}
              doc={doc}
              ariaLabel={labels.docCardAriaLabel(doc)}
              onDocClick={onDocClick}
              onEditClick={onEditClick}
              editLabel={labels.editLabel}
            />
          ))}
        </div>
      )}
    </section>
  );
}
