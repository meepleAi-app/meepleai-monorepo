/**
 * @vitest-environment jsdom
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import type { ValidationDashboardRowDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { DashboardSummaryCards } from '../DashboardSummaryCards';

function row(
  overrides: Partial<ValidationDashboardRowDto> &
    Pick<ValidationDashboardRowDto, 'sharedGameId' | 'status'>
): ValidationDashboardRowDto {
  return {
    name: 'Wingspan',
    overallScore: 0.8,
    lastComputedAt: '2026-04-25T10:00:00Z',
    ...overrides,
  };
}

describe('DashboardSummaryCards', () => {
  it('renders all three tiles with 0 when the rows array is empty', () => {
    render(<DashboardSummaryCards rows={[]} />);

    expect(screen.getByTestId('dashboard-summary-certified-count')).toHaveTextContent('0');
    expect(screen.getByTestId('dashboard-summary-not-certified-count')).toHaveTextContent('0');
    expect(screen.getByTestId('dashboard-summary-not-evaluated-count')).toHaveTextContent('0');
  });

  it('computes counts correctly for a mixed rows array', () => {
    const rows: ValidationDashboardRowDto[] = [
      row({ sharedGameId: '11111111-1111-1111-1111-111111111111', status: 'Certified' }),
      row({ sharedGameId: '22222222-2222-2222-2222-222222222222', status: 'Certified' }),
      row({ sharedGameId: '33333333-3333-3333-3333-333333333333', status: 'NotCertified' }),
      row({ sharedGameId: '44444444-4444-4444-4444-444444444444', status: 'NotEvaluated' }),
      row({ sharedGameId: '55555555-5555-5555-5555-555555555555', status: 'NotEvaluated' }),
      row({ sharedGameId: '66666666-6666-6666-6666-666666666666', status: 'NotEvaluated' }),
    ];

    render(<DashboardSummaryCards rows={rows} />);

    expect(screen.getByTestId('dashboard-summary-certified-count')).toHaveTextContent('2');
    expect(screen.getByTestId('dashboard-summary-not-certified-count')).toHaveTextContent('1');
    expect(screen.getByTestId('dashboard-summary-not-evaluated-count')).toHaveTextContent('3');
  });

  it('shows other tiles as 0 when all rows share a single status', () => {
    const rows: ValidationDashboardRowDto[] = [
      row({ sharedGameId: '11111111-1111-1111-1111-111111111111', status: 'Certified' }),
      row({ sharedGameId: '22222222-2222-2222-2222-222222222222', status: 'Certified' }),
      row({ sharedGameId: '33333333-3333-3333-3333-333333333333', status: 'Certified' }),
    ];

    render(<DashboardSummaryCards rows={rows} />);

    expect(screen.getByTestId('dashboard-summary-certified-count')).toHaveTextContent('3');
    expect(screen.getByTestId('dashboard-summary-not-certified-count')).toHaveTextContent('0');
    expect(screen.getByTestId('dashboard-summary-not-evaluated-count')).toHaveTextContent('0');
  });
});
