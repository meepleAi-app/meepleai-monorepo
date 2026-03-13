'use client';

/**
 * ParticipantList - Display session participants with role badges and agent access toggles
 *
 * Game Night Improvvisata - multi-device participant management.
 * Shows avatar placeholders, role badges, agent access switches (host only), and disconnect status.
 */

import { useCallback } from 'react';

import { LogOut } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Switch } from '@/components/ui/forms/switch';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface SessionParticipant {
  id: string;
  displayName: string;
  role: 'Host' | 'Player' | 'Guest';
  agentAccessEnabled: boolean;
  joinedAt: string;
  leftAt?: string;
}

export interface ParticipantListProps {
  sessionId: string;
  isHost: boolean;
  participants: SessionParticipant[];
  onToggleAgentAccess?: (participantId: string, enabled: boolean) => void;
}

// ============================================================================
// Helpers
// ============================================================================

const ROLE_COLORS: Record<SessionParticipant['role'], string> = {
  Host: 'bg-amber-100 text-amber-900 border-amber-200',
  Player: 'bg-blue-100 text-blue-900 border-blue-200',
  Guest: 'bg-slate-100 text-slate-700 border-slate-200',
};

const AVATAR_COLORS: Record<SessionParticipant['role'], string> = {
  Host: 'bg-amber-200 text-amber-800',
  Player: 'bg-blue-200 text-blue-800',
  Guest: 'bg-slate-200 text-slate-700',
};

function getInitial(name: string): string {
  return name.charAt(0).toUpperCase();
}

// ============================================================================
// Component
// ============================================================================

export function ParticipantList({
  isHost,
  participants,
  onToggleAgentAccess,
}: ParticipantListProps) {
  const handleToggle = useCallback(
    (participantId: string, enabled: boolean) => {
      onToggleAgentAccess?.(participantId, enabled);
    },
    [onToggleAgentAccess]
  );

  const activeCount = participants.filter(p => !p.leftAt).length;

  return (
    <div className="space-y-3">
      {/* Header */}
      <h3 className="font-quicksand text-sm font-semibold text-gray-700">
        Participants ({activeCount})
      </h3>

      {/* List */}
      <ul className="space-y-2" role="list" aria-label="Session participants">
        {participants.map(participant => {
          const hasLeft = Boolean(participant.leftAt);

          return (
            <li
              key={participant.id}
              className={cn(
                'flex items-center gap-3 rounded-lg border px-3 py-2.5 transition-colors',
                'bg-white/70 backdrop-blur-md',
                hasLeft && 'opacity-50'
              )}
              data-testid={`participant-${participant.id}`}
            >
              {/* Avatar */}
              <div
                className={cn(
                  'flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-sm font-bold font-quicksand',
                  AVATAR_COLORS[participant.role]
                )}
                aria-hidden="true"
              >
                {getInitial(participant.displayName)}
              </div>

              {/* Name + Role */}
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <span className="truncate text-sm font-medium font-nunito text-gray-900">
                    {participant.displayName}
                  </span>
                  <Badge
                    variant="outline"
                    className={cn('shrink-0 text-xs px-1.5 py-0', ROLE_COLORS[participant.role])}
                  >
                    {participant.role}
                  </Badge>
                </div>

                {/* Left indicator */}
                {hasLeft && (
                  <div className="mt-0.5 flex items-center gap-1 text-xs text-muted-foreground">
                    <LogOut className="h-3 w-3" />
                    <span>Left</span>
                  </div>
                )}
              </div>

              {/* Agent Access Toggle - host only */}
              {isHost && !hasLeft && (
                <div className="flex shrink-0 items-center gap-2">
                  <label
                    htmlFor={`agent-access-${participant.id}`}
                    className="text-xs text-muted-foreground font-nunito"
                  >
                    AI
                  </label>
                  <Switch
                    id={`agent-access-${participant.id}`}
                    checked={participant.agentAccessEnabled}
                    onCheckedChange={(checked: boolean) => handleToggle(participant.id, checked)}
                    aria-label={`Toggle agent access for ${participant.displayName}`}
                    data-testid={`agent-toggle-${participant.id}`}
                  />
                </div>
              )}
            </li>
          );
        })}
      </ul>

      {/* Empty state */}
      {participants.length === 0 && (
        <p className="py-4 text-center text-sm text-muted-foreground font-nunito">
          No participants yet. Share the invite to get started.
        </p>
      )}
    </div>
  );
}

export default ParticipantList;
