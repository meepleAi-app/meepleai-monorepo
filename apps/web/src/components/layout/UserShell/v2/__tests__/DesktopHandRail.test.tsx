import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/stores/use-card-hand', () => {
  const cards = [
    { id: 'azul', entity: 'game', title: 'Azul', href: '/library/azul' },
    { id: 'wings', entity: 'game', title: 'Wingspan', href: '/library/wings' },
    { id: 'ses', entity: 'session', title: 'Serata', href: '/sessions/live/123' },
  ];
  const state = {
    cards,
    pinnedIds: new Set<string>(),
    pinCard: vi.fn(),
    unpinCard: vi.fn(),
  };
  return {
    useCardHand: (selector?: (s: typeof state) => unknown) => (selector ? selector(state) : state),
  };
});

vi.mock('next/navigation', () => ({
  usePathname: () => '/library/azul',
}));

import { DesktopHandRail } from '../DesktopHandRail';

describe('DesktopHandRail', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders hand label', () => {
    render(<DesktopHandRail />);
    expect(screen.getByText(/la tua mano/i)).toBeInTheDocument();
  });

  it('renders all cards from useCardHand', () => {
    render(<DesktopHandRail />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Serata')).toBeInTheDocument();
  });

  it('marks the card matching the current pathname as active', () => {
    render(<DesktopHandRail />);
    const azul = screen.getByText('Azul').closest('a');
    expect(azul).toHaveAttribute('data-active', 'true');
  });

  it('renders the toolbar with pin/expand buttons', () => {
    render(<DesktopHandRail />);
    expect(screen.getByRole('button', { name: /pin/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /expand/i })).toBeInTheDocument();
  });

  it('is 76px wide when collapsed (default)', () => {
    const { container } = render(<DesktopHandRail />);
    const rail = container.querySelector('[data-testid="desktop-hand-rail"]');
    expect(rail).toHaveClass('w-[76px]');
    expect(rail).toHaveAttribute('data-expanded', 'false');
  });

  it('expands to 220px when toggle is clicked', async () => {
    const { container } = render(<DesktopHandRail />);
    const expandBtn = screen.getByRole('button', { name: /expand/i });
    await userEvent.click(expandBtn);
    const rail = container.querySelector('[data-testid="desktop-hand-rail"]');
    expect(rail).toHaveClass('w-[220px]');
    expect(rail).toHaveAttribute('data-expanded', 'true');
  });
});
