import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { OwnershipBadge } from '../OwnershipBadge';

describe('OwnershipBadge', () => {
  it('renders owned with CheckCircle2', () => {
    const { container } = render(<OwnershipBadge value="owned" />);
    expect(screen.getByTestId('ownership-badge-owned')).toBeInTheDocument();
    expect(container.querySelector('svg')).toBeTruthy();
  });

  it('renders wishlist with Heart', () => {
    render(<OwnershipBadge value="wishlist" />);
    expect(screen.getByTestId('ownership-badge-wishlist')).toBeInTheDocument();
  });

  it('renders archived with Archive', () => {
    render(<OwnershipBadge value="archived" />);
    expect(screen.getByTestId('ownership-badge-archived')).toBeInTheDocument();
  });
});
