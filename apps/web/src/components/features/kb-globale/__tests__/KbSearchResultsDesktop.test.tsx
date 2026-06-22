/**
 * KbSearchResultsDesktop.test.tsx
 * Issue #1482 Task 5 — KbSearchResultsDesktop search results component tests
 *
 * Tests for the pure-presentational KbSearchResultsDesktop component that
 * renders cross-game search results when the KB page has an active query (?q=).
 *
 * States tested:
 * - Loading:         5 skeleton rows, no result rows
 * - Error:           inline alert banner with optional retry, results hidden
 * - Empty:           delegates to KbEmptyState kind="no-results"
 * - Loaded:          vertical list rows with gameName pill + docTitle + snippet + metadata
 * - Load-more:       explicit button (not infinite scroll), disabled when fetching
 * - Click handlers:  rows clickable only when onResultClick provided
 * - docTypeLabel:    called when provided, falls back to raw docType
 *
 * A11y: jest-axe on loaded / loading / empty states
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import type { GlobalKbSearchResult } from '@/lib/api/schemas/kb-globale.schemas';

import type { KbSearchResultsDesktopProps } from '../KbSearchResultsDesktop';
import { KbSearchResultsDesktop } from '../KbSearchResultsDesktop';

expect.extend(toHaveNoViolations);

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const LABELS: KbSearchResultsDesktopProps['labels'] = {
  resultsCount: (n, q) => `${n} risultati per «${q}»`,
  loadMore: 'Carica altri',
  loadingMore: 'Caricamento...',
  empty: {
    title: 'Nessun risultato trovato',
    description: 'Prova a cambiare i termini di ricerca.',
    cta: 'Nuovo tentativo',
  },
  errorTitle: 'Errore nella ricerca',
  errorDescription: 'Non è stato possibile recuperare i risultati.',
  retry: 'Riprova',
  resultAriaLabel: r => `Vai al risultato: ${r.docTitle}`,
  pageLabel: n => `Pagina ${n}`,
};

function makeResult(overrides: Partial<GlobalKbSearchResult> = {}): GlobalKbSearchResult {
  return {
    chunkId: 'chunk-1',
    docId: '00000000-0000-0000-0000-000000000001',
    docTitle: 'Regole base Gloomhaven',
    gameId: '00000000-0000-0000-0000-000000000010',
    gameName: 'Gloomhaven',
    docType: 'rulebook',
    headingPath: 'Capitolo 1 > Regole di base',
    snippet: 'Il giocatore attivo pesca una carta dalla sua mano.',
    pageNumber: 14,
    score: 0.92,
    ...overrides,
  };
}

const THREE_RESULTS: readonly GlobalKbSearchResult[] = [
  makeResult({
    chunkId: 'chunk-1',
    docId: '00000000-0000-0000-0000-000000000001',
    docTitle: 'Regole base Gloomhaven',
    gameName: 'Gloomhaven',
    docType: 'rulebook',
    headingPath: 'Capitolo 1 > Regole di base',
    snippet: 'Il giocatore attivo pesca una carta.',
    pageNumber: 14,
    score: 0.92,
  }),
  makeResult({
    chunkId: 'chunk-2',
    docId: '00000000-0000-0000-0000-000000000002',
    docTitle: 'Scout abilità speciali',
    gameName: 'Gloomhaven',
    docType: 'clarification',
    headingPath: null,
    snippet: 'Le abilità scout includono movimento silenzioso.',
    pageNumber: null,
    score: 0.85,
  }),
  makeResult({
    chunkId: 'chunk-3',
    docId: '00000000-0000-0000-0000-000000000003',
    docTitle: 'Regole Azul',
    gameName: 'Azul',
    gameId: '00000000-0000-0000-0000-000000000020',
    docType: 'rulebook',
    headingPath: 'Sezione Finale',
    snippet: 'Il punteggio finale viene calcolato a fine partita.',
    pageNumber: 8,
    score: 0.74,
  }),
];

function defaultProps(
  overrides: Partial<KbSearchResultsDesktopProps> = {}
): KbSearchResultsDesktopProps {
  return {
    query: 'scout abilities',
    results: [],
    hasMore: false,
    isLoading: false,
    isFetchingNextPage: false,
    error: null,
    onLoadMore: vi.fn(),
    labels: LABELS,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('KbSearchResultsDesktop', () => {
  // ── 1. Header with resultsCount ─────────────────────────────────────────
  it('renders header with resultsCount formatted', () => {
    render(<KbSearchResultsDesktop {...defaultProps({ results: THREE_RESULTS, query: 'azul' })} />);

    expect(
      screen.getByRole('heading', { level: 2, name: /3 risultati per «azul»/i })
    ).toBeInTheDocument();
  });

  // ── 2. Loading state ────────────────────────────────────────────────────
  it('renders 5 skeleton rows when isLoading && results=[], no result rows', () => {
    render(<KbSearchResultsDesktop {...defaultProps({ isLoading: true, results: [] })} />);

    const skeletons = screen.getAllByTestId('kb-search-skeleton');
    expect(skeletons).toHaveLength(5);

    // No actual result rows
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });

  // ── 3. Error state with retry ───────────────────────────────────────────
  it('renders error banner with retry when error + onRetry, results hidden', async () => {
    const onRetry = vi.fn();
    const error = new Error('Network error');

    render(
      <KbSearchResultsDesktop
        {...defaultProps({
          error,
          onRetry,
          // Even with results, they should be hidden on error
          results: THREE_RESULTS,
        })}
      />
    );

    const alert = screen.getByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(alert).toHaveTextContent(LABELS.errorTitle);
    expect(alert).toHaveTextContent(LABELS.errorDescription);

    const retryBtn = screen.getByRole('button', { name: /riprova/i });
    expect(retryBtn).toBeInTheDocument();
    await userEvent.click(retryBtn);
    expect(onRetry).toHaveBeenCalledTimes(1);

    // Result list is hidden in error state
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  // ── 4. Error state WITHOUT retry button ─────────────────────────────────
  it('renders error banner WITHOUT retry button when onRetry missing', () => {
    const error = new Error('Network error');

    render(<KbSearchResultsDesktop {...defaultProps({ error })} />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
    // No retry prop and no labels.retry → no button
    const propsNoRetryLabel = { ...LABELS };
    delete propsNoRetryLabel.retry;
    expect(screen.queryByRole('button', { name: /riprova/i })).not.toBeInTheDocument();
  });

  // ── 5. Empty state — delegates to KbEmptyState kind="no-results" ────────
  it('renders KbEmptyState kind="no-results" when results=[] + !isLoading + !error', async () => {
    const onEmptyCtaClick = vi.fn();

    render(
      <KbSearchResultsDesktop
        {...defaultProps({
          results: [],
          isLoading: false,
          error: null,
          onEmptyCtaClick,
        })}
      />
    );

    expect(screen.getByText(LABELS.empty.title)).toBeInTheDocument();
    expect(screen.getByText(LABELS.empty.description)).toBeInTheDocument();

    const ctaBtn = screen.getByRole('button', { name: LABELS.empty.cta });
    await userEvent.click(ctaBtn);
    expect(onEmptyCtaClick).toHaveBeenCalledTimes(1);
  });

  // ── 6. Loaded state — renders N result rows ─────────────────────────────
  it('renders N result rows with gameName, docTitle, snippet, pageNumber, score', () => {
    render(<KbSearchResultsDesktop {...defaultProps({ results: THREE_RESULTS })} />);

    // Result list present
    expect(screen.getByRole('list')).toBeInTheDocument();

    // First result
    expect(screen.getByText('Regole base Gloomhaven')).toBeInTheDocument();
    expect(screen.getByText('Il giocatore attivo pesca una carta.')).toBeInTheDocument();
    expect(screen.getByText('Pagina 14')).toBeInTheDocument();
    expect(screen.getByText('0.92')).toBeInTheDocument();

    // gameName pills present
    const gloomhavenChips = screen.getAllByText('Gloomhaven');
    expect(gloomhavenChips.length).toBeGreaterThanOrEqual(1);

    // All 3 items
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
  });

  // ── 7. headingPath rendered when non-null, hidden when null ─────────────
  it('renders headingPath when non-null, does not render when null', () => {
    render(<KbSearchResultsDesktop {...defaultProps({ results: THREE_RESULTS })} />);

    // First result has headingPath
    expect(screen.getByText('Capitolo 1 > Regole di base')).toBeInTheDocument();

    // Third result (Azul) also has headingPath
    expect(screen.getByText('Sezione Finale')).toBeInTheDocument();

    // Second result (Scout) has headingPath=null — should NOT render anything for it
    // Only 2 headingPaths should be visible (chunk-1 and chunk-3)
    const headingPaths = screen.queryAllByTestId('result-heading-path');
    expect(headingPaths).toHaveLength(2);
  });

  // ── 8. pageNumber via labels.pageLabel when non-null, hidden when null ───
  it('renders pageNumber via labels.pageLabel when non-null, not rendered when null', () => {
    render(<KbSearchResultsDesktop {...defaultProps({ results: THREE_RESULTS })} />);

    // First result has pageNumber 14
    expect(screen.getByText('Pagina 14')).toBeInTheDocument();

    // Third result has pageNumber 8
    expect(screen.getByText('Pagina 8')).toBeInTheDocument();

    // Second result has pageNumber null — 'Pagina null' should NOT appear
    expect(screen.queryByText('Pagina null')).not.toBeInTheDocument();

    // Only 2 page number labels
    const pageNumbers = screen.queryAllByTestId('result-page-number');
    expect(pageNumbers).toHaveLength(2);
  });

  // ── 9. clicking a row invokes onResultClick(result) ─────────────────────
  it('clicking a row invokes onResultClick with the result when onResultClick provided', async () => {
    const onResultClick = vi.fn();

    render(<KbSearchResultsDesktop {...defaultProps({ results: THREE_RESULTS, onResultClick })} />);

    const firstRowBtn = screen.getByRole('button', {
      name: LABELS.resultAriaLabel(THREE_RESULTS[0]),
    });
    await userEvent.click(firstRowBtn);
    expect(onResultClick).toHaveBeenCalledWith(THREE_RESULTS[0]);
  });

  // ── 10. rows NOT interactive when onResultClick missing ──────────────────
  it('rows are NOT interactive buttons when onResultClick is not provided', () => {
    render(<KbSearchResultsDesktop {...defaultProps({ results: THREE_RESULTS })} />);

    // No button with result aria-label
    expect(
      screen.queryByRole('button', { name: LABELS.resultAriaLabel(THREE_RESULTS[0]) })
    ).not.toBeInTheDocument();

    // Content still renders
    expect(screen.getByText('Regole base Gloomhaven')).toBeInTheDocument();
  });

  // ── 11. load-more rendered when hasMore + !error + !isLoading + !empty ───
  it('renders load-more button when hasMore + !error + !isLoading, click invokes onLoadMore', async () => {
    const onLoadMore = vi.fn();

    render(
      <KbSearchResultsDesktop
        {...defaultProps({ results: THREE_RESULTS, hasMore: true, onLoadMore })}
      />
    );

    const loadMoreBtn = screen.getByRole('button', { name: LABELS.loadMore });
    expect(loadMoreBtn).toBeInTheDocument();
    expect(loadMoreBtn).not.toBeDisabled();

    await userEvent.click(loadMoreBtn);
    expect(onLoadMore).toHaveBeenCalledTimes(1);
  });

  // ── 12. load-more shows loadingMore + disabled when isFetchingNextPage ───
  it('shows loadingMore label and disabled state when isFetchingNextPage is true', () => {
    render(
      <KbSearchResultsDesktop
        {...defaultProps({
          results: THREE_RESULTS,
          hasMore: true,
          isFetchingNextPage: true,
        })}
      />
    );

    const loadMoreBtn = screen.getByRole('button', { name: LABELS.loadingMore });
    expect(loadMoreBtn).toBeInTheDocument();
    expect(loadMoreBtn).toBeDisabled();
  });

  // ── 13. load-more NOT rendered when hasMore=false ───────────────────────
  it('does not render load-more button when hasMore=false', () => {
    render(
      <KbSearchResultsDesktop {...defaultProps({ results: THREE_RESULTS, hasMore: false })} />
    );

    expect(screen.queryByRole('button', { name: LABELS.loadMore })).not.toBeInTheDocument();
  });

  // ── 14. load-more NOT rendered when error present ───────────────────────
  it('does not render load-more button when error is present', () => {
    render(
      <KbSearchResultsDesktop
        {...defaultProps({
          results: THREE_RESULTS,
          hasMore: true,
          error: new Error('fail'),
        })}
      />
    );

    expect(screen.queryByRole('button', { name: LABELS.loadMore })).not.toBeInTheDocument();
  });

  // ── 15. docTypeLabel called when provided, falls back to raw docType ─────
  it('uses docTypeLabel when provided, falls back to raw docType when absent', () => {
    const docTypeLabel = vi.fn((rawType: string) => rawType.toUpperCase());

    render(
      <KbSearchResultsDesktop
        {...defaultProps({
          results: THREE_RESULTS,
          labels: { ...LABELS, docTypeLabel },
        })}
      />
    );

    // docTypeLabel was called for each result's docType
    expect(docTypeLabel).toHaveBeenCalledWith('rulebook');
    expect(docTypeLabel).toHaveBeenCalledWith('clarification');

    // The transformed values appear (not lowercase originals)
    expect(screen.getAllByText('RULEBOOK').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('CLARIFICATION')).toBeInTheDocument();
  });

  it('renders raw docType when docTypeLabel is not provided', () => {
    render(
      <KbSearchResultsDesktop
        {...defaultProps({
          results: [makeResult({ docType: 'house-rule' })],
          labels: { ...LABELS, docTypeLabel: undefined },
        })}
      />
    );

    expect(screen.getByText('house-rule')).toBeInTheDocument();
  });

  // ── 16. jest-axe: loaded state ──────────────────────────────────────────
  it('has no axe violations in the loaded state', async () => {
    const { container } = render(
      <KbSearchResultsDesktop {...defaultProps({ results: THREE_RESULTS })} />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  // ── 17. jest-axe: loading state ─────────────────────────────────────────
  it('has no axe violations in the loading state', async () => {
    const { container } = render(
      <KbSearchResultsDesktop {...defaultProps({ isLoading: true, results: [] })} />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  // ── 18. jest-axe: empty state ────────────────────────────────────────────
  it('has no axe violations in the empty state', async () => {
    const { container } = render(
      <KbSearchResultsDesktop {...defaultProps({ results: [], isLoading: false, error: null })} />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
