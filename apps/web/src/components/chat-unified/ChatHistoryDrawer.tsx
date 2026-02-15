/**
 * ChatHistoryDrawer - Thread History Drawer grouped by game (Issue #4366)
 *
 * Shows all user chat threads grouped by game in a right-side drawer.
 * Features: search, active thread highlight, thread actions (delete).
 * Opens from the "History" button in ChatThreadHeader.
 */

'use client';

import React, { useState, useEffect, useCallback, useMemo } from 'react';

import {
  Clock,
  MessageSquare,
  Plus,
  Search,
  Trash2,
  X,
} from 'lucide-react';
import Link from 'next/link';

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import { formatRelativeTime } from '@/lib/utils/timeUtils';

// ============================================================================
// Types
// ============================================================================

interface ThreadItem {
  id: string;
  title: string | null;
  gameId: string | null;
  messageCount: number;
  lastMessageAt: string | null;
  createdAt: string;
}

interface GameGroup {
  gameId: string | null;
  gameName: string;
  threads: ThreadItem[];
}

export interface ChatHistoryDrawerProps {
  /** Whether the drawer is open */
  open: boolean;
  /** Close handler */
  onClose: () => void;
  /** Current active thread ID (for highlighting) */
  activeThreadId?: string;
  /** Handler when a thread is selected */
  onThreadSelect?: (threadId: string) => void;
}

// ============================================================================
// Component
// ============================================================================

