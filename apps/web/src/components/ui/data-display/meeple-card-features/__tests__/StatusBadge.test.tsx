/**
 * StatusBadge Tests - Issue #3826
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusBadge } from '../StatusBadge';

describe('StatusBadge', () => {
  describe('Rendering', () => {
    it('should render single status', () => {
      render(<StatusBadge status="owned" />);
      expect(screen.getByTestId('status-badge-owned')).toBeInTheDocument();
      expect(screen.getByText('Posseduto')).toBeInTheDocument();
    });

    it('should render multiple statuses', () => {
      render(<StatusBadge status={['owned', 'played']} />);
      expect(screen.getByTestId('status-badge-owned')).toBeInTheDocument();
      expect(screen.getByTestId('status-badge-played')).toBeInTheDocument();
    });

    it('should render all 5 status types', () => {
      const statuses = ['owned', 'wishlisted', 'played', 'borrowed', 'for-trade'] as const;
      render(<StatusBadge status={statuses} />);

      statuses.forEach((s) => {
        expect(screen.getByTestId(`status-badge-${s}`)).toBeInTheDocument();
      });
    });

    it('should show icon when showIcon=true', () => {
      render(<StatusBadge status="owned" showIcon={true} />);
      const badge = screen.getByTestId('status-badge-owned');
      expect(badge.querySelector('svg')).toBeInTheDocument();
    });

    it('should hide icon when showIcon=false', () => {
      render(<StatusBadge status="owned" showIcon={false} />);
      const badge = screen.getByTestId('status-badge-owned');
      expect(badge.querySelector('svg')).not.toBeInTheDocument();
    });

    it('should return null for empty array', () => {
      const { container } = render(<StatusBadge status={[]} />);
      expect(container.firstChild).toBeNull();
    });

    it('should apply correct colors for each status', () => {
      const { rerender } = render(<StatusBadge status="owned" />);
      expect(screen.getByTestId('status-badge-owned')).toHaveClass('bg-teal-100');

      rerender(<StatusBadge status="wishlisted" />);
      expect(screen.getByTestId('status-badge-wishlisted')).toHaveClass('bg-amber-100');

      rerender(<StatusBadge status="played" />);
      expect(screen.getByTestId('status-badge-played')).toHaveClass('bg-blue-100');
    });
  });
});
