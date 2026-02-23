/**
 * Admin Agent View Page Tests (Task #8)
 */

import { screen, fireEvent, waitFor } from '@testing-library/react';
import { useParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { agentDefinitionsApi } from '@/lib/api/agent-definitions.api';

import AdminAgentViewPage from '../page';

// Mock dependencies
vi.mock('next/navigation', () => ({
  useParams: vi.fn(),
}));

vi.mock('@/lib/api/agent-definitions.api', () => ({
  agentDefinitionsApi: {
    getById: vi.fn(),
  },
}));

describe('AdminAgentViewPage', () => {
  const mockAgent = {
    id: 'agent-123',
    name: 'Test Agent',
    description: 'Test description',
    isActive: true,
    config: {
      model: 'gpt-4',
      strategyName: 'HybridSearch',
      temperature: 0.7,
      maxTokens: 2048,
    },
    prompts: [
      { role: 'system', content: 'You are a helpful assistant' },
    ],
    tools: [],
  };

  beforeEach(() => {
    vi.clearAllMocks();
    (useParams as ReturnType<typeof vi.fn>).mockReturnValue({ id: 'agent-123' });
  });

  it('renders agent information', async () => {
    (agentDefinitionsApi.getById as ReturnType<typeof vi.fn>).mockResolvedValue(mockAgent);

    renderWithQuery(<AdminAgentViewPage />);

    await waitFor(() => {
      expect(screen.getByText('Test Agent')).toBeInTheDocument();
    });

    expect(screen.getByText('Test description')).toBeInTheDocument();
    expect(screen.getByText('Active')).toBeInTheDocument();
  });

  it('shows configuration section', async () => {
    (agentDefinitionsApi.getById as ReturnType<typeof vi.fn>).mockResolvedValue(mockAgent);

    renderWithQuery(<AdminAgentViewPage />);

    await waitFor(() => {
      expect(screen.getByText('Configuration')).toBeInTheDocument();
    });

    expect(screen.getByText('gpt-4')).toBeInTheDocument();
    // Component renders hardcoded 'POC Strategy' instead of config.strategyName
    expect(screen.getByText('POC Strategy')).toBeInTheDocument();
    expect(screen.getByText('0.7')).toBeInTheDocument();
  });

  it('shows channel configuration section', async () => {
    (agentDefinitionsApi.getById as ReturnType<typeof vi.fn>).mockResolvedValue(mockAgent);

    renderWithQuery(<AdminAgentViewPage />);

    await waitFor(() => {
      expect(screen.getByText('Channel Configuration')).toBeInTheDocument();
    });

    expect(screen.getByRole('button', { name: /Enable Channel/i })).toBeInTheDocument();
  });

  it('enables channel when button clicked', async () => {
    (agentDefinitionsApi.getById as ReturnType<typeof vi.fn>).mockResolvedValue(mockAgent);

    renderWithQuery(<AdminAgentViewPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Enable Channel/i })).toBeInTheDocument();
    });

    const enableBtn = screen.getByRole('button', { name: /Enable Channel/i });
    fireEvent.click(enableBtn);

    // Channel should be enabled
    await waitFor(() => {
      expect(screen.getByText('WebSocket Endpoint')).toBeInTheDocument();
      expect(screen.getByText('Connected')).toBeInTheDocument();
    });
  });

  it('shows chat section after channel section', async () => {
    (agentDefinitionsApi.getById as ReturnType<typeof vi.fn>).mockResolvedValue(mockAgent);

    renderWithQuery(<AdminAgentViewPage />);

    await waitFor(() => {
      expect(screen.getByText('Agent Chat')).toBeInTheDocument();
    });

    // Chat section now renders inline content with link to unified chat
    expect(screen.getByText('Open Unified Chat')).toBeInTheDocument();
  });

  it('shows chat migration notice with unified chat link', async () => {
    (agentDefinitionsApi.getById as ReturnType<typeof vi.fn>).mockResolvedValue(mockAgent);

    renderWithQuery(<AdminAgentViewPage />);

    await waitFor(() => {
      expect(screen.getByText('Agent Chat')).toBeInTheDocument();
    });

    // Chat section shows migration notice and link to unified chat
    expect(screen.getByText(/unified chat system/i)).toBeInTheDocument();
    const chatLink = screen.getByText('Open Unified Chat');
    expect(chatLink.closest('a')).toHaveAttribute('href', '/chat/new');
  });

  it('has edit button linking to edit page', async () => {
    (agentDefinitionsApi.getById as ReturnType<typeof vi.fn>).mockResolvedValue(mockAgent);

    renderWithQuery(<AdminAgentViewPage />);

    await waitFor(() => {
      const editLink = screen.getByRole('link', { name: /Edit/i });
      expect(editLink).toHaveAttribute('href', '/admin/agents/definitions/agent-123/edit');
    });
  });
});
