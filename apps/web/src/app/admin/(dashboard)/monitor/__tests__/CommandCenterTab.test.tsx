import { vi, describe, it, expect } from 'vitest';

vi.mock('@/components/admin/command-center/CommandCenterDashboard', () => ({
  CommandCenterDashboard: () => (
    <div data-testid="command-center-dashboard">Command Center Dashboard</div>
  ),
}));

import { render, screen } from '@testing-library/react';

import { CommandCenterTab } from '../CommandCenterTab';

describe('CommandCenterTab', () => {
  it('renders CommandCenterDashboard', () => {
    render(<CommandCenterTab />);

    expect(screen.getByTestId('command-center-dashboard')).toBeInTheDocument();
  });
});
