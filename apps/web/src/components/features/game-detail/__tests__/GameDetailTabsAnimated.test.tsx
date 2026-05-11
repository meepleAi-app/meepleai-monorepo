/**
 * Wave C.1 (Issue #581) — GameDetailTabsAnimated unit tests.
 *
 * Verifies WAI-ARIA APG tablist contract per Phase 0.5 sez. 4.1:
 *  - role="tablist" with aria-label
 *  - role="tab" + aria-selected + aria-controls + id per button
 *  - Keyboard nav (ArrowRight/Left/Home/End) via useTablistKeyboardNav hook
 *  - Locked tabs excluded from keyboard cycle and click
 *  - Count badges rendered only when count is defined
 *  - tabIdFor / panelIdFor helpers export correctly
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  GameDetailTabsAnimated,
  panelIdFor,
  tabIdFor,
  type GameDetailTabConfig,
} from '../GameDetailTabsAnimated';

type TabKey = 'info' | 'rules' | 'sessions' | 'agents';

const tabs: ReadonlyArray<GameDetailTabConfig<TabKey>> = [
  { key: 'info', label: 'Info', icon: '📘' },
  { key: 'rules', label: 'Regole', icon: '📜', count: 3 },
  { key: 'sessions', label: 'Partite', icon: '🎯', count: 17 },
  { key: 'agents', label: 'Agenti', icon: '🤖', count: 0, locked: true },
];

describe('GameDetailTabsAnimated (Wave C.1)', () => {
  it('renders a role="tablist" with aria-label and data-slot', () => {
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="info"
        onChange={() => {}}
        ariaLabel="Sezioni del gioco"
      />
    );
    const tablist = screen.getByRole('tablist', { name: 'Sezioni del gioco' });
    expect(tablist).toBeInTheDocument();
    expect(tablist).toHaveAttribute('data-slot', 'game-detail-tabs');
  });

  it('marks the active tab with aria-selected="true" and tabIndex=0', () => {
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="rules"
        onChange={() => {}}
        ariaLabel="Sezioni del gioco"
      />
    );
    const rules = screen.getByRole('tab', { name: /Regole/ });
    expect(rules).toHaveAttribute('aria-selected', 'true');
    expect(rules).toHaveAttribute('tabIndex', '0');

    const info = screen.getByRole('tab', { name: /Info/ });
    expect(info).toHaveAttribute('aria-selected', 'false');
    expect(info).toHaveAttribute('tabIndex', '-1');
  });

  it('wires aria-controls and id using tabIdFor/panelIdFor helpers', () => {
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="info"
        onChange={() => {}}
        ariaLabel="Sezioni del gioco"
      />
    );
    const infoTab = screen.getByRole('tab', { name: /Info/ });
    expect(infoTab).toHaveAttribute('id', tabIdFor('info'));
    expect(infoTab).toHaveAttribute('aria-controls', panelIdFor('info'));
  });

  it('calls onChange when a non-locked tab is clicked', () => {
    const onChange = vi.fn();
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="info"
        onChange={onChange}
        ariaLabel="Sezioni del gioco"
      />
    );
    fireEvent.click(screen.getByRole('tab', { name: /Regole/ }));
    expect(onChange).toHaveBeenCalledWith('rules');
  });

  it('does not call onChange when a locked tab is clicked', () => {
    const onChange = vi.fn();
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="info"
        onChange={onChange}
        ariaLabel="Sezioni del gioco"
      />
    );
    const lockedTab = screen.getByRole('tab', { name: /Agenti/ });
    expect(lockedTab).toBeDisabled();
    fireEvent.click(lockedTab);
    expect(onChange).not.toHaveBeenCalled();
  });

  it('exposes data-locked on locked tabs', () => {
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="info"
        onChange={() => {}}
        ariaLabel="Sezioni del gioco"
      />
    );
    const lockedTab = screen.getByRole('tab', { name: /Agenti/ });
    expect(lockedTab).toHaveAttribute('data-locked', 'true');
  });

  it('cycles via ArrowRight (skipping locked tabs)', () => {
    const onChange = vi.fn();
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="info"
        onChange={onChange}
        ariaLabel="Sezioni del gioco"
      />
    );
    const infoTab = screen.getByRole('tab', { name: /Info/ });
    fireEvent.keyDown(infoTab, { key: 'ArrowRight' });
    expect(onChange).toHaveBeenCalledWith('rules');
  });

  it('jumps to first via Home and last via End (excluding locked)', () => {
    const onChange = vi.fn();
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="rules"
        onChange={onChange}
        ariaLabel="Sezioni del gioco"
      />
    );
    const rulesTab = screen.getByRole('tab', { name: /Regole/ });
    fireEvent.keyDown(rulesTab, { key: 'Home' });
    expect(onChange).toHaveBeenLastCalledWith('info');

    fireEvent.keyDown(rulesTab, { key: 'End' });
    // last unlocked = sessions
    expect(onChange).toHaveBeenLastCalledWith('sessions');
  });

  it('renders count badges only when count is defined', () => {
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="info"
        onChange={() => {}}
        ariaLabel="Sezioni del gioco"
      />
    );
    const rulesTab = screen.getByRole('tab', { name: /Regole/ });
    expect(rulesTab).toHaveTextContent('3');
    const infoTab = screen.getByRole('tab', { name: /Info/ });
    // Info has no count — should not contain a bare number
    expect(infoTab.querySelectorAll('.tabular-nums')).toHaveLength(0);
  });

  it('tabIdFor and panelIdFor return consistent stable IDs', () => {
    expect(tabIdFor('info')).toBe('game-detail-tab-info');
    expect(panelIdFor('info')).toBe('game-detail-panel-info');
    expect(tabIdFor('agents')).toBe('game-detail-tab-agents');
    expect(panelIdFor('agents')).toBe('game-detail-panel-agents');
  });
});
