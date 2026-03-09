/**
 * ChatDrawerSheet - Chat History Drawer for Library Cards
 *
 * Right-side Sheet that displays chat history for a game.
 * Shows chat thread list with last message preview + "New Chat" button.
 * Triggered by clicking "Chats" in CardNavigationFooter.
 */

'use client';

import { useQuery } from '@tanstack/react-query';
import { Loader2, MessageCircle, Plus } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

export interface ChatDrawerSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameId: string;
  gameTitle: string;
}

export function ChatDrawerSheet({ open, onOpenChange, gameId, gameTitle }: ChatDrawerSheetProps) {
  const {
    data: threads = [],
    isLoading: loading,
    error,
  } = useQuery({
    queryKey: ['game-chat-threads', gameId],
    queryFn: () => api.chat.getThreadsByGame(gameId),
    enabled: open && !!gameId,
    staleTime: 60 * 1000,
  });

  const handleNewChat = () => {
    onOpenChange(false);
    window.location.href = `/chat/new?game=${gameId}`;
  };

  const handleOpenThread = (threadId: string) => {
    onOpenChange(false);
    window.location.href = `/chat/${threadId}`;
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className={cn('sm:max-w-lg', 'bg-white/80 backdrop-blur-xl')}>
        <SheetHeader className="pb-4 border-b border-border/30">
          <SheetTitle className="font-quicksand text-lg">
            <span className="text-[hsl(262,83%,58%)]">Chat</span>{' '}
            <span className="text-card-foreground">{gameTitle}</span>
          </SheetTitle>
          <div className="flex items-center gap-3 mt-1">
            <span className="text-xs text-muted-foreground font-nunito">
              {threads.length} {threads.length === 1 ? 'conversazione' : 'conversazioni'}
            </span>
            <div className="flex-1" />
            <Button
              variant="outline"
              size="sm"
              onClick={handleNewChat}
              className="h-7 gap-1.5 text-xs font-nunito"
            >
              <Plus className="h-3.5 w-3.5" />
              Nuova Chat
            </Button>
          </div>
        </SheetHeader>

        <div className="mt-4 flex flex-col gap-3 overflow-y-auto max-h-[calc(100vh-180px)]">
          {/* Loading */}
          {loading && threads.length === 0 && (
            <div className="flex flex-col items-center justify-center py-12 gap-2">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              <p className="text-sm text-muted-foreground font-nunito">Caricamento...</p>
            </div>
          )}

          {/* Error */}
          {error && (
            <div className="flex items-center gap-2 rounded-lg bg-red-50 p-3 text-sm text-red-700">
              Errore nel caricamento delle chat
            </div>
          )}

          {/* Empty state */}
          {!loading && !error && threads.length === 0 && (
            <div className="flex flex-col items-center justify-center py-12 gap-3 text-center">
              <div className="rounded-full bg-muted/50 p-4">
                <MessageCircle className="h-8 w-8 text-muted-foreground" />
              </div>
              <div>
                <p className="font-quicksand font-semibold text-card-foreground">
                  Nessuna conversazione
                </p>
                <p className="text-sm text-muted-foreground font-nunito mt-1">
                  Inizia una chat con l&apos;agente AI di questo gioco
                </p>
              </div>
              <Button variant="outline" size="sm" onClick={handleNewChat} className="mt-2 gap-1.5">
                <Plus className="h-4 w-4" />
                Nuova Chat
              </Button>
            </div>
          )}

          {/* Thread list */}
          {threads.map(thread => {
            const lastMessage = thread.messages?.[thread.messages.length - 1];
            const date = new Date(thread.createdAt);

            return (
              <button
                key={thread.id}
                type="button"
                onClick={() => handleOpenThread(thread.id)}
                className={cn(
                  'flex items-start gap-3 rounded-xl p-3 text-left',
                  'bg-[rgba(45,42,38,0.04)] hover:bg-[rgba(45,42,38,0.06)]',
                  'transition-colors cursor-pointer w-full'
                )}
              >
                <MessageCircle className="h-4 w-4 mt-0.5 flex-shrink-0 text-violet-500" />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between gap-2">
                    <p className="text-sm font-medium text-card-foreground truncate font-nunito">
                      {thread.title || 'Chat senza titolo'}
                    </p>
                    <span className="text-[10px] text-muted-foreground flex-shrink-0">
                      {date.toLocaleDateString('it-IT', {
                        day: '2-digit',
                        month: '2-digit',
                      })}
                    </span>
                  </div>
                  {lastMessage && (
                    <p className="text-xs text-muted-foreground mt-0.5 line-clamp-2 font-nunito">
                      {lastMessage.content}
                    </p>
                  )}
                  <span className="text-[10px] text-muted-foreground mt-1">
                    {thread.messages?.length || 0} messaggi
                  </span>
                </div>
              </button>
            );
          })}
        </div>
      </SheetContent>
    </Sheet>
  );
}
