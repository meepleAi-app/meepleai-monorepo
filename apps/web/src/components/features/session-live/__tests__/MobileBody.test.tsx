/**
 * MobileBody unit tests — Issue #2374 G1 (T9 refactor).
 *
 * Bottom-sheet pattern (sess.46r): mockup parts.jsx L266-286.
 * Replaces legacy 4-tab bottom-nav (Foundation #746) — the old 4-tab union
 * (score|log|tools|chat) is DELETED in favour of LiveTab (score|turn|widget|notes)
 * shared with the desktop RIGHT column.
 *
 * Coverage:
 *   1. default render = mainContent + floating action button (no bottom nav)
 *   2. FAB has data-slot="mobile-body-open-sheet" + i18n aria-label
 *   3. FAB click calls onSheetOpenChange(true)
 *   4. sheetOpen=false hides drawer content
 *   5. sheetOpen=true mounts MobileBottomSheetDrawer with sheetContent
 *   6. drawer tab click forwards to onSheetTabChange
 *   7. token discipline: no raw HSL literals
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { MobileBodyLabels, MobileBodyProps } from '../MobileBody';
import { MobileBody } from '../MobileBody';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: MobileBodyLabels = {
  openSheetCta: 'Apri pannello',
  closeSheetAriaLabel: 'Chiudi pannello',
  drawerTitle: 'Strumenti sessione',
  tabsAriaLabel: 'Tab strumenti',
  tabFlavor: 'Catan',
  tabScore: 'Score',
  tabTurn: 'Turni',
  tabWidget: 'Widget',
  tabNotes: 'Note',
  tabPhotos: 'Foto',
  tabAgent: 'Arbitro',
};

function renderBody(overrides: Partial<MobileBodyProps> = {}) {
  const onSheetOpenChange = vi.fn();
  const onSheetTabChange = vi.fn();
  const props: MobileBodyProps = {
    mainContent: <div data-testid="main-content">main</div>,
    sheetOpen: false,
    onSheetOpenChange,
    sheetActiveTab: 'score',
    onSheetTabChange,
    sheetContent: <div data-testid="sheet-content">tab body</div>,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<MobileBody {...props} />);
  return { ...result, onSheetOpenChange, onSheetTabChange };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('MobileBody — default (closed sheet)', () => {
  it('renders data-slot="mobile-body" with mainContent visible', () => {
    const { container } = renderBody({ sheetOpen: false });
    const root = container.querySelector('[data-slot="mobile-body"]');
    expect(root).not.toBeNull();
    expect(screen.getByTestId('main-content')).toBeInTheDocument();
    // Drawer content is not in DOM when closed
    expect(screen.queryByTestId('sheet-content')).toBeNull();
  });

  it('renders data-layout="mobile-sheet" on root', () => {
    const { container } = renderBody();
    const root = container.querySelector('[data-slot="mobile-body"]');
    expect(root).toHaveAttribute('data-layout', 'mobile-sheet');
  });

  it('floating action button has data-slot="mobile-body-open-sheet" + aria-label', () => {
    renderBody();
    const fab = screen.getByRole('button', { name: LABELS.openSheetCta });
    expect(fab).toBeInTheDocument();
    expect(fab).toHaveAttribute('data-slot', 'mobile-body-open-sheet');
  });

  it('clicking FAB calls onSheetOpenChange(true)', () => {
    const { onSheetOpenChange } = renderBody({ sheetOpen: false });
    fireEvent.click(screen.getByRole('button', { name: LABELS.openSheetCta }));
    expect(onSheetOpenChange).toHaveBeenCalledWith(true);
  });

  it('NO bottom-nav (legacy 4-tab pattern removed)', () => {
    const { container } = renderBody();
    // No legacy mobile-body-tab buttons should exist
    const legacyTabs = container.querySelectorAll('[data-slot="mobile-body-tab"]');
    expect(legacyTabs).toHaveLength(0);
  });
});

describe('MobileBody — open sheet', () => {
  it('sheetOpen=true mounts MobileBottomSheetDrawer with sheetContent', () => {
    renderBody({ sheetOpen: true });
    // Drawer root portal mounted
    expect(document.querySelector('[data-slot="mobile-bottom-sheet"]')).not.toBeNull();
    expect(screen.getByTestId('sheet-content')).toBeInTheDocument();
  });

  it('drawer tab click forwards to onSheetTabChange', () => {
    const { onSheetTabChange } = renderBody({ sheetOpen: true, sheetActiveTab: 'score' });
    // 4 tabs in drawer; click 'turn'
    const turnTab = screen.getAllByRole('tab')[1];
    fireEvent.click(turnTab!);
    expect(onSheetTabChange).toHaveBeenCalledWith('turn');
  });

  it('drawer close button forwards to onSheetOpenChange(false)', () => {
    const { onSheetOpenChange } = renderBody({ sheetOpen: true });
    const closeBtn = screen.getByRole('button', { name: LABELS.closeSheetAriaLabel });
    fireEvent.click(closeBtn);
    expect(onSheetOpenChange).toHaveBeenCalledWith(false);
  });
});

describe('MobileBody — token discipline', () => {
  it('contains no raw HSL literals', () => {
    const { container } = renderBody({ sheetOpen: true });
    const root = container.querySelector('[data-slot="mobile-body"]') as HTMLElement | null;
    expect(root).not.toBeNull();
    expect(root!.outerHTML).not.toMatch(/\[hsl\(/);
  });
});
