/**
 * Fixtures for ResumeBooksList story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy:
 *   ResumeBooksList is a pure props-driven component (`progress: BookProgress[]`,
 *   `onResume`). It has exactly 2 real branches:
 *
 *   - Empty state (progress.length === 0):    renders "Nessun libro in corso."
 *   - Non-empty list (progress.length >= 1):  renders a <ul> with one row per entry.
 *
 *   No MSW required — no API calls in the component.
 *
 *   Five mockup states map to story frames as follows:
 *
 *   Frame01_FirstTime     → real component, progress: []  (empty-state branch)
 *   Frame02_SingleResume  → real component, progress: [1 item, non-stale]
 *   Frame03_MultiCampaign → real component, progress: [2 items]
 *   Frame04_StaleWarning  → Template K mock (ResumePickerStaleMock) —
 *                           no `isStale` prop on real component (forward-refactor)
 *   Frame05_WithTutorial  → Template K mock (ResumePickerTutorialMock) —
 *                           no `tutorialAvailable` prop (forward-refactor)
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-resume-picker.html
 * Refs: umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import type { BookProgress } from '@/components/features/gamebook/ResumeBooksList';

// ---------------------------------------------------------------------------
// Frame02 — single non-stale campaign (Campagna con i ragazzi, §289)
// ---------------------------------------------------------------------------

export const FIXTURE_SINGLE_RESUME: BookProgress[] = [
  {
    bookId: '00000000-0000-0000-0000-000000000030',
    bookName: 'Campagna con i ragazzi',
    lastLocation: '§289',
    lastVisitedAt: '2026-06-15T20:42:00+00:00',
  },
];

// ---------------------------------------------------------------------------
// Frame03 — two campaigns ordered by last access (Sara first, then ragazzi)
// ---------------------------------------------------------------------------

export const FIXTURE_MULTI_CAMPAIGN: BookProgress[] = [
  {
    bookId: '00000000-0000-0000-0000-000000000031',
    bookName: 'Sera con Sara',
    lastLocation: '§47',
    lastVisitedAt: '2026-06-21T22:00:00+00:00',
  },
  {
    bookId: '00000000-0000-0000-0000-000000000030',
    bookName: 'Campagna con i ragazzi',
    lastLocation: '§289',
    lastVisitedAt: '2026-06-15T20:42:00+00:00',
  },
];

// ---------------------------------------------------------------------------
// Frame04 — stale campaign (same content, used only by the Template K mock)
// ---------------------------------------------------------------------------

export const FIXTURE_STALE_CAMPAIGN: BookProgress & {
  startedAt: string;
  lastSessionAt: string;
  daysSinceLastSession: number;
  party: string[];
  glossaryTerms: number;
} = {
  bookId: '00000000-0000-0000-0000-000000000030',
  bookName: 'Campagna con i ragazzi',
  lastLocation: '§289',
  lastVisitedAt: '2026-03-09T20:00:00+00:00',
  startedAt: '2026-03-05T18:00:00+00:00',
  lastSessionAt: '2026-03-09T20:00:00+00:00',
  daysSinceLastSession: 60,
  party: ['Marco', 'Giulia', 'Luca', 'Aaron'],
  glossaryTerms: 12,
};

// ---------------------------------------------------------------------------
// Frame05 — tutorial-available data (used only by the Template K mock)
// ---------------------------------------------------------------------------

export const FIXTURE_WITH_TUTORIAL: BookProgress & {
  tutorialSteps: number;
  tutorialDocumentCategory: string;
} = {
  bookId: '00000000-0000-0000-0000-000000000030',
  bookName: 'Campagna con i ragazzi',
  lastLocation: '§289',
  lastVisitedAt: '2026-06-15T20:42:00+00:00',
  tutorialSteps: 8,
  tutorialDocumentCategory: 'QuickStart',
};
