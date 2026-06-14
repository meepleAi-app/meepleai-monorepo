/**
 * SearchPill — unit tests (#2320 CommandPalette wiring).
 *
 * Covers the basic trigger contract: rendering, the dispatched window event,
 * and the a11y label.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SearchPill } from '../SearchPill';

describe('SearchPill', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('renders the search-pill trigger button', () => {
    render(<SearchPill />);
    expect(screen.getByTestId('topbar-search-pill')).toBeInTheDocument();
  });

  it('dispatches `meeple:command-palette:open` when clicked', () => {
    const spy = vi.spyOn(window, 'dispatchEvent');
    render(<SearchPill />);
    fireEvent.click(screen.getByTestId('topbar-search-pill'));
    expect(spy).toHaveBeenCalledTimes(1);
    const event = spy.mock.calls[0][0] as CustomEvent;
    expect(event.type).toBe('meeple:command-palette:open');
  });

  it('exposes an a11y label mentioning the keyboard shortcut', () => {
    render(<SearchPill />);
    const button = screen.getByTestId('topbar-search-pill');
    expect(button.getAttribute('aria-label')).toMatch(/ricerca globale/i);
    // platform-dependent (⌘ on mac, Ctrl on win/linux) — just check the K suffix.
    expect(button.getAttribute('aria-label')).toMatch(/K\)$/);
  });
});
