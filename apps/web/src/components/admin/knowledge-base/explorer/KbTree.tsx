/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber accent + zinc dark palette (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import { useMemo } from 'react';

import { useKbGameDocuments } from '@/hooks/queries/useGameDocuments';
import type { GameKbStatusItem } from '@/lib/api/schemas/admin-knowledge-base.schemas';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

import type { SelectedDocMeta } from './explorer-types';

export interface KbTreeProps {
  readonly games: ReadonlyArray<GameKbStatusItem>;
  readonly expandedGameIds: ReadonlySet<string>;
  readonly selectedDocId: string | null;
  readonly filter: string;
  readonly onToggleGame: (gameId: string) => void;
  readonly onSelectDoc: (doc: SelectedDocMeta) => void;
  readonly onFilterChange: (filter: string) => void;
}

function statusColor(status: GameDocument['status']): string {
  switch (status) {
    case 'indexed':
      return 'text-emerald-600 dark:text-emerald-400';
    case 'processing':
      return 'text-amber-600 dark:text-amber-400';
    case 'failed':
      return 'text-rose-600 dark:text-rose-400';
  }
}

function statusLabel(status: GameDocument['status'], pageCount: number): string {
  if (status === 'processing') return 'indicizzando…';
  if (status === 'failed') return '⚠ failed';
  return `${pageCount}p`;
}

/**
 * KbTree — esploratore gioco→documenti per /admin/knowledge-base.
 * Controlled component: tutto lo stato (espansione, selezione, filtro) è
 * gestito dal parent (KbExplorer). Lazy-load: i doc di un game sono
 * fetchati solo all'espansione (sub-componente interno KbTreeGameDocs).
 */
export function KbTree({
  games,
  expandedGameIds,
  selectedDocId,
  filter,
  onToggleGame,
  onSelectDoc,
  onFilterChange,
}: KbTreeProps) {
  const lcFilter = filter.trim().toLowerCase();

  const visibleGames = useMemo(() => {
    if (!lcFilter) return games;
    // Filter games by name only. Doc-title filtering happens independently
    // inside expanded games (KbTreeGameDocs) — we cannot check doc-match at
    // game level because docs are lazy-loaded per-game on expansion.
    return games.filter(g => g.gameName.toLowerCase().includes(lcFilter));
  }, [games, lcFilter]);

  return (
    <div className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 p-2 overflow-y-auto max-h-[calc(100vh-180px)]">
      <div className="px-2 py-1.5">
        <input
          type="search"
          value={filter}
          onChange={e => onFilterChange(e.target.value)}
          placeholder="Filtra tree…"
          className="w-full bg-muted/60 dark:bg-zinc-800/60 border border-border/40 rounded-md px-2.5 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-amber-500/60"
        />
      </div>
      <div className="px-2 py-0.5 text-[10px] font-mono text-muted-foreground uppercase tracking-wider">
        {games.length} giochi
      </div>

      <ul role="tree" aria-label="Knowledge Base alberatura">
        {visibleGames.map(game => {
          const expanded = expandedGameIds.has(game.gameId);
          return (
            <li key={game.gameId}>
              <button
                type="button"
                role="treeitem"
                aria-expanded={expanded}
                aria-label={game.gameName}
                onClick={() => onToggleGame(game.gameId)}
                className="w-full flex items-center gap-2 px-2 py-1.5 rounded-md hover:bg-muted/70 dark:hover:bg-zinc-800/70 font-semibold text-[13px] text-left text-foreground"
              >
                <span aria-hidden="true" className="text-muted-foreground">
                  {expanded ? '▾' : '▸'}
                </span>
                <span aria-hidden="true">🎲</span>
                <span className="truncate">{game.gameName}</span>
                <span className="ml-auto font-mono text-[10px] text-muted-foreground bg-muted/60 dark:bg-zinc-800/60 rounded-full px-1.5 py-0.5">
                  {game.totalChunks}
                </span>
              </button>
              {expanded && (
                <KbTreeGameDocs
                  gameId={game.gameId}
                  filter={lcFilter}
                  selectedDocId={selectedDocId}
                  onSelectDoc={onSelectDoc}
                />
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}

interface KbTreeGameDocsProps {
  readonly gameId: string;
  readonly filter: string;
  readonly selectedDocId: string | null;
  readonly onSelectDoc: (doc: SelectedDocMeta) => void;
}

function KbTreeGameDocs({ gameId, filter, selectedDocId, onSelectDoc }: KbTreeGameDocsProps) {
  const result = useKbGameDocuments(gameId, /* enabled */ true);
  const data = result?.data;
  const isLoading = result?.isLoading ?? false;

  if (isLoading) {
    return (
      <div
        data-testid={`kb-tree-docs-loading-${gameId}`}
        className="pl-8 py-1.5 text-[11px] text-muted-foreground font-mono"
      >
        Caricamento documenti…
      </div>
    );
  }

  const docs = (data ?? []).filter(d => (filter ? d.title.toLowerCase().includes(filter) : true));

  if (!docs.length) {
    return (
      <div className="pl-8 py-1 text-[11px] text-muted-foreground italic">
        {filter ? 'Nessun documento corrispondente' : 'Nessun documento'}
      </div>
    );
  }

  return (
    <ul role="group">
      {docs.map(doc => {
        const selected = selectedDocId === doc.id;
        return (
          <li key={doc.id}>
            <button
              type="button"
              role="treeitem"
              aria-selected={selected}
              data-status={doc.status}
              onClick={() => onSelectDoc({ id: doc.id, title: doc.title, gameId })}
              className={[
                'w-full text-left pl-8 pr-2 py-1 rounded-md flex items-center gap-2 text-[12px]',
                selected
                  ? 'bg-amber-500/10 text-amber-700 dark:text-amber-300'
                  : 'text-muted-foreground hover:bg-muted/70 dark:hover:bg-zinc-800/70 hover:text-foreground',
              ].join(' ')}
            >
              <span aria-hidden="true">📄</span>
              <span className="truncate flex-1">{doc.title}</span>
              <span className={`font-mono text-[10px] ${statusColor(doc.status)}`}>
                {statusLabel(doc.status, doc.pageCount)}
              </span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}
