import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { id: 'u1', displayName: 'Marco' },
  }),
}));

vi.mock('@/stores/use-card-hand', () => {
  const state = {
    cards: [],
    pinnedIds: new Set(),
    drawCard: vi.fn(),
    pinCard: vi.fn(),
    unpinCard: vi.fn(),
  };
  return {
    useCardHand: (selector?: (s: typeof state) => unknown) => (selector ? selector(state) : state),
  };
});

vi.mock('@/lib/stores/mini-nav-config-store', () => {
  const state = {
    config: null,
    setConfig: vi.fn(),
    clear: vi.fn(),
  };
  return {
    useMiniNavConfigStore: (selector?: (s: typeof state) => unknown) =>
      selector ? selector(state) : state,
  };
});

import { DashboardClientV2 } from '../DashboardClientV2';

describe('DashboardClientV2', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the greeting with the user name', () => {
    render(<DashboardClientV2 />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
  });

  it('renders the KPI strip', () => {
    render(<DashboardClientV2 />);
    expect(screen.getByText(/libreria/i)).toBeInTheDocument();
    expect(screen.getByText(/sessioni/i)).toBeInTheDocument();
  });

  it('renders the empty-state hero when no live session', () => {
    render(<DashboardClientV2 />);
    expect(screen.getByText(/Nessuna partita in corso/i)).toBeInTheDocument();
  });
});
