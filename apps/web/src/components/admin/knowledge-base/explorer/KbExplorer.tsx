'use client';

import { useMemo, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

import { KbDocDetailPanel } from './KbDocDetailPanel';
import { KbTree } from './KbTree';

const ADMIN_GAME_KB_STATUSES_QUERY_KEY = ['admin-game-kb-statuses'] as const;

/**
 * KbExplorer — master-detail dell'admin KB. Carica i game-statuses via
 * adminClient, gestisce stato locale (espansione + filtro) e sincronizza
 * il documento selezionato con la query string `?doc=<id>` (deep-link
 * condivisibile/bookmarkabile).
 *
 * Pattern: ricalca il setup di /admin/knowledge-base/games/page.tsx
 * (createAdminClient({ httpClient: new HttpClient() }) + useQuery).
 */
export function KbExplorer() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const selectedDocId = searchParams.get('doc');

  const [expandedGameIds, setExpandedGameIds] = useState<Set<string>>(() => new Set());
  const [filter, setFilter] = useState('');

  const adminClient = useMemo(() => createAdminClient({ httpClient: new HttpClient() }), []);

  const {
    data: games,
    isLoading,
    error,
  } = useQuery({
    queryKey: ADMIN_GAME_KB_STATUSES_QUERY_KEY,
    // getGameKbStatuses returns { items: GameKbStatusItem[] }; unwrap to the array.
    queryFn: () => adminClient.getGameKbStatuses().then(r => r.items),
  });

  const setSelectedDocId = (docId: string | null) => {
    const params = new URLSearchParams(searchParams.toString());
    if (docId === null) params.delete('doc');
    else params.set('doc', docId);
    const qs = params.toString();
    router.replace(qs ? `${pathname}?${qs}` : pathname);
  };

  const handleToggleGame = (gameId: string) => {
    setExpandedGameIds(prev => {
      const next = new Set(prev);
      if (next.has(gameId)) next.delete(gameId);
      else next.add(gameId);
      return next;
    });
  };

  if (isLoading) {
    return (
      <div
        data-testid="kb-explorer-loading"
        className="grid grid-cols-[300px_1fr] gap-4 min-h-[400px] animate-pulse"
      >
        <div className="rounded-lg bg-muted/40 h-[400px]" />
        <div className="rounded-lg bg-muted/40 h-[400px]" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-lg border border-rose-500/30 bg-rose-500/5 p-6">
        <h3 className="font-quicksand font-bold text-rose-700 dark:text-rose-300 mb-1">
          Errore di caricamento
        </h3>
        <p className="text-sm text-muted-foreground">
          Impossibile recuperare lo stato KB dei giochi. {error.message}
        </p>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-[300px_1fr] gap-4 items-start">
      <KbTree
        games={games ?? []}
        expandedGameIds={expandedGameIds}
        selectedDocId={selectedDocId}
        filter={filter}
        onToggleGame={handleToggleGame}
        onSelectDoc={id => setSelectedDocId(id)}
        onFilterChange={setFilter}
      />
      <KbDocDetailPanel docId={selectedDocId} />
    </div>
  );
}
