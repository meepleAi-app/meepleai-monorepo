// UI-04: TimelineEventItem component comprehensive unit tests
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TimelineEventItem } from '../TimelineEventItem';
import { TimelineEvent } from '@/lib/timeline-types';

describe('TimelineEventItem Component', () => {
  const mockEvent: TimelineEvent = {
    id: 'event-1',
    type: 'message',
    timestamp: new Date('2025-01-15T10:00:00Z'),
    status: 'success',
    data: {
      message: 'Test message',
      role: 'user'
    }
  };

  const mockOnSelect = vi.fn();
  const mockOnToggleExpand = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Event Header Rendering', () => {
    it('renders event type badge', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Messaggio')).toBeInTheDocument();
    });

    it('renders status icon', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('✅')).toBeInTheDocument();
    });

    it('renders event message', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Test message')).toBeInTheDocument();
    });

    it('renders timestamp', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      // Check for time format (will be locale-specific)
      const timePattern = /\d{1,2}:\d{2}:\d{2}/;
      expect(screen.getByText(timePattern)).toBeInTheDocument();
    });
  });

  describe('Event Status Indicators', () => {
    it('renders success status icon', () => {
      const successEvent = { ...mockEvent, status: 'success' as const };
      render(
        <TimelineEventItem
          event={successEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('✅')).toBeInTheDocument();
    });

    it('renders error status icon', () => {
      const errorEvent = { ...mockEvent, status: 'error' as const };
      render(
        <TimelineEventItem
          event={errorEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('❌')).toBeInTheDocument();
    });

    it('renders in_progress status icon', () => {
      const inProgressEvent = { ...mockEvent, status: 'in_progress' as const };
      render(
        <TimelineEventItem
          event={inProgressEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('🔄')).toBeInTheDocument();
    });

    it('renders pending status icon', () => {
      const pendingEvent = { ...mockEvent, status: 'pending' as const };
      render(
        <TimelineEventItem
          event={pendingEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('⏱️')).toBeInTheDocument();
    });
  });

  describe('Metrics Display', () => {
    it('renders metrics badge when metrics are present', () => {
      const eventWithMetrics = {
        ...mockEvent,
        data: {
          ...mockEvent.data,
          metrics: {
            latencyMs: 1500,
            totalTokens: 100
          }
        }
      };

      render(
        <TimelineEventItem
          event={eventWithMetrics}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText(/1.50s/i)).toBeInTheDocument();
      expect(screen.getByText(/100 tokens/i)).toBeInTheDocument();
    });

    it('does not render metrics badge when no metrics exist', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.queryByText(/tokens/i)).not.toBeInTheDocument();
    });
  });

  describe('Selection State', () => {
    it('applies selected styling when isSelected is true', () => {
      const { container } = render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={true}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      const eventDiv = container.firstChild as HTMLElement;
      expect(eventDiv).toHaveClass('bg-muted');
      expect(eventDiv).toHaveClass('border-2');
    });

    it('applies default styling when isSelected is false', () => {
      const { container } = render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      const eventDiv = container.firstChild as HTMLElement;
      expect(eventDiv).toHaveClass('border');
      expect(eventDiv).not.toHaveClass('bg-muted');
      expect(eventDiv).not.toHaveClass('border-2');
    });

    it('calls onSelect with event id when clicked', async () => {
      const user = userEvent.setup();
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      const eventHeader = screen.getByText('Test message').parentElement;
      await user.click(eventHeader!);

      expect(mockOnSelect).toHaveBeenCalledWith('event-1');
    });
  });

  describe('Expand/Collapse', () => {
    it('renders expand button when collapsed', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByTitle('Espandi')).toBeInTheDocument();
      expect(screen.getByText('▼')).toBeInTheDocument();
    });

    it('renders collapse button when expanded', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByTitle('Comprimi')).toBeInTheDocument();
      expect(screen.getByText('▲')).toBeInTheDocument();
    });

    it('calls onToggleExpand when expand button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      const expandButton = screen.getByTitle('Espandi');
      await user.click(expandButton);

      expect(mockOnToggleExpand).toHaveBeenCalledWith('event-1');
      expect(mockOnSelect).not.toHaveBeenCalled(); // Should not trigger selection
    });

    it('shows expanded details when isExpanded is true', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Messaggio:')).toBeInTheDocument();
      expect(screen.getByText('Dettagli Tecnici')).toBeInTheDocument();
    });

    it('hides expanded details when isExpanded is false', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
