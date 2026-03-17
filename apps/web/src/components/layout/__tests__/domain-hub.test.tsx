import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { DomainHub } from '../DomainHub';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

describe('DomainHub', () => {
  it('renders 8 domain tiles', () => {
    render(<DomainHub userName="Marco" />);
    const tiles = screen.getAllByRole('link');
    expect(tiles).toHaveLength(8);
  });

  it('renders all domain labels', () => {
    render(<DomainHub userName="Marco" />);
    expect(screen.getByText('Games')).toBeInTheDocument();
    expect(screen.getByText('Agents')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Library')).toBeInTheDocument();
    expect(screen.getByText('Leaderboards')).toBeInTheDocument();
    expect(screen.getByText('Sessions')).toBeInTheDocument();
    expect(screen.getByText('Notifications')).toBeInTheDocument();
    expect(screen.getByText('Settings')).toBeInTheDocument();
  });

  it('renders 2-column grid', () => {
    const { container } = render(<DomainHub userName="Marco" />);
    const grid = container.querySelector('[class*="grid-cols-2"]');
    expect(grid).not.toBeNull();
  });

  it('shows greeting with user name', () => {
    render(<DomainHub userName="Marco" />);
    expect(screen.getByText(/Marco/)).toBeInTheDocument();
  });

  it('does not show greeting without user name', () => {
    render(<DomainHub />);
    expect(screen.queryByText(/Welcome/)).toBeNull();
  });
});
