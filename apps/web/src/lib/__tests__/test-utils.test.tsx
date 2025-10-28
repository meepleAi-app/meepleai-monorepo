/**
 * Tests for test-utils utilities
 * Ensures that test utility functions work correctly
 */

import { renderWithProviders, createMockEvents } from './test-utils';

describe('test-utils', () => {
  describe('renderWithProviders', () => {
    it('should render children correctly', () => {
      const { getByText } = renderWithProviders(<div>hello world</div>);
      expect(getByText('hello world')).toBeInTheDocument();
    });
  });

  describe('createMockEvents', () => {
    it('should create requested number of events', () => {
      const events = createMockEvents(3);
      expect(events).toHaveLength(3);
    });

    it('should create events with different types', () => {
      const events = createMockEvents(6);
      const types = events.map(e => e.type);
      expect(types).toContain('message');
      expect(types).toContain('rag_search');
      expect(types).toContain('rag_retrieval');
    });

    it('should create events with sequential timestamps', () => {
      const events = createMockEvents(3);
      expect(events[0].timestamp.getTime()).toBeGreaterThan(events[1].timestamp.getTime());
      expect(events[1].timestamp.getTime()).toBeGreaterThan(events[2].timestamp.getTime());
    });
  });
});
