import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MobileBottomBar } from '../MobileBottomBar';

vi.mock('@/components/dashboard', () => ({
  useDashboardMode: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
}));

vi.mock('@/lib/stores/cascade-navigation-store', () => ({
  useCascadeNavigationStore: vi.fn(() => vi.fn()),
}));

import { useDashboardMode } from '@/components/dashboard';

describe('MobileBottomBar', () => {
  it('renders normal mode tabs when no session active', () => {
    vi.mocked(useDashboardMode).mockReturnValue({
      isGameMode: false,
      activeSessionId: null,
    } as never);
    render(<MobileBottomBar />);
    expect(screen.getByText('Home')).toBeInTheDocument();
    expect(screen.getByText('Collection')).toBeInTheDocument();
    expect(screen.getByText('Sessions')).toBeInTheDocument();
  });

  it('renders session mode when session is active', () => {
    vi.mocked(useDashboardMode).mockReturnValue({
      isGameMode: true,
      activeSessionId: 'session-123',
    } as never);
    render(<MobileBottomBar />);
    expect(screen.getByText('Classifica')).toBeInTheDocument();
    expect(screen.queryByText('Collection')).not.toBeInTheDocument();
  });
});
