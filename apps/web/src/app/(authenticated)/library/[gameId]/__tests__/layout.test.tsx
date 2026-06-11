/**
 * Library Game Detail Layout — #1816 P2-2 + #2158 regression
 *
 * #1816 P2-2 introduced 3-state document.title resolution:
 *   - loading: document.title "Loading game — MeepleAI"
 *   - loaded:  document.title "Catan · MeepleAI"
 *   - 404:     document.title "Game not found — MeepleAI"
 *
 * #2158 (Fix #2 codemod + visual-smoke follow-up) retired the legacy
 * `PageHeader` (h1 + tabs + primaryAction). The visual smoke pass also
 * surfaced that `GameDetailDesktop` already owns BOTH the title hero AND a
 * 5-tab nav, so the post-codemod MiniNavSlot strip would have shown a
 * duplicate (4-tab) navigation. The layout therefore neither renders an h1
 * nor registers a MiniNavSlot config — it only manages `document.title`.
 *
 * Test scope: the inner `LibraryGameHeader` (exported for this purpose).
 */

import { useParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

import { renderWithIntl } from '../../../../../__tests__/fixtures/common-fixtures';

import { LibraryGameHeader } from '../layout';

// ── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
}));

const mockUseLibraryGameDetail = vi.fn();
vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryGameDetail: (...args: unknown[]) => mockUseLibraryGameDetail(...args),
}));

const useMiniNavConfigMock = vi.fn();
vi.mock('@/hooks/useMiniNavConfig', () => ({
  useMiniNavConfig: (cfg: unknown) => useMiniNavConfigMock(cfg),
}));

beforeEach(() => {
  vi.clearAllMocks();
  (useParams as Mock).mockReturnValue({ gameId: 'cc1678e8-f460-4b53-81f6-6d6539f82b65' });
});

describe('LibraryGameHeader — document.title 3-state machine', () => {
  it('renders null and sets the loading document.title while the query is pending', () => {
    mockUseLibraryGameDetail.mockReturnValue({ data: undefined, isLoading: true });

    const { container } = renderWithIntl(<LibraryGameHeader />);

    expect(container.firstChild).toBeNull();
    expect(document.title).toBe('Loading game — MeepleAI');
  });

  it('uses the game title in document.title once data resolves', () => {
    mockUseLibraryGameDetail.mockReturnValue({
      data: { gameTitle: 'Catan' },
      isLoading: false,
    });

    renderWithIntl(<LibraryGameHeader />);

    expect(document.title).toBe('Catan · MeepleAI');
  });

  it('falls back to the 404 document.title when the query resolves to null', () => {
    mockUseLibraryGameDetail.mockReturnValue({ data: null, isLoading: false });

    renderWithIntl(<LibraryGameHeader />);

    expect(document.title).toBe('Game not found — MeepleAI');
  });

  it('uses the catalog-fallback game title (F4.1 #1974) for document.title', () => {
    // F4.1 regression guard: when a user opens /library/[gameId] for a
    // SharedGame that is NOT in their personal library, `useLibraryGameDetail`
    // falls back to the shared-catalog payload (libraryEntryId === ''). The
    // browser tab title must still surface the catalog game name.
    mockUseLibraryGameDetail.mockReturnValue({
      data: {
        libraryEntryId: '', // catalog fallback sentinel
        gameId: 'shared-catan-uuid',
        gameTitle: 'Catan',
        userId: '',
      },
      isLoading: false,
    });

    renderWithIntl(<LibraryGameHeader />);

    expect(document.title).toBe('Catan · MeepleAI');
  });

  it('does NOT register a mini-nav config (#2158 visual-smoke follow-up)', () => {
    // The MiniNavSlot strip from the initial codemod was suppressed after
    // visual smoke showed it duplicated the 5-tab nav owned by
    // `GameDetailDesktop`. The layout no longer consumes MiniNavSlot.
    mockUseLibraryGameDetail.mockReturnValue({
      data: { gameTitle: 'Catan' },
      isLoading: false,
    });

    renderWithIntl(<LibraryGameHeader />);

    expect(useMiniNavConfigMock).not.toHaveBeenCalled();
  });
});
