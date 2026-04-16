'use client';

/**
 * Admin KB Games Overview Page
 * Shows all games with KB status badges, document counts, and snapshot info.
 * Admin can see at a glance which games have KB data and avoid re-processing.
 */

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import {
  DatabaseIcon,
  RefreshCwIcon,
  CheckCircleIcon,
  AlertCircleIcon,
  CircleIcon,
  FileTextIcon,
  ClockIcon,
  SearchIcon,
} from 'lucide-react';
import Link from 'next/link';

import { GamesWithoutKbSection } from '@/components/admin/knowledge-base/games-without-kb-section';
import { UploadForGameDrawer } from '@/components/admin/knowledge-base/upload-for-game-drawer';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { GameWithoutKbDto } from '@/lib/api/kb-games-without-kb-api';
import type { GameKbStatusItem } from '@/lib/api/schemas/admin-knowledge-base.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function KbStatusBadge({ status }: { status: GameKbStatusItem['kbStatus'] }) {
  if (status === 'complete') {
    return (
      <Badge className="bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800">
        <CheckCircleIcon className="h-3 w-3 mr-1" />
        Completa
      </Badge>
    );
  }
  if (status === 'partial') {
    return (
      <Badge className="bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400 border-amber-200 dark:border-amber-800">
        <AlertCircleIcon className="h-3 w-3 mr-1" />
        Parziale
      </Badge>
    );
  }
  return (
    <Badge className="bg-slate-100 text-slate-500 dark:bg-zinc-800 dark:text-zinc-400 border-slate-200 dark:border-zinc-700">
      <CircleIcon className="h-3 w-3 mr-1" />
      Nessuna KB
    </Badge>
  );
}

function GameKbRow({ item }: { item: GameKbStatusItem }) {
  const indexedDate = item.latestIndexedAt
    ? new Date(item.latestIndexedAt).toLocaleDateString('it-IT', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      })
    : null;

  return (
    <div className="flex items-center gap-4 py-3 px-4 rounded-xl hover:bg-slate-50 dark:hover:bg-zinc-800/50 transition-colors">
      {/* Status badge */}
      <div className="w-28 flex-shrink-0">
        <KbStatusBadge status={item.kbStatus} />
      </div>

      {/* Game name */}
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium text-slate-900 dark:text-zinc-100 truncate">
          {item.gameName}
        </p>
        {item.kbStatus !== 'none' && (
          <p className="text-xs text-slate-500 dark:text-zinc-400 mt-0.5">
            {item.documentCount} {item.documentCount === 1 ? 'documento' : 'documenti'} •{' '}
            {item.totalChunks.toLocaleString('it-IT')} chunks
          </p>
        )}
      </div>

      {/* Last indexed */}
      <div className="hidden md:flex items-center gap-1.5 text-xs text-slate-500 dark:text-zinc-400 w-32 flex-shrink-0">
        {indexedDate ? (
          <>
            <ClockIcon className="h-3 w-3" />
            {indexedDate}
          </>
        ) : (
          <span className="text-slate-400 dark:text-zinc-500">—</span>
        )}
      </div>

      {/* Backup status */}
      <div className="hidden lg:flex w-24 flex-shrink-0">
        {item.hasAutoBackup ? (
          <span className="text-xs text-emerald-600 dark:text-emerald-400 font-medium">
            ✓ Backup
          </span>
        ) : (
          <span className="text-xs text-slate-400 dark:text-zinc-500">—</span>
        )}
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2 flex-shrink-0">
        <Link
          href={`/admin/knowledge-base/games/${item.gameId}`}
          className="text-xs text-blue-600 dark:text-blue-400 hover:underline"
        >
          Documenti
        </Link>
        {item.kbStatus !== 'none' && (
          <Link
            href={`/admin/knowledge-base/snapshots?gameId=${item.gameId}`}
            className="text-xs text-purple-600 dark:text-purple-400 hover:underline"
          >
            Snapshot
          </Link>
        )}
        {item.kbStatus === 'none' && (
          <Link
            href={`/admin/knowledge-base/upload?gameId=${item.gameId}`}
            className="text-xs text-emerald-600 dark:text-emerald-400 hover:underline"
          >
            Carica PDF
          </Link>
        )}
      </div>
    </div>
  );
}

