/**
 * AddGameDrawer Component Tests
 *
 * Tests for the right-side Sheet drawer with simplified 2-state method-choice flow.
 * Covers:
 * - Rendering closed by default (no ?action=add)
 * - Opening via ?action=add URL param (AddGameDrawerController)
 * - Step 0: choice cards (manual + catalog)
 * - Step 1a: manual wizard embed (compactMode — 1-step game creation)
 * - Step 1b: catalog search → 1-click add → close + redirect /library/{id}
 *   (PDF/Agent setup deferred to detail-page CTAs)
 * - Closing clears ?action=add from URL
 * - Direct prop-driven open/close (AddGameDrawer)
 */

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter, useSearchParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';

import { trackEvent } from '@/lib/analytics/track-event';

import { renderWithIntl } from '../../../../__tests__/fixtures/common-fixtures';
import { AddGameDrawer, AddGameDrawerController } from '../AddGameDrawer';

// #1816 P3-5 — AddGameDrawer migrated from hardcoded EN strings to i18n keys
// (`pages.library.addGame.*`). Tests use `renderWithIntl` (loads EN test
// catalog) so existing 'Add a game'/'Add manually'/... assertions stay valid.
const render = renderWithIntl;

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
    compactMode,
  }: {
    onComplete?: () => void;
    onCancel?: () => void;
    compactMode?: boolean;
  }) => (
    <div data-testid="user-wizard-client" data-compact-mode={compactMode ? 'true' : 'false'}>
      <button data-testid="wizard-complete" onClick={onComplete}>
        Complete wizard
      </button>
      <button data-testid="wizard-cancel" onClick={onCancel}>
        Cancel wizard
      </button>
    </div>
  ),
}));

// #2012 — telemetry instrumentation for AddGameDrawer conversion funnel.
// Tests assert trackEvent is called with the expected event name + props.
vi.mock('@/lib/analytics/track-event', () => ({
  trackEvent: vi.fn(),
}));

// Mock CatalogSearchStep — contains React Query hooks
vi.mock('@/app/(authenticated)/library/CatalogSearchStep', () => ({
  CatalogSearchStep: ({
    onSelect,
    onBack,
    onGoToManual,
    onNavigateToGame,
  }: {
    onSelect: (gameId: string) => void;
    onBack: () => void;
    onGoToManual?: () => void;
    onNavigateToGame?: (gameId: string) => void;
  }) => (
    <div data-testid="catalog-search-step">
      <button data-testid="catalog-select-game" onClick={() => onSelect('game-123')}>
        Select Catan
      </button>
      <button data-testid="catalog-back" onClick={onBack}>
        Back
      </button>
      {onGoToManual && (
        <button data-testid="catalog-go-to-manual" onClick={onGoToManual}>
          Go to manual
        </button>
      )}
      {onNavigateToGame && (
        <button data-testid="catalog-navigate-to-game" onClick={() => onNavigateToGame('game-456')}>
          Vai alla scheda
        </button>
      )}
    </div>
  ),
}));

const mockRouter = { replace: vi.fn(), push: vi.fn() };
const mockUseRouter = useRouter as Mock;
const mockUseSearchParams = useSearchParams as Mock;

// ── Helpers ───────────────────────────────────────────────────────────────────

