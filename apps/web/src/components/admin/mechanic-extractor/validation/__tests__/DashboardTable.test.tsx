/**
 * @vitest-environment jsdom
 */
import { render, screen, within } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { ValidationDashboardRowDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

import { DashboardTable } from '../DashboardTable';

// Next.js Link — provide a minimal pass-through so href assertions work in jsdom.
vi.mock('next/link', () => ({
  __esModule: true,
  default: ({
    href,
    children,
    ...rest
  }: {
    href: string;
    children: React.ReactNode;
  } & React.AnchorHTMLAttributes<HTMLAnchorElement>) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

function row(
  overrides: Partial<ValidationDashboardRowDto> &
    Pick<ValidationDashboardRowDto, 'sharedGameId' | 'status'>
): ValidationDashboardRowDto {
  return {
    name: 'Game',
    overallScore: 0.5,
    lastComputedAt: '2026-04-25T10:00:00Z',
    ...overrides,
  };
}

describe('DashboardTable', () => {
  it('renders the empty state when no rows are provided', () => {
    render(<DashboardTable rows={[]} />);

    expect(screen.getByTestId('dashboard-table-empty')).toBeInTheDocument();
    expect(screen.getByTestId('dashboard-table-empty')).toHaveTextContent(/No games yet/i);
    expect(screen.queryByTestId('dashboard-table')).not.toBeInTheDocument();
  });

  it('renders one row per dashboard entry with name, status, score and computed date', () => {
    const rows: ValidationDashboardRowDto[] = [
      row({
        sharedGameId: '11111111-1111-1111-1111-111111111111',
        name: 'Wingspan',
        status: 'Certified',
        overallScore: 0.83,
        lastComputedAt: '2026-04-25T10:00:00Z',
      }),
      row({
        sharedGameId: '22222222-2222-2222-2222-222222222222',
        name: 'Brass',
        status: 'NotCertified',
        overallScore: 0.42,
        lastComputedAt: null,
      }),
    ];

    render(<DashboardTable rows={rows} />);

    const tableRows = screen.getAllByTestId('dashboard-table-row');
    expect(tableRows).toHaveLength(2);

    // Wingspan row — higher score → appears first.
    const first = within(tableRows[0]);
    expect(first.getByText('Wingspan')).toBeInTheDocument();
    expect(first.getByTestId('dashboard-row-status')).toHaveTextContent(/Certified/);
    expect(first.getByTestId('dashboard-row-overall')).toHaveTextContent('83%');
    expect(first.getByTestId('dashboard-row-last-computed')).toHaveTextContent('2026-04-25');

    // Brass row — null `lastComputedAt` renders as em-dash.
    const second = within(tableRows[1]);
    expect(second.getByText('Brass')).toBeInTheDocument();
    expect(second.getByTestId('dashboard-row-status')).toHaveTextContent(/Not certified/i);
    expect(second.getByTestId('dashboard-row-overall')).toHaveTextContent('42%');
    expect(second.getByTestId('dashboard-row-last-computed')).toHaveTextContent('\u2014');
  });

  it('sorts rows by overallScore descending, with null-ish scores pushed to the bottom', () => {
    const rows: ValidationDashboardRowDto[] = [
      row({
        sharedGameId: '11111111-1111-1111-1111-111111111111',
        name: 'Low',
        status: 'NotCertified',
        overallScore: 0.1,
      }),
      row({
        sharedGameId: '22222222-2222-2222-2222-222222222222',
        name: 'NullScore',
        status: 'NotEvaluated',
        overallScore: Number.NaN, // simulates null-ish numeric value in the DTO
      }),
      row({
        sharedGameId: '33333333-3333-3333-3333-333333333333',
        name: 'High',
        status: 'Certified',
        overallScore: 0.95,
      }),
      row({
        sharedGameId: '44444444-4444-4444-4444-444444444444',
        name: 'Mid',
        status: 'NotCertified',
        overallScore: 0.5,
      }),
    ];

    render(<DashboardTable rows={rows} />);

    const rowTexts = screen.getAllByTestId('dashboard-table-row').map(r => r.textContent ?? '');

    expect(rowTexts).toHaveLength(4);
    expect(rowTexts[0]).toContain('High');
    expect(rowTexts[1]).toContain('Mid');
    expect(rowTexts[2]).toContain('Low');
    expect(rowTexts[3]).toContain('NullScore');
  });

  it('renders a "View" link with href containing the sharedGameId query parameter', () => {
    const rows: ValidationDashboardRowDto[] = [
      row({
        sharedGameId: '11111111-1111-1111-1111-111111111111',
        name: 'Wingspan',
        status: 'Certified',
      }),
    ];

    render(<DashboardTable rows={rows} />);

    const link = screen.getByTestId('dashboard-row-view-link') as HTMLAnchorElement;
    expect(link).toHaveTextContent(/View/i);
    expect(link.getAttribute('href')).toContain(
      'sharedGameId=11111111-1111-1111-1111-111111111111'
    );
    expect(link.getAttribute('href')).toContain('/admin/knowledge-base/mechanic-extractor/review');
  });
});
