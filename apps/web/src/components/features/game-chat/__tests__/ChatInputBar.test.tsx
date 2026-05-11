import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ChatInputBar } from '../ChatInputBar';

describe('ChatInputBar', () => {
  it('renders input + send button', () => {
    render(
      <ChatInputBar
        value=""
        onChange={vi.fn()}
        onSubmit={vi.fn()}
        placeholder="Chiedi una regola…"
      />
    );
    expect(screen.getByPlaceholderText('Chiedi una regola…')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /invia/i })).toBeInTheDocument();
  });

  it('calls onChange when typing', () => {
    const onChange = vi.fn();
    render(<ChatInputBar value="" onChange={onChange} onSubmit={vi.fn()} />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'wing' } });
    expect(onChange).toHaveBeenCalledWith('wing');
  });

  it('calls onSubmit on Enter with trimmed value', () => {
    const onSubmit = vi.fn();
    render(<ChatInputBar value="  ciao  " onChange={vi.fn()} onSubmit={onSubmit} />);
    fireEvent.keyDown(screen.getByRole('textbox'), { key: 'Enter', code: 'Enter' });
    expect(onSubmit).toHaveBeenCalledWith('ciao');
  });

  it('does NOT submit when value is whitespace only', () => {
    const onSubmit = vi.fn();
    render(<ChatInputBar value="   " onChange={vi.fn()} onSubmit={onSubmit} />);
    fireEvent.keyDown(screen.getByRole('textbox'), { key: 'Enter' });
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('disables input and button when disabled prop true', () => {
    render(<ChatInputBar value="x" onChange={vi.fn()} onSubmit={vi.fn()} disabled />);
    expect(screen.getByRole('textbox')).toBeDisabled();
    expect(screen.getByRole('button', { name: /invia/i })).toBeDisabled();
  });

  it('calls onSubmit on send button click', () => {
    const onSubmit = vi.fn();
    render(<ChatInputBar value="ciao" onChange={vi.fn()} onSubmit={onSubmit} />);
    fireEvent.click(screen.getByRole('button', { name: /invia/i }));
    expect(onSubmit).toHaveBeenCalledWith('ciao');
  });
});
