/**
 * ProposalsTable Component Tests - Issue #3182
 *
 * Tests for proposals table with action buttons and status-based UI.
 */

import { render, screen, waitFor } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { vi } from 'vitest';

import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

import { ProposalsTable } from '../_components/ProposalsTable';

// Mock router
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
}));

// Mock API
vi.mock('@/lib/api/agent-typologies.api', () => ({
  agentTypologiesApi: {
    submitForApproval: vi.fn(),
  },
}));

// Mock toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const draftProposal: Typology = {
  id: 'draft-1',
  name: 'Draft Proposal',
  description: 'Test draft',
  basePrompt: 'Test prompt',
  defaultStrategyName: 'HybridSearch',
  status: 'Draft',
  createdBy: 'user-1',
  createdAt: new Date().toISOString(),
  isDeleted: false,
};

const pendingProposal: Typology = {
  ...draftProposal,
  id: 'pending-1',
  name: 'Pending Proposal',
  status: 'PendingReview',
};

const approvedProposal: Typology = {
  ...draftProposal,
  id: 'approved-1',
  name: 'Approved Proposal',
  status: 'Approved',
  approvedBy: 'admin-1',
  approvedAt: new Date().toISOString(),
};

function renderComponent(proposals: Typology[]) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  const mockRouter = {
    push: vi.fn(),
  };
  vi.mocked(useRouter).mockReturnValue(mockRouter as any);

  return {
    ...render(
      <QueryClientProvider client={queryClient}>
        <ProposalsTable proposals={proposals} />
      </QueryClientProvider>
    ),
    mockRouter,
  };
}

describe('ProposalsTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows Edit, Test, Submit buttons for Draft proposals', () => {
    renderComponent([draftProposal]);

    expect(screen.getByRole('button', { name: /Edit/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Test/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Submit/ })).toBeInTheDocument();
  });

  it('shows "Awaiting Admin Review" for Pending proposals', () => {
    renderComponent([pendingProposal]);

    expect(screen.getByText('Awaiting Admin Review')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Edit/ })).not.toBeInTheDocument();
  });

  it('shows View button for Approved proposals', () => {
    renderComponent([approvedProposal]);

    expect(screen.getByRole('button', { name: /View/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Edit/ })).not.toBeInTheDocument();
  });

  it('navigates to edit page on Edit click', async () => {
    const user = userEvent.setup();
    const { mockRouter } = renderComponent([draftProposal]);

    const editButton = screen.getByRole('button', { name: /Edit/ });
    await user.click(editButton);

    expect(mockRouter.push).toHaveBeenCalledWith('/editor/agent-proposals/draft-1/edit');
  });

  it('navigates to test page on Test click', async () => {
    const user = userEvent.setup();
    const { mockRouter } = renderComponent([draftProposal]);

    const testButton = screen.getByRole('button', { name: /Test/ });
    await user.click(testButton);

    expect(mockRouter.push).toHaveBeenCalledWith('/editor/agent-proposals/draft-1/test');
  });

  it('submits proposal for approval with confirmation', async () => {
    const user = userEvent.setup();
    vi.mocked(agentTypologiesApi.submitForApproval).mockResolvedValue();
    global.confirm = vi.fn(() => true);

    renderComponent([draftProposal]);

    const submitButton = screen.getByRole('button', { name: /Submit/ });
    await user.click(submitButton);

    await waitFor(() => {
      expect(agentTypologiesApi.submitForApproval).toHaveBeenCalledWith('draft-1', draftProposal);
    });
  });

  it('shows empty state when no proposals match filter', () => {
    renderComponent([]);

    expect(screen.getByText('No proposals match your filters')).toBeInTheDocument();
  });

  it('displays status badges correctly', () => {
    renderComponent([draftProposal, pendingProposal, approvedProposal]);

    expect(screen.getByText('Draft')).toBeInTheDocument();
    expect(screen.getByText('Pending Review')).toBeInTheDocument();
    expect(screen.getByText('Approved')).toBeInTheDocument();
  });
});
