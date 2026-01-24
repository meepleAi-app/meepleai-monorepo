import { useMutation, useQuery, useQueryClient, type UseQueryOptions } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type {
  AdminShareRequestDto,
  ShareRequestDetailsDto,
  PaginatedAdminShareRequestsResponse,
  GetAdminShareRequestsParams,
  ApproveRequestData,
  RejectRequestData,
  RequestChangesData,
  StartReviewResponse,
  ActiveReviewDto,
} from '@/lib/api/schemas/admin-share-requests.schemas';
import { toast } from 'sonner';

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
  options?: Omit<
    UseQueryOptions<PaginatedAdminShareRequestsResponse>,
    'queryKey' | 'queryFn'
  >
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

      toast.success('Review started. You have 30 minutes to complete the review.');
    },
    onError: (error: any) => {
      if (error.status === 409) {
        toast.error('This request is already being reviewed by another admin.');
      } else if (error.status === 403) {
        toast.error('You are not authorized to review this request.');
      } else {
        toast.error('Failed to start review. Please try again.');
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

      toast.success('Review released. Other admins can now review this request.');
    },
    onError: () => {
      toast.error('Failed to release review. Please try again.');
    },
  });
}

/**
 * Approve a share request and optionally modify game details.
 *
 * Optional modifications:
 * - Override game title
 * - Override game description
 * - Select specific documents to include
 * - Add internal admin notes
 *
 * On success:
 * - Request status changes to Approved
 * - Game is added/updated in shared catalog
 * - Contributor receives approval notification
 * - Badge progress may be updated
 *
 * @example
 * ```typescript
 * const { mutate: approve, isPending } = useApproveRequest();
 *
 * const handleApprove = () => {
 *   approve({
 *     shareRequestId: id,
 *     modifiedTitle: editedTitle,
 *     modifiedDescription: editedDescription,
 *     documentsToInclude: selectedDocIds,
 *     adminNotes: 'Great contribution!',
 *   });
 * };
 * ```
 */
export function useApproveRequest() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      shareRequestId,
      ...data
    }: { shareRequestId: string } & ApproveRequestData) =>
      api.adminShareRequests.approve(shareRequestId, data),
    onSuccess: (_, variables) => {
      // Invalidate all related queries
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.lists() });
      queryClient.invalidateQueries({
        queryKey: adminShareRequestsKeys.detail(variables.shareRequestId),
      });
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.myReviews() });

      toast.success('Share request approved! Contributor will be notified.');
    },
    onError: (error: any) => {
      if (error.status === 409) {
        toast.error('Cannot approve: request is locked by another admin or status changed.');
      } else if (error.status === 403) {
        toast.error('You are not authorized to approve this request.');
      } else {
        toast.error('Failed to approve request. Please try again.');
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
 * On success:
 * - Request status changes to Rejected
 * - Contributor receives rejection notification with reason
 * - Review lock is automatically released
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

      toast.success('Share request rejected. Contributor will be notified.');
    },
    onError: (error: any) => {
      if (error.status === 409) {
        toast.error('Cannot reject: request is locked by another admin or status changed.');
      } else if (error.status === 403) {
        toast.error('You are not authorized to reject this request.');
      } else {
        toast.error('Failed to reject request. Please try again.');
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
 * On success:
 * - Request status changes to ChangesRequested
 * - Contributor receives notification with feedback
 * - Review lock is automatically released
 * - User can resubmit after making changes
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
    mutationFn: ({
      shareRequestId,
      ...data
    }: { shareRequestId: string } & RequestChangesData) =>
      api.adminShareRequests.requestChanges(shareRequestId, data),
    onSuccess: (_, variables) => {
      // Invalidate all related queries
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.lists() });
      queryClient.invalidateQueries({
        queryKey: adminShareRequestsKeys.detail(variables.shareRequestId),
      });
      queryClient.invalidateQueries({ queryKey: adminShareRequestsKeys.myReviews() });

      toast.success('Changes requested. Contributor will be notified.');
    },
    onError: (error: any) => {
      if (error.status === 409) {
        toast.error(
          'Cannot request changes: request is locked by another admin or status changed.'
        );
      } else if (error.status === 403) {
        toast.error('You are not authorized to request changes for this request.');
      } else {
        toast.error('Failed to request changes. Please try again.');
      }
    },
  });
}
