/**
 * Game Detail Page (Italian Version) - Issue #1841 (PAGE-005)
 *
 * Full-page game detail view with:
 * - Hero image (16:9 aspect ratio)
 * - Info grid (Players, Time, Difficulty)
 * - 3 Tabs: Overview (Panoramica), FAQ, Chat
 * - Integrated AI chat in Chat tab
 * - Quick questions chips in Chat tab
 *
 * Route: /giochi/[id]
 * Pattern: Server Component + Client interactivity (follows /games/[id])
 *
 * @see Issue #1841 PAGE-005
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

import { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { GameDetailClient } from './game-detail-client';

// ============================================================================
// Types
// ============================================================================

interface GameDetailPageProps {
  params: Promise<{ id: string }>;
}

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

// ============================================================================
// Data Fetching
// ============================================================================

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

async function fetchGame(id: string): Promise<Game | null> {
  try {
    const url = `${API_BASE}/api/v1/games/${id}`;
    const response = await fetch(url, {
      next: { revalidate: 300 }, // Cache for 5 minutes
    });

    if (!response.ok) {
      if (response.status === 404) return null;
      throw new Error(`Failed to fetch game: ${response.status}`);
    }

    return response.json();
  } catch (error) {
    console.error(`Error fetching game ${id}:`, error);
    return null;
  }
}

// ============================================================================
// Metadata Generation
// ============================================================================

export async function generateMetadata({ params }: GameDetailPageProps): Promise<Metadata> {
  const { id } = await params;
  const game = await fetchGame(id);

  if (!game) {
    return {
      title: 'Gioco Non Trovato - MeepleAI',
      description: 'Il gioco richiesto non è stato trovato.',
    };
  }

  return {
    title: `${game.title} - Dettagli Gioco - MeepleAI`,
    description: `Esplora ${game.title}${game.publisher ? ` di ${game.publisher}` : ''}. Scopri le regole, fai domande all'AI e ottieni aiuto per giocare.`,
    keywords: [game.title, 'gioco da tavolo', 'regole', 'AI assistant', 'MeepleAI'],
    openGraph: {
      title: `${game.title} - MeepleAI`,
      description: `Scopri le regole e fai domande all'AI su ${game.title}`,
      images: game.imageUrl
        ? [
            {
              url: game.imageUrl,
              width: 800,
              height: 600,
              alt: game.title,
            },
          ]
        : undefined,
      type: 'website',
    },
  };
}

// ============================================================================
// Page Component (Server Component)
// ============================================================================

export default async function GameDetailPage({ params }: GameDetailPageProps) {
  const { id } = await params;
  const game = await fetchGame(id);

  if (!game) {
    notFound();
  }

  // Pass game data to Client Component for interactivity
  return <GameDetailClient game={game} />;
}
