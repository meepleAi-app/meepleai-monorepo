import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/library',
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { displayName: 'Aaron', email: 'aaron@test.com', role: 'admin' },
  }),
}));

vi.mock('@/stores/notification/store', () => ({
  useNotificationStore: () => 3,
  selectUnreadCount: vi.fn(),
}));

import { MobileTopBar } from '@/components/layout/AppNav/MobileTopBar';

describe('MobileTopBar', () => {
  it('renders the hamburger and calls onHamburgerClick', () => {
    const onHamburgerClick = vi.fn();
    render(<MobileTopBar onHamburgerClick={onHamburgerClick} />);
    const btn = screen.getByRole('button', { name: /menu/i });
    fireEvent.click(btn);
    expect(onHamburgerClick).toHaveBeenCalledTimes(1);
  });

  it('shows the contextual title of the active section', () => {
    render(<MobileTopBar onHamburgerClick={vi.fn()} />);
    // pathname is /library → active item label is "Libreria"
    expect(screen.getByText('Libreria')).toBeDefined();
  });

  it('renders the notifications link with the unread badge', () => {
    render(<MobileTopBar onHamburgerClick={vi.fn()} />);
    const bell = screen.getByTestId('mobile-top-bar-notifications');
    expect(bell.getAttribute('href')).toBe('/notifications');
    expect(screen.getByText('3')).toBeDefined();
  });

  it('forces the title to "Admin" in adminMode', () => {
    render(<MobileTopBar onHamburgerClick={vi.fn()} adminMode />);
    expect(screen.getByText('Admin')).toBeDefined();
  });
});
