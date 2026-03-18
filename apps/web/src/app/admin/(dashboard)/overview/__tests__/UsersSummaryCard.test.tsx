import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { UsersSummaryCard } from '../UsersSummaryCard';

const baseProps = {
  totalUsers: 200,
  activeUsers: 85,
  pendingInvitations: 3,
  recentUsers: [
    {
      id: 'u1',
      displayName: 'Alice Rossi',
      email: 'alice@example.com',
      createdAt: '2026-03-14T09:00:00Z',
    },
    { id: 'u2', displayName: null, email: 'bob@example.com', createdAt: '2026-03-13T15:30:00Z' },
  ],
};

describe('UsersSummaryCard', () => {
  it('renders header', () => {
    render(<UsersSummaryCard {...baseProps} />);

    expect(screen.getByText('Utenti')).toBeInTheDocument();
  });

  it('renders total users stat', () => {
    render(<UsersSummaryCard {...baseProps} />);

    expect(screen.getByTestId('users-total')).toHaveTextContent('200');
    expect(screen.getByText('totali')).toBeInTheDocument();
  });

  it('renders active users count', () => {
    render(<UsersSummaryCard {...baseProps} />);

    expect(screen.getByTestId('users-active')).toHaveTextContent('85 attivi 30gg');
  });

  it('renders pending invitations when > 0', () => {
    render(<UsersSummaryCard {...baseProps} pendingInvitations={3} />);

    expect(screen.getByTestId('users-pending-invitations')).toBeInTheDocument();
    expect(screen.getByTestId('users-pending-invitations')).toHaveTextContent('3 inviti pendenti');
  });

  it('hides pending invitations when 0', () => {
    render(<UsersSummaryCard {...baseProps} pendingInvitations={0} />);

    expect(screen.queryByTestId('users-pending-invitations')).not.toBeInTheDocument();
  });

  it('renders recent users with displayName when available', () => {
    render(<UsersSummaryCard {...baseProps} />);

    expect(screen.getByText('Alice Rossi')).toBeInTheDocument();
  });

  it('falls back to email when displayName is null', () => {
    render(<UsersSummaryCard {...baseProps} />);

    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
  });

  it('renders empty state when recentUsers is empty', () => {
    render(<UsersSummaryCard {...baseProps} recentUsers={[]} />);

    expect(screen.getByText('Nessun utente recente')).toBeInTheDocument();
  });

  it('renders manage users link with correct href', () => {
    render(<UsersSummaryCard {...baseProps} />);

    const link = screen.getByRole('link', { name: /gestisci utenti/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/admin/users');
  });
});
