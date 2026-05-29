/**
 * KbSearchResultsDesktop.tsx
 * Issue #1482 Task 5 — KB Globale search results component
 *
 * Pure presentational component shown when the KB page has an active search
 * query (?q=). Receives all data from the orchestrator (Task 6) — does NOT
 * call any hooks directly.
 *
 * States:
 *  - Loading:  5 skeleton rows (list-shaped, compact)
 *  - Error:    inline alert banner with optional retry, result list hidden
 *  - Empty:    delegates to KbEmptyState kind="no-results"
 *  - Loaded:   <ul> of result rows: gameName pill + docTitle + DocType pill
 *              + snippet + headingPath? + pageNumber? + score badge
 *
 * Load-more:
 *  - Explicit button under the list (NO infinite scroll — Nygard D6 predictability)
 *  - Disabled + label swap when isFetchingNextPage
 *
 * Design tokens: DS-15 semantic tokens (bg-card, text-foreground,
 *   text-muted-foreground, border-border, bg-entity-kb, text-entity-kb)
 *
 * @see Issue #1482 Phase 1 Foundation
 * @see admin-mockups/design_files/sp4-kb-globale.jsx (Screen 2: "KB Search Results Desktop")
 */

import { type JSX } from 'react';

import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { GlobalKbSearchResult } from '@/lib/api/schemas/kb-globale.schemas';
import { cn } from '@/lib/utils';

import { KbEmptyState } from './KbEmptyState';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface KbSearchResultsDesktopLabels {
  /** Header count formatter: (n, q) => "12 risultati per «azul»" */
  resultsCount: (n: number, q: string) => string;
  /** Label for the load-more button when idle */
  loadMore: string;
  /** Label for the load-more button when isFetchingNextPage */
  loadingMore: string;
  /** Labels forwarded to KbEmptyState kind="no-results" */
  empty: {
    title: string;
    description: string;
    cta?: string;
  };
  /** Error banner title */
  errorTitle: string;
  /** Error banner description */
  errorDescription: string;
  /** Optional retry button label; omit to hide retry button */
  retry?: string;
  /** Accessibility label factory for each result row */
  resultAriaLabel: (r: GlobalKbSearchResult) => string;
  /** Label for page number, e.g. "Pagina 14" */
  pageLabel: (n: number) => string;
  /** Optional docType display transformer; falls back to raw docType string */
  docTypeLabel?: (rawType: string) => string;
}

