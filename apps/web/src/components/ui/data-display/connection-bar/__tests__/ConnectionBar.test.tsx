import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { Bot, FileText, MessageCircle, Dices } from 'lucide-react';
import { ConnectionBar } from '../ConnectionBar';
import type { ConnectionPip } from '../types';

const mockPips: ConnectionPip[] = [
  { entityType: 'agent', count: 1, label: 'Agent', icon: Bot, isEmpty: false },
  { entityType: 'kb', count: 3, label: 'KB', icon: FileText, isEmpty: false },
  { entityType: 'chat', count: 0, label: 'Chat', icon: MessageCircle, isEmpty: true },
  { entityType: 'session', count: 23, label: 'Sessioni', icon: Dices, isEmpty: false },
];

describe('ConnectionBar', () => {
  it('renders a pill for each connection', () => {
    render(<ConnectionBar connections={mockPips} onPipClick={vi.fn()} />);
    expect(screen.getByText('Agent')).toBeInTheDocument();
    expect(screen.getByText('KB')).toBeInTheDocument();
    expect(screen.getByText('Chat')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
  });

  it('shows count badge when count > 0', () => {
    render(<ConnectionBar connections={mockPips} onPipClick={vi.fn()} />);
    expect(screen.getByText('3')).toBeInTheDocument(); // KB count
    expect(screen.getByText('23')).toBeInTheDocument(); // Session count
  });

  it('shows "+" indicator when isEmpty is true', () => {
    render(<ConnectionBar connections={mockPips} onPipClick={vi.fn()} />);
    const chatPip = screen.getByTestId('connection-pip-chat');
    expect(chatPip).toHaveTextContent('+');
  });

  it('calls onPipClick with pip and anchorRect', async () => {
    const handler = vi.fn();
    render(<ConnectionBar connections={mockPips} onPipClick={handler} />);
    await userEvent.click(screen.getByTestId('connection-pip-kb'));
    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler.mock.calls[0][0].entityType).toBe('kb');
    expect(handler.mock.calls[0][1]).toBeInstanceOf(DOMRect);
  });

  it('renders nothing when connections array is empty', () => {
    const { container } = render(<ConnectionBar connections={[]} onPipClick={vi.fn()} />);
    expect(container.firstChild).toBeNull();
  });
});
