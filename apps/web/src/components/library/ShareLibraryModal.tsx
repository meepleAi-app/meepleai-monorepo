/**
 * ShareLibraryModal Component (Issue #2614)
 *
 * Modal dialog for managing library sharing settings.
 * Features:
 * - Create new share link with privacy settings
 * - Update existing share link settings
 * - Copy share URL to clipboard
 * - Revoke share link
 * - View count display
 */

'use client';

import { useState, useEffect } from 'react';

import {
  Loader2,
  Check,
  Link2,
  Copy,
  Trash2,
  Eye,
  Globe,
  Lock,
  Calendar,
  Share2,
  AlertTriangle,
} from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { Badge } from '@/components/ui/data-display/badge';
import { Switch } from '@/components/ui/forms/switch';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import {
  useLibraryShareLink,
  useCreateShareLink,
  useUpdateShareLink,
  useRevokeShareLink,
} from '@/hooks/queries';
import type {
  CreateLibraryShareLinkRequest,
  UpdateLibraryShareLinkRequest,
} from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

interface ShareLibraryModalProps {
  isOpen: boolean;
  onClose: () => void;
}

type PrivacyLevel = 'public' | 'unlisted';

export function ShareLibraryModal({ isOpen, onClose }: ShareLibraryModalProps) {
  // Fetch current share link
  const { data: shareLink, isLoading: isLoadingLink } = useLibraryShareLink(isOpen);
  const createMutation = useCreateShareLink();
  const updateMutation = useUpdateShareLink();
  const revokeMutation = useRevokeShareLink();

  // Form state
  const [privacyLevel, setPrivacyLevel] = useState<PrivacyLevel>('unlisted');
  const [includeNotes, setIncludeNotes] = useState(false);
  const [expiresAt, setExpiresAt] = useState<string>('');
  const [showRevokeConfirm, setShowRevokeConfirm] = useState(false);

  // Load current settings when modal opens
  useEffect(() => {
    if (isOpen && shareLink) {
      setPrivacyLevel(shareLink.privacyLevel as PrivacyLevel);
      setIncludeNotes(shareLink.includeNotes);
      setExpiresAt(shareLink.expiresAt ? formatDateTimeLocal(shareLink.expiresAt) : '');
    } else if (isOpen && !shareLink) {
      // Reset to defaults
      setPrivacyLevel('unlisted');
      setIncludeNotes(false);
      setExpiresAt('');
    }
    setShowRevokeConfirm(false);
  }, [isOpen, shareLink]);

  const isSaving = createMutation.isPending || updateMutation.isPending;
  const isRevoking = revokeMutation.isPending;
  const hasActiveLink = shareLink && shareLink.isActive;

  const handleCreateOrUpdate = async () => {
    const expiresAtDate = expiresAt ? new Date(expiresAt).toISOString() : null;

    try {
      if (hasActiveLink) {
        // Update existing link
        const request: UpdateLibraryShareLinkRequest = {
          privacyLevel,
          includeNotes,
          expiresAt: expiresAtDate,
        };
        await updateMutation.mutateAsync({
          shareToken: shareLink.shareToken,
          request,
        });
        toast.success('Impostazioni condivisione aggiornate.');
      } else {
        // Create new link
        const request: CreateLibraryShareLinkRequest = {
          privacyLevel,
          includeNotes,
          expiresAt: expiresAtDate,
        };
        await createMutation.mutateAsync(request);
        toast.success('Link di condivisione creato!');
      }
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : 'Errore durante il salvataggio delle impostazioni.'
      );
    }
  };

  const handleCopyUrl = async () => {
    if (!shareLink?.shareUrl) return;

    try {
      await navigator.clipboard.writeText(shareLink.shareUrl);
      toast.success('Link copiato negli appunti!');
    } catch {
      toast.error('Impossibile copiare il link.');
    }
  };

  const handleRevoke = async () => {
    if (!shareLink?.shareToken) return;

    try {
      await revokeMutation.mutateAsync(shareLink.shareToken);
      toast.success('Link di condivisione revocato.');
      setShowRevokeConfirm(false);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Errore durante la revoca del link.'
      );
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Share2 className="h-5 w-5" />
            Condividi Libreria
          </DialogTitle>
          <DialogDescription>
            Crea un link pubblico per condividere la tua collezione di giochi con altri.
          </DialogDescription>
        </DialogHeader>

        {isLoadingLink ? (
          <div className="flex items-center justify-center py-12">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        ) : (
          <div className="space-y-6">
            {/* Active Link Display */}
            {hasActiveLink && (
              <div className="space-y-3 rounded-lg border bg-muted/50 p-4">
                <div className="flex items-center justify-between">
                  <Label className="text-sm font-medium">Link Attivo</Label>
                  <Badge variant="secondary" className="flex items-center gap-1">
                    <Eye className="h-3 w-3" />
                    {shareLink.viewCount} visualizzazioni
                  </Badge>
                </div>
                <div className="flex items-center gap-2">
                  <Input
                    value={shareLink.shareUrl}
                    readOnly
                    className="font-mono text-xs"
                    aria-label="Share URL"
                  />
                  <Button
                    variant="outline"
                    size="icon"
                    onClick={handleCopyUrl}
                    aria-label="Copia link"
                  >
                    <Copy className="h-4 w-4" />
                  </Button>
                </div>
                {shareLink.lastAccessedAt && (
                  <p className="text-xs text-muted-foreground">
                    Ultimo accesso: {formatDate(shareLink.lastAccessedAt)}
                  </p>
                )}
              </div>
            )}

            {/* Privacy Level Selection */}
            <div className="space-y-3">
              <Label className="text-sm font-medium">Livello di Privacy</Label>
              <RadioGroup
                value={privacyLevel}
                onValueChange={(v) => setPrivacyLevel(v as PrivacyLevel)}
                className="grid grid-cols-2 gap-4"
              >
                <label
                  htmlFor="privacy-unlisted"
                  className={cn(
                    'flex cursor-pointer flex-col items-center gap-2 rounded-lg border p-4 transition-colors hover:bg-muted/50',
                    privacyLevel === 'unlisted' && 'border-primary bg-primary/5'
                  )}
                >
                  <RadioGroupItem
                    value="unlisted"
                    id="privacy-unlisted"
                    className="sr-only"
                  />
                  <Lock className="h-6 w-6 text-muted-foreground" />
                  <span className="font-medium">Non elencato</span>
                  <span className="text-center text-xs text-muted-foreground">
                    Solo chi ha il link
                  </span>
                </label>
                <label
                  htmlFor="privacy-public"
                  className={cn(
                    'flex cursor-pointer flex-col items-center gap-2 rounded-lg border p-4 transition-colors hover:bg-muted/50',
                    privacyLevel === 'public' && 'border-primary bg-primary/5'
                  )}
                >
                  <RadioGroupItem
                    value="public"
                    id="privacy-public"
                    className="sr-only"
                  />
                  <Globe className="h-6 w-6 text-muted-foreground" />
                  <span className="font-medium">Pubblico</span>
                  <span className="text-center text-xs text-muted-foreground">
                    Visibile a tutti
                  </span>
                </label>
              </RadioGroup>
            </div>

            {/* Include Notes Toggle */}
            <div className="flex items-center justify-between rounded-lg border p-4">
              <div className="space-y-0.5">
                <Label htmlFor="include-notes" className="font-medium">
                  Includi Note Personali
                </Label>
                <p className="text-xs text-muted-foreground">
                  Mostra le tue note ai visitatori
                </p>
              </div>
              <Switch
                id="include-notes"
                checked={includeNotes}
                onCheckedChange={setIncludeNotes}
              />
            </div>

            {/* Expiration Date */}
            <div className="space-y-2">
              <Label htmlFor="expires-at" className="flex items-center gap-2 text-sm font-medium">
                <Calendar className="h-4 w-4" />
                Data di Scadenza (opzionale)
              </Label>
              <Input
                id="expires-at"
                type="datetime-local"
                value={expiresAt}
                onChange={(e) => setExpiresAt(e.target.value)}
                min={getMinDateTime()}
                className="w-full"
              />
              <p className="text-xs text-muted-foreground">
                Lascia vuoto per un link senza scadenza
              </p>
            </div>

            {/* Revoke Section */}
            {hasActiveLink && (
              <div className="space-y-3 rounded-lg border border-destructive/20 bg-destructive/5 p-4">
                {!showRevokeConfirm ? (
                  <Button
                    variant="outline"
                    className="w-full text-destructive hover:bg-destructive/10"
                    onClick={() => setShowRevokeConfirm(true)}
                    disabled={isRevoking}
                  >
                    <Trash2 className="mr-2 h-4 w-4" />
                    Revoca Link
                  </Button>
                ) : (
                  <div className="space-y-3">
                    <div className="flex items-start gap-2">
                      <AlertTriangle className="h-5 w-5 text-destructive" />
                      <p className="text-sm text-destructive">
                        Questa azione disabiliterà permanentemente il link di condivisione.
                        Chiunque abbia il link non potrà più accedere alla tua libreria.
                      </p>
                    </div>
                    <div className="flex gap-2">
                      <Button
                        variant="outline"
                        className="flex-1"
                        onClick={() => setShowRevokeConfirm(false)}
                        disabled={isRevoking}
                      >
                        Annulla
                      </Button>
                      <Button
                        variant="destructive"
                        className="flex-1"
                        onClick={handleRevoke}
                        disabled={isRevoking}
                      >
                        {isRevoking ? (
                          <>
                            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                            Revoca...
                          </>
                        ) : (
                          <>
                            <Trash2 className="mr-2 h-4 w-4" />
                            Conferma Revoca
                          </>
                        )}
                      </Button>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            Chiudi
          </Button>
          <Button onClick={handleCreateOrUpdate} disabled={isSaving || isLoadingLink}>
            {isSaving ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Salvataggio...
              </>
            ) : hasActiveLink ? (
              <>
                <Check className="mr-2 h-4 w-4" />
                Aggiorna Impostazioni
              </>
            ) : (
              <>
                <Link2 className="mr-2 h-4 w-4" />
                Crea Link
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// Helper functions
function formatDateTimeLocal(isoDate: string): string {
  const date = new Date(isoDate);
  const offset = date.getTimezoneOffset();
  const localDate = new Date(date.getTime() - offset * 60 * 1000);
  return localDate.toISOString().slice(0, 16);
}

function formatDate(isoDate: string): string {
  return new Date(isoDate).toLocaleDateString('it-IT', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function getMinDateTime(): string {
  const now = new Date();
  const offset = now.getTimezoneOffset();
  const localDate = new Date(now.getTime() - offset * 60 * 1000);
  return localDate.toISOString().slice(0, 16);
}
