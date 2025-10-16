// UI-04: TimelineFilters component comprehensive unit tests
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TimelineFilters } from '../TimelineFilters';
import { TimelineFilters as TimelineFiltersType, DEFAULT_FILTERS } from '@/lib/timeline-types';

describe('TimelineFilters Component', () => {
  const mockOnFiltersChange = jest.fn();
  const mockOnToggleCollapse = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Collapse/Expand State', () => {
    it('renders full filters sidebar when not collapsed', () => {
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      expect(screen.getByText('Filtri Timeline')).toBeInTheDocument();
    });

    it('renders collapsed state with minimal UI', () => {
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={true}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      expect(screen.queryByText('Filtri Timeline')).not.toBeInTheDocument();
      expect(screen.getByTitle('Mostra filtri')).toBeInTheDocument();
    });

    it('calls onToggleCollapse when collapse button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const collapseButton = screen.getByTitle('Nascondi filtri');
      await user.click(collapseButton);

      expect(mockOnToggleCollapse).toHaveBeenCalledTimes(1);
    });

    it('calls onToggleCollapse when expand button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={true}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const expandButton = screen.getByTitle('Mostra filtri');
      await user.click(expandButton);

      expect(mockOnToggleCollapse).toHaveBeenCalledTimes(1);
    });
  });

  describe('Search Filter', () => {
    it('renders search input', () => {
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      expect(screen.getByPlaceholderText(/Cerca nei messaggi, citazioni.../i)).toBeInTheDocument();
    });

    it('displays current search text value', () => {
      const filtersWithSearch: TimelineFiltersType = {
        ...DEFAULT_FILTERS,
        searchText: 'test search'
      };

      render(
        <TimelineFilters
          filters={filtersWithSearch}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const searchInput = screen.getByPlaceholderText(/Cerca nei messaggi, citazioni.../i) as HTMLInputElement;
      expect(searchInput.value).toBe('test search');
    });

    it('calls onFiltersChange when search text changes', async () => {
      const user = userEvent.setup();
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const searchInput = screen.getByPlaceholderText(/Cerca nei messaggi, citazioni.../i);
      await user.type(searchInput, 'new search');

      expect(mockOnFiltersChange).toHaveBeenCalled();
    });

    it('clears search text when input is emptied', async () => {
      const user = userEvent.setup();
      const filtersWithSearch: TimelineFiltersType = {
        ...DEFAULT_FILTERS,
        searchText: 'existing search'
      };

      render(
        <TimelineFilters
          filters={filtersWithSearch}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const searchInput = screen.getByPlaceholderText(/Cerca nei messaggi, citazioni.../i);
      await user.clear(searchInput);

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({ searchText: undefined })
      );
    });
  });

  describe('Event Type Filters', () => {
    it('renders all event type checkboxes', () => {
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      expect(screen.getByText('Messaggio')).toBeInTheDocument();
      expect(screen.getByText('Ricerca RAG')).toBeInTheDocument();
      expect(screen.getByText('Recupero Citazioni')).toBeInTheDocument();
      expect(screen.getByText('Generazione Risposta')).toBeInTheDocument();
      // "Completato" appears in both event types and statuses, so use getAllByText
      expect(screen.getAllByText('Completato').length).toBeGreaterThan(0);
      // "Errore" appears in both event types and statuses, so use getAllByText
      expect(screen.getAllByText('Errore').length).toBeGreaterThan(0);
    });

    it('checks event types that are in the filter', () => {
      const filters: TimelineFiltersType = {
        eventTypes: new Set(['message', 'error']),
        statuses: DEFAULT_FILTERS.statuses
      };

      render(
        <TimelineFilters
          filters={filters}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const messageCheckbox = screen.getByRole('checkbox', { name: /Messaggio/i }) as HTMLInputElement;
      // "Errore" appears in both event types (index 5) and statuses (index 9), get the first one (event type)
      const allErrorCheckboxes = screen.getAllByRole('checkbox', { name: /Errore/i });
      const errorCheckbox = allErrorCheckboxes[0] as HTMLInputElement; // First is event type
      const ragSearchCheckbox = screen.getByRole('checkbox', { name: /Ricerca RAG/i }) as HTMLInputElement;

      expect(messageCheckbox.checked).toBe(true);
      expect(errorCheckbox.checked).toBe(true);
      expect(ragSearchCheckbox.checked).toBe(false);
    });

    it('toggles event type when checkbox is clicked', async () => {
      const user = userEvent.setup();
      const filters: TimelineFiltersType = {
        eventTypes: new Set(['message']),
        statuses: DEFAULT_FILTERS.statuses
      };

      render(
        <TimelineFilters
          filters={filters}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      // "Errore" appears in both event types and statuses, get the first one (event type)
      const allErrorCheckboxes = screen.getAllByRole('checkbox', { name: /Errore/i });
      const errorCheckbox = allErrorCheckboxes[0]; // First is event type
      await user.click(errorCheckbox);

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          eventTypes: new Set(['message', 'error'])
        })
      );
    });

    it('selects all event types when "Tutti" button is clicked', async () => {
      const user = userEvent.setup();
      const filters: TimelineFiltersType = {
        eventTypes: new Set(['message']),
        statuses: DEFAULT_FILTERS.statuses
      };

      render(
        <TimelineFilters
          filters={filters}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const selectAllButtons = screen.getAllByText('Tutti');
      await user.click(selectAllButtons[0]); // First "Tutti" is for event types

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          eventTypes: new Set(['message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'])
        })
      );
    });

    it('deselects all event types when "Nessuno" button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const deselectAllButtons = screen.getAllByText('Nessuno');
      await user.click(deselectAllButtons[0]); // First "Nessuno" is for event types

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          eventTypes: new Set()
        })
      );
    });
  });

  describe('Status Filters', () => {
    it('renders all status checkboxes', () => {
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      expect(screen.getByText('In Attesa')).toBeInTheDocument();
      expect(screen.getByText('In Corso')).toBeInTheDocument();
      // "Completato" and "Errore" appear multiple times, use getAllByText
      expect(screen.getAllByText('Completato').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Errore').length).toBeGreaterThan(0);
    });

    it('checks statuses that are in the filter', () => {
      const filters: TimelineFiltersType = {
        eventTypes: DEFAULT_FILTERS.eventTypes,
        statuses: new Set(['success', 'error'])
      };

      render(
        <TimelineFilters
          filters={filters}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      // "Completato" appears in both event types and statuses, get the second one (status)
      const allCompletatoCheckboxes = screen.getAllByRole('checkbox', { name: /Completato/i });
      const successCheckbox = allCompletatoCheckboxes[1] as HTMLInputElement; // Second is status
      // "Errore" appears in both event types and statuses, get the second one (status)
      const allErrorCheckboxes = screen.getAllByRole('checkbox', { name: /Errore/i });
      const errorCheckbox = allErrorCheckboxes[1] as HTMLInputElement; // Second is status
      const pendingCheckbox = screen.getByRole('checkbox', { name: /In Attesa/i }) as HTMLInputElement;

      expect(successCheckbox.checked).toBe(true);
      expect(errorCheckbox.checked).toBe(true);
      expect(pendingCheckbox.checked).toBe(false);
    });

    it('toggles status when checkbox is clicked', async () => {
      const user = userEvent.setup();
      const filters: TimelineFiltersType = {
        eventTypes: DEFAULT_FILTERS.eventTypes,
        statuses: new Set(['success'])
      };

      render(
        <TimelineFilters
          filters={filters}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      // "Errore" appears in both event types and statuses, get the second one (status)
      const allErrorCheckboxes = screen.getAllByRole('checkbox', { name: /Errore/i });
      const errorCheckbox = allErrorCheckboxes[1]; // Second is status
      await user.click(errorCheckbox);

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          statuses: new Set(['success', 'error'])
        })
      );
    });

    it('selects all statuses when "Tutti" button is clicked', async () => {
      const user = userEvent.setup();
      const filters: TimelineFiltersType = {
        eventTypes: DEFAULT_FILTERS.eventTypes,
        statuses: new Set(['success'])
      };

      render(
        <TimelineFilters
          filters={filters}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const selectAllButtons = screen.getAllByText('Tutti');
      await user.click(selectAllButtons[1]); // Second "Tutti" is for statuses

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          statuses: new Set(['pending', 'in_progress', 'success', 'error'])
        })
      );
    });

    it('deselects all statuses when "Nessuno" button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const deselectAllButtons = screen.getAllByText('Nessuno');
      await user.click(deselectAllButtons[1]); // Second "Nessuno" is for statuses

      expect(mockOnFiltersChange).toHaveBeenCalledWith(
        expect.objectContaining({
          statuses: new Set()
        })
      );
    });
  });

  describe('Reset Filters', () => {
    it('resets all filters when reset button is clicked', async () => {
      const user = userEvent.setup();
      const modifiedFilters: TimelineFiltersType = {
        eventTypes: new Set(['message']),
        statuses: new Set(['success']),
        searchText: 'custom search'
      };

      render(
        <TimelineFilters
          filters={modifiedFilters}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      const resetButton = screen.getByText('Ripristina Filtri');
      await user.click(resetButton);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        eventTypes: new Set(['message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error']),
        statuses: new Set(['pending', 'in_progress', 'success', 'error']),
        searchText: undefined
      });
    });
  });

  describe('Accessibility', () => {
    it('has proper labels for inputs', () => {
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      expect(screen.getByLabelText('Cerca')).toBeInTheDocument();
    });

    it('has descriptive button titles', () => {
      render(
        <TimelineFilters
          filters={DEFAULT_FILTERS}
          onFiltersChange={mockOnFiltersChange}
          isCollapsed={false}
          onToggleCollapse={mockOnToggleCollapse}
        />
      );

      expect(screen.getByTitle('Nascondi filtri')).toBeInTheDocument();
    });
  });
});
