/**
 * TEST-05b: TimelineEventList Filtering Tests
 * BDD-style tests for event type, status, date range, and text search filtering
 * Tests will initially FAIL until implementation is fixed/completed
 */

import { render, screen } from '@testing-library/react';
import { TimelineEventList } from '../TimelineEventList';
import { createMockEvents, createMockEvent } from '@/lib/__tests__/test-utils';
import { TimelineFilters } from '@/lib/timeline-types';
import {
  createDefaultProps,
  makeEventTypes,
  makeStatuses
} from './TimelineEventList.test-helpers';

describe('Feature: Timeline Event Filtering', () => {
  let defaultProps: ReturnType<typeof createDefaultProps>;

  beforeEach(() => {
    defaultProps = createDefaultProps();
    vi.clearAllMocks();
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
});
