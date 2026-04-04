'use client';

/**
 * InviteModal - Session invite dialog with QR code + PIN + copy link
 *
 * Game Night Improvvisata - multi-device session invite flow.
 * Shows a QR code, a large PIN, a copyable join link, and an auto-expire countdown.
 */

import { useState, useEffect, useCallback, useRef } from 'react';

import { Copy, Check, Clock } from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { SessionInviteResult } from '@/lib/api/schemas/session-invite.schemas';

// ============================================================================
// Types
// ============================================================================

export interface InviteModalProps {
  sessionId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

// ============================================================================
// Helpers
// ============================================================================

function formatCountdown(totalSeconds: number): string {
  if (totalSeconds <= 0) return '0:00';
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, '0')}`;
}

// ============================================================================
// Component
// ============================================================================

export function InviteModal({ sessionId, open, onOpenChange }: InviteModalProps) {
  const [inviteData, setInviteData] = useState<SessionInviteResult | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [remainingSeconds, setRemainingSeconds] = useState<number | null>(null);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Compute join link from invite data
  const joinLink = inviteData
    ? `${typeof window !== 'undefined' ? window.location.origin : ''}/sessions/${sessionId}/join?token=${inviteData.linkToken}`
    : '';

  // --------------------------------------------------------------------------
  // Fetch invite on open
  // --------------------------------------------------------------------------

  useEffect(() => {
    if (!open) {
      // Reset state when dialog closes
      setInviteData(null);
      setError(null);
      setCopied(false);
      setRemainingSeconds(null);
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
      return;
    }

    let cancelled = false;

    async function fetchInvite() {
      setIsLoading(true);
      setError(null);

      try {
        const result = await api.sessionInvites.createInvite(sessionId, {});
        if (cancelled) return;

        setInviteData(result);

        // Calculate initial remaining seconds from expiresAt
        if (result.expiresAt) {
          const expiresMs = new Date(result.expiresAt).getTime();
          const nowMs = Date.now();
          const diffSeconds = Math.max(0, Math.floor((expiresMs - nowMs) / 1000));
          setRemainingSeconds(diffSeconds);
        }
      } catch {
        if (!cancelled) {
          setError('Failed to create invite. Please try again.');
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    fetchInvite();

    return () => {
      cancelled = true;
    };
  }, [open, sessionId]);

  // --------------------------------------------------------------------------
  // Countdown timer
  // --------------------------------------------------------------------------

  useEffect(() => {
    if (remainingSeconds === null || remainingSeconds <= 0) return;

    timerRef.current = setInterval(() => {
      setRemainingSeconds(prev => {
        if (prev === null || prev <= 1) {
          if (timerRef.current) clearInterval(timerRef.current);
          return 0;
        }
        return prev - 1;
      });
    }, 1000);

    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    };
  }, [remainingSeconds !== null && remainingSeconds > 0]); // eslint-disable-line react-hooks/exhaustive-deps

  // --------------------------------------------------------------------------
  // Copy link handler
  // --------------------------------------------------------------------------

  const handleCopyLink = useCallback(async () => {
    if (!joinLink) return;

    try {
      await navigator.clipboard.writeText(joinLink);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback: ignore copy failure silently
    }
  }, [joinLink]);

  // --------------------------------------------------------------------------
  // Expired state
  // --------------------------------------------------------------------------

  const isExpired = remainingSeconds !== null && remainingSeconds <= 0;

  // --------------------------------------------------------------------------
  // Render
  // --------------------------------------------------------------------------

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md bg-white/70 backdrop-blur-md">
        <DialogHeader>
          <DialogTitle className="font-quicksand text-lg">Invite Players</DialogTitle>
          <DialogDescription className="font-nunito text-sm">
            Share the PIN or QR code so others can join this session
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-5 py-2">
          {/* Loading state */}
          {isLoading && (
            <div className="space-y-4" data-testid="invite-loading">
              <div className="flex justify-center">
                <Skeleton className="h-48 w-48 rounded-lg" />
              </div>
              <Skeleton className="mx-auto h-12 w-40" />
              <Skeleton className="h-10 w-full" />
            </div>
          )}

          {/* Error state */}
          {error && !isLoading && (
            <div
              className="rounded-lg bg-red-50 px-4 py-3 text-center text-sm text-red-700"
              role="alert"
            >
              {error}
            </div>
          )}

          {/* Invite data */}
          {inviteData && !isLoading && (
            <>
              {/* QR Code */}
              <div className="flex justify-center">
                <div className="rounded-xl border bg-white p-4 shadow-sm">
                  <QRCodeSVG value={joinLink} size={192} level="M" data-testid="invite-qr-code" />
                </div>
              </div>

              {/* PIN */}
              <div className="text-center">
                <p className="mb-1 text-xs font-medium uppercase tracking-wider text-muted-foreground font-nunito">
                  Session PIN
                </p>
                <p
                  className="font-mono text-4xl font-bold tracking-[0.25em] text-gray-900"
                  data-testid="invite-pin"
                >
                  {inviteData.pin}
                </p>
              </div>

              {/* Copy Link */}
              <Button
                variant="outline"
                className="w-full gap-2 font-nunito"
                onClick={handleCopyLink}
                data-testid="copy-link-button"
              >
                {copied ? (
                  <>
                    <Check className="h-4 w-4 text-green-600" />
                    Copied!
                  </>
                ) : (
                  <>
                    <Copy className="h-4 w-4" />
                    Copy Link
                  </>
                )}
              </Button>

              {/* Countdown */}
              {remainingSeconds !== null && (
                <div
                  className="flex items-center justify-center gap-1.5 text-sm font-nunito"
                  data-testid="invite-countdown"
                >
                  <Clock className="h-3.5 w-3.5 text-muted-foreground" />
                  {isExpired ? (
                    <span className="text-red-600 font-medium">Invite expired</span>
                  ) : (
                    <span className="text-muted-foreground">
                      Expires in {formatCountdown(remainingSeconds)}
                    </span>
                  )}
                </div>
              )}
            </>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default InviteModal;
