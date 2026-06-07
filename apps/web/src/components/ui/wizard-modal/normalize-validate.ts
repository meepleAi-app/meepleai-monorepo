/**
 * normalizeValidate — Unifies sync/async validate return shapes.
 *
 * MAJ-1: validate callback may return:
 *   - boolean (true=valid, false=invalid with empty errors)
 *   - ValidationResult ({ valid, errors? })
 *   - Promise<boolean | ValidationResult>
 *
 * Helper returns Promise<ValidationResult> with errors always defined as array.
 */

import type { ValidateResult, ValidationResult } from './wizard-modal-types';

export async function normalizeValidate(
  result: ValidateResult | Promise<ValidateResult>
): Promise<ValidationResult> {
  const awaited = await Promise.resolve(result);
  if (typeof awaited === 'boolean') {
    return { valid: awaited, errors: [] };
  }
  return {
    valid: awaited.valid,
    errors: awaited.errors ?? [],
  };
}
