/**
 * AgentsDashboardSection — Unit Tests
 * Issue #5097, Epic #5094
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AgentsDashboardSection } from '../agents-section';
import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('@/hooks/queries/useRecentAgents', () => ({
  useRecentAgents: vi.fn(),
}));

const { useRecentAgents } = await import('@/hooks/queries/useRecentAgents');

const makeAgent = (overrides: Partial<AgentDto> = {}): AgentDto => ({
  id: 'agent-1',
  name: 'Catan Expert',
  type: 'KnowledgeBase',
  strategyName: 'hybrid',
  strategyParameters: {},
  isActive: true,
  createdAt: new Date().toISOString(),
  lastInvokedAt: null,
  invocationCount: 5,
  isRecentlyUsed: false,
  isIdle: false,
  ...overrides,
});

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('AgentsDashboardSection', () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it('renders loading skeletons when isLoading', () => {
    (useRecentAgents as ReturnType<typeof vi.fn>).mockReturnValue({
      data: undefined,
      isLoading: true,
    });

    const { container } = render(<AgentsDashboardSection />);
    expect(container.querySelectorAll('.animate-pulse').length).toBeGreaterThan(0);
    expect(screen.queryByText('Catan Expert')).not.toBeInTheDocument();
  });

  it('does not render the "Crea nuovo agente" CTA card', () => {
    (useRecentAgents as ReturnType<typeof vi.fn>).mockReturnValue({
      data: [],
      isLoading: false,
    });

    render(<AgentsDashboardSection />);
    expect(screen.queryByText(/Crea nuovo agente/)).not.toBeInTheDocument();
  });

  it('renders agent cards', () => {
    (useRecentAgents as ReturnType<typeof vi.fn>).mockReturnValue({
      data: [
        makeAgent({ id: 'a1', name: 'Catan Expert' }),
        makeAgent({ id: 'a2', name: 'Wingspan Guru' }),
      ],
      isLoading: false,
    });

    render(<AgentsDashboardSection />);
    expect(screen.getByText('Catan Expert')).toBeInTheDocument();
    expect(screen.getByText('Wingspan Guru')).toBeInTheDocument();
  });

  it('shows "Attivo" badge for active agents', () => {
    (useRecentAgents as ReturnType<typeof vi.fn>).mockReturnValue({
      data: [makeAgent({ isActive: true })],
      isLoading: false,
    });

    render(<AgentsDashboardSection />);
    expect(screen.getByText('Attivo')).toBeInTheDocument();
  });

  it('shows "Inattivo" badge for inactive agents', () => {
    (useRecentAgents as ReturnType<typeof vi.fn>).mockReturnValue({
      data: [makeAgent({ isActive: false })],
      isLoading: false,
    });

    render(<AgentsDashboardSection />);
    expect(screen.getByText('Inattivo')).toBeInTheDocument();
  });

  it('links each card to /agents/{id}', () => {
    (useRecentAgents as ReturnType<typeof vi.fn>).mockReturnValue({
      data: [makeAgent({ id: 'agent-42', name: 'My Agent' })],
      isLoading: false,
    });

    render(<AgentsDashboardSection />);
    expect(screen.getByRole('link', { name: /My Agent/ })).toHaveAttribute(
      'href',
      '/agents/agent-42'
    );
  });

  it('links "Vedi tutti" to /agents', () => {
    (useRecentAgents as ReturnType<typeof vi.fn>).mockReturnValue({
      data: [],
      isLoading: false,
    });

    render(<AgentsDashboardSection />);
    expect(screen.getByRole('link', { name: /Vedi tutti/ })).toHaveAttribute('href', '/agents');
  });
});
