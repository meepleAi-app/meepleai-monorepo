import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';

import { AppNavbar } from '../AppNavbar';

vi.mock('next/navigation', () => ({ usePathname: () => '/dashboard' }));
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { displayName: 'Mario Rossi', id: '1', email: 'x@x.com', role: 'User' },
  }),
}));

describe('AppNavbar', () => {
  it('renders all 4 nav links', () => {
    render(<AppNavbar />);
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Libreria' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sessioni' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'AI Chat' })).toBeInTheDocument();
  });

  it('highlights active link when pathname matches', () => {
    render(<AppNavbar />);
    const dashboardLink = screen.getByRole('link', { name: 'Dashboard' });
    expect(dashboardLink.className).toContain('bg-secondary');
  });

  it('non-active links have muted-foreground style', () => {
    render(<AppNavbar />);
    const librariaLink = screen.getByRole('link', { name: 'Libreria' });
    expect(librariaLink.className).toContain('text-muted-foreground');
  });

  it('shows user initials in avatar', () => {
    render(<AppNavbar />);
    expect(screen.getByText('MR')).toBeInTheDocument();
  });

  it('renders MeepleAI logo text', () => {
    render(<AppNavbar />);
    expect(screen.getByText('AI')).toBeInTheDocument();
  });
});
