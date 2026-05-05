'use client';

/**
 * Claims viewer for the M1.2 admin Mechanic Extractor page (ISSUE-584).
 *
 * Endpoints consumed (admin-only):
 *   GET  /admin/mechanic-analyses/{id}/claims                         → list
 *   POST /admin/mechanic-analyses/{id}/claims/{claimId}/approve       → single
 *   POST /admin/mechanic-analyses/{id}/claims/{claimId}/reject        → single + note
 *   POST /admin/mechanic-analyses/{id}/claims/bulk-approve            → batch
 *
 * Lifecycle invariant (AC-10): the parent analysis can only be promoted to
 * Published once **every** claim is Approved. The viewer surfaces totals so
 * reviewers know what blocks the Approve action on the parent.
 */

import React, { useMemo, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CheckIcon, ChevronDownIcon, ChevronRightIcon, Loader2Icon, XIcon } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import {
  MECHANIC_CLAIM_STATUS_LABELS,
  MECHANIC_SECTION_LABELS,
  MechanicClaimStatus,
  type MechanicClaimDto,
} from '@/lib/api/schemas/mechanic-analyses.schemas';

import { RejectClaimDialog } from './RejectClaimDialog';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

const CLAIM_STATUS_BADGE_CLASS: Record<number, string> = {
  [MechanicClaimStatus.Pending]: 'bg-amber-100 text-amber-800 border-amber-300',
  [MechanicClaimStatus.Approved]: 'bg-green-100 text-green-800 border-green-300',
  [MechanicClaimStatus.Rejected]: 'bg-rose-100 text-rose-800 border-rose-300',
};

interface ClaimsSectionProps {
  analysisId: string;
  /**
   * When false, the section is rendered as a small note instead of the table.
   * Used for terminal states where claims are still useful read-only context.
   */
  isClaimsActionable?: boolean;
}

