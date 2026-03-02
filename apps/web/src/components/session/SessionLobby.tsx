/**
 * SessionLobby - Waiting room before session starts
 * Issue #4767 - SSE Client + Player/Spectator Mode UI
 *
 * Shows participant list, session code for sharing, and connection status.
 */

'use client';

import { useState, useCallback, useRef, useEffect } from 'react';

import { Copy, Check, Users, Gamepad2 } from 'lucide-react';

import type { Participant } from '@/components/session/types';
import type { ConnectionStatus } from '@/lib/hooks/useSessionStream';

import { ConnectionStatusBadge } from './ConnectionStatusBadge';

interface SessionLobbyProps {
  sessionCode: string;
  sessionName?: string;
  participants: Participant[];
  connectionStatus: ConnectionStatus;
  reconnectCount?: number;
  isHost?: boolean;
  className?: string;
}

export function SessionLobby({
  sessionCode,
  sessionName,
  participants,
  connectionStatus,
  reconnectCount = 0,
  isHost: _isHost = false,
  className = '',
}: SessionLobbyProps) {
  const [copied, setCopied] = useState(false);
  const copyTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current);
    };
  }, []);

  const copyCode = useCallback(async () => {
    try {
      await navigator.clipboard.writeText(sessionCode);
    } catch {
      // Fallback for non-HTTPS or restricted contexts
      const el = document.createElement('textarea');
      el.value = sessionCode;
      el.style.position = 'fixed';
      el.style.opacity = '0';
      document.body.appendChild(el);
      el.select();
      document.execCommand('copy');
      document.body.removeChild(el);
    }
    setCopied(true);
    if (copyTimeoutRef.current) clearTimeout(copyTimeoutRef.current);
    copyTimeoutRef.current = setTimeout(() => setCopied(false), 2000);
  }, [sessionCode]);

  return (
    <div className={`space-y-6 ${className}`} data-testid="session-lobby">
      {/* Header */}
      <div className="text-center">
        {sessionName && (
          <div className="mb-1 flex items-center justify-center gap-1.5 text-sm text-gray-500">
            <Gamepad2 className="h-4 w-4" />
            {sessionName}
          </div>
        )}
        <h2 className="font-quicksand text-xl font-bold text-gray-800">Session Lobby</h2>
        <ConnectionStatusBadge
          status={connectionStatus}
          reconnectCount={reconnectCount}
          className="mt-2"
        />
      </div>

      {/* Share Code */}
      <div className="rounded-xl bg-white/70 p-4 shadow-sm backdrop-blur-md">
        <p className="mb-2 text-center text-xs font-medium uppercase tracking-wider text-gray-500">
          Share this code
        </p>
        <div className="flex items-center justify-center gap-2">
          <span className="font-mono text-3xl font-bold tracking-[0.4em] text-indigo-600">
            {sessionCode}
          </span>
          <button
            onClick={copyCode}
            className="rounded-md p-1.5 text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600"
            aria-label={copied ? 'Copied!' : 'Copy session code'}
          >
            {copied ? <Check className="h-5 w-5 text-emerald-500" /> : <Copy className="h-5 w-5" />}
          </button>
        </div>
      </div>

      {/* Participants */}
      <div>
        <div className="mb-2 flex items-center gap-1.5 text-sm text-gray-500">
          <Users className="h-4 w-4" />
          <span>
            {participants.length} {participants.length === 1 ? 'player' : 'players'} joined
          </span>
        </div>

        <div className="space-y-1.5">
          {participants.map(p => (
            <div
              key={p.id}
              className="flex items-center gap-2 rounded-md bg-white/50 px-3 py-2 text-sm"
            >
              <span
                className="inline-block h-3 w-3 rounded-full"
                style={{ backgroundColor: p.avatarColor }}
              />
              <span className="font-medium">{p.displayName}</span>
              {p.isOwner && (
                <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold uppercase text-amber-700">
                  Host
                </span>
              )}
              {p.isCurrentUser && <span className="text-xs text-gray-400">(you)</span>}
            </div>
          ))}
        </div>

        {participants.length === 0 && (
          <p className="text-center text-sm text-gray-400">Waiting for players to join...</p>
        )}
      </div>
    </div>
  );
}
