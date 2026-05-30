/**
 * KbHomeDesktop.test.tsx
 * Issue #1482 Task 4 — KbHomeDesktop landing component tests
 *
 * Tests for the pure-presentational KbHomeDesktop component that renders
 * recent KB docs when there is no active search query.
 *
 * States tested:
 * - Loading: 12 skeleton cards, no doc cards
 * - Error: inline banner with role="alert", optional retry button
 * - Empty: delegates to KbEmptyState kind="no-query"
 * - Loaded: grid of doc cards, fileName + gameName + updatedAt visible
 *
 * A11y: jest-axe on loaded / loading / empty states
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import type { KbDoc } from '@/lib/library/hybrid-hub.mappers';

import type { KbHomeDesktopProps } from '../KbHomeDesktop';
import { KbHomeDesktop } from '../KbHomeDesktop';

expect.extend(toHaveNoViolations);

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const HEADING = 'Documenti recenti';

const LABELS: KbHomeDesktopProps['labels'] = {
  heading: HEADING,
  empty: {
    title: 'Nessun documento ancora',
    description: 'Carica il tuo primo PDF per iniziare.',
    cta: 'Carica PDF',
  },
  errorTitle: 'Errore nel caricamento',
  errorDescription: 'Non è stato possibile recuperare i documenti.',
  retry: 'Riprova',
  docCardAriaLabel: doc => `Apri documento ${doc.fileName}`,
};

function makeDoc(overrides: Partial<KbDoc> = {}): KbDoc {
  return {
    id: 'doc-1',
    gameId: 'game-1',
    gameName: 'Gloomhaven',
    fileName: 'gloomhaven-rulebook.pdf',
    processingState: 'ready',
    pageCount: 72,
    processedAt: '2026-05-20T10:00:00Z',
    uploadedAt: '2026-05-19T08:00:00Z',
    updatedAt: '2026-05-20T10:00:00Z',
    ...overrides,
  };
}

const THREE_DOCS: readonly KbDoc[] = [
  makeDoc({ id: 'doc-1', fileName: 'gloomhaven-rulebook.pdf', gameName: 'Gloomhaven' }),
  makeDoc({
    id: 'doc-2',
    fileName: 'wingspan-guide.pdf',
    gameName: 'Wingspan',
    gameId: 'game-2',
  }),
  makeDoc({
    id: 'doc-3',
    fileName: 'brass-rules.pdf',
    gameName: null,
    gameId: null,
    processingState: 'indexing',
  }),
];

function defaultProps(overrides: Partial<KbHomeDesktopProps> = {}): KbHomeDesktopProps {
  return {
    recentDocs: [],
    isLoading: false,
    error: null,
    labels: LABELS,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('KbHomeDesktop', () => {
  // ── 1. Renders heading ──────────────────────────────────────────────────
  it('renders heading with labels.heading', () => {
    // Use a loaded state (3 docs) to avoid KbEmptyState also rendering an h2
    render(<KbHomeDesktop {...defaultProps({ recentDocs: THREE_DOCS })} />);

    expect(screen.getByRole('heading', { level: 2, name: HEADING })).toBeInTheDocument();
  });

  // ── 2. Loading state: 12 skeleton cards, no doc cards ──────────────────
  it('renders 12 skeleton cards when isLoading is true, no doc cards visible', () => {
    render(<KbHomeDesktop {...defaultProps({ isLoading: true })} />);

    const skeletons = screen.getAllByTestId('kb-home-skeleton');
    expect(skeletons).toHaveLength(12);

    // No actual document cards rendered
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  // ── 3. Error state with retry ───────────────────────────────────────────
  it('renders error banner with role="alert" and retry button when error + onRetry + labels.retry provided', async () => {
    const onRetry = vi.fn();
    const error = new Error('Network error');

    render(
      <KbHomeDesktop
        {...defaultProps({
          error,
          onRetry,
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
  });

  // ── 4. Error state WITHOUT retry button when onRetry missing ───────────
  it('renders error banner WITHOUT retry button when onRetry is not provided', () => {
    const error = new Error('Network error');

    render(<KbHomeDesktop {...defaultProps({ error })} />);

    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /riprova/i })).not.toBeInTheDocument();
  });

  // ── 5. Empty state — delegates to KbEmptyState kind="no-query" ─────────
  it('renders KbEmptyState kind="no-query" when recentDocs=[], not loading, no error', () => {
    const onEmptyCtaClick = vi.fn();

    render(
      <KbHomeDesktop
        {...defaultProps({
          recentDocs: [],
          isLoading: false,
          error: null,
          onEmptyCtaClick,
        })}
      />
    );

    // KbEmptyState renders the empty.title and empty.description
    expect(screen.getByText(LABELS.empty.title)).toBeInTheDocument();
    expect(screen.getByText(LABELS.empty.description)).toBeInTheDocument();
  });

  // ── 5b. Empty CTA click invokes onEmptyCtaClick ─────────────────────────
  it('invokes onEmptyCtaClick when KbEmptyState CTA is clicked', async () => {
    const onEmptyCtaClick = vi.fn();

    render(
      <KbHomeDesktop
        {...defaultProps({
          recentDocs: [],
          onEmptyCtaClick,
        })}
      />
    );

    const ctaBtn = screen.getByRole('button', { name: LABELS.empty.cta });
    await userEvent.click(ctaBtn);
    expect(onEmptyCtaClick).toHaveBeenCalledTimes(1);
  });

  // ── 6. Loaded state — renders N doc cards ──────────────────────────────
  it('renders N doc cards when recentDocs is populated', () => {
    render(<KbHomeDesktop {...defaultProps({ recentDocs: THREE_DOCS })} />);

    // Each doc card shows fileName
    expect(screen.getByText('gloomhaven-rulebook.pdf')).toBeInTheDocument();
    expect(screen.getByText('wingspan-guide.pdf')).toBeInTheDocument();
    expect(screen.getByText('brass-rules.pdf')).toBeInTheDocument();

    // gameName visible for docs that have it
    expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  // ── 7. Clicking a card invokes onDocClick(docId) ───────────────────────
  it('invokes onDocClick with the docId when a card is clicked', async () => {
    const onDocClick = vi.fn();

    render(<KbHomeDesktop {...defaultProps({ recentDocs: THREE_DOCS, onDocClick })} />);

    const cardBtn = screen.getByRole('button', {
      name: LABELS.docCardAriaLabel(THREE_DOCS[0]),
    });
    await userEvent.click(cardBtn);
    expect(onDocClick).toHaveBeenCalledWith(THREE_DOCS[0].id);
  });

  // ── 8. Card NOT clickable when onDocClick missing ──────────────────────
  it('does NOT render a button wrapper on doc cards when onDocClick is not provided', () => {
    render(<KbHomeDesktop {...defaultProps({ recentDocs: THREE_DOCS })} />);

    // No interactive role for doc cards
    expect(
      screen.queryByRole('button', { name: LABELS.docCardAriaLabel(THREE_DOCS[0]) })
    ).not.toBeInTheDocument();

    // But doc content still renders
    expect(screen.getByText('gloomhaven-rulebook.pdf')).toBeInTheDocument();
  });

  // ── 9. gameName fallback when doc.gameName is null ─────────────────────
  it('renders fallback text "(senza gioco)" when doc.gameName is null', () => {
    const docNoGame = makeDoc({ id: 'doc-no-game', gameName: null, gameId: null });

    render(<KbHomeDesktop {...defaultProps({ recentDocs: [docNoGame] })} />);

    expect(screen.getByText('(senza gioco)')).toBeInTheDocument();
  });

  // ── 10. jest-axe: loaded state ─────────────────────────────────────────
  it('has no axe violations in the loaded state', async () => {
    const { container } = render(<KbHomeDesktop {...defaultProps({ recentDocs: THREE_DOCS })} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  // ── 11. jest-axe: loading state ────────────────────────────────────────
  it('has no axe violations in the loading state', async () => {
    const { container } = render(<KbHomeDesktop {...defaultProps({ isLoading: true })} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  // ── 12. jest-axe: empty state ──────────────────────────────────────────
  it('has no axe violations in the empty state', async () => {
    const { container } = render(
      <KbHomeDesktop {...defaultProps({ recentDocs: [], isLoading: false, error: null })} />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
