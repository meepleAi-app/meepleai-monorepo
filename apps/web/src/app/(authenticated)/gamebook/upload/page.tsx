/**
 * /gamebook/upload — SP6 Phase C.1.C thin Suspense shell (Issue #789).
 *
 * Big-Bang Foundation per contract §19: page replaces the legacy
 * `GamebookUploadClient` with the new `GamebookUploadView` orchestrator
 * that renders the 14-cell FSM via static visual fixture (no real camera /
 * upload yet — that's Interactions sub-PR scope).
 *
 * Pattern: Wave D.3 SessionSummaryView — page server component does NO
 * data fetching; orchestrator is a `'use client'` boundary that reads
 * URL state + renders FSM cells.
 *
 * Note: legacy `GamebookUploadClient.tsx` and `PhotoUploader.tsx` are kept
 * in repo (their unit tests still pass) but are no longer wired into this
 * route. Phase C.1.D (Interactions) replaces wiring with real camera +
 * upload flow.
 */

import { Suspense, type JSX } from 'react';

import { GamebookUploadView } from './_components/GamebookUploadView';

export const metadata = {
  title: 'Carica manuale | MeepleAI',
  description: 'Fotografa il tuo manuale di gioco passo dopo passo',
};

export default function GamebookUploadPage(): JSX.Element {
  return (
    <Suspense fallback={null}>
      <GamebookUploadView />
    </Suspense>
  );
}
