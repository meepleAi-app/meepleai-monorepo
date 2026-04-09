import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { TopBar64 } from '../TopBar64';

vi.mock('next/navigation', () => ({
  usePathname: () => '/',
}));

// Mock the notification bell (existing component with runtime deps)
vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifications">🔔</button>,
}));

vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <button aria-label="User menu">MR</button>,
}));

describe('TopBar64', () => {
  it('renders logo, nav links, search, chat button, and user menu', () => {
    render(<TopBar64 />);
    expect(screen.getByRole('link', { name: /meepleai home/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Home' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /search/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /chat agente/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /notifications/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /user menu/i })).toBeInTheDocument();
  });

  it('is 64px tall and sticky', () => {
    const { container } = render(<TopBar64 />);
    const header = container.querySelector('header');
    expect(header).toHaveClass('h-16');
    expect(header).toHaveClass('sticky');
  });
});
