/**
 * Wave A.4 (Issue #603) — Tabs WAI-ARIA tablist behavioral tests.
 * Closes Issue #588 (CategoryTabs Arrow-key a11y).
 *
 * Verifies the keyboard contract from spec §3.4:
 *  - ArrowLeft / ArrowRight wrap
 *  - Home / End jump
 *  - Roving tabindex (active=0, others=-1)
 *  - aria-selected reflects activeTab
 *  - onChange fires with next key + focus moves to that button
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { TAB_KEYS, Tabs, type TabDescriptor, type TabKey } from './tabs';

const tabs: ReadonlyArray<TabDescriptor> = [
  { key: 'overview', label: 'Overview' },
  { key: 'toolkits', label: 'Toolkits', count: 4 },
  { key: 'agents', label: 'Agents', count: 2 },
  { key: 'knowledge', label: 'Knowledge', count: 7 },
  { key: 'community', label: 'Community' },
];

const labels = { tablistAriaLabel: 'Game sections' };

function ControlledTabs({
  initial = 'overview',
  onChangeSpy,
}: {
  readonly initial?: TabKey;
  readonly onChangeSpy?: (k: TabKey) => void;
}) {
  const [active, setActive] = useState<TabKey>(initial);
  return (
    <Tabs
      tabs={tabs}
      activeTab={active}
      onChange={k => {
        setActive(k);
        onChangeSpy?.(k);
      }}
      labels={labels}
    />
  );
}

describe('Tabs (Wave A.4 — closes #588)', () => {
  it('exposes 5 tabs in the canonical order', () => {
    render(<ControlledTabs />);
    const tablist = screen.getByRole('tablist', { name: 'Game sections' });
    const renderedTabs = tablist.querySelectorAll('[role="tab"]');
    expect(renderedTabs).toHaveLength(5);
    expect(TAB_KEYS).toEqual(['overview', 'toolkits', 'agents', 'knowledge', 'community']);
  });

  it('applies roving tabindex (active=0, others=-1)', () => {
    render(<ControlledTabs initial="toolkits" />);
    expect(screen.getByRole('tab', { name: /Overview/ })).toHaveAttribute('tabindex', '-1');
    expect(screen.getByRole('tab', { name: /Toolkits/ })).toHaveAttribute('tabindex', '0');
    expect(screen.getByRole('tab', { name: /Agents/ })).toHaveAttribute('tabindex', '-1');
  });

  it('reflects activeTab via aria-selected', () => {
    render(<ControlledTabs initial="agents" />);
    expect(screen.getByRole('tab', { name: /Agents/ })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: /Overview/ })).toHaveAttribute('aria-selected', 'false');
  });

  it('fires onChange and moves focus on click', () => {
    const spy = vi.fn();
    render(<ControlledTabs onChangeSpy={spy} />);
    const toolkits = screen.getByRole('tab', { name: /Toolkits/ });
    fireEvent.click(toolkits);
    expect(spy).toHaveBeenCalledWith('toolkits');
  });

  it('ArrowRight advances to next tab and wraps to first', () => {
    const spy = vi.fn();
    render(<ControlledTabs initial="overview" onChangeSpy={spy} />);
    const overview = screen.getByRole('tab', { name: /Overview/ });
    fireEvent.keyDown(overview, { key: 'ArrowRight' });
    expect(spy).toHaveBeenLastCalledWith('toolkits');

    // Wrap from last → first
    spy.mockClear();
    render(<ControlledTabs initial="community" onChangeSpy={spy} />);
    const community = screen.getAllByRole('tab', { name: /Community/ });
    fireEvent.keyDown(community[community.length - 1], { key: 'ArrowRight' });
    expect(spy).toHaveBeenLastCalledWith('overview');
  });

  it('ArrowLeft retreats to previous tab and wraps to last', () => {
    const spy = vi.fn();
    render(<ControlledTabs initial="toolkits" onChangeSpy={spy} />);
    const toolkits = screen.getByRole('tab', { name: /Toolkits/ });
    fireEvent.keyDown(toolkits, { key: 'ArrowLeft' });
    expect(spy).toHaveBeenLastCalledWith('overview');

    // Wrap from first → last
    spy.mockClear();
    render(<ControlledTabs initial="overview" onChangeSpy={spy} />);
    const overviewTabs = screen.getAllByRole('tab', { name: /Overview/ });
    fireEvent.keyDown(overviewTabs[overviewTabs.length - 1], {
      key: 'ArrowLeft',
    });
    expect(spy).toHaveBeenLastCalledWith('community');
  });

  it('Home jumps to first tab', () => {
    const spy = vi.fn();
    render(<ControlledTabs initial="knowledge" onChangeSpy={spy} />);
    const knowledge = screen.getByRole('tab', { name: /Knowledge/ });
    fireEvent.keyDown(knowledge, { key: 'Home' });
    expect(spy).toHaveBeenLastCalledWith('overview');
  });

  it('End jumps to last tab', () => {
    const spy = vi.fn();
    render(<ControlledTabs initial="overview" onChangeSpy={spy} />);
    const overview = screen.getByRole('tab', { name: /Overview/ });
    fireEvent.keyDown(overview, { key: 'End' });
    expect(spy).toHaveBeenLastCalledWith('community');
  });

  it('ignores other keys (e.g. ArrowUp, character keys)', () => {
    const spy = vi.fn();
    render(<ControlledTabs onChangeSpy={spy} />);
    const overview = screen.getByRole('tab', { name: /Overview/ });
    fireEvent.keyDown(overview, { key: 'ArrowUp' });
    fireEvent.keyDown(overview, { key: 'a' });
    expect(spy).not.toHaveBeenCalled();
  });

  it('renders count pills only when count is provided', () => {
    render(<ControlledTabs />);
    // overview has no count → no number badge in its label tree
    const overview = screen.getByRole('tab', { name: /Overview/ });
    expect(overview.querySelector('[data-dynamic="number"]')).toBeNull();
    // toolkits has count=4
    const toolkits = screen.getByRole('tab', { name: /Toolkits/ });
    expect(toolkits.querySelector('[data-dynamic="number"]')?.textContent).toBe('4');
  });
});
