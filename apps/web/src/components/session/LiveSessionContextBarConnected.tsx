'use client';

import { useCallback } from 'react';

import { useOverlayStore } from '@/lib/stores/overlay-store';

import { LiveSessionContextBar } from './LiveSessionContextBar';

interface LiveSessionContextBarConnectedProps {
  sessionId: string;
}

export function LiveSessionContextBarConnected({ sessionId }: LiveSessionContextBarConnectedProps) {
  const open = useOverlayStore(s => s.open);

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
