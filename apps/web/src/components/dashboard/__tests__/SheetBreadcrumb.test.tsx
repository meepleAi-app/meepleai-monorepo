/**
 * @vitest-environment jsdom
 */
import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { SheetBreadcrumb } from '../sheet/SheetBreadcrumb';
import type { BreadcrumbEntry } from '../DashboardEngine';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const twoEntries: BreadcrumbEntry[] = [
  { context: 'scores', label: 'Punteggi' },
  { context: 'players', label: 'Giocatori' },
];

const threeEntries: BreadcrumbEntry[] = [
  { context: 'scores', label: 'Punteggi' },
  { context: 'rules-ai', label: 'Regole AI' },
  { context: 'players', label: 'Giocatori' },
];

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('SheetBreadcrumb', () => {
  it('renders nothing when entries has 0 items', () => {
    const { container } = render(<SheetBreadcrumb entries={[]} onNavigate={vi.fn()} />);
    expect(container.innerHTML).toBe('');
  });

  it('renders nothing when entries has exactly 1 item', () => {
    const single: BreadcrumbEntry[] = [{ context: 'scores', label: 'Punteggi' }];
    const { container } = render(<SheetBreadcrumb entries={single} onNavigate={vi.fn()} />);
    expect(container.innerHTML).toBe('');
  });

  it('renders the breadcrumb nav with 2 entries', () => {
    render(<SheetBreadcrumb entries={twoEntries} onNavigate={vi.fn()} />);
    expect(screen.getByTestId('sheet-breadcrumb')).toBeInTheDocument();
  });

  it('renders all entry labels', () => {
    render(<SheetBreadcrumb entries={twoEntries} onNavigate={vi.fn()} />);
    expect(screen.getByText('Punteggi')).toBeInTheDocument();
    expect(screen.getByText('Giocatori')).toBeInTheDocument();
  });

  it('makes non-last entries clickable buttons', () => {
    render(<SheetBreadcrumb entries={twoEntries} onNavigate={vi.fn()} />);
    const firstEntry = screen.getByTestId('breadcrumb-entry-0');
    expect(firstEntry.tagName).toBe('BUTTON');
  });

  it('last entry is not a button (not clickable)', () => {
    render(<SheetBreadcrumb entries={twoEntries} onNavigate={vi.fn()} />);
    const lastEntry = screen.getByTestId('breadcrumb-entry-1');
    expect(lastEntry.tagName).not.toBe('BUTTON');
  });

  it('calls onNavigate with correct index when non-last entry is clicked', async () => {
    const onNavigate = vi.fn();
    render(<SheetBreadcrumb entries={twoEntries} onNavigate={onNavigate} />);

    await userEvent.click(screen.getByTestId('breadcrumb-entry-0'));
    expect(onNavigate).toHaveBeenCalledTimes(1);
    expect(onNavigate).toHaveBeenCalledWith(0);
  });

  it('renders separator › between entries', () => {
    render(<SheetBreadcrumb entries={twoEntries} onNavigate={vi.fn()} />);
    expect(screen.getByText('›')).toBeInTheDocument();
  });

  it('handles three entries with correct clickability', async () => {
    const onNavigate = vi.fn();
    render(<SheetBreadcrumb entries={threeEntries} onNavigate={onNavigate} />);

    // First and second entries should be buttons
    const first = screen.getByTestId('breadcrumb-entry-0');
    const second = screen.getByTestId('breadcrumb-entry-1');
    const third = screen.getByTestId('breadcrumb-entry-2');

    expect(first.tagName).toBe('BUTTON');
    expect(second.tagName).toBe('BUTTON');
    expect(third.tagName).not.toBe('BUTTON');

    await userEvent.click(second);
    expect(onNavigate).toHaveBeenCalledWith(1);
  });
});
