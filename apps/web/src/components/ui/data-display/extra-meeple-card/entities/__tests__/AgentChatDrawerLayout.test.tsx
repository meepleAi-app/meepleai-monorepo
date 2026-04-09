import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { AgentChatDrawerLayout } from '../AgentChatDrawerLayout';
import type { AgentDetailData } from '../../types';
import * as useAgentDataModule from '@/hooks/queries/useAgentData';

vi.mock('@/hooks/queries/useAgentData', () => ({
  useAgentThreads: vi.fn(() => ({ data: [], isLoading: false })),
  useAgentKbDocs: vi.fn(() => ({ data: [], isLoading: false })),
}));
vi.mock('@/hooks/useAgentStatus', () => ({
  useAgentStatus: () => ({ status: null, isLoading: false, error: null }),
}));
vi.mock('@/components/chat-unified/ChatThreadView', () => ({
  ChatThreadView: ({ threadId }: { threadId: string }) => (
    <div data-testid="chat-thread-view" data-thread-id={threadId} />
  ),
}));

const mockAgent: AgentDetailData = {
  id: 'agent-1',
  name: 'Azul Assistant',
  type: 'qa',
  strategyName: 'hybrid-rag',
  strategyParameters: {},
  isActive: true,
  isIdle: false,
  invocationCount: 3,
  lastInvokedAt: null,
  createdAt: '2024-01-01T00:00:00Z',
  gameId: 'game-1',
  gameName: 'Azul',
};

describe('AgentChatDrawerLayout', () => {
  it('renders sidebar and chat area in 2-column layout', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByTestId('agent-chat-sidebar')).toBeInTheDocument();
    expect(screen.getByTestId('agent-chat-area')).toBeInTheDocument();
  });

  it('shows game name in sidebar context', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('shows "+ Nuova chat" button', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByTestId('new-chat-button')).toBeInTheDocument();
  });

  it('shows CHAT RECENTI section label', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByText('CHAT RECENTI')).toBeInTheDocument();
  });

  it('shows KNOWLEDGE BASE section label', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByText('KNOWLEDGE BASE')).toBeInTheDocument();
  });

  it('shows empty state when no game linked', () => {
    const noGameAgent = { ...mockAgent, gameId: undefined, gameName: undefined };
    render(<AgentChatDrawerLayout data={noGameAgent} />);
    expect(screen.getByTestId('no-game-placeholder')).toBeInTheDocument();
  });

  it('shows thread preview in recent chats when threads exist', () => {
    vi.mocked(useAgentDataModule.useAgentThreads).mockReturnValue({
      data: [{ id: 't1', createdAt: '2024-03-01T00:00:00Z', messageCount: 5, firstMessagePreview: 'Qual è la regola finale?' }],
      isLoading: false,
    } as ReturnType<typeof useAgentDataModule.useAgentThreads>);
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByText('Qual è la regola finale?')).toBeInTheDocument();
  });

  it('clicking a recent thread updates chat area data-thread-id', async () => {
    const user = userEvent.setup();
    vi.mocked(useAgentDataModule.useAgentThreads).mockReturnValue({
      data: [{ id: 't1', createdAt: '2024-03-01T00:00:00Z', messageCount: 5, firstMessagePreview: 'Domanda test' }],
      isLoading: false,
    } as ReturnType<typeof useAgentDataModule.useAgentThreads>);
    render(<AgentChatDrawerLayout data={mockAgent} />);
    await user.click(screen.getByTestId('thread-item-t1'));
    expect(screen.getByTestId('agent-chat-area')).toHaveAttribute('data-thread-id', 't1');
  });
});
