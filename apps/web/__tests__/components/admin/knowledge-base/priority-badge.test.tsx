import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { PriorityBadge } from '@/components/admin/knowledge-base/priority-badge';

describe('PriorityBadge', () => {
  it('renders urgent badge (priority >= 30)', () => {
    render(<PriorityBadge priority={30} />);
    expect(screen.getByTestId('priority-badge-urgent')).toBeInTheDocument();
    expect(screen.getByText('Urgente')).toBeInTheDocument();
  });

  it('renders high badge (priority >= 20)', () => {
    render(<PriorityBadge priority={20} />);
    expect(screen.getByTestId('priority-badge-high')).toBeInTheDocument();
    expect(screen.getByText('Alta')).toBeInTheDocument();
  });

  it('does not render normal badge by default (priority >= 10)', () => {
    const { container } = render(<PriorityBadge priority={10} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders normal badge when showNormal is true', () => {
    render(<PriorityBadge priority={10} showNormal />);
    expect(screen.getByTestId('priority-badge-normal')).toBeInTheDocument();
    expect(screen.getByText('Normale')).toBeInTheDocument();
  });

  it('renders low badge (priority < 10)', () => {
    render(<PriorityBadge priority={0} />);
    expect(screen.getByTestId('priority-badge-low')).toBeInTheDocument();
    expect(screen.getByText('Bassa')).toBeInTheDocument();
  });

  it('applies correct styles for urgent', () => {
    render(<PriorityBadge priority={30} />);
    const badge = screen.getByTestId('priority-badge-urgent');
    expect(badge.className).toContain('bg-red-50');
    expect(badge.className).toContain('text-red-700');
  });
});
