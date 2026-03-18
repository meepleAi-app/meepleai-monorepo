/**
 * GameDetailClient - Client Component Wrapper
 *
 * Coordinates all interactive components:
 * - HeroSection (image, title overlay)
 * - InfoGrid (3-col metadata)
 * - Tabs (Overview, Rules, Sessions, FAQ, Chat)
 *   - GameOverviewTab (reuses from /games/detail)
 *   - GameRulesTab (KB documents with processing state)
 *   - GameSessionsTab (session history with stats)
 *   - GameFAQTab (Accordion with placeholder)
 *   - GameChatTab (AI chat + quick questions)
 *
 * Pattern: Client component receives server-fetched data as props
 *
 * Issue #1841 (PAGE-005), M3/M4: Wire Rules & Sessions tabs
 */

'use client';

import React, { useState } from 'react';

import { ArrowLeft, Info, FileText, PlayCircle, HelpCircle, MessageSquare } from 'lucide-react';
import Link from 'next/link';

import { GameOverviewTab } from '@/components/games/detail/GameOverviewTab';
import { GameRulesTab } from '@/components/games/detail/GameRulesTab';
import { GameSessionsTab } from '@/components/games/detail/GameSessionsTab';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { useGameDocuments, useGameSessions } from '@/hooks/queries/useGames';
import { logger } from '@/lib/logger';

import { GameFAQTab } from './components/GameFAQTab';
import { HeroSection } from './components/HeroSection';
import { InfoGrid } from './components/InfoGrid';

// ============================================================================
// Types
// ============================================================================

interface Game {
  id: string;
  title: string;
  publisher: string | null;
  yearPublished: number | null;
  minPlayers: number | null;
  maxPlayers: number | null;
  minPlayTimeMinutes: number | null;
  maxPlayTimeMinutes: number | null;
  bggId: number | null;
  imageUrl?: string | null;
  averageRating?: number | null;
  createdAt: string;
}

export interface GameDetailClientProps {
  game: Game;
}

// ============================================================================
// Component
// ============================================================================

export function GameDetailClient({ game }: GameDetailClientProps) {
  const [activeTab, setActiveTab] = useState('overview');

  // Fetch BGG details for complexity/weight (for InfoGrid)
  const [bggWeight, setBggWeight] = React.useState<number | null>(null);

  // Fetch documents and sessions when their tabs are active (or eagerly)
  const { data: documents = [], isLoading: documentsLoading } = useGameDocuments(game.id);
  const { data: sessions = [], isLoading: sessionsLoading } = useGameSessions(game.id);

  React.useEffect(() => {
    if (!game.bggId) return;

    // Fetch BGG details to get averageWeight for complexity display
    fetch(
      `${process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080'}/api/v1/bgg/games/${game.bggId}`
    )
      .then(res => (res.ok ? res.json() : null))
      .then(data => {
        if (data?.averageWeight) {
          setBggWeight(data.averageWeight);
        }
      })
      .catch(err => logger.error('Failed to fetch BGG weight:', err));
  }, [game.bggId]);

  return (
    <div className="container mx-auto px-4 py-6 max-w-6xl">
      {/* Back Navigation */}
      <div className="mb-6">
        <Button asChild variant="ghost" size="sm">
          <Link href="/games">
            <ArrowLeft className="mr-2 h-4 w-4" />
            Torna al Catalogo
          </Link>
        </Button>
      </div>

      {/* Hero Section (16:9 image with title overlay) */}
      <HeroSection title={game.title} imageUrl={game.imageUrl} publisher={game.publisher} />

      {/* Info Grid (3-col: Players, Time, Complexity) */}
      <InfoGrid
        minPlayers={game.minPlayers}
        maxPlayers={game.maxPlayers}
        minPlayTimeMinutes={game.minPlayTimeMinutes}
        maxPlayTimeMinutes={game.maxPlayTimeMinutes}
        averageWeight={bggWeight}
      />

      {/* Tabs (Overview, Rules, Sessions, FAQ, Chat) */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
        <TabsList className="grid w-full grid-cols-5">
          <TabsTrigger value="overview" className="flex items-center gap-1.5">
            <Info className="h-4 w-4" />
            <span className="hidden sm:inline">Panoramica</span>
            <span className="sm:hidden text-xs">Info</span>
          </TabsTrigger>
          <TabsTrigger value="rules" className="flex items-center gap-1.5">
            <FileText className="h-4 w-4" />
            <span className="hidden sm:inline">Regole</span>
            <span className="sm:hidden text-xs">Regole</span>
          </TabsTrigger>
          <TabsTrigger value="sessions" className="flex items-center gap-1.5">
            <PlayCircle className="h-4 w-4" />
            <span className="hidden sm:inline">Partite</span>
            <span className="sm:hidden text-xs">Partite</span>
          </TabsTrigger>
          <TabsTrigger value="faq" className="flex items-center gap-1.5">
            <HelpCircle className="h-4 w-4" />
            <span className="hidden sm:inline">FAQ</span>
            <span className="sm:hidden text-xs">FAQ</span>
          </TabsTrigger>
          <TabsTrigger value="chat" className="flex items-center gap-1.5">
            <MessageSquare className="h-4 w-4" />
            <span className="hidden sm:inline">Chat AI</span>
            <span className="sm:hidden text-xs">Chat</span>
          </TabsTrigger>
        </TabsList>

        {/* Overview Tab */}
        <TabsContent value="overview" className="mt-6">
          <GameOverviewTab game={game} />
        </TabsContent>

        {/* Rules Tab (M3: KB Documents) */}
        <TabsContent value="rules" className="mt-6">
          <GameRulesTab gameId={game.id} documents={documents} isLoading={documentsLoading} />
        </TabsContent>

        {/* Sessions Tab (M4: Session History) */}
        <TabsContent value="sessions" className="mt-6">
          <GameSessionsTab gameId={game.id} sessions={sessions} isLoading={sessionsLoading} />
        </TabsContent>

        {/* FAQ Tab */}
        <TabsContent value="faq" className="mt-6">
          <GameFAQTab gameId={game.id} gameTitle={game.title} />
        </TabsContent>

        {/* Chat Tab - Redirects to unified chat */}
        <TabsContent value="chat" className="mt-6">
          <div className="flex flex-col items-center justify-center py-12 text-center">
            <MessageSquare className="h-12 w-12 text-muted-foreground/50 mb-4" />
            <h3 className="text-lg font-semibold mb-2">Chat AI</h3>
            <p className="text-sm text-muted-foreground mb-4">
              Parla con l&apos;assistente AI su {game.title}
            </p>
            <Button asChild>
              <Link href={`/chat/new?gameId=${game.id}`}>Avvia Chat AI</Link>
            </Button>
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}
