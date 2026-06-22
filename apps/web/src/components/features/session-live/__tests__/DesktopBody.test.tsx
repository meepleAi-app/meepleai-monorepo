/**
 * DesktopBody unit tests — G1 #2374 60/40 layout contract.
 *
 * Coverage:
 * - Regression: data-slot="desktop-body" still present.
 * - New contract: mainColumn + rightColumn slots → 2-zone 60/40 grid.
 * - Back-compat: legacy leftSidebar + centerColumn path still works.
 * - Token discipline: no raw HSL literals (R-4).
 * - Responsive: hidden on mobile (< lg).
 *
 * @see docs/superpowers/plans/2026-06-15-issue-2374-session-live-g1-3-col-layout.md §4 T2
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DesktopBody } from '../DesktopBody';

// ─── Regression: existing data-slot ───────────────────────────────────────────

describe('DesktopBody — regression', () => {
  it('renders data-slot="desktop-body"', () => {
    render(
      <DesktopBody
        mainColumn={<div data-testid="main-zone">Main</div>}
        rightColumn={<div data-testid="right-zone">Right</div>}
      />
    );

    expect(document.querySelector('[data-slot="desktop-body"]')).not.toBeNull();
  });
});

// ─── New 2-zone 60/40 contract ────────────────────────────────────────────────

describe('DesktopBody — new 60/40 layout (mainColumn + rightColumn)', () => {
  it('renders mainColumn + rightColumn slots', () => {
    render(
      <DesktopBody
        mainColumn={<div data-testid="main-zone">Main content</div>}
        rightColumn={<div data-testid="right-zone">Right content</div>}
      />
    );

    expect(screen.getByTestId('main-zone')).toBeInTheDocument();
    expect(screen.getByTestId('right-zone')).toBeInTheDocument();
  });

  it('applies 60/40 grid template via grid-cols-[minmax(0,3fr)_minmax(0,2fr)]', () => {
    render(
      <DesktopBody
        mainColumn={<div data-testid="main-zone">Main</div>}
        rightColumn={<div data-testid="right-zone">Right</div>}
      />
    );

    const root = document.querySelector('[data-slot="desktop-body"]');
    expect(root).not.toBeNull();
    expect(root?.className).toContain('lg:grid-cols-[minmax(0,3fr)_minmax(0,2fr)]');
  });

  it('omits leftSidebar slot when undefined (back-compat path uses mainColumn only)', () => {
    render(
      <DesktopBody
        mainColumn={<div data-testid="main-zone">Main</div>}
        rightColumn={<div data-testid="right-zone">Right</div>}
      />
    );

    // No legacy left-sidebar zone should be rendered when mainColumn is used.
    expect(screen.queryByTestId('legacy-left-sidebar')).toBeNull();
    const root = document.querySelector('[data-slot="desktop-body"]');
    expect(root?.getAttribute('data-layout')).toBe('2col-60-40');
  });
});

// ─── Back-compat: 3-zone legacy path ──────────────────────────────────────────

describe('DesktopBody — legacy 3-zone path (leftSidebar + centerColumn)', () => {
  it('still mounts leftSidebar when provided (legacy callers)', () => {
    render(
      <DesktopBody
        leftSidebar={<div data-testid="legacy-left-sidebar">Left</div>}
        centerColumn={<div data-testid="legacy-center">Center</div>}
        rightColumn={<div data-testid="legacy-right">Right</div>}
      />
    );

    expect(screen.getByTestId('legacy-left-sidebar')).toBeInTheDocument();
    expect(screen.getByTestId('legacy-center')).toBeInTheDocument();
    expect(screen.getByTestId('legacy-right')).toBeInTheDocument();
    const root = document.querySelector('[data-slot="desktop-body"]');
    expect(root?.getAttribute('data-layout')).toBe('3col-legacy');
  });
});

// ─── Token discipline (R-4) ───────────────────────────────────────────────────

describe('DesktopBody — token discipline', () => {
  it('root uses semantic tokens (no raw HSL)', () => {
    const { container } = render(
      <DesktopBody
        mainColumn={<div data-testid="main-zone">Main</div>}
        rightColumn={<div data-testid="right-zone">Right</div>}
      />
    );

    // Walk the entire subtree — no element should carry a raw HSL literal.
    const allElements = container.querySelectorAll('*');
    allElements.forEach(el => {
      expect(el.className.toString()).not.toContain('bg-[hsl(');
    });
  });
});

// ─── Responsive ───────────────────────────────────────────────────────────────

describe('DesktopBody — responsive', () => {
  it('hidden on mobile (< lg)', () => {
    render(
      <DesktopBody
        mainColumn={<div data-testid="main-zone">Main</div>}
        rightColumn={<div data-testid="right-zone">Right</div>}
      />
    );

    const root = document.querySelector('[data-slot="desktop-body"]');
    expect(root?.className).toContain('hidden');
    expect(root?.className).toMatch(/lg:/);
  });
});
