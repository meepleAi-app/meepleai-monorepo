import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { InputField } from './input-field';

describe('InputField', () => {
  it('renders label and input', () => {
    render(<InputField label="Email" value="" onChange={() => {}} />);
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
  });

  it('fires onChange with new value', () => {
    const onChange = vi.fn();
    render(<InputField label="Email" value="" onChange={onChange} />);
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'a@b.c' } });
    expect(onChange).toHaveBeenCalledWith('a@b.c');
  });

  it('shows error with aria-invalid and aria-describedby', () => {
    render(<InputField label="Email" value="" onChange={() => {}} error="Invalid" />);
    const input = screen.getByLabelText('Email');
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('Invalid');
    expect(input.getAttribute('aria-describedby')).toContain('error');
  });

  it('shows hint when no error', () => {
    render(<InputField label="Email" value="" onChange={() => {}} hint="Use work email" />);
    expect(screen.getByText('Use work email')).toBeInTheDocument();
  });

  it('hides hint when error present', () => {
    render(<InputField label="Email" value="" onChange={() => {}} hint="Hint text" error="Bad" />);
    expect(screen.queryByText('Hint text')).not.toBeInTheDocument();
  });

  it('renders right slot', () => {
    render(
      <InputField
        label="Email"
        value=""
        onChange={() => {}}
        right={<span data-testid="slot">x</span>}
      />
    );
    expect(screen.getByTestId('slot')).toBeInTheDocument();
  });
});
