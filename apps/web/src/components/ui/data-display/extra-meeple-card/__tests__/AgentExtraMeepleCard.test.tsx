/**
 * Tests for AgentExtraMeepleCard
 * Issue #5026 — AgentExtraMeepleCard (Epic #5023)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AgentExtraMeepleCard } from '../EntityExtraMeepleCard';
import type { AgentDetailData } from '../types';

// ============================================================================
// Mocks
// ============================================================================

const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

vi.stubGlobal('matchMedia', (query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: vi.fn(),
  removeListener: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  dispatchEvent: vi.fn(),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockAgentData: AgentDetailData = {
  id: 'agent-1',
  name: 'Catan Expert',
  type: 'qa',
  strategyName: 'hybrid-rag',
  strategyParameters: { model: 'claude-sonnet-4-6', temperature: 0.7 },
  isActive: true,
  isIdle: false,
  invocationCount: 42,
  lastInvokedAt: '2026-02-21T10:00:00Z',
  createdAt: '2026-01-01T00:00:00Z',
  gameId: 'game-1',
  gameName: 'Catan',
};

// API response format that mapThreads expects
const mockThreadsApiResponse = [
  {
    id: 'thread-1',
    createdAt: '2026-02-20T10:00:00Z',
    messages: [{ content: 'How do I build a settlement?' }],
  },
  {
    id: 'thread-2',
    createdAt: '2026-02-19T10:00:00Z',
    messages: [{ content: 'What are the trading rules?' }, { content: 'You can trade...' }],
  },
];

// API response format that mapKbDocs expects
const mockDocsApiResponse = [
  {
    id: 'doc-1',
    fileName: 'catan-rules.pdf',
    uploadedAt: '2026-02-01T10:00:00Z',
    status: 'indexed',
  },
  {
    id: 'doc-2',
    fileName: 'catan-faq.pdf',
    uploadedAt: '2026-02-05T10:00:00Z',
    status: 'processing',
  },
];

// ============================================================================
// Helpers
// ============================================================================

function mockFetchResponses({
  threads = mockThreadsApiResponse,
  docs = mockDocsApiResponse,
  threadsOk = true,
  docsOk = true,
}: {
  threads?: unknown[];
  docs?: unknown[];
  threadsOk?: boolean;
  docsOk?: boolean;
} = {}) {
  mockFetch.mockImplementation((url: string) => {
    if ((url as string).includes('/chat/threads')) {
      return Promise.resolve({
        ok: threadsOk,
        status: threadsOk ? 200 : 500,
        json: () => Promise.resolve(threads),
      } as Response);
    }
    if ((url as string).includes('/documents')) {
      return Promise.resolve({
        ok: docsOk,
        status: docsOk ? 200 : 500,
        json: () => Promise.resolve(docs),
      } as Response);
    }
    return Promise.resolve({ ok: false, status: 404 } as Response);
  });
}

function renderAgent(props: Partial<React.ComponentProps<typeof AgentExtraMeepleCard>> = {}) {
  return render(<AgentExtraMeepleCard data={mockAgentData} {...props} />);
}

// ============================================================================
// AgentExtraMeepleCard Tests
// ============================================================================

describe('AgentExtraMeepleCard', () => {
  beforeEach(() => {
    mockFetch.mockReset();
    mockFetchResponses();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  // --------------------------------------------------------------------------
  // Header
  // --------------------------------------------------------------------------

  describe('header', () => {
    it('renders agent name', () => {
      renderAgent();
      expect(screen.getByText('Catan Expert')).toBeInTheDocument();
    });

    it('renders type subtitle', () => {
      renderAgent();
      expect(screen.getByText('Tipo: qa')).toBeInTheDocument();
    });

    it('renders invocation count in badge when > 0', () => {
      renderAgent();
      expect(screen.getByText('42')).toBeInTheDocument();
    });

    it('does not render badge when invocationCount is 0', () => {
      renderAgent({ data: { ...mockAgentData, invocationCount: 0 } });
      // "0" should not appear as a badge — badge is conditionally rendered
      expect(screen.queryByText('0')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Tabs — presence
  // --------------------------------------------------------------------------

  describe('tab strip', () => {
    it('renders all 4 tabs', () => {
      renderAgent();
      expect(screen.getByRole('tab', { name: /overview/i })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /stats/i })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /history/i })).toBeInTheDocument();
      expect(screen.getByRole('tab', { name: /kb/i })).toBeInTheDocument();
    });

    it('starts on overview tab by default', () => {
      renderAgent();
      expect(screen.getByRole('tab', { name: /overview/i })).toHaveAttribute('data-state', 'active');
    });
  });

  // --------------------------------------------------------------------------
  // Overview Tab
  // --------------------------------------------------------------------------

  describe('Overview tab', () => {
    it('shows active status badge', () => {
      renderAgent({ data: { ...mockAgentData, isActive: true, isIdle: false } });
      // AgentStatusBadge renders 'Active' label
      expect(screen.getByText('Active')).toBeInTheDocument();
    });

    it('shows idle status badge', () => {
      renderAgent({ data: { ...mockAgentData, isActive: true, isIdle: true } });
      expect(screen.getByText('Idle')).toBeInTheDocument();
    });

    it('shows error status badge when agent is inactive', () => {
      renderAgent({ data: { ...mockAgentData, isActive: false } });
      expect(screen.getByText('Error')).toBeInTheDocument();
    });

    it('shows linked game chip when gameName is present', () => {
      renderAgent();
      expect(screen.getByText('Gioco collegato')).toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('does not show linked game chip when gameName is absent', () => {
      renderAgent({ data: { ...mockAgentData, gameName: undefined } });
      expect(screen.queryByText('Gioco collegato')).not.toBeInTheDocument();
    });

    it('shows "Avvia Chat" link', () => {
      renderAgent();
      const link = screen.getByTestId('agent-action-start-chat');
      expect(link).toBeInTheDocument();
      expect(link).toHaveAttribute('href', '/chat/new?agentId=agent-1');
    });

    it('shows "Configura" link when gameId is present', () => {
      renderAgent();
      const link = screen.getByTestId('agent-action-configure');
      expect(link).toBeInTheDocument();
      expect(link).toHaveAttribute('href', '/library/games/game-1/agent');
    });

    it('does not show "Configura" link when gameId is absent', () => {
      renderAgent({ data: { ...mockAgentData, gameId: undefined } });
      expect(screen.queryByTestId('agent-action-configure')).not.toBeInTheDocument();
    });

    it('shows model name from strategyParameters', () => {
      renderAgent({ data: { ...mockAgentData, strategyParameters: { model: 'claude-opus-4-6' } } });
      expect(screen.getByText('claude-opus-4-6')).toBeInTheDocument();
    });

    it('falls back to strategyName when model param is absent', () => {
      renderAgent({ data: { ...mockAgentData, strategyParameters: {}, strategyName: 'hybrid-rag' } });
      expect(screen.getByText('hybrid-rag')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Stats Tab
  // --------------------------------------------------------------------------

  describe('Stats tab', () => {
    it('switches to stats tab', async () => {
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /stats/i }));

      expect(screen.getByRole('tab', { name: /stats/i })).toHaveAttribute('data-state', 'active');
    });

    it('shows invocation count in stats', async () => {
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /stats/i }));

      // AgentStatsDisplay renders data-testid="invocation-count" and "42 total invocations"
      expect(screen.getByTestId('invocation-count')).toBeInTheDocument();
    });

    it('shows empty state when invocationCount is 0', async () => {
      const user = userEvent.setup();
      renderAgent({ data: { ...mockAgentData, invocationCount: 0 } });

      await user.click(screen.getByRole('tab', { name: /stats/i }));

      expect(screen.getByText('Nessuna conversazione ancora')).toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // History Tab
  // --------------------------------------------------------------------------

  describe('History tab', () => {
    it('shows loading spinner while fetching threads', async () => {
      mockFetch.mockImplementation((url: string) => {
        if ((url as string).includes('/chat/threads')) {
          return new Promise(() => {}); // never resolves
        }
        return Promise.resolve({ ok: true, json: () => Promise.resolve([]) } as Response);
      });
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /history/i }));

      // Spinner via Loader2 — wrapped in an element; detect via animate-spin class
      const spinner = document.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    it('shows empty state when no threads', async () => {
      mockFetchResponses({ threads: [] });
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /history/i }));

      await waitFor(() => {
        expect(screen.getByText('Nessun thread di chat')).toBeInTheDocument();
      });
    });

    it('renders thread list with message count', async () => {
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /history/i }));

      await waitFor(() => {
        // thread-1 has 1 message
        expect(screen.getByText('1 msg')).toBeInTheDocument();
        // thread-2 has 2 messages
        expect(screen.getByText('2 msg')).toBeInTheDocument();
      });
    });

    it('renders first message preview in threads', async () => {
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /history/i }));

      await waitFor(() => {
        expect(screen.getByText('How do I build a settlement?')).toBeInTheDocument();
      });
    });

    it('renders external link to each thread', async () => {
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /history/i }));

      await waitFor(() => {
        const links = screen.getAllByRole('link', { name: /vai al thread/i });
        expect(links.length).toBe(2);
        expect(links[0]).toHaveAttribute('href', '/chat/thread-1');
      });
    });

    it('hides spinner after threads load', async () => {
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /history/i }));

      await waitFor(() => {
        expect(document.querySelector('.animate-spin')).not.toBeInTheDocument();
      });
    });
  });

  // --------------------------------------------------------------------------
  // KB Tab
  // --------------------------------------------------------------------------

  describe('KB tab', () => {
    it('shows "no game linked" message when gameId is absent', async () => {
      renderAgent({ data: { ...mockAgentData, gameId: undefined } });
      const user = userEvent.setup();

      await user.click(screen.getByRole('tab', { name: /kb/i }));

      expect(screen.getByText('Nessun gioco collegato a questo agente')).toBeInTheDocument();
    });

    it('shows empty state when no docs', async () => {
      mockFetchResponses({ docs: [] });
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /kb/i }));

      await waitFor(() => {
        expect(screen.getByText('Nessun documento KB')).toBeInTheDocument();
      });
    });

    it('shows "Aggiungi documento" link in empty state', async () => {
      mockFetchResponses({ docs: [] });
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /kb/i }));

      await waitFor(() => {
        const link = screen.getByRole('link', { name: /aggiungi documento/i });
        expect(link).toHaveAttribute('href', '/library/games/game-1/agent');
      });
    });

    it('renders KB document list with file names', async () => {
      const user = userEvent.setup();
      renderAgent();

      await user.click(screen.getByRole('tab', { name: /kb/i }));

      await waitFor(() => {
        expect(screen.getByText('catan-rules.pdf')).toBeInTheDocument();
        expect(screen.getByText('catan-faq.pdf')).toBeInTheDocument();
      });
    });

    it('does not fetch KB docs when gameId is absent', async () => {
      renderAgent({ data: { ...mockAgentData, gameId: undefined } });
      const user = userEvent.setup();

      await user.click(screen.getByRole('tab', { name: /kb/i }));

      const docCalls = mockFetch.mock.calls.filter(([url]) => (url as string).includes('/documents'));
      expect(docCalls.length).toBe(0);
    });
  });

  // --------------------------------------------------------------------------
  // Loading / Error states
  // --------------------------------------------------------------------------

  describe('loading and error states', () => {
    it('renders loading state', () => {
      renderAgent({ loading: true });
      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('renders error state', () => {
      renderAgent({ error: 'Agente non trovato' });
      expect(screen.getByText('Agente non trovato')).toBeInTheDocument();
    });

    it('loading state takes precedence over data', () => {
      renderAgent({ loading: true });
      // agent name should not be visible in loading state
      expect(screen.queryByText('Catan Expert')).not.toBeInTheDocument();
    });
  });

  // --------------------------------------------------------------------------
  // Props
  // --------------------------------------------------------------------------

  describe('props', () => {
    it('applies custom className', () => {
      renderAgent({ className: 'my-custom-class', 'data-testid': 'agent-card' });
      expect(screen.getByTestId('agent-card')).toHaveClass('my-custom-class');
    });

    it('fetches threads with correct agentId URL', async () => {
      renderAgent({ data: { ...mockAgentData, id: 'special-agent-id', gameId: undefined } });

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith(
          '/api/v1/chat/threads?agentId=special-agent-id',
          expect.objectContaining({ signal: expect.any(AbortSignal) }),
        );
      });
    });

    it('fetches KB docs with correct gameId URL', async () => {
      renderAgent();

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith(
          '/api/v1/library/games/game-1/documents',
          expect.objectContaining({ signal: expect.any(AbortSignal) }),
        );
      });
    });
  });
});
