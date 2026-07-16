/**
 * Lifecycle guards for the Mechanic Analyses admin UI (ISSUE-524 / ADR-051).
 *
 * Extracted from `page.tsx` so the derived-state logic is unit-testable without
 * rendering the whole (heavy, `'use client'`) page component.
 */

import { MechanicAnalysisStatus } from '@/lib/api/schemas/mechanic-analyses.schemas';
import type { MechanicAnalysisStatusDto } from '@/lib/api/schemas/mechanic-analyses.schemas';

/**
 * True while the async extraction pipeline is still running (Draft with incomplete
 * section runs). Any terminal status (InReview/Published/Rejected/PartiallyExtracted)
 * returns false. Drives the `/status` poll interval + lifecycle button gating.
 */
export function isPipelineRunning(status: MechanicAnalysisStatusDto | null | undefined): boolean {
  if (!status) return false;
  if (status.status !== MechanicAnalysisStatus.Draft) return false;
  if (status.sectionRuns.length === 0) return true;
  // All 9 sections should complete (v1.1.0 added Setup/Components/Endgame).
  return status.sectionRuns.length < 9;
}

/**
 * Whether the "Submit for review" lifecycle action is available for the given analysis.
 */
export function canSubmitAnalysisForReview(
  status: MechanicAnalysisStatusDto | null | undefined
): boolean {
  return (
    !!status &&
    !status.isSuppressed &&
    // #2953 (#4): the domain allows PartiallyExtracted → InReview (MechanicAnalysis valid
    // transitions + SubmitForReview branch), so a partial-salvage analysis (ADR-051 Sprint 2)
    // must be promotable from the UI too — not only Draft/Rejected.
    (status.status === MechanicAnalysisStatus.Draft ||
      status.status === MechanicAnalysisStatus.Rejected ||
      status.status === MechanicAnalysisStatus.PartiallyExtracted) &&
    status.claimsCount > 0 &&
    !isPipelineRunning(status)
  );
}
