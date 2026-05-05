/**
 * Mechanic Extractor — AI Comprehension Validation feature flag (ADR-051).
 *
 * Sprint 2 ships behind `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED`. Centralised
 * here so we have one source of truth for the gate (callers used to spell out
 * `process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true'` inline at
 * every page top — easy to typo, hard to test).
 *
 * Usage:
 *   if (!isMechanicValidationEnabled()) notFound();
 *   <FeatureFlagGate>{...}</FeatureFlagGate>
 */
export function isMechanicValidationEnabled(): boolean {
  return process.env.NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED === 'true';
}
