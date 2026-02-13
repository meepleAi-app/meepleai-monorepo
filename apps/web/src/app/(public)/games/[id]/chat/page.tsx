/**
 * Game Chat Page (Task #2: User Chat UI)
 *
 * Full-page chat interface for users to interact with AI agent about a specific game.
 * Features:
 * - Game context header (name, image)
 * - SSE streaming responses
 * - Citation support
 * - Conversation history
 * - Mobile responsive
 */

'use client';

import { useEffect, useState } from 'react';

import { ArrowLeft, AlertCircle } from 'lucide-react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';

import { AgentChat } from '@/components/agent/AgentChat';
import { useAuth } from '@/components/auth/AuthProvider';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api, type SharedGameDetail } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';

// ============================================================================
// Main Component
// ============================================================================

export default function GameChatPage() {
  const params = useParams();
  const router = useRouter();
  const gameId = params?.id as string | undefined;
  const { user } = useAuth();

  const [gameDetail, setGameDetail] = useState<SharedGameDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Redirect unauthenticated users
  useEffect(() => {
    if (!user) {
      router.push(`/login?redirect=/games/${gameId}/chat`);
    }
  }, [user, router, gameId]);

  // Load game data
  useEffect(() => {
    if (!gameId) return;

    const loadGame = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await api.sharedGames.getById(gameId);
        if (!data) {
          throw new Error('Game not found');
        }
        setGameDetail(data);
      } catch (err) {
        setError('Failed to load game');
        logger.error(
          'Failed to load game for chat',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameChatPage', 'loadGame', { gameId })
        );
      } finally {
        setLoading(false);
      }
    };

    loadGame();
  }, [gameId]);

  // ============================================================================
  // Loading & Error States
  // ============================================================================

  if (!user) {
    return null; // Will redirect in useEffect
  }

  if (loading) {
    return (
      <div className="min-h-screen bg-[#FAF8F5] py-8 px-4">
        <div className="container mx-auto max-w-4xl">
          <Skeleton className="h-8 w-48 mb-6" />
          <Skeleton className="h-[600px] w-full" />
        </div>
      </div>
    );
  }

  if (error || !gameDetail) {
    return (
      <div className="min-h-screen bg-[#FAF8F5] py-8 px-4">
        <div className="container mx-auto max-w-4xl">
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error || 'Game not found'}</AlertDescription>
          </Alert>
          <Button asChild className="mt-4">
            <Link href="/games">
              <ArrowLeft className="mr-2 h-4 w-4" /> Torna al Catalogo
            </Link>
          </Button>
        </div>
      </div>
    );
  }

  // ============================================================================
  // Main Render
  // ============================================================================

  return (
    <div className="min-h-screen bg-[#FAF8F5] py-8 px-4">
      <div className="container mx-auto max-w-4xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href={`/games/${gameId}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Torna a {gameDetail.title}
          </Link>
        </Button>

        {/* Chat Interface */}
        <AgentChat
          agentId="tutor" // Default to Tutor agent (POC)
          gameId={gameId}
          layout="full-page"
          gameName={gameDetail.title}
          agentName="MeepleAI Assistant"
          strategy="RetrievalOnly"
        />
      </div>
    </div>
  );
}
