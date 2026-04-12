import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AgentCharacterSheet } from '../AgentCharacterSheet';
import type { AgentDetailData } from '@/components/ui/data-display/extra-meeple-card/types';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock('@/hooks/queries/useAgentData', () => ({
  useAgentKbDocs: () => ({ data: [], isLoading: false }),
  useAgentThreads: () => ({ data: [], isLoading: false }),
}));
vi.mock('@/hooks/useAgentStatus', () => ({
  useAgentStatus: () => ({ status: null, isLoading: false, error: null }),
}));
vi.mock('@/hooks/useConnectionBarNav', () => ({
  useConnectionBarNav: () => ({ handlePipClick: vi.fn() }),
}));
vi.mock('@/components/ui/data-display/connection-bar', () => ({
  ConnectionBar: ({ connections }: { connections: Array<unknown> }) =>
    connections.length > 0 ? <div data-testid="connection-bar" /> : null,
  buildAgentConnections: () => [{ count: 1 }], // always returns non-empty (real impl always returns 3 pips)
}));
vi.mock('@/components/chat-unified/ChatThreadView', () => ({
  ChatThreadView: () => <div data-testid="chat-thread-view" />,
}));

const mockAgent: AgentDetailData = {
  id: 'agent-1',
  name: 'Test Agent',
  type: 'RAG',
  strategyName: 'default',
  strategyParameters: {},
  isActive: true,
  isIdle: false,
  invocationCount: 42,
  lastInvokedAt: null,
  createdAt: '2025-01-01T00:00:00Z',
  gameId: 'game-1',
  gameName: 'Catan',
};

describe('AgentCharacterSheet', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders agent name', () => {
    render(<AgentCharacterSheet data={mockAgent} />);
    expect(screen.getByTestId('agent-name')).toHaveTextContent('Test Agent');
  });

  it('renders connection-bar when game is linked', () => {
    render(<AgentCharacterSheet data={mockAgent} />);
    expect(screen.getByTestId('connection-bar')).toBeInTheDocument();
  });

  it('renders connection-bar even when no game or docs (empty pips)', () => {
    render(<AgentCharacterSheet data={{ ...mockAgent, gameId: undefined, gameName: undefined }} />);
    expect(screen.getByTestId('connection-bar')).toBeInTheDocument();
  });
});
