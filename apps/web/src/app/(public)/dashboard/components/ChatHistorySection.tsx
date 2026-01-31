/**
 * ChatHistorySection Component - Dashboard Chat History
 *
 * Issue #1836: PAGE-002 - Dashboard Page
 *
 * Displays user's most recent chat threads across all games.
 * For MVP, shows placeholder UI with link to full chat page.
 *
 * Future Enhancement (Issue to be created):
 * - Backend: GET /api/v1/knowledge-base/chat-threads (no gameId filter)
 * - Frontend: useRecentChats() hook for global chat history
 * - Display: 5 most recent threads with game badges
 *
 * Features:
 * - Placeholder UI for MVP
 * - Link to full chat interface
 * - Skeleton loading states (pulse animation)
 * - Empty state for no chats
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

import React from 'react';

import { MessageSquare, ArrowRight } from 'lucide-react';
import Link from 'next/link';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

export interface ChatHistorySectionProps {
  /** User ID for fetching chat history */
  userId: string;
}

/**
 * ChatHistorySection Component (MVP Placeholder)
 *
 * Backend endpoint available: GET /api/v1/knowledge-base/my-chats (Issue #2026)
 * Optimized response with lightweight DTOs and correct pagination.
 *
 * Response Format:
 * {
 *   threads: Array<{id, gameId, gameName, title, lastMessageContent, lastMessageAt, messageCount}>,
 *   count: number,  // Total threads (for pagination)
 *   page: { skip, take, hasMore }
 * }
 *
 * Future Enhancement: Implement data fetching hook and uncomment UI below.
 */
export function ChatHistorySection({ userId: _userId }: ChatHistorySectionProps) {
  return (
    <section className="space-y-4" aria-label="Chat history" data-testid="chat-history-section">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-quicksand font-semibold" data-testid="chat-history-title">Cronologia Chat</h2>
        <Button variant="ghost" size="sm" asChild data-testid="chat-history-view-all-button">
          <Link href="/chat">
            Vedi Tutte
            <ArrowRight className="ml-2 h-4 w-4" />
          </Link>
        </Button>
      </div>

      {/* MVP Placeholder Card */}
      <Card className="border-dashed" data-testid="chat-history-placeholder-card">
        <CardHeader className="pb-4">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-primary/10 rounded-lg">
              <MessageSquare className="h-5 w-5 text-primary" />
            </div>
            <div className="flex-1">
              <CardTitle className="text-base" data-testid="chat-history-card-title">Chat Recenti</CardTitle>
              <CardDescription className="text-sm mt-1" data-testid="chat-history-card-description">
                Visualizza le tue conversazioni più recenti
              </CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground mb-4" data-testid="chat-history-info-text">
            La cronologia completa delle chat è disponibile nella sezione Chat.
          </p>
          <Button asChild className="w-full sm:w-auto" data-testid="chat-history-open-button">
            <Link href="/chat">
              Apri Chat
              <MessageSquare className="ml-2 h-4 w-4" />
            </Link>
          </Button>
        </CardContent>
      </Card>

      {/* Future Implementation: Uncomment when backend endpoint is ready */}
      {/*
      {isLoading && (
        <div className="space-y-3">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-20 w-full" />
          ))}
        </div>
      )}

      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Errore di Caricamento</AlertTitle>
          <AlertDescription>
            Impossibile caricare la cronologia chat. Riprova più tardi.
          </AlertDescription>
        </Alert>
      )}

      {chats && chats.length > 0 && (
        <div className="space-y-3">
          {chats.slice(0, 5).map((chat) => (
            <Card key={chat.id} className="cursor-pointer hover:shadow-md transition-shadow">
              <CardHeader className="p-4">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <CardTitle className="text-sm line-clamp-1">
                      {chat.title || 'Chat senza titolo'}
                    </CardTitle>
                    <CardDescription className="text-xs mt-1">
                      {new Date(chat.lastMessageAt || chat.createdAt).toLocaleDateString('it-IT', {
                        day: 'numeric',
                        month: 'short',
                        year: 'numeric',
                      })}
                    </CardDescription>
                  </div>
                  {chat.gameId && (
                    <Badge variant="secondary" className="text-xs flex-shrink-0">
                      {chat.gameName || 'Gioco'}
                    </Badge>
                  )}
                </div>
              </CardHeader>
            </Card>
          ))}
        </div>
      )}
      */}
    </section>
  );
}
