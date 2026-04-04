import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { FirstAgentStep } from '../FirstAgentStep';

const mockCreateUserAgent = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      createUserAgent: mockCreateUserAgent,
    },
  },
  ApiError: class ApiError extends Error {
    constructor(message: string) {
      super(message);
      this.name = 'ApiError';
    }
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('FirstAgentStep', () => {
  const user = userEvent.setup();
  const onComplete = vi.fn();
  const onSkip = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders with default agent name from game name', () => {
    renderWithQuery(
      <FirstAgentStep gameId="g1" gameName="Catan" onComplete={onComplete} onSkip={onSkip} />
    );

    expect(screen.getByText('Create Your AI Assistant')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Catan Assistant')).toBeInTheDocument();
    // Game name appears in description text
    expect(screen.getByText(/Set up an AI agent for/)).toBeInTheDocument();
  });

  it('shows agent capabilities info', () => {
    renderWithQuery(
      <FirstAgentStep gameId="g1" gameName="Catan" onComplete={onComplete} onSkip={onSkip} />
    );

    expect(screen.getByText(/What can your agent do/)).toBeInTheDocument();
    expect(screen.getByText(/Answer rules questions/)).toBeInTheDocument();
  });

  it('calls onSkip when skip button clicked', async () => {
    renderWithQuery(
      <FirstAgentStep gameId="g1" gameName="Catan" onComplete={onComplete} onSkip={onSkip} />
    );

    await user.click(screen.getByTestId('agent-skip'));

    expect(onSkip).toHaveBeenCalled();
  });

  it('creates agent on submit', async () => {
    mockCreateUserAgent.mockResolvedValueOnce({ id: 'agent-1', name: 'Catan Assistant' });

    renderWithQuery(
      <FirstAgentStep gameId="g1" gameName="Catan" onComplete={onComplete} onSkip={onSkip} />
    );

    await user.click(screen.getByRole('button', { name: /create agent/i }));

    await waitFor(() => {
      expect(mockCreateUserAgent).toHaveBeenCalledWith({
        gameId: 'g1',
        agentType: 'RuleExpert',
        name: 'Catan Assistant',
      });
    });

    await waitFor(() => {
      expect(onComplete).toHaveBeenCalled();
    });
  });

  it('allows custom agent name', async () => {
    mockCreateUserAgent.mockResolvedValueOnce({ id: 'agent-1', name: 'My Catan Bot' });

    renderWithQuery(
      <FirstAgentStep gameId="g1" gameName="Catan" onComplete={onComplete} onSkip={onSkip} />
    );

    const input = screen.getByDisplayValue('Catan Assistant');
    await user.clear(input);
    await user.type(input, 'My Catan Bot');
    await user.click(screen.getByRole('button', { name: /create agent/i }));

    await waitFor(() => {
      expect(mockCreateUserAgent).toHaveBeenCalledWith({
        gameId: 'g1',
        agentType: 'RuleExpert',
        name: 'My Catan Bot',
      });
    });
  });

  it('shows error on API failure', async () => {
    mockCreateUserAgent.mockRejectedValueOnce(new Error('Quota exceeded'));

    renderWithQuery(
      <FirstAgentStep gameId="g1" gameName="Catan" onComplete={onComplete} onSkip={onSkip} />
    );

    await user.click(screen.getByRole('button', { name: /create agent/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    expect(onComplete).not.toHaveBeenCalled();
  });
});
