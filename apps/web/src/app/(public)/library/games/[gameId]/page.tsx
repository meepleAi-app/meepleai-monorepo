/**
 * Game Detail Page (Issue #3152)
 *
 * Server component for individual game detail view.
 * Fetches game data and renders split view with agent chat + PDF viewer.
 */

import { notFound } from 'next/navigation';

import type { AgentMode, GamePdf } from '@/components/game-detail';

import GameDetailClient from './GameDetailClient';

interface PageProps {
  params: Promise<{
    gameId: string;
  }>;
}

async function fetchGameDetails(gameId: string) {
  try {
    const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'}/api/v1/library/games/${gameId}`, {
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      return null;
    }

    return response.json();
  } catch (error) {
    console.error('Error fetching game details:', error);
    return null;
  }
}

async function fetchAgentModes(gameId: string): Promise<AgentMode[]> {
  try {
    const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'}/api/v1/games/${gameId}/agents`, {
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      console.error('Failed to fetch agent modes:', response.status);
      return [];
    }

    const data = await response.json();

    // Map AgentDto to AgentMode format
    return data.agents?.map((agent: any) => ({
      id: agent.type.toLowerCase(),
      name: `Agente ${agent.name}`,
      description: agent.type,
      model: agent.strategyName || 'default',
    })) || [];
  } catch (error) {
    console.error('Error fetching agent modes:', error);
    return [];
  }
}

async function fetchGamePdfs(gameId: string): Promise<GamePdf[]> {
  try {
    const response = await fetch(`${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'}/api/v1/library/games/${gameId}/pdfs`, {
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
      },
    });

    if (!response.ok) {
      console.error('Failed to fetch game PDFs:', response.status);
      return [];
    }

    const data = await response.json();
    return data.map((pdf: any) => ({
      id: pdf.id,
      name: pdf.name,
      pageCount: pdf.pageCount,
    }));
  } catch (error) {
    console.error('Error fetching game PDFs:', error);
    return [];
  }
}

export default async function GameDetailPage({ params }: PageProps) {
  const { gameId } = await params;

  // Fetch game data
  const [gameDetails, agentModes, gamePdfs] = await Promise.all([
    fetchGameDetails(gameId),
    fetchAgentModes(gameId),
    fetchGamePdfs(gameId),
  ]);

  if (!gameDetails) {
    notFound();
  }

  return (
    <GameDetailClient
      gameId={gameId}
      gameTitle={gameDetails.title}
      gamePublisher={gameDetails.publisher}
      gameImageUrl={gameDetails.imageUrl}
      agentModes={agentModes}
      availablePdfs={gamePdfs}
    />
  );
}

// Enable static params generation for known games (optional)
// export async function generateStaticParams() {
//   return [];
// }