export function ChatHistoryDrawer({
  open,
  onClose,
  activeThreadId,
  onThreadSelect,
}: ChatHistoryDrawerProps) {
  const [groups, setGroups] = useState<GameGroup[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  // Load thread history
  useEffect(() => {
    if (!open) return;

    async function loadHistory() {
      setIsLoading(true);
      try {
        // Load games first, then threads per game
        const gamesResponse = await api.games.getAll();
        const games = gamesResponse.games ?? [];

        const gameGroups: GameGroup[] = [];

        // Load threads for each game in parallel
        const results = await Promise.allSettled(
          games.map(async (game: { id: string; title: string }) => {
            const threads = await api.chat.getThreadsByGame(game.id);
            return {
              gameId: game.id,
              gameName: game.title,
              threads: (threads ?? []).map((t) => ({
                id: t.id,
                title: t.title ?? 'Untitled',
                gameId: t.gameId ?? game.id,
                messageCount: t.messageCount ?? 0,
                lastMessageAt: t.lastMessageAt ?? t.createdAt,
                createdAt: t.createdAt,
              })),
            };
          })
        );

        for (const result of results) {
          if (result.status === 'fulfilled' && result.value.threads.length > 0) {
            gameGroups.push(result.value);
          }
        }

        // Sort groups by most recent thread activity
        gameGroups.sort((a, b) => {
          const aLatest = a.threads[0]?.lastMessageAt ?? a.threads[0]?.createdAt ?? '';
          const bLatest = b.threads[0]?.lastMessageAt ?? b.threads[0]?.createdAt ?? '';
          return bLatest.localeCompare(aLatest);
        });

        setGroups(gameGroups);
      } catch {
        // Silent fail - drawer is non-critical
      } finally {
        setIsLoading(false);
      }
    }

    void loadHistory();
  }, [open]);

  // Filter by search query
  const filteredGroups = useMemo(() => {
    if (!searchQuery.trim()) return groups;

    const query = searchQuery.toLowerCase();
    return groups
      .map(group => ({
        ...group,
        threads: group.threads.filter(
          t =>
            (t.title ?? '').toLowerCase().includes(query) ||
            group.gameName.toLowerCase().includes(query)
        ),
      }))
      .filter(group => group.threads.length > 0);
  }, [groups, searchQuery]);

  // Total thread count
  const totalThreads = useMemo(
    () => groups.reduce((sum, g) => sum + g.threads.length, 0),
    [groups]
  );

  // Handle thread click
  const handleThreadClick = useCallback(
    (threadId: string) => {
      onThreadSelect?.(threadId);
      onClose();
    },
    [onThreadSelect, onClose]
  );

  // Handle delete thread
  const handleDeleteThread = useCallback(
    async (threadId: string, e: React.MouseEvent) => {
      e.stopPropagation();
      try {
        await api.chat.deleteThread(threadId);
        setGroups(prev =>
          prev
            .map(group => ({
              ...group,
              threads: group.threads.filter(t => t.id !== threadId),
            }))
            .filter(group => group.threads.length > 0)
        );
      } catch {
        // Silent fail
      }
    },
    []
  );

  return (
    <Sheet open={open} onOpenChange={val => !val && onClose()}>
      <SheetContent
        className="w-[340px] sm:w-[380px] p-0 flex flex-col"
        data-testid="chat-history-drawer"
      >
        {/* Header */}
        <SheetHeader className="px-4 pt-4 pb-3 border-b border-border/50">
          <div className="flex items-center justify-between">
            <SheetTitle className="text-lg font-quicksand">
              Storia Conversazioni
            </SheetTitle>
            <span className="text-xs text-muted-foreground font-nunito">
              {totalThreads} thread
            </span>
          </div>

          {/* Search */}
          <div className="relative mt-2">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <input
              type="text"
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              placeholder="Cerca conversazioni..."
              className={cn(
                'w-full pl-9 pr-8 py-2 rounded-lg border border-border/50',
                'bg-white/70 dark:bg-card/70 backdrop-blur-md text-sm font-nunito',
                'placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-500/40'
              )}
              data-testid="history-search-input"
            />
            {searchQuery && (
              <button
                onClick={() => setSearchQuery('')}
                className="absolute right-2 top-1/2 -translate-y-1/2 p-0.5 rounded hover:bg-muted/50"
                aria-label="Cancella ricerca"
                data-testid="history-search-clear"
              >
                <X className="h-3.5 w-3.5 text-muted-foreground" />
              </button>
            )}
          </div>
        </SheetHeader>

        {/* Thread list */}
        <div className="flex-1 overflow-y-auto" data-testid="history-thread-list">
          {isLoading ? (
            <div className="p-4 space-y-4" data-testid="history-loading">
              {[1, 2, 3].map(i => (
                <div key={i} className="animate-pulse space-y-2">
                  <div className="h-4 w-24 bg-muted rounded" />
                  <div className="h-14 bg-muted rounded-lg" />
                  <div className="h-14 bg-muted rounded-lg" />
                </div>
              ))}
            </div>
          ) : filteredGroups.length === 0 ? (
            <div
              className="flex flex-col items-center justify-center h-full text-center p-8"
              data-testid="history-empty"
            >
              <MessageSquare className="h-10 w-10 text-muted-foreground/40 mb-3" />
              <p className="text-sm text-muted-foreground font-nunito">
                {searchQuery
                  ? 'Nessun risultato per la ricerca'
                  : 'Nessuna conversazione trovata'}
              </p>
            </div>
          ) : (
            <div className="py-2">
              {filteredGroups.map(group => (
                <div key={group.gameId ?? 'no-game'} className="mb-2">
                  {/* Game group header */}
                  <div
                    className="px-4 py-1.5 text-[11px] font-semibold text-muted-foreground uppercase tracking-wide font-quicksand"
                    data-testid={`game-group-${group.gameId ?? 'general'}`}
                  >
                    {group.gameName}
                  </div>

                  {/* Threads */}
                  {group.threads.map(thread => {
                    const isActive = thread.id === activeThreadId;
                    return (
                      <div
                        key={thread.id}
                        role="button"
                        tabIndex={0}
                        onClick={() => handleThreadClick(thread.id)}
                        onKeyDown={e => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault();
                            handleThreadClick(thread.id);
                          }
                        }}
                        className={cn(
                          'w-full text-left px-4 py-2.5 transition-colors group relative cursor-pointer',
                          isActive
                            ? 'bg-amber-50 dark:bg-amber-500/10 border-l-2 border-amber-500'
                            : 'hover:bg-muted/50 border-l-2 border-transparent'
                        )}
                        aria-current={isActive ? 'true' : undefined}
                        data-testid={`thread-item-${thread.id}`}
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div className="min-w-0 flex-1">
                            <p
                              className={cn(
                                'text-sm font-medium truncate font-nunito',
                                isActive && 'text-amber-900 dark:text-amber-200'
                              )}
                            >
                              {thread.title ?? 'Chat senza titolo'}
                            </p>
                            <div className="flex items-center gap-2 mt-0.5 text-[11px] text-muted-foreground">
                              <span className="flex items-center gap-0.5">
                                <MessageSquare className="h-3 w-3" />
                                {thread.messageCount}
                              </span>
                              {thread.lastMessageAt && (
                                <>
                                  <span>•</span>
                                  <span className="flex items-center gap-0.5">
                                    <Clock className="h-3 w-3" />
                                    {formatRelativeTime(new Date(thread.lastMessageAt))}
                                  </span>
                                </>
                              )}
                            </div>
                          </div>

                          {/* Delete button */}
                          <button
                            onClick={e => void handleDeleteThread(thread.id, e)}
                            className={cn(
                              'p-1 rounded opacity-0 group-hover:opacity-100 transition-opacity',
                              'hover:bg-red-100 dark:hover:bg-red-500/10 text-muted-foreground hover:text-red-600'
                            )}
                            aria-label={`Elimina ${thread.title ?? 'thread'}`}
                            data-testid={`thread-delete-${thread.id}`}
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              ))}
            </div>
          )}
        </div>

        {/* New conversation button */}
        <div className="p-3 border-t border-border/50">
          <Link
            href="/chat/new"
            onClick={onClose}
            className={cn(
              'flex items-center justify-center gap-2 w-full py-2.5 rounded-xl',
              'bg-amber-500 hover:bg-amber-600 text-white text-sm font-medium',
              'transition-colors font-nunito'
            )}
            data-testid="history-new-chat-btn"
          >
            <Plus className="h-4 w-4" />
            Nuova Conversazione
          </Link>
        </div>
      </SheetContent>
    </Sheet>
  );
}
