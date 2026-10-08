/**
 * OwnershipConfirmationDialog Component Tests
 *
 * Issue #4138: three of the five tests that lived here covered the quick-create
 * flow — `quick-create calls API and navigates to the created agent page`,
 * `Personalizza navigates to agent creation wizard`, and `shows loading state
 * during quick-create`. They are gone with the feature: declaring ownership no
 * longer creates a per-game agent. The two that survive (KB chips, the
 * "not available" variant) are kept, and the remaining cases are new.
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { OwnershipConfirmationDialog } from '../OwnershipConfirmationDialog';

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

// Kept from the original fixtures: the full OwnershipResult shape, rather than
// a partial behind a cast.
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

describe('OwnershipConfirmationDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows KB card chips when kbCardCount > 0', () => {
    render(<OwnershipConfirmationDialog {...defaultProps} ownershipResult={withKbResult} />);

    expect(screen.getByTestId('kb-card-chips')).toBeInTheDocument();
    expect(screen.getByText(/3 schede KB/)).toBeInTheDocument();
  });

  it('uses the singular label for a single KB card', () => {
    render(
      <OwnershipConfirmationDialog
        {...defaultProps}
        ownershipResult={{ ...withKbResult, kbCardCount: 1 }}
      />
    );

    expect(screen.getByText(/1 scheda KB/)).toBeInTheDocument();
  });

  it('shows the "not available" message when kbCardCount is 0', () => {
    render(<OwnershipConfirmationDialog {...defaultProps} ownershipResult={noKbResult} />);

    expect(screen.queryByTestId('kb-card-chips')).not.toBeInTheDocument();
    expect(screen.getByText(/non puoi ancora fare domande/i)).toBeInTheDocument();
  });

  // The destination is asserted, not hoped for: `/library/{id}?tab=aiChat` is
  // the canonical per-game chat, and `aiChat` is a member of GameTabId. #4118
  // is a family of redirects that point at tab ids which do not exist and
  // silently open Info — so the exact query string is the thing worth pinning.
  it('sends the user to the per-game AI chat tab', async () => {
    const user = userEvent.setup();
    render(<OwnershipConfirmationDialog {...defaultProps} ownershipResult={withKbResult} />);

    await user.click(screen.getByTestId('ownership-ask-question'));

    expect(mockPush).toHaveBeenCalledWith('/library/game-1?tab=aiChat');
  });

  // Without an indexed KB there is nothing to ask about, so the CTA must not be
  // offered — otherwise the user lands on a chat that cannot answer.
  it('offers no question CTA when nothing is indexed', () => {
    render(<OwnershipConfirmationDialog {...defaultProps} ownershipResult={noKbResult} />);

    expect(screen.queryByTestId('ownership-ask-question')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Chiudi/i })).toBeInTheDocument();
  });

  // Vocabulary is part of the decision: no user-facing surface offers to create
  // or configure an agent any more.
  it('never offers to create or customise an agent', () => {
    render(<OwnershipConfirmationDialog {...defaultProps} ownershipResult={withKbResult} />);

    expect(screen.queryByText(/Crea Tutor/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Personalizza/i)).not.toBeInTheDocument();
  });
});
