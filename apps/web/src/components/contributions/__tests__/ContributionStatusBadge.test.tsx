/**
 * ContributionStatusBadge Component Tests
 *
 * Issue #2744: Frontend - Dashboard Contributi Utente
 *
 * Tests:
 * - Badge rendering for all statuses
 * - Icon display
 * - Color variants
 * - Custom className support
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ContributionStatusBadge } from '../ContributionStatusBadge';
import type { ShareRequestStatus } from '@/lib/api/schemas/share-requests.schemas';

describe('ContributionStatusBadge', () => {
  const statuses: ShareRequestStatus[] = [
    'Pending',
    'InReview',
    'ChangesRequested',
    'Approved',
    'Rejected',
    'Withdrawn',
  ];

  it.each(statuses)('renders %s status badge correctly', (status) => {
    render(<ContributionStatusBadge status={status} />);

    const badge = screen.getByText(
      status === 'ChangesRequested' ? 'Changes Requested' :
      status === 'InReview' ? 'In Review' :
      status
    );

    expect(badge).toBeInTheDocument();
  });

  it('renders icon by default', () => {
    const { container } = render(<ContributionStatusBadge status="Pending" />);
    const icon = container.querySelector('svg');
    expect(icon).toBeInTheDocument();
  });

  it('hides icon when showIcon=false', () => {
    const { container } = render(<ContributionStatusBadge status="Pending" showIcon={false} />);
    const icon = container.querySelector('svg');
    expect(icon).not.toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <ContributionStatusBadge status="Approved" className="custom-class" />
    );
    const badge = container.querySelector('.custom-class');
    expect(badge).toBeInTheDocument();
  });

  it('applies correct color for Pending status', () => {
    const { container } = render(<ContributionStatusBadge status="Pending" />);
    const badge = container.querySelector('.bg-yellow-50');
    expect(badge).toBeInTheDocument();
  });

  it('applies correct color for Approved status', () => {
    const { container } = render(<ContributionStatusBadge status="Approved" />);
    const badge = container.querySelector('.bg-green-50');
    expect(badge).toBeInTheDocument();
  });

  it('applies correct color for Rejected status', () => {
    const { container } = render(<ContributionStatusBadge status="Rejected" />);
    const badge = container.querySelector('.bg-red-50');
    expect(badge).toBeInTheDocument();
  });
});
