/**
 * SharedChatView Component (Issue #2052)
 *
 * Public page for viewing shared chat threads via share link.
 * Supports two modes:
 * - View: Read-only access to messages and citations
 * - Comment: Read + ability to add new messages (rate-limited)
 *
 * Features:
 * - Token-based authentication (JWT from URL)
 * - Read-only message display
 * - Comment box for 'comment' role
 * - Error handling for invalid/expired tokens
 * - SSR-friendly with loading states
 */

'use client';

import { useState, useEffect } from 'react';

import { AlertCircle, Eye, MessageSquare, Loader2 } from 'lucide-react';
import { useSearchParams } from 'next/navigation';

import { CommentBox } from '@/components/chat/CommentBox';
import { Message } from '@/components/chat/Message';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Separator } from '@/components/ui/navigation/separator';
import { api, type GetSharedThreadResponse } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';

export function SharedChatView() {
  const searchParams = useSearchParams();
  const token = searchParams?.get('token') ?? null;

  const [thread, setThread] = useState<GetSharedThreadResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!token) {
      setError('No share link token provided');
      setIsLoading(false);
      return;
    }

    const loadThread = async () => {
      try {
        setIsLoading(true);
        const result = await api.shareLinks.getSharedThread(token);
        setThread(result);
        setError(null);
      } catch (err: unknown) {
        const context = createErrorContext('SharedChatView', 'loadThread', { token: '***' });
        const error = err instanceof Error ? err : new Error(String(err));
        logger.error('Failed to load shared thread', error, context);
        setError('This link is invalid, expired, or has been revoked');
      } finally {
        setIsLoading(false);
      }
    };

    loadThread();
  }, [token]);

  const handleCommentAdded = () => {
    // Reload thread to show new comment
    if (token) {
      api.shareLinks
        .getSharedThread(token)
        .then(setThread)
        .catch((err: unknown) => {
          const error = err instanceof Error ? err : new Error(String(err));
          logger.error('Failed to reload thread after comment', error);
        });
    }
  };

  // Loading state
  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="text-center">
          <Loader2 className="animate-spin h-12 w-12 text-primary mx-auto mb-4" />
          <p className="text-muted-foreground">Loading shared chat...</p>
        </div>
      </div>
    );
  }

  // Error state
  if (error || !thread) {
    return (
      <div className="flex h-screen items-center justify-center p-4">
        <Card className="max-w-md">
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-destructive">
              <AlertCircle className="h-5 w-5" />
              Access Denied
            </CardTitle>
            <CardDescription>{error || 'Unable to load shared chat thread'}</CardDescription>
          </CardHeader>
          <CardContent>
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                This link may have expired or been revoked by the owner.
              </AlertDescription>
            </Alert>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Success state: Display thread
  return (
    <div className="flex flex-col h-screen">
      {/* Header with thread info */}
      <div className="border-b bg-card">
        <div className="container max-w-4xl mx-auto px-4 py-4">
          <div className="flex items-start justify-between">
            <div className="flex-1">
              <h1 className="text-2xl font-bold mb-1">{thread.title || 'Shared Chat Thread'}</h1>
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Badge variant={thread.role === 'comment' ? 'default' : 'secondary'}>
                  {thread.role === 'view' && (
                    <>
                      <Eye className="mr-1 h-3 w-3" />
                      View Only
                    </>
                  )}
                  {thread.role === 'comment' && (
                    <>
                      <MessageSquare className="mr-1 h-3 w-3" />
                      View + Comment
                    </>
                  )}
                </Badge>
                <span>•</span>
                <span>{thread.messages.length} messages</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto">
        <div className="container max-w-4xl mx-auto px-4 py-6">
          <div className="space-y-4">
            {thread.messages.map(message => (
              <Message
                key={message.id}
                message={{
                  id: message.id,
                  content: message.content,
                  role: message.role as 'user' | 'assistant',
                  timestamp: new Date(message.timestamp),
                  updatedAt: message.updatedAt || undefined,
                  isDeleted: message.isDeleted,
                  isInvalidated: message.isInvalidated,
                }}
                isUser={message.role === 'user'}
                isSharedView={true}
              />
            ))}
          </div>
        </div>
      </div>

      {/* Comment box for 'comment' role */}
      {thread.role === 'comment' && token && (
        <>
          <Separator />
          <div className="border-t bg-card">
            <div className="container max-w-4xl mx-auto px-4 py-4">
              <CommentBox token={token} onCommentAdded={handleCommentAdded} />
            </div>
          </div>
        </>
      )}

      {/* View-only footer */}
      {thread.role === 'view' && (
        <div className="border-t bg-muted/30">
          <div className="container max-w-4xl mx-auto px-4 py-3">
            <p className="text-xs text-center text-muted-foreground">
              This is a view-only link. You cannot add comments.
            </p>
          </div>
        </div>
      )}
    </div>
  );
}
