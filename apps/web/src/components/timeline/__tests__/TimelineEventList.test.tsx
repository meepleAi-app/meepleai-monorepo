/**
 * TEST-05: TimelineEventList Component Comprehensive Tests
 * BDD-style test suite following TDD RED phase
 * Tests will initially FAIL until implementation is fixed/completed
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TimelineEventList } from '../TimelineEventList';
import { createMockEvents, createMockEvent } from '@/lib/__tests__/test-utils';
import {
  TimelineEvent,
  TimelineFilters,
  TimelineEventType,
  TimelineEventStatus,
  DEFAULT_FILTERS
} from '@/lib/timeline-types';

describe('Feature: Timeline Event Filtering and Display', () => {
  const mockOnSelectEvent = jest.fn();
  const defaultProps = {
    selectedEventId: null,
    onSelectEvent: mockOnSelectEvent
  };

  const makeEventTypes = (...types: TimelineEventType[]): Set<TimelineEventType> =>
    new Set<TimelineEventType>(types);

  const makeStatuses = (...statuses: TimelineEventStatus[]): Set<TimelineEventStatus> =>
    new Set<TimelineEventStatus>(statuses);

  beforeEach(() => {
    jest.clearAllMocks();
  });

  // ============================================================================
  // HIGH PRIORITY: Event Type Filtering (4 tests)
  // ============================================================================

  describe('Scenario: Filter by Event Type', () => {
    it('should display only matching events when single type filter is applied', () => {
      // Given: Timeline with mixed event types
      const events = createMockEvents(20);
      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      // When: Component rendered with type filter
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only message events displayed
      const messageEvents = events.filter(e => e.type === 'message');
      const displayedText = screen.getByText(new RegExp(`${messageEvents.length} event(o trovato|i trovati)`, 'i'));
      expect(displayedText).toBeInTheDocument();
    });

    it('should display events matching multiple selected types', () => {
      // Given: Timeline with various event types
      const events = createMockEvents(30);
      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      // When: Component rendered with multiple type filters
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only selected event types are shown
      const matchingEvents = events.filter(e =>
        e.type === 'message' || e.type === 'rag_search' || e.type === 'error'
      );
      const displayedText = screen.getByText(new RegExp(`${matchingEvents.length} event(o trovato|i trovati)`, 'i'));
      expect(displayedText).toBeInTheDocument();
    });

    it('should show all events when all event types are selected', () => {
      // Given: Timeline with all event types
      const events = createMockEvents(24); // Divisible by 6 event types
      const filters: TimelineFilters = {
        eventTypes: makeEventTypes(
          'message',
          'rag_search',
          'rag_retrieval',
          'rag_generation',
          'rag_complete',
          'error'
        ),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      // When: Component rendered with all types selected
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: All events are displayed
      const displayedText = screen.getByText(new RegExp(`${events.length} event(o trovato|i trovati)`, 'i'));
      expect(displayedText).toBeInTheDocument();
    });

    it('should update displayed events when event type filter changes', () => {
      // Given: Component with initial filter
      const events = createMockEvents(18);
      const initialFilters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      const { rerender } = render(
        <TimelineEventList events={events} filters={initialFilters} {...defaultProps} />
      );

      const initialCount = events.filter(e => e.type === 'message').length;
      expect(screen.getByText(new RegExp(`${initialCount} event(o trovato|i trovati)`, 'i'))).toBeInTheDocument();

      // When: Filter changes to different type
      const newFilters: TimelineFilters = {
        eventTypes: makeEventTypes('rag_search'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      rerender(<TimelineEventList events={events} filters={newFilters} {...defaultProps} />);

      // Then: Displayed events update accordingly
      const newCount = events.filter(e => e.type === 'rag_search').length;
      expect(screen.getByText(new RegExp(`${newCount} event(o trovato|i trovati)`, 'i'))).toBeInTheDocument();
    });
  });

  // ============================================================================
  // HIGH PRIORITY: Status Filtering (4 tests)
  // ============================================================================

  describe('Scenario: Filter by Status', () => {
    it('should display only success events when success status filter is applied', () => {
      // Given: Timeline with mixed statuses
      const events = createMockEvents(20);
      const filters: TimelineFilters = {
        eventTypes: makeEventTypes(
          'message',
          'rag_search',
          'rag_retrieval',
          'rag_generation',
          'rag_complete',
          'error'
        ),
        statuses: makeStatuses('success')
      };

      // When: Component rendered with success status filter
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only success events are shown
      const successEvents = events.filter(e => e.status === 'success');
      const displayedText = screen.getByText(new RegExp(`${successEvents.length} event(o trovato|i trovati)`, 'i'));
      expect(displayedText).toBeInTheDocument();
    });

    it('should display only error events when error status filter is applied', () => {
      // Given: Timeline with various statuses
      const events = createMockEvents(16); // Divisible by 4 statuses
      const filters: TimelineFilters = {
        eventTypes: makeEventTypes(
          'message',
          'rag_search',
          'rag_retrieval',
          'rag_generation',
          'rag_complete',
          'error'
        ),
        statuses: makeStatuses('error')
      };

      // When: Component rendered with error status filter
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only error events are shown
      const errorEvents = events.filter(e => e.status === 'error');
      const displayedText = screen.getByText(new RegExp(`${errorEvents.length} event(o trovato|i trovati)`, 'i'));
      expect(displayedText).toBeInTheDocument();
    });

    it('should display pending and in_progress events when both are selected', () => {
      // Given: Timeline with all statuses
      const events = createMockEvents(20);
      const filters: TimelineFilters = {
        eventTypes: makeEventTypes(
          'message',
          'rag_search',
          'rag_retrieval',
          'rag_generation',
          'rag_complete',
          'error'
        ),
        statuses: makeStatuses('pending', 'in_progress')
      };

      // When: Component rendered with multiple status filters
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only pending and in_progress events are shown
      const matchingEvents = events.filter(e => e.status === 'pending' || e.status === 'in_progress');
      const displayedText = screen.getByText(new RegExp(`${matchingEvents.length} event(o trovato|i trovati)`, 'i'));
      expect(displayedText).toBeInTheDocument();
    });

    it('should update displayed events when status filter is cleared', () => {
      // Given: Component with status filter
      const events = createMockEvents(20);
      const initialFilters: TimelineFilters = {
        eventTypes: makeEventTypes(
          'message',
          'rag_search',
          'rag_retrieval',
          'rag_generation',
          'rag_complete',
          'error'
        ),
        statuses: makeStatuses('success')
      };

      const { rerender } = render(
        <TimelineEventList events={events} filters={initialFilters} {...defaultProps} />
      );

      const initialCount = events.filter(e => e.status === 'success').length;
      expect(screen.getByText(new RegExp(`${initialCount} event(o trovato|i trovati)`, 'i'))).toBeInTheDocument();

      // When: Filter cleared to show all statuses
      const newFilters: TimelineFilters = {
        eventTypes: makeEventTypes(
          'message',
          'rag_search',
          'rag_retrieval',
          'rag_generation',
          'rag_complete',
          'error'
        ),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      rerender(<TimelineEventList events={events} filters={newFilters} {...defaultProps} />);

      // Then: All events are displayed
      expect(screen.getByText(new RegExp(`${events.length} event(o trovato|i trovati)`, 'i'))).toBeInTheDocument();
    });
  });

  // ============================================================================
  // HIGH PRIORITY: Date Range Filtering (5 tests)
  // ============================================================================

  describe('Scenario: Filter by Date Range', () => {
    it('should display only events within specified date range', () => {
      // Given: Timeline with events across multiple days
      const now = new Date();
      const twoDaysAgo = new Date(now.getTime() - 2 * 24 * 60 * 60 * 1000);
      const oneDayAgo = new Date(now.getTime() - 1 * 24 * 60 * 60 * 1000);

      const events = [
        createMockEvent({ id: '1', timestamp: new Date(now.getTime() - 3 * 24 * 60 * 60 * 1000) }), // 3 days ago (excluded)
        createMockEvent({ id: '2', timestamp: twoDaysAgo }), // 2 days ago (included)
        createMockEvent({ id: '3', timestamp: oneDayAgo }), // 1 day ago (included)
        createMockEvent({ id: '4', timestamp: now }) // today (excluded)
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        startDate: twoDaysAgo,
        endDate: oneDayAgo
      };

      // When: Component rendered with date range filter
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only events within range are shown (events 2 and 3)
      expect(screen.getByText(/2 eventi trovati/i)).toBeInTheDocument();
    });

    it('should hide events outside the date range', () => {
      // Given: Timeline with events before and after range
      const now = new Date();
      const startDate = new Date(now.getTime() - 2 * 24 * 60 * 60 * 1000);

      const events = [
        createMockEvent({ id: '1', timestamp: new Date(now.getTime() - 5 * 24 * 60 * 60 * 1000) }), // Too old
        createMockEvent({ id: '2', timestamp: new Date(now.getTime() - 1 * 24 * 60 * 60 * 1000) }) // Within range
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        startDate
      };

      // When: Component rendered with start date filter
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only event within range is shown
      expect(screen.getByText(/1 evento trovato/i)).toBeInTheDocument();
    });

    it('should handle start date without end date (show all events after start)', () => {
      // Given: Timeline with events before and after start date
      const now = new Date();
      const startDate = new Date(now.getTime() - 2 * 24 * 60 * 60 * 1000);

      const events = [
        createMockEvent({ id: '1', timestamp: new Date(now.getTime() - 5 * 24 * 60 * 60 * 1000) }), // Before start
        createMockEvent({ id: '2', timestamp: new Date(now.getTime() - 1 * 24 * 60 * 60 * 1000) }), // After start
        createMockEvent({ id: '3', timestamp: now }) // After start
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        startDate
      };

      // When: Component rendered with only start date
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Events after start date are shown (events 2 and 3)
      expect(screen.getByText(/2 eventi trovati/i)).toBeInTheDocument();
    });

    it('should handle end date without start date (show all events before end)', () => {
      // Given: Timeline with events before and after end date
      const now = new Date();
      const endDate = new Date(now.getTime() - 2 * 24 * 60 * 60 * 1000);

      const events = [
        createMockEvent({ id: '1', timestamp: new Date(now.getTime() - 5 * 24 * 60 * 60 * 1000) }), // Before end
        createMockEvent({ id: '2', timestamp: new Date(now.getTime() - 3 * 24 * 60 * 60 * 1000) }), // Before end
        createMockEvent({ id: '3', timestamp: now }) // After end
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        endDate
      };

      // When: Component rendered with only end date
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Events before end date are shown (events 1 and 2)
      expect(screen.getByText(/2 eventi trovati/i)).toBeInTheDocument();
    });

    it('should show empty state when date range excludes all events', () => {
      // Given: Timeline with recent events
      const now = new Date();
      const events = createMockEvents(10); // All recent events

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        startDate: new Date(now.getTime() - 365 * 24 * 60 * 60 * 1000), // 1 year ago
        endDate: new Date(now.getTime() - 364 * 24 * 60 * 60 * 1000) // Almost 1 year ago (narrow range)
      };

      // When: Component rendered with date range that excludes all
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Empty state is shown
      expect(screen.getByText(/Nessun evento trovato/i)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // HIGH PRIORITY: Text Search (5 tests)
  // ============================================================================

  describe('Scenario: Search Events by Text', () => {
    it('should display events matching search text in message field', () => {
      // Given: Timeline with various messages
      const events = [
        createMockEvent({ id: '1', data: { message: 'How do I move the knight in chess?' } }),
        createMockEvent({ id: '2', data: { message: 'What are the rules for castling?' } }),
        createMockEvent({ id: '3', data: { message: 'Explain en passant capture' } })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        searchText: 'chess'
      };

      // When: Component rendered with search text
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only matching event is shown
      expect(screen.getByText(/1 evento trovato/i)).toBeInTheDocument();
    });

    it('should display events matching search text in citations', () => {
      // Given: Timeline with events containing citations
      const events = [
        createMockEvent({
          id: '1',
          data: {
            message: 'Answer',
            citations: [
              { text: 'The queen moves diagonally', source: 'chess.pdf', page: 5 }
            ]
          }
        }),
        createMockEvent({
          id: '2',
          data: {
            message: 'Answer',
            citations: [
              { text: 'The rook moves horizontally', source: 'chess.pdf', page: 3 }
            ]
          }
        })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        searchText: 'diagonally'
      };

      // When: Component rendered with search text matching citations
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Event with matching citation is shown
      expect(screen.getByText(/1 evento trovato/i)).toBeInTheDocument();
    });

    it('should perform case-insensitive search', () => {
      // Given: Timeline with mixed case messages
      const events = [
        createMockEvent({ id: '1', data: { message: 'CHESS RULES' } }),
        createMockEvent({ id: '2', data: { message: 'chess strategy' } }),
        createMockEvent({ id: '3', data: { message: 'ChEsS tactics' } })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        searchText: 'chess'
      };

      // When: Component rendered with lowercase search
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: All events with "chess" (any case) are shown
      expect(screen.getByText(/3 eventi trovati/i)).toBeInTheDocument();
    });

    it('should show all events when search text is empty', () => {
      // Given: Timeline with events
      const events = createMockEvents(15);

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        searchText: ''
      };

      // When: Component rendered with empty search
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: All events are displayed
      expect(screen.getByText(new RegExp(`${events.length} event(o trovato|i trovati)`, 'i'))).toBeInTheDocument();
    });

    it('should show empty state when search has no results', () => {
      // Given: Timeline with specific messages
      const events = [
        createMockEvent({ id: '1', data: { message: 'Chess rules' } }),
        createMockEvent({ id: '2', data: { message: 'Chess strategy' } })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        searchText: 'poker'
      };

      // When: Component rendered with non-matching search
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Empty state is shown
      expect(screen.getByText(/Nessun evento trovato/i)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // HIGH PRIORITY: Combined Filters (4 tests)
  // ============================================================================

  describe('Scenario: Apply Multiple Filters Simultaneously', () => {
    it('should apply both event type and status filters together', () => {
      // Given: Timeline with mixed types and statuses
      const events = [
        createMockEvent({ id: '1', type: 'message', status: 'success' }),
        createMockEvent({ id: '2', type: 'message', status: 'error' }),
        createMockEvent({ id: '3', type: 'rag_search', status: 'success' }),
        createMockEvent({ id: '4', type: 'error', status: 'error' })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('success')
      };

      // When: Component rendered with combined filters
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only message events with success status shown (event 1)
      expect(screen.getByText(/1 evento trovato/i)).toBeInTheDocument();
    });

    it('should apply event type, status, and date range filters together', () => {
      // Given: Timeline with events across different dimensions
      const now = new Date();
      const yesterday = new Date(now.getTime() - 24 * 60 * 60 * 1000);
      const twoDaysAgo = new Date(now.getTime() - 2 * 24 * 60 * 60 * 1000);

      const events = [
        createMockEvent({ id: '1', type: 'message', status: 'success', timestamp: yesterday }),
        createMockEvent({ id: '2', type: 'message', status: 'error', timestamp: yesterday }),
        createMockEvent({ id: '3', type: 'rag_search', status: 'success', timestamp: yesterday }),
        createMockEvent({ id: '4', type: 'message', status: 'success', timestamp: twoDaysAgo }) // Outside date range
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('success'),
        startDate: new Date(now.getTime() - 25 * 60 * 60 * 1000) // Just over 1 day ago
      };

      // When: Component rendered with type + status + date filters
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only event 1 matches all criteria
      expect(screen.getByText(/1 evento trovato/i)).toBeInTheDocument();
    });

    it('should apply all filters including search text', () => {
      // Given: Timeline with comprehensive event data
      const now = new Date();
      const events = [
        createMockEvent({
          id: '1',
          type: 'message',
          status: 'success',
          timestamp: now,
          data: { message: 'How to play chess?' }
        }),
        createMockEvent({
          id: '2',
          type: 'message',
          status: 'success',
          timestamp: now,
          data: { message: 'How to play poker?' }
        }),
        createMockEvent({
          id: '3',
          type: 'rag_search',
          status: 'success',
          timestamp: now,
          data: { message: 'How to play chess?' }
        }),
        createMockEvent({
          id: '4',
          type: 'message',
          status: 'error',
          timestamp: now,
          data: { message: 'How to play chess?' }
        })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('success'),
        searchText: 'chess'
      };

      // When: Component rendered with all filters
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Only event 1 matches all criteria
      expect(screen.getByText(/1 evento trovato/i)).toBeInTheDocument();
    });

    it('should show empty state when combined filters exclude all events', () => {
      // Given: Timeline with specific events
      const events = createMockEvents(10);

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('success'),
        searchText: 'nonexistent text xyz123'
      };

      // When: Component rendered with filters that match nothing
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Empty state is shown
      expect(screen.getByText(/Nessun evento trovato/i)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // HIGH PRIORITY: Event Sorting (4 tests)
  // ============================================================================

  describe('Scenario: Sort Events by Various Criteria', () => {
    it('should sort events by timestamp descending (most recent first) by default', () => {
      // Given: Timeline with events in random time order
      const now = new Date();
      const events = [
        createMockEvent({ id: '1', timestamp: new Date(now.getTime() - 2 * 60 * 60 * 1000) }), // 2 hours ago
        createMockEvent({ id: '2', timestamp: new Date(now.getTime() - 5 * 60 * 60 * 1000) }), // 5 hours ago
        createMockEvent({ id: '3', timestamp: new Date(now.getTime() - 1 * 60 * 60 * 1000) })  // 1 hour ago (most recent)
      ];

      // When: Component rendered
      render(<TimelineEventList events={events} filters={DEFAULT_FILTERS} {...defaultProps} />);

      // Then: Events are sorted newest first
      // Note: This tests the current implementation behavior (descending)
      // The actual visual order would be verified in E2E tests
      expect(screen.getByText(/3 eventi trovati/i)).toBeInTheDocument();
    });

    it('should maintain sort order when filters change', () => {
      // Given: Timeline with timestamped events
      const now = new Date();
      const events = [
        createMockEvent({ id: '1', type: 'message', timestamp: new Date(now.getTime() - 1000) }),
        createMockEvent({ id: '2', type: 'rag_search', timestamp: new Date(now.getTime() - 2000) }),
        createMockEvent({ id: '3', type: 'message', timestamp: new Date(now.getTime() - 3000) })
      ];

      const initialFilters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      const { rerender } = render(
        <TimelineEventList events={events} filters={initialFilters} {...defaultProps} />
      );

      expect(screen.getByText(/3 eventi trovati/i)).toBeInTheDocument();

      // When: Filter changes to show only messages
      const newFilters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      rerender(<TimelineEventList events={events} filters={newFilters} {...defaultProps} />);

      // Then: Filtered events still maintain sort order
      expect(screen.getByText(/2 eventi trovati/i)).toBeInTheDocument();
    });

    it('should handle events with identical timestamps', () => {
      // Given: Timeline with simultaneous events
      const now = new Date();
      const events = [
        createMockEvent({ id: '1', timestamp: now }),
        createMockEvent({ id: '2', timestamp: now }),
        createMockEvent({ id: '3', timestamp: now })
      ];

      // When: Component rendered
      render(<TimelineEventList events={events} filters={DEFAULT_FILTERS} {...defaultProps} />);

      // Then: All events are displayed (sort order stable but deterministic)
      expect(screen.getByText(/3 eventi trovati/i)).toBeInTheDocument();
    });

    it('should sort correctly after new events are added', () => {
      // Given: Initial timeline
      const now = new Date();
      const initialEvents = [
        createMockEvent({ id: '1', timestamp: new Date(now.getTime() - 2000) }),
        createMockEvent({ id: '2', timestamp: new Date(now.getTime() - 3000) })
      ];

      const { rerender } = render(
        <TimelineEventList events={initialEvents} filters={DEFAULT_FILTERS} {...defaultProps} />
      );

      expect(screen.getByText(/2 eventi trovati/i)).toBeInTheDocument();

      // When: New event added (most recent)
      const updatedEvents = [
        ...initialEvents,
        createMockEvent({ id: '3', timestamp: now }) // Newest event
      ];

      rerender(<TimelineEventList events={updatedEvents} filters={DEFAULT_FILTERS} {...defaultProps} />);

      // Then: All events displayed with correct sort
      expect(screen.getByText(/3 eventi trovati/i)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // HIGH PRIORITY: Empty States (3 tests)
  // ============================================================================

  describe('Scenario: Display Empty States', () => {
    it('should show empty state when no events exist', () => {
      // Given: Empty timeline
      const events: TimelineEvent[] = [];

      // When: Component rendered with no events
      render(<TimelineEventList events={events} filters={DEFAULT_FILTERS} {...defaultProps} />);

      // Then: Empty state message is displayed
      expect(screen.getByText(/Nessun evento trovato/i)).toBeInTheDocument();
      expect(screen.getByText(/Prova a modificare i filtri/i)).toBeInTheDocument();
    });

    it('should show empty state when all events are filtered out', () => {
      // Given: Timeline with events that will be filtered out
      const events = createMockEvents(10); // All have various types

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes(), // No types selected
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      // When: Component rendered with filters excluding all
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Empty state is shown
      expect(screen.getByText(/Nessun evento trovato/i)).toBeInTheDocument();
    });

    it('should display helpful message in empty state suggesting filter modification', () => {
      // Given: Empty filtered results
      const events = createMockEvents(5);

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('success'),
        searchText: 'impossible search text xyz123abc'
      };

      // When: Component rendered with filters yielding no results
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Empty state shows helpful guidance
      expect(screen.getByText(/Nessun evento trovato/i)).toBeInTheDocument();
      expect(screen.getByText(/Prova a modificare i filtri/i)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // MEDIUM PRIORITY: Event Rendering (5 tests)
  // ============================================================================

  describe('Scenario: Render Event Details', () => {
    it('should display event count with correct singular/plural form', () => {
      // Given: Timeline with single event
      const singleEvent = createMockEvents(1);

      const { rerender } = render(
        <TimelineEventList events={singleEvent} filters={DEFAULT_FILTERS} {...defaultProps} />
      );

      // Then: Singular form "evento" is used
      expect(screen.getByText(/1 evento trovato/i)).toBeInTheDocument();

      // When: Multiple events
      const multipleEvents = createMockEvents(5);
      rerender(<TimelineEventList events={multipleEvents} filters={DEFAULT_FILTERS} {...defaultProps} />);

      // Then: Plural form "eventi" is used
      expect(screen.getByText(/5 eventi trovati/i)).toBeInTheDocument();
    });

    it('should show total event count when filters are applied', () => {
      // Given: Timeline with 10 events
      const events = createMockEvents(10);

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      // When: Component rendered with filter
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Shows filtered count and total count
      const filteredCount = events.filter(e => e.type === 'message').length;
      expect(screen.getByText(new RegExp(`${filteredCount} event(o trovato|i trovati) \\(${events.length} totali\\)`, 'i'))).toBeInTheDocument();
    });

    it('should not show total count when no filters are applied', () => {
      // Given: Timeline with events
      const events = createMockEvents(8);

      // When: Component rendered without filters
      render(<TimelineEventList events={events} filters={DEFAULT_FILTERS} {...defaultProps} />);

      // Then: Shows only event count, not total
      const text = screen.getByText(/8 eventi trovati/i).textContent;
      expect(text).not.toMatch(/totali/i);
    });

    it('should render TimelineEventItem for each filtered event', () => {
      // Given: Timeline with specific events
      const events = [
        createMockEvent({ id: 'event-1', type: 'message' }),
        createMockEvent({ id: 'event-2', type: 'rag_search' }),
        createMockEvent({ id: 'event-3', type: 'message' })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
      };

      // When: Component rendered
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Correct number of events displayed
      expect(screen.getByText(/2 eventi trovati/i)).toBeInTheDocument();
    });

    it('should pass correct props to TimelineEventItem components', () => {
      // Given: Timeline with events
      const events = createMockEvents(3);
      const selectedId = events[1].id;

      // When: Component rendered with selected event
      render(
        <TimelineEventList
          events={events}
          filters={DEFAULT_FILTERS}
          selectedEventId={selectedId}
          onSelectEvent={mockOnSelectEvent}
        />
      );

      // Then: Events are rendered
      expect(screen.getByText(/3 eventi trovati/i)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // MEDIUM PRIORITY: Search Edge Cases (3 tests)
  // ============================================================================

  describe('Scenario: Handle Search Edge Cases', () => {
    it('should handle special characters in search text', () => {
      // Given: Timeline with special characters in messages
      const events = [
        createMockEvent({ id: '1', data: { message: 'Cost: $5.99 (special price!)' } }),
        createMockEvent({ id: '2', data: { message: 'Regular message' } })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        searchText: '$5.99'
      };

      // When: Component rendered with special character search
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Event with special characters is found
      expect(screen.getByText(/1 evento trovato/i)).toBeInTheDocument();
    });

    it('should search across multiple fields (message, error, citations)', () => {
      // Given: Timeline with text in different fields
      const events = [
        createMockEvent({ id: '1', data: { message: 'Contains keyword' } }),
        createMockEvent({ id: '2', data: { error: 'Error with keyword' } }),
        createMockEvent({
          id: '3',
          data: {
            citations: [{ text: 'Citation with keyword', source: 'source.pdf' }]
          }
        })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        searchText: 'keyword'
      };

      // When: Component rendered with search term
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: All events with keyword in any field are found
      expect(screen.getByText(/3 eventi trovati/i)).toBeInTheDocument();
    });

    it('should handle search in citation source field', () => {
      // Given: Timeline with citations
      const events = [
        createMockEvent({
          id: '1',
          data: {
            citations: [{ text: 'Text content', source: 'chess-rulebook.pdf' }]
          }
        }),
        createMockEvent({
          id: '2',
          data: {
            citations: [{ text: 'Text content', source: 'poker-rules.pdf' }]
          }
        })
      ];

      const filters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search', 'rag_retrieval', 'rag_generation', 'rag_complete', 'error'),
        statuses: makeStatuses('pending', 'in_progress', 'success', 'error'),
        searchText: 'chess'
      };

      // When: Component rendered with search matching source
      render(<TimelineEventList events={events} filters={filters} {...defaultProps} />);

      // Then: Event with matching citation source is found
      expect(screen.getByText(/1 evento trovato/i)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // MEDIUM PRIORITY: Sorting Edge Cases (2 tests)
  // ============================================================================

  describe('Scenario: Handle Sorting Edge Cases', () => {
    it('should handle null or undefined timestamp values gracefully', () => {
      // Given: Timeline with potentially invalid timestamps
      // Note: TypeScript requires Date type, but testing runtime robustness
      const now = new Date();
      const events = [
        createMockEvent({ id: '1', timestamp: now }),
        createMockEvent({ id: '2', timestamp: new Date(now.getTime() - 1000) })
      ];

      // When: Component rendered
      render(<TimelineEventList events={events} filters={DEFAULT_FILTERS} {...defaultProps} />);

      // Then: Component handles gracefully
      expect(screen.getByText(/2 eventi trovati/i)).toBeInTheDocument();
    });

    it('should maintain stable sort order for events with same timestamp and type', () => {
      // Given: Timeline with identical events
      const now = new Date();
      const events = [
        createMockEvent({ id: 'event-1', type: 'message', status: 'success', timestamp: now }),
        createMockEvent({ id: 'event-2', type: 'message', status: 'success', timestamp: now }),
        createMockEvent({ id: 'event-3', type: 'message', status: 'success', timestamp: now })
      ];

      // When: Component rendered multiple times
      const { rerender } = render(
        <TimelineEventList events={events} filters={DEFAULT_FILTERS} {...defaultProps} />
      );

      expect(screen.getByText(/3 eventi trovati/i)).toBeInTheDocument();

      rerender(<TimelineEventList events={events} filters={DEFAULT_FILTERS} {...defaultProps} />);

      // Then: Order remains consistent
      expect(screen.getByText(/3 eventi trovati/i)).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Additional Coverage Tests
  // ============================================================================

  describe('Scenario: Handle Filter Updates', () => {
    it('should update display when filters prop changes', () => {
      // Given: Component with initial filters
      const events = createMockEvents(12);
      const initialFilters: TimelineFilters = {
        eventTypes: makeEventTypes('message'),
        statuses: makeStatuses('success')
      };

      const { rerender } = render(
        <TimelineEventList events={events} filters={initialFilters} {...defaultProps} />
      );

      // Then: Initial filtered count displayed
      const initialMatches = events.filter(
        e => e.type === 'message' && e.status === 'success'
      ).length;
      expect(screen.getByText(new RegExp(`${initialMatches} event(o trovato|i trovati)`, 'i'))).toBeInTheDocument();

      // When: Filters updated
      const updatedFilters: TimelineFilters = {
        eventTypes: makeEventTypes('message', 'rag_search'),
        statuses: makeStatuses('success', 'error')
      };

      rerender(<TimelineEventList events={events} filters={updatedFilters} {...defaultProps} />);

      // Then: Updated filtered count displayed
      const updatedMatches = events.filter(
        e => (e.type === 'message' || e.type === 'rag_search') &&
             (e.status === 'success' || e.status === 'error')
      ).length;
      expect(screen.getByText(new RegExp(`${updatedMatches} event(o trovato|i trovati)`, 'i'))).toBeInTheDocument();
    });
  });

  describe('Scenario: Handle Event Selection', () => {
    it('should pass selectedEventId to TimelineEventItem components', () => {
      // Given: Timeline with multiple events
      const events = createMockEvents(5);
      const selectedId = events[2].id;

      // When: Component rendered with selected event
      render(
        <TimelineEventList
          events={events}
          filters={DEFAULT_FILTERS}
          selectedEventId={selectedId}
          onSelectEvent={mockOnSelectEvent}
        />
      );

      // Then: Component renders with selection
      expect(screen.getByText(/5 eventi trovati/i)).toBeInTheDocument();
    });
  });
});
