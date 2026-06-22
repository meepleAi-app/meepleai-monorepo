import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { ConfidenceBadge } from '../ConfidenceBadge';

describe('ConfidenceBadge', () => {
  it('renders alta tier when score >= 0.80', () => {
    render(<ConfidenceBadge score={0.92} />);
    const badge = screen.getByRole('status');
    expect(badge).toHaveAttribute('data-tier', 'alta');
    expect(badge.textContent).toMatch(/0\.92/);
  });
  it('renders media tier when 0.50 <= score < 0.80', () => {
    render(<ConfidenceBadge score={0.65} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'media');
  });
  it('renders bassa tier when score < 0.50', () => {
    render(<ConfidenceBadge score={0.42} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'bassa');
  });
  it('boundary 0.80 = alta', () => {
    render(<ConfidenceBadge score={0.8} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'alta');
  });
  it('boundary 0.50 = media (not bassa)', () => {
    render(<ConfidenceBadge score={0.5} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'media');
  });
  it('edge 0.0 = bassa', () => {
    render(<ConfidenceBadge score={0.0} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'bassa');
  });
  it('edge 1.0 = alta', () => {
    render(<ConfidenceBadge score={1.0} />);
    expect(screen.getByRole('status')).toHaveAttribute('data-tier', 'alta');
  });
  it('compact kind hides numeric score', () => {
    render(<ConfidenceBadge score={0.92} kind="compact" />);
    expect(screen.getByRole('status').textContent).not.toMatch(/0\.92/);
  });
});
