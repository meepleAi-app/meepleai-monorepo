/**
 * @vitest-environment jsdom
 *
 * Tests for `isLegacyMechanicExtractorEnabled` (#537 ME-M4.2).
 *
 * The helper is a thin wrapper over a single `NEXT_PUBLIC_*` env read gating the
 * deprecated Variant C editor's admin nav entry. Strict equality on the literal
 * `'true'` — no truthy coercion — so the entry stays hidden unless explicitly opted in.
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { isLegacyMechanicExtractorEnabled } from '../legacy-mechanic-extractor';

const ENV_KEY = 'NEXT_PUBLIC_SHOW_LEGACY_MECHANIC_EXTRACTOR';

describe('isLegacyMechanicExtractorEnabled', () => {
  let original: string | undefined;

  beforeEach(() => {
    original = process.env[ENV_KEY];
  });

  afterEach(() => {
    if (original === undefined) {
      delete process.env[ENV_KEY];
    } else {
      process.env[ENV_KEY] = original;
    }
  });

  it("returns true when the flag is the literal string 'true'", () => {
    process.env[ENV_KEY] = 'true';
    expect(isLegacyMechanicExtractorEnabled()).toBe(true);
  });

  it("returns false when the flag is the literal string 'false'", () => {
    process.env[ENV_KEY] = 'false';
    expect(isLegacyMechanicExtractorEnabled()).toBe(false);
  });

  it("returns false for truthy-looking but non-'true' values (no coercion)", () => {
    process.env[ENV_KEY] = '1';
    expect(isLegacyMechanicExtractorEnabled()).toBe(false);

    process.env[ENV_KEY] = 'yes';
    expect(isLegacyMechanicExtractorEnabled()).toBe(false);

    process.env[ENV_KEY] = 'TRUE';
    expect(isLegacyMechanicExtractorEnabled()).toBe(false);
  });

  it('returns false when the flag is unset (deprecated entry hidden by default)', () => {
    delete process.env[ENV_KEY];
    expect(isLegacyMechanicExtractorEnabled()).toBe(false);
  });

  it('returns false for the empty string', () => {
    process.env[ENV_KEY] = '';
    expect(isLegacyMechanicExtractorEnabled()).toBe(false);
  });
});
