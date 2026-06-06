import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { act } from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';

// Mock ExtraMeepleCardDrawer to avoid full UI render
const onCloseMock = vi.fn();
vi.mock('@/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer', () => ({
  ExtraMeepleCardDrawer: ({
    open,
    onClose,
    entityType,
    entityId,
  }: {
    open: boolean;
    onClose: () => void;
    entityType: string;
    entityId: string;
  }) => (
    <div
      data-testid="mock-extra-drawer"
      data-open={String(open)}
      data-entity-type={entityType}
      data-entity-id={entityId}
    >
      <button type="button" onClick={onClose} data-testid="close-btn">
        close
      </button>
    </div>
  ),
}));

import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { CascadeDrawerHost } from '../CascadeDrawerHost';

describe('CascadeDrawerHost (#1929 WP5)', () => {
  beforeEach(() => {
    act(() => useCascadeNavigationStore.getState().closeCascade());
    onCloseMock.mockClear();
  });

  it('renders ExtraMeepleCardDrawer in closed state by default', () => {
    render(<CascadeDrawerHost />);
    const drawer = screen.getByTestId('mock-extra-drawer');
    expect(drawer.dataset.open).toBe('false');
  });

  it('opens ExtraMeepleCardDrawer when store transitions to drawer state', () => {
    render(<CascadeDrawerHost />);
    act(() => useCascadeNavigationStore.getState().openDrawer('gameNightEvent', 'gn-test-1'));
    const drawer = screen.getByTestId('mock-extra-drawer');
    expect(drawer.dataset.open).toBe('true');
    expect(drawer.dataset.entityType).toBe('gameNightEvent');
    expect(drawer.dataset.entityId).toBe('gn-test-1');
  });

  it('closeCascade is called when dismiss button clicked', async () => {
    const user = userEvent.setup();
    render(<CascadeDrawerHost />);
    act(() => useCascadeNavigationStore.getState().openDrawer('gameNightEvent', 'gn-test-1'));
    await user.click(screen.getByTestId('close-btn'));
    expect(useCascadeNavigationStore.getState().state).toBe('closed');
  });
});
