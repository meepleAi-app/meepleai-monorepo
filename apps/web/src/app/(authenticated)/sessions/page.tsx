/**
 * Sessions Hub Page
 * Issue #5045 — Sessions + MiniNav + ActionBar
 *
 * Handles tab routing via ?tab= search param:
 *   (default)     → Sessioni attive
 *   ?tab=history  → Storico sessioni
 *
 * Content migration from legacy routes is planned for issue #5053.
 */

'use client';

import { Suspense } from 'react';

import { useSearchParams } from 'next/navigation';

import { Clock, History } from 'lucide-react';

function SessionsContent() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab') ?? 'active';

  return (
    <div className="space-y-6 container mx-auto px-4 py-8">
      <div className="flex items-center gap-3">
        {tab === 'history' ? (
          <History className="h-6 w-6 text-primary" />
        ) : (
          <Clock className="h-6 w-6 text-primary" />
        )}
        <div>
          <h1 className="text-2xl font-bold font-quicksand text-foreground">
            {tab === 'history' ? 'Storico Sessioni' : 'Sessioni Attive'}
          </h1>
          <p className="text-sm text-muted-foreground">
            {tab === 'history'
              ? 'Revisiona le tue sessioni di gioco passate'
              : 'Gestisci le sessioni di gioco in corso'}
          </p>
        </div>
      </div>

      <div className="rounded-xl border border-border bg-card p-6 text-muted-foreground text-sm">
        <p>
          Tab: <strong>{tab}</strong>
        </p>
        <p className="mt-2 text-xs">Contenuto in migrazione — issue #5053</p>
      </div>
    </div>
  );
}

export default function SessionsPage() {
  return (
    <Suspense>
      <SessionsContent />
    </Suspense>
  );
}
