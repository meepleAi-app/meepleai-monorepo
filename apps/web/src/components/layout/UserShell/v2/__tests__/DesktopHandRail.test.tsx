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
    expandedStack: false,
    toggleExpandStack: vi.fn(),
  };
  return {
    useCardHand: (selector?: (s: typeof state) => unknown) => (selector ? selector(state) : state),
    __mockCardHandState: state,
  };
});

vi.mock('next/navigation', () => ({
  usePathname: () => '/library/azul',
}));

import { DesktopHandRail } from '../DesktopHandRail';

// @ts-expect-error mock accessor not in real module types
import { __mockCardHandState } from '@/stores/use-card-hand';

describe('DesktopHandRail', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    __mockCardHandState.expandedStack = false;
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

  it('is collapsed when expandedStack is false', () => {
    const { container } = render(<DesktopHandRail />);
    const rail = container.querySelector('[data-testid="desktop-hand-rail"]');
    expect(rail).toHaveClass('w-[var(--card-rack-width,76px)]');
    expect(rail).toHaveAttribute('data-expanded', 'false');
  });

  it('is expanded to 240px when expandedStack is true', () => {
    __mockCardHandState.expandedStack = true;
    const { container } = render(<DesktopHandRail />);
    const rail = container.querySelector('[data-testid="desktop-hand-rail"]');
    expect(rail).toHaveClass('w-[var(--card-rack-hover-width,240px)]');
    expect(rail).toHaveAttribute('data-expanded', 'true');
  });

  it('calls toggleExpandStack when toggle is clicked', async () => {
    const user = userEvent.setup();
    render(<DesktopHandRail />);
    await user.click(screen.getByRole('button', { name: /expand/i }));
    expect(__mockCardHandState.toggleExpandStack).toHaveBeenCalled();
  });
});
