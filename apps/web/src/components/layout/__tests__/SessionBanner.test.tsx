import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SessionBanner } from '../UserShell/SessionBanner';

vi.mock('@/components/dashboard', () => ({
  useDashboardMode: vi.fn(),
}));

vi.mock('@/lib/stores/cascade-navigation-store', () => ({
  useCascadeNavigationStore: vi.fn(() => vi.fn()),
}));

import { useDashboardMode } from '@/components/dashboard';

describe('SessionBanner', () => {
  it('renders when session is active', () => {
    vi.mocked(useDashboardMode).mockReturnValue({
      isGameMode: true,
      activeSessionId: 'session-abc',
    } as never);
    render(<SessionBanner />);
    expect(screen.getByTestId('session-banner')).toBeInTheDocument();
  });

  it('hidden when no active session', () => {
    vi.mocked(useDashboardMode).mockReturnValue({
      isGameMode: false,
      activeSessionId: null,
    } as never);
    render(<SessionBanner />);
    expect(screen.queryByTestId('session-banner')).not.toBeInTheDocument();
  });
});
