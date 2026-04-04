/**
 * InviteModal
 *
 * Game Night Improvvisata — Task 19
 *
 * Dialog showing a QR code and invite code so the host can invite guests.
 * Uses qrcode.react (already installed as a project dependency).
 */

'use client';

import { useState } from 'react';

import { Check, Copy } from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';

import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface InviteModalProps {
  /** The short invite code (e.g. "ABC123") */
  inviteCode: string;
  /** Full share URL shown as QR code (e.g. "https://app.meepleai.it/join/ABC123") */
  shareLink: string;
  /** Controls dialog visibility */
  isOpen: boolean;
  /** Called when dialog should close */
  onClose: () => void;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function InviteModal({ inviteCode, shareLink, isOpen, onClose }: InviteModalProps) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(shareLink);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback: select the text via execCommand (older browsers)
      const input = document.createElement('input');
      input.value = shareLink;
      document.body.appendChild(input);
      input.select();
      document.execCommand('copy');
      document.body.removeChild(input);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={open => !open && onClose()}>
      <DialogContent className="max-w-xs text-center sm:max-w-sm">
        <DialogHeader>
          <DialogTitle className="font-quicksand text-xl font-bold text-center">
            Invita giocatori
          </DialogTitle>
        </DialogHeader>

        {/* QR Code */}
        <div className="flex justify-center my-2">
          <div
            className="rounded-2xl bg-white p-4 shadow-md border border-gray-100"
            data-testid="qr-container"
          >
            <QRCodeSVG
              value={shareLink}
              size={180}
              bgColor="#ffffff"
              fgColor="#1c1917"
              level="M"
              aria-label={`QR code per il link ${shareLink}`}
            />
          </div>
        </div>

        {/* Session code */}
        <div className="space-y-1 mt-1">
          <p className="text-xs text-gray-500 font-nunito uppercase tracking-wider">
            Codice sessione
          </p>
          <p
            className="text-3xl font-mono font-bold text-gray-900 tracking-widest"
            data-testid="invite-code"
          >
            {inviteCode}
          </p>
        </div>

        {/* Share link (truncated display) */}
        <p className="text-xs text-gray-400 font-nunito truncate px-2" title={shareLink}>
          {shareLink}
        </p>

        {/* Copy button */}
        <Button
          onClick={handleCopy}
          variant="outline"
          className="w-full font-nunito gap-2 mt-2"
          data-testid="copy-link-button"
        >
          {copied ? (
            <>
              <Check className="h-4 w-4 text-green-600" />
              <span className="text-green-600">Link copiato!</span>
            </>
          ) : (
            <>
              <Copy className="h-4 w-4" />
              Copia link
            </>
          )}
        </Button>
      </DialogContent>
    </Dialog>
  );
}
