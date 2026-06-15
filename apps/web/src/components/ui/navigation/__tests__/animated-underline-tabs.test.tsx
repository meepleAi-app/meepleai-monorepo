import { fireEvent, render } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AnimatedUnderlineTabs } from '../animated-underline-tabs';

type TestKey = 'a' | 'b' | 'c';

const TABS = [
  { key: 'a' as TestKey, label: 'Alpha' },
  { key: 'b' as TestKey, label: 'Beta' },
  { key: 'c' as TestKey, label: 'Gamma' },
];

function renderTabs(
  overrides: Partial<React.ComponentProps<typeof AnimatedUnderlineTabs<TestKey>>> = {}
) {
  return render(
    <AnimatedUnderlineTabs<TestKey>
      tabs={TABS}
      active="a"
      onChange={vi.fn()}
      ariaLabel="test tablist"
      {...overrides}
    />
  );
}

describe('AnimatedUnderlineTabs (#2311 DEC-D5)', () => {
  it('renders a role=tablist with the aria-label', () => {
    const { getByRole } = renderTabs();
    const tablist = getByRole('tablist');
    expect(tablist.getAttribute('aria-label')).toBe('test tablist');
  });

  it('renders one role=tab per item with aria-selected reflecting active', () => {
    const { getAllByRole } = renderTabs({ active: 'b' });
    const tabs = getAllByRole('tab');
    expect(tabs).toHaveLength(3);
    expect(tabs[0].getAttribute('aria-selected')).toBe('false');
    expect(tabs[1].getAttribute('aria-selected')).toBe('true');
    expect(tabs[2].getAttribute('aria-selected')).toBe('false');
  });

  it('default slotPrefix derives stable id + data-slot ("tab-{key}", "tab-panel-{key}")', () => {
    const { container } = renderTabs();
    expect(container.querySelector('#tab-a')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="tab"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="tabs"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="tab-indicator"]')).toBeInTheDocument();
    const firstTab = container.querySelector('#tab-a')!;
    expect(firstTab.getAttribute('aria-controls')).toBe('tab-panel-a');
  });

  it('slotPrefix override produces stable selectors for the game-detail consumer', () => {
    const { container } = renderTabs({ slotPrefix: 'game-detail-tab' });
    expect(container.querySelector('#game-detail-tab-a')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="game-detail-tab"]')).toBeInTheDocument();
    expect(container.querySelector('[data-slot="game-detail-tab-indicator"]')).toBeInTheDocument();
    expect(container.querySelector('#game-detail-tab-a')!.getAttribute('aria-controls')).toBe(
      'game-detail-panel-a'
    );
  });

  it('invokes onChange with the clicked tab key', () => {
    const onChange = vi.fn();
    const { getAllByRole } = renderTabs({ onChange });
    fireEvent.click(getAllByRole('tab')[2]);
    expect(onChange).toHaveBeenCalledWith('c');
  });

  it('does NOT invoke onChange when a locked tab is clicked', () => {
    const onChange = vi.fn();
    const lockedTabs = [...TABS.slice(0, 2), { key: 'c' as TestKey, label: 'Gamma', locked: true }];
    const { getAllByRole } = renderTabs({ tabs: lockedTabs, onChange });
    fireEvent.click(getAllByRole('tab')[2]);
    expect(onChange).not.toHaveBeenCalled();
  });

  it('renders the optional count badge when provided', () => {
    const tabsWithCount = [{ key: 'a' as TestKey, label: 'Alpha', count: 7 }];
    const { container } = render(
      <AnimatedUnderlineTabs<TestKey>
        tabs={tabsWithCount}
        active="a"
        onChange={vi.fn()}
        ariaLabel="t"
      />
    );
    expect(container.textContent).toContain('7');
  });

  it('honours custom accent classes (active label + indicator)', () => {
    const { container } = renderTabs({
      accentActiveClassName: 'text-entity-kb',
      accentIndicatorClassName: 'bg-entity-kb',
    });
    const activeTab = container.querySelector('#tab-a')!;
    expect(activeTab.className).toContain('text-entity-kb');
    const indicator = container.querySelector('[data-slot="tab-indicator"]')!;
    expect(indicator.className).toContain('bg-entity-kb');
  });
});
