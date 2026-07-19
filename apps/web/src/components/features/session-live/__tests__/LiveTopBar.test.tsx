/**
 * LiveTopBar unit tests — G4 props (Issue #2352).
 *
 * Coverage:
 * - Backward compat: existing call sites (no elapsedMs, no connectionState) render unchanged
 * - G4 elapsedMs prop: timer chip rendered when provided, hidden when absent
 * - G4 connectionState prop: pip rendered for each of 3 states + aria-label
 * - Combined: both props together
 *
 * @see docs/superpowers/specs/2026-06-14-issue-2281-session-skeleton-g2-g4-g7-scope.md §G4
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { LiveTopBarLabels, LiveTopBarProps } from '../LiveTopBar';
import { LiveTopBar } from '../LiveTopBar';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const LABELS: LiveTopBarLabels = {
  sessionTitleAriaLabel: 'Sessione live: Test',
  turnLabelResolved: 'Turno 3/8',
  statusInProgress: 'In corso',
  statusPaused: 'In pausa',
  pauseCta: 'Pausa',
  resumeCta: 'Riprendi',
  endgameCta: 'Termina partita',
  exitAriaLabel: 'Esci',
  // G4 labels
  elapsedTimeAriaLabel: 'Tempo trascorso',
  // SI-4 label
  startedAtChipAriaLabel: 'Ora di inizio della sessione (derivata)',
  connectionStateAriaLabels: {
    connected: 'Connessione attiva',
    reconnecting: 'Riconnessione in corso',
    failed: 'Connessione persa',
  },
};

function renderTopBar(overrides: Partial<LiveTopBarProps> = {}) {
  const onExit = vi.fn();
  const props: LiveTopBarProps = {
    sessionName: 'Test Session',
    status: 'InProgress',
    viewerRole: 'Host',
    onExit,
    labels: LABELS,
    ...overrides,
  };
  return { ...render(<LiveTopBar {...props} />), onExit };
}

// ─── Backward compatibility ───────────────────────────────────────────────────

describe('LiveTopBar — backward compat (no G4 props)', () => {
  it('renders without timer chip when elapsedMs is undefined', () => {
    renderTopBar();
    expect(screen.queryByTestId('session-live-top-bar-timer')).toBeNull();
    expect(document.querySelector('[data-slot="session-live-top-bar-timer"]')).toBeNull();
  });

  it('renders without connection pip when connectionState is undefined', () => {
    renderTopBar();
    expect(document.querySelector('[data-slot="session-live-top-bar-connection-pip"]')).toBeNull();
  });

  it('preserves existing TopBar slot + status', () => {
    renderTopBar();
    expect(document.querySelector('[data-slot="session-live-top-bar"]')).not.toBeNull();
  });
});

// ─── SI-4 (#2635) — derived start-time chip ───────────────────────────────────

describe('LiveTopBar — SI-4 start-time chip', () => {
  it('renders the read-only start-time chip when startedAtLabel is provided', () => {
    renderTopBar({ startedAtLabel: '▶ Ora di inizio 5 lug, 20:35 · derivata' });
    const chip = document.querySelector('[data-slot="session-live-top-bar-started-at"]');
    expect(chip).not.toBeNull();
    expect(chip?.textContent).toBe('▶ Ora di inizio 5 lug, 20:35 · derivata');
    expect(chip?.getAttribute('aria-label')).toBe('Ora di inizio della sessione (derivata)');
  });

  it('hides the chip when startedAtLabel is undefined', () => {
    renderTopBar();
    expect(document.querySelector('[data-slot="session-live-top-bar-started-at"]')).toBeNull();
  });

  it('renders no editable input — the chip is display-only (Invariante 5)', () => {
    renderTopBar({ startedAtLabel: '▶ Ora di inizio 5 lug, 20:35 · derivata' });
    const chip = document.querySelector('[data-slot="session-live-top-bar-started-at"]');
    expect(chip?.querySelector('input')).toBeNull();
    expect(chip?.tagName.toLowerCase()).toBe('span'); // static text, not a control
  });
});

// ─── #3146 Slice 1 — chips are desktop-only (lg+) ──────────────────────────────

describe('LiveTopBar — session-state chips are desktop-only (lg+, #3146)', () => {
  // Below lg the chips move to LiveMobileMetaStrip (the mobile always-visible
  // counterpart), so the topbar chips are gated `lg:inline` — they belong to
  // the desktop layer (which itself switches on lg via DesktopBody/MobileBody).
  it('gates the start-time chip to lg (not md/sm)', () => {
    renderTopBar({ startedAtLabel: '▶ Ora di inizio 5 lug, 20:35 · derivata' });
    const chip = document.querySelector('[data-slot="session-live-top-bar-started-at"]');
    expect(chip?.className).toContain('lg:inline');
    expect(chip?.className).not.toContain('md:inline');
  });

  it('gates the elapsed timer chip to lg (not sm)', () => {
    renderTopBar({ elapsedMs: 60_000 });
    const chip = document.querySelector('[data-slot="session-live-top-bar-timer"]');
    expect(chip?.className).toContain('lg:inline');
    expect(chip?.className).not.toContain('sm:inline');
  });

  it('gates the turn label to lg (not sm)', () => {
    renderTopBar();
    const turn = screen.getByText('Turno 3/8');
    expect(turn.className).toContain('lg:inline');
    expect(turn.className).not.toContain('sm:inline');
  });
});

// ─── G4 — Elapsed timer chip ──────────────────────────────────────────────────

describe('LiveTopBar — G4 elapsed timer chip', () => {
  it('renders timer chip in HH:MM:SS when elapsedMs is provided', () => {
    renderTopBar({ elapsedMs: 9_296_000 }); // 02:34:56
    const chip = document.querySelector('[data-slot="session-live-top-bar-timer"]');
    expect(chip).not.toBeNull();
    expect(chip?.textContent).toBe('02:34:56');
  });

  it('renders 00:00:00 for elapsedMs=0', () => {
    renderTopBar({ elapsedMs: 0 });
    const chip = document.querySelector('[data-slot="session-live-top-bar-timer"]');
    expect(chip?.textContent).toBe('00:00:00');
  });

  it('attaches aria-label from labels.elapsedTimeAriaLabel', () => {
    renderTopBar({ elapsedMs: 60_000 });
    const chip = document.querySelector('[data-slot="session-live-top-bar-timer"]');
    expect(chip?.getAttribute('aria-label')).toBe('Tempo trascorso');
  });
});

// ─── G4 — Connection state pip ────────────────────────────────────────────────

describe('LiveTopBar — G4 connection pip', () => {
  it('renders pip with data-connection-state="connected"', () => {
    renderTopBar({ connectionState: 'connected' });
    const pip = document.querySelector('[data-slot="session-live-top-bar-connection-pip"]');
    expect(pip).not.toBeNull();
    expect(pip?.getAttribute('data-connection-state')).toBe('connected');
    expect(pip?.className).toContain('bg-emerald-400');
  });

  it('renders amber pip for connectionState="reconnecting"', () => {
    renderTopBar({ connectionState: 'reconnecting' });
    const pip = document.querySelector('[data-slot="session-live-top-bar-connection-pip"]');
    expect(pip?.getAttribute('data-connection-state')).toBe('reconnecting');
    expect(pip?.className).toContain('bg-amber-400');
  });

  it('renders destructive pip for connectionState="failed"', () => {
    renderTopBar({ connectionState: 'failed' });
    const pip = document.querySelector('[data-slot="session-live-top-bar-connection-pip"]');
    expect(pip?.getAttribute('data-connection-state')).toBe('failed');
    expect(pip?.className).toContain('bg-destructive');
  });

  it('attaches aria-label per connection state', () => {
    renderTopBar({ connectionState: 'reconnecting' });
    const pip = document.querySelector('[data-slot="session-live-top-bar-connection-pip"]');
    expect(pip?.getAttribute('aria-label')).toBe('Riconnessione in corso');
    expect(pip?.getAttribute('role')).toBe('status');
  });

  it('omits aria-label when connectionStateAriaLabels is absent', () => {
    const labelsNoConnection: LiveTopBarLabels = {
      ...LABELS,
      connectionStateAriaLabels: undefined,
    };
    renderTopBar({ connectionState: 'connected', labels: labelsNoConnection });
    const pip = document.querySelector('[data-slot="session-live-top-bar-connection-pip"]');
    expect(pip).not.toBeNull();
    expect(pip?.getAttribute('aria-label')).toBeNull();
  });
});

// ─── G4 — Combined timer + pip ────────────────────────────────────────────────

describe('LiveTopBar — G4 combined', () => {
  it('renders both timer chip and connection pip when both props provided', () => {
    renderTopBar({ elapsedMs: 125_000, connectionState: 'connected' });
    expect(document.querySelector('[data-slot="session-live-top-bar-timer"]')).not.toBeNull();
    expect(
      document.querySelector('[data-slot="session-live-top-bar-connection-pip"]')
    ).not.toBeNull();
  });
});
