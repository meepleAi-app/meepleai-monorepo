/**
 * ChatHistoryDrawer - Thread History Drawer grouped by game (Issue #4366)
 *
 * Shows all user chat threads grouped by game in a right-side drawer.
 * Features: search, agent filter, active thread highlight, swipe-to-delete (mobile).
 * Opens from the "History" button in ChatThreadHeader.
 */

'use client';

import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';

import {
  Bot,
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
  agentName: string | null;
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
// SwipeableThreadItem - Thread row with swipe-to-delete on mobile
// ============================================================================

const SWIPE_THRESHOLD = 80;

interface SwipeableThreadItemProps {
  thread: ThreadItem;
  isActive: boolean;
  onClick: () => void;
  onDelete: (threadId: string, e?: React.MouseEvent) => Promise<void>;
}

function SwipeableThreadItem({ thread, isActive, onClick, onDelete }: SwipeableThreadItemProps) {
  const itemRef = useRef<HTMLDivElement>(null);
  const [swipeX, setSwipeX] = useState(0);
  const [isSwiping, setIsSwiping] = useState(false);
  const touchStartRef = useRef<{ x: number; y: number } | null>(null);

  const handleTouchStart = useCallback((e: React.TouchEvent) => {
    touchStartRef.current = { x: e.touches[0].clientX, y: e.touches[0].clientY };
    setIsSwiping(false);
  }, []);

  const handleTouchMove = useCallback((e: React.TouchEvent) => {
    if (!touchStartRef.current) return;
    const dx = e.touches[0].clientX - touchStartRef.current.x;
    const dy = e.touches[0].clientY - touchStartRef.current.y;

    // Only swipe left, and only if horizontal movement is dominant
    if (Math.abs(dx) > Math.abs(dy) && dx < 0) {
      setIsSwiping(true);
      setSwipeX(Math.max(dx, -SWIPE_THRESHOLD - 20));
    }
  }, []);

  const handleTouchEnd = useCallback(() => {
    if (Math.abs(swipeX) >= SWIPE_THRESHOLD) {
      // Past threshold: trigger delete
      void onDelete(thread.id);
    }
    setSwipeX(0);
    setIsSwiping(false);
    touchStartRef.current = null;
  }, [swipeX, thread.id, onDelete]);

  return (
    <div className="relative overflow-hidden">
      {/* Delete background revealed on swipe */}
      <div className="absolute inset-y-0 right-0 flex items-center justify-center bg-red-500 text-white px-4">
        <Trash2 className="h-4 w-4" />
      </div>

      {/* Swipeable foreground */}
      <div
        ref={itemRef}
        role="button"
        tabIndex={0}
        data-testid={`thread-item-${thread.id}`}
        onClick={() => !isSwiping && onClick()}
        onKeyDown={e => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            onClick();
          }
        }}
        onTouchStart={handleTouchStart}
        onTouchMove={handleTouchMove}
        onTouchEnd={handleTouchEnd}
        className={cn(
          'w-full text-left px-4 py-2.5 transition-colors group relative cursor-pointer bg-background',
          isActive
            ? 'bg-amber-50 dark:bg-amber-500/10 border-l-2 border-amber-500'
            : 'hover:bg-muted/50 border-l-2 border-transparent'
        )}
        style={{
          transform: `translateX(${swipeX}px)`,
          transition: isSwiping ? 'none' : 'transform 200ms ease-out',
        }}
        aria-current={isActive ? 'true' : undefined}
      >
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0 flex-1">
            <div className="flex items-center gap-1.5">
              <p
                className={cn(
                  'text-sm font-medium truncate font-nunito',
                  isActive && 'text-amber-900 dark:text-amber-200'
                )}
              >
                {thread.title ?? 'Chat senza titolo'}
              </p>
              {thread.agentName && (
                <span className="flex items-center gap-0.5 text-[10px] px-1.5 py-0.5 rounded-full bg-amber-100 dark:bg-amber-500/10 text-amber-700 dark:text-amber-300 flex-shrink-0">
                  <Bot className="h-2.5 w-2.5" />
                  {thread.agentName}
                </span>
              )}
            </div>
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

          {/* Delete button (desktop hover) */}
          <button
            onClick={e => { e.stopPropagation(); void onDelete(thread.id, e); }}
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
    </div>
  );
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
  const [agentFilter, setAgentFilter] = useState<string | null>(null);

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
                agentName: t.agentName ?? null,
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

  // Distinct agent names across all threads
  const agentNames = useMemo(() => {
    const names = new Set<string>();
    for (const group of groups) {
      for (const thread of group.threads) {
        if (thread.agentName) names.add(thread.agentName);
      }
    }
    return Array.from(names).sort();
  }, [groups]);

  // Filter by search query and agent
  const filteredGroups = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    return groups
      .map(group => ({
        ...group,
        threads: group.threads.filter(t => {
          // Agent filter
          if (agentFilter && t.agentName !== agentFilter) return false;
          // Search filter
          if (query) {
            return (
              (t.title ?? '').toLowerCase().includes(query) ||
              group.gameName.toLowerCase().includes(query)
            );
          }
          return true;
        }),
      }))
      .filter(group => group.threads.length > 0);
  }, [groups, searchQuery, agentFilter]);

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

  // Handle delete thread (called from both click and swipe)
  const handleDeleteThread = useCallback(
    async (threadId: string, e?: React.MouseEvent) => {
      e?.stopPropagation();
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

          {/* Agent filter chips */}
          {agentNames.length > 0 && (
            <div className="flex items-center gap-1.5 mt-2 overflow-x-auto no-scrollbar" data-testid="agent-filter-bar">
              <button
                onClick={() => setAgentFilter(null)}
                className={cn(
                  'flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-medium whitespace-nowrap transition-colors',
                  agentFilter === null
                    ? 'bg-amber-500 text-white'
                    : 'bg-muted/50 text-muted-foreground hover:bg-muted'
                )}
                data-testid="agent-filter-all"
              >
                Tutti
              </button>
              {agentNames.map(name => (
                <button
                  key={name}
                  onClick={() => setAgentFilter(agentFilter === name ? null : name)}
                  className={cn(
                    'flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-medium whitespace-nowrap transition-colors',
                    agentFilter === name
                      ? 'bg-amber-500 text-white'
                      : 'bg-muted/50 text-muted-foreground hover:bg-muted'
                  )}
                  data-testid={`agent-filter-${name}`}
                >
                  <Bot className="h-3 w-3" />
                  {name}
                </button>
              ))}
            </div>
          )}
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
                  {group.threads.map(thread => (
                    <SwipeableThreadItem
                      key={thread.id}
                      thread={thread}
                      isActive={thread.id === activeThreadId}
                      onClick={() => handleThreadClick(thread.id)}
                      onDelete={handleDeleteThread}
                    />
                  ))}
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
