import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { Textarea } from '../primitives/textarea';

describe('Textarea', () => {
  it('renders textarea with classes and rows', () => {
    render(<Textarea aria-label="message" rows={4} defaultValue="hello" />);
    const area = screen.getByLabelText('message');
    expect(area).toHaveAttribute('rows', '4');
    expect(area).toHaveClass('rounded-md');
  });

  it('respects disabled prop', () => {
    render(<Textarea aria-label="disabled text" disabled />);
    expect(screen.getByLabelText('disabled text')).toBeDisabled();
  });
});
