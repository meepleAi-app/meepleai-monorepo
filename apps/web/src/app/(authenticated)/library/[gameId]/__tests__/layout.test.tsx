/**
 * Library Game Detail Layout — #1816 P2-2 regression
 *
 * Asserts the 3-state h1 + `document.title` resolution introduced by P2-2:
 *   - loading: "Loading game…" + document.title "Loading game — MeepleAI"
 *   - loaded:  "Catan" + document.title "Catan · MeepleAI"
 *   - 404:     "Game not found" + document.title "Game not found — MeepleAI"
 *
 * Pre-fix behavior was a hardcoded "Gioco" literal regardless of state, which
 * broke a11y (screen reader heading) + SEO (`<title>`) + breadcrumb
 * differentiation. Audit ref: 2026-06-02-mobile-golden-path-audit § P2 h1.
 *
 * Test scope: the inner `LibraryGameHeader` component (exported from the
 * layout file for this purpose). The Suspense boundary in the default export
 * is not exercised here to avoid a jsdom timeout under react-intl + Suspense.
 */

import { screen } from '@testing-library/react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

import { renderWithIntl } from '../../../../../__tests__/fixtures/common-fixtures';

import { LibraryGameHeader } from '../layout';

// ── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
  useRouter: vi.fn(),
  useSearchParams: vi.fn(),
}));

const mockUseLibraryGameDetail = vi.fn();
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: (...args: unknown[]) => mockUseLibraryGameDetail(...args),
}));

beforeEach(() => {
  vi.clearAllMocks();
  (useParams as Mock).mockReturnValue({ gameId: 'cc1678e8-f460-4b53-81f6-6d6539f82b65' });
  (useRouter as Mock).mockReturnValue({ push: vi.fn() });
  (useSearchParams as Mock).mockReturnValue(new URLSearchParams(''));
});

describe('LibraryGameHeader — #1816 P2-2 h1 + document.title state machine', () => {
  it('renders loading h1 + document.title while the query is pending', () => {
    mockUseLibraryGameDetail.mockReturnValue({ data: undefined, isLoading: true });

    renderWithIntl(<LibraryGameHeader />);

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Loading game…');
    expect(document.title).toBe('Loading game — MeepleAI');
  });

  it('renders the game title as h1 + document.title once data resolves', () => {
    mockUseLibraryGameDetail.mockReturnValue({
      data: { gameTitle: 'Catan' },
      isLoading: false,
    });

    renderWithIntl(<LibraryGameHeader />);

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Catan');
    expect(document.title).toBe('Catan · MeepleAI');
  });

  it('renders 404 h1 + document.title when the query resolves to null', () => {
    mockUseLibraryGameDetail.mockReturnValue({ data: null, isLoading: false });

    renderWithIntl(<LibraryGameHeader />);

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Game not found');
    expect(document.title).toBe('Game not found — MeepleAI');
  });

  it('does not render the legacy hardcoded "Gioco" literal as h1', () => {
    mockUseLibraryGameDetail.mockReturnValue({
      data: { gameTitle: 'Catan' },
      isLoading: false,
    });

    renderWithIntl(<LibraryGameHeader />);

    // Audit regression guard: the hardcoded "Gioco" generic must never appear
    // as the page h1. Use heading-scoped queryByRole to avoid matching nav
    // labels or other surrounding text.
    expect(screen.queryByRole('heading', { level: 1, name: 'Gioco' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 1, name: 'Catan' })).toBeInTheDocument();
  });
});
