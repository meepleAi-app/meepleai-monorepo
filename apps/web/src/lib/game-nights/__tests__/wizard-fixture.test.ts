/**
 * Unit tests for the visual-regression fixture (issue #950 W2 Foundation).
 *
 * These tests stub `IS_VISUAL_TEST_BUILD` to true via env var so we can
 * exercise the fixture-on path without polluting production build output.
 */

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

describe('parseFixtureParam', () => {
  it('returns the matching state for known fixture identifiers', async () => {
    const { parseFixtureParam } = await import('../wizard-fixture');
    expect(parseFixtureParam('step1-date')).toBe('step1-date');
    expect(parseFixtureParam('step3-filled')).toBe('step3-filled');
    expect(parseFixtureParam('desktop-split')).toBe('desktop-split');
  });

  it('returns null for unknown / nullish input', async () => {
    const { parseFixtureParam } = await import('../wizard-fixture');
    expect(parseFixtureParam(null)).toBeNull();
    expect(parseFixtureParam(undefined)).toBeNull();
    expect(parseFixtureParam('')).toBeNull();
    expect(parseFixtureParam('unknown-state')).toBeNull();
  });
});

describe('getWizardFixture (production build)', () => {
  it('returns null for every state when IS_VISUAL_TEST_BUILD is false', async () => {
    // Default Vitest env: NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED is not '1'
    const { getWizardFixture } = await import('../wizard-fixture');
    expect(getWizardFixture('step1-date')).toBeNull();
    expect(getWizardFixture('step3-filled')).toBeNull();
    expect(getWizardFixture('desktop-split')).toBeNull();
  });
});

describe('getWizardFixture (visual-test build)', () => {
  const previousEnv = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED;

  beforeEach(() => {
    process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED = '1';
  });

  afterEach(() => {
    if (previousEnv === undefined) {
      delete process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED;
    } else {
      process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED = previousEnv;
    }
  });

  it('returns a WizardState for every FSM identifier (10 states)', async () => {
    // Re-import in this env so the module-level IS_VISUAL_TEST_BUILD picks up
    // the override. The import path is identical so vitest serves the cached
    // module — bust it explicitly.
    vi.resetModules();
    const { getWizardFixture } = await import('../wizard-fixture');

    const identifiers = [
      'step1-date',
      'step1-warning',
      'step2-location',
      'step3-empty',
      'step3-typing',
      'step3-filled',
      'step4-games',
      'step4-decide-group',
      'mobile-step-flow',
      'desktop-split',
    ] as const;

    for (const id of identifiers) {
      const state = getWizardFixture(id);
      expect(state, `fixture for ${id} should be non-null in visual-test build`).not.toBeNull();
    }
  });

  it('step1-warning exposes a non-empty conflictResult', async () => {
    vi.resetModules();
    const { getWizardFixture } = await import('../wizard-fixture');

    const state = getWizardFixture('step1-warning');
    expect(state).not.toBeNull();
    expect(state?.date.conflictResult?.hasConflict).toBe(true);
    expect(state?.date.conflictResult?.conflicts.length).toBeGreaterThan(0);
  });

  it('step3-filled exposes 6 invitees mixing registered + email', async () => {
    vi.resetModules();
    const { getWizardFixture } = await import('../wizard-fixture');

    const state = getWizardFixture('step3-filled');
    expect(state?.invitees).toHaveLength(6);
    const userCount = state?.invitees.filter(i => i.kind === 'user').length ?? 0;
    const emailCount = state?.invitees.filter(i => i.kind === 'email').length ?? 0;
    expect(userCount).toBeGreaterThan(0);
    expect(emailCount).toBeGreaterThan(0);
  });

  it('step4-decide-group sets decideAtGroup=true and clears selection', async () => {
    vi.resetModules();
    const { getWizardFixture } = await import('../wizard-fixture');

    const state = getWizardFixture('step4-decide-group');
    expect(state?.games.decideAtGroup).toBe(true);
    expect(state?.games.selected).toHaveLength(0);
  });
});

// vi import resolution for tests above
import { vi } from 'vitest';
