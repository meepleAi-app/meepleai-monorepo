/**
 * Tests for Private Games Page
 * Issue #4052: Verify RequireRole protection
 */

import { render, screen } from '@testing-library/react';
import { useRouter } from 'next/navigation';
import { vi, Mock } from 'vitest';

import { getCurrentUser } from '@/actions/auth';

import PrivateGamesPage from '../page';

// Mock Next.js navigation
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({
    push: vi.fn(),
    replace: vi.fn(),
  })),
  usePathname: vi.fn(() => '/library/private'),
}));

// Mock getCurrentUser action
vi.mock('@/actions/auth', () => ({
  getCurrentUser: vi.fn(),
}));

// Mock PrivateGamesClient to isolate page-level testing
vi.mock('../PrivateGamesClient', () => ({
  default: () => <div data-testid="private-games-client">Private Games</div>,
}));

const mockGetCurrentUser = getCurrentUser as Mock;
const mockUseRouter = useRouter as Mock;

describe('PrivateGamesPage', () => {
  const mockReplace = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockUseRouter.mockReturnValue({
      push: vi.fn(),
      replace: mockReplace,
    });
  });

  it('should render RequireRole wrapper (shows loading initially)', () => {
    mockGetCurrentUser.mockReturnValue(new Promise(() => {}));

    render(<PrivateGamesPage />);

    expect(screen.getByText('Verifica autorizzazioni...')).toBeInTheDocument();
  });

  it('should render private games content when user is authenticated', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      user: { id: '1', email: 'test@test.com', role: 'User' },
    });

    render(<PrivateGamesPage />);

    expect(await screen.findByTestId('private-games-client')).toBeInTheDocument();
  });

  it('should redirect to login when user is not authenticated', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: false,
      user: undefined,
    });

    render(<PrivateGamesPage />);

    await vi.waitFor(() => {
      expect(mockReplace).toHaveBeenCalledWith(
        expect.stringContaining('/login')
      );
    });
  });

  it('should show content for Admin role', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      user: { id: '1', email: 'admin@test.com', role: 'Admin' },
    });

    render(<PrivateGamesPage />);

    expect(await screen.findByTestId('private-games-client')).toBeInTheDocument();
  });

  it('should show content for Editor role', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      user: { id: '1', email: 'editor@test.com', role: 'Editor' },
    });

    render(<PrivateGamesPage />);

    expect(await screen.findByTestId('private-games-client')).toBeInTheDocument();
  });
});
