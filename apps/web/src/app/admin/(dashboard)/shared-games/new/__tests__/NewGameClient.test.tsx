/**
 * NewGameClient Integration Tests
 *
 * Tests BGG search integration, form auto-fill, manual creation,
 * and successful submission with navigation.
 *
 * Pattern: Vitest + React Testing Library + renderWithQuery
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { NewGameClient } from '../client';

import type { BggFullGameData } from '@/types/bgg';

// ─── Mocks ──────────────────────────────────────────────────────────────────

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

// Capture the onSelect callback from BggSearchPanel so tests can trigger it
let capturedOnSelect: ((data: BggFullGameData) => void) | null = null;
vi.mock('@/components/admin/shared-games/BggSearchPanel', () => ({
  BggSearchPanel: ({ onSelect }: { onSelect: (data: BggFullGameData) => void }) => {
    capturedOnSelect = onSelect;
    return <div data-testid="bgg-search-panel" />;
  },
}));

// Simplify MetadataTagInput to avoid complex tag-input testing
vi.mock('@/components/admin/shared-games/MetadataTagInput', () => ({
  MetadataTagInput: ({ label, tags }: { label: string; tags: string[] }) => (
    <div data-testid={`metadata-tag-${label.toLowerCase()}`}>
      {tags.map((t: string) => (
        <span key={t}>{t}</span>
      ))}
    </div>
  ),
}));

// Mock API client
const mockCreate = vi.fn().mockResolvedValue('new-game-id');
const mockGetDistinctMetadata = vi.fn().mockResolvedValue({
  categories: [],
  mechanics: [],
  designers: [],
  publishers: [],
});
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      create: (...args: unknown[]) => mockCreate(...args),
      getDistinctMetadata: () => mockGetDistinctMetadata(),
    },
  },
}));

// ─── Test Data ──────────────────────────────────────────────────────────────

const bggGameData: BggFullGameData = {
  id: 13,
  name: 'Catan',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playingTime: 120,
  minAge: 10,
  description: 'Trade, build, settle.',
  imageUrl: 'https://example.com/catan.jpg',
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  categories: ['Negotiation', 'Economic'],
  mechanics: ['Dice Rolling', 'Trading'],
  designers: ['Klaus Teuber'],
  publishers: ['KOSMOS'],
  complexityRating: 2.32,
  averageRating: 7.14,
};

// ─── Tests ──────────────────────────────────────────────────────────────────

describe('NewGameClient with BGG integration', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    capturedOnSelect = null;
  });

  it('renders BggSearchPanel', () => {
    renderWithQuery(<NewGameClient />);
    expect(screen.getByTestId('bgg-search-panel')).toBeInTheDocument();
  });

  it('auto-fills form fields when BGG game is selected', async () => {
    renderWithQuery(<NewGameClient />);
    capturedOnSelect?.(bggGameData);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
      expect(screen.getByDisplayValue('1995')).toBeInTheDocument();
      expect(screen.getByDisplayValue('3')).toBeInTheDocument();
      expect(screen.getByDisplayValue('4')).toBeInTheDocument();
      expect(screen.getByDisplayValue('120')).toBeInTheDocument();
    });
  });

  it('shows BGG linked badge after selection', async () => {
    renderWithQuery(<NewGameClient />);
    capturedOnSelect?.(bggGameData);

    await waitFor(() => {
      expect(screen.getByText(/Collegato a ID #13/)).toBeInTheDocument();
    });
  });

  it('populates metadata tags from BGG data', async () => {
    renderWithQuery(<NewGameClient />);
    capturedOnSelect?.(bggGameData);

    await waitFor(() => {
      const categoriesTag = screen.getByTestId('metadata-tag-categories');
      expect(categoriesTag).toHaveTextContent('Negotiation');
      expect(categoriesTag).toHaveTextContent('Economic');

      const mechanicsTag = screen.getByTestId('metadata-tag-mechanics');
      expect(mechanicsTag).toHaveTextContent('Dice Rolling');
      expect(mechanicsTag).toHaveTextContent('Trading');

      const designersTag = screen.getByTestId('metadata-tag-designers');
      expect(designersTag).toHaveTextContent('Klaus Teuber');

      const publishersTag = screen.getByTestId('metadata-tag-publishers');
      expect(publishersTag).toHaveTextContent('KOSMOS');
    });
  });

  it('submits with BGG metadata fields', async () => {
    const user = userEvent.setup();
    renderWithQuery(<NewGameClient />);
    capturedOnSelect?.(bggGameData);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
    });

    // Submit the form
    const submitButton = screen.getByRole('button', { name: /create game/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(mockCreate).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Catan',
          description: 'Trade, build, settle.',
          yearPublished: 1995,
          minPlayers: 3,
          maxPlayers: 4,
          playingTimeMinutes: 120,
          bggId: 13,
          categories: ['Negotiation', 'Economic'],
          mechanics: ['Dice Rolling', 'Trading'],
          designers: ['Klaus Teuber'],
          publishers: ['KOSMOS'],
        })
      );
    });
  });

  it('allows manual creation without BGG (regression)', async () => {
    const user = userEvent.setup();
    renderWithQuery(<NewGameClient />);

    // Fill required form fields manually
    const titleInput = screen.getByLabelText(/title/i);
    await user.type(titleInput, 'My Custom Game');

    const descriptionInput = screen.getByLabelText(/description/i);
    await user.type(descriptionInput, 'A fun game');

    // Year, minPlayers, maxPlayers, playingTime already have defaults,
    // so we just need to ensure they pass validation.
    // Default values: yearPublished=current year, minPlayers=2, maxPlayers=4, playingTimeMinutes=60

    const submitButton = screen.getByRole('button', { name: /create game/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(mockCreate).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'My Custom Game',
          description: 'A fun game',
          minPlayers: 2,
          maxPlayers: 4,
          playingTimeMinutes: 60,
        })
      );
      // BGG fields should be absent/undefined
      const callArgs = mockCreate.mock.calls[0][0];
      expect(callArgs.bggId).toBeUndefined();
    });
  });

  it('navigates to game detail page after successful creation', async () => {
    const user = userEvent.setup();
    renderWithQuery(<NewGameClient />);
    capturedOnSelect?.(bggGameData);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
    });

    const submitButton = screen.getByRole('button', { name: /create game/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith('/admin/shared-games/new-game-id');
    });
  });

  it('displays error when creation fails', async () => {
    mockCreate.mockRejectedValueOnce(new Error('Network failure'));

    const user = userEvent.setup();
    renderWithQuery(<NewGameClient />);
    capturedOnSelect?.(bggGameData);

    await waitFor(() => {
      expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
    });

    const submitButton = screen.getByRole('button', { name: /create game/i });
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Network failure')).toBeInTheDocument();
    });
  });

  it('renders Create Game button', () => {
    renderWithQuery(<NewGameClient />);
    expect(screen.getByRole('button', { name: /create game/i })).toBeInTheDocument();
  });
});
