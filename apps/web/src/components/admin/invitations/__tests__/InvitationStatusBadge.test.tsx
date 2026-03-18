import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { InvitationStatusBadge } from '../InvitationStatusBadge';

describe('InvitationStatusBadge', () => {
  it('renders Pending status with amber styling', () => {
    render(<InvitationStatusBadge status="Pending" />);

    const badge = screen.getByText('Pending');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveClass('border-amber-300');
    expect(badge).toHaveClass('bg-amber-50');
    expect(badge).toHaveClass('text-amber-900');
  });

  it('renders Accepted status with green styling', () => {
    render(<InvitationStatusBadge status="Accepted" />);

    const badge = screen.getByText('Accepted');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveClass('border-green-300');
    expect(badge).toHaveClass('bg-green-50');
    expect(badge).toHaveClass('text-green-900');
  });

  it('renders Expired status with slate styling', () => {
    render(<InvitationStatusBadge status="Expired" />);

    const badge = screen.getByText('Expired');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveClass('border-slate-300');
    expect(badge).toHaveClass('bg-slate-50');
    expect(badge).toHaveClass('text-slate-700');
  });

  it('renders Revoked status with red styling', () => {
    render(<InvitationStatusBadge status="Revoked" />);

    const badge = screen.getByText('Revoked');
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveClass('border-red-300');
    expect(badge).toHaveClass('bg-red-50');
    expect(badge).toHaveClass('text-red-900');
  });
});
