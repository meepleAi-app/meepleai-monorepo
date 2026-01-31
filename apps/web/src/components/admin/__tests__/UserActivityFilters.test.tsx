/**
 * UserActivityFilters Unit Tests - Issue #911
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { UserActivityFilters, UserActivityFilters as FilterType } from '../UserActivityFilters';

const mockAvailableEventTypes = [
  'UserLogin',
  'UserRegistered',
  'PdfUploaded',
  'PdfProcessed',
  'GameAdded',
];

const defaultFilters: FilterType = {
  eventTypes: new Set(mockAvailableEventTypes),
  severities: new Set(['Info', 'Warning', 'Error', 'Critical']),
};

describe('UserActivityFilters', () => {
  describe('Rendering', () => {
    it('renders all available event types', () => {
      // Arrange
      const onFiltersChange = vi.fn();

      // Act
      render(
        <UserActivityFilters
          filters={defaultFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Assert
      expect(screen.getByLabelText('Login Utente')).toBeInTheDocument();
      expect(screen.getByLabelText('Registrazione Utente')).toBeInTheDocument();
      expect(screen.getByLabelText('PDF Caricato')).toBeInTheDocument();
      expect(screen.getByLabelText('PDF Processato')).toBeInTheDocument();
      expect(screen.getByLabelText('Gioco Aggiunto')).toBeInTheDocument();
    });

    it('renders all severity levels', () => {
      // Arrange
      const onFiltersChange = vi.fn();

      // Act
      render(
        <UserActivityFilters
          filters={defaultFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Assert
      expect(screen.getByLabelText('Info')).toBeInTheDocument();
      expect(screen.getByLabelText('Warning')).toBeInTheDocument();
      expect(screen.getByLabelText('Error')).toBeInTheDocument();
      expect(screen.getByLabelText('Critical')).toBeInTheDocument();
    });

    it('checks all event types by default', () => {
      // Arrange
      const onFiltersChange = vi.fn();

      // Act
      render(
        <UserActivityFilters
          filters={defaultFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Assert
      const expectedLabels = [
        'Login Utente',
        'Registrazione Utente',
        'PDF Caricato',
        'PDF Processato',
        'Gioco Aggiunto',
      ];
      expectedLabels.forEach(label => {
        const checkbox = screen.getByLabelText(label);
        expect(checkbox).toBeChecked();
      });
    });
  });

  describe('Event Type Filtering', () => {
    it('toggles event type when checkbox is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const onFiltersChange = vi.fn();
      render(
        <UserActivityFilters
          filters={defaultFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Act
      await user.click(screen.getByLabelText('Login Utente'));

      // Assert
      expect(onFiltersChange).toHaveBeenCalledWith({
        eventTypes: new Set(['UserRegistered', 'PdfUploaded', 'PdfProcessed', 'GameAdded']),
        severities: defaultFilters.severities,
      });
    });

    it('selects all event types when "Tutti" button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const onFiltersChange = vi.fn();
      const partialFilters = {
        ...defaultFilters,
        eventTypes: new Set(['UserLogin']),
      };
      render(
        <UserActivityFilters
          filters={partialFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Act
      const tuttiButtons = screen.getAllByText('Tutti');
      await user.click(tuttiButtons[0]); // Event types "Tutti" button

      // Assert
      expect(onFiltersChange).toHaveBeenCalledWith({
        eventTypes: new Set(mockAvailableEventTypes),
        severities: partialFilters.severities,
      });
    });

    it('deselects all event types when "Nessuno" button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const onFiltersChange = vi.fn();
      render(
        <UserActivityFilters
          filters={defaultFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Act
      const nessunoButtons = screen.getAllByText('Nessuno');
      await user.click(nessunoButtons[0]); // Event types "Nessuno" button

      // Assert
      expect(onFiltersChange).toHaveBeenCalledWith({
        eventTypes: new Set(),
        severities: defaultFilters.severities,
      });
    });
  });

  describe('Severity Filtering', () => {
    it('toggles severity when checkbox is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const onFiltersChange = vi.fn();
      render(
        <UserActivityFilters
          filters={defaultFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Act
      await user.click(screen.getByLabelText('Warning'));

      // Assert
      expect(onFiltersChange).toHaveBeenCalledWith({
        eventTypes: defaultFilters.eventTypes,
        severities: new Set(['Info', 'Error', 'Critical']),
      });
    });

    it('selects all severities when "Tutti" button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const onFiltersChange = vi.fn();
      const partialFilters = {
        ...defaultFilters,
        severities: new Set(['Info']),
      };
      render(
        <UserActivityFilters
          filters={partialFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Act
      const tuttiButtons = screen.getAllByText('Tutti');
      await user.click(tuttiButtons[1]); // Severity "Tutti" button

      // Assert
      expect(onFiltersChange).toHaveBeenCalledWith({
        eventTypes: partialFilters.eventTypes,
        severities: new Set(['Info', 'Warning', 'Error', 'Critical']),
      });
    });

    it('deselects all severities when "Nessuno" button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const onFiltersChange = vi.fn();
      render(
        <UserActivityFilters
          filters={defaultFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Act
      const nessunoButtons = screen.getAllByText('Nessuno');
      await user.click(nessunoButtons[1]); // Severity "Nessuno" button

      // Assert
      expect(onFiltersChange).toHaveBeenCalledWith({
        eventTypes: defaultFilters.eventTypes,
        severities: new Set(),
      });
    });
  });

  describe('Active Filters Display', () => {
    it('shows active filters chips when filters are applied', () => {
      // Arrange
      const onFiltersChange = vi.fn();
      const partialFilters: FilterType = {
        eventTypes: new Set(['UserLogin']),
        severities: new Set(['Info', 'Error']),
      };

      // Act
      render(
        <UserActivityFilters
          filters={partialFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Assert
      expect(screen.getByText('Filtri attivi:')).toBeInTheDocument();
      expect(screen.getByText(/Escluso: Registrazione Utente/)).toBeInTheDocument();
      expect(screen.getByText(/Escluso: Warning/)).toBeInTheDocument();
    });

    it('does not show active filters when all filters are selected', () => {
      // Arrange
      const onFiltersChange = vi.fn();

      // Act
      render(
        <UserActivityFilters
          filters={defaultFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Assert
      expect(screen.queryByText('Filtri attivi:')).not.toBeInTheDocument();
    });

    it('resets all filters when "Ripristina tutti" is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const onFiltersChange = vi.fn();
      const partialFilters: FilterType = {
        eventTypes: new Set(['UserLogin']),
        severities: new Set(['Info']),
      };
      render(
        <UserActivityFilters
          filters={partialFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Act
      await user.click(screen.getByText('Ripristina tutti'));

      // Assert
      expect(onFiltersChange).toHaveBeenCalledWith({
        eventTypes: new Set(mockAvailableEventTypes),
        severities: new Set(['Info', 'Warning', 'Error', 'Critical']),
      });
    });

    it('removes individual filter when X button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const onFiltersChange = vi.fn();
      const partialFilters: FilterType = {
        eventTypes: new Set(['UserLogin']),
        severities: new Set(['Info', 'Error', 'Critical']),
      };
      render(
        <UserActivityFilters
          filters={partialFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Act
      const includiButton = screen.getByLabelText('Includi Registrazione Utente');
      await user.click(includiButton);

      // Assert
      expect(onFiltersChange).toHaveBeenCalledWith({
        eventTypes: new Set(['UserLogin', 'UserRegistered']),
        severities: partialFilters.severities,
      });
    });
  });

  describe('Filter Summary', () => {
    it('displays correct number of active filters', () => {
      // Arrange
      const onFiltersChange = vi.fn();
      const partialFilters: FilterType = {
        eventTypes: new Set(['UserLogin']), // 4 excluded
        severities: new Set(['Info', 'Error']), // 2 excluded
      };

      // Act
      render(
        <UserActivityFilters
          filters={partialFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Assert
      expect(screen.getByText('6 filtri attivi')).toBeInTheDocument();
    });

    it('uses singular form for single filter', () => {
      // Arrange
      const onFiltersChange = vi.fn();
      const partialFilters: FilterType = {
        eventTypes: new Set(mockAvailableEventTypes),
        severities: new Set(['Info', 'Warning', 'Error']), // 1 excluded
      };

      // Act
      render(
        <UserActivityFilters
          filters={partialFilters}
          onFiltersChange={onFiltersChange}
          availableEventTypes={mockAvailableEventTypes}
        />
      );

      // Assert
      expect(screen.getByText('1 filtro attivo')).toBeInTheDocument();
    });
  });
});
