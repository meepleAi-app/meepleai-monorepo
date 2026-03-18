/**
 * GroupMemoryPanel
 *
 * AgentMemory — Task 25
 *
 * Shows group information during a session: name, members, preferences, stats.
 * Fetches data from the agent-memory API.
 */

'use client';

import { useEffect, useState } from 'react';

import { Clock, Loader2, Users, Gamepad2 } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import type { GroupMemoryDto } from '@/lib/api/clients/agentMemoryClient';
import { useApiClient } from '@/lib/api/context';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface GroupMemoryPanelProps {
  groupId: string;
}

// ─── Component ───────────────────────────────────────────────────────────────

export function GroupMemoryPanel({ groupId }: GroupMemoryPanelProps) {
  const api = useApiClient();
  const [group, setGroup] = useState<GroupMemoryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function fetchGroup() {
      setLoading(true);
      setError(null);
      try {
        const data = await api.agentMemory.getGroup(groupId);
        if (!cancelled) {
          setGroup(data);
        }
      } catch (_err) {
        if (!cancelled) {
          setError('Failed to load group data');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    fetchGroup();
    return () => {
      cancelled = true;
    };
  }, [api, groupId]);

  if (loading) {
    return (
      <div
        className="rounded-xl border border-white/40 bg-white/70 backdrop-blur-md shadow-sm p-4 flex items-center justify-center gap-2"
        data-testid="group-memory-panel"
      >
        <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
        <span className="text-sm text-gray-500 font-nunito">Loading group...</span>
      </div>
    );
  }

  if (error || !group) {
    return (
      <div
        className="rounded-xl border border-red-200 bg-red-50/70 backdrop-blur-md shadow-sm p-4"
        data-testid="group-memory-panel"
      >
        <p className="text-sm text-red-600 font-nunito">{error ?? 'Group not found'}</p>
      </div>
    );
  }

  return (
    <div
      className="rounded-xl border border-white/40 bg-white/70 backdrop-blur-md shadow-sm p-4 space-y-3"
      data-testid="group-memory-panel"
    >
      {/* Group name */}
      <div className="flex items-center gap-2">
        <Users className="h-4 w-4 text-amber-600" />
        <h3 className="font-quicksand font-semibold text-sm text-gray-900">{group.name}</h3>
      </div>

      {/* Members */}
      <div className="space-y-1">
        <span className="text-xs text-gray-500 font-nunito uppercase tracking-wide">Members</span>
        <div className="flex flex-wrap gap-1.5">
          {group.members.map((member, index) => (
            <Badge
              key={member.userId ?? `guest-${index}`}
              variant="outline"
              className={
                member.guestName
                  ? 'bg-purple-50 border-purple-200 text-purple-700 text-xs font-nunito'
                  : 'bg-gray-50 border-gray-200 text-gray-700 text-xs font-nunito'
              }
            >
              {member.guestName ?? member.userId?.slice(0, 8) ?? 'Unknown'}
              {member.guestName && <span className="ml-1 text-purple-400">(guest)</span>}
            </Badge>
          ))}
        </div>
      </div>

      {/* Preferences */}
      {group.preferences && (
        <div className="space-y-1">
          <span className="text-xs text-gray-500 font-nunito uppercase tracking-wide">
            Preferences
          </span>
          <div className="text-sm text-gray-700 font-nunito space-y-0.5">
            {group.preferences.preferredComplexity && (
              <p>
                <Gamepad2 className="inline h-3 w-3 mr-1 text-gray-400" />
                Complexity: {group.preferences.preferredComplexity}
              </p>
            )}
            {group.preferences.maxDuration && (
              <p>
                <Clock className="inline h-3 w-3 mr-1 text-gray-400" />
                Max duration: {group.preferences.maxDuration}
              </p>
            )}
            {group.preferences.customNotes && (
              <p className="italic text-gray-500">{group.preferences.customNotes}</p>
            )}
          </div>
        </div>
      )}

      {/* Stats */}
      {group.stats && (
        <div className="space-y-1">
          <span className="text-xs text-gray-500 font-nunito uppercase tracking-wide">Stats</span>
          <div className="flex gap-4 text-sm font-nunito">
            <span className="text-gray-700">
              <strong className="font-quicksand">{group.stats.totalSessions}</strong> sessions
            </span>
            {group.stats.lastPlayedAt && (
              <span className="text-gray-500">
                Last: {new Date(group.stats.lastPlayedAt).toLocaleDateString()}
              </span>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
