/**
 * @vitest-environment jsdom
 *
 * Tests for `isMechanicValidationEnabled` (ADR-051 Sprint 2 / Task 25).
 *
 * The helper is a thin wrapper over a single `NEXT_PUBLIC_*` env read. We
 * exercise the three states the rest of the app cares about:
 *  - `'true'` → enabled
 *  - any other defined value (`'false'`, `'1'`, `'yes'`) → disabled (strict
 *    equality on the literal `'true'`, no truthy coercion)
 *  - undefined (flag never set) → disabled
 */
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { isMechanicValidationEnabled } from '../mechanic-validation';

const ENV_KEY = 'NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED';

describe('isMechanicValidationEnabled', () => {
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
    expect(isMechanicValidationEnabled()).toBe(true);
  });

  it("returns false when the flag is the literal string 'false'", () => {
    process.env[ENV_KEY] = 'false';
    expect(isMechanicValidationEnabled()).toBe(false);
  });

  it("returns false for truthy-looking but non-'true' values (no coercion)", () => {
    process.env[ENV_KEY] = '1';
    expect(isMechanicValidationEnabled()).toBe(false);

    process.env[ENV_KEY] = 'yes';
    expect(isMechanicValidationEnabled()).toBe(false);

    process.env[ENV_KEY] = 'TRUE';
    expect(isMechanicValidationEnabled()).toBe(false);
  });

  it('returns false when the flag is unset', () => {
    delete process.env[ENV_KEY];
    expect(isMechanicValidationEnabled()).toBe(false);
  });

  it('returns false for the empty string', () => {
    process.env[ENV_KEY] = '';
    expect(isMechanicValidationEnabled()).toBe(false);
  });
});
