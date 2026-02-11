/**
 * Tests for My Proposals Page
 * Issue #4056: Verify RequireRole protection
 */

import { render, screen } from '@testing-library/react';
import { useRouter } from 'next/navigation';
import { vi, Mock } from 'vitest';

import { getCurrentUser } from '@/actions/auth';

import MyProposalsPage from '../page';

// Mock Next.js navigation
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({
    push: vi.fn(),
    replace: vi.fn(),
  })),
  usePathname: vi.fn(() => '/library/proposals'),
}));

// Mock getCurrentUser action
vi.mock('@/actions/auth', () => ({
  getCurrentUser: vi.fn(),
}));

const mockGetCurrentUser = getCurrentUser as Mock;
const mockUseRouter = useRouter as Mock;

describe('MyProposalsPage', () => {
  const mockReplace = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockUseRouter.mockReturnValue({
      push: vi.fn(),
      replace: mockReplace,
    });
  });

  it('should render RequireRole wrapper (shows loading initially)', () => {
    // getCurrentUser is not resolved yet - RequireRole shows loading
    mockGetCurrentUser.mockReturnValue(new Promise(() => {})); // Never resolves

    render(<MyProposalsPage />);

    // RequireRole shows loading indicator
    expect(screen.getByText('Verifica autorizzazioni...')).toBeInTheDocument();
  });

  it('should render proposals content when user is authenticated', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      user: { id: '1', email: 'test@test.com', role: 'User' },
    });

    render(<MyProposalsPage />);

    // Wait for RequireRole to complete auth check
    expect(await screen.findByText('My Proposals')).toBeInTheDocument();
  });

  it('should redirect to login when user is not authenticated', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: false,
      user: undefined,
    });

    render(<MyProposalsPage />);

    // RequireRole redirects to login
    await vi.waitFor(() => {
      expect(mockReplace).toHaveBeenCalledWith(
        expect.stringContaining('/login')
      );
    });
  });

  it('should show proposals content for Admin role', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      user: { id: '1', email: 'admin@test.com', role: 'Admin' },
    });

    render(<MyProposalsPage />);

    expect(await screen.findByText('My Proposals')).toBeInTheDocument();
  });

  it('should show proposals content for Editor role', async () => {
    mockGetCurrentUser.mockResolvedValue({
      success: true,
      user: { id: '1', email: 'editor@test.com', role: 'Editor' },
    });

    render(<MyProposalsPage />);

    expect(await screen.findByText('My Proposals')).toBeInTheDocument();
  });
});
