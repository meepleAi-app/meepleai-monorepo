/**
 * Library Game Detail Layout — #1816 P2-2 + #2158 regression
 *
 * #1816 P2-2 introduced 3-state document.title resolution:
 *   - loading: document.title "Loading game — MeepleAI"
 *   - loaded:  document.title "Catan · MeepleAI"
 *   - 404:     document.title "Game not found — MeepleAI"
 *
 * #2158 (Fix #2 codemod) migrated the legacy `PageHeader` (h1 + tabs +
 * primaryAction) to `useMiniNavConfig` (breadcrumb + tabs). The game title is
 * now rendered as a big hero by `GameDetailDesktop`, so the layout no longer
 * surfaces an h1 element — the breadcrumb crumb keeps the 3-state resolution.
 *
 * Test scope: the inner `LibraryGameHeader` (exported for this purpose).
 */

import { useParams, useSearchParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';

import { renderWithIntl } from '../../../../../__tests__/fixtures/common-fixtures';

import { LibraryGameHeader } from '../layout';

// ── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
  useSearchParams: vi.fn(),
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
  (useSearchParams as Mock).mockReturnValue(new URLSearchParams(''));
});

describe('LibraryGameHeader — document.title + breadcrumb 3-state machine', () => {
  it('renders null and sets the loading document.title while the query is pending', () => {
    mockUseLibraryGameDetail.mockReturnValue({ data: undefined, isLoading: true });

    const { container } = renderWithIntl(<LibraryGameHeader />);

    expect(container.firstChild).toBeNull();
    expect(document.title).toBe('Loading game — MeepleAI');
    expect(useMiniNavConfigMock).toHaveBeenCalledWith(
      expect.objectContaining({
        breadcrumb: 'Libreria · Loading game…',
      })
    );
  });

  it('uses the game title in breadcrumb + document.title once data resolves', () => {
    mockUseLibraryGameDetail.mockReturnValue({
      data: { gameTitle: 'Catan' },
      isLoading: false,
    });

    renderWithIntl(<LibraryGameHeader />);

    expect(document.title).toBe('Catan · MeepleAI');
    expect(useMiniNavConfigMock).toHaveBeenCalledWith(
      expect.objectContaining({
        breadcrumb: 'Libreria · Catan',
      })
    );
  });

  it('falls back to the 404 label when the query resolves to null', () => {
    mockUseLibraryGameDetail.mockReturnValue({ data: null, isLoading: false });

    renderWithIntl(<LibraryGameHeader />);

    expect(document.title).toBe('Game not found — MeepleAI');
    expect(useMiniNavConfigMock).toHaveBeenCalledWith(
      expect.objectContaining({
        breadcrumb: 'Libreria · Game not found',
      })
    );
  });

  it('registers the 4 contextual tabs (Dettagli · Agente · Toolkit · FAQ)', () => {
    mockUseLibraryGameDetail.mockReturnValue({
      data: { gameTitle: 'Catan' },
      isLoading: false,
    });

    renderWithIntl(<LibraryGameHeader />);

    const cfg = useMiniNavConfigMock.mock.calls.at(-1)?.[0] as {
      tabs: ReadonlyArray<{ id: string; href: string }>;
      activeTabId: string;
    };
    expect(cfg.tabs).toHaveLength(4);
    expect(cfg.tabs.map(t => t.id)).toEqual(['details', 'agent', 'toolkit', 'faq']);
    expect(cfg.activeTabId).toBe('details');
  });

  it('switches activeTabId from URL ?tab= search param', () => {
    mockUseLibraryGameDetail.mockReturnValue({
      data: { gameTitle: 'Catan' },
      isLoading: false,
    });
    (useSearchParams as Mock).mockReturnValue(new URLSearchParams('tab=agent'));

    renderWithIntl(<LibraryGameHeader />);

    const cfg = useMiniNavConfigMock.mock.calls.at(-1)?.[0] as { activeTabId: string };
    expect(cfg.activeTabId).toBe('agent');
  });

  it('renders the catalog-fallback game title (F4.1 #1974) into the breadcrumb', () => {
    // F4.1 regression guard: when a user opens /library/[gameId] for a
    // SharedGame that is NOT in their personal library, `useLibraryGameDetail`
    // falls back to the shared-catalog payload (libraryEntryId === ''). The
    // breadcrumb must still surface the catalog game name — NOT the legacy
    // "Gioco" generic OR "Gioco non trovato".
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

    expect(useMiniNavConfigMock).toHaveBeenCalledWith(
      expect.objectContaining({ breadcrumb: 'Libreria · Catan' })
    );
    expect(document.title).toBe('Catan · MeepleAI');
  });
});
