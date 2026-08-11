import { describe, expect, it } from 'vitest';

import { MechanicAnalysisStatus } from '@/lib/api/schemas/mechanic-analyses.schemas';
import type { MechanicAnalysisStatusDto } from '@/lib/api/schemas/mechanic-analyses.schemas';

import { canSubmitAnalysisForReview, isPipelineRunning } from '../lifecycle-guards';

function makeStatus(over: Partial<MechanicAnalysisStatusDto> = {}): MechanicAnalysisStatusDto {
  return {
    status: MechanicAnalysisStatus.Rejected,
    isSuppressed: false,
    claimsCount: 3,
    sectionRuns: [],
    ...over,
  } as unknown as MechanicAnalysisStatusDto;
}

describe('isPipelineRunning', () => {
  it('is false for a null status', () => {
    expect(isPipelineRunning(null)).toBe(false);
  });

  it('is true for a queued Draft (0 section runs)', () => {
    expect(
      isPipelineRunning(makeStatus({ status: MechanicAnalysisStatus.Draft, sectionRuns: [] }))
    ).toBe(true);
  });

  it('is false for a terminal status', () => {
    expect(
      isPipelineRunning(makeStatus({ status: MechanicAnalysisStatus.PartiallyExtracted }))
    ).toBe(false);
  });
});

describe('canSubmitAnalysisForReview', () => {
  it('allows a Rejected analysis', () => {
    expect(
      canSubmitAnalysisForReview(makeStatus({ status: MechanicAnalysisStatus.Rejected }))
    ).toBe(true);
  });

  // #2953 (#4): the domain allows PartiallyExtracted → InReview (MechanicAnalysis valid
  // transitions), so a partial-salvage analysis must be promotable from the UI too.
  it('allows a PartiallyExtracted analysis (ADR-051 Sprint 2 salvage state)', () => {
    expect(
      canSubmitAnalysisForReview(makeStatus({ status: MechanicAnalysisStatus.PartiallyExtracted }))
    ).toBe(true);
  });

  it('blocks an InReview analysis', () => {
    expect(
      canSubmitAnalysisForReview(makeStatus({ status: MechanicAnalysisStatus.InReview }))
    ).toBe(false);
  });

  it('blocks a suppressed analysis', () => {
    expect(
      canSubmitAnalysisForReview(
        makeStatus({ status: MechanicAnalysisStatus.PartiallyExtracted, isSuppressed: true })
      )
    ).toBe(false);
  });

  it('blocks when there are no claims', () => {
    expect(
      canSubmitAnalysisForReview(
        makeStatus({ status: MechanicAnalysisStatus.PartiallyExtracted, claimsCount: 0 })
      )
    ).toBe(false);
  });

  it('blocks while the pipeline is still running (Draft, queued)', () => {
    expect(
      canSubmitAnalysisForReview(
        makeStatus({ status: MechanicAnalysisStatus.Draft, sectionRuns: [] })
      )
    ).toBe(false);
  });
});
