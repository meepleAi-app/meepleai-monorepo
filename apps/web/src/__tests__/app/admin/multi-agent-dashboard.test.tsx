/**
 * ISSUE-3778: Multi-Agent Dashboard Tests
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import MultiAgentDashboardPage from '@/app/(protected)/admin/ai-lab/multi-agent/page';

describe('Multi-Agent Dashboard', () => {
  it('renders dashboard title', () => {
    render(<MultiAgentDashboardPage />);
    expect(screen.getByText('Multi-Agent Dashboard')).toBeInTheDocument();
  });

  it('shows summary cards', () => {
    render(<MultiAgentDashboardPage />);
    expect(screen.getByText('Total Queries')).toBeInTheDocument();
    expect(screen.getByText('Total Cost')).toBeInTheDocument();
    expect(screen.getByText('Avg Response Time')).toBeInTheDocument();
  });

  it('displays all 3 agent cards', () => {
    render(<MultiAgentDashboardPage />);
    // Agent names appear in multiple places (selector badge + card title)
    expect(screen.getAllByText('Tutor').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Arbitro').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Decisore').length).toBeGreaterThan(0);
  });

  it('shows agent status indicators', () => {
    render(<MultiAgentDashboardPage />);
    const healthyBadges = screen.getAllByText('Healthy');
    expect(healthyBadges.length).toBeGreaterThan(0);
  });

  it('displays cost trend chart', () => {
    render(<MultiAgentDashboardPage />);
    expect(screen.getByText(/cost trend/i)).toBeInTheDocument();
  });
});
