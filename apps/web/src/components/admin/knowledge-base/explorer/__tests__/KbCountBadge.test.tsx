import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { KbCountBadge } from '../KbCountBadge';

describe('KbCountBadge', () => {
  it('renders a loading skeleton when loading and count is undefined', () => {
    const { container } = render(<KbCountBadge count={undefined} loading={true} testId="badge" />);
    expect(container.querySelector('[data-testid="badge-loading"]')).toBeInTheDocument();
  });

  it('renders 0 with muted style when count is 0', () => {
    render(<KbCountBadge count={0} loading={false} testId="badge" />);
    const badge = screen.getByTestId('badge');
    expect(badge).toHaveTextContent('0');
    expect(badge.className).toContain('bg-muted');
  });

  it('renders 23 with amber style when count is greater than 0', () => {
    render(<KbCountBadge count={23} loading={false} testId="badge" />);
    const badge = screen.getByTestId('badge');
    expect(badge).toHaveTextContent('23');
    expect(badge.className).toContain('bg-amber-500/15');
  });

  it('renders "99+" when count exceeds 99', () => {
    render(<KbCountBadge count={150} loading={false} testId="badge" />);
    expect(screen.getByTestId('badge')).toHaveTextContent('99+');
  });

  it('falls back to 0 with muted style when count is undefined and not loading', () => {
    render(<KbCountBadge count={undefined} loading={false} testId="badge" />);
    const badge = screen.getByTestId('badge');
    expect(badge).toHaveTextContent('0');
    expect(badge.className).toContain('bg-muted');
  });

  it('applies the title attribute from the tooltip prop', () => {
    render(<KbCountBadge count={1} loading={false} tooltip="hello" testId="badge" />);
    expect(screen.getByTestId('badge')).toHaveAttribute('title', 'hello');
  });

  it('omits data-testid when not provided', () => {
    const { container } = render(<KbCountBadge count={1} loading={false} />);
    expect(container.querySelector('[data-testid]')).toBeNull();
  });

  it('renders em-dash with muted style when isError is true and count is undefined', () => {
    render(<KbCountBadge count={undefined} loading={false} isError={true} testId="badge" />);
    const badge = screen.getByTestId('badge');
    expect(badge).toHaveTextContent('—');
    expect(badge).toHaveAttribute('aria-label', 'conteggio non disponibile');
    expect(badge.className).toContain('bg-muted');
  });
});
