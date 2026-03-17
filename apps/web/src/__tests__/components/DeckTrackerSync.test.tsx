/**
 * DeckTrackerSync Unit Tests
 *
 * Verifies that the render-less client component calls drawCard on mount
 * and produces no visible DOM output.
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render } from '@testing-library/react';

import { DeckTrackerSync } from '@/components/layout/DeckTrackerSync';
import { useCardHand } from '@/stores/use-card-hand';

// Reset zustand store between tests
beforeEach(() => {
  useCardHand.setState({ cards: [], focusedIdx: -1 });
});

describe('DeckTrackerSync', () => {
  it('calls drawCard on mount and adds the card to the store', () => {
    render(
      <DeckTrackerSync
        entity="agent"
        id="agent-123"
        title="Test Agent"
        href="/agents/agent-123"
        subtitle="Test subtitle"
      />
    );

    const cards = useCardHand.getState().cards;
    expect(cards).toHaveLength(1);
    expect(cards[0]).toMatchObject({
      id: 'agent-123',
      entity: 'agent',
      title: 'Test Agent',
      href: '/agents/agent-123',
      subtitle: 'Test subtitle',
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

  it('does not add duplicate cards when re-rendered with same id', () => {
    const { rerender } = render(
      <DeckTrackerSync entity="agent" id="agent-789" title="Agent One" href="/agents/agent-789" />
    );

    rerender(
      <DeckTrackerSync
        entity="agent"
        id="agent-789"
        title="Agent One Updated"
        href="/agents/agent-789"
      />
    );

    const cards = useCardHand.getState().cards;
    expect(cards).toHaveLength(1);
    expect(cards[0].id).toBe('agent-789');
  });
});
