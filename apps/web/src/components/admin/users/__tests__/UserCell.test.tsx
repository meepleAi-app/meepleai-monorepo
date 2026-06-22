import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { UserCell } from '../UserCell';

describe('UserCell', () => {
  it('renders displayName, email and an avatar initial', () => {
    render(<UserCell user={{ displayName: 'Aaron D.', email: 'aaron@example.com' }} />);
    expect(screen.getByText('Aaron D.')).toBeInTheDocument();
    expect(screen.getByText('aaron@example.com')).toBeInTheDocument();
    expect(screen.getByTestId('user-cell-avatar')).toHaveTextContent('A');
  });

  it('falls back to email initial when displayName is null', () => {
    render(<UserCell user={{ displayName: null, email: 'maria@example.com' }} />);
    expect(screen.getByText('maria@example.com')).toBeInTheDocument();
    expect(screen.getByTestId('user-cell-avatar')).toHaveTextContent('M');
  });

  it('truncates long emails via CSS class', () => {
    render(<UserCell user={{ displayName: 'X', email: 'very.long.email.address@example.com' }} />);
    const email = screen.getByText('very.long.email.address@example.com');
    expect(email).toHaveClass('truncate');
  });
});
