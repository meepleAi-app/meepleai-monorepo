import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { TopBarNavLinks } from '../TopBarNavLinks';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

import { usePathname } from 'next/navigation';

const mockUsePathname = usePathname as unknown as ReturnType<typeof vi.fn>;

describe('TopBarNavLinks', () => {
  it('renders all 3 links', () => {
    mockUsePathname.mockReturnValue('/');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Home' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Libreria' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sessioni' })).toBeInTheDocument();
  });

  it('marks Home active when pathname is /', () => {
    mockUsePathname.mockReturnValue('/');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page');
  });

  it('marks Home active when pathname is /dashboard', () => {
    mockUsePathname.mockReturnValue('/dashboard');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Home' })).toHaveAttribute('aria-current', 'page');
  });

  it('marks Libreria active when pathname starts with /library', () => {
    mockUsePathname.mockReturnValue('/library?tab=personal');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Libreria' })).toHaveAttribute('aria-current', 'page');
  });

  it('marks Sessioni active when pathname starts with /sessions', () => {
    mockUsePathname.mockReturnValue('/sessions/live/abc');
    render(<TopBarNavLinks />);
    expect(screen.getByRole('link', { name: 'Sessioni' })).toHaveAttribute('aria-current', 'page');
  });
});
