/**
 * RightColumnTabs unit tests — Wave D.2 Interactions sub-PR (Issue #750)
 *
 * Coverage:
 * - Render shape (data-slot, tablist, tab buttons)
 * - Tab switching via click (onTabChange)
 * - Active tab aria-selected
 * - Roving tabindex (active → 0, others → -1)
 * - Keyboard nav: ArrowLeft/ArrowRight cycling (via useTablistKeyboardNav)
 * - Home/End jump
 * - Tab panel rendered with children
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { useState } from 'react';

import type { RightColumnTabsLabels, RightColumnTabsProps, LiveTab } from '../RightColumnTabs';
import { RightColumnTabs } from '../RightColumnTabs';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: RightColumnTabsLabels = {
  tabsAriaLabel: 'Colonna destra',
  tabTools: 'Strumenti',
  tabChat: 'Chat',
  tabNotes: 'Note',
};

function renderTabs(overrides: Partial<RightColumnTabsProps> = {}) {
  const onTabChange = vi.fn();
  const props: RightColumnTabsProps = {
    activeTab: 'tools',
    onTabChange,
    children: <div data-testid="tab-content">Contenuto</div>,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<RightColumnTabs {...props} />);
  return { ...result, onTabChange };
}

/** Controlled wrapper for keyboard tests requiring real state */
function ControlledTabs({ initialTab = 'tools' as LiveTab } = {}) {
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

  it('renders 3 tab buttons', () => {
    renderTabs();
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(3);
  });

  it('renders tab labels', () => {
    renderTabs();
    expect(screen.getByRole('tab', { name: 'Strumenti' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Chat' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Note' })).toBeInTheDocument();
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
    renderTabs({ activeTab: 'tools' });
    expect(screen.getByRole('tab', { name: 'Strumenti' })).toHaveAttribute('aria-selected', 'true');
  });

  it('inactive tabs have aria-selected="false"', () => {
    renderTabs({ activeTab: 'tools' });
    expect(screen.getByRole('tab', { name: 'Chat' })).toHaveAttribute('aria-selected', 'false');
    expect(screen.getByRole('tab', { name: 'Note' })).toHaveAttribute('aria-selected', 'false');
  });

  it('active tab has tabIndex=0', () => {
    renderTabs({ activeTab: 'chat' });
    expect(screen.getByRole('tab', { name: 'Chat' })).toHaveAttribute('tabindex', '0');
  });

  it('inactive tabs have tabIndex=-1', () => {
    renderTabs({ activeTab: 'chat' });
    expect(screen.getByRole('tab', { name: 'Strumenti' })).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('tab', { name: 'Note' })).toHaveAttribute('tabindex', '-1');
  });
});

describe('RightColumnTabs — click handlers', () => {
  it('calls onTabChange with "chat" when Chat tab clicked', async () => {
    const user = userEvent.setup();
    const { onTabChange } = renderTabs({ activeTab: 'tools' });

    await user.click(screen.getByRole('tab', { name: 'Chat' }));
    expect(onTabChange).toHaveBeenCalledOnce();
    expect(onTabChange).toHaveBeenCalledWith('chat');
  });

  it('calls onTabChange with "notes" when Notes tab clicked', async () => {
    const user = userEvent.setup();
    const { onTabChange } = renderTabs({ activeTab: 'tools' });

    await user.click(screen.getByRole('tab', { name: 'Note' }));
    expect(onTabChange).toHaveBeenCalledWith('notes');
  });
});

describe('RightColumnTabs — keyboard navigation', () => {
  it('ArrowRight advances from tools → chat', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="tools" />);

    const toolsTab = screen.getByRole('tab', { name: 'Strumenti' });
    toolsTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(screen.getByRole('tab', { name: 'Chat' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowRight wraps from notes → tools', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="notes" />);

    const notesTab = screen.getByRole('tab', { name: 'Note' });
    notesTab.focus();
    await user.keyboard('{ArrowRight}');

    expect(screen.getByRole('tab', { name: 'Strumenti' })).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowLeft moves from chat → tools', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="chat" />);

    const chatTab = screen.getByRole('tab', { name: 'Chat' });
    chatTab.focus();
    await user.keyboard('{ArrowLeft}');

    expect(screen.getByRole('tab', { name: 'Strumenti' })).toHaveAttribute('aria-selected', 'true');
  });

  it('Home jumps to first tab (tools)', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="notes" />);

    const notesTab = screen.getByRole('tab', { name: 'Note' });
    notesTab.focus();
    await user.keyboard('{Home}');

    expect(screen.getByRole('tab', { name: 'Strumenti' })).toHaveAttribute('aria-selected', 'true');
  });

  it('End jumps to last tab (notes)', async () => {
    const user = userEvent.setup();
    render(<ControlledTabs initialTab="tools" />);

    const toolsTab = screen.getByRole('tab', { name: 'Strumenti' });
    toolsTab.focus();
    await user.keyboard('{End}');

    expect(screen.getByRole('tab', { name: 'Note' })).toHaveAttribute('aria-selected', 'true');
  });
});
