'use client';

/**
 * Invite Session Component (Issue #3354)
 *
 * Allows users to generate and share session invite links with QR codes.
 */

import { useState, useCallback } from 'react';

import { Check, Copy, Link2, Loader2, RefreshCw, Share2, Users } from 'lucide-react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

import type { InviteTokenResponse } from './types';

// ============================================================================
// Types
// ============================================================================

export interface InviteSessionProps {
  /** Session ID */
  sessionId: string;

  /** Session code for display */
  sessionCode: string;

  /** Whether the session is finalized */
  isFinalized?: boolean;

  /** Custom class name */
  className?: string;
}

type ExpirationOption = '1' | '24' | '72' | '168' | 'never';

// ============================================================================
// Component
// ============================================================================

export function InviteSession({
  sessionId,
  sessionCode,
  isFinalized = false,
  className,
}: InviteSessionProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);
  const [copied, setCopied] = useState(false);
  const [inviteData, setInviteData] = useState<InviteTokenResponse | null>(null);
  const [expiration, setExpiration] = useState<ExpirationOption>('24');

  // ==========================================================================
  // Handlers
  // ==========================================================================

  const handleGenerateInvite = useCallback(async () => {
    setIsGenerating(true);

    try {
      const expiresInHours = expiration === 'never' ? null : parseInt(expiration, 10);
      const params = new URLSearchParams();
      if (expiresInHours !== null) {
        params.set('expiresInHours', expiresInHours.toString());
      }

      const response = await fetch(`/api/v1/game-sessions/${sessionId}/generate-invite?${params}`, {
        method: 'POST',
        credentials: 'include',
      });

      if (!response.ok) {
        throw new Error('Failed to generate invite');
      }

      const data: InviteTokenResponse = await response.json();
      setInviteData(data);
    } catch (error) {
      logger.error('Failed to generate invite:', error);
    } finally {
      setIsGenerating(false);
    }
  }, [sessionId, expiration]);

  const handleCopyLink = useCallback(async () => {
    if (!inviteData) return;

    try {
      await navigator.clipboard.writeText(inviteData.inviteUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (error) {
      logger.error('Failed to copy:', error);
    }
  }, [inviteData]);

  const handleNativeShare = useCallback(async () => {
    if (!inviteData || !navigator.share) return;

    try {
      await navigator.share({
        title: `Join Session ${sessionCode}`,
        text: 'Join my game session on MeepleAI!',
        url: inviteData.inviteUrl,
      });
    } catch (error) {
      // User cancelled or share failed
      logger.error('Share failed:', error);
    }
  }, [inviteData, sessionCode]);

  const formatExpiration = (expiresAt: Date | null): string => {
    if (!expiresAt) return 'Never expires';

    const date = new Date(expiresAt);
    const now = new Date();
    const diffMs = date.getTime() - now.getTime();
    const diffHours = Math.round(diffMs / (1000 * 60 * 60));

    if (diffHours < 1) return 'Expires soon';
    if (diffHours < 24) return `Expires in ${diffHours}h`;
    const diffDays = Math.round(diffHours / 24);
    return `Expires in ${diffDays}d`;
  };

  // ==========================================================================
  // Render
  // ==========================================================================

  if (isFinalized) {
    return null;
  }

  return (
    <Dialog open={isOpen} onOpenChange={setIsOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm" className={cn('gap-2', className)}>
          <Users className="h-4 w-4" />
          <span className="hidden sm:inline">Invite</span>
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Link2 className="h-5 w-5 text-primary" />
            Invite Players
          </DialogTitle>
          <DialogDescription>Share this link to invite players to your session</DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {!inviteData ? (
            // Generate invite form
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="expiration">Link Expiration</Label>
                <Select
                  value={expiration}
                  onValueChange={(value: string) => setExpiration(value as ExpirationOption)}
                >
                  <SelectTrigger id="expiration">
                    <SelectValue placeholder="Select expiration" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="1">1 hour</SelectItem>
                    <SelectItem value="24">24 hours</SelectItem>
                    <SelectItem value="72">3 days</SelectItem>
                    <SelectItem value="168">7 days</SelectItem>
                    <SelectItem value="never">Never expires</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <Button
                onClick={handleGenerateInvite}
                disabled={isGenerating}
                className="w-full gap-2"
              >
                {isGenerating ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Generating...
                  </>
                ) : (
                  <>
                    <Link2 className="h-4 w-4" />
                    Generate Invite Link
                  </>
                )}
              </Button>
            </div>
          ) : (
            // Invite link display
            <div className="space-y-4">
              {/* QR Code - using <img> for data URL, next/image doesn't optimize data URLs */}
              <div className="flex justify-center">
                <div className="rounded-lg border bg-white p-4">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={inviteData.qrCodeDataUrl}
                    alt="QR Code"
                    data-testid="invite-qr-code"
                    className="h-40 w-40"
                  />
                </div>
              </div>

              {/* Session code */}
              <div className="text-center">
                <p className="text-sm text-muted-foreground">Session Code</p>
                <p className="font-mono text-2xl font-bold tracking-wider">
                  {inviteData.sessionCode}
                </p>
              </div>

              {/* Invite URL */}
              <div className="space-y-2">
                <Label htmlFor="invite-url">Invite Link</Label>
                <div className="flex gap-2">
                  <Input
                    id="invite-url"
                    data-testid="invite-url-input"
                    value={inviteData.inviteUrl}
                    readOnly
                    className="font-mono text-sm"
                  />
                  <Button
                    variant="outline"
                    size="icon"
                    onClick={handleCopyLink}
                    className="shrink-0"
                    aria-label="Copy invite link"
                    data-testid="copy-invite-link-btn"
                  >
                    {copied ? (
                      <Check className="h-4 w-4 text-green-500" />
                    ) : (
                      <Copy className="h-4 w-4" />
                    )}
                  </Button>
                </div>
              </div>

              {/* Expiration info */}
              <p className="text-center text-sm text-muted-foreground">
                {formatExpiration(inviteData.expiresAt)}
              </p>

              {/* Actions */}
              <div className="flex gap-2">
                <Button variant="outline" onClick={handleGenerateInvite} className="flex-1 gap-2">
                  <RefreshCw className="h-4 w-4" />
                  New Link
                </Button>

                {typeof navigator !== 'undefined' && 'share' in navigator && (
                  <Button onClick={handleNativeShare} className="flex-1 gap-2">
                    <Share2 className="h-4 w-4" />
                    Share
                  </Button>
                )}
              </div>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default InviteSession;
