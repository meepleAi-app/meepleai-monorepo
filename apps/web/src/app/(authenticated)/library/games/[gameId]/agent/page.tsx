/**
 * Agent Page - Main AI Assistant Interface
 * Issue #3237 (FRONT-001): Base Setup & Routing
 * Issue #3249: [FRONT-013] Agent Type Switcher & Dynamic Typology
 *
 * Features:
 * - Agent configuration and launch
 * - Real-time chat with SSE streaming
 * - PDF viewer with citation links
 * - Session management and history
 * - Dynamic typology switching during chat (preserves history)
 */

'use client';

import { useEffect, useState } from 'react';

import { useParams } from 'next/navigation';

import { AgentConfigSheet } from '@/components/agent/config/AgentConfigSheet';
import { Button } from '@/components/ui/primitives/button';
import { useAgentStore } from '@/stores/agentStore';
import type { Game } from '@/types/domain';

export default function AgentPage() {
  const params = useParams();
  const gameId = (params?.gameId as string) || '';
  const [game, setGame] = useState<Game | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const { isConfigOpen, openConfig, closeConfig } = useAgentStore();

  useEffect(() => {
    const fetchGame = async () => {
      try {
        const response = await fetch(`/api/v1/games/${gameId}`);
        if (response.ok) {
          const data = await response.json();
          setGame(data);
        }
      } catch (error) {
        console.error('Failed to fetch game:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchGame();
  }, [gameId]);

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="text-lg">Loading agent...</div>
      </div>
    );
  }

  if (!game) {
    return (
      <div className="flex h-screen items-center justify-center">
        <div className="text-lg text-red-500">Game not found</div>
      </div>
    );
  }

  return (
    <div className="flex h-screen flex-col bg-slate-950">
      {/* Header */}
      <header className="border-b border-slate-800 bg-slate-900 px-6 py-4">
        <div className="flex items-center gap-4">
          <div className="text-4xl">🤖</div>
          <div>
            <h1 className="text-2xl font-bold text-white">{game.title}</h1>
            <p className="text-sm text-slate-400">AI Assistant</p>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="flex flex-1 items-center justify-center">
        <div className="text-center">
          <h2 className="mb-4 text-3xl font-bold text-cyan-400">Agent Page</h2>
          <p className="mb-6 text-slate-400">
            Configure your AI assistant
          </p>
          <Button onClick={openConfig} size="lg">
            Configure Agent
          </Button>
          <div className="mt-8 text-sm text-slate-500">
            Issue #3238 (FRONT-002) - Config Sheet Container
          </div>
        </div>
      </main>

      {/* Config Sheet */}
      {game && (
        <AgentConfigSheet
          isOpen={isConfigOpen}
          onClose={closeConfig}
          gameId={gameId}
          gameTitle={game.title}
        />
      )}

    </div>
  );
}
