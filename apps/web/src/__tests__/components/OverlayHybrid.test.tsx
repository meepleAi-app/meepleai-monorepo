import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { OverlayHybrid } from '@/components/ui/overlays';
import { useOverlayStore } from '@/lib/stores/overlay-store';

describe('OverlayHybrid', () => {
  beforeEach(() => {
    useOverlayStore.setState({
      isOpen: false,
      entityType: null,
      entityId: null,
      deckItems: null,
      deckIndex: 0,
    });
  });

  it('is not visible when store is closed', () => {
    render(<OverlayHybrid>{() => <div>Content</div>}</OverlayHybrid>);
    expect(screen.queryByTestId('overlay-hybrid')).toBeNull();
  });

  it('renders when store is open', () => {
    useOverlayStore.getState().open('game', 'catan');
    render(
      <OverlayHybrid>
        {({ entityType, entityId }) => (
          <div>
            Showing {entityType}:{entityId}
          </div>
        )}
      </OverlayHybrid>
    );
    expect(screen.getByText('Showing game:catan')).toBeDefined();
  });

  it('renders deck navigation dots when deckItems present', () => {
    useOverlayStore.getState().openDeck(
      [
        { entityType: 'player', entityId: 'a' },
        { entityType: 'player', entityId: 'b' },
        { entityType: 'player', entityId: 'c' },
      ],
      0
    );
    render(<OverlayHybrid>{({ entityId }) => <div>Player: {entityId}</div>}</OverlayHybrid>);
    const dots = screen.getAllByRole('tab');
    expect(dots).toHaveLength(3);
    expect(dots[0].getAttribute('aria-selected')).toBe('true');
  });
});
