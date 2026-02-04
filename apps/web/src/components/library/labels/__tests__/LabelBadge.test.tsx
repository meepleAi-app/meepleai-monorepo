/**
 * LabelBadge Component Tests (Issue #3516)
 *
 * Coverage: LabelBadge component for displaying game labels
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { LabelBadge } from '../LabelBadge';
import type { LabelDto } from '@/lib/api/schemas/library.schemas';

const mockPredefinedLabel: LabelDto = {
  id: 'label-1',
  name: 'Strategy',
  color: '#3b82f6',
  isPredefined: true,
  createdAt: '2024-01-15T10:00:00Z',
};

const mockCustomLabel: LabelDto = {
  id: 'label-2',
  name: 'My Custom',
  color: '#22c55e',
  isPredefined: false,
  createdAt: '2024-01-16T10:00:00Z',
};

describe('LabelBadge - Issue #3516', () => {
  describe('rendering', () => {
    it('should render label name', () => {
      render(<LabelBadge label={mockPredefinedLabel} />);

      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });

    it('should apply label color as inline style', () => {
      render(<LabelBadge label={mockPredefinedLabel} />);

      const badge = screen.getByText('Strategy').closest('span');
      expect(badge).toHaveStyle({ color: '#3b82f6' });
    });

    it('should render predefined label', () => {
      render(<LabelBadge label={mockPredefinedLabel} />);

      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });

    it('should render custom label', () => {
      render(<LabelBadge label={mockCustomLabel} />);

      expect(screen.getByText('My Custom')).toBeInTheDocument();
    });
  });

  describe('remove button', () => {
    it('should show remove button when onRemove is provided', () => {
      const onRemove = vi.fn();
      render(<LabelBadge label={mockPredefinedLabel} onRemove={onRemove} />);

      expect(screen.getByRole('button', { name: /rimuovi etichetta strategy/i })).toBeInTheDocument();
    });

    it('should not show remove button when onRemove is not provided', () => {
      render(<LabelBadge label={mockPredefinedLabel} />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('should call onRemove with label id when remove button is clicked', () => {
      const onRemove = vi.fn();
      render(<LabelBadge label={mockPredefinedLabel} onRemove={onRemove} />);

      fireEvent.click(screen.getByRole('button', { name: /rimuovi etichetta strategy/i }));

      expect(onRemove).toHaveBeenCalledWith('label-1');
    });

    it('should not show remove button when disabled', () => {
      const onRemove = vi.fn();
      render(<LabelBadge label={mockPredefinedLabel} onRemove={onRemove} disabled />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });
  });

  describe('disabled state', () => {
    it('should apply disabled styling when disabled', () => {
      render(<LabelBadge label={mockPredefinedLabel} disabled />);

      const badge = screen.getByText('Strategy').closest('span');
      expect(badge).toHaveClass('opacity-50');
    });
  });

  describe('custom className', () => {
    it('should apply custom className', () => {
      render(<LabelBadge label={mockPredefinedLabel} className="custom-class" />);

      const badge = screen.getByText('Strategy').closest('span');
      expect(badge).toHaveClass('custom-class');
    });
  });
});
