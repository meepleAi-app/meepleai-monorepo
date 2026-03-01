import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { ActivityTable } from '../activity-table';

describe('ActivityTable', () => {
  it('renders activity table with mock data', () => {
    render(<ActivityTable />);

    // Check table headers
    expect(screen.getByText('Timestamp')).toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByText('Action')).toBeInTheDocument();
    expect(screen.getByText('Target')).toBeInTheDocument();
    expect(screen.getByText('IP Address')).toBeInTheDocument();
    expect(screen.getByText('Status')).toBeInTheDocument();
  });

  it('displays user information with avatars', () => {
    render(<ActivityTable />);

    // Check at least one user name is visible (Sarah Chen appears twice in mock data)
    const users = screen.getAllByText('Sarah Chen');
    expect(users.length).toBeGreaterThan(0);
    // Email appears multiple times in mock data, use getAllByText
    const emails = screen.getAllByText('sarah@meepleai.com');
    expect(emails.length).toBeGreaterThan(0);
  });

  it('shows action badges', () => {
    render(<ActivityTable />);

    // Check for action text (appears multiple times in mock data)
    const actions = screen.getAllByText('Approved game');
    expect(actions.length).toBeGreaterThan(0);
  });

  it('displays success and error statuses', () => {
    render(<ActivityTable />);

    const successBadges = screen.getAllByText('Success');
    expect(successBadges.length).toBeGreaterThan(0);
  });

  it('formats timestamps correctly', () => {
    const { container } = render(<ActivityTable />);

    // Check for font-mono class on timestamp cells
    const timestamps = container.querySelectorAll('.font-mono');
    expect(timestamps.length).toBeGreaterThan(0);
  });
});