export function ClaimsSection({
  analysisId,
  isClaimsActionable = true,
}: ClaimsSectionProps): React.JSX.Element {
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState<string | null>(null);
  // Separate channel for success-with-skipped (amber) so we don't conflate it
  // with hard failures (rose) — spec-panel P1 fix.
  const [actionWarning, setActionWarning] = useState<string | null>(null);
  const [rejectTarget, setRejectTarget] = useState<MechanicClaimDto | null>(null);
  const [pendingClaimId, setPendingClaimId] = useState<string | null>(null);

  const claimsQuery = useQuery({
    queryKey: ['mechanic-analysis', analysisId, 'claims'],
    queryFn: () => adminClient.getMechanicAnalysisClaims(analysisId),
    enabled: !!analysisId,
    staleTime: 5_000,
  });

  const claims = useMemo<MechanicClaimDto[]>(() => claimsQuery.data ?? [], [claimsQuery.data]);

  const stats = useMemo(() => {
    let pending = 0;
    let approved = 0;
    let rejected = 0;
    for (const c of claims) {
      if (c.status === MechanicClaimStatus.Pending) pending++;
      else if (c.status === MechanicClaimStatus.Approved) approved++;
      else if (c.status === MechanicClaimStatus.Rejected) rejected++;
    }
    return { pending, approved, rejected, total: claims.length };
  }, [claims]);

  const grouped = useMemo(() => {
    const map = new Map<number, MechanicClaimDto[]>();
    for (const c of claims) {
      const key = Number(c.section);
      const arr = map.get(key) ?? [];
      arr.push(c);
      map.set(key, arr);
    }
    // Stable section ordering by enum number; claims by displayOrder within.
    const orderedKeys = Array.from(map.keys()).sort((a, b) => a - b);
    return orderedKeys.map(section => ({
      section,
      claims: (map.get(section) ?? []).slice().sort((a, b) => a.displayOrder - b.displayOrder),
    }));
  }, [claims]);

  const invalidateClaimsAndStatus = () => {
    queryClient.invalidateQueries({ queryKey: ['mechanic-analysis', analysisId, 'claims'] });
    // Status carries `claimsCount`; refetch in case approval flipped section run summaries.
    queryClient.invalidateQueries({ queryKey: ['mechanic-analysis', analysisId] });
  };

  const approveMutation = useMutation({
    mutationFn: (claimId: string) => adminClient.approveMechanicClaim(analysisId, claimId),
    onMutate: claimId => setPendingClaimId(claimId),
    onSuccess: () => {
      setActionError(null);
      setActionWarning(null);
      invalidateClaimsAndStatus();
    },
    onError: (err: unknown) => {
      setActionError(err instanceof Error ? err.message : 'Failed to approve claim');
    },
    onSettled: () => setPendingClaimId(null),
  });

  const rejectMutation = useMutation({
    mutationFn: ({ claimId, note }: { claimId: string; note: string }) =>
      adminClient.rejectMechanicClaim(analysisId, claimId, { note }),
    onMutate: ({ claimId }) => setPendingClaimId(claimId),
    onSuccess: () => {
      setActionError(null);
      setActionWarning(null);
      setRejectTarget(null);
      invalidateClaimsAndStatus();
    },
    onError: (err: unknown) => {
      setActionError(err instanceof Error ? err.message : 'Failed to reject claim');
    },
    onSettled: () => setPendingClaimId(null),
  });

  const bulkApproveMutation = useMutation({
    mutationFn: () => adminClient.bulkApproveMechanicClaims(analysisId),
    onSuccess: result => {
      setActionError(null);
      // Partial success: report skipped-rejected as a warning (amber), not error (rose).
      if (result.skippedRejectedCount > 0) {
        setActionWarning(
          `Bulk approve completed: ${result.approvedCount} approved, ` +
            `${result.skippedRejectedCount} rejected claim(s) skipped — re-approve them ` +
            `individually to clear the lifecycle block.`
        );
      } else {
        setActionWarning(null);
      }
      invalidateClaimsAndStatus();
    },
    onError: (err: unknown) => {
      setActionError(err instanceof Error ? err.message : 'Bulk approve failed');
    },
  });

  if (claimsQuery.isLoading) {
    return (
      <div
        className="rounded-md border border-slate-200 p-3 text-sm text-muted-foreground dark:border-zinc-700"
        data-testid="claims-loading"
      >
        <Loader2Icon className="mr-2 inline h-4 w-4 animate-spin" />
        Loading claims…
      </div>
    );
  }

  if (claimsQuery.isError) {
    return (
      <div
        className="rounded-md border border-rose-300 bg-rose-50 p-3 text-sm text-rose-800"
        data-testid="claims-load-error"
      >
        Failed to load claims:{' '}
        {claimsQuery.error instanceof Error ? claimsQuery.error.message : 'unknown error'}
      </div>
    );
  }

  if (claims.length === 0) {
    // Parent gates this section on `status.claimsCount > 0`, so an empty list
    // here is a transient state (status cached, claims not yet refetched) —
    // neutral wording avoids implying "extraction still running".
    return (
      <div
        className="rounded-md border border-slate-200 bg-slate-50 p-3 text-sm text-muted-foreground dark:border-zinc-700 dark:bg-zinc-900"
        data-testid="claims-empty"
      >
        No claims to display.
      </div>
    );
  }

  const canBulkApprove = isClaimsActionable && stats.pending > 0 && !bulkApproveMutation.isPending;

  return (
    <div className="space-y-3" data-testid="claims-section">
      {/* Header / stats */}
      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-slate-200 pt-3 dark:border-zinc-700">
        <div>
          <h3 className="text-sm font-medium">Claims ({stats.total})</h3>
          <p className="text-xs text-muted-foreground">
            <span className="text-amber-700">Pending: {stats.pending}</span>
            {' · '}
            <span className="text-green-700">Approved: {stats.approved}</span>
            {' · '}
            <span className="text-rose-700">Rejected: {stats.rejected}</span>
          </p>
          {stats.rejected > 0 && (
            <p className="mt-1 text-xs text-rose-700" data-testid="claims-ac10-warning">
              ⚠ {stats.rejected} rejected claim(s) block analysis approval (AC-10). Re-approve them
              to clear the block.
            </p>
          )}
        </div>
        {isClaimsActionable && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => bulkApproveMutation.mutate()}
            disabled={!canBulkApprove}
            data-testid="bulk-approve-button"
          >
            {bulkApproveMutation.isPending ? (
              <Loader2Icon className="mr-1 h-4 w-4 animate-spin" />
            ) : (
              <CheckIcon className="mr-1 h-4 w-4" />
            )}
            Bulk approve pending ({stats.pending})
          </Button>
        )}
      </div>

      {actionError && (
        <div
          className="rounded-md border border-rose-300 bg-rose-50 p-2 text-xs text-rose-800"
          data-testid="claims-action-error"
        >
          {actionError}
        </div>
      )}

      {actionWarning && (
        <div
          className="rounded-md border border-amber-300 bg-amber-50 p-2 text-xs text-amber-900"
          data-testid="claims-action-warning"
        >
          {actionWarning}
        </div>
      )}

      {/* Grouped tables */}
      <div className="space-y-4">
        {grouped.map(({ section, claims: sectionClaims }) => (
          <SectionGroup
            key={section}
            section={section}
            claims={sectionClaims}
            isActionable={isClaimsActionable}
            pendingClaimId={pendingClaimId}
            onApprove={id => approveMutation.mutate(id)}
            onReject={claim => setRejectTarget(claim)}
          />
        ))}
      </div>

      <RejectClaimDialog
        open={!!rejectTarget}
        onOpenChange={open => {
          if (!open) setRejectTarget(null);
        }}
        onConfirm={note => {
          if (!rejectTarget) return;
          rejectMutation.mutate({ claimId: rejectTarget.id, note });
        }}
        isPending={rejectMutation.isPending}
        claimPreview={rejectTarget ? truncate(rejectTarget.text, 120) : undefined}
      />
    </div>
  );
}

