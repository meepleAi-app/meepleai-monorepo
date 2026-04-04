/**
 * DeclareOwnershipButton + RagAccessBadge Tests
 *
 * Tests: visibility by gameState, click opens dialog, badge states
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DeclareOwnershipButton } from '../DeclareOwnershipButton';
import { RagAccessBadge } from '../RagAccessBadge';

// ============================================================================
// Mock Setup
// ============================================================================

vi.mock('@/lib/api', () => ({
  api: {
    library: { declareOwnership: vi.fn() },
    agents: { quickCreateTutor: vi.fn() },
  },
}));

vi.mock('@/components/layout/Toast', () => ({
  toast: { error: vi.fn(), success: vi.fn() },
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
  }),
}));

// ============================================================================
// DeclareOwnershipButton Tests
// ============================================================================

describe('DeclareOwnershipButton', () => {
  const defaultProps = {
    gameId: 'game-1',
    gameName: 'Catan',
    gameState: 'Nuovo',
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders when gameState is Nuovo', () => {
    render(<DeclareOwnershipButton {...defaultProps} />);
    expect(screen.getByTestId('declare-ownership-button')).toBeInTheDocument();
  });

  it('does not render when gameState is Owned', () => {
    render(<DeclareOwnershipButton {...defaultProps} gameState="Owned" />);
    expect(screen.queryByTestId('declare-ownership-button')).not.toBeInTheDocument();
  });

  it('does not render when gameState is Wishlist', () => {
    render(<DeclareOwnershipButton {...defaultProps} gameState="Wishlist" />);
    expect(screen.queryByTestId('declare-ownership-button')).not.toBeInTheDocument();
  });

  it('does not render when gameState is InPrestito', () => {
    render(<DeclareOwnershipButton {...defaultProps} gameState="InPrestito" />);
    expect(screen.queryByTestId('declare-ownership-button')).not.toBeInTheDocument();
  });

  it('click opens declaration dialog', async () => {
    const user = userEvent.setup();
    render(<DeclareOwnershipButton {...defaultProps} />);

    const button = screen.getByTestId('declare-ownership-button');
    await user.click(button);

    // Dialog should open showing the game name
    expect(screen.getByText(/Possiedi Catan\?/)).toBeInTheDocument();
  });
});

// ============================================================================
// RagAccessBadge Tests
// ============================================================================

describe('RagAccessBadge', () => {
  it('shows locked badge when no RAG access', () => {
    render(<RagAccessBadge hasRagAccess={false} isRagPublic={false} />);
    expect(screen.getByTestId('rag-badge-locked')).toBeInTheDocument();
    expect(screen.getByText(/Bloccato/i)).toBeInTheDocument();
  });

  it('shows unlocked badge when RAG access granted', () => {
    render(<RagAccessBadge hasRagAccess={true} isRagPublic={false} />);
    expect(screen.getByTestId('rag-badge-unlocked')).toBeInTheDocument();
    expect(screen.getByText(/Sbloccato/i)).toBeInTheDocument();
  });

  it('shows public badge when RAG is public (overrides unlocked)', () => {
    render(<RagAccessBadge hasRagAccess={true} isRagPublic={true} />);
    expect(screen.getByTestId('rag-badge-public')).toBeInTheDocument();
    expect(screen.getByText(/Pubblico/i)).toBeInTheDocument();
  });
});
