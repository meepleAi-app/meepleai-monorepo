import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { act } from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { RecentsBar } from '../RecentsBar';
import { useRecentsStore } from '@/stores/use-recents';

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  usePathname: () => '/games/g1',
}));

function seedRecents() {
  act(() => {
    useRecentsStore
      .getState()
      .push({ id: 'g2', entity: 'game', title: 'Catan', href: '/games/g2' });
    useRecentsStore
      .getState()
      .push({ id: 'a1', entity: 'agent', title: 'Azul Expert', href: '/agents/a1' });
  });
}

describe('RecentsBar', () => {
  beforeEach(() => {
    sessionStorage.clear();
    act(() => useRecentsStore.getState().clear());
    mockPush.mockClear();
  });

  it('renders nothing when no recents', () => {
    const { container } = render(<RecentsBar />);
    expect(container.firstChild).toBeNull();
  });

  it('renders pills for each recent (excluding current path)', () => {
    seedRecents();
    render(<RecentsBar />);
    // g2 and a1 are visible; current path is /games/g1 so no exclusion happens here
    expect(screen.getAllByTestId(/^recent-pill-/)).toHaveLength(2);
  });

  it('excludes the current pathname from display', () => {
    act(() => {
      useRecentsStore
        .getState()
        .push({ id: 'g1', entity: 'game', title: 'Azul', href: '/games/g1' });
      useRecentsStore
        .getState()
        .push({ id: 'g2', entity: 'game', title: 'Catan', href: '/games/g2' });
    });
    render(<RecentsBar />);
    // /games/g1 is current path, should be excluded
    expect(screen.queryByTestId('recent-pill-g1')).not.toBeInTheDocument();
    expect(screen.getByTestId('recent-pill-g2')).toBeInTheDocument();
  });

  it('navigates on click', async () => {
    seedRecents();
    render(<RecentsBar />);
    await userEvent.click(screen.getByTestId('recent-pill-g2'));
    expect(mockPush).toHaveBeenCalledWith('/games/g2');
  });

  it('shows tooltip with title on hover', async () => {
    seedRecents();
    render(<RecentsBar />);
    const pill = screen.getByTestId('recent-pill-g2');
    expect(pill).toHaveAttribute('title', 'Catan');
  });
});
