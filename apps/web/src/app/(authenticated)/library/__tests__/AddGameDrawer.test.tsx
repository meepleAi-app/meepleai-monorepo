/**
 * AddGameDrawer Component Tests — Issue #5168
 *
 * Tests for the right-side Sheet drawer with method-choice wizard.
 * Covers:
 * - Rendering closed by default (no ?action=add)
 * - Opening via ?action=add URL param (AddGameDrawerController)
 * - Step 0: choice cards (manual + catalog)
 * - Step 1a: manual wizard embed
 * - Closing clears ?action=add from URL
 * - Direct prop-driven open/close (AddGameDrawer)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter, useSearchParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';

import { AddGameDrawer, AddGameDrawerController } from '../AddGameDrawer';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
  useSearchParams: vi.fn(),
}));

// Mock UserWizardClient — it's a heavyweight component with API calls
vi.mock('@/app/(authenticated)/library/private/add/client', () => ({
  UserWizardClient: ({
    onComplete,
    onCancel,
  }: {
    onComplete?: () => void;
    onCancel?: () => void;
  }) => (
    <div data-testid="user-wizard-client">
      <button data-testid="wizard-complete" onClick={onComplete}>
        Complete wizard
      </button>
      <button data-testid="wizard-cancel" onClick={onCancel}>
        Cancel wizard
      </button>
    </div>
  ),
}));

const mockRouter = { replace: vi.fn(), push: vi.fn() };
const mockUseRouter = useRouter as Mock;
const mockUseSearchParams = useSearchParams as Mock;

// ── Helpers ───────────────────────────────────────────────────────────────────

function renderDrawer(props: { open: boolean; onClose?: () => void } = { open: false }) {
  return render(
    <AddGameDrawer open={props.open} onClose={props.onClose ?? vi.fn()} />,
  );
}

function renderController(searchParamsString = '') {
  mockUseSearchParams.mockReturnValue(new URLSearchParams(searchParamsString));
  return render(<AddGameDrawerController />);
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('AddGameDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseRouter.mockReturnValue(mockRouter);
    mockUseSearchParams.mockReturnValue(new URLSearchParams(''));
  });

  describe('Rendering', () => {
    it('does not render drawer content when closed', () => {
      renderDrawer({ open: false });
      expect(screen.queryByTestId('add-game-drawer')).not.toBeInTheDocument();
    });

    it('renders drawer content when open', () => {
      renderDrawer({ open: true });
      expect(screen.getByTestId('add-game-drawer')).toBeInTheDocument();
    });

    it('shows correct title when open on Step 0', () => {
      renderDrawer({ open: true });
      expect(screen.getByTestId('add-game-drawer-title')).toHaveTextContent('Add a game');
    });
  });

  describe('Step 0: Choice', () => {
    it('shows choice step content when open', () => {
      renderDrawer({ open: true });
      expect(screen.getByTestId('add-game-step-choice')).toBeInTheDocument();
    });

    it('shows manual choice card', () => {
      renderDrawer({ open: true });
      expect(screen.getByTestId('add-game-choice-manual')).toBeInTheDocument();
      expect(screen.getByText('Add manually')).toBeInTheDocument();
    });

    it('shows catalog choice card', () => {
      renderDrawer({ open: true });
      expect(screen.getByTestId('add-game-choice-catalog')).toBeInTheDocument();
      expect(screen.getByText('From shared catalog')).toBeInTheDocument();
    });

    it('does not show wizard yet on Step 0', () => {
      renderDrawer({ open: true });
      expect(screen.queryByTestId('user-wizard-client')).not.toBeInTheDocument();
    });
  });

  describe('Step 1a: Manual wizard', () => {
    it('shows manual wizard after clicking manual choice', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-manual'));

      expect(screen.getByTestId('add-game-step-manual')).toBeInTheDocument();
      expect(screen.getByTestId('user-wizard-client')).toBeInTheDocument();
    });

    it('updates title to "Add game manually" in manual step', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-manual'));

      expect(screen.getByTestId('add-game-drawer-title')).toHaveTextContent(
        'Add game manually',
      );
    });

    it('calls onClose when wizard completes', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      renderDrawer({ open: true, onClose });

      await user.click(screen.getByTestId('add-game-choice-manual'));
      await user.click(screen.getByTestId('wizard-complete'));

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('goes back to choice step when wizard cancel is clicked', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-manual'));
      expect(screen.getByTestId('add-game-step-manual')).toBeInTheDocument();

      await user.click(screen.getByTestId('wizard-cancel'));

      expect(screen.getByTestId('add-game-step-choice')).toBeInTheDocument();
    });
  });

  describe('Step 1b: Catalog placeholder', () => {
    it('shows catalog placeholder after clicking catalog choice', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));

      expect(screen.getByText(/coming in Issue #5169/i)).toBeInTheDocument();
    });

    it('updates title to "Add from catalog" in catalog step', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));

      expect(screen.getByTestId('add-game-drawer-title')).toHaveTextContent(
        'Add from catalog',
      );
    });

    it('goes back to choice step from catalog placeholder', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByRole('button', { name: /back/i }));

      expect(screen.getByTestId('add-game-step-choice')).toBeInTheDocument();
    });
  });

  describe('Close behavior', () => {
    it('calls onClose when Escape key pressed', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      renderDrawer({ open: true, onClose });

      await user.keyboard('{Escape}');

      expect(onClose).toHaveBeenCalledTimes(1);
    });
  });
});

describe('AddGameDrawerController', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseRouter.mockReturnValue(mockRouter);
  });

  it('renders closed when ?action=add is not in URL', () => {
    renderController('');
    expect(screen.queryByTestId('add-game-drawer')).not.toBeInTheDocument();
  });

  it('renders closed when ?action= has different value', () => {
    renderController('action=filter');
    expect(screen.queryByTestId('add-game-drawer')).not.toBeInTheDocument();
  });

  it('renders open when ?action=add is in URL', () => {
    renderController('action=add');
    expect(screen.getByTestId('add-game-drawer')).toBeInTheDocument();
  });

  it('shows choice step when ?action=add is present', () => {
    renderController('action=add');
    expect(screen.getByTestId('add-game-step-choice')).toBeInTheDocument();
  });

  it('calls router.replace to remove action=add on close (Escape)', async () => {
    const user = userEvent.setup();
    renderController('action=add');

    await user.keyboard('{Escape}');

    expect(mockRouter.replace).toHaveBeenCalledWith('/library');
  });

  it('preserves other URL params when closing', async () => {
    const user = userEvent.setup();
    renderController('tab=collection&action=add');

    await user.keyboard('{Escape}');

    expect(mockRouter.replace).toHaveBeenCalledWith('/library?tab=collection');
  });
});
