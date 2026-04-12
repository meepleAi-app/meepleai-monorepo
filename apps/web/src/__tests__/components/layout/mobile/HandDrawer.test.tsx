import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { useCardHand } from '@/lib/stores/card-hand-store';
import { HandDrawer } from '@/components/layout/mobile/HandDrawer';

// Mock DrawerContent — testato separatamente
vi.mock('@/components/layout/mobile/drawer/DrawerContent', () => ({
  DrawerContent: ({ entityType }: { entityType: string }) => (
    <div data-testid="drawer-content">{entityType}</div>
  ),
}));

const CLOSED_STATE = {
  state: 'closed' as const,
  activeEntityType: null,
  activeEntityId: null,
  activeTabId: null,
  sourceEntityId: null,
  anchorRect: null,
  deckStackSkipped: false,
  drawerStack: [],
};

describe('HandDrawer', () => {
  beforeEach(() => {
    useCascadeNavigationStore.setState(CLOSED_STATE);
    useCardHand.setState({ cards: [] });
  });

  it('renders nothing when state is closed', () => {
    render(<HandDrawer />);
    expect(screen.queryByTestId('hand-drawer-panel')).toBeNull();
  });

  it('renders panel when state is drawer', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'game',
      activeEntityId: 'g1',
    });
    render(<HandDrawer />);
    expect(screen.getByTestId('hand-drawer-panel')).toBeDefined();
  });

  it('renders DrawerContent with correct entityType', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'session',
      activeEntityId: 's1',
    });
    render(<HandDrawer />);
    expect(screen.getByTestId('drawer-content')).toBeDefined();
    expect(screen.getByText('session')).toBeDefined();
  });

  it('shows entity icon in header', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'game',
      activeEntityId: 'g1',
    });
    render(<HandDrawer />);
    expect(screen.getByText('🎲')).toBeDefined();
  });

  it('closes on overlay click', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'agent',
      activeEntityId: 'a1',
    });
    render(<HandDrawer />);
    fireEvent.click(screen.getByTestId('hand-drawer-overlay'));
    expect(useCascadeNavigationStore.getState().state).toBe('closed');
  });

  it('closes on Escape key', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'kb',
      activeEntityId: 'kb1',
    });
    render(<HandDrawer />);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(useCascadeNavigationStore.getState().state).toBe('closed');
  });

  it('uses card label from hand when available', () => {
    useCardHand.setState({
      cards: [
        {
          id: 'game:g1',
          entityType: 'game',
          entityId: 'g1',
          label: 'Catan',
          href: '/games/g1',
          pinned: false,
          addedAt: 1,
        },
      ],
    });
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'game',
      activeEntityId: 'g1',
    });
    render(<HandDrawer />);
    expect(screen.getByText('Catan')).toBeDefined();
  });
});
