/**
 * GamebookUploadClient — unit tests (G7 wizard + G8 checklist)
 *
 * Covers:
 *   1. Step 1 renders game picker when no gameId in URL
 *   2. Skips to upload step when ?gameId= is present
 *   3. Shows selected game title in upload step
 *   4. Back button returns to picker
 *   5. Game picker loads games via useQuery
 *   6. Game picker shows error state
 *   7. Search input is rendered in picker
 */

import { fireEvent, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { t } from '@/test-utils/test-i18n';

import { GamebookUploadClient } from '../_components/GamebookUploadClient';

// ── Mocks ─────────────────────────────────────────────────────────────────────

// Mock shared-games API
const mockSearchSharedGames = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/shared-games', () => ({
  searchSharedGames: mockSearchSharedGames,
}));

// Mock next/navigation
const mockSearchParamsGet = vi.hoisted(() => vi.fn());
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => ({ get: mockSearchParamsGet }),
}));

// Mock PhotoUploader to avoid deep dependency chain in wizard tests
vi.mock('../_components/PhotoUploader', () => ({
  PhotoUploader: ({ gameId }: { gameId: string }) => (
    <div data-testid="mock-photo-uploader" data-game-id={gameId} />
  ),
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

const GAME_ID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const GAME_TITLE = 'Tainted Grail';

function makePagedGames(overrides?: Partial<{ id: string; title: string }>[]) {
  const items = (overrides ?? [{ id: GAME_ID, title: GAME_TITLE }]).map(o => ({
    id: o.id ?? GAME_ID,
    title: o.title ?? GAME_TITLE,
    bggId: null,
    yearPublished: 2019,
    description: 'A dark RPG board game',
    minPlayers: 1,
    maxPlayers: 4,
    playingTimeMinutes: 120,
    minAge: 14,
    complexityRating: null,
    averageRating: 8.5,
    imageUrl: '',
    thumbnailUrl: '',
    status: 'Published',
    createdAt: '2026-01-01T00:00:00Z',
    modifiedAt: null,
    isRagPublic: false,
    hasKnowledgeBase: false,
    toolkitsCount: 0,
    agentsCount: 0,
    kbsCount: 0,
    newThisWeekCount: 0,
    contributorsCount: 0,
    isTopRated: false,
    isNew: false,
  }));

  return { items, total: items.length, page: 1, pageSize: 20 };
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('GamebookUploadClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockSearchParamsGet.mockReturnValue(null);
    mockSearchSharedGames.mockResolvedValue(makePagedGames());
  });

  it('shows game picker (step 1) when no gameId in URL', async () => {
    renderWithQuery(<GamebookUploadClient />);

    await waitFor(() => {
      expect(screen.getByTestId('gamebook-wizard-pick')).toBeInTheDocument();
    });
    expect(screen.getByTestId('game-picker')).toBeInTheDocument();
  });

  it('shows search input in game picker', async () => {
    renderWithQuery(<GamebookUploadClient />);

    await waitFor(() => {
      expect(screen.getByTestId('game-search-input')).toBeInTheDocument();
    });
  });

  it('skips to upload step when ?gameId= is present in URL', async () => {
    mockSearchParamsGet.mockReturnValue(GAME_ID);

    renderWithQuery(<GamebookUploadClient />);

    await waitFor(() => {
      expect(screen.getByTestId('gamebook-wizard-upload')).toBeInTheDocument();
    });
    expect(screen.getByTestId('mock-photo-uploader')).toHaveAttribute('data-game-id', GAME_ID);
    // Picker should NOT be visible
    expect(screen.queryByTestId('gamebook-wizard-pick')).not.toBeInTheDocument();
  });

  it('loads and displays games from the API', async () => {
    renderWithQuery(<GamebookUploadClient />);

    await waitFor(() => {
      expect(screen.getByTestId(`game-option-${GAME_ID}`)).toBeInTheDocument();
    });
    expect(screen.getByText(GAME_TITLE)).toBeInTheDocument();
  });

  it('transitions to upload step after selecting a game', async () => {
    renderWithQuery(<GamebookUploadClient />);

    // Wait for game to appear
    await waitFor(() => {
      expect(screen.getByTestId(`game-option-${GAME_ID}`)).toBeInTheDocument();
    });

    // Click the game
    fireEvent.click(screen.getByTestId(`game-option-${GAME_ID}`));

    await waitFor(() => {
      expect(screen.getByTestId('gamebook-wizard-upload')).toBeInTheDocument();
    });
    // PhotoUploader rendered with correct gameId
    expect(screen.getByTestId('mock-photo-uploader')).toHaveAttribute('data-game-id', GAME_ID);
  });

  it('shows selected game title in upload step', async () => {
    renderWithQuery(<GamebookUploadClient />);

    await waitFor(() => {
      expect(screen.getByTestId(`game-option-${GAME_ID}`)).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId(`game-option-${GAME_ID}`));

    await waitFor(() => {
      expect(screen.getByTestId('selected-game-title')).toHaveTextContent(GAME_TITLE);
    });
  });

  it('back button returns to picker', async () => {
    renderWithQuery(<GamebookUploadClient />);

    await waitFor(() => {
      expect(screen.getByTestId(`game-option-${GAME_ID}`)).toBeInTheDocument();
    });
    fireEvent.click(screen.getByTestId(`game-option-${GAME_ID}`));

    await waitFor(() => {
      expect(screen.getByTestId('gamebook-wizard-upload')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('back-to-picker'));

    await waitFor(() => {
      expect(screen.getByTestId('gamebook-wizard-pick')).toBeInTheDocument();
    });
  });

  it('shows error state when API fails', async () => {
    mockSearchSharedGames.mockRejectedValue(new Error('Network error'));

    renderWithQuery(<GamebookUploadClient />);

    await waitFor(() => {
      expect(screen.getByTestId('picker-error')).toBeInTheDocument();
    });
  });

  it('shows empty state when no games returned', async () => {
    mockSearchSharedGames.mockResolvedValue(makePagedGames([]));

    renderWithQuery(<GamebookUploadClient />);

    await waitFor(() => {
      expect(screen.getByTestId('picker-empty')).toBeInTheDocument();
    });
  });
});
