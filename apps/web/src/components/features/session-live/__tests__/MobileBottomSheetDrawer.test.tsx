/**
 * MobileBottomSheetDrawer unit tests — Issue #2374 G1 (T9).
 *
 * Coverage (per plan §4 T9):
 *   1. data-slot="mobile-bottom-sheet" regression hook
 *   2. closed by default (open=false) — drawer content not in DOM
 *   3. open=true renders drawer with drag handle + close button + tab strip
 *   4. tab strip has 4 buttons in order: Score, Turn, Widget, Notes
 *   5. clicking close button calls onOpenChange(false)
 *   6. pressing Escape calls onOpenChange(false) (a11y dialog behaviour)
 *   7. token discipline: no raw HSL; uses semantic tokens + entity-session pill on active tab
 *
 * Mockup reference: admin-mockups/design_files/sp4-session-skeleton-parts.jsx L266-286.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type {
  MobileBottomSheetDrawerLabels,
  MobileBottomSheetDrawerProps,
} from '../MobileBottomSheetDrawer';
import { MobileBottomSheetDrawer } from '../MobileBottomSheetDrawer';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: MobileBottomSheetDrawerLabels = {
  drawerTitle: 'Strumenti sessione',
  closeAriaLabel: 'Chiudi pannello',
  tabsAriaLabel: 'Tab strumenti',
  tabScore: 'Score',
  tabTurn: 'Turni',
  tabWidget: 'Widget',
  tabNotes: 'Note',
};

function renderDrawer(overrides: Partial<MobileBottomSheetDrawerProps> = {}) {
  const onOpenChange = vi.fn();
  const onTabChange = vi.fn();
  const props: MobileBottomSheetDrawerProps = {
    open: true,
    onOpenChange,
    activeTab: 'score',
    onTabChange,
    labels: LABELS,
    children: <div data-testid="drawer-body">Drawer content</div>,
    ...overrides,
  };
  const result = render(<MobileBottomSheetDrawer {...props} />);
  return { ...result, onOpenChange, onTabChange };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('MobileBottomSheetDrawer — render shape', () => {
  it('renders data-slot="mobile-bottom-sheet" (regression hook) when open=true', () => {
    renderDrawer({ open: true });
    const root = document.querySelector('[data-slot="mobile-bottom-sheet"]');
    expect(root).not.toBeNull();
  });

  it('closed by default (open=false) — drawer content not in DOM', () => {
    renderDrawer({ open: false });
    // Radix Dialog portals nothing when closed → drawer content absent
    const root = document.querySelector('[data-slot="mobile-bottom-sheet"]');
    expect(root).toBeNull();
    expect(screen.queryByTestId('drawer-body')).toBeNull();
  });

  it('open=true renders drawer with drag handle + close button + tab strip', () => {
    renderDrawer({ open: true });
    // Drag handle: aria-hidden visual cue
    const dragHandle = document.querySelector(
      '[data-slot="mobile-bottom-sheet"] [data-slot="mobile-bottom-sheet-drag-handle"]'
    );
    expect(dragHandle).not.toBeNull();
    expect(dragHandle).toHaveAttribute('aria-hidden', 'true');

    // Close button with aria-label
    const closeBtn = screen.getByRole('button', { name: LABELS.closeAriaLabel });
    expect(closeBtn).toBeInTheDocument();

    // Tab strip (role="tablist") with 4 buttons
    const tablist = screen.getByRole('tablist', { name: LABELS.tabsAriaLabel });
    expect(tablist).toBeInTheDocument();
  });
});

describe('MobileBottomSheetDrawer — tab strip', () => {
  it('tab strip has 4 buttons in order: Score, Turn, Widget, Notes', () => {
    renderDrawer({ open: true });
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(4);
    expect(tabs[0]).toHaveTextContent(LABELS.tabScore);
    expect(tabs[1]).toHaveTextContent(LABELS.tabTurn);
    expect(tabs[2]).toHaveTextContent(LABELS.tabWidget);
    expect(tabs[3]).toHaveTextContent(LABELS.tabNotes);
  });

  it('active tab has aria-selected="true" and others false', () => {
    renderDrawer({ open: true, activeTab: 'turn' });
    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveAttribute('aria-selected', 'false');
    expect(tabs[1]).toHaveAttribute('aria-selected', 'true');
    expect(tabs[2]).toHaveAttribute('aria-selected', 'false');
    expect(tabs[3]).toHaveAttribute('aria-selected', 'false');
  });

  it('clicking a tab calls onTabChange with the tab id', () => {
    const { onTabChange } = renderDrawer({ open: true, activeTab: 'score' });
    const widgetTab = screen.getAllByRole('tab')[2];
    fireEvent.click(widgetTab!);
    expect(onTabChange).toHaveBeenCalledWith('widget');
  });
});

describe('MobileBottomSheetDrawer — close interactions', () => {
  it('clicking close button calls onOpenChange(false)', () => {
    const { onOpenChange } = renderDrawer({ open: true });
    const closeBtn = screen.getByRole('button', { name: LABELS.closeAriaLabel });
    fireEvent.click(closeBtn);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('pressing Escape calls onOpenChange(false) — Radix Dialog default behaviour', () => {
    const { onOpenChange } = renderDrawer({ open: true });
    // Radix Dialog handles Escape automatically via the Root component.
    // We dispatch keydown on document; Radix listens on the dialog content.
    fireEvent.keyDown(document.activeElement ?? document.body, {
      key: 'Escape',
      code: 'Escape',
    });
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});

describe('MobileBottomSheetDrawer — token discipline', () => {
  it('uses semantic tokens + entity-session and contains no raw HSL', () => {
    renderDrawer({ open: true });
    const root = document.querySelector('[data-slot="mobile-bottom-sheet"]') as HTMLElement | null;
    expect(root).not.toBeNull();
    const html = root!.outerHTML;
    // No raw `[hsl(` literals — must use semantic tokens or entity utilities.
    expect(html).not.toMatch(/\[hsl\(/);
    // Active tab should carry entity-session colour family (border + text).
    expect(html).toMatch(/entity-session/);
  });
});
