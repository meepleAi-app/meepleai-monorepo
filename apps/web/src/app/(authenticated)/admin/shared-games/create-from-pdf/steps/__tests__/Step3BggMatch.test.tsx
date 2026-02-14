/**
 * Step3BggMatch Tests - Issue #4141
 */

import { screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { Step3BggMatch } from '../Step3BggMatch';

// Mock SWR
vi.mock('swr', () => ({
  default: vi.fn((key, fetcher) => {
    if (!key) return { data: null, isLoading: false };

    if (key[0] === 'bgg-search') {
      return {
        data: [
          { id: 13, name: 'Catan', yearPublished: 1995, thumbnail: 'https://example.com/catan.jpg' },
          { id: 822, name: 'Carcassonne', yearPublished: 2000, thumbnail: null },
        ],
        isLoading: false,
      };
    }

    if (key[0] === 'bgg-details') {
      return {
        data: {
          id: 13,
          name: 'Catan',
          yearPublished: 1995,
          minPlayers: 3,
          maxPlayers: 4,
          playingTime: 90,
          minAge: 10,
          rating: 7.2,
          thumbnail: 'https://example.com/catan.jpg',
        },
        isLoading: false,
      };
    }

    return { data: null, isLoading: false };
  }),
}));

vi.mock('@/lib/stores/pdf-wizard-store', () => ({
  usePdfWizardStore: vi.fn((selector) => {
    const mockStore = {
      extractedTitle: 'Catan',
      bggDetails: null,
      setStep3Data: vi.fn(),
    };
    return selector ? selector(mockStore) : mockStore;
  }),
}));

describe('Step3BggMatch', () => {
  const mockOnNext = vi.fn();
  const mockOnBack = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render tabs for search and manual ID', () => {
    renderWithQuery(<Step3BggMatch onNext={mockOnNext} onBack={mockOnBack} />);

    expect(screen.getByText('Search by Title')).toBeInTheDocument();
    expect(screen.getByText('Enter BGG ID')).toBeInTheDocument();
  });

  it('should display search results', async () => {
    renderWithQuery(<Step3BggMatch onNext={mockOnNext} onBack={mockOnBack} />);

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByText('Carcassonne')).toBeInTheDocument();
    });
  });

  // TODO: Add test for tab switching with proper Tabs mock

  it('should have skip BGG button', () => {
    renderWithQuery(<Step3BggMatch onNext={mockOnNext} onBack={mockOnBack} />);

    expect(screen.getByRole('button', { name: /Skip BGG/i })).toBeInTheDocument();
  });

  it('should call onBack when back button clicked', () => {
    renderWithQuery(<Step3BggMatch onNext={mockOnNext} onBack={mockOnBack} />);

    const backButton = screen.getByRole('button', { name: /back/i });
    fireEvent.click(backButton);

    expect(mockOnBack).toHaveBeenCalledTimes(1);
  });

  it('should disable Use BGG Data button when no game selected', () => {
    renderWithQuery(<Step3BggMatch onNext={mockOnNext} onBack={mockOnBack} />);

    const useButton = screen.getByRole('button', { name: /Use BGG Data/i });
    expect(useButton).toBeDisabled();
  });
});
