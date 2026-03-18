import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { KPIStatsRow } from '../KPIStatsRow';

describe('KPIStatsRow', () => {
  const baseProps = {
    totalGames: 42,
    totalUsers: 150,
    pendingApprovals: 0,
  };

  it('renders games card with correct value', () => {
    render(<KPIStatsRow {...baseProps} />);

    expect(screen.getByTestId('kpi-games')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-games-title')).toHaveTextContent('Giochi');
    expect(screen.getByTestId('kpi-games-value')).toHaveTextContent('42');
  });

  it('renders users card with correct value', () => {
    render(<KPIStatsRow {...baseProps} />);

    expect(screen.getByTestId('kpi-users')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-users-title')).toHaveTextContent('Utenti');
    expect(screen.getByTestId('kpi-users-value')).toHaveTextContent('150');
  });

  it('hides pending card when pendingApprovals is 0', () => {
    render(<KPIStatsRow {...baseProps} pendingApprovals={0} />);

    expect(screen.queryByTestId('kpi-pending')).not.toBeInTheDocument();
  });

  it('shows pending card when pendingApprovals > 0', () => {
    render(<KPIStatsRow {...baseProps} pendingApprovals={5} />);

    expect(screen.getByTestId('kpi-pending')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-pending-title')).toHaveTextContent('Pendenti');
    expect(screen.getByTestId('kpi-pending-value')).toHaveTextContent('5');
  });

  it('hides documents card when totalDocuments is not provided', () => {
    render(<KPIStatsRow {...baseProps} />);

    expect(screen.queryByTestId('kpi-documents')).not.toBeInTheDocument();
  });

  it('shows documents card when totalDocuments is provided', () => {
    render(<KPIStatsRow {...baseProps} totalDocuments={30} />);

    expect(screen.getByTestId('kpi-documents')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-documents-title')).toHaveTextContent('Documenti');
    expect(screen.getByTestId('kpi-documents-value')).toHaveTextContent('30');
  });

  it('shows queue depth badge on documents card when queueDepth > 0', () => {
    render(<KPIStatsRow {...baseProps} totalDocuments={30} queueDepth={3} />);

    const badge = screen.getByTestId('kpi-documents-badge');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveTextContent('3 in coda');
  });

  it('does not show queue depth badge when queueDepth is 0', () => {
    render(<KPIStatsRow {...baseProps} totalDocuments={30} queueDepth={0} />);

    expect(screen.queryByTestId('kpi-documents-badge')).not.toBeInTheDocument();
  });

  it('renders all four cards when all data is provided', () => {
    render(
      <KPIStatsRow
        totalGames={10}
        totalUsers={200}
        pendingApprovals={3}
        totalDocuments={50}
        queueDepth={2}
      />
    );

    expect(screen.getByTestId('kpi-games')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-documents')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-users')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-pending')).toBeInTheDocument();
  });

  it('renders only games and users cards by default', () => {
    render(<KPIStatsRow totalGames={10} totalUsers={200} pendingApprovals={0} />);

    expect(screen.getByTestId('kpi-games')).toBeInTheDocument();
    expect(screen.getByTestId('kpi-users')).toBeInTheDocument();
    expect(screen.queryByTestId('kpi-documents')).not.toBeInTheDocument();
    expect(screen.queryByTestId('kpi-pending')).not.toBeInTheDocument();
  });
});
