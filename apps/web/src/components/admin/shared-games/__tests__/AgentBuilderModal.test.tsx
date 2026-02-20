/**
 * AgentBuilderModal Component Tests - Issue #4230, #4928
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

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

// Mock @/lib/api for KB cards query
const mockGetKbCards = vi.fn().mockResolvedValue([]);

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getKbCards: (...args: unknown[]) => mockGetKbCards(...args),
      linkAgent: vi.fn().mockResolvedValue(undefined),
    },
  },
}));

vi.mock('@/lib/api/agent-definitions.api', () => ({
  agentDefinitionsApi: {
    create: vi.fn(),
  },
}));

function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
}

const Wrapper = ({ children }: { children: React.ReactNode }) => (
  <QueryClientProvider client={makeQueryClient()}>{children}</QueryClientProvider>
);

describe('AgentBuilderModal', () => {
  const mockContext = {
    gameId: 'game-123',
    gameTitle: 'Settlers of Catan',
    gameDescription: 'A strategic board game about resource management',
  };

  beforeEach(() => {
    mockGetKbCards.mockClear();
    mockGetKbCards.mockResolvedValue([]);
  });

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

  it('shows KB cards section header', () => {
    render(
      <Wrapper>
        <AgentBuilderModal
          open={true}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    expect(screen.getByText('Knowledge Base Documents')).toBeInTheDocument();
  });

  it('shows empty state when no KB cards are available', async () => {
    mockGetKbCards.mockResolvedValue([]);

    render(
      <Wrapper>
        <AgentBuilderModal
          open={true}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    await waitFor(() => {
      expect(
        screen.getByText(/No indexed documents available/)
      ).toBeInTheDocument();
    });
  });

  it('shows KB cards as checkboxes when available', async () => {
    mockGetKbCards.mockResolvedValue([
      {
        id: 'card-1',
        pdfDocumentId: 'pdf-1',
        fileName: 'rulebook.pdf',
        indexingStatus: 'completed',
        chunkCount: 42,
        indexedAt: '2026-02-20T10:00:00Z',
        documentType: 'rulebook',
        version: '1.0',
        isActive: true,
      },
      {
        id: 'card-2',
        pdfDocumentId: 'pdf-2',
        fileName: 'errata.pdf',
        indexingStatus: 'completed',
        chunkCount: 5,
        indexedAt: '2026-02-20T11:00:00Z',
        documentType: 'errata',
        version: '2.0',
        isActive: false,
      },
    ]);

    render(
      <Wrapper>
        <AgentBuilderModal
          open={true}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    await waitFor(() => {
      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
      expect(screen.getByText('errata.pdf')).toBeInTheDocument();
    });

    // Both checkboxes should be unchecked by default
    const checkboxes = screen.getAllByRole('checkbox') as HTMLInputElement[];
    expect(checkboxes).toHaveLength(2);
    checkboxes.forEach((cb) => expect(cb.checked).toBe(false));
  });

  it('shows fallback message when no KB cards selected', async () => {
    mockGetKbCards.mockResolvedValue([
      {
        id: 'card-1',
        pdfDocumentId: 'pdf-1',
        fileName: 'rulebook.pdf',
        indexingStatus: 'completed',
        chunkCount: 10,
        indexedAt: null,
        documentType: 'rulebook',
        version: '1.0',
        isActive: true,
      },
    ]);

    render(
      <Wrapper>
        <AgentBuilderModal
          open={true}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    await waitFor(() => {
      expect(
        screen.getByText(/No documents selected — the agent will use all available documents/)
      ).toBeInTheDocument();
    });
  });

  it('does not fetch KB cards when modal is closed', () => {
    render(
      <Wrapper>
        <AgentBuilderModal
          open={false}
          onClose={vi.fn()}
          sharedGameContext={mockContext}
        />
      </Wrapper>
    );

    expect(mockGetKbCards).not.toHaveBeenCalled();
  });
});
