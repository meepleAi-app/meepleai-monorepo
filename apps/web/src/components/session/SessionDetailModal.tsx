'use client';

import React from 'react';
import { Calendar, Users, Trophy } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { Badge } from '@/components/ui/data-display/badge';
import { Scoreboard } from './Scoreboard';
import type { Session, ScoreboardData, Participant } from './types';

interface SessionDetailModalProps {
  session: Session;
  scoreboard?: ScoreboardData;
  participants?: Participant[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/**
 * Session Detail Modal
 *
 * Shows complete session details including:
 * - Session metadata (date, duration, participants)
 * - Full scoreboard with round breakdown
 * - Winner information
 * - Notes (if any)
 */
export function SessionDetailModal({
  session,
  scoreboard,
  participants,
  open,
  onOpenChange,
}: SessionDetailModalProps) {
  // Mock scoreboard if not provided (for history view)
  const displayScoreboard: ScoreboardData = scoreboard || {
    participants: participants || [],
    scores: [],
    rounds: [],
    categories: [],
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <div className="flex items-center gap-3">
            {session.gameIcon && <span className="text-3xl">{session.gameIcon}</span>}
            <div>
              <DialogTitle className="text-2xl">
                {session.gameName || 'Session'} - {session.sessionCode}
              </DialogTitle>
              <DialogDescription>
                Session details and final scores
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        {/* Session Metadata */}
        <div className="grid md:grid-cols-2 gap-4 mb-6">
          <div className="flex items-center gap-3 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <Calendar className="w-5 h-5 text-gray-600 dark:text-gray-400" />
            <div>
              <p className="text-xs text-gray-500 dark:text-gray-400">Date</p>
              <p className="font-medium">
                {new Date(session.sessionDate).toLocaleString('it-IT')}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <Users className="w-5 h-5 text-gray-600 dark:text-gray-400" />
            <div>
              <p className="text-xs text-gray-500 dark:text-gray-400">Participants</p>
              <p className="font-medium">{session.participantCount} players</p>
            </div>
          </div>

          <div className="flex items-center gap-3 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <Badge
              variant={session.status === 'Finalized' ? 'default' : 'secondary'}
              className="text-sm"
            >
              {session.status}
            </Badge>
          </div>

          <div className="flex items-center gap-3 p-3 bg-gray-50 dark:bg-gray-800 rounded-lg">
            <Trophy className="w-5 h-5 text-gray-600 dark:text-gray-400" />
            <div>
              <p className="text-xs text-gray-500 dark:text-gray-400">Type</p>
              <p className="font-medium">{session.sessionType}</p>
            </div>
          </div>
        </div>

        {/* Scoreboard */}
        {displayScoreboard && (
          <div className="mb-4">
            <h3 className="text-lg font-semibold mb-3">Final Scores</h3>
            <Scoreboard data={displayScoreboard} isRealTime={false} />
          </div>
        )}

        {/* Participants List */}
        {participants && participants.length > 0 && (
          <div>
            <h3 className="text-lg font-semibold mb-3">Participants</h3>
            <div className="grid md:grid-cols-2 gap-2">
              {participants
                .sort((a, b) => (a.rank || 999) - (b.rank || 999))
                .map((participant) => (
                  <div
                    key={participant.id}
                    className="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-800 rounded-lg"
                  >
                    <div className="flex items-center gap-3">
                      <div
                        className="w-8 h-8 rounded-full flex items-center justify-center text-white font-bold text-sm"
                        style={{ backgroundColor: participant.avatarColor }}
                      >
                        {participant.displayName.charAt(0).toUpperCase()}
                      </div>
                      <span className="font-medium">{participant.displayName}</span>
                    </div>
                    <div className="flex items-center gap-2">
                      {participant.rank && (
                        <Badge variant={participant.rank === 1 ? 'default' : 'outline'}>
                          {participant.rank === 1 && '🥇'} #{participant.rank}
                        </Badge>
                      )}
                      <span className="font-bold text-lg">{participant.totalScore}</span>
                    </div>
                  </div>
                ))}
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
