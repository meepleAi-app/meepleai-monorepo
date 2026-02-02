/**
 * ApprovalStatusBadge Tests (Issue #3482)
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { ApprovalStatusBadge } from '../ApprovalStatusBadge';

describe('ApprovalStatusBadge', () => {
  it('renders Draft status correctly', () => {
    render(<ApprovalStatusBadge status="Draft" />);
    expect(screen.getByText('Bozza')).toBeInTheDocument();
  });

  it('renders PendingReview status correctly', () => {
    render(<ApprovalStatusBadge status="PendingReview" />);
    expect(screen.getByText('In Revisione')).toBeInTheDocument();
  });

  it('renders Approved status correctly', () => {
    render(<ApprovalStatusBadge status="Approved" />);
    expect(screen.getByText('Approvato')).toBeInTheDocument();
  });

  it('renders Rejected status correctly', () => {
    render(<ApprovalStatusBadge status="Rejected" />);
    expect(screen.getByText('Rifiutato')).toBeInTheDocument();
  });

  it('handles unknown status gracefully', () => {
    render(<ApprovalStatusBadge status={'Unknown' as any} />);
    expect(screen.getByText(/Sconosciuto/)).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(<ApprovalStatusBadge status="Draft" className="custom-class" />);
    const badge = container.querySelector('.custom-class');
    expect(badge).toBeTruthy();
  });
});
