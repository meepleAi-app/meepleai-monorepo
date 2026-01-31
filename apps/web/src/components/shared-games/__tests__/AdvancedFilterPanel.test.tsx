/**
 * AdvancedFilterPanel Component Tests
 *
 * Issue #2873: Advanced Filter Panel Component
 *
 * Tests:
 * - Panel open/close with slide-in animation
 * - Filter sections rendering (Players, Complexity, Duration, Categories, Mechanics)
 * - Orange active state for selected filters
 * - Apply Filters and Reset buttons functionality
 * - Filter count badges per section
 * - Draft state management (changes only applied on "Apply")
 *
 * Note: Mocks api module directly due to singleton timing issues with MSW.
 */

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AdvancedFilterPanel, type AdvancedFilterPanelProps } from '../AdvancedFilterPanel';
import { DEFAULT_FILTERS, type SearchFilters } from '../SharedGameSearchFilters';

// Mock the api module directly (singleton timing issue with MSW)
const mockGetCategories = vi.fn();
const mockGetMechanics = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getCategories: () => mockGetCategories(),
      getMechanics: () => mockGetMechanics(),
    },
  },
}));

// Mock categories data
const mockCategories = [
  { id: 'cat-strategy', name: 'Strategy' },
  { id: 'cat-family', name: 'Family' },
  { id: 'cat-party', name: 'Party Games' },
];

// Mock mechanics data
const mockMechanics = [
  { id: 'mech-worker-placement', name: 'Worker Placement' },
  { id: 'mech-deck-building', name: 'Deck Building' },
  { id: 'mech-area-control', name: 'Area Control' },
];

