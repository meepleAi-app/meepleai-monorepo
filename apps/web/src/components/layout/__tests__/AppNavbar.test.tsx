import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';

import { AppNavbar } from '../AppNavbar';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: mockPush }),
}));
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { displayName: 'Mario Rossi', id: '1', email: 'mario@test.com', role: 'User' },
    logout: vi.fn().mockResolvedValue(undefined),
  }),
}));

beforeEach(() => mockPush.mockClear());

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

  it('dropdown is closed by default', () => {
    render(<AppNavbar />);
    expect(screen.queryByText('Profilo')).not.toBeInTheDocument();
    expect(screen.queryByText('Esci')).not.toBeInTheDocument();
  });

  it('clicking avatar opens dropdown with Profilo, Impostazioni, Esci', () => {
    render(<AppNavbar />);
    fireEvent.click(screen.getByRole('button', { name: 'Menu utente' }));
    expect(screen.getByText('Profilo')).toBeInTheDocument();
    expect(screen.getByText('Impostazioni')).toBeInTheDocument();
    expect(screen.getByText('Esci')).toBeInTheDocument();
  });

  it('dropdown shows user display name and email', () => {
    render(<AppNavbar />);
    fireEvent.click(screen.getByRole('button', { name: 'Menu utente' }));
    expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
    expect(screen.getByText('mario@test.com')).toBeInTheDocument();
  });

  it('clicking Profilo navigates to /profile and closes dropdown', () => {
    render(<AppNavbar />);
    fireEvent.click(screen.getByRole('button', { name: 'Menu utente' }));
    fireEvent.click(screen.getByText('Profilo'));
    expect(mockPush).toHaveBeenCalledWith('/profile');
    expect(screen.queryByText('Profilo')).not.toBeInTheDocument();
  });

  it('clicking backdrop closes dropdown', () => {
    render(<AppNavbar />);
    fireEvent.click(screen.getByRole('button', { name: 'Menu utente' }));
    expect(screen.getByText('Esci')).toBeInTheDocument();
    const backdrop = document.querySelector('[aria-hidden="true"]');
    if (backdrop) fireEvent.click(backdrop);
    expect(screen.queryByText('Esci')).not.toBeInTheDocument();
  });
});