export interface KbSearchResultsDesktopProps {
  /** Active search query string (used for header + empty state context) */
  query: string;
  /** Flat list of search result chunks from the orchestrator */
  results: readonly GlobalKbSearchResult[];
  /** True when there are more pages available */
  hasMore: boolean;
  /** True during the initial fetch (no results yet) */
  isLoading: boolean;
  /** True while a load-more page is being fetched */
  isFetchingNextPage: boolean;
  /** Non-null when the orchestrator query errored */
  error: Error | null;
  /** Called when load-more button is clicked */
  onLoadMore: () => void;
  /** Called when a result row is clicked (Phase 2 viewer wire, optional) */
  onResultClick?: (r: GlobalKbSearchResult) => void;
  /** Called when the KbEmptyState CTA is clicked */
  onEmptyCtaClick?: () => void;
  /** Called when the error banner retry button is clicked */
  onRetry?: () => void;
  /** All UI labels (consumers wire i18n externally) */
  labels: KbSearchResultsDesktopLabels;
  /** Extra CSS classes on the root element */
  className?: string;
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

/** 5-row loading skeleton — compact row-shaped to match the result list */
function KbSearchSkeletons(): JSX.Element {
  return (
    <div className="flex flex-col gap-3">
      {Array.from({ length: 5 }).map((_, i) => (
        <div
          key={`skeleton-${i}`}
          data-testid="kb-search-skeleton"
          className="flex flex-col gap-2 rounded-md border border-border bg-card p-4"
        >
          {/* Top line: pill + title */}
          <div className="flex items-center gap-2">
            <Skeleton className="h-5 w-20 rounded-full" />
            <Skeleton className="h-5 w-48" />
          </div>
          {/* Snippet */}
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-3/4" />
          {/* Footer */}
          <div className="flex gap-3">
            <Skeleton className="h-3 w-24" />
            <Skeleton className="h-3 w-16" />
          </div>
        </div>
      ))}
    </div>
  );
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

interface ResultRowProps {
  result: GlobalKbSearchResult;
  ariaLabel: string;
  pageLabel: (n: number) => string;
  docTypeLabel?: (rawType: string) => string;
  onResultClick?: (r: GlobalKbSearchResult) => void;
}

/** Single search result row */
function ResultRow({
  result,
  ariaLabel,
  pageLabel,
  docTypeLabel,
  onResultClick,
}: ResultRowProps): JSX.Element {
  const displayDocType = docTypeLabel ? docTypeLabel(result.docType) : result.docType;
  const formattedScore = result.score.toFixed(2);

  const rowContent = (
    <>
      {/* Top line: gameName pill + docTitle + docType pill */}
      <div className="flex flex-wrap items-center gap-2">
        {/* gameName pill — KB entity color */}
        <span
          className={cn(
            'inline-flex items-center gap-1',
            'rounded-full px-2 py-0.5',
            'bg-entity-kb/10 text-entity-kb',
            'border border-entity-kb/20',
            'text-xs font-semibold',
            'whitespace-nowrap'
          )}
        >
          {result.gameName}
        </span>

        {/* docTitle */}
        <span className="text-sm font-bold text-foreground leading-snug">{result.docTitle}</span>

        {/* DocType pill */}
        <Badge
          variant="outline"
          className="text-[10px] font-semibold uppercase tracking-wide text-muted-foreground"
        >
          {displayDocType}
        </Badge>
      </div>

      {/* Snippet */}
      <p className="mt-2 text-sm text-foreground leading-relaxed line-clamp-3">{result.snippet}</p>

      {/* Footer: headingPath + pageNumber + score */}
      <div className="mt-2 flex flex-wrap items-center gap-3 font-mono text-[11px] text-muted-foreground">
        {result.headingPath != null && (
          <span data-testid="result-heading-path" className="truncate max-w-xs">
            {result.headingPath}
          </span>
        )}

        {result.pageNumber != null && (
          <span data-testid="result-page-number">{pageLabel(result.pageNumber)}</span>
        )}

        {/* Score badge — always shown */}
        <span
          className={cn(
            'ml-auto inline-flex items-center',
            'rounded-full px-2 py-0.5',
            'bg-muted text-muted-foreground',
            'text-[10px] font-mono font-semibold'
          )}
        >
          {formattedScore}
        </span>
      </div>
    </>
  );

  const sharedClasses = cn(
    'w-full rounded-md border border-border bg-card',
    'p-4 flex flex-col',
    'transition-shadow duration-150'
  );

  if (onResultClick) {
    return (
      <button
        type="button"
        aria-label={ariaLabel}
        onClick={() => onResultClick(result)}
        className={cn(
          sharedClasses,
          'text-left',
          'hover:shadow-md hover:border-entity-kb/40',
          'focus:outline-none focus:ring-2 focus:ring-entity-kb/40 focus:ring-offset-2'
        )}
      >
        {rowContent}
      </button>
    );
  }

  return <div className={sharedClasses}>{rowContent}</div>;
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function KbSearchResultsDesktop({
  query,
  results,
  hasMore,
  isLoading,
  isFetchingNextPage,
  error,
  onLoadMore,
  onResultClick,
  onEmptyCtaClick,
  onRetry,
  labels,
  className,
}: KbSearchResultsDesktopProps): JSX.Element {
  const isEmpty = results.length === 0 && !isLoading && !error;
  const showResults = !isLoading && !error && results.length > 0;
  const showLoadMore = hasMore && !error && !isLoading;

  return (
    <section className={cn('flex flex-col gap-4', className)}>
      {/* Header: resultsCount — shown when not in error and not initial loading */}
      {!error && !isLoading && (
        <h2 className="text-xl font-bold text-foreground">
          {labels.resultsCount(results.length, query)}
        </h2>
      )}

      {/* Loading skeleton (initial fetch, no results yet) */}
      {isLoading && results.length === 0 && <KbSearchSkeletons />}

      {/* Error state — hides result list */}
      {error && !isLoading && (
        <ErrorBanner
          errorTitle={labels.errorTitle}
          errorDescription={labels.errorDescription}
          retry={labels.retry}
          onRetry={onRetry}
        />
      )}

      {/* Empty state */}
      {isEmpty && (
        <KbEmptyState kind="no-results" labels={labels.empty} onCtaClick={onEmptyCtaClick} />
      )}

      {/* Loaded state — vertical result list */}
      {showResults && (
        <ul className="flex flex-col gap-3 list-none p-0 m-0">
          {results.map(result => (
            <li key={result.chunkId}>
              <ResultRow
                result={result}
                ariaLabel={labels.resultAriaLabel(result)}
                pageLabel={labels.pageLabel}
                docTypeLabel={labels.docTypeLabel}
                onResultClick={onResultClick}
              />
            </li>
          ))}
        </ul>
      )}

      {/* Load-more button — explicit click, no infinite scroll */}
      {showLoadMore && (
        <div className="flex justify-center pt-2">
          <button
            type="button"
            disabled={isFetchingNextPage}
            onClick={onLoadMore}
            className={cn(
              'inline-flex items-center justify-center gap-2',
              'px-6 py-2.5 rounded-md',
              'border border-border',
              'text-sm font-medium text-foreground',
              'bg-card hover:bg-muted',
              'transition-colors duration-150',
              'focus:outline-none focus:ring-2 focus:ring-entity-kb/40 focus:ring-offset-2',
              'disabled:opacity-50 disabled:cursor-not-allowed'
            )}
          >
            {isFetchingNextPage && (
              <span
                aria-hidden="true"
                className="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent"
              />
            )}
            {isFetchingNextPage ? labels.loadingMore : labels.loadMore}
          </button>
        </div>
      )}
    </section>
  );
}