interface SectionGroupProps {
  section: number;
  claims: MechanicClaimDto[];
  isActionable: boolean;
  pendingClaimId: string | null;
  onApprove: (claimId: string) => void;
  onReject: (claim: MechanicClaimDto) => void;
}

function SectionGroup({
  section,
  claims,
  isActionable,
  pendingClaimId,
  onApprove,
  onReject,
}: SectionGroupProps): React.JSX.Element {
  const [expanded, setExpanded] = useState(true);
  const label = MECHANIC_SECTION_LABELS[section] ?? `Section ${section}`;
  const pendingCount = claims.filter(c => c.status === MechanicClaimStatus.Pending).length;

  return (
    <div className="rounded-md border border-slate-200 dark:border-zinc-700">
      <button
        type="button"
        onClick={() => setExpanded(v => !v)}
        className="flex w-full items-center justify-between gap-2 bg-slate-50 px-3 py-2 text-left text-sm dark:bg-zinc-900"
        data-testid={`claims-section-header-${section}`}
      >
        <span className="flex items-center gap-2 font-medium">
          {expanded ? (
            <ChevronDownIcon className="h-4 w-4" />
          ) : (
            <ChevronRightIcon className="h-4 w-4" />
          )}
          {label} <span className="text-muted-foreground">({claims.length})</span>
        </span>
        {pendingCount > 0 && (
          <Badge variant="outline" className="border-amber-300 bg-amber-50 text-amber-800">
            {pendingCount} pending
          </Badge>
        )}
      </button>
      {expanded && (
        <ul className="divide-y divide-slate-200 dark:divide-zinc-700">
          {claims.map(claim => (
            <ClaimRow
              key={claim.id}
              claim={claim}
              isActionable={isActionable}
              isPending={pendingClaimId === claim.id}
              onApprove={() => onApprove(claim.id)}
              onReject={() => onReject(claim)}
            />
          ))}
        </ul>
      )}
    </div>
  );
}

interface ClaimRowProps {
  claim: MechanicClaimDto;
  isActionable: boolean;
  isPending: boolean;
  onApprove: () => void;
  onReject: () => void;
}

/**
 * Threshold above which a claim's text is considered "long" and gets clamped
 * to 3 lines with a Show more/Show less toggle (spec-panel gap #6 — keeps
 * action buttons close to the row content even when extractors return a
 * paragraph-length claim sentence).
 */
