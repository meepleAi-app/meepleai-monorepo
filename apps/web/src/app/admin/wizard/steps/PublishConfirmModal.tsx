'use client';

/**
 * Publish Confirmation Modal
 *
 * Confirmation dialog before publishing game to SharedGameCatalog.
 * Issue #3480: Admin wizard publish confirmation
 */

import { Button } from '@/components/ui/primitives/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import type { ApprovalStatus } from '@/lib/api/schemas/admin.schemas';

interface PublishConfirmModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  gameName: string;
  pdfFileName: string;
  approvalStatus: ApprovalStatus;
}

const STATUS_LABELS: Record<ApprovalStatus, { label: string; color: string }> = {
  Draft: { label: 'Bozza', color: 'text-slate-600 dark:text-slate-400' },
  PendingReview: { label: 'In Revisione', color: 'text-amber-600 dark:text-amber-400' },
  Approved: { label: 'Approvato', color: 'text-green-600 dark:text-green-400' },
  Rejected: { label: 'Rifiutato', color: 'text-red-600 dark:text-red-400' },
};

export function PublishConfirmModal({
  isOpen,
  onClose,
  onConfirm,
  gameName,
  pdfFileName,
  approvalStatus,
}: PublishConfirmModalProps) {
  const statusInfo = STATUS_LABELS[approvalStatus];

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span className="text-2xl">📚</span>
            Conferma Pubblicazione
          </DialogTitle>
          <DialogDescription>
            Stai per pubblicare questo gioco nella libreria condivisa. Verifica i dettagli prima di
            procedere.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          {/* Game Details */}
          <div className="bg-slate-50 dark:bg-slate-800/50 rounded-lg p-4 space-y-3">
            <div>
              <span className="text-sm text-slate-500">Nome Gioco:</span>
              <p className="font-semibold text-slate-900 dark:text-white">{gameName}</p>
            </div>
            <div>
              <span className="text-sm text-slate-500">PDF Regolamento:</span>
              <p className="font-medium text-slate-700 dark:text-slate-300">{pdfFileName}</p>
            </div>
            <div>
              <span className="text-sm text-slate-500">Stato di Approvazione:</span>
              <p className={`font-semibold ${statusInfo.color}`}>{statusInfo.label}</p>
            </div>
          </div>

          {/* Warning for Approved */}
          {approvalStatus === 'Approved' && (
            <div className="bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 rounded-lg p-3">
              <div className="flex items-start gap-2">
                <span className="text-lg">⚠️</span>
                <div className="text-sm">
                  <p className="font-semibold text-amber-900 dark:text-amber-200 mb-1">
                    Attenzione
                  </p>
                  <p className="text-amber-800 dark:text-amber-300">
                    Il gioco sara' immediatamente visibile nel catalogo pubblico.
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>

        <DialogFooter className="flex-row gap-2">
          <Button variant="outline" onClick={onClose} className="flex-1">
            Annulla
          </Button>
          <Button
            onClick={onConfirm}
            className="flex-1 bg-green-600 hover:bg-green-700 text-white"
          >
            Conferma Pubblicazione
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
