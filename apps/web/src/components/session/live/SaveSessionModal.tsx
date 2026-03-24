/**
 * SaveSessionModal
 *
 * Game Night Improvvisata — Task 20
 *
 * Confirmation dialog for "Salva & Esci":
 *   1. Asks the user to confirm saving
 *   2. Calls createPauseSnapshot()
 *   3. On success → redirect to library
 */

'use client';

import { useState } from 'react';

import { Camera, Loader2, Save } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface SaveSessionModalProps {
  sessionId: string;
  gameName: string;
  isOpen: boolean;
  onClose: () => void;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function SaveSessionModal({ sessionId, gameName, isOpen, onClose }: SaveSessionModalProps) {
  const router = useRouter();
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedOk, setSavedOk] = useState(false);

  async function handleSave() {
    setError(null);
    setIsSaving(true);
    try {
      await api.liveSessions.createPauseSnapshot(sessionId);
      setSavedOk(true);
      // Brief success flash before redirect
      setTimeout(() => {
        router.push('/library');
      }, 800);
    } catch {
      setError('Impossibile salvare la partita. Riprova.');
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={open => !open && !isSaving && onClose()}>
      <DialogContent className="max-w-sm">
        <DialogHeader>
          <DialogTitle className="font-quicksand text-xl font-bold flex items-center gap-2">
            <Save className="h-5 w-5 text-amber-500" />
            Salva &amp; Esci
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {/* Confirmation message */}
          <p className="text-sm font-nunito text-gray-600">
            Vuoi salvare lo stato della partita{' '}
            <span className="font-semibold text-gray-900">{gameName}</span>?
          </p>
          <p className="text-xs font-nunito text-gray-500">
            Potrai riprendere da dove hai lasciato in qualsiasi momento.
          </p>

          {/* Optional photo prompt */}
          <div className="flex items-center gap-2 rounded-lg bg-amber-50 border border-amber-200 p-3 text-sm text-amber-800 font-nunito">
            <Camera className="h-4 w-4 shrink-0 text-amber-600" />
            <span>
              Consiglio: scatta una foto del tabellone prima di uscire per ricordare la situazione.
            </span>
          </div>

          {/* Error */}
          {error && (
            <p className="text-xs text-red-600 font-nunito" role="alert">
              {error}
            </p>
          )}

          {/* Success flash */}
          {savedOk && (
            <p
              className="text-sm text-green-700 font-nunito text-center font-semibold"
              role="status"
            >
              Partita salvata! Ritorno alla libreria...
            </p>
          )}
        </div>

        {/* Actions */}
        <div className="flex gap-2 pt-2">
          <Button
            variant="outline"
            className="flex-1 font-nunito"
            onClick={onClose}
            disabled={isSaving || savedOk}
            data-testid="cancel-save"
          >
            Annulla
          </Button>
          <Button
            className="flex-1 bg-amber-500 hover:bg-amber-600 text-white font-nunito font-semibold"
            onClick={handleSave}
            disabled={isSaving || savedOk}
            data-testid="confirm-save"
          >
            {isSaving ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Salvataggio...
              </>
            ) : (
              'Salva partita'
            )}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
