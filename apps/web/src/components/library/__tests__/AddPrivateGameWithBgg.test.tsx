/**
 * AddPrivateGameWithBgg Component Tests
 * Issue #4053: User-Facing BGG Search for Private Game Creation
 */

import { screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { AddPrivateGameWithBgg } from '../AddPrivateGameWithBgg';

// Mock BggGameSearch
vi.mock('../BggGameSearch', () => ({
  BggGameSearch: ({ onSelect }: { onSelect: (result: unknown) => void }) => (
    <div data-testid="bgg-game-search">
      <button
        data-testid="mock-bgg-select"
        onClick={() =>
          onSelect({
            bggId: 13,
            name: 'Catan',
            yearPublished: 1995,
            thumbnailUrl: 'https://example.com/catan-thumb.jpg',
            type: 'boardgame',
          })
        }
      >
        Select Catan
      </button>
    </div>
  ),
}));

// Mock AddPrivateGameForm
vi.mock('../AddPrivateGameForm', () => ({
  AddPrivateGameForm: ({
    onSubmit,
    onCancel,
    initialValues,
    submitLabel,
  }: {
    onSubmit: (data: Record<string, unknown>) => Promise<void>;
    onCancel: () => void;
    isSubmitting?: boolean;
    initialValues?: Record<string, unknown>;
    submitLabel?: string;
  }) => (
    <div data-testid="add-private-game-form">
      <div data-testid="form-initial-values">{JSON.stringify(initialValues ?? {})}</div>
      <div data-testid="form-submit-label">{submitLabel}</div>
      <button
        data-testid="mock-form-submit"
        onClick={() =>
          onSubmit({
            title: initialValues?.title ?? 'Manual Game',
            minPlayers: initialValues?.minPlayers ?? 1,
            maxPlayers: initialValues?.maxPlayers ?? 4,
          })
        }
      >
        Submit
      </button>
      <button data-testid="mock-form-cancel" onClick={onCancel}>
        Cancel
      </button>
    </div>
  ),
}));

// Mock API
const mockGetGameDetails = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      getGameDetails: (...args: unknown[]) => mockGetGameDetails(...args),
    },
  },
}));

// Mock sonner
vi.mock('sonner', () => ({
  toast: {
    error: vi.fn(),
    info: vi.fn(),
  },
}));

const mockBggDetails = {
  bggId: 13,
  name: 'Catan',
  description: 'Trade, build, and settle the island of Catan.',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playingTime: 90,
  minPlayTime: 60,
  maxPlayTime: 120,
  minAge: 10,
  averageRating: 7.1,
  bayesAverageRating: 7.0,
  usersRated: 100000,
  averageWeight: 2.3,
  thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  imageUrl: 'https://example.com/catan.jpg',
  categories: ['Negotiation'],
  mechanics: ['Dice Rolling'],
  designers: ['Klaus Teuber'],
  publishers: ['KOSMOS'],
};

