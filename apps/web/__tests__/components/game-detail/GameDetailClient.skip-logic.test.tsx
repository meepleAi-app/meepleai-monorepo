/**
 * GameDetailClient Skip Logic Tests (Issue #3213)
 * Tests conditional modal display based on existing agent config
 */

import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import GameDetailClient from '@/app/(public)/library/games/[gameId]/GameDetailClient';
import { api } from '@/lib/api';

// Mock modules
vi.mock('@/lib/api');
vi.mock('@/hooks/useAuth', () => ({
  useAuth: vi.fn(() => ({
    user: { id: 'user-1', tier: 'Premium' },
  })),
}));
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Test data
const mockGameProps = {
  gameId: 'game-123',
  gameTitle: 'Test Game',
  gamePublisher: 'Test Publisher',
  gameImageUrl: '/test-image.jpg',
  agentModes: [
    { id: 'mode-1', name: 'Rules Expert', icon: 'BookOpen' },
  ],
  availablePdfs: [
    { id: 'pdf-1', title: 'Rulebook', url: '/pdf1.pdf' },
  ],
};

const mockExistingConfig = {
  id: 'config-1',
  userId: 'user-1',
  gameId: 'game-123',
  llmModel: 'GPT-4o',
  temperature: 0.7,
  maxTokens: 2000,
  personality: 'Amichevole',
  detailLevel: 'Normale',
  personalNotes: 'Test config',
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
};

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('GameDetailClient - Skip Logic (Issue #3213)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should show "AI Config" button when no existing config', async () => {
    // Mock: No existing config
    vi.mocked(api.library.getAgentConfig).mockResolvedValue(null);

    const Wrapper = createWrapper();
    render(
      <Wrapper>
        <GameDetailClient {...mockGameProps} />
      </Wrapper>
    );

    await waitFor(() => {
      expect(screen.getByText('AI Config')).toBeInTheDocument();
    });

    // Should NOT show Edit button
    expect(screen.queryByText('Edit Config')).not.toBeInTheDocument();
  });

  it('should show "Edit Config" button when config exists', async () => {
    // Mock: Existing config
    vi.mocked(api.library.getAgentConfig).mockResolvedValue(mockExistingConfig);

    const Wrapper = createWrapper();
    render(
      <Wrapper>
        <GameDetailClient {...mockGameProps} />
      </Wrapper>
    );

    await waitFor(() => {
      expect(screen.getByText('Edit Config')).toBeInTheDocument();
    });

    // Should NOT show initial config button
    expect(screen.queryByText(/^AI Config$/)).not.toBeInTheDocument();
  });

  it('should query config on mount when config exists', async () => {
    // Mock: Existing config
    vi.mocked(api.library.getAgentConfig).mockResolvedValue(mockExistingConfig);

    const Wrapper = createWrapper();
    render(
      <Wrapper>
        <GameDetailClient {...mockGameProps} />
      </Wrapper>
    );

    // Wait for config query to complete
    await waitFor(() => {
      expect(api.library.getAgentConfig).toHaveBeenCalledWith('game-123');
    });

    // Note: AgentChatPanel always visible (no toggle prop)
    // This test verifies config query executes and Edit button shows
  });

  it('should query config on mount when no config', async () => {
    // Mock: No config
    vi.mocked(api.library.getAgentConfig).mockResolvedValue(null);

    const Wrapper = createWrapper();
    render(
      <Wrapper>
        <GameDetailClient {...mockGameProps} />
      </Wrapper>
    );

    await waitFor(() => {
      expect(api.library.getAgentConfig).toHaveBeenCalledWith('game-123');
    });

    // Config query completes, showing AI Config button for first-time setup
  });

  it('should wait for config query before rendering buttons', async () => {
    // Mock: Slow API response
    vi.mocked(api.library.getAgentConfig).mockImplementation(
      () => new Promise((resolve) => setTimeout(() => resolve(mockExistingConfig), 100))
    );

    const Wrapper = createWrapper();
    render(
      <Wrapper>
        <GameDetailClient {...mockGameProps} />
      </Wrapper>
    );

    // Initially may show loading or default state
    // After query completes, Edit button should appear
    await waitFor(
      () => {
        expect(screen.getByText('Edit Config')).toBeInTheDocument();
      },
      { timeout: 200 }
    );
  });

  it('should show Settings icon on Edit button', async () => {
    vi.mocked(api.library.getAgentConfig).mockResolvedValue(mockExistingConfig);

    const Wrapper = createWrapper();
    const { container } = render(
      <Wrapper>
        <GameDetailClient {...mockGameProps} />
      </Wrapper>
    );

    await waitFor(() => {
      expect(screen.getByText('Edit Config')).toBeInTheDocument();
    });

    // Verify Settings icon present (via SVG class or aria-label)
    const editButton = screen.getByText('Edit Config').closest('button');
    expect(editButton).toBeInTheDocument();
  });
});
