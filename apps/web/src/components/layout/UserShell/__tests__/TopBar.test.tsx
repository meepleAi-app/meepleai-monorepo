import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { TopBar } from '../TopBar';

vi.mock('next/navigation', () => ({
  usePathname: () => '/',
}));

vi.mock('@/hooks/useChatPanel', () => ({
  useChatPanel: () => ({
    isOpen: false,
    gameContext: null,
    open: vi.fn(),
    close: vi.fn(),
    setGameContext: vi.fn(),
  }),
}));

// Mock the notification bell (existing component with runtime deps)
vi.mock('@/components/notifications', () => ({
  NotificationBell: () => <button aria-label="Notifications">🔔</button>,
}));

vi.mock('@/components/layout/UserMenuDropdown', () => ({
  UserMenuDropdown: () => <button aria-label="User menu">MR</button>,
}));

describe('TopBar', () => {
  it('renders logo, nav links, search, chat button, and user menu', () => {
    render(<TopBar />);
    expect(screen.getByRole('link', { name: /meepleai home/i })).toBeInTheDocument();
    // Desktop NavLinks + mobile drawer both render the same labels (media-query hidden, still in DOM)
    expect(screen.getAllByRole('link', { name: 'Home' }).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /search/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /chat agente/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /notifications/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /user menu/i })).toBeInTheDocument();
  });

  it('renders mobile hamburger menu button', () => {
    render(<TopBar />);
    expect(screen.getByRole('button', { name: /apri menu/i })).toBeInTheDocument();
  });

  it('is 64px tall and sticky', () => {
    const { container } = render(<TopBar />);
    const header = container.querySelector('header');
    expect(header).toHaveClass('h-16');
    expect(header).toHaveClass('sticky');
  });
});
