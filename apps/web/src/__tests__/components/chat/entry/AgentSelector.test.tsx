/**
 * AgentSelector — Unit tests
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock api before importing component
const mockGetUserAgentsForGame = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      getUserAgentsForGame: (...args: unknown[]) => mockGetUserAgentsForGame(...args),
    },
  },
}));

import { AgentSelector } from '@/components/chat/entry/AgentSelector';

describe('AgentSelector', () => {
  const onSelectSystem = vi.fn();
  const onSelectCustom = vi.fn();
  const onCustomAgentsResolved = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetUserAgentsForGame.mockResolvedValue([]);
  });

  it('renders all 5 system agents', () => {
    render(
      <AgentSelector
        gameId={null}
        onSelectSystemAgent={onSelectSystem}
        onSelectCustomAgent={onSelectCustom}
        selectedAgentType="auto"
        selectedCustomAgentId={null}
      />
    );

    expect(screen.getByTestId('agent-card-auto')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-qa')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-rules')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-strategy')).toBeInTheDocument();
    expect(screen.getByTestId('agent-card-narrative')).toBeInTheDocument();
  });

  it('calls onSelectSystemAgent when system agent clicked', () => {
    render(
      <AgentSelector
        gameId={null}
        onSelectSystemAgent={onSelectSystem}
        onSelectCustomAgent={onSelectCustom}
        selectedAgentType={null}
        selectedCustomAgentId={null}
      />
    );

    fireEvent.click(screen.getByTestId('agent-card-qa'));
    expect(onSelectSystem).toHaveBeenCalledWith('qa');
  });

  it('highlights selected system agent', () => {
    render(
      <AgentSelector
        gameId={null}
        onSelectSystemAgent={onSelectSystem}
        onSelectCustomAgent={onSelectCustom}
        selectedAgentType="rules"
        selectedCustomAgentId={null}
      />
    );

    expect(screen.getByTestId('agent-card-rules')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('agent-card-auto')).toHaveAttribute('aria-pressed', 'false');
  });

  it('fetches custom agents when gameId is provided', async () => {
    mockGetUserAgentsForGame.mockResolvedValue([{ id: 'ca1', name: 'My Agent', type: 'qa' }]);

    render(
      <AgentSelector
        gameId="game-123"
        onSelectSystemAgent={onSelectSystem}
        onSelectCustomAgent={onSelectCustom}
        selectedAgentType={null}
        selectedCustomAgentId={null}
        onCustomAgentsResolved={onCustomAgentsResolved}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('custom-agent-card-ca1')).toBeInTheDocument();
    });

    expect(mockGetUserAgentsForGame).toHaveBeenCalledWith('game-123');
  });

  it('calls onSelectCustomAgent when custom agent clicked', async () => {
    mockGetUserAgentsForGame.mockResolvedValue([{ id: 'ca1', name: 'My Agent', type: 'qa' }]);

    render(
      <AgentSelector
        gameId="game-123"
        onSelectSystemAgent={onSelectSystem}
        onSelectCustomAgent={onSelectCustom}
        selectedAgentType={null}
        selectedCustomAgentId={null}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('custom-agent-card-ca1')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('custom-agent-card-ca1'));
    expect(onSelectCustom).toHaveBeenCalledWith('ca1');
  });

  it('does not fetch agents when gameId is null', () => {
    render(
      <AgentSelector
        gameId={null}
        onSelectSystemAgent={onSelectSystem}
        onSelectCustomAgent={onSelectCustom}
        selectedAgentType="auto"
        selectedCustomAgentId={null}
      />
    );

    expect(mockGetUserAgentsForGame).not.toHaveBeenCalled();
  });

  it('does not show custom agent section when no custom agents exist', async () => {
    mockGetUserAgentsForGame.mockResolvedValue([]);

    render(
      <AgentSelector
        gameId="game-123"
        onSelectSystemAgent={onSelectSystem}
        onSelectCustomAgent={onSelectCustom}
        selectedAgentType="auto"
        selectedCustomAgentId={null}
      />
    );

    await waitFor(() => {
      expect(mockGetUserAgentsForGame).toHaveBeenCalled();
    });

    expect(screen.queryByTestId('create-agent-link')).not.toBeInTheDocument();
  });

  it('shows create-agent link when custom agents exist', async () => {
    mockGetUserAgentsForGame.mockResolvedValue([{ id: 'ca1', name: 'Agent One', type: 'qa' }]);

    render(
      <AgentSelector
        gameId="game-456"
        onSelectSystemAgent={onSelectSystem}
        onSelectCustomAgent={onSelectCustom}
        selectedAgentType={null}
        selectedCustomAgentId={null}
      />
    );

    await waitFor(() => {
      expect(screen.getByTestId('create-agent-link')).toBeInTheDocument();
    });

    const link = screen.getByTestId('create-agent-link').closest('a');
    expect(link).toHaveAttribute('href', '/chat/agents/create?gameId=game-456');
  });
});
