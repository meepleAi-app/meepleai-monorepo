/**
 * AdminAgentChat Tests (Task #3)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { AdminAgentChat } from '../AdminAgentChat';

// Mock AgentChat component
vi.mock('@/components/agent/AgentChat', () => ({
  AgentChat: ({ agentName }: { agentName: string }) => (
    <div data-testid="agent-chat">Chat with {agentName}</div>
  ),
}));

describe('AdminAgentChat', () => {
  const defaultProps = {
    agentId: 'agent-123',
    agentName: 'Test Agent',
    channelEnabled: true,
  };

  it('renders chat when channel is enabled', () => {
    render(<AdminAgentChat {...defaultProps} />);

    expect(screen.getByText(/Agent Chat/i)).toBeInTheDocument();
    expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
  });

  it('shows message when channel is disabled', () => {
    render(<AdminAgentChat {...defaultProps} channelEnabled={false} />);

    expect(screen.getByText(/Enable channel to use chat feature/i)).toBeInTheDocument();
    expect(screen.queryByTestId('agent-chat')).not.toBeInTheDocument();
  });

  it('has popout window button', () => {
    render(<AdminAgentChat {...defaultProps} />);

    const popoutBtn = screen.getByRole('button', { name: /popout window/i });
    expect(popoutBtn).toBeInTheDocument();
  });

  it('opens popout window when button clicked', () => {
    const mockOpen = vi.fn();
    window.open = mockOpen;

    render(<AdminAgentChat {...defaultProps} />);

    const popoutBtn = screen.getByRole('button', { name: /popout window/i });
    fireEvent.click(popoutBtn);

    expect(mockOpen).toHaveBeenCalledWith(
      expect.stringContaining('/admin/agent-definitions/agent-123/chat-popout'),
      'AgentChat',
      expect.stringContaining('width=800,height=600')
    );
  });

  it('shows chat component when channel enabled', () => {
    render(<AdminAgentChat {...defaultProps} />);

    expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
  });
});
