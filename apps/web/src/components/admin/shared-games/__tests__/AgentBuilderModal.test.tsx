/**
 * AgentBuilderModal Component Tests - Issue #4230
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { AgentBuilderModal } from '../AgentBuilderModal';

// Mock AgentBuilderForm
vi.mock('@/components/admin/agent-definitions/AgentBuilderForm', () => ({
  AgentBuilderForm: ({ defaultValues }: { defaultValues: Record<string, unknown> }) => (
    <div data-testid="agent-builder-form">
      <div>Name: {String(defaultValues.name)}</div>
      <div>Description: {String(defaultValues.description)}</div>
    </div>
  ),
}));

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false },
    mutations: { retry: false },
  },
});

const Wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
);

describe('AgentBuilderModal', () => {
  const mockContext = {
    gameId: 'game-123',
    gameTitle: 'Settlers of Catan',
    gameDescription: 'A strategic board game about resource management',
  };

  it('renders when open is true', () => {
    render(
      <Wrapper>
        <AgentBuilderModal
          open={true}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    expect(screen.getByText('Create AI Agent for Settlers of Catan')).toBeInTheDocument();
    expect(screen.getByTestId('agent-builder-form')).toBeInTheDocument();
  });

  it('does not render when open is false', () => {
    const { container } = render(
      <Wrapper>
        <AgentBuilderModal
          open={false}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    expect(container).toBeEmptyDOMElement();
  });

  it('pre-populates form with suggested agent name', () => {
    render(
      <Wrapper>
        <AgentBuilderModal
          open={true}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    expect(screen.getByText('Name: Settlers of Catan Arbitro')).toBeInTheDocument();
  });

  it('pre-populates form with game description context', () => {
    render(
      <Wrapper>
        <AgentBuilderModal
          open={true}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    expect(screen.getByText(/AI assistant for Settlers of Catan/)).toBeInTheDocument();
  });

  it('shows dialog description with knowledge base context', () => {
    render(
      <Wrapper>
        <AgentBuilderModal
          open={true}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    expect(
      screen.getByText(/The agent will have access to the game's knowledge base and documents/)
    ).toBeInTheDocument();
  });
});
