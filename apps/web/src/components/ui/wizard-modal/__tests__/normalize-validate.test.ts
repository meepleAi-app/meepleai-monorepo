/**
 * normalizeValidate — unit tests.
 * MAJ-1: validate must normalize boolean | ValidationResult | Promise<...>.
 */

import { describe, expect, it } from 'vitest';

import { normalizeValidate } from '../normalize-validate';

describe('normalizeValidate', () => {
  it('normalizes sync boolean true → { valid: true, errors: [] }', async () => {
    const result = await normalizeValidate(true);
    expect(result).toEqual({ valid: true, errors: [] });
  });

  it('normalizes sync boolean false → { valid: false, errors: [] }', async () => {
    const result = await normalizeValidate(false);
    expect(result).toEqual({ valid: false, errors: [] });
  });

  it('normalizes sync ValidationResult (valid only) → preserves with empty errors', async () => {
    const result = await normalizeValidate({ valid: true });
    expect(result).toEqual({ valid: true, errors: [] });
  });

  it('normalizes sync ValidationResult (with errors) → preserves errors', async () => {
    const input = { valid: false, errors: [{ message: 'Field X required' }] };
    const result = await normalizeValidate(input);
    expect(result).toEqual(input);
  });

  it('normalizes async Promise<boolean> → resolves to ValidationResult', async () => {
    const result = await normalizeValidate(Promise.resolve(true));
    expect(result).toEqual({ valid: true, errors: [] });
  });

  it('normalizes async Promise<ValidationResult> → resolves preserving errors', async () => {
    const input = {
      valid: false,
      errors: [
        { field: 'email', message: 'Invalid email' },
        { field: 'name', message: 'Required' },
      ],
    };
    const result = await normalizeValidate(Promise.resolve(input));
    expect(result).toEqual(input);
  });

  it('normalizes async Promise<false> → resolves to invalid with empty errors', async () => {
    const result = await normalizeValidate(Promise.resolve(false));
    expect(result).toEqual({ valid: false, errors: [] });
  });
});
