/**
 * PlayerTabs unit tests — Stage 3 cluster (Issue #1113).
 *
 * Verifies ARIA tablist semantics, count rendering, and keyboard nav
 * delegation to the underlying useTablistKeyboardNav hook.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { PlayerTabs, type PlayerTabKey } from '../PlayerTabs';

const labels = {
  tablistAriaLabel: 'Player sections',
  sessions: 'Partite',
  games: 'Giochi',
  toolkits: 'Toolkit',
  achievements: 'Achievement',
};

const counts: Record<PlayerTabKey, number> = {
  sessions: 23,
  games: 5,
  toolkits: 1,
  achievements: 4,
};

describe('PlayerTabs', () => {
  it('renders four tabs in canonical order with labels and counts', () => {
    render(<PlayerTabs activeTab="sessions" onChange={vi.fn()} counts={counts} labels={labels} />);

    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(4);
    expect(tabs[0]).toHaveTextContent('Partite');
    expect(tabs[0]).toHaveTextContent('23');
    expect(tabs[1]).toHaveTextContent('Giochi');
    expect(tabs[1]).toHaveTextContent('5');
    expect(tabs[2]).toHaveTextContent('Toolkit');
    expect(tabs[3]).toHaveTextContent('Achievement');
  });

  it('marks the active tab with aria-selected="true" and others with false', () => {
    render(<PlayerTabs activeTab="games" onChange={vi.fn()} counts={counts} labels={labels} />);

    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveAttribute('aria-selected', 'false');
    expect(tabs[1]).toHaveAttribute('aria-selected', 'true');
    expect(tabs[2]).toHaveAttribute('aria-selected', 'false');
    expect(tabs[3]).toHaveAttribute('aria-selected', 'false');
  });

  it('exposes a tablist with the provided aria-label', () => {
    render(<PlayerTabs activeTab="sessions" onChange={vi.fn()} counts={counts} labels={labels} />);
    expect(screen.getByRole('tablist', { name: /player sections/i })).toBeInTheDocument();
  });

  it('calls onChange with the clicked key', () => {
    const onChange = vi.fn();
    render(<PlayerTabs activeTab="sessions" onChange={onChange} counts={counts} labels={labels} />);

    fireEvent.click(screen.getByRole('tab', { name: /giochi/i }));
    expect(onChange).toHaveBeenCalledWith('games');
  });

  it('omits the count pill when the count is zero', () => {
    const zeroCounts: Record<PlayerTabKey, number> = {
      sessions: 0,
      games: 0,
      toolkits: 0,
      achievements: 0,
    };
    render(<PlayerTabs activeTab="sessions" onChange={vi.fn()} counts={zeroCounts} labels={labels} />);

    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveTextContent('Partite');
    // No numeric content beyond the label.
    expect(tabs[0].textContent).not.toMatch(/\d/);
  });

  it('moves focus to the next tab on ArrowRight (keyboard nav delegated)', () => {
    const onChange = vi.fn();
    render(<PlayerTabs activeTab="sessions" onChange={onChange} counts={counts} labels={labels} />);

    const tabs = screen.getAllByRole('tab');
    tabs[0].focus();
    fireEvent.keyDown(tabs[0], { key: 'ArrowRight' });
    expect(onChange).toHaveBeenCalledWith('games');
  });
});
