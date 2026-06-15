/**
 * SessionStateRenderer unit tests — G7 primitive (Issue #2356).
 *
 * Coverage:
 * - default: children passthrough
 * - empty: delegates to EmptyState (title + description + action)
 * - loading: skeleton with aria-live polite + aria-label
 * - error: inline banner with title + message + retry CTA
 * - sse-disconnect: delegates to ConnectionLostBanner kind='failed'
 * - exhaustiveness: assertNever throws on unknown kind (runtime defense)
 *
 * @see docs/superpowers/specs/2026-06-14-issue-2281-session-skeleton-g2-g4-g7-scope.md §G7
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { ConnectionLostBannerLabels } from '../ConnectionLostBanner';
import { SessionStateRenderer, type SessionLiveState } from '../SessionStateRenderer';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const CONNECTION_LABELS: ConnectionLostBannerLabels = {
  retryCountResolved: 'Tentativo 1/5',
  reconnecting: 'Riconnessione in corso',
  degradedPolling: 'Aggiornamenti ogni 5s',
  failed: 'Connessione persa',
  manualRetryLabel: 'Riprova',
};

// ─── default ──────────────────────────────────────────────────────────────────

describe('SessionStateRenderer — default', () => {
  it('renders children passthrough (no wrapper visible)', () => {
    const state: SessionLiveState = {
      kind: 'default',
      children: <div data-testid="passthrough-content">Score 12</div>,
    };
    render(<SessionStateRenderer state={state} />);
    expect(screen.getByTestId('passthrough-content')).toBeInTheDocument();
    // No state-slot wrapper for default (passthrough)
    expect(document.querySelector('[data-slot^="session-state-"]')).toBeNull();
  });
});

// ─── empty ────────────────────────────────────────────────────────────────────

describe('SessionStateRenderer — empty', () => {
  it('renders EmptyState with title + description', () => {
    const state: SessionLiveState = {
      kind: 'empty',
      title: 'Nessun evento',
      description: 'La sessione non ha ancora dati',
    };
    render(<SessionStateRenderer state={state} />);
    expect(document.querySelector('[data-slot="session-state-empty"]')).not.toBeNull();
    expect(screen.getByText('Nessun evento')).toBeInTheDocument();
    expect(screen.getByText('La sessione non ha ancora dati')).toBeInTheDocument();
  });

  it('renders emptyCta action button when provided', async () => {
    const onClick = vi.fn();
    const state: SessionLiveState = {
      kind: 'empty',
      title: 'Vuota',
      emptyCta: { label: 'Aggiungi giocatori', onClick },
    };
    render(<SessionStateRenderer state={state} />);
    const button = screen.getByRole('button', { name: 'Aggiungi giocatori' });
    await userEvent.click(button);
    expect(onClick).toHaveBeenCalledTimes(1);
  });
});

// ─── loading ──────────────────────────────────────────────────────────────────

describe('SessionStateRenderer — loading', () => {
  it('renders skeleton with role=status + aria-live=polite + aria-label', () => {
    const state: SessionLiveState = {
      kind: 'loading',
      loadingAriaLabel: 'Caricamento sessione in corso',
    };
    render(<SessionStateRenderer state={state} />);
    const slot = document.querySelector('[data-slot="session-state-loading"]');
    expect(slot).not.toBeNull();
    expect(slot?.getAttribute('role')).toBe('status');
    expect(slot?.getAttribute('aria-live')).toBe('polite');
    expect(slot?.getAttribute('aria-label')).toBe('Caricamento sessione in corso');
  });
});

// ─── error ────────────────────────────────────────────────────────────────────

describe('SessionStateRenderer — error', () => {
  it('renders inline banner with title + error message', () => {
    const state: SessionLiveState = {
      kind: 'error',
      error: new Error('Backend 500: handler timeout'),
      onRetry: vi.fn(),
      errorTitle: 'Errore durante il caricamento',
      retryLabel: 'Riprova',
    };
    render(<SessionStateRenderer state={state} />);
    const slot = document.querySelector('[data-slot="session-state-error"]');
    expect(slot).not.toBeNull();
    expect(slot?.getAttribute('role')).toBe('alert');
    expect(screen.getByText('Errore durante il caricamento')).toBeInTheDocument();
    expect(screen.getByText('Backend 500: handler timeout')).toBeInTheDocument();
  });

  it('fires onRetry when retry button is clicked', async () => {
    const onRetry = vi.fn();
    const state: SessionLiveState = {
      kind: 'error',
      error: new Error('Network'),
      onRetry,
      errorTitle: 'Errore',
      retryLabel: 'Riprova',
    };
    render(<SessionStateRenderer state={state} />);
    const button = screen.getByRole('button', { name: /Riprova/ });
    await userEvent.click(button);
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});

// ─── sse-disconnect ───────────────────────────────────────────────────────────

describe('SessionStateRenderer — sse-disconnect', () => {
  it('delegates to ConnectionLostBanner kind="failed"', () => {
    const state: SessionLiveState = {
      kind: 'sse-disconnect',
      connectionLabels: CONNECTION_LABELS,
    };
    render(<SessionStateRenderer state={state} />);
    expect(document.querySelector('[data-slot="session-state-sse-disconnect"]')).not.toBeNull();
    expect(document.querySelector('[data-slot="connection-lost-banner"]')).not.toBeNull();
    expect(
      document.querySelector('[data-slot="connection-lost-banner"]')?.getAttribute('data-kind')
    ).toBe('failed');
  });
});

// ─── exhaustiveness ───────────────────────────────────────────────────────────

describe('SessionStateRenderer — exhaustiveness', () => {
  it('assertNever throws on unknown kind (defense-in-depth)', () => {
    // Force an invalid state at runtime (TypeScript would catch at compile time).
    const invalid = { kind: 'totally-unknown-state' } as unknown as SessionLiveState;
    expect(() => render(<SessionStateRenderer state={invalid} />)).toThrow(/unhandled state kind/);
  });
});
