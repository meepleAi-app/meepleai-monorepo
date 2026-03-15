/**
 * OwnershipConfirmationDialog Component Tests
 *
 * Tests: KB available variant, KB unavailable variant, quick-create flow, customize, loading
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OwnershipConfirmationDialog } from '../OwnershipConfirmationDialog';

// ============================================================================
// Mock Setup
// ============================================================================

const mockPush = vi.fn();

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
  }),
}));

const mockQuickCreateTutor = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      quickCreateTutor: (...args: unknown[]) => mockQuickCreateTutor(...args),
    },
  },
}));

const mockToastError = vi.fn();

vi.mock('@/components/layout/Toast', () => ({
  toast: {
    error: (...args: unknown[]) => mockToastError(...args),
    success: vi.fn(),
  },
}));

// ============================================================================
// Helpers
// ============================================================================

const withKbResult = {
  gameState: 'Owned',
  ownershipDeclaredAt: '2026-03-14T10:00:00Z',
  hasRagAccess: true,
  kbCardCount: 3,
  isRagPublic: false,
};

const noKbResult = {
  gameState: 'Owned',
  ownershipDeclaredAt: '2026-03-14T10:00:00Z',
  hasRagAccess: false,
  kbCardCount: 0,
  isRagPublic: false,
};

const defaultProps = {
  gameId: 'game-1',
  gameName: 'Catan',
  open: true,
  onOpenChange: vi.fn(),
};

// ============================================================================
// Tests
// ============================================================================

describe('OwnershipConfirmationDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockQuickCreateTutor.mockResolvedValue({
      agentId: 'agent-1',
      chatThreadId: 'thread-1',
      agentName: 'Catan Tutor',
      kbCardCount: 3,
    });
  });

  it('shows KB card chips when kbCardCount > 0', () => {
    render(<OwnershipConfirmationDialog {...defaultProps} ownershipResult={withKbResult} />);

    expect(screen.getByTestId('kb-card-chips')).toBeInTheDocument();
    expect(screen.getByText(/3 schede KB/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Crea Tutor veloce/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Personalizza/i })).toBeInTheDocument();
  });

  it('shows "not available" message when kbCardCount is 0', () => {
    render(<OwnershipConfirmationDialog {...defaultProps} ownershipResult={noKbResult} />);

    expect(screen.getByText(/Tutor non ancora disponibile/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Chiudi/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Crea Tutor veloce/i })).not.toBeInTheDocument();
  });

  it('quick-create calls API and navigates to chat thread', async () => {
    const user = userEvent.setup();
    render(
      <OwnershipConfirmationDialog
        {...defaultProps}
        sharedGameId="shared-1"
        ownershipResult={withKbResult}
      />
    );

    const quickCreateBtn = screen.getByRole('button', { name: /Crea Tutor veloce/i });
    await user.click(quickCreateBtn);

    await waitFor(() => {
      expect(mockQuickCreateTutor).toHaveBeenCalledWith('game-1', 'shared-1');
      expect(mockPush).toHaveBeenCalledWith('/chat/thread-1');
    });
  });

  it('Personalizza navigates to agent creation wizard', async () => {
    const user = userEvent.setup();
    render(<OwnershipConfirmationDialog {...defaultProps} ownershipResult={withKbResult} />);

    const customizeBtn = screen.getByRole('button', { name: /Personalizza/i });
    await user.click(customizeBtn);

    expect(mockPush).toHaveBeenCalledWith('/chat/agents/create?gameId=game-1&step=2');
  });

  it('shows loading state during quick-create', async () => {
    const user = userEvent.setup();
    // Create a promise we can control
    let resolvePromise: (v: unknown) => void;
    mockQuickCreateTutor.mockReturnValueOnce(
      new Promise(resolve => {
        resolvePromise = resolve;
      })
    );

    render(<OwnershipConfirmationDialog {...defaultProps} ownershipResult={withKbResult} />);

    const quickCreateBtn = screen.getByRole('button', { name: /Crea Tutor veloce/i });
    await user.click(quickCreateBtn);

    // Should show loading text
    await waitFor(() => {
      expect(screen.getByText(/Creazione.../i)).toBeInTheDocument();
    });

    // Resolve to clean up
    resolvePromise!({
      agentId: 'agent-1',
      chatThreadId: 'thread-1',
      agentName: 'Catan Tutor',
      kbCardCount: 3,
    });
  });
});
