import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <div data-testid="user-menu">Avatar</div>,
}));

import { TopBarV2 } from '@/components/layout/UserShell/TopBarV2';

describe('TopBarV2', () => {
  it('renders hamburger button', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} />);
    const btn = screen.getByRole('button', { name: /menu/i });
    expect(btn).toBeDefined();
  });

  it('renders logo linking to /dashboard', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} />);
    const link = screen.getByRole('link', { name: /meepleai/i });
    expect(link).toBeDefined();
    expect(link.getAttribute('href')).toBe('/dashboard');
  });

  it('renders search button', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} />);
    const btn = screen.getByRole('button', { name: /cerca/i });
    expect(btn).toBeDefined();
  });

  it('renders user menu', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} />);
    expect(screen.getByTestId('user-menu')).toBeDefined();
  });

  it('calls onHamburgerClick when hamburger is pressed', () => {
    const onHamburgerClick = vi.fn();
    render(<TopBarV2 onHamburgerClick={onHamburgerClick} />);
    fireEvent.click(screen.getByRole('button', { name: /menu/i }));
    expect(onHamburgerClick).toHaveBeenCalledTimes(1);
  });

  it('calls onSearchClick when search button is pressed', () => {
    const onSearchClick = vi.fn();
    render(<TopBarV2 onHamburgerClick={vi.fn()} onSearchClick={onSearchClick} />);
    fireEvent.click(screen.getByRole('button', { name: /cerca/i }));
    expect(onSearchClick).toHaveBeenCalledTimes(1);
  });

  it('does not show Admin badge when adminMode is not set', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} />);
    expect(screen.queryByText('Admin')).toBeNull();
  });

  it('shows Admin badge when adminMode is true', () => {
    render(<TopBarV2 onHamburgerClick={vi.fn()} adminMode />);
    expect(screen.getByText('Admin')).toBeDefined();
  });
});
