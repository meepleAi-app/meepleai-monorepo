import type {
  ContributionType,
  ShareRequestStatus,
} from '@/lib/api/schemas/admin-share-requests.schemas';

/**
 * Admin share-request review (#5076): labels and the approval each contribution type takes.
 */

export const STATUS_LABELS: Record<ShareRequestStatus, string> = {
  Pending: 'In attesa',
  InReview: 'In revisione',
  ChangesRequested: 'Modifiche richieste',
  Approved: 'Approvata',
  Rejected: 'Rifiutata',
  Withdrawn: 'Ritirata',
};

export const CONTRIBUTION_TYPE_LABELS: Record<ContributionType, string> = {
  NewGame: 'Nuovo gioco',
  AdditionalContent: 'Contenuti aggiuntivi',
  NewGameProposal: 'Proposta di gioco privato',
  CoverChange: 'Cambio copertina',
};

/** A request the admin can no longer act on. */
export function isClosed(status: ShareRequestStatus): boolean {
  return status === 'Approved' || status === 'Rejected' || status === 'Withdrawn';
}

/**
 * How a contribution type is approved. The generic approve refuses a NewGameProposal and a CoverChange with 409:
 * a proposal goes through approve-game-proposal with one of three actions, a cover change with UpdateCover.
 */
export type ApprovalKind = 'proposal' | 'cover' | 'generic';

export function approvalKindOf(type: ContributionType): ApprovalKind {
  if (type === 'NewGameProposal') return 'proposal';
  if (type === 'CoverChange') return 'cover';
  return 'generic';
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('it-IT', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
