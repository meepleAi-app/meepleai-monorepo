'use client';

/**
 * Step 5: Publish to Shared Library
 *
 * Final wizard step to publish game to SharedGameCatalog with approval status selection.
 * Issue #3480: Admin wizard publish step
 */

import { useState, useCallback } from 'react';

import { useRouter } from 'next/navigation';

import { toast } from '@/components/layout';
import { Spinner } from '@/components/loading';
import { Card } from '@/components/ui/data-display/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';
import type { ApprovalStatus } from '@/lib/api/schemas/admin.schemas';

import { PublishConfirmModal } from './PublishConfirmModal';

interface PublishStepProps {
  gameId: string;
  gameName: string;
  pdfId: string;
  pdfFileName: string;
  onBack: () => void;
  onComplete: () => void;
}

const APPROVAL_STATUS_OPTIONS: { value: ApprovalStatus; label: string; description: string }[] = [
  {
    value: 'Draft',
    label: 'Bozza (Draft)',
    description: 'Gioco salvato ma non pubblicato. Solo admin possono vedere.',
  },
  {
    value: 'PendingReview',
    label: 'In Revisione (Pending Review)',
    description: 'Richiede approvazione admin prima della pubblicazione.',
  },
  {
    value: 'Approved',
    label: 'Approvato (Approved)',
    description: 'Approvato e pubblicato nel catalogo condiviso.',
  },
  {
    value: 'Rejected',
    label: 'Rifiutato (Rejected)',
    description: 'Richiede modifiche prima della pubblicazione.',
  },
];

export function PublishStep({
  gameId,
  gameName,
  pdfId,
  pdfFileName,
  onBack,
  onComplete,
}: PublishStepProps) {
  const router = useRouter();
  const [approvalStatus, setApprovalStatus] = useState<ApprovalStatus>('PendingReview');
  const [showConfirmModal, setShowConfirmModal] = useState(false);
  const [publishing, setPublishing] = useState(false);

  const handlePublish = useCallback(async () => {
    setShowConfirmModal(false);
    setPublishing(true);

    try {
      // Issue #3480 + #3481: Real backend integration
      await api.admin.publishGameToSharedLibrary(gameId, approvalStatus);

      toast.success(`Gioco "${gameName}" pubblicato con stato: ${approvalStatus}`);
      onComplete();

      // Redirect to admin dashboard or game details
      router.push('/admin/shared-games');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Pubblicazione fallita: ${message}`);
      setPublishing(false);
    }
  }, [gameId, gameName, approvalStatus, onComplete, router]);

  const selectedOption = APPROVAL_STATUS_OPTIONS.find(opt => opt.value === approvalStatus);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white mb-2">
          Pubblica nella Libreria Condivisa
        </h2>
        <p className="text-slate-600 dark:text-slate-400">
          Seleziona lo stato di approvazione e pubblica il gioco nel catalogo condiviso.
        </p>
      </div>

      {/* Game Summary */}
      <Card className="p-4 bg-slate-50 dark:bg-slate-800/50 space-y-3">
        <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-300">
          Riepilogo Gioco
        </h3>
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <span className="text-slate-500">Nome Gioco:</span>{' '}
            <span className="font-medium">{gameName}</span>
          </div>
          <div>
            <span className="text-slate-500">PDF Regolamento:</span>{' '}
            <span className="font-medium">{pdfFileName}</span>
          </div>
          <div className="col-span-2">
            <span className="text-slate-500">Game ID:</span>{' '}
            <span className="font-mono text-xs">{gameId}</span>
          </div>
          <div className="col-span-2">
            <span className="text-slate-500">PDF ID:</span>{' '}
            <span className="font-mono text-xs">{pdfId}</span>
          </div>
        </div>
      </Card>

      {/* Approval Status Selector */}
      <div className="space-y-3">
        <Label htmlFor="approval-status" className="text-base font-medium">
          Stato di Approvazione *
        </Label>
        <Select value={approvalStatus} onValueChange={(value: string) => setApprovalStatus(value as ApprovalStatus)}>
          <SelectTrigger id="approval-status" className="w-full">
            <SelectValue placeholder="Seleziona stato..." />
          </SelectTrigger>
          <SelectContent>
            {APPROVAL_STATUS_OPTIONS.map(option => (
              <SelectItem key={option.value} value={option.value}>
                <div className="flex flex-col py-1">
                  <span className="font-medium">{option.label}</span>
                  <span className="text-xs text-slate-500">{option.description}</span>
                </div>
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        {selectedOption && (
          <p className="text-sm text-slate-600 dark:text-slate-400">
            <span className="font-semibold">Selezione:</span> {selectedOption.description}
          </p>
        )}
      </div>

      {/* Warning for Approved status */}
      {approvalStatus === 'Approved' && (
        <Card className="p-4 bg-amber-50 dark:bg-amber-900/20 border-amber-200 dark:border-amber-800">
          <div className="flex items-start gap-3">
            <span className="text-2xl">⚠️</span>
            <div>
              <h4 className="font-semibold text-amber-900 dark:text-amber-200 mb-1">
                Attenzione: Pubblicazione Immediata
              </h4>
              <p className="text-sm text-amber-800 dark:text-amber-300">
                Selezionando "Pubblicato", il gioco sara' immediatamente visibile nel catalogo pubblico.
                Assicurati che il contenuto sia corretto e completo.
              </p>
            </div>
          </div>
        </Card>
      )}

      {/* Actions */}
      <div className="flex justify-between gap-3 pt-4">
        <Button variant="outline" onClick={onBack} disabled={publishing}>
          ← Indietro
        </Button>
        <Button
          onClick={() => setShowConfirmModal(true)}
          disabled={publishing}
          className="min-w-40 bg-green-600 hover:bg-green-700"
        >
          {publishing ? (
            <>
              <Spinner size="sm" className="mr-2" />
              Pubblicazione...
            </>
          ) : (
            <>
              <span className="mr-2">📚</span>
              Pubblica Gioco
            </>
          )}
        </Button>
      </div>

      {/* Confirmation Modal */}
      <PublishConfirmModal
        isOpen={showConfirmModal}
        onClose={() => setShowConfirmModal(false)}
        onConfirm={handlePublish}
        gameName={gameName}
        pdfFileName={pdfFileName}
        approvalStatus={approvalStatus}
      />
    </div>
  );
}
