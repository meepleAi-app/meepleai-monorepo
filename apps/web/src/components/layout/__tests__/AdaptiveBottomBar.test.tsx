import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

// Mock dependencies
const mockUsePathname = vi.fn().mockReturnValue('/library');
vi.mock('next/navigation', () => ({ usePathname: () => mockUsePathname() }));

const mockUseNavigation = vi.fn().mockReturnValue({ actionBarActions: [] });
vi.mock('@/context/NavigationContext', () => ({ useNavigation: () => mockUseNavigation() }));

const mockUseCurrentUser = vi.fn().mockReturnValue({ data: { id: '1', name: 'Test' } });
vi.mock('@/hooks/queries/useCurrentUser', () => ({ useCurrentUser: () => mockUseCurrentUser() }));

import { AdaptiveBottomBar } from '../AdaptiveBottomBar';

describe('AdaptiveBottomBar', () => {
  it('renders persistent tabs for authenticated user', () => {
    render(<AdaptiveBottomBar />);
    expect(screen.getByText('Libreria')).toBeInTheDocument();
    expect(screen.getByText('Catalogo')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('Profilo')).toBeInTheDocument();
  });

  it('hides auth-only tabs for anonymous user', () => {
    mockUseCurrentUser.mockReturnValue({ data: null });
    render(<AdaptiveBottomBar />);
    expect(screen.getByText('Libreria')).toBeInTheDocument();
    expect(screen.getByText('Catalogo')).toBeInTheDocument();
    expect(screen.queryByText('Chat')).not.toBeInTheDocument();
    mockUseCurrentUser.mockReturnValue({ data: { id: '1', name: 'Test' } });
  });

  it('shows contextual actions when present', () => {
    mockUseNavigation.mockReturnValue({
      actionBarActions: [{ id: 'add', label: 'Add Game', variant: 'primary', onClick: vi.fn() }],
    });
    render(<AdaptiveBottomBar />);
    expect(screen.getByTestId('adaptive-actions')).toBeInTheDocument();
    mockUseNavigation.mockReturnValue({ actionBarActions: [] });
  });

  it('hides actions row when no actions', () => {
    mockUseNavigation.mockReturnValue({ actionBarActions: [] });
    render(<AdaptiveBottomBar />);
    expect(screen.queryByTestId('adaptive-actions')).not.toBeInTheDocument();
  });

  it('has md:hidden class for mobile-only display', () => {
    render(<AdaptiveBottomBar />);
    const bar = screen.getByTestId('adaptive-bottom-bar');
    expect(bar.className).toContain('md:hidden');
  });
});
