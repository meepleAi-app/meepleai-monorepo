/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameTableZoneKnowledge } from '../GameTableZoneKnowledge';

// ============================================================================
// Mocks
// ============================================================================

const mockDrawerOpen = vi.fn();
const mockRouterPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockRouterPush }),
}));

vi.mock('@/lib/stores/gameTableDrawerStore', () => ({
  useGameTableDrawer: (selector: (s: { open: typeof mockDrawerOpen }) => unknown) =>
    selector({ open: mockDrawerOpen }),
}));

const mockUseAgentKbDocs = vi.fn().mockReturnValue({
  data: [
    {
      id: 'doc-1',
      fileName: 'rules.pdf',
      uploadedAt: '2026-03-01T00:00:00Z',
      status: 'indexed' as const,
    },
    {
      id: 'doc-2',
      fileName: 'faq.pdf',
      uploadedAt: '2026-03-02T00:00:00Z',
      status: 'processing' as const,
    },
  ],
  isLoading: false,
});

const mockUseAgentThreads = vi.fn().mockReturnValue({
  data: [
    {
      id: 't-1',
      createdAt: '2026-03-19T10:00:00Z',
      messageCount: 5,
      firstMessagePreview: 'Come si gioca?',
    },
  ],
  isLoading: false,
});

vi.mock('@/hooks/queries/useAgentData', () => ({
  useAgentKbDocs: (...args: unknown[]) => mockUseAgentKbDocs(...args),
  useAgentThreads: (...args: unknown[]) => mockUseAgentThreads(...args),
}));

vi.mock('@/hooks/useAgentStatus', () => ({
  useAgentStatus: () => ({
    status: {
      agentId: 'agent-1',
      name: 'Test',
      isActive: true,
      isReady: true,
      hasConfiguration: true,
      hasDocuments: true,
      documentCount: 2,
      ragStatus: 'ready',
    },
    isLoading: false,
    error: null,
    refetch: vi.fn(),
  }),
}));

const mockUseGameAgents = vi.fn().mockReturnValue({
  data: [{ id: 'agent-1', name: 'Test Agent', gameId: 'g-1' }],
  isLoading: false,
});

vi.mock('@/hooks/queries/useGameAgents', () => ({
  useGameAgents: (...args: unknown[]) => mockUseGameAgents(...args),
}));

vi.mock('@/components/ui/data-display/meeple-card-features/DocumentStatusBadge', () => ({
  KbStatusBadge: ({ status }: { status: string }) => (
    <span data-testid="doc-status-badge">{status}</span>
  ),
}));

vi.mock('@/components/ui/data-display/meeple-card-features/AgentStatusBadge', () => ({
  AgentStatusBadge: ({ status }: { status: string }) => (
    <span data-testid="agent-status-badge">{status}</span>
  ),
}));

// ============================================================================
// Tests
// ============================================================================

describe('GameTableZoneKnowledge', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseGameAgents.mockReturnValue({
      data: [{ id: 'agent-1', name: 'Test Agent', gameId: 'g-1' }],
      isLoading: false,
    });
    mockUseAgentKbDocs.mockReturnValue({
      data: [
        {
          id: 'doc-1',
          fileName: 'rules.pdf',
          uploadedAt: '2026-03-01T00:00:00Z',
          status: 'indexed' as const,
        },
        {
          id: 'doc-2',
          fileName: 'faq.pdf',
          uploadedAt: '2026-03-02T00:00:00Z',
          status: 'processing' as const,
        },
      ],
      isLoading: false,
    });
    mockUseAgentThreads.mockReturnValue({
      data: [
        {
          id: 't-1',
          createdAt: '2026-03-19T10:00:00Z',
          messageCount: 5,
          firstMessagePreview: 'Come si gioca?',
        },
      ],
      isLoading: false,
    });
  });

  it('renders KB doc items with file names', () => {
    render(<GameTableZoneKnowledge gameId="g-1" />);

    const items = screen.getAllByTestId('kb-doc-item');
    expect(items).toHaveLength(2);
    expect(items[0]).toHaveTextContent('rules.pdf');
    expect(items[1]).toHaveTextContent('faq.pdf');
  });

  it('renders document count', () => {
    render(<GameTableZoneKnowledge gameId="g-1" />);

    expect(screen.getByTestId('doc-count')).toHaveTextContent('2');
  });

  it('renders document status badges', () => {
    render(<GameTableZoneKnowledge gameId="g-1" />);

    const badges = screen.getAllByTestId('doc-status-badge');
    expect(badges).toHaveLength(2);
    expect(badges[0]).toHaveTextContent('indexed');
    expect(badges[1]).toHaveTextContent('processing');
  });

  it('renders upload PDF button', () => {
    render(<GameTableZoneKnowledge gameId="g-1" />);

    const btn = screen.getByTestId('upload-pdf-btn');
    expect(btn).toHaveTextContent('Carica PDF');
  });

  it('navigates to upload-pdf action on upload button click', () => {
    render(<GameTableZoneKnowledge gameId="g-1" />);

    screen.getByTestId('upload-pdf-btn').click();
    expect(mockRouterPush).toHaveBeenCalledWith('/library/g-1?action=upload-pdf');
  });

  it('renders chat thread preview when agent exists', () => {
    render(<GameTableZoneKnowledge gameId="g-1" />);

    expect(screen.getByTestId('last-thread-preview')).toHaveTextContent('Come si gioca?');
  });

  it('renders open chat button when agent exists', () => {
    render(<GameTableZoneKnowledge gameId="g-1" />);

    const btn = screen.getByTestId('open-chat-btn');
    expect(btn).toHaveTextContent('Apri chat');
  });

  it('calls drawer open with resolved agentId on chat button click', () => {
    render(<GameTableZoneKnowledge gameId="g-1" />);

    screen.getByTestId('open-chat-btn').click();
    expect(mockDrawerOpen).toHaveBeenCalledWith({ type: 'chat', agentId: 'agent-1' });
  });

  it('renders agent status indicator when agent exists', () => {
    render(<GameTableZoneKnowledge gameId="g-1" />);

    expect(screen.getByTestId('agent-status-badge')).toBeInTheDocument();
    expect(screen.getByTestId('agent-status-badge')).toHaveTextContent('active');
  });

  it('hides chat and agent sections when no agent exists', () => {
    mockUseGameAgents.mockReturnValue({ data: [], isLoading: false });

    render(<GameTableZoneKnowledge gameId="g-1" />);

    expect(screen.queryByTestId('chat-preview-section')).not.toBeInTheDocument();
    expect(screen.queryByTestId('agent-status-section')).not.toBeInTheDocument();
    // KB docs and upload button should still be visible
    expect(screen.getByTestId('kb-docs-section')).toBeInTheDocument();
    expect(screen.getByTestId('upload-pdf-btn')).toBeInTheDocument();
  });
});

describe('GameTableZoneKnowledge — loading state', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseGameAgents.mockReturnValue({
      data: [{ id: 'agent-1', name: 'Test Agent', gameId: 'g-1' }],
      isLoading: false,
    });
  });

  it('renders skeletons when docs are loading', () => {
    mockUseAgentKbDocs.mockReturnValue({
      data: [],
      isLoading: true,
    });

    render(<GameTableZoneKnowledge gameId="g-1" />);

    expect(screen.getAllByTestId('doc-skeleton')).toHaveLength(2);
  });
});
