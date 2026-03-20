'use client';

import { useCallback } from 'react';

import { useOverlayStore } from '@/lib/stores/overlay-store';

import { LiveSessionContextBar } from './LiveSessionContextBar';

interface LiveSessionContextBarConnectedProps {
  sessionId: string;
}

export function LiveSessionContextBarConnected({ sessionId }: LiveSessionContextBarConnectedProps) {
  const open = useOverlayStore(s => s.open);

  // TODO: Replace sessionId placeholders with real entity IDs from live session data
  // - handleGameClick should pass gameId (from session's game reference)
  // - handlePlayersClick should use openDeck with player items array
  // - handleScoreClick correctly uses sessionId
  const handleGameClick = useCallback(() => {
    open('game', sessionId);
  }, [open, sessionId]);

  const handlePlayersClick = useCallback(() => {
    open('player', sessionId);
  }, [open, sessionId]);

  const handleScoreClick = useCallback(() => {
    open('session', sessionId);
  }, [open, sessionId]);

  return (
    <LiveSessionContextBar
      onGameClick={handleGameClick}
      onPlayersClick={handlePlayersClick}
      onScoreClick={handleScoreClick}
    />
  );
}