describe('AdvancedFilterPanel', () => {
  const defaultFilters: SearchFilters = { ...DEFAULT_FILTERS };
  const mockOnOpenChange = vi.fn();
  const mockOnApplyFilters = vi.fn();

  const defaultProps: AdvancedFilterPanelProps = {
    open: true,
    onOpenChange: mockOnOpenChange,
    filters: defaultFilters,
    onApplyFilters: mockOnApplyFilters,
  };

  beforeEach(() => {
    vi.clearAllMocks();

    // Set up default API mocks
    mockGetCategories.mockResolvedValue(mockCategories);
    mockGetMechanics.mockResolvedValue(mockMechanics);
  });

  // ==========================================================================
  // Basic Rendering
  // ==========================================================================

  describe('Basic Rendering', () => {
    it('renders panel when open is true', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Filtri Avanzati')).toBeInTheDocument();
      });
    });

    it('does not render panel content when open is false', () => {
      render(<AdvancedFilterPanel {...defaultProps} open={false} />);

      expect(screen.queryByText('Filtri Avanzati')).not.toBeInTheDocument();
    });

    it('renders all filter sections', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Giocatori')).toBeInTheDocument();
        expect(screen.getByText(/Complessit/)).toBeInTheDocument();
        expect(screen.getByText('Durata')).toBeInTheDocument();
        expect(screen.getByText('Categorie')).toBeInTheDocument();
        expect(screen.getByText('Meccaniche')).toBeInTheDocument();
      });
    });

    it('renders Apply and Reset buttons', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Applica Filtri/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /Resetta/i })).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Apply Filters Button
  // ==========================================================================

  describe('Apply Filters Button', () => {
    it('calls onApplyFilters with current draft filters when clicked', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Applica Filtri/i })).toBeInTheDocument();
      });

      const applyButton = screen.getByRole('button', { name: /Applica Filtri/i });
      await act(async () => {
        fireEvent.click(applyButton);
      });

      expect(mockOnApplyFilters).toHaveBeenCalledWith(defaultFilters);
    });

    it('closes panel after applying filters', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Applica Filtri/i })).toBeInTheDocument();
      });

      const applyButton = screen.getByRole('button', { name: /Applica Filtri/i });
      await act(async () => {
        fireEvent.click(applyButton);
      });

      expect(mockOnOpenChange).toHaveBeenCalledWith(false);
    });

    it('has orange background styling', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        const applyButton = screen.getByRole('button', { name: /Applica Filtri/i });
        expect(applyButton).toHaveClass('bg-orange-500');
      });
    });
  });

  // ==========================================================================
  // Reset Button
  // ==========================================================================

  describe('Reset Button', () => {
    it('resets draft filters to defaults when clicked', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
        minPlayers: 2,
        maxPlayers: 4,
      };

      render(<AdvancedFilterPanel {...defaultProps} filters={activeFilters} />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Resetta/i })).toBeInTheDocument();
      });

      const resetButton = screen.getByRole('button', { name: /Resetta/i });
      await act(async () => {
        fireEvent.click(resetButton);
      });

      // Now click Apply to verify the reset was applied
      const applyButton = screen.getByRole('button', { name: /Applica Filtri/i });
      await act(async () => {
        fireEvent.click(applyButton);
      });

      expect(mockOnApplyFilters).toHaveBeenCalledWith(DEFAULT_FILTERS);
    });
  });

  // ==========================================================================
  // Active Filters Count
  // ==========================================================================

  describe('Active Filters Count', () => {
    it('shows total active count badge in header', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
        minPlayers: 2,
        minComplexity: 2.0,
      };

      render(<AdvancedFilterPanel {...defaultProps} filters={activeFilters} />);

      await waitFor(() => {
        // Should show "3 attivi" badge (categories + players + complexity)
        expect(screen.getByText(/3 attivi/i)).toBeInTheDocument();
      });
    });

    it('does not show active count when no filters', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Filtri Avanzati')).toBeInTheDocument();
      });

      // Should not show "attivi" badge
      expect(screen.queryByText(/attivi/i)).not.toBeInTheDocument();
    });
  });

  // ==========================================================================
  // Section Count Badges
  // ==========================================================================

  describe('Section Count Badges', () => {
    it('shows count badge on Players section when players filter is active', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        minPlayers: 2,
      };

      render(<AdvancedFilterPanel {...defaultProps} filters={activeFilters} />);

      await waitFor(() => {
        // Find the Giocatori section button and check for badge
        const playerSection = screen.getByRole('button', { name: /Giocatori/i });
        expect(playerSection.querySelector('.bg-orange-500')).toBeInTheDocument();
      });
    });

    it('shows count badge on Categories section with number of selected categories', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy', 'cat-family'],
      };

      render(<AdvancedFilterPanel {...defaultProps} filters={activeFilters} />);

      await waitFor(() => {
        // Should show badge with "2" for categories
        expect(screen.getByText('2')).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Category Selection
  // ==========================================================================

  describe('Category Selection', () => {
    it('loads and displays categories from API', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      // Expand categories section by clicking on it
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Categorie/i })).toBeInTheDocument();
      });

      const categoriesButton = screen.getByRole('button', { name: /Categorie/i });
      await act(async () => {
        fireEvent.click(categoriesButton);
      });

      await waitFor(() => {
        expect(screen.getByText('Strategy')).toBeInTheDocument();
        expect(screen.getByText('Family')).toBeInTheDocument();
        expect(screen.getByText('Party Games')).toBeInTheDocument();
      });
    });

    it('shows orange styling when category is selected', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
      };

      render(<AdvancedFilterPanel {...defaultProps} filters={activeFilters} />);

      // Expand categories section
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Categorie/i })).toBeInTheDocument();
      });

      const categoriesButton = screen.getByRole('button', { name: /Categorie/i });
      await act(async () => {
        fireEvent.click(categoriesButton);
      });

      await waitFor(() => {
        // The label for Strategy should have orange background
        const strategyLabel = screen.getByText('Strategy').closest('label');
        expect(strategyLabel).toHaveClass('bg-orange-500/10');
      });
    });
  });

  // ==========================================================================
  // Mechanics Selection
  // ==========================================================================

  describe('Mechanics Selection', () => {
    it('loads and displays mechanics from API', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      // Expand mechanics section (it's collapsed by default)
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /Meccaniche/i })).toBeInTheDocument();
      });

      const mechanicsButton = screen.getByRole('button', { name: /Meccaniche/i });
      await act(async () => {
        fireEvent.click(mechanicsButton);
      });

      await waitFor(() => {
        expect(screen.getByText('Worker Placement')).toBeInTheDocument();
        expect(screen.getByText('Deck Building')).toBeInTheDocument();
        expect(screen.getByText('Area Control')).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Players Range Slider
  // ==========================================================================

  describe('Players Range Slider', () => {
    it('renders players range slider', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Numero giocatori')).toBeInTheDocument();
      });
    });

    it('displays min and max values', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        minPlayers: 3,
        maxPlayers: 6,
      };

      render(<AdvancedFilterPanel {...defaultProps} filters={activeFilters} />);

      await waitFor(() => {
        expect(screen.getByText(/Min: 3/)).toBeInTheDocument();
        expect(screen.getByText(/Max: 6/)).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Complexity Range Slider
  // ==========================================================================

  describe('Complexity Range Slider', () => {
    it('renders complexity range slider', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Complessit/)).toBeInTheDocument();
      });
    });

    it('displays min and max complexity values', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        minComplexity: 2.0,
        maxComplexity: 4.0,
      };

      render(<AdvancedFilterPanel {...defaultProps} filters={activeFilters} />);

      await waitFor(() => {
        expect(screen.getByText(/Semplice: 2.0/)).toBeInTheDocument();
        expect(screen.getByText(/Complesso: 4.0/)).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Duration Range Slider
  // ==========================================================================

  describe('Duration Range Slider', () => {
    it('renders duration range slider', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Durata del gioco')).toBeInTheDocument();
      });
    });

    it('displays min and max duration values', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        minPlayingTime: 30,
        maxPlayingTime: 120,
      };

      render(<AdvancedFilterPanel {...defaultProps} filters={activeFilters} />);

      await waitFor(() => {
        expect(screen.getByText(/Min: 30 min/)).toBeInTheDocument();
        expect(screen.getByText(/Max: 120 min/)).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Catalog Only Toggle
  // ==========================================================================

  describe('Catalog Only Toggle', () => {
    it('renders catalog only checkbox', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByText('Solo dal catalogo')).toBeInTheDocument();
      });
    });

    it('shows orange styling when catalog only is enabled', async () => {
      const activeFilters: SearchFilters = {
        ...defaultFilters,
        catalogOnly: true,
      };

      render(<AdvancedFilterPanel {...defaultProps} filters={activeFilters} />);

      await waitFor(() => {
        const catalogLabel = screen.getByText('Solo dal catalogo').closest('label');
        expect(catalogLabel).toHaveClass('bg-orange-500/10');
      });
    });
  });

  // ==========================================================================
  // Draft State Management
  // ==========================================================================

  describe('Draft State Management', () => {
    it('syncs draft filters when panel opens', async () => {
      const { rerender } = render(<AdvancedFilterPanel {...defaultProps} open={false} />);

      // Initially closed
      expect(screen.queryByText('Filtri Avanzati')).not.toBeInTheDocument();

      // Open with new filters
      const newFilters: SearchFilters = {
        ...defaultFilters,
        categoryIds: ['cat-strategy'],
      };

      rerender(<AdvancedFilterPanel {...defaultProps} open={true} filters={newFilters} />);

      await waitFor(() => {
        expect(screen.getByText('Filtri Avanzati')).toBeInTheDocument();
        // Badge should show 1 active filter
        expect(screen.getByText(/1 attivi/i)).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Loading State
  // ==========================================================================

  describe('Loading State', () => {
    it('shows loading message while fetching categories and mechanics', async () => {
      // Make the API call take longer
      mockGetCategories.mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve(mockCategories), 100))
      );
      mockGetMechanics.mockImplementation(
        () => new Promise(resolve => setTimeout(() => resolve(mockMechanics), 100))
      );

      render(<AdvancedFilterPanel {...defaultProps} />);

      // Should show loading initially
      expect(screen.getByText('Caricamento filtri...')).toBeInTheDocument();

      // Wait for content to load
      await waitFor(() => {
        expect(screen.queryByText('Caricamento filtri...')).not.toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Accessibility
  // ==========================================================================

  describe('Accessibility', () => {
    it('has accessible slider labels', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        expect(screen.getByLabelText('Numero giocatori')).toBeInTheDocument();
        expect(screen.getByLabelText(/Complessit/)).toBeInTheDocument();
        expect(screen.getByLabelText('Durata del gioco')).toBeInTheDocument();
      });
    });

    it('panel has proper dialog role', async () => {
      render(<AdvancedFilterPanel {...defaultProps} />);

      await waitFor(() => {
        // Sheet uses dialog role internally
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
    });
  });
});
