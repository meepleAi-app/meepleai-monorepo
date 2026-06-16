import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { Stepper } from '../Stepper';

describe('Stepper', () => {
  const baseProps = {
    value: 5,
    onChange: vi.fn(),
    incrementAriaLabel: 'Aumenta',
    decrementAriaLabel: 'Diminuisci',
  };

  it('renders current value', () => {
    render(<Stepper {...baseProps} value={42} />);
    expect(screen.getByText('42')).toBeInTheDocument();
  });

  it('increment button calls onChange with value+step', () => {
    const onChange = vi.fn();
    render(<Stepper {...baseProps} value={5} onChange={onChange} step={2} />);
    fireEvent.click(screen.getByLabelText('Aumenta'));
    expect(onChange).toHaveBeenCalledWith(7);
  });

  it('decrement button calls onChange with value-step', () => {
    const onChange = vi.fn();
    render(<Stepper {...baseProps} value={5} onChange={onChange} step={2} />);
    fireEvent.click(screen.getByLabelText('Diminuisci'));
    expect(onChange).toHaveBeenCalledWith(3);
  });

  it('disables decrement at min', () => {
    render(<Stepper {...baseProps} value={0} min={0} />);
    expect(screen.getByLabelText('Diminuisci')).toBeDisabled();
  });

  it('disables increment at max', () => {
    render(<Stepper {...baseProps} value={10} max={10} />);
    expect(screen.getByLabelText('Aumenta')).toBeDisabled();
  });
});
