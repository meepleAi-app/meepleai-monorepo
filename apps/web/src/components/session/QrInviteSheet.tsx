'use client';

import React, { useState } from 'react';

import { QRCodeSVG } from 'qrcode.react';

import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { cn } from '@/lib/utils';

export interface QrInviteSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  sessionCode: string;
  shareLink: string;
  className?: string;
}

export function QrInviteSheet({
  open,
  onOpenChange,
  sessionCode,
  shareLink,
  className,
}: QrInviteSheetProps) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(shareLink);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback: ignore silently
    }
  }

  return (
    <BottomSheet open={open} onOpenChange={onOpenChange} title="Invita Giocatori">
      <div className={cn('flex flex-col items-center gap-6 pb-4', className)}>
        {/* QR Code */}
        <div className="rounded-xl bg-white p-3">
          <QRCodeSVG
            value={shareLink}
            size={180}
            bgColor="#ffffff"
            fgColor="#000000"
            aria-label="QR code di invito alla sessione"
          />
        </div>

        {/* Session code */}
        <div className="flex flex-col items-center gap-1">
          <span className="text-xs text-[var(--gaming-text-secondary,#aaa)]">Codice sessione</span>
          <span className="font-mono text-2xl font-bold tracking-widest text-white">
            {sessionCode}
          </span>
        </div>

        {/* Copy link button */}
        <button
          onClick={handleCopy}
          aria-label={copied ? 'Link copiato!' : 'Copia link di invito'}
          className={cn(
            'w-full rounded-xl px-4 py-3 text-sm font-medium transition-colors',
            copied ? 'bg-green-500/20 text-green-400' : 'bg-white/10 text-white hover:bg-white/20'
          )}
        >
          {copied ? 'Link copiato!' : 'Copia link'}
        </button>
      </div>
    </BottomSheet>
  );
}
