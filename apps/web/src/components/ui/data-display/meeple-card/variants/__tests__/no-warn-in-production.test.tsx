import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render } from '@testing-library/react';

/**
 * Production-safety guard for spec §11 risk #2.
 *
 * `useConnectionSource` emits a W4 dev-warn when callers pass `navItems` (the
 * deprecated path). That warn must be suppressed in production builds to avoid
 * log noise in deployed environments.
 *
 * The underlying gate lives in `hooks/devWarn.ts` (`devWarnOnce` short-circuits
 * when `process.env.NODE_ENV === 'production'`). This test exercises the full
 * MeepleCard render path with `navItems` set, ensuring no console.warn is
 * emitted under the production env stub.
 *
 * Refs:
 * - docs/superpowers/specs/2026-04-23-connectionchip-step-1.6-renderer-integration.md §11
 * - docs/superpowers/plans/2026-04-23-connectionchip-step-1.6-renderer-integration.md §10
 */
describe('deprecation warn is production-safe', () => {
  beforeEach(() => {
    vi.stubEnv('NODE_ENV', 'production');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.restoreAllMocks();
  });

  it('emits no warn in production when navItems is used', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const { MeepleCard } = await import('../../MeepleCard');
    render(
      <MeepleCard
        entity="game"
        title="X"
        navItems={[{ label: 'L', entity: 'session', icon: null }]}
      />
    );
    expect(warn).not.toHaveBeenCalled();
  });
});
