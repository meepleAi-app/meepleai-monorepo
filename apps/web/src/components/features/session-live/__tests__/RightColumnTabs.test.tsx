/**
 * RightColumnTabs unit tests — Issue #2374 G1 3-col layout (Task T1)
 *
 * Tabs migrated from legacy (tools/chat/notes) → mockup (score/turn/widget/notes).
 * See plan §3 D-2 (polymorphic tab keys) + §4 T1.
 *
 * Coverage:
 * - Render shape (data-slot, tablist, tab buttons)
 * - Tab order matches mockup: Score → Turn → Widget → Notes
 * - Tab switching via click (onTabChange)
 * - Active tab aria-selected
 * - Roving tabindex (active → 0, others → -1)
 * - Keyboard nav: ArrowLeft/ArrowRight cycling (via useTablistKeyboardNav)
 * - Home/End jump
 * - Tab panel rendered with children
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { useState } from 'react';

import type { RightColumnTabsLabels, RightColumnTabsProps, LiveTab } from '../RightColumnTabs';
import { RightColumnTabs } from '../RightColumnTabs';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: RightColumnTabsLabels = {
  tabsAriaLabel: 'Colonna destra',
  tabScore: 'Score',
  tabTurn: 'Turni',
  tabWidget: 'Widget',
  tabNotes: 'Note',
  tabPhotos: 'Foto',
  tabAgent: 'Arbitro',
};

function renderTabs(overrides: Partial<RightColumnTabsProps> = {}) {
  const onTabChange = vi.fn();
  const props: RightColumnTabsProps = {
    activeTab: 'score',
    onTabChange,
    children: <div data-testid="tab-content">Contenuto</div>,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<RightColumnTabs {...props} />);
  return { ...result, onTabChange };
}

/** Controlled wrapper for keyboard tests requiring real state */
function ControlledTabs({ initialTab = 'score' as LiveTab } = {}) {
  const [activeTab, setActiveTab] = useState<LiveTab>(initialTab);
  return (
    <RightColumnTabs activeTab={activeTab} onTabChange={setActiveTab} labels={LABELS}>
      <div data-testid="panel-content">Panel: {activeTab}</div>
    </RightColumnTabs>
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RightColumnTabs — render shape', () => {
  it('renders data-slot="right-column-tabs"', () => {
    renderTabs();
    expect(
      screen.getByTestId('tab-content').closest('[data-slot="right-column-tabs"]')
    ).toBeInTheDocument();
  });

  it('renders tablist with aria-label', () => {
    renderTabs();
    expect(screen.getByRole('tablist', { name: 'Colonna destra' })).toBeInTheDocument();
  });

  it('renders 6 tab buttons', () => {
    renderTabs();
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(6);
  });

  it('renders tab labels (Score / Turni / Widget / Note / Foto / Arbitro)', () => {
    renderTabs();
    expect(screen.getByRole('tab', { name: 'Score' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Turni' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Widget' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Note' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Foto' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Arbitro' })).toBeInTheDocument();
  });

  it('renders tab buttons in order: Score, Turn, Widget, Notes, Photos, Agent', () => {
    renderTabs();
    const tablist = screen.getByRole('tablist');
    const tabs = within(tablist).getAllByRole('tab');
    const labels = tabs.map(t => t.textContent);
    expect(labels).toEqual(['Score', 'Turni', 'Widget', 'Note', 'Foto', 'Arbitro']);
  });

  it('renders tabpanel with children', () => {
    renderTabs();
    expect(screen.getByRole('tabpanel')).toBeInTheDocument();
    expect(screen.getByTestId('tab-content')).toBeInTheDocument();
  });

  it('tablist has aria-orientation="horizontal"', () => {
    renderTabs();
    expect(screen.getByRole('tablist')).toHaveAttribute('aria-orientation', 'horizontal');
  });
});

describe('RightColumnTabs — aria-selected + roving tabindex', () => {
  it('active tab has aria-selected="true"', () => {
    renderTabs({ activeTab: 'score' });
    expect(screen.getByRole('tab', { name: 'Score' })).toHaveAttribute('aria-selected', 'true');
  });

  it('inactive tabs have aria-selected="false"', () => {
    renderTabs({ activeTab: 'score' });
    expect(screen.getByRole('tab', { name: 'Turni' })).toHaveAttribute('aria-selected', 'false');
    expect(screen.getByRole('tab', { name: 'Widget' })).toHaveAttribute('aria-selected', 'false');
    expect(screen.getByRole('tab', { name: 'Note' })).toHaveAttribute('aria-selected', 'false');
    expect(screen.getByRole('tab', { name: 'Foto' })).toHaveAttribute('aria-selected', 'false');
    expect(screen.getByRole('tab', { name: 'Arbitro' })).toHaveAttribute('aria-selected', 'false');
  });

  it('active tab has tabIndex=0', () => {
    renderTabs({ activeTab: 'turn' });
    expect(screen.getByRole('tab', { name: 'Turni' })).toHaveAttribute('tabindex', '0');
  });

  it('inactive tabs have tabIndex=-1', () => {
    renderTabs({ activeTab: 'turn' });
    expect(screen.getByRole('tab', { name: 'Score' })).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('tab', { name: 'Widget' })).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('tab', { name: 'Note' })).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('tab', { name: 'Foto' })).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('tab', { name: 'Arbitro' })).toHaveAttribute('tabindex', '-1');
  });
});