export default function KbGamesPage() {
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState<'all' | 'complete' | 'partial' | 'none'>('all');
  const [uploadTarget, setUploadTarget] = useState<GameWithoutKbDto | null>(null);

  const { data, isLoading, error, refetch, isRefetching } = useQuery({
    queryKey: ['admin', 'kb-game-statuses'],
    queryFn: () => adminClient.getGameKbStatuses(),
    staleTime: 60_000,
  });

  const items = data?.items ?? [];

  const filtered = items.filter(item => {
    const matchesSearch = search
      ? item.gameName.toLowerCase().includes(search.toLowerCase())
      : true;
    const matchesFilter = filter === 'all' || item.kbStatus === filter;
    return matchesSearch && matchesFilter;
  });

  const counts = {
    all: items.length,
    complete: items.filter(i => i.kbStatus === 'complete').length,
    partial: items.filter(i => i.kbStatus === 'partial').length,
    none: items.filter(i => i.kbStatus === 'none').length,
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-zinc-100">
            Knowledge Base per Gioco
          </h1>
          <p className="text-sm text-slate-500 dark:text-zinc-400 mt-1">
            Stato KB e snapshot per ogni gioco. I giochi con backup non richiedono ri-elaborazione.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => refetch()}
          disabled={isRefetching}
          className="gap-2"
        >
          <RefreshCwIcon className={`h-4 w-4 ${isRefetching ? 'animate-spin' : ''}`} />
          Aggiorna
        </Button>
      </div>

      {/* Stats summary */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        {(
          [
            { key: 'all', label: 'Totale giochi', color: 'text-slate-700 dark:text-zinc-300' },
            {
              key: 'complete',
              label: 'KB completa',
              color: 'text-emerald-600 dark:text-emerald-400',
            },
            {
              key: 'partial',
              label: 'KB parziale',
              color: 'text-amber-600 dark:text-amber-400',
            },
            { key: 'none', label: 'Senza KB', color: 'text-slate-400 dark:text-zinc-500' },
          ] as const
        ).map(({ key, label, color }) => (
          <button
            key={key}
            onClick={() => setFilter(key)}
            className={`rounded-xl border p-3 text-left transition-all ${
              filter === key
                ? 'border-blue-400 bg-blue-50 dark:bg-blue-900/20 dark:border-blue-700'
                : 'border-slate-200 dark:border-zinc-700 bg-white dark:bg-zinc-800/60 hover:border-slate-300 dark:hover:border-zinc-600'
            }`}
          >
            <p className={`text-2xl font-bold ${color}`}>{counts[key]}</p>
            <p className="text-xs text-slate-500 dark:text-zinc-400 mt-0.5">{label}</p>
          </button>
        ))}
      </div>

      {/* Filter & search */}
      <div className="relative">
        <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
        <Input
          placeholder="Cerca gioco..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="pl-9"
        />
      </div>

      {/* Games list */}
      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
        <CardHeader className="pb-2">
          <CardTitle className="text-base font-semibold flex items-center gap-2">
            <DatabaseIcon className="h-4 w-4 text-blue-500" />
            {filter === 'all' ? 'Tutti i giochi' : `Giochi — ${filter}`}
            <span className="ml-auto text-sm font-normal text-slate-400 dark:text-zinc-500">
              {filtered.length} risultati
            </span>
          </CardTitle>
        </CardHeader>
        <CardContent className="px-2">
          {filter === 'none' ? (
            <div className="p-2">
              <GamesWithoutKbSection onUploadClick={setUploadTarget} />
            </div>
          ) : isLoading ? (
            <div className="space-y-1 p-2">
              {Array.from({ length: 6 }).map((_, i) => (
                <div
                  key={i}
                  className="h-14 rounded-xl bg-slate-100 dark:bg-zinc-700/50 animate-pulse"
                />
              ))}
            </div>
          ) : error ? (
            <div className="flex items-center gap-2 p-4 text-red-500 dark:text-red-400">
              <AlertCircleIcon className="h-4 w-4" />
              <span className="text-sm">Errore nel caricamento dei dati</span>
            </div>
          ) : filtered.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-12 text-slate-400 dark:text-zinc-500">
              <FileTextIcon className="h-8 w-8" />
              <p className="text-sm">Nessun gioco trovato</p>
            </div>
          ) : (
            <div className="divide-y divide-slate-100 dark:divide-zinc-700/50">
              {filtered.map(item => (
                <GameKbRow key={item.gameId} item={item} />
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      <UploadForGameDrawer
        game={uploadTarget}
        open={uploadTarget !== null}
        onClose={() => setUploadTarget(null)}
      />
    </div>
  );
}
