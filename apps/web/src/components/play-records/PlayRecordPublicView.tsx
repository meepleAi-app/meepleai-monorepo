/**
 * PlayRecordPublicView — public (unauthenticated) view for a shared play record.
 *
 * Fetches the record by share token via `useSharedPlayRecord` and delegates
 * rendering to `PlayRecordDetailBody` with `currentUserId={null}` (spectator
 * mode — no creator actions exposed).
 *
 * Issue #2437-2: Public shared play-record route.
 */
'use client';

import type { ReactElement } from 'react';

import { AlertTriangle } from 'lucide-react';
import Link from 'next/link';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { useSharedPlayRecord } from '@/lib/domain-hooks/usePlayRecords';

import { PlayRecordDetailBody } from './PlayRecordDetailBody';

// ── Loading skeleton ──────────────────────────────────────────────────────────

function LoadingSkeleton(): ReactElement {
  return (
    <div data-testid="play-record-public-loading" className="flex flex-col gap-4 p-4 sm:p-8">
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

// ── Not found / error state ───────────────────────────────────────────────────

function NotFoundState(): ReactElement {
  return (
    <main className="min-h-screen bg-background">
      <div className="container mx-auto px-4 py-16">
        <Card className="max-w-md mx-auto">
          <CardContent className="flex flex-col items-center py-12 text-center">
            <AlertTriangle className="h-16 w-16 text-muted-foreground mb-4" />
            <h2 className="text-xl font-semibold mb-2">Partita Non Trovata</h2>
            <p className="text-muted-foreground mb-6">
              Questo link di condivisione potrebbe essere scaduto, revocato o non valido.
            </p>
            <Button asChild>
              <Link href="/">Torna alla Home</Link>
            </Button>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}

// ── Public view ───────────────────────────────────────────────────────────────

export interface PlayRecordPublicViewProps {
  token: string;
}

/**
 * Renders a shared play record identified by `token`.
 * `currentUserId={null}` ensures the body is fully read-only:
 * no "Condividi" / "Aggiungi foto" creator buttons are shown.
 */
export function PlayRecordPublicView({ token }: PlayRecordPublicViewProps): ReactElement {
  const { data: record, isLoading, error } = useSharedPlayRecord(token);

  if (isLoading) return <LoadingSkeleton />;
  if (error || !record) return <NotFoundState />;

  return (
    <main className="min-h-screen bg-background">
      <PlayRecordDetailBody record={record} currentUserId={null} />
    </main>
  );
}
