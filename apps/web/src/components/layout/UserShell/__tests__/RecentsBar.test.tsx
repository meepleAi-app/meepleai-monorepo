import { render, screen } from '@testing-library/react';
import { act } from 'react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { RecentsBar } from '../RecentsBar';
import { useRecentsStore } from '@/stores/use-recents';

// Mock next/navigation — RecentsBar no longer needs useRouter after #2193
// sub#3 (pills became next/link <a>), but the page mock still needs
// usePathname for the "exclude current path" logic.
vi.mock('next/navigation', () => ({
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

  it('renders pills as next/link <a> with the destination href (#2193 sub#3)', () => {
    seedRecents();
    render(<RecentsBar />);
    const pill = screen.getByTestId('recent-pill-g2');
    expect(pill.tagName.toLowerCase()).toBe('a');
    expect(pill).toHaveAttribute('href', '/games/g2');
  });

  it('exposes a descriptive aria-label so the single-letter content is not ambiguous (#2193 sub#3)', () => {
    seedRecents();
    render(<RecentsBar />);
    const pill = screen.getByTestId('recent-pill-g2');
    expect(pill).toHaveAttribute('aria-label', 'Apri Catan');
  });

  it('shows tooltip with title on hover', () => {
    seedRecents();
    render(<RecentsBar />);
    const pill = screen.getByTestId('recent-pill-g2');
    expect(pill).toHaveAttribute('title', 'Catan');
  });
});
