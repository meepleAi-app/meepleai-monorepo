import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { Input } from '../primitives/input';

describe('Input', () => {
  it('renders with base styles and forwards props', () => {
    render(<Input placeholder="Email" data-testid="inp" defaultValue="me@x.com" />);
    const input = screen.getByTestId('inp');
    expect(input).toHaveAttribute('value', 'me@x.com');
    expect(input.className).toMatch(/rounded/);
  });

  it('supports disabled state', () => {
    render(<Input aria-label="disabled input" disabled />);
    const input = screen.getByLabelText('disabled input');
    expect(input).toBeDisabled();
  });
});
