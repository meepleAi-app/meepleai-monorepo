import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { OTPInput6Slot } from '../two-factor/OTPInput6Slot';

describe('OTPInput6Slot', () => {
  it('renders 6 numeric input slots', () => {
    render(<OTPInput6Slot onComplete={() => {}} />);
    expect(screen.getAllByRole('textbox')).toHaveLength(6);
  });

  it('auto-advances and calls onComplete with 6 digits', () => {
    const onComplete = vi.fn();
    render(<OTPInput6Slot onComplete={onComplete} />);
    const inputs = screen.getAllByRole('textbox');
    '123456'.split('').forEach((d, i) => fireEvent.change(inputs[i], { target: { value: d } }));
    expect(onComplete).toHaveBeenCalledWith('123456');
  });

  it('fills all slots from a pasted 6-digit code', () => {
    const onComplete = vi.fn();
    render(<OTPInput6Slot onComplete={onComplete} />);
    const inputs = screen.getAllByRole('textbox');
    fireEvent.paste(inputs[0], {
      clipboardData: { getData: () => '654321' },
    });
    expect(onComplete).toHaveBeenCalledWith('654321');
  });

  it('strips non-numeric characters from pasted input', () => {
    const onComplete = vi.fn();
    render(<OTPInput6Slot onComplete={onComplete} />);
    const inputs = screen.getAllByRole('textbox');
    fireEvent.paste(inputs[0], {
      clipboardData: { getData: () => '12-34-56' },
    });
    expect(onComplete).toHaveBeenCalledWith('123456');
  });

  it('shows error styling when error prop set', () => {
    render(<OTPInput6Slot onComplete={() => {}} error />);
    expect(screen.getByRole('group')).toHaveClass('animate-shake');
  });

  it('disables all inputs when disabled prop is set (G4)', () => {
    render(<OTPInput6Slot onComplete={() => {}} disabled />);
    screen.getAllByRole('textbox').forEach(i => expect(i).toBeDisabled());
  });

  it('moves focus to previous slot on Backspace from empty slot', () => {
    render(<OTPInput6Slot onComplete={() => {}} />);
    const inputs = screen.getAllByRole('textbox');
    // First fill slot 0 to move focus to slot 1
    fireEvent.change(inputs[0], { target: { value: '1' } });
    // inputs[1] should now be focused
    expect(document.activeElement).toBe(inputs[1]);
    // Backspace on empty slot 1 -> focus back to slot 0
    fireEvent.keyDown(inputs[1], { key: 'Backspace' });
    expect(document.activeElement).toBe(inputs[0]);
  });
});