const LONG_CLAIM_THRESHOLD = 240;

function ClaimRow({
  claim,
  isActionable,
  isPending,
  onApprove,
  onReject,
}: ClaimRowProps): React.JSX.Element {
  const [showCitations, setShowCitations] = useState(false);
  const [textExpanded, setTextExpanded] = useState(false);
  const status = Number(claim.status);
  const canActPerStatus =
    status === MechanicClaimStatus.Pending || status === MechanicClaimStatus.Rejected;
  // Pending → Approve+Reject; Rejected → Approve only (recovers from a wrong reject);
  // Approved → no actions (idempotent re-approve adds noise without value).
  const canApprove = isActionable && canActPerStatus && !isPending;
  const canReject = isActionable && status === MechanicClaimStatus.Pending && !isPending;
  const isLongText = claim.text.length > LONG_CLAIM_THRESHOLD;
  const clampClass = isLongText && !textExpanded ? 'line-clamp-3' : '';

  return (
    <li className="space-y-2 px-3 py-2 text-sm" data-testid={`claim-row-${claim.id}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <p className={`break-words ${clampClass}`.trim()} data-testid={`claim-text-${claim.id}`}>
            {claim.text}
          </p>
          {isLongText && (
            <button
              type="button"
              onClick={() => setTextExpanded(v => !v)}
              className="mt-1 text-xs text-sky-700 hover:underline"
              data-testid={`claim-expand-${claim.id}`}
            >
              {textExpanded ? 'Show less' : 'Show more'}
            </button>
          )}
          {claim.rejectionNote && (
            <p
              className="mt-1 rounded border border-rose-200 bg-rose-50 p-1 text-xs text-rose-800 break-words"
              data-testid={`claim-rejection-note-${claim.id}`}
            >
              <strong>Rejection:</strong> {claim.rejectionNote}
            </p>
          )}
        </div>
        <div className="flex flex-shrink-0 flex-wrap items-center gap-2">
          <Badge
            variant="outline"
            className={CLAIM_STATUS_BADGE_CLASS[status] ?? ''}
            data-testid={`claim-status-${claim.id}`}
          >
            {MECHANIC_CLAIM_STATUS_LABELS[status] ?? String(status)}
          </Badge>
          {canApprove && (
            <Button
              variant="outline"
              size="sm"
              onClick={onApprove}
              disabled={!canApprove}
              className="border-green-300 text-green-700 hover:bg-green-50"
              data-testid={`claim-approve-${claim.id}`}
            >
              {isPending ? (
                <Loader2Icon className="mr-1 h-3 w-3 animate-spin" />
              ) : (
                <CheckIcon className="mr-1 h-3 w-3" />
              )}
              Approve
            </Button>
          )}
          {canReject && (
            <Button
              variant="outline"
              size="sm"
              onClick={onReject}
              disabled={!canReject}
              className="border-rose-300 text-rose-700 hover:bg-rose-50"
              data-testid={`claim-reject-${claim.id}`}
            >
              <XIcon className="mr-1 h-3 w-3" />
              Reject
            </Button>
          )}
        </div>
      </div>
      {claim.citations.length > 0 && (
        <div>
          <button
            type="button"
            onClick={() => setShowCitations(v => !v)}
            className="text-xs text-sky-700 hover:underline"
            data-testid={`claim-citations-toggle-${claim.id}`}
          >
            {showCitations ? 'Hide' : 'Show'} {claim.citations.length} citation
            {claim.citations.length === 1 ? '' : 's'}
          </button>
          {showCitations && (
            <ul className="mt-1 space-y-1 border-l-2 border-sky-200 pl-3 text-xs">
              {claim.citations.map(c => (
                <li key={c.id} className="text-muted-foreground">
                  <span className="font-medium text-slate-700 dark:text-slate-300">
                    p.{c.pdfPage}
                  </span>{' '}
                  — &ldquo;{c.quote}&rdquo;
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </li>
  );
}

function truncate(s: string, max: number): string {
  if (s.length <= max) return s;
  return `${s.slice(0, max - 1).trimEnd()}…`;
}
