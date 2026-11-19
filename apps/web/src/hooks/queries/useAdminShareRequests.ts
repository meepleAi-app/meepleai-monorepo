import { useMutation, useQuery, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';
import type {
  ShareRequestDetailsDto,
  PaginatedAdminShareRequestsResponse,
  GetAdminShareRequestsParams,
  ApproveRequestData,
  ApproveGameProposalData,
  RejectRequestData,
  RequestChangesData,
  ActiveReviewDto,
} from '@/lib/api/schemas/admin-share-requests.schemas';

/**
 * Admin Share Requests Query Hooks
 *
 * React Query hooks for admin review and management of user share requests.
 *
 * Features:
 * - Paginated list with filters and sorting
 * - Detailed request view with full context
 * - Review lock management (start/release)
 * - Approval, rejection, and change requests
 * - Active reviews tracking
 * - Optimistic UI updates
 * - Automatic cache invalidation
 *
 * Issue #2745: Frontend - Admin Review Interface
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 */

/**
 * Query key factory for admin share requests
 *
 * Provides hierarchical cache keys for efficient invalidation and refetching.
 */
export const adminShareRequestsKeys = {
  all: ['admin', 'shareRequests'] as const,
  lists: () => [...adminShareRequestsKeys.all, 'list'] as const,
  list: (params?: GetAdminShareRequestsParams) =>
    [...adminShareRequestsKeys.lists(), { params }] as const,
  details: () => [...adminShareRequestsKeys.all, 'detail'] as const,
  detail: (id: string) => [...adminShareRequestsKeys.details(), id] as const,
  myReviews: () => [...adminShareRequestsKeys.all, 'myReviews'] as const,
};

/**
 * #5076: the HTTP status of a failed request. The client throws ApiError, whose status is `statusCode`; these
 * hooks read `error.status`, which it does not have, so their 409 and 403 messages never showed.
 */
function statusOf(error: Error): number | undefined {
  return error instanceof ApiError ? error.statusCode : undefined;
}

/**
 * Get paginated list of share requests with filters.
 *
 * Features:
 * - Server-side pagination
 * - Status and contribution type filters
 * - Search by game title or contributor name
 * - Sorting by various fields
 * - Lock status indicators
 *
 * @param params - Query parameters (page, pageSize, filters, sort)
 * @param options - React Query options
 *
 * @example
 * ```typescript
 * const { data, isLoading } = useAdminShareRequests({
 *   page: 1,
 *   pageSize: 20,
 *   status: 'Pending',
 *   sortBy: 'CreatedAt',
 *   sortDirection: 'Descending',
 * });
 * ```
 */
export function useAdminShareRequests(
  params?: GetAdminShareRequestsParams,
  options?: Omit<UseQueryOptions<PaginatedAdminShareRequestsResponse>, 'queryKey' | 'queryFn'>
) {
  return useQuery({
    queryKey: adminShareRequestsKeys.list(params),
    queryFn: () => api.adminShareRequests.getAll(params),
    staleTime: 30_000, // 30 seconds - admin data changes frequently
    ...options,
  });
}

/**
 * Get detailed information about a specific share request.
 *
 * Includes:
 * - Full game details with all metadata
 * - Contributor profile with badges and stats
 * - Attached documents with previews
 * - Complete history timeline
 * - Current lock status
 *
 * @param id - Share request ID
 * @param options - React Query options
 *
 * @example
 * ```typescript
 * const { data: request, isLoading } = useShareRequestDetails('uuid');
 * ```
 */
export function useShareRequestDetails(
  id: string,
  options?: Omit<UseQueryOptions<ShareRequestDetailsDto>, 'queryKey' | 'queryFn'>
) {
  return useQuery({
    queryKey: adminShareRequestsKeys.detail(id),
    queryFn: () => api.adminShareRequests.getById(id),
    staleTime: 30_000, // 30 seconds
    enabled: !!id && (options?.enabled ?? true),
    ...options,
  });
}

/**
 * Get list of share requests currently being reviewed by the admin.
 *
 * Useful for:
 * - "My Reviews" dashboard
 * - Lock expiration warnings
 * - Active review tracking
 *
 * @param options - React Query options
 *
 * @example
 * ```typescript
 * const { data: myReviews } = useMyReviews();
 * ```
 */
export function useMyReviews(
  options?: Omit<UseQueryOptions<ActiveReviewDto[]>, 'queryKey' | 'queryFn'>
) {
  return useQuery({
    queryKey: adminShareRequestsKeys.myReviews(),
    queryFn: () => api.adminShareRequests.getMyReviews(),
    staleTime: 10_000, // 10 seconds - frequently changing data
    refetchInterval: 30_000, // Auto-refresh every 30 seconds to track lock expiration
    ...options,
  });
}

/**
 * Acquire exclusive review lock on a share request.
 *
 * Features:
 * - Returns full request details immediately
 * - Lock duration: 30 minutes (configurable server-side)
 * - Prevents other admins from reviewing simultaneously
 * - Automatic cache invalidation on success
 *
 * Throws:
 * - 409 Conflict if already locked by another admin
 * - 403 Forbidden if not authorized
 *
 * @example
 * ```typescript
 * const { mutate: startReview, isPending } = useStartReview();
 *
 * const handleStartReview = (id: string) => {
 *   startReview({ shareRequestId: id }, {
 *     onSuccess: (response) => {
 *       navigate(`/admin/share-requests/${id}`);
 *     },
 *   });
 * };
 * ```
 */
export function useStartReview() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ shareRequestId }: { shareRequestId: string }) =>
      api.adminShareRequests.startReview(shareRequestId),
    onSuccess: (response, variables) => {
      // Invalidate list to update lock status
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.lists() });

      // Update detail cache with fresh data from response
      queryClient.setQueryData(
        adminShareRequestsKeys.detail(variables.shareRequestId),
        response.requestDetails
      );

      // Invalidate my reviews
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.myReviews() });

      toast.success('Revisione avviata: hai 30 minuti per completarla.');
    },
    onError: (error: Error) => {
      if (statusOf(error) === 409) {
        toast.error('Un altro admin sta già rivedendo questa richiesta.');
      } else if (statusOf(error) === 403) {
        toast.error('Non hai i permessi per rivedere questa richiesta.');
      } else {
        toast.error('Non è stato possibile avviare la revisione. Riprova.');
      }
    },
  });
}

