/**
 * TEST HELPERS: TimelineEventList Component Tests
 * Shared mocks, setup utilities, and helper functions
 */

import { TimelineFilters, TimelineEventType, TimelineEventStatus } from '@/lib/timeline-types';

/**
 * Helper to create Set<TimelineEventType> with type safety
 */
export const makeEventTypes = (...types: TimelineEventType[]): Set<TimelineEventType> =>
  new Set<TimelineEventType>(types);

/**
 * Helper to create Set<TimelineEventStatus> with type safety
 */
export const makeStatuses = (...statuses: TimelineEventStatus[]): Set<TimelineEventStatus> =>
  new Set<TimelineEventStatus>(statuses);

/**
 * Default props for TimelineEventList component testing
 */
export const createDefaultProps = () => {
  const mockOnSelectEvent = vi.fn();
  return {
    selectedEventId: null,
    onSelectEvent: mockOnSelectEvent,
    mockOnSelectEvent // Return mock for assertions
  };
};

/**
 * Create filters with all event types
 */
export const createAllEventTypesFilter = (): TimelineFilters => ({
  eventTypes: makeEventTypes(
    'message',
    'rag_search',
    'rag_retrieval',
    'rag_generation',
    'rag_complete',
    'error'
  ),
  statuses: makeStatuses('pending', 'in_progress', 'success', 'error')
});

/**
 * Create filters with specific types and statuses
 */
export const createCustomFilter = (
  types: TimelineEventType[],
  statuses: TimelineEventStatus[],
  options?: {
    startDate?: Date;
    endDate?: Date;
    searchText?: string;
  }
): TimelineFilters => ({
  eventTypes: makeEventTypes(...types),
  statuses: makeStatuses(...statuses),
  ...options
});