function renderDrawer(props: { open: boolean; onClose?: () => void } = { open: false }) {
  return render(<AddGameDrawer open={props.open} onClose={props.onClose ?? vi.fn()} />);
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
      expect(screen.getByText('Add from the shared catalog')).toBeInTheDocument();
    });

    it('does not show wizard or catalog step on Step 0', () => {
      renderDrawer({ open: true });
      expect(screen.queryByTestId('user-wizard-client')).not.toBeInTheDocument();
      expect(screen.queryByTestId('catalog-search-step')).not.toBeInTheDocument();
    });

    it('hides decorative choice-card icons from assistive tech (F2.2 T6 #1974)', () => {
      // T6 a11y audit: the choice cards layer an icon next to the title +
      // description. The icon is purely decorative — its wrapper must
      // carry `aria-hidden="true"` so a screen reader announces "Add
      // manually, Enter the game details…" instead of "image, Add
      // manually, …". We assert at least one aria-hidden wrapper inside
      // each choice card.
      renderDrawer({ open: true });
      const manualCard = screen.getByTestId('add-game-choice-manual');
      const catalogCard = screen.getByTestId('add-game-choice-catalog');
      expect(manualCard.querySelector('[aria-hidden="true"]')).not.toBeNull();
      expect(catalogCard.querySelector('[aria-hidden="true"]')).not.toBeNull();
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

      expect(screen.getByTestId('add-game-drawer-title')).toHaveTextContent('Add game manually');
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

      expect(screen.getByTestId('add-game-drawer-title')).toHaveTextContent('Add from catalog');
    });

    it('goes back to choice step when catalog back is clicked', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-back'));

      expect(screen.getByTestId('add-game-step-choice')).toBeInTheDocument();
    });

    it('closes drawer and redirects to detail page after catalog selection (1-click add)', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      renderDrawer({ open: true, onClose });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-select-game'));

      expect(onClose).toHaveBeenCalledTimes(1);
      expect(mockRouter.push).toHaveBeenCalledWith('/library/game-123');
    });

    it('passes compactMode={true} to wizard in manual step', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-manual'));

      const wizard = screen.getByTestId('user-wizard-client');
      expect(wizard).toHaveAttribute('data-compact-mode', 'true');
    });

    // #2269 P0-1 (M1) — when the catalog is empty, CatalogSearchStep exposes
    // an onGoToManual bridge that must transition the drawer to step "manual".
    // This breaks the dead-end in the empty-search flow.
    it('transitions to manual step when CatalogSearchStep invokes onGoToManual', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-go-to-manual'));

      expect(screen.getByTestId('add-game-step-manual')).toBeInTheDocument();
      expect(screen.queryByTestId('add-game-step-catalog')).not.toBeInTheDocument();
    });

    // #2269 P0-2 (M2 / review #2270 finding C1) — when the user clicks an
    // already-in-library card, the blocked-alert CTA triggers
    // onNavigateToGame, which the drawer wires to close + router.push to the
    // game's detail page. Pre-fix the mock destructured only onSelect/onBack/
    // onGoToManual so this wiring had no unit-level guard (only the live
    // Playwright QA in PR #2270 exercised it).
    it('closes drawer and pushes to /library/{id} when CatalogSearchStep invokes onNavigateToGame', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      renderDrawer({ open: true, onClose });

      await user.click(screen.getByTestId('add-game-choice-catalog'));
      await user.click(screen.getByTestId('catalog-navigate-to-game'));

      expect(onClose).toHaveBeenCalledTimes(1);
      expect(mockRouter.push).toHaveBeenCalledWith('/library/game-456');
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

  // #2269 P0-3 (M4) — Accessibility regression guards. The drawer must
  // expose itself as a modal dialog and be labelled by its title so screen
  // readers announce it as "Add a game, dialog". Memory `radix-dialog-no-
  // aria-modal` notes that the Radix Dialog primitive (which Sheet wraps)
  // does NOT emit `aria-modal` on its own — we force the attribute here.
  describe('Accessibility (M4)', () => {
    it('exposes role="dialog" on the SheetContent', () => {
      renderDrawer({ open: true });
      // Radix renders the modal content into a portal; the role survives.
      const dialog = screen.getByRole('dialog');
      expect(dialog).toBeInTheDocument();
    });

    it('exposes aria-modal="true" on the dialog', () => {
      renderDrawer({ open: true });
      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-modal', 'true');
    });

    it('connects aria-labelledby to the SheetTitle element', () => {
      renderDrawer({ open: true });
      const dialog = screen.getByRole('dialog');
      const labelledById = dialog.getAttribute('aria-labelledby');
      expect(labelledById).toBeTruthy();

      const title = screen.getByTestId('add-game-drawer-title');
      expect(title.id).toBe(labelledById);
    });
  });

  describe('Telemetry (#2012)', () => {
    // T3 conversion funnel instrumentation: measure drawer-open → choice
    // conversion rate, manual vs catalog ratio, abandonment without choice.
    // Once 2 weeks of data are collected, T3 UX writing review can land
    // with Adzic-style measurable thresholds (#2012 acceptance criteria).

    it('tracks library_addgame_drawer_opened when drawer opens', () => {
      renderDrawer({ open: true });

      expect(trackEvent).toHaveBeenCalledWith('library_addgame_drawer_opened');
    });

    it('does not track drawer_opened when drawer stays closed', () => {
      renderDrawer({ open: false });

      expect(trackEvent).not.toHaveBeenCalled();
    });

    it('tracks choice_selected with choice=manual on manual click', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });
      (trackEvent as Mock).mockClear();

      await user.click(screen.getByTestId('add-game-choice-manual'));

      expect(trackEvent).toHaveBeenCalledWith('library_addgame_choice_selected', {
        choice: 'manual',
      });
    });

    it('tracks choice_selected with choice=catalog on catalog click', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });
      (trackEvent as Mock).mockClear();

      await user.click(screen.getByTestId('add-game-choice-catalog'));

      expect(trackEvent).toHaveBeenCalledWith('library_addgame_choice_selected', {
        choice: 'catalog',
      });
    });

    it('tracks closed_without_choice when drawer is closed from choice step (Escape)', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });
      (trackEvent as Mock).mockClear();

      await user.keyboard('{Escape}');

      expect(trackEvent).toHaveBeenCalledWith('library_addgame_drawer_closed_without_choice');
    });

    it('does NOT track closed_without_choice when drawer is closed after a choice was selected', async () => {
      const user = userEvent.setup();
      renderDrawer({ open: true });
      await user.click(screen.getByTestId('add-game-choice-manual'));
      (trackEvent as Mock).mockClear();

      await user.keyboard('{Escape}');

      expect(trackEvent).not.toHaveBeenCalledWith('library_addgame_drawer_closed_without_choice');
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
