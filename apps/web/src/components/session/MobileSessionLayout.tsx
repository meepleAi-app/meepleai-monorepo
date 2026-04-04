'use client';

import { useState } from 'react';

import { ActivityFeed } from './ActivityFeed';
import { ActivityFeedInputBar } from './ActivityFeedInputBar';
import { BottomSheet } from './BottomSheet';
import { MobileScorebar } from './MobileScorebar';
import { MobileStatusBar } from './MobileStatusBar';

interface MobileSessionLayoutProps {
  sessionId: string;
  gameName: string;
  currentPlayer: string;
  players: { id: string; name: string; score: number }[];
  playerId: string;
  playerName: string;
}

export function MobileSessionLayout({
  sessionId,
  gameName,
  currentPlayer,
  players,
  playerId,
  playerName,
}: MobileSessionLayoutProps) {
  const [diceSheetOpen, setDiceSheetOpen] = useState(false);
  const [cameraSheetOpen, setCameraSheetOpen] = useState(false);
  const [aiSheetOpen, setAISheetOpen] = useState(false);

  return (
    <div className="flex flex-col h-[100dvh] lg:hidden">
      <MobileStatusBar gameName={gameName} currentPlayer={currentPlayer} />
      <MobileScorebar players={players} />

      <div className="flex-1 overflow-y-auto">
        <ActivityFeed />
      </div>

      <ActivityFeedInputBar
        sessionId={sessionId}
        playerId={playerId}
        playerName={playerName}
        onDiceClick={() => setDiceSheetOpen(true)}
        onCameraClick={() => setCameraSheetOpen(true)}
        onAIClick={() => setAISheetOpen(true)}
      />

      <BottomSheet isOpen={diceSheetOpen} onClose={() => setDiceSheetOpen(false)} title="Dadi">
        <p className="text-sm text-muted-foreground p-4">Lancio dadi in arrivo</p>
      </BottomSheet>
      <BottomSheet isOpen={cameraSheetOpen} onClose={() => setCameraSheetOpen(false)} title="Foto">
        <p className="text-sm text-muted-foreground p-4">Fotocamera in arrivo</p>
      </BottomSheet>
      <BottomSheet isOpen={aiSheetOpen} onClose={() => setAISheetOpen(false)} title="AI Assistente">
        <p className="text-sm text-muted-foreground p-4">Assistente AI in arrivo</p>
      </BottomSheet>
    </div>
  );
}
