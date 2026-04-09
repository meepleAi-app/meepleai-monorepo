import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { ChatInputBar } from '../ChatInputBar';

describe('ChatInputBar', () => {
  it('renders textarea and send button', () => {
    render(<ChatInputBar placeholder="Chiedi una regola…" onSend={() => {}} />);
    expect(screen.getByRole('textbox')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /invia/i })).toBeInTheDocument();
  });

  it('calls onSend with trimmed value when send is clicked', async () => {
    const onSend = vi.fn();
    const user = userEvent.setup();
    render(<ChatInputBar onSend={onSend} />);
    const textarea = screen.getByRole('textbox');
    await user.type(textarea, '  come si vince?  ');
    await user.click(screen.getByRole('button', { name: /invia/i }));
    expect(onSend).toHaveBeenCalledWith('come si vince?');
  });

  it('does not call onSend when value is empty or whitespace', async () => {
    const onSend = vi.fn();
    const user = userEvent.setup();
    render(<ChatInputBar onSend={onSend} />);
    const textarea = screen.getByRole('textbox');
    await user.type(textarea, '   ');
    await user.click(screen.getByRole('button', { name: /invia/i }));
    expect(onSend).not.toHaveBeenCalled();
  });

  it('sends on Enter (no shift)', async () => {
    const onSend = vi.fn();
    const user = userEvent.setup();
    render(<ChatInputBar onSend={onSend} />);
    const textarea = screen.getByRole('textbox');
    await user.type(textarea, 'hello{Enter}');
    expect(onSend).toHaveBeenCalledWith('hello');
  });

  it('does NOT send on Shift+Enter (newline)', async () => {
    const onSend = vi.fn();
    const user = userEvent.setup();
    render(<ChatInputBar onSend={onSend} />);
    const textarea = screen.getByRole('textbox');
    await user.type(textarea, 'hello{Shift>}{Enter}{/Shift}');
    expect(onSend).not.toHaveBeenCalled();
  });
});
