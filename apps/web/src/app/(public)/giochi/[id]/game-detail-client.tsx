/**
 * GameDetailClient - Client Component Wrapper
 *
 * Coordinates all interactive components:
 * - HeroSection (image, title overlay)
 * - InfoGrid (3-col metadata)
 * - Tabs (Overview, FAQ, Chat)
 *   - GameOverviewTab (reuses from /games/detail)
 *   - GameFAQTab (Accordion with placeholder)
 *   - GameChatTab (AI chat + quick questions)
 *
 * Pattern: Client component receives server-fetched data as props
 *
 * Issue #1841 (PAGE-005)
 */

'use client';

import React, { useState } from 'react';

import { ArrowLeft, Info, HelpCircle, MessageSquare } from 'lucide-react';
import Link from 'next/link';

import { GameOverviewTab } from '@/components/games/detail/GameOverviewTab';
import { Button } from '@/components/ui/primitives/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

import { GameChatTab } from './components/GameChatTab';
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
      .catch(err => console.error('Failed to fetch BGG weight:', err));
  }, [game.bggId]);

  return (
    <div className="container mx-auto px-4 py-6 max-w-6xl">
      {/* Back Navigation */}
      <div className="mb-6">
        <Button asChild variant="ghost" size="sm">
          <Link href="/giochi">
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

      {/* Tabs (Overview, FAQ, Chat) */}
      <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
        <TabsList className="grid w-full grid-cols-3">
          <TabsTrigger value="overview" className="flex items-center gap-2">
            <Info className="h-4 w-4" />
            <span className="hidden sm:inline">Panoramica</span>
            <span className="sm:hidden">Info</span>
          </TabsTrigger>
          <TabsTrigger value="faq" className="flex items-center gap-2">
            <HelpCircle className="h-4 w-4" />
            FAQ
          </TabsTrigger>
          <TabsTrigger value="chat" className="flex items-center gap-2">
            <MessageSquare className="h-4 w-4" />
            Chat AI
          </TabsTrigger>
        </TabsList>

        {/* Overview Tab (Reuses GameOverviewTab) */}
        <TabsContent value="overview" className="mt-6">
          <GameOverviewTab game={game} />
        </TabsContent>

        {/* FAQ Tab (Accordion with placeholder data) */}
        <TabsContent value="faq" className="mt-6">
          <GameFAQTab gameId={game.id} gameTitle={game.title} />
        </TabsContent>

        {/* Chat Tab (AI chat + Quick questions) */}
        <TabsContent value="chat" className="mt-6">
          <GameChatTab gameId={game.id} gameTitle={game.title} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
