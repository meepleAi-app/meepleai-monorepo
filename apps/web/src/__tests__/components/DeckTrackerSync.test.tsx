/**
 * DeckTrackerSync Unit Tests
 *
 * Verifies that the render-less client component calls useRecentsStore.push on
 * mount and produces no visible DOM output.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@testing-library/react';

import { DeckTrackerSync } from '@/components/layout/DeckTrackerSync';
import { useRecentsStore } from '@/stores/use-recents';

// Reset zustand store between tests
beforeEach(() => {
  useRecentsStore.setState({ items: [] });
});

describe('DeckTrackerSync', () => {
  it('pushes the card to useRecentsStore on mount', () => {
    render(
      <DeckTrackerSync
        entity="agent"
        id="agent-123"
        title="Test Agent"
        href="/agents/agent-123"
        subtitle="Test subtitle"
      />
    );

    const items = useRecentsStore.getState().items;
    expect(items).toHaveLength(1);
    expect(items[0]).toMatchObject({
      id: 'agent-123',
      entity: 'agent',
      title: 'Test Agent',
      href: '/agents/agent-123',
    });
  });

  it('renders nothing (container innerHTML is empty)', () => {
    const { container } = render(
      <DeckTrackerSync
        entity="kb"
        id="kb-456"
        title="Test Document"
        href="/knowledge-base/kb-456"
      />
    );

    expect(container.innerHTML).toBe('');
  });
});
