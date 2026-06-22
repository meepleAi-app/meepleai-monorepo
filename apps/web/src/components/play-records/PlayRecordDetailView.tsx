/**
 * PlayRecordDetailView — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * Thin wrapper: resolves hooks, renders loading/error guards, then delegates
 * the full composition to PlayRecordDetailBody.
 *
 * AC-2.12: 404 → redirect to /play-records
 *
 * @see PlayRecordDetailBody — pure prop-driven body (extracted #2437-2)
 * @see plan `docs/superpowers/plans/2026-05-29-play-records-reskin.md` Task 2
 * @see mockup `admin-mockups/design_files/sp4-play-records-detail.jsx`
 */
/* eslint-disable local/no-hardcoded-color-utility -- text-white on ErrorState link uses colored bg via style prop, following .e-bg mockup pattern */
'use client';

import type { ReactElement } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { usePlayRecord } from '@/lib/domain-hooks/usePlayRecords';

import { PlayRecordDetailBody } from './PlayRecordDetailBody';

// ── Loading skeleton ──────────────────────────────────────────────────────────

function LoadingSkeleton(): ReactElement {
  return (
    <div data-testid="play-record-detail-loading" className="flex flex-col gap-4 p-4 sm:p-8">
      <div className="h-52 animate-pulse rounded-2xl bg-muted sm:h-64" />
      <div className="h-12 animate-pulse rounded-xl bg-muted" />
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {[0, 1, 2, 3].map(i => (
          <div key={i} className="h-20 animate-pulse rounded-lg bg-muted" />
        ))}
      </div>
      <div className="h-48 animate-pulse rounded-xl bg-muted" />
    </div>
  );
}

// ── Error / Not found ─────────────────────────────────────────────────────────

function ErrorState({ message }: { message: string }): ReactElement {
  return (
    <div
      data-testid="play-record-detail-error"
      className="mx-4 mt-4 rounded-xl bg-danger/6 border border-danger/25 px-6 py-8 text-center sm:mx-8"
      style={{
        background: 'hsl(var(--c-danger) / .06)',
        borderColor: 'hsl(var(--c-danger) / .25)',
      }}
    >
      <div className="mb-2 text-3xl" aria-hidden="true">
        ⚠
      </div>
      <h3 className="mb-1 font-display text-base font-extrabold text-foreground">
        Partita non trovata
      </h3>
      <p className="mb-4 text-sm text-muted-foreground">{message}</p>
      <Link
        href="/play-records"
        className="inline-flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-bold text-white"
        style={{ background: 'hsl(var(--c-danger))' }}
      >
        ← Torna alle partite
      </Link>
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export interface PlayRecordDetailViewProps {
  readonly recordId: string;
}

export function PlayRecordDetailView({ recordId }: PlayRecordDetailViewProps): ReactElement {
  const router = useRouter();
  const { data: currentUser } = useCurrentUser();
  const { data: record, isLoading, error } = usePlayRecord(recordId);

  if (isLoading) {
    return <LoadingSkeleton />;
  }

  if (error || !record) {
    // AC-2.12: 404 redirect
    if (typeof window !== 'undefined' && error) {
      // Only redirect on actual 404, not on loading
      const is404 = error instanceof Error && error.message?.includes('404');
      if (is404) {
        router.push('/play-records');
      }
    }
    return (
      <ErrorState
        message={
          error instanceof Error
            ? error.message
            : 'Impossibile caricare il dettaglio della partita.'
        }
      />
    );
  }

  return <PlayRecordDetailBody record={record} currentUserId={currentUser?.id ?? null} />;
}
