/**
 * PlayerList
 *
 * Game Night Improvvisata — Task 21
 *
 * Renders session players with online/offline indicator and host crown.
 * Provides an "Invita" button that opens the InviteModal.
 */

'use client';

import { useState } from 'react';

import { Crown, UserPlus } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { useLiveSessionStore, type PlayerInfo } from '@/lib/stores/live-session-store';

import { InviteModal } from './InviteModal';

// ─── Sub-components ───────────────────────────────────────────────────────────

interface OnlineDotProps {
  isOnline: boolean;
}

function OnlineDot({ isOnline }: OnlineDotProps) {
  return (
    <span
      className={[
        'h-2.5 w-2.5 rounded-full shrink-0',
        isOnline ? 'bg-green-400 shadow-sm shadow-green-200' : 'bg-gray-300',
      ].join(' ')}
      aria-label={isOnline ? 'Online' : 'Offline'}
    />
  );
}

interface PlayerRowProps {
  player: PlayerInfo;
}

function PlayerRow({ player }: PlayerRowProps) {
  return (
    <div className="flex items-center gap-3 rounded-xl bg-white/70 backdrop-blur-md border border-white/40 shadow-sm px-4 py-3">
      <OnlineDot isOnline={player.isOnline} />

      <span className="flex-1 font-nunito text-sm font-medium text-gray-900 truncate">
        {player.name}
      </span>

      {player.isHost && (
        <Badge
          variant="outline"
          className="bg-amber-50 border-amber-200 text-amber-700 gap-1 text-xs font-nunito"
        >
          <Crown className="h-3 w-3" />
          Host
        </Badge>
      )}

      {!player.isHost && (
        <span className="text-xs text-gray-400 font-nunito">
          {player.isOnline ? 'Online' : 'Offline'}
        </span>
      )}
    </div>
  );
}

// ─── Main Component ────────────────────────────────────────────────────────────

interface PlayerListProps {
  sessionId: string;
  inviteCode: string;
  shareLink: string;
}

export function PlayerList({ sessionId: _sessionId, inviteCode, shareLink }: PlayerListProps) {
  const players = useLiveSessionStore(s => s.players);
  const [showInvite, setShowInvite] = useState(false);

  const onlineCount = players.filter((p: PlayerInfo) => p.isOnline).length;

  return (
    <div className="space-y-4 p-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-quicksand font-bold text-gray-900">Giocatori</h2>
          <p className="text-xs text-gray-500 font-nunito mt-0.5">
            {onlineCount}/{players.length} online
          </p>
        </div>
        <Button
          size="sm"
          variant="outline"
          className="gap-2 font-nunito"
          onClick={() => setShowInvite(true)}
          data-testid="invite-button"
        >
          <UserPlus className="h-4 w-4" />
          Invita
        </Button>
      </div>

      {/* Player list */}
      {players.length === 0 ? (
        <p className="text-sm text-gray-500 font-nunito text-center py-8">
          Nessun giocatore registrato.
        </p>
      ) : (
        <div className="space-y-2">
          {players.map((player: PlayerInfo) => (
            <PlayerRow key={player.id} player={player} />
          ))}
        </div>
      )}

      {/* Invite Modal */}
      <InviteModal
        inviteCode={inviteCode}
        shareLink={shareLink}
        isOpen={showInvite}
        onClose={() => setShowInvite(false)}
      />
    </div>
  );
}
