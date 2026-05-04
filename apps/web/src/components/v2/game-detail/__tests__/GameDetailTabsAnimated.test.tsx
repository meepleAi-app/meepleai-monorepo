/**
 * Wave C.1 (Issue #581) — GameDetailTabsAnimated unit tests.
 *
 * Verifies WAI-ARIA APG tablist contract via `useTablistKeyboardNav` hook
 * (Wave A.6 PR #623) plus the animated underline indicator wiring.
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { GameDetailTabsAnimated, type GameDetailTabConfig } from '../GameDetailTabsAnimated';

type TabKey = 'info' | 'rules' | 'sessions' | 'agents';

const tabs: ReadonlyArray<GameDetailTabConfig<TabKey>> = [
  { key: 'info', label: 'Info', icon: '📘' },
  { key: 'rules', label: 'Regole', icon: '📜', count: 3 },
  { key: 'sessions', label: 'Partite', icon: '🎯', count: 17 },
  { key: 'agents', label: 'Agenti', icon: '🤖', count: 0, locked: true },
];

describe('GameDetailTabsAnimated (Wave C.1)', () => {
  it('renders a role="tablist" with aria-label', () => {
    render(
      <GameDetailTabsAnimated
        tabs={tabs}
        active="info"
        onChange={() => {}}
        ariaLabel="Sezioni del gioco"
      />
    );
    expect(screen.getByRole('tablist', { name: 'Sezioni del gioco' })).toBeInTheDocument();
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
    expect(infoTab).not.toHaveTextContent(/^\d+$/);
  });
});
