/**
 * Session State Editor Page
 * Issue #2406: Game State Editor UI
 *
 * Interactive game state editor for viewing and manually editing game state during sessions.
 */

'use client';

import { useEffect, useState } from 'react';

import { ChevronLeft, Eye, Pencil } from 'lucide-react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';

import { ErrorDisplay } from '@/components/errors';
import { GameStateEditor, GameStateViewer, StateHistoryTimeline } from '@/components/game-state';
import { Button } from '@/components/ui/primitives/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { useGameStateKeyboard } from '@/hooks/useGameStateKeyboard';
import { useGameStateSignalR } from '@/hooks/useGameStateSignalR';
import { categorizeError } from '@/lib/errorUtils';
import { useGameStateStore } from '@/lib/stores/game-state-store';
import type { GameState } from '@/types/game-state';

export default function SessionStatePage() {
  const router = useRouter();
  const params = useParams();
  const sessionId = params?.id as string | undefined;

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'view' | 'edit' | 'history'>('view');

  const { currentState, template, saveState, isConnected } = useGameStateStore();

  // SignalR connection for real-time updates
  const { connectionError } = useGameStateSignalR({
    sessionId: sessionId || '',
    enabled: !!sessionId,
    onStateChanged: _newState => {
      // State updated via SignalR - handled by store
    },
    onConflictDetected: conflict => {
      console.warn('State conflict detected:', conflict);
      // TODO: Show conflict resolution UI
    },
  });

  // Keyboard shortcuts
  useGameStateKeyboard({
    enabled: activeTab === 'edit',
    onSave: async () => {
      if (sessionId) {
        await handleSave();
      }
    },
  });

  // Fetch state and template
  useEffect(() => {
    if (!sessionId) return;

    const fetchData = async () => {
      try {
        setLoading(true);
        setError(null);

        // TODO: Replace with actual API calls
        // await loadTemplate(sessionId);
        // await loadState(sessionId);

        // Mock data for development
        const mockState: GameState = {
          sessionId,
          gameId: 'mock-game-id',
          templateId: 'mock-template-id',
          version: '1.0',
          phase: 'Setup',
          currentPlayerIndex: 0,
          roundNumber: 1,
          players: [
            {
              playerName: 'Alice',
              playerOrder: 1,
              color: '#FF6B6B',
              score: 10,
              resources: { wood: 5, stone: 3 },
            },
            {
              playerName: 'Bob',
              playerOrder: 2,
              color: '#4ECDC4',
              score: 8,
              resources: { wood: 3, stone: 5 },
            },
          ],
          globalResources: { bank: 100, victory_points: 50 },
        };

        useGameStateStore.getState().setState(mockState);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load game state');
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, [sessionId]);

  const handleSave = async () => {
    if (!sessionId || !currentState) return;

    try {
      await saveState(sessionId);
      // State saved successfully
    } catch (err) {
      console.error('Failed to save state:', err);
      setError(err instanceof Error ? err.message : 'Failed to save state');
    }
  };

  // Handle missing session ID
  if (!sessionId) {
    return (
      <div className="container mx-auto py-8 px-4">
        <Card>
          <CardHeader>
            <CardTitle>Invalid Session</CardTitle>
            <CardDescription>No session ID provided</CardDescription>
          </CardHeader>
          <CardContent>
            <Button onClick={() => router.push('/sessions')}>Back to Sessions</Button>
          </CardContent>
        </Card>
      </div>
    );
  }

  // Loading state
  if (loading) {
    return (
      <div className="container mx-auto py-8 px-4">
        <Skeleton className="h-8 w-64 mb-4" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  // Error state
  if (error && !currentState) {
    return (
      <div className="container mx-auto py-8 px-4">
        <ErrorDisplay
          error={categorizeError(new Error(error))}
          onRetry={() => window.location.reload()}
          onDismiss={() => router.push(`/sessions/${sessionId}`)}
          showTechnicalDetails={process.env.NODE_ENV === 'development'}
        />
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8 px-4">
      {/* Header */}
      <div className="mb-6">
        <Link
          href={`/sessions/${sessionId}`}
          className="text-sm text-muted-foreground hover:text-foreground mb-2 inline-flex items-center gap-1"
        >
          <ChevronLeft className="h-4 w-4" />
          Back to Session
        </Link>
        <div className="flex justify-between items-start">
          <div>
            <h1 className="text-3xl font-bold mb-2">Game State Editor</h1>
            <div className="flex gap-2 items-center">
              {template && (
                <p className="text-sm text-muted-foreground">
                  Schema: {template.name} (v{template.version})
                </p>
              )}
              {connectionError && (
                <p className="text-sm text-destructive">Connection: {connectionError}</p>
              )}
              {isConnected && <p className="text-sm text-green-600">● Connected</p>}
            </div>
          </div>
        </div>
      </div>

      {/* Error display */}
      {error && (
        <ErrorDisplay
          error={categorizeError(new Error(error))}
          onDismiss={() => setError(null)}
          showTechnicalDetails={process.env.NODE_ENV === 'development'}
        />
      )}

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={v => setActiveTab(v as typeof activeTab)}>
        <TabsList className="grid w-full grid-cols-3 max-w-md">
          <TabsTrigger value="view" className="flex items-center gap-2">
            <Eye className="h-4 w-4" />
            View
          </TabsTrigger>
          <TabsTrigger value="edit" className="flex items-center gap-2">
            <Pencil className="h-4 w-4" />
            Edit
          </TabsTrigger>
          <TabsTrigger value="history" className="flex items-center gap-2">
            History
          </TabsTrigger>
        </TabsList>

        <TabsContent value="view" className="mt-6">
          {currentState ? (
            <GameStateViewer
              state={currentState}
              currentPlayerIndex={currentState.currentPlayerIndex}
            />
          ) : (
            <p className="text-muted-foreground">No state available</p>
          )}
        </TabsContent>

        <TabsContent value="edit" className="mt-6">
          <GameStateEditor
            sessionId={sessionId}
            onSave={handleSave}
            onCancel={() => setActiveTab('view')}
          />
        </TabsContent>

        <TabsContent value="history" className="mt-6">
          <StateHistoryTimeline sessionId={sessionId} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
