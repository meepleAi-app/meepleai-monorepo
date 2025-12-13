/**
 * UserActivityItem Unit Tests - Issue #911
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { UserActivityItem, UserActivityEvent } from '../UserActivityItem';

const mockEvent: UserActivityEvent = {
  id: 'event-1',
  eventType: 'UserLogin',
  description: 'User logged in successfully',
  userId: 'user-123',
  userEmail: 'test@example.com',
  timestamp: new Date('2025-12-11T10:00:00Z').toISOString(),
  severity: 'Info',
  metadata: {
    ip: '192.168.1.1',
    browser: 'Chrome',
  },
};

describe('UserActivityItem', () => {
  describe('Rendering', () => {
    it('renders event description', () => {
      // Arrange & Act
      render(<UserActivityItem event={mockEvent} />);

      // Assert
      expect(screen.getByText('User logged in successfully')).toBeInTheDocument();
    });

    it('renders user email', () => {
      // Arrange & Act
      render(<UserActivityItem event={mockEvent} />);

      // Assert
      expect(screen.getByText('test@example.com')).toBeInTheDocument();
    });

    it('renders relative timestamp', () => {
      // Arrange & Act
      render(<UserActivityItem event={mockEvent} />);

      // Assert
      const timeElement = screen.getByRole('time');
      expect(timeElement).toBeInTheDocument();
      expect(timeElement).toHaveAttribute('dateTime', mockEvent.timestamp);
    });

    it('renders severity icon with correct style', () => {
      // Arrange & Act
      render(<UserActivityItem event={mockEvent} />);

      // Assert
      const iconContainer = screen.getByLabelText('Info event');
      expect(iconContainer).toHaveClass('text-blue-600', 'bg-blue-50');
    });

    it('renders expand button when metadata exists', () => {
      // Arrange & Act
      render(<UserActivityItem event={mockEvent} />);

      // Assert
      expect(screen.getByLabelText('Mostra dettagli')).toBeInTheDocument();
    });

    it('does not render expand button when no metadata', () => {
      // Arrange
      const eventWithoutMetadata = { ...mockEvent, metadata: undefined };

      // Act
      render(<UserActivityItem event={eventWithoutMetadata} />);

      // Assert
      expect(screen.queryByLabelText('Mostra dettagli')).not.toBeInTheDocument();
    });
  });

  describe('Metadata Expansion', () => {
    it('expands metadata when expand button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityItem event={mockEvent} />);

      // Act
      await user.click(screen.getByLabelText('Mostra dettagli'));

      // Assert
      expect(screen.getByText('Dettagli:')).toBeInTheDocument();
      expect(screen.getByText('User ID:')).toBeInTheDocument();
      expect(screen.getByText('user-123')).toBeInTheDocument();
    });

    it('collapses metadata when collapse button is clicked', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityItem event={mockEvent} />);

      // Act
      await user.click(screen.getByLabelText('Mostra dettagli'));
      expect(screen.getByText('Dettagli:')).toBeInTheDocument();

      await user.click(screen.getByLabelText('Nascondi dettagli'));

      // Assert
      expect(screen.queryByText('Dettagli:')).not.toBeInTheDocument();
    });

    it('displays metadata JSON when expanded', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityItem event={mockEvent} />);

      // Act
      await user.click(screen.getByLabelText('Mostra dettagli'));

      // Assert
      expect(screen.getByText(/192.168.1.1/)).toBeInTheDocument();
      expect(screen.getByText(/Chrome/)).toBeInTheDocument();
    });

    it('handles controlled expansion via props', async () => {
      // Arrange
      const onToggleExpand = vi.fn();
      render(
        <UserActivityItem event={mockEvent} isExpanded={false} onToggleExpand={onToggleExpand} />
      );

      // Act
      await userEvent.click(screen.getByLabelText('Mostra dettagli'));

      // Assert
      expect(onToggleExpand).toHaveBeenCalledWith('event-1');
    });
  });

  describe('Severity Styles', () => {
    it('renders Info severity with blue style', () => {
      // Arrange & Act
      render(<UserActivityItem event={{ ...mockEvent, severity: 'Info' }} />);

      // Assert
      expect(screen.getByLabelText('Info event')).toHaveClass('text-blue-600', 'bg-blue-50');
    });

    it('renders Warning severity with yellow style', () => {
      // Arrange & Act
      render(<UserActivityItem event={{ ...mockEvent, severity: 'Warning' }} />);

      // Assert
      expect(screen.getByLabelText('Warning event')).toHaveClass('text-yellow-600', 'bg-yellow-50');
    });

    it('renders Error severity with red style', () => {
      // Arrange & Act
      render(<UserActivityItem event={{ ...mockEvent, severity: 'Error' }} />);

      // Assert
      expect(screen.getByLabelText('Error event')).toHaveClass('text-red-600', 'bg-red-50');
    });

    it('renders Critical severity with dark red style', () => {
      // Arrange & Act
      render(<UserActivityItem event={{ ...mockEvent, severity: 'Critical' }} />);

      // Assert
      expect(screen.getByLabelText('Critical event')).toHaveClass('text-red-700', 'bg-red-100');
    });
  });

  describe('Edge Cases', () => {
    it('handles invalid timestamp gracefully', () => {
      // Arrange
      const eventWithInvalidTimestamp = {
        ...mockEvent,
        timestamp: 'invalid-date',
      };

      // Act & Assert
      expect(() => render(<UserActivityItem event={eventWithInvalidTimestamp} />)).not.toThrow();
    });

    it('renders fallback icon for unknown event type', () => {
      // Arrange
      const eventWithUnknownType = {
        ...mockEvent,
        eventType: 'UnknownEventType',
      };

      // Act
      render(<UserActivityItem event={eventWithUnknownType} />);

      // Assert - Should render ActivityIcon (fallback)
      expect(screen.getByLabelText('Info event')).toBeInTheDocument();
    });

    it('handles missing optional fields', () => {
      // Arrange
      const minimalEvent: UserActivityEvent = {
        id: 'event-minimal',
        eventType: 'SystemEvent',
        description: 'Minimal event',
        timestamp: new Date().toISOString(),
      };

      // Act & Assert
      expect(() => render(<UserActivityItem event={minimalEvent} />)).not.toThrow();
    });
  });

  describe('Accessibility', () => {
    it('has accessible expand button', () => {
      // Arrange & Act
      render(<UserActivityItem event={mockEvent} />);

      // Assert
      const button = screen.getByLabelText('Mostra dettagli');
      expect(button).toHaveAttribute('aria-expanded', 'false');
    });

    it('updates aria-expanded when expanded', async () => {
      // Arrange
      const user = userEvent.setup();
      render(<UserActivityItem event={mockEvent} />);

      // Act
      await user.click(screen.getByLabelText('Mostra dettagli'));

      // Assert
      expect(screen.getByLabelText('Nascondi dettagli')).toHaveAttribute('aria-expanded', 'true');
    });

    it('has accessible time element with datetime attribute', () => {
      // Arrange & Act
      render(<UserActivityItem event={mockEvent} />);

      // Assert
      const timeElement = screen.getByRole('time');
      expect(timeElement).toHaveAttribute('dateTime', mockEvent.timestamp);
    });
  });
});
