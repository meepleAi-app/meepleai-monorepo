/**
 * AddGameDrawer Component Tests — Issue #5168 / #5169
 *
 * Tests for the right-side Sheet drawer with method-choice wizard.
 * Covers:
 * - Rendering closed by default (no ?action=add)
 * - Opening via ?action=add URL param (AddGameDrawerController)
 * - Step 0: choice cards (manual + catalog)
 * - Step 1a: manual wizard embed
 * - Step 1b: catalog search (CatalogSearchStep)
 * - Step 2 (catalog-pdf): PDF wizard after catalog game selection
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

// Mock UserWizardClient — heavyweight component with API calls
vi.mock('@/app/(authenticated)/library/private/add/client', () => ({
  UserWizardClient: ({
    onComplete,
    onCancel,
    startAtPdf,
    gameId,
    gameName,
  }: {
    onComplete?: () => void;
    onCancel?: () => void;
    startAtPdf?: boolean;
    gameId?: string;
    gameName?: string;
  }) => (
    <div
      data-testid="user-wizard-client"
      data-start-at-pdf={startAtPdf ? 'true' : 'false'}
      data-game-id={gameId ?? ''}
      data-game-name={gameName ?? ''}
    >
      <button data-testid="wizard-complete" onClick={onComplete}>
        Complete wizard
      </button>
      <button data-testid="wizard-cancel" onClick={onCancel}>
        Cancel wizard
      </button>
    </div>
  ),
}));

// Mock CatalogSearchStep — contains React Query hooks
vi.mock('@/app/(authenticated)/library/CatalogSearchStep', () => ({
  CatalogSearchStep: ({
    onSelect,
    onBack,
  }: {
    onSelect: (gameId: string, gameName: string) => void;
    onBack: () => void;
  }) => (
    <div data-testid="catalog-search-step">
      <button
        data-testid="catalog-select-game"
        onClick={() => onSelect('game-123', 'Catan')}
      >
        Select Catan
      </button>
      <button data-testid="catalog-back" onClick={onBack}>
        Back
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

    it('does not show wizard or catalog step on Step 0', () => {
      renderDrawer({ open: true });
      expect(screen.queryByTestId('user-wizard-client')).not.toBeInTheDocument();
      expect(screen.queryByTestId('catalog-search-step')).not.toBeInTheDocument();
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

  describe('Step 1b: Catalog search', () => {
    it('shows catalog search step after clicking catalog choice', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));

      expect(screen.getByTestId('add-game-step-catalog')).toBeInTheDocument();
      expect(screen.getByTestId('catalog-search-step')).toBeInTheDocument();
    });

    it('updates title to "Add from catalog" in catalog step', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));

      expect(screen.getByTestId('add-game-drawer-title')).toHaveTextContent(
        'Add from catalog',
      );
    });

    it('goes back to choice step when catalog back is clicked', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-back'));

      expect(screen.getByTestId('add-game-step-choice')).toBeInTheDocument();
    });

    it('advances to catalog-pdf step after selecting a game from catalog', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-select-game'));

      expect(screen.getByTestId('add-game-step-catalog-pdf')).toBeInTheDocument();
      expect(screen.getByTestId('user-wizard-client')).toBeInTheDocument();
    });

    it('passes startAtPdf and game info to wizard after catalog selection', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-select-game'));

      const wizard = screen.getByTestId('user-wizard-client');
      expect(wizard).toHaveAttribute('data-start-at-pdf', 'true');
      expect(wizard).toHaveAttribute('data-game-id', 'game-123');
      expect(wizard).toHaveAttribute('data-game-name', 'Catan');
    });

    it('keeps title as "Add from catalog" in catalog-pdf step', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-select-game'));

      expect(screen.getByTestId('add-game-drawer-title')).toHaveTextContent(
        'Add from catalog',
      );
    });

    it('calls onClose when catalog-pdf wizard completes', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      renderDrawer({ open: true, onClose });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-select-game'));
      await user.click(screen.getByTestId('wizard-complete'));

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('goes back to catalog search when catalog-pdf wizard cancel is clicked', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-select-game'));
      await user.click(screen.getByTestId('wizard-cancel'));

      expect(screen.getByTestId('add-game-step-catalog')).toBeInTheDocument();
      expect(screen.getByTestId('catalog-search-step')).toBeInTheDocument();
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
