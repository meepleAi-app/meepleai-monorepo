'use client';

/**
 * #5076: what the admin can do with a share request. Every decision needs the review lock: start the review, then
 * approve (the way the contribution type takes), reject, or ask for changes; or release it.
 */

import { useEffect, useState, type JSX } from 'react';

import { useQueryClient } from '@tanstack/react-query';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  adminShareRequestsKeys,
  useApproveGameProposal,
  useApproveRequest,
  useRejectRequest,
  useReleaseReview,
  useRequestChanges,
  useStartReview,
} from '@/hooks/queries/useAdminShareRequests';
import type { ShareRequestDetailsDto } from '@/lib/api/schemas/admin-share-requests.schemas';

import { ProposalApprovalForm } from './ProposalApprovalForm';
import { ReasonForm } from './ReasonForm';
import { approvalKindOf, formatDateTime, isClosed } from './share-request-labels';

/**
 * True from the moment `iso` passes. The backend reports the lock as held until the release job runs (every few
 * minutes), and refuses every decision on an expired one: the panel stops offering them on time.
 */
function useExpired(iso: string | null): boolean {
  const deadline = iso ? Date.parse(iso) : null;
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (deadline === null || deadline <= now) return;
    const timer = setTimeout(() => setNow(Date.now()), Math.min(deadline - now + 250, 2 ** 31 - 1));
    return () => clearTimeout(timer);
  }, [deadline, now]);

  return deadline !== null && deadline <= now;
}

function GenericApproval({
  shareRequestId,
  documentCount,
}: {
  shareRequestId: string;
  documentCount: number;
}): JSX.Element {
  const [notes, setNotes] = useState('');
  const { mutate, isPending } = useApproveRequest();

  // Decision of 2026-11-19: approval cannot copy attached documents to the catalog yet, and the backend refuses it
  // (409). Approved, the request would close with its documents left out.
  if (documentCount > 0) {
    return (
      <p className="text-sm text-muted-foreground" data-testid="approve-blocked-by-documents">
        Ha {documentCount === 1 ? 'un documento allegato' : `${documentCount} documenti allegati`}:
        l&apos;approvazione non sa ancora copiarli nel catalogo, quindi per ora non si può
        approvare. Puoi chiedere modifiche o rifiutare.
      </p>
    );
  }

  return (
    <div className="space-y-2">
      <Label htmlFor="generic-admin-notes">Note dell&apos;admin (facoltative)</Label>
      <Textarea
        id="generic-admin-notes"
        value={notes}
        onChange={e => setNotes(e.target.value)}
        maxLength={2000}
        rows={3}
      />
      <Button
        onClick={() => mutate({ shareRequestId, adminNotes: notes.trim() || undefined })}
        disabled={isPending}
      >
        Approva
      </Button>
    </div>
  );
}

function CoverApproval({ shareRequestId }: { shareRequestId: string }): JSX.Element {
  const { mutate, isPending } = useApproveGameProposal();

  return (
    <div className="space-y-2">
      <p className="text-sm text-muted-foreground">
        La copertina proposta diventa la copertina del gioco del catalogo.
      </p>
      <Button
        onClick={() =>
          mutate({ shareRequestId, approvalAction: 'UpdateCover', targetSharedGameId: null })
        }
        disabled={isPending}
      >
        Approva la copertina
      </Button>
    </div>
  );
}

export function ReviewPanel({ request }: { request: ShareRequestDetailsDto }): JSX.Element {
  const queryClient = useQueryClient();
  const startReview = useStartReview();
  const releaseReview = useReleaseReview();
  const reject = useRejectRequest();
  const requestChanges = useRequestChanges();
  const { lockStatus, status, id } = request;
  const lockExpired = useExpired(lockStatus.isLocked ? lockStatus.lockExpiresAt : null);

  if (isClosed(status)) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="font-quicksand text-base">Revisione</CardTitle>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          Richiesta chiusa il {formatDateTime(request.resolvedAt)}.
        </CardContent>
      </Card>
    );
  }

  if (lockStatus.isLocked && lockExpired) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="font-quicksand text-base">Revisione</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <p className="text-muted-foreground" data-testid="review-expired">
            La revisione
            {lockStatus.isLockedByCurrentAdmin
              ? ''
              : ` di ${lockStatus.lockedByAdminName ?? 'un altro admin'}`}{' '}
            è scaduta il {formatDateTime(lockStatus.lockExpiresAt)}. Si potrà riprendere quando il
            blocco viene rilasciato, entro pochi minuti.
          </p>
          <Button
            variant="outline"
            onClick={() =>
              queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.detail(id) })
            }
          >
            Aggiorna
          </Button>
        </CardContent>
      </Card>
    );
  }

  if (lockStatus.isLocked && !lockStatus.isLockedByCurrentAdmin) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="font-quicksand text-base">Revisione</CardTitle>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          In revisione da {lockStatus.lockedByAdminName ?? 'un altro admin'} fino a{' '}
          {formatDateTime(lockStatus.lockExpiresAt)}.
        </CardContent>
      </Card>
    );
  }

  if (!lockStatus.isLockedByCurrentAdmin) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="font-quicksand text-base">Revisione</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          <p className="text-muted-foreground">
            Per decidere serve la revisione: la richiesta resta tua per 30 minuti.
          </p>
          <Button
            onClick={() => startReview.mutate({ shareRequestId: id })}
            disabled={startReview.isPending}
          >
            Avvia la revisione
          </Button>
        </CardContent>
      </Card>
    );
  }

  const kind = approvalKindOf(request.contributionType);

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-2 space-y-0">
        <CardTitle className="font-quicksand text-base">Revisione</CardTitle>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => releaseReview.mutate({ shareRequestId: id })}
          disabled={releaseReview.isPending}
        >
          Rilascia
        </Button>
      </CardHeader>
      <CardContent className="space-y-6">
        <p className="text-sm text-muted-foreground">
          La revisione è tua fino a {formatDateTime(lockStatus.lockExpiresAt)}.
        </p>

        <section className="space-y-3" aria-labelledby="review-approve">
          <h3 id="review-approve" className="text-sm font-semibold">
            Approva
          </h3>
          {kind === 'proposal' ? (
            <ProposalApprovalForm shareRequestId={id} />
          ) : kind === 'cover' ? (
            <CoverApproval shareRequestId={id} />
          ) : (
            <GenericApproval shareRequestId={id} documentCount={request.attachedDocuments.length} />
          )}
        </section>

        <section className="space-y-3 border-t border-border pt-4" aria-labelledby="review-changes">
          <h3 id="review-changes" className="text-sm font-semibold">
            Chiedi modifiche
          </h3>
          <ReasonForm
            id="request-changes-feedback"
            label="Cosa deve cambiare"
            submitLabel="Chiedi modifiche"
            isPending={requestChanges.isPending}
            onSubmit={feedback => requestChanges.mutate({ shareRequestId: id, feedback })}
          />
        </section>

        <section className="space-y-3 border-t border-border pt-4" aria-labelledby="review-reject">
          <h3 id="review-reject" className="text-sm font-semibold">
            Rifiuta
          </h3>
          <ReasonForm
            id="reject-reason"
            label="Motivo del rifiuto"
            submitLabel="Rifiuta"
            destructive
            isPending={reject.isPending}
            onSubmit={reason => reject.mutate({ shareRequestId: id, reason })}
          />
        </section>
      </CardContent>
    </Card>
  );
}