/**
 * Release review lock on a share request.
 *
 * Allows other admins to review the request.
 * Use when admin wants to pause or abandon review.
 *
 * @example
 * ```typescript
 * const { mutate: releaseReview } = useReleaseReview();
 *
 * const handleRelease = (id: string) => {
 *   releaseReview({ shareRequestId: id }, {
 *     onSuccess: () => navigate('/admin/share-requests'),
 *   });
 * };
 * ```
 */
export function useReleaseReview() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ shareRequestId }: { shareRequestId: string }) =>
      api.adminShareRequests.releaseReview(shareRequestId),
    onSuccess: (_, variables) => {
      // Invalidate all related queries
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.lists() });
      queryClient.invalidateQueries({
        queryKey: adminShareRequestsKeys.detail(variables.shareRequestId),
      });
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.myReviews() });

      toast.success('Revisione rilasciata: altri admin possono riprenderla.');
    },
    onError: () => {
      toast.error('Non è stato possibile rilasciare la revisione. Riprova.');
    },
  });
}

/**
 * Approve a share request, with optional admin notes.
 *
 * On success the request status changes to Approved. It refuses a NewGameProposal and a CoverChange (409):
 * approve those with {@link useApproveGameProposal}. No notification or badge follows today: ShareRequest's
 * domain events reach no handler (#5078).
 *
 * @example
 * ```typescript
 * const { mutate: approve, isPending } = useApproveRequest();
 *
 * const handleApprove = () => {
 *   approve({
 *     shareRequestId: id,
 *     adminNotes: 'Great contribution!',
 *   });
 * };
 * ```
 */
export function useApproveRequest() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ shareRequestId, ...data }: { shareRequestId: string } & ApproveRequestData) =>
      api.adminShareRequests.approve(shareRequestId, data),
    onSuccess: (_, variables) => {
      // Invalidate all related queries
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.lists() });
      queryClient.invalidateQueries({
        queryKey: adminShareRequestsKeys.detail(variables.shareRequestId),
      });
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.myReviews() });

      toast.success('Richiesta approvata.');
    },
    onError: (error: Error) => {
      if (statusOf(error) === 409) {
        toast.error('Non approvabile: la revisione non è tua, è scaduta, o lo stato è cambiato.');
      } else if (statusOf(error) === 403) {
        toast.error('Non hai i permessi per approvare questa richiesta.');
      } else {
        toast.error('Non è stato possibile approvare la richiesta. Riprova.');
      }
    },
  });
}