describe('AddPrivateGameWithBgg', () => {
  const defaultProps = {
    onSubmit: vi.fn(),
    onCancel: vi.fn(),
    isSubmitting: false,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetGameDetails.mockReset();
  });

  // ===== Initial State (Choose Mode) =====

  it('renders choose mode initially', () => {
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    expect(screen.getByTestId('add-game-choose-mode')).toBeInTheDocument();
    expect(screen.getByTestId('bgg-game-search')).toBeInTheDocument();
    expect(screen.getByTestId('manual-entry-btn')).toBeInTheDocument();
  });

  it('shows description text in choose mode', () => {
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    expect(
      screen.getByText(/Cerca il gioco su BoardGameGeek/i)
    ).toBeInTheDocument();
  });

  it('renders cancel button in choose mode', () => {
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    const cancelBtn = screen.getByText('Cancel');
    fireEvent.click(cancelBtn);
    expect(defaultProps.onCancel).toHaveBeenCalled();
  });

  // ===== Manual Entry Flow =====

  it('switches to manual form when manual entry button is clicked', () => {
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('manual-entry-btn'));

    expect(screen.getByTestId('add-game-manual-form')).toBeInTheDocument();
    expect(screen.getByTestId('add-private-game-form')).toBeInTheDocument();
  });

  it('shows "Manuale" badge in manual mode', () => {
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('manual-entry-btn'));

    expect(screen.getByText('Manuale')).toBeInTheDocument();
  });

  it('submits with Manual source in manual mode', async () => {
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('manual-entry-btn'));
    fireEvent.click(screen.getByTestId('mock-form-submit'));

    await waitFor(() => {
      expect(defaultProps.onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'Manual Game' }),
        'Manual'
      );
    });
  });

  it('shows submit label "Add Private Game" in manual mode', () => {
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('manual-entry-btn'));

    expect(screen.getByTestId('form-submit-label')).toHaveTextContent('Add Private Game');
  });

  // ===== BGG Search Flow =====

  it('shows loading state while fetching BGG details', async () => {
    mockGetGameDetails.mockImplementation(() => new Promise(() => {})); // Never resolves
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      expect(screen.getByTestId('loading-bgg-details')).toBeInTheDocument();
    });
  });

  it('transitions to BGG form after loading details', async () => {
    mockGetGameDetails.mockResolvedValue(mockBggDetails);
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      expect(screen.getByTestId('add-game-bgg-form')).toBeInTheDocument();
    });
  });

  it('shows "da BGG" badge in BGG mode', async () => {
    mockGetGameDetails.mockResolvedValue(mockBggDetails);
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      expect(screen.getByText('da BGG')).toBeInTheDocument();
    });
  });

  it('pre-populates form with BGG data', async () => {
    mockGetGameDetails.mockResolvedValue(mockBggDetails);
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      const initialValues = screen.getByTestId('form-initial-values');
      const parsed = JSON.parse(initialValues.textContent || '{}');
      expect(parsed.title).toBe('Catan');
      expect(parsed.minPlayers).toBe(3);
      expect(parsed.maxPlayers).toBe(4);
      expect(parsed.yearPublished).toBe(1995);
      expect(parsed.playingTimeMinutes).toBe(90);
      expect(parsed.minAge).toBe(10);
      expect(parsed.imageUrl).toBe('https://example.com/catan.jpg');
    });
  });

  it('maps averageWeight to complexityRating rounded to 1 decimal', async () => {
    mockGetGameDetails.mockResolvedValue(mockBggDetails);
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      const initialValues = screen.getByTestId('form-initial-values');
      const parsed = JSON.parse(initialValues.textContent || '{}');
      expect(parsed.complexityRating).toBe(2.3);
    });
  });

  it('submits with Bgg source and bggId', async () => {
    mockGetGameDetails.mockResolvedValue(mockBggDetails);
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      expect(screen.getByTestId('add-game-bgg-form')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('mock-form-submit'));

    await waitFor(() => {
      expect(defaultProps.onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'Catan' }),
        'BoardGameGeek',
        13,
        'https://example.com/catan-thumb.jpg'
      );
    });
  });

  it('shows submit label "Add from BGG" in BGG mode', async () => {
    mockGetGameDetails.mockResolvedValue(mockBggDetails);
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      expect(screen.getByTestId('form-submit-label')).toHaveTextContent('Add from BGG');
    });
  });

  // ===== Back Navigation =====

  it('goes back to choose mode from manual form', () => {
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('manual-entry-btn'));
    expect(screen.getByTestId('add-game-manual-form')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('back-to-search-btn'));
    expect(screen.getByTestId('add-game-choose-mode')).toBeInTheDocument();
  });

  it('goes back to choose mode from BGG form', async () => {
    mockGetGameDetails.mockResolvedValue(mockBggDetails);
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      expect(screen.getByTestId('add-game-bgg-form')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('back-to-search-btn'));
    expect(screen.getByTestId('add-game-choose-mode')).toBeInTheDocument();
  });

  // ===== Error Handling =====

  it('returns to choose mode on BGG details fetch failure', async () => {
    mockGetGameDetails.mockRejectedValue(new Error('API error'));
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      expect(screen.getByTestId('add-game-choose-mode')).toBeInTheDocument();
    });
  });

  // ===== BGG Data Mapping Edge Cases =====

  it('handles BGG details with null fields gracefully', async () => {
    mockGetGameDetails.mockResolvedValue({
      ...mockBggDetails,
      minPlayers: null,
      maxPlayers: null,
      playingTime: null,
      minAge: null,
      averageWeight: null,
      description: null,
      imageUrl: null,
    });
    renderWithQuery(<AddPrivateGameWithBgg {...defaultProps} />);

    fireEvent.click(screen.getByTestId('mock-bgg-select'));

    await waitFor(() => {
      const initialValues = screen.getByTestId('form-initial-values');
      const parsed = JSON.parse(initialValues.textContent || '{}');
      expect(parsed.title).toBe('Catan');
      expect(parsed.minPlayers).toBe(1); // fallback
      expect(parsed.maxPlayers).toBe(4); // fallback
    });
  });
});
