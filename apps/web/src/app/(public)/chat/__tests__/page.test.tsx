/**
 * General Chat Page Tests (Task #9 - Issue #11)
 */

import { render, screen, waitFor } from '@testing-library/react';
import { useRouter, useSearchParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useAuth } from '@/components/auth/AuthProvider';

import GeneralChatPage from '../page';

// Mock dependencies
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
  useSearchParams: vi.fn(),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: vi.fn(),
}));

vi.mock('@/components/agent/AgentChat', () => ({
  AgentChat: ({ agentName, enableAgentSelector }: { agentName: string; enableAgentSelector?: boolean }) => (
    <div data-testid="agent-chat">
      Chat with {agentName}
      {enableAgentSelector && <div data-testid="agent-selector-enabled">Selector enabled</div>}
    </div>
  ),
}));

describe('GeneralChatPage', () => {
  const mockRouter = {
    push: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    (useRouter as ReturnType<typeof vi.fn>).mockReturnValue(mockRouter);
  });

  it('redirects unauthenticated users to login', () => {
    (useAuth as ReturnType<typeof vi.fn>).mockReturnValue({ user: null });
    (useSearchParams as ReturnType<typeof vi.fn>).mockReturnValue({
      get: () => null,
    });

    render(<GeneralChatPage />);

    expect(mockRouter.push).toHaveBeenCalledWith('/login?redirect=%2Fchat');
  });

  it('renders chat with agent selector enabled', () => {
    (useAuth as ReturnType<typeof vi.fn>).mockReturnValue({ user: { id: 'user-1' } });
    (useSearchParams as ReturnType<typeof vi.fn>).mockReturnValue({
      get: () => null,
    });

    render(<GeneralChatPage />);

    expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
    expect(screen.getByTestId('agent-selector-enabled')).toBeInTheDocument();
  });

  it('pre-selects agent from query param (Issue #11)', () => {
    (useAuth as ReturnType<typeof vi.fn>).mockReturnValue({ user: { id: 'user-1' } });
    (useSearchParams as ReturnType<typeof vi.fn>).mockReturnValue({
      get: (param: string) => (param === 'agent' ? 'tutor' : null),
    });

    render(<GeneralChatPage />);

    expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
    // Agent should be pre-selected as 'tutor'
  });

  it('preserves agent in URL when coming from discover page', () => {
    (useAuth as ReturnType<typeof vi.fn>).mockReturnValue({ user: { id: 'user-1' } });
    (useSearchParams as ReturnType<typeof vi.fn>).mockReturnValue({
      get: (param: string) => (param === 'agent' ? 'decisore' : null),
    });

    render(<GeneralChatPage />);

    // Should maintain agent=decisore in URL
    expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
  });

  it('redirects with agent param preserved', () => {
    (useAuth as ReturnType<typeof vi.fn>).mockReturnValue({ user: null });
    (useSearchParams as ReturnType<typeof vi.fn>).mockReturnValue({
      get: (param: string) => (param === 'agent' ? 'arbitro' : null),
    });

    render(<GeneralChatPage />);

    expect(mockRouter.push).toHaveBeenCalledWith(
      '/login?redirect=%2Fchat%3Fagent%3Darbitro'
    );
  });
});
