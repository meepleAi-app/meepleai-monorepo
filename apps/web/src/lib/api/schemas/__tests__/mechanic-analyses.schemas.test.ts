import { describe, expect, it } from 'vitest';
import {
  MechanicClaimDtoSchema,
  BulkRejectMechanicClaimsResponseDtoSchema,
  MechanicClaimValidationDtoSchema,
} from '../mechanic-analyses.schemas';

describe('mechanic-analyses schemas #526', () => {
  it('parses validations[] + reviewNote on a claim', () => {
    const parsed = MechanicClaimDtoSchema.parse({
      id: '11111111-1111-4111-8111-111111111111',
      analysisId: '22222222-2222-4222-8222-222222222222',
      section: 1,
      text: 't',
      displayOrder: 0,
      status: 0,
      reviewedBy: null,
      reviewedAt: null,
      rejectionNote: null,
      reviewNote: null,
      citations: [],
      validations: [{ rule: 'T1', outcome: 'pass', message: null }],
    });
    expect(parsed.validations[0].rule).toBe('T1');
  });

  it('parses bulk-reject response', () => {
    const r = BulkRejectMechanicClaimsResponseDtoSchema.parse({
      rejectedCount: 2,
      skippedAlreadyRejectedCount: 0,
      claims: [],
    });
    expect(r.rejectedCount).toBe(2);
  });
});

describe('MechanicClaimValidationDtoSchema #2782', () => {
  it('preserves the T3b score field (guards against Zod silent-drop)', () => {
    const parsed = MechanicClaimValidationDtoSchema.parse({
      rule: 'T3b',
      outcome: 'pass',
      message: null,
      score: 0.87,
    });
    expect(parsed.score).toBe(0.87);
  });

  it('allows a null/absent score for non-T3b rules', () => {
    const parsed = MechanicClaimValidationDtoSchema.parse({
      rule: 'T1',
      outcome: 'fail',
      message: 'too long',
    });
    expect(parsed.score ?? null).toBeNull();
  });
});
