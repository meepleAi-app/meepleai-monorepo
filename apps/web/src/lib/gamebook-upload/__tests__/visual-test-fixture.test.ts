/**
 * Unit tests for gamebook-upload visual fixture (SP6 Phase C.1.A Foundation).
 */

import { describe, expect, it } from 'vitest';

import { BggSearchResultSchema, CatalogGameRefSchema, wizardFixtureKindSchema } from '../schemas';
import { STATE_OVERRIDE_ENABLED, parseStateOverride, wizardFixtures } from '../visual-test-fixture';

import type { WizardFixtureKind } from '../schemas';

const ALL_FIXTURE_KINDS: readonly WizardFixtureKind[] = [
  'step1-default',
  'step1-searching',
  'step1-no-results',
  'step1-bgg-loading',
  'step2-ready',
  'step2-capturing',
  'step2-low-light',
  'step2-failed',
  'step2-denied',
  'step3-progress',
  'step3-partial',
  'step3-complete',
  'step3-offline',
  'step3-cancel-modal',
];

// ---------------------------------------------------------------------------
// wizardFixtures record
// ---------------------------------------------------------------------------

describe('wizardFixtures', () => {
  it('exposes all 14 fixture kinds', () => {
    const kinds = Object.keys(wizardFixtures).sort();
    expect(kinds).toEqual([...ALL_FIXTURE_KINDS].sort());
  });

  it('every fixture validates catalog/BGG entries against schemas', () => {
    for (const [kind, fixture] of Object.entries(wizardFixtures)) {
      for (const game of fixture.catalogResults) {
        const result = CatalogGameRefSchema.safeParse(game);
        if (!result.success) {
          throw new Error(
            `Fixture "${kind}" catalog entry "${game.title}" invalid: ${result.error.message}`
          );
        }
      }
      for (const bgg of fixture.bggResults) {
        const result = BggSearchResultSchema.safeParse(bgg);
        if (!result.success) {
          throw new Error(
            `Fixture "${kind}" BGG entry "${bgg.title}" invalid: ${result.error.message}`
          );
        }
      }
    }
  });

  it('step1-default exposes 5 catalog games (mockup parity)', () => {
    expect(wizardFixtures['step1-default'].catalogResults).toHaveLength(5);
  });

  it('step1-no-results exposes empty catalog + empty BGG', () => {
    const f = wizardFixtures['step1-no-results'];
    expect(f.catalogResults).toHaveLength(0);
    expect(f.bggResults).toHaveLength(0);
  });

  it('step1-bgg-loading exposes bggIsLoading=true and activeTab=bgg', () => {
    const f = wizardFixtures['step1-bgg-loading'];
    expect(f.bggIsLoading).toBe(true);
    expect(f.activeTab).toBe('bgg');
  });

  it('step2-ready exposes cameraPermission=granted with healthy values', () => {
    const f = wizardFixtures['step2-ready'];
    expect(f.cameraPermission).toBe('granted');
    expect(f.lightMeterValue).toBeGreaterThanOrEqual(0.3);
    expect(f.detectionScore).toBeGreaterThanOrEqual(0.5);
  });

  it('step2-low-light has lightMeterValue below threshold (<0.3)', () => {
    const f = wizardFixtures['step2-low-light'];
    expect(f.lightMeterValue).toBeLessThan(0.3);
  });

  it('step2-failed has detectionScore below threshold (<0.5)', () => {
    const f = wizardFixtures['step2-failed'];
    expect(f.detectionScore).toBeLessThan(0.5);
  });

  it('step2-denied has cameraPermission=denied', () => {
    expect(wizardFixtures['step2-denied'].cameraPermission).toBe('denied');
  });

  it('step3-progress exposes Processing batch status', () => {
    const f = wizardFixtures['step3-progress'];
    expect(f.batchStatus?.status).toBe('Processing');
    expect(f.batchStatus?.processedPages).toBeLessThan(f.batchStatus!.totalPages);
  });

  it('step3-partial exposes Completed status with low conf + failed pages', () => {
    const f = wizardFixtures['step3-partial'];
    expect(f.batchStatus?.status).toBe('Completed');
    expect(f.batchStatus?.failedPageNumbers?.length).toBeGreaterThan(0);
  });

  it('step3-complete exposes Completed with high avg confidence', () => {
    const f = wizardFixtures['step3-complete'];
    expect(f.batchStatus?.status).toBe('Completed');
    expect(f.batchStatus?.averageConfidence ?? 0).toBeGreaterThanOrEqual(0.8);
  });

  it('step3-offline has isOffline=true and active retry attempt', () => {
    const f = wizardFixtures['step3-offline'];
    expect(f.isOffline).toBe(true);
    expect(f.retryAttempt).toBeGreaterThan(0);
  });

  it('step3-cancel-modal has cancelModalOpen=true with active batch', () => {
    const f = wizardFixtures['step3-cancel-modal'];
    expect(f.cancelModalOpen).toBe(true);
    expect(f.batchId).toBeTruthy();
  });
});

// ---------------------------------------------------------------------------
// STATE_OVERRIDE_ENABLED gating
// ---------------------------------------------------------------------------

describe('STATE_OVERRIDE_ENABLED', () => {
  it('is true in test environments (NODE_ENV !== production)', () => {
    // Vitest sets NODE_ENV=test, so override is enabled.
    expect(STATE_OVERRIDE_ENABLED).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// parseStateOverride
// ---------------------------------------------------------------------------

describe('parseStateOverride', () => {
  it.each(ALL_FIXTURE_KINDS)('accepts kind %s', kind => {
    expect(parseStateOverride(kind)).toBe(kind);
  });

  it('returns null for missing param', () => {
    expect(parseStateOverride(null)).toBe(null);
    expect(parseStateOverride(undefined)).toBe(null);
  });

  it('returns null for empty string', () => {
    expect(parseStateOverride('')).toBe(null);
  });

  it('returns null for unknown kind', () => {
    expect(parseStateOverride('step99-foo')).toBe(null);
  });

  it('extracts kind from URLSearchParams', () => {
    const params = new URLSearchParams('fixture=step1-default&other=value');
    expect(parseStateOverride(params)).toBe('step1-default');
  });

  it('returns null when URLSearchParams missing fixture key', () => {
    const params = new URLSearchParams('foo=bar');
    expect(parseStateOverride(params)).toBe(null);
  });

  it('returns null when fixture key in URLSearchParams is unknown', () => {
    const params = new URLSearchParams('fixture=step99-foo');
    expect(parseStateOverride(params)).toBe(null);
  });

  it('rejects wizard-cancelled (terminal cell, not a fixture)', () => {
    expect(parseStateOverride('wizard-cancelled')).toBe(null);
  });

  it('schema-aligned: every key in record is parseable', () => {
    for (const kind of Object.keys(wizardFixtures)) {
      expect(wizardFixtureKindSchema.safeParse(kind).success).toBe(true);
    }
  });
});
