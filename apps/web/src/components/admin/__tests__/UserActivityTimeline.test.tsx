/**
 * UserActivityTimeline Unit Tests - Issue #911
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { UserActivityTimeline } from '../UserActivityTimeline';
import { UserActivityEvent } from '../UserActivityItem';

const mockEvents: UserActivityEvent[] = Array.from({ length: 25 }, (_, i) => ({
  id: `event-${i + 1}`,
  eventType: ['UserLogin', 'PdfProcessed', 'GameAdded', 'ConfigurationChanged'][i % 4],
  description: `Event ${i + 1} description`,
  userId: `user-${i + 1}`,
  userEmail: `user${i + 1}@example.com`,
  timestamp: new Date(Date.now() - i * 60000).toISOString(),
  severity: ['Info', 'Warning', 'Error', 'Critical'][i % 4] as UserActivityEvent['severity'],
  metadata:
    i % 3 === 0
      ? {
          key: `value-${i}`,
        }
      : undefined,
}));

describe('UserActivityTimeline', () => {
  describe('Rendering', () => {
    it('renders timeline with events', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents.slice(0, 5)} />);

      // Assert
      expect(screen.getByText('User Activity Timeline')).toBeInTheDocument();
      expect(screen.getByText('Event 1 description')).toBeInTheDocument();
    });

    it('renders empty state when no events', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={[]} />);

      // Assert
      expect(screen.getByText('Nessuna attività trovata')).toBeInTheDocument();
      expect(screen.getByText('Non ci sono attività da visualizzare')).toBeInTheDocument();
    });

    it('renders filter button when showFilters is true', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents} showFilters={true} />);

      // Assert
      expect(screen.getByLabelText('Nascondi filtri')).toBeInTheDocument();
    });

    it('does not render filter button when showFilters is false', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents} showFilters={false} />);

      // Assert
      expect(screen.queryByLabelText('Mostra filtri')).not.toBeInTheDocument();
    });
  });

  describe('Pagination', () => {
    it('displays first page of events by default', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents} pageSize={10} />);

      // Assert
      expect(screen.getByText('Event 1 description')).toBeInTheDocument();
      expect(screen.getByText('Event 10 description')).toBeInTheDocument();
      expect(screen.queryByText('Event 11 description')).not.toBeInTheDocument();
    });

    it('navigates to next page when "Successiva" is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} pageSize={10} />);

      // Act
      await user.click(screen.getByLabelText('Next page'));

      // Assert
      expect(screen.getByText('Event 11 description')).toBeInTheDocument();
      expect(screen.getByText('Event 20 description')).toBeInTheDocument();
      expect(screen.queryByText('Event 1 description')).not.toBeInTheDocument();
    });

    it('navigates to previous page when "Precedente" is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} pageSize={10} />);

      // Act
      await user.click(screen.getByLabelText('Next page'));
      await user.click(screen.getByLabelText('Previous page'));

      // Assert
      expect(screen.getByText('Event 1 description')).toBeInTheDocument();
    });

    it('disables "Precedente" on first page', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents} pageSize={10} />);

      // Assert
      expect(screen.getByLabelText('Previous page')).toBeDisabled();
    });

    it('disables "Successiva" on last page', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} pageSize={10} />);

      // Act - Navigate to last page
      await user.click(screen.getByLabelText('Next page'));
      await user.click(screen.getByLabelText('Next page'));

      // Assert
      expect(screen.getByLabelText('Next page')).toBeDisabled();
    });

    it('displays correct page information', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents} pageSize={10} />);

      // Assert
      expect(screen.getByText('Pagina 1 di 3')).toBeInTheDocument();
    });

    it('changes page size when dropdown is changed', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} pageSize={10} />);

      // Act
      const pageSizeSelect = screen.getByLabelText('Items per page');
      await user.selectOptions(pageSizeSelect, '20');

      // Assert
      expect(screen.getByText('Pagina 1 di 2')).toBeInTheDocument();
    });

    it('resets to page 1 when page size is changed', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} pageSize={10} />);

      // Act
      await user.click(screen.getByLabelText('Next page')); // Go to page 2
      const pageSizeSelect = screen.getByLabelText('Items per page');
      await user.selectOptions(pageSizeSelect, '20');

      // Assert
      expect(screen.getByText('Pagina 1 di 2')).toBeInTheDocument();
    });
  });

  describe('Filtering', () => {
    it('toggles filter panel when button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} showFilters={true} />);

      // Act
      await user.click(screen.getByLabelText('Nascondi filtri'));

      // Assert
      expect(screen.queryByLabelText('Login Utente')).not.toBeInTheDocument();
      expect(screen.getByLabelText('Mostra filtri')).toBeInTheDocument();
    });

    it('filters events by event type', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} pageSize={25} />);

      // Act - Uncheck all event types except UserLogin
      await user.click(screen.getByLabelText('PDF Processato'));
      await user.click(screen.getByLabelText('Gioco Aggiunto'));
      await user.click(screen.getByLabelText('Configurazione Modificata'));

      // Assert - Should show only UserLogin events (25/4 = 6-7 events)
      const eventList = screen.getByRole('list');
      const items = within(eventList).getAllByRole('listitem');
      expect(items.length).toBeLessThanOrEqual(7);
    });

    it('filters events by severity', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} pageSize={25} />);

      // Act - Keep only Info severity
      await user.click(screen.getByLabelText('Warning'));
      await user.click(screen.getByLabelText('Error'));
      await user.click(screen.getByLabelText('Critical'));

      // Assert - Should show only Info events (25/4 = 6-7 events)
      const eventList = screen.getByRole('list');
      const items = within(eventList).getAllByRole('listitem');
      expect(items.length).toBeLessThanOrEqual(7);
    });

    it('shows empty state when filters exclude all events', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} />);

      // Act - Deselect all event types
      const nessunoButtons = screen.getAllByText('Nessuno');
      await user.click(nessunoButtons[0]); // Event types "Nessuno"

      // Assert
      expect(screen.getByText('Nessuna attività trovata')).toBeInTheDocument();
      expect(screen.getByText('Prova a modificare i filtri')).toBeInTheDocument();
    });
  });

  describe('Results Summary', () => {
    it('displays results summary with filter info', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents} pageSize={10} />);

      // Assert
      expect(screen.getByText(/Visualizzati 1-10 di 25 eventi/)).toBeInTheDocument();
    });

    it('shows hidden events count when filters are active', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityTimeline events={mockEvents} pageSize={25} />);

      // Act - Filter to show only UserLogin events
      await user.click(screen.getByLabelText('PDF Processato'));
      await user.click(screen.getByLabelText('Gioco Aggiunto'));
      await user.click(screen.getByLabelText('Configurazione Modificata'));

      // Assert
      expect(screen.getByText(/nascosti dai filtri/)).toBeInTheDocument();
    });
  });

  describe('Controlled Pagination', () => {
    it('uses controlled currentPage when provided', () => {
      // Arrange
      const onPageChange = vi.fn();

      // Act
      render(
        <UserActivityTimeline
          events={mockEvents}
          currentPage={2}
          onPageChange={onPageChange}
          pageSize={10}
        />
      );

      // Assert
      expect(screen.getByText('Pagina 2 di 3')).toBeInTheDocument();
      expect(screen.getByText('Event 11 description')).toBeInTheDocument();
    });

    it('calls onPageChange when navigation buttons are clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      const onPageChange = vi.fn();
      render(
        <UserActivityTimeline
          events={mockEvents}
          currentPage={1}
          onPageChange={onPageChange}
          pageSize={10}
        />
      );

      // Act
      await user.click(screen.getByLabelText('Next page'));

      // Assert
      expect(onPageChange).toHaveBeenCalledWith(2);
    });
  });

  describe('Accessibility', () => {
    it('has accessible region label for event list', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents} />);

      // Assert
      expect(screen.getByRole('region', { name: 'Activity timeline' })).toBeInTheDocument();
    });

    it('has accessible list semantics', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents.slice(0, 5)} />);

      // Assert
      const list = screen.getByRole('list');
      expect(list).toBeInTheDocument();
      const items = within(list).getAllByRole('listitem');
      expect(items).toHaveLength(5);
    });
  });

  describe('Edge Cases', () => {
    it('handles single event without pagination', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={[mockEvents[0]]} />);

      // Assert
      expect(screen.queryByLabelText('Next page')).not.toBeInTheDocument();
      expect(screen.queryByText(/Pagina/)).not.toBeInTheDocument();
    });

    it('handles exactly pageSize events (no pagination needed)', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents.slice(0, 10)} pageSize={10} />);

      // Assert
      expect(screen.queryByLabelText('Next page')).not.toBeInTheDocument();
    });

    it('handles pageSize + 1 events (pagination needed)', () => {
      // Arrange & Act
      render(<UserActivityTimeline events={mockEvents.slice(0, 11)} pageSize={10} />);

      // Assert
      expect(screen.getByText('Pagina 1 di 2')).toBeInTheDocument();
    });
  });
});
