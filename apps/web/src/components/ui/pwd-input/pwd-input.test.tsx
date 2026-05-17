import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { PwdInput } from './pwd-input';

describe('PwdInput', () => {
  it('renders as password input by default', () => {
    render(<PwdInput label="Password" value="secret" onChange={() => {}} />);
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  });

  it('toggles visibility when eye button clicked', () => {
    render(<PwdInput label="Password" value="secret" onChange={() => {}} />);
    const toggle = screen.getByRole('button', { name: /mostra password/i });
    fireEvent.click(toggle);
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'text');
    expect(toggle).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(toggle);
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  });

  it('fires onChange with new value', () => {
    const onChange = vi.fn();
    render(<PwdInput label="Password" value="" onChange={onChange} />);
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'abc' } });
    expect(onChange).toHaveBeenCalledWith('abc');
  });

  it('shows strength meter when showStrength and value present', () => {
    render(<PwdInput label="Password" value="abc123" onChange={() => {}} showStrength />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('hides strength meter when showStrength false', () => {
    render(<PwdInput label="Password" value="abc123" onChange={() => {}} />);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('hides strength meter when value empty', () => {
    render(<PwdInput label="Password" value="" onChange={() => {}} showStrength />);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('shows error with aria-invalid', () => {
    render(<PwdInput label="Password" value="" onChange={() => {}} error="Required" />);
    expect(screen.getByLabelText('Password')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('alert')).toHaveTextContent('Required');
  });
});