/**
 * #5076: approve a NewGameProposal or a CoverChange through approve-game-proposal.
 *
 * - ApproveAsNew creates a shared game from the proposed private game
 * - ApproveAsVariant creates it as a variant of `targetSharedGameId`
 * - MergeKnowledgeBase adds the documents to `targetSharedGameId`, creating no game
 * - UpdateCover promotes a CoverChange's pending cover onto its game
 */
export function useApproveGameProposal() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      shareRequestId,
      ...data
    }: { shareRequestId: string } & ApproveGameProposalData) =>
      api.adminShareRequests.approveGameProposal(shareRequestId, data),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.lists() });
      queryClient.invalidateQueries({
        queryKey: adminShareRequestsKeys.detail(variables.shareRequestId),
      });
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.myReviews() });

      toast.success('Proposta approvata.');
    },
    onError: (error: Error) => {
      const status = statusOf(error);
      if (status === 409) {
        toast.error('Non approvabile: la revisione non è tua, è scaduta, o lo stato è cambiato.');
      } else if (status === 404) {
        toast.error('Il gioco proposto o il gioco di destinazione non esiste più.');
      } else if (status === 403) {
        toast.error('Non hai i permessi per approvare questa proposta.');
      } else {
        toast.error('Non è stato possibile approvare la proposta. Riprova.');
      }
    },
  });
}

/**
 * Reject a share request with a required reason.
 *
 * Required:
 * - Rejection reason (min 10 characters)
 *
 * On success the request status changes to Rejected and the review lock is released. No notification
 * follows today (#5078).
 *
 * @example
 * ```typescript
 * const { mutate: reject, isPending } = useRejectRequest();
 *
 * const handleReject = () => {
 *   reject({
 *     shareRequestId: id,
 *     reason: 'Quality issues with rulebook scan.',
 *   });
 * };
 * ```
 */
export function useRejectRequest() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ shareRequestId, ...data }: { shareRequestId: string } & RejectRequestData) =>
      api.adminShareRequests.reject(shareRequestId, data),
    onSuccess: (_, variables) => {
      // Invalidate all related queries
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.lists() });
      queryClient.invalidateQueries({
        queryKey: adminShareRequestsKeys.detail(variables.shareRequestId),
      });
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.myReviews() });

      toast.success('Richiesta rifiutata.');
    },
    onError: (error: Error) => {
      if (statusOf(error) === 409) {
        toast.error('Non rifiutabile: la revisione non è tua, è scaduta, o lo stato è cambiato.');
      } else if (statusOf(error) === 403) {
        toast.error('Non hai i permessi per rifiutare questa richiesta.');
      } else {
        toast.error('Non è stato possibile rifiutare la richiesta. Riprova.');
      }
    },
  });
}

/**
 * Request changes from the user before approval.
 *
 * Required:
 * - Detailed feedback for user (min 10 characters)
 *
 * On success the request status changes to ChangesRequested and the review lock is released; the user can
 * resubmit after making changes. No notification follows today (#5078).
 *
 * @example
 * ```typescript
 * const { mutate: requestChanges, isPending } = useRequestChanges();
 *
 * const handleRequestChanges = () => {
 *   requestChanges({
 *     shareRequestId: id,
 *     feedback: 'Please provide higher quality scans of pages 5-10.',
 *   });
 * };
 * ```
 */
export function useRequestChanges() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ shareRequestId, ...data }: { shareRequestId: string } & RequestChangesData) =>
      api.adminShareRequests.requestChanges(shareRequestId, data),
    onSuccess: (_, variables) => {
      // Invalidate all related queries
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.lists() });
      queryClient.invalidateQueries({
        queryKey: adminShareRequestsKeys.detail(variables.shareRequestId),
      });
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.myReviews() });

      toast.success('Modifiche richieste.');
    },
    onError: (error: Error) => {
      if (statusOf(error) === 409) {
        toast.error(
          'Modifiche non richiedibili: la revisione non è tua, è scaduta, o lo stato è cambiato.'
        );
      } else if (statusOf(error) === 403) {
        toast.error('Non hai i permessi per chiedere modifiche a questa richiesta.');
      } else {
        toast.error('Non è stato possibile chiedere le modifiche. Riprova.');
      }
    },
  });
}