describe('RightColumnTabs — click handlers', () => {
  it('calls onTabChange with "turn" when Turni tab clicked', async () => {
    const user = userEvent.setup();
    const { onTabChange } = renderTabs({ activeTab: 'score' });

    await user.click(screen.getByRole('tab', { name: 'Turni' }));
    expect(onTabChange).toHaveBeenCalledOnce();
    expect(onTabChange).toHaveBeenCalledWith('turn');
  });

  it('calls onTabChange with "widget" when Widget tab clicked', async () => {
    const user = userEvent.setup();
    const { onTabChange } = renderTabs({ activeTab: 'score' });

    await user.click(screen.getByRole('tab', { name: 'Widget' }));
    expect(onTabChange).toHaveBeenCalledWith('widget');
  });

  it('calls onTabChange with "notes" when Notes tab clicked', async () => {
    const user = userEvent.setup();
    const { onTabChange } = renderTabs({ activeTab: 'score' });

    await user.click(screen.getByRole('tab', { name: 'Note' }));
    expect(onTabChange).toHaveBeenCalledWith('notes');
  });
});

describe('RightColumnTabs — keyboard navigation', () => {
  it('ArrowRight advances from score → turn', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="score" />);

    const scoreTab = screen.getByRole('tab', { name: 'Score' });
    scoreTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(screen.getByRole('tab', { name: 'Turni' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowRight advances from turn → widget', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="turn" />);

    const turnTab = screen.getByRole('tab', { name: 'Turni' });
    turnTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(screen.getByRole('tab', { name: 'Widget' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowRight advances from widget → notes', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="widget" />);

    const widgetTab = screen.getByRole('tab', { name: 'Widget' });
    widgetTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(screen.getByRole('tab', { name: 'Note' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowRight advances from notes → photos', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="notes" />);

    const notesTab = screen.getByRole('tab', { name: 'Note' });
    notesTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(screen.getByRole('tab', { name: 'Foto' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowRight advances from photos → agent', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="photos" />);

    const photosTab = screen.getByRole('tab', { name: 'Foto' });
    photosTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(screen.getByRole('tab', { name: 'Arbitro' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowRight wraps from agent → score', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="agent" />);

    const agentTab = screen.getByRole('tab', { name: 'Arbitro' });
    agentTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(screen.getByRole('tab', { name: 'Score' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowLeft moves from turn → score', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="turn" />);

    const turnTab = screen.getByRole('tab', { name: 'Turni' });
    turnTab.focus();
    await user.keyboard('{ArrowLeft}');

    expect(screen.getByRole('tab', { name: 'Score' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowLeft wraps from score → agent', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="score" />);

    const scoreTab = screen.getByRole('tab', { name: 'Score' });
    scoreTab.focus();
    await user.keyboard('{ArrowLeft}');

    expect(screen.getByRole('tab', { name: 'Arbitro' })).toHaveAttribute('aria-selected', 'true');
  });

  it('Home jumps to first tab (score)', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="notes" />);

    const notesTab = screen.getByRole('tab', { name: 'Note' });
    notesTab.focus();
    await user.keyboard('{Home}');

    expect(screen.getByRole('tab', { name: 'Score' })).toHaveAttribute('aria-selected', 'true');
  });

  it('End jumps to last tab (agent)', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="score" />);

    const scoreTab = screen.getByRole('tab', { name: 'Score' });
    scoreTab.focus();
    await user.keyboard('{End}');

    expect(screen.getByRole('tab', { name: 'Arbitro' })).toHaveAttribute('aria-selected', 'true');
  });
});
