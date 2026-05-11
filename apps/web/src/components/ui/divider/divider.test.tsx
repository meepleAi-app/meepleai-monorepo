import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Divider } from './divider';

describe('Divider', () => {
  it('renders without label by default (single continuous bar)', () => {
    render(<Divider />);
    const separator = screen.getByRole('separator');
    expect(separator).toBeInTheDocument();
    // No label rendered — only the bar <div>
    expect(separator.textContent ?? '').toBe('');
  });

  it('renders custom label', () => {
    render(<Divider label="or" />);
    expect(screen.getByText('or')).toBeInTheDocument();
  });

  it('has role separator with horizontal orientation', () => {
    render(<Divider />);
    expect(screen.getByRole('separator')).toHaveAttribute('aria-orientation', 'horizontal');
  });

  it('label is aria-hidden', () => {
    render(<Divider label="oppure" />);
    expect(screen.getByText('oppure')).toHaveAttribute('aria-hidden', 'true');
  });
});
