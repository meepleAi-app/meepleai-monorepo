/**
 * MSW handlers for admin share requests endpoints (Issue #2745)
 *
 * Covers: /api/v1/admin/share-requests/* routes
 * - List share requests with filters/pagination
 * - Get request details
 * - Start/Release review (lock management)
 * - Approve/Reject/Request changes
 */

import { http, HttpResponse } from 'msw';
import type {
  AdminShareRequestDto,
  ShareRequestDetailsDto,
  PaginatedAdminShareRequestsResponse,
  StartReviewResponse,
  ApproveRequestData,
  RejectRequestData,
  RequestChangesData,
  ShareRequestStatus,
  ContributionType,
} from '@/lib/api/schemas/admin-share-requests.schemas';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Generate mock UUIDs
const generateUUID = () => `${Date.now()}-${Math.random().toString(36).substring(2, 11)}`;

// Mock share requests in-memory store
let shareRequests: AdminShareRequestDto[] = [
  {
    id: 'share-req-1',
    status: 'Pending' as ShareRequestStatus,
    contributionType: 'NewGame' as ContributionType,
    sourceGameId: 'game-1',
    gameTitle: 'Wingspan',
    gameThumbnailUrl: 'https://placehold.co/100x100/png?text=Wingspan',
    bggId: 266192,
    userId: 'user-1',
    userName: 'Sarah Johnson',
    userAvatarUrl: 'https://placehold.co/40x40/png?text=SJ',
    userTotalContributions: 15,
    userNotes: 'Great engine-building game about birds!',
    attachedDocumentCount: 3,
    createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
    isInReview: false,
    reviewingAdminId: null,
    reviewingAdminName: null,
    reviewStartedAt: null,
    targetSharedGameId: null,
    targetSharedGameTitle: null,
  },
  {
    id: 'share-req-2',
    status: 'InReview' as ShareRequestStatus,
    contributionType: 'AdditionalContent' as ContributionType,
    sourceGameId: 'game-2',
    gameTitle: 'Terraforming Mars',
    gameThumbnailUrl: 'https://placehold.co/100x100/png?text=TM',
    bggId: 167791,
    userId: 'user-2',
    userName: 'Mike Chen',
    userAvatarUrl: 'https://placehold.co/40x40/png?text=MC',
    userTotalContributions: 8,
    userNotes: 'Additional FAQ content for advanced strategies',
    attachedDocumentCount: 1,
    createdAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
    isInReview: true,
    reviewingAdminId: 'admin-1',
    reviewingAdminName: 'Admin User',
    reviewStartedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
    targetSharedGameId: 'shared-game-1',
    targetSharedGameTitle: 'Terraforming Mars',
  },
  {
    id: 'share-req-3',
    status: 'Pending' as ShareRequestStatus,
    contributionType: 'NewGame' as ContributionType,
    sourceGameId: 'game-3',
    gameTitle: 'Gloomhaven',
    gameThumbnailUrl: 'https://placehold.co/100x100/png?text=Gloomhaven',
    bggId: 174430,
    userId: 'user-3',
    userName: 'Emma Wilson',
    userAvatarUrl: 'https://placehold.co/40x40/png?text=EW',
    userTotalContributions: 23,
    userNotes: 'Complete campaign tracking system included',
    attachedDocumentCount: 5,
    createdAt: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(),
    isInReview: false,
    reviewingAdminId: null,
    reviewingAdminName: null,
    reviewStartedAt: null,
    targetSharedGameId: null,
    targetSharedGameTitle: null,
  },
];

// Mock detailed request store
const detailedRequests: Record<string, ShareRequestDetailsDto> = {
  'share-req-1': {
    id: 'share-req-1',
    status: 'Pending' as ShareRequestStatus,
    contributionType: 'NewGame' as ContributionType,
    sourceGame: {
      id: 'game-1',
      title: 'Wingspan',
      description: 'A competitive, medium-weight, card-driven, engine-building board game',
      thumbnailUrl: 'https://placehold.co/100x100/png?text=Wingspan',
      bggId: 266192,
      minPlayers: 1,
      maxPlayers: 5,
      playingTime: 70,
      complexity: 2.5,
      categories: ['Animals', 'Card Game'],
      mechanisms: ['Hand Management', 'Set Collection', 'Engine Building'],
    },
    targetSharedGame: null,
    contributor: {
      userId: 'user-1',
      userName: 'Sarah Johnson',
      avatarUrl: 'https://placehold.co/40x40/png?text=SJ',
      joinedAt: new Date(Date.now() - 365 * 24 * 60 * 60 * 1000).toISOString(),
      totalContributions: 15,
      approvedContributions: 12,
      approvalRate: 0.8,
      badges: [
        {
          id: 'badge-1',
          name: 'Contributor',
          iconUrl: null,
          awardedAt: new Date(Date.now() - 180 * 24 * 60 * 60 * 1000).toISOString(),
        },
      ],
    },
    userNotes: 'Great engine-building game about birds!',
    attachedDocuments: [
      {
        documentId: 'doc-1',
        fileName: 'wingspan-rulebook.pdf',
        contentType: 'application/pdf',
        fileSize: 2_500_000,
        previewUrl: null,
        pageCount: 20,
      },
      {
        documentId: 'doc-2',
        fileName: 'wingspan-quick-reference.pdf',
        contentType: 'application/pdf',
        fileSize: 500_000,
        previewUrl: null,
        pageCount: 2,
      },
      {
        documentId: 'doc-3',
        fileName: 'wingspan-faq.pdf',
        contentType: 'application/pdf',
        fileSize: 750_000,
        previewUrl: null,
        pageCount: 5,
      },
    ],
    history: [
      {
        timestamp: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
        action: 'Created',
        actorId: 'user-1',
        actorName: 'Sarah Johnson',
        details: 'Share request created',
      },
    ],
    lockStatus: {
      isLocked: false,
      isLockedByCurrentAdmin: false,
      lockedByAdminId: null,
      lockedByAdminName: null,
      lockExpiresAt: null,
    },
    createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
    resolvedAt: null,
  },
};

// Track lock state separately for easier manipulation
const lockState: Record<
  string,
  {
    isLocked: boolean;
    lockedByAdminId: string | null;
    lockedByAdminName: string | null;
    lockExpiresAt: string | null;
  }
> = {};

export const adminShareRequestsHandlers = [
  // GET /api/v1/admin/share-requests - List with pagination and filters
  http.get(`${API_BASE}/api/v1/admin/share-requests`, ({ request }) => {
    const url = new URL(request.url);

    // Parse query params
    const page = parseInt(url.searchParams.get('page') || '1', 10);
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20', 10);
    const status = url.searchParams.get('status') as ShareRequestStatus | null;
    const contributionType = url.searchParams.get('contributionType') as ContributionType | null;
    const searchTerm = url.searchParams.get('searchTerm') || '';
    const sortBy = url.searchParams.get('sortBy') || 'CreatedAt';
    const sortDirection = url.searchParams.get('sortDirection') || 'Descending';

    // Filter requests
    let filtered = [...shareRequests];

    if (status) {
      filtered = filtered.filter(req => req.status === status);
    }

    if (contributionType) {
      filtered = filtered.filter(req => req.contributionType === contributionType);
    }

    if (searchTerm) {
      const term = searchTerm.toLowerCase();
      filtered = filtered.filter(
        req =>
          req.gameTitle.toLowerCase().includes(term) || req.userName.toLowerCase().includes(term)
      );
    }

    // Sort
    if (sortBy === 'CreatedAt') {
      filtered.sort((a, b) => {
        const dateA = new Date(a.createdAt).getTime();
        const dateB = new Date(b.createdAt).getTime();
        return sortDirection === 'Ascending' ? dateA - dateB : dateB - dateA;
      });
    } else if (sortBy === 'GameTitle') {
      filtered.sort((a, b) => {
        const cmp = a.gameTitle.localeCompare(b.gameTitle);
        return sortDirection === 'Ascending' ? cmp : -cmp;
      });
    }

    // Paginate
    const total = filtered.length;
    const startIndex = (page - 1) * pageSize;
    const endIndex = startIndex + pageSize;
    const items = filtered.slice(startIndex, endIndex);

    const response: PaginatedAdminShareRequestsResponse = {
      items,
      page,
      pageSize,
      total,
    };

    return HttpResponse.json(response, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/admin/share-requests/:id - Get request details
  http.get(`${API_BASE}/api/v1/admin/share-requests/:id`, ({ params }) => {
    const { id } = params;
    const details = detailedRequests[id as string];

    if (!details) {
      return HttpResponse.json({ error: 'Share request not found' }, { status: 404 });
    }

    // Update lock status from separate state
    const lock = lockState[id as string] || {
      isLocked: false,
      lockedByAdminId: null,
      lockedByAdminName: null,
      lockExpiresAt: null,
    };

    const response: ShareRequestDetailsDto = {
      ...details,
      lockStatus: {
        ...lock,
        isLockedByCurrentAdmin: lock.lockedByAdminId === 'admin-1', // Assume current admin is admin-1
      },
    };

    return HttpResponse.json(response, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/admin/share-requests/:id/start-review - Acquire lock
  http.post(`${API_BASE}/api/v1/admin/share-requests/:id/start-review`, ({ params }) => {
    const { id } = params;
    const details = detailedRequests[id as string];

    if (!details) {
      return HttpResponse.json({ error: 'Share request not found' }, { status: 404 });
    }

    // Check if already locked
    const lock = lockState[id as string];
    if (lock?.isLocked) {
      return HttpResponse.json(
        {
          error: 'Share request is already being reviewed',
          lockedBy: lock.lockedByAdminName,
        },
        { status: 409 }
      );
    }

    // Acquire lock (15 min expiry)
    const lockExpiresAt = new Date(Date.now() + 15 * 60 * 1000).toISOString();
    lockState[id as string] = {
      isLocked: true,
      lockedByAdminId: 'admin-1',
      lockedByAdminName: 'Admin User',
      lockExpiresAt,
    };

    // Update request status
    const reqIndex = shareRequests.findIndex(r => r.id === id);
    if (reqIndex !== -1) {
      shareRequests[reqIndex] = {
        ...shareRequests[reqIndex],
        status: 'InReview',
        isInReview: true,
        reviewingAdminId: 'admin-1',
        reviewingAdminName: 'Admin User',
        reviewStartedAt: new Date().toISOString(),
      };
    }

    // Update detailed request
    detailedRequests[id as string] = {
      ...details,
      status: 'InReview',
      lockStatus: {
        isLocked: true,
        isLockedByCurrentAdmin: true,
        lockedByAdminId: 'admin-1',
        lockedByAdminName: 'Admin User',
        lockExpiresAt,
      },
      history: [
        ...details.history,
        {
          timestamp: new Date().toISOString(),
          action: 'ReviewStarted',
          actorId: 'admin-1',
          actorName: 'Admin User',
          details: 'Review started',
        },
      ],
    };

    const response: StartReviewResponse = {
      shareRequestId: id as string,
      lockExpiresAt,
      requestDetails: detailedRequests[id as string],
    };

    return HttpResponse.json(response, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/admin/share-requests/:id/release-review - Release lock
  http.post(`${API_BASE}/api/v1/admin/share-requests/:id/release-review`, ({ params }) => {
    const { id } = params;
    const details = detailedRequests[id as string];

    if (!details) {
      return HttpResponse.json({ error: 'Share request not found' }, { status: 404 });
    }

    // Release lock
    delete lockState[id as string];

    // Update request status back to Pending
    const reqIndex = shareRequests.findIndex(r => r.id === id);
    if (reqIndex !== -1) {
      shareRequests[reqIndex] = {
        ...shareRequests[reqIndex],
        status: 'Pending',
        isInReview: false,
        reviewingAdminId: null,
        reviewingAdminName: null,
        reviewStartedAt: null,
      };
    }

    // Update detailed request
    detailedRequests[id as string] = {
      ...details,
      status: 'Pending',
      lockStatus: {
        isLocked: false,
        isLockedByCurrentAdmin: false,
        lockedByAdminId: null,
        lockedByAdminName: null,
        lockExpiresAt: null,
      },
      history: [
        ...details.history,
        {
          timestamp: new Date().toISOString(),
          action: 'ReviewReleased',
          actorId: 'admin-1',
          actorName: 'Admin User',
          details: 'Review released',
        },
      ],
    };

    return HttpResponse.json(
      { message: 'Review released successfully' },
      {
        headers: {
          'X-Correlation-Id': `test-correlation-${Date.now()}`,
        },
      }
    );
  }),

  // POST /api/v1/admin/share-requests/:id/approve - Approve request
  http.post(`${API_BASE}/api/v1/admin/share-requests/:id/approve`, async ({ params, request }) => {
    const { id } = params;
    const details = detailedRequests[id as string];
    const body = (await request.json()) as ApproveRequestData;

    if (!details) {
      return HttpResponse.json({ error: 'Share request not found' }, { status: 404 });
    }

    // Check lock
    const lock = lockState[id as string];
    if (!lock?.isLocked || lock.lockedByAdminId !== 'admin-1') {
      return HttpResponse.json(
        { error: 'You must acquire the review lock first' },
        { status: 403 }
      );
    }

    // Approve request
    const reqIndex = shareRequests.findIndex(r => r.id === id);
    if (reqIndex !== -1) {
      shareRequests[reqIndex] = {
        ...shareRequests[reqIndex],
        status: 'Approved',
        isInReview: false,
        reviewingAdminId: null,
        reviewingAdminName: null,
      };
    }

    // Release lock
    delete lockState[id as string];

    // Update detailed request
    detailedRequests[id as string] = {
      ...details,
      status: 'Approved',
      lockStatus: {
        isLocked: false,
        isLockedByCurrentAdmin: false,
        lockedByAdminId: null,
        lockedByAdminName: null,
        lockExpiresAt: null,
      },
      resolvedAt: new Date().toISOString(),
      history: [
        ...details.history,
        {
          timestamp: new Date().toISOString(),
          action: 'Approved',
          actorId: 'admin-1',
          actorName: 'Admin User',
          details: body.adminNotes || 'Request approved',
        },
      ],
    };

    return HttpResponse.json(
      { message: 'Share request approved successfully' },
      {
        headers: {
          'X-Correlation-Id': `test-correlation-${Date.now()}`,
        },
      }
    );
  }),

  // POST /api/v1/admin/share-requests/:id/reject - Reject request
  http.post(`${API_BASE}/api/v1/admin/share-requests/:id/reject`, async ({ params, request }) => {
    const { id } = params;
    const details = detailedRequests[id as string];
    const body = (await request.json()) as RejectRequestData;

    if (!details) {
      return HttpResponse.json({ error: 'Share request not found' }, { status: 404 });
    }

    // Check lock
    const lock = lockState[id as string];
    if (!lock?.isLocked || lock.lockedByAdminId !== 'admin-1') {
      return HttpResponse.json(
        { error: 'You must acquire the review lock first' },
        { status: 403 }
      );
    }

    // Reject request
    const reqIndex = shareRequests.findIndex(r => r.id === id);
    if (reqIndex !== -1) {
      shareRequests[reqIndex] = {
        ...shareRequests[reqIndex],
        status: 'Rejected',
        isInReview: false,
        reviewingAdminId: null,
        reviewingAdminName: null,
      };
    }

    // Release lock
    delete lockState[id as string];

    // Update detailed request
    detailedRequests[id as string] = {
      ...details,
      status: 'Rejected',
      lockStatus: {
        isLocked: false,
        isLockedByCurrentAdmin: false,
        lockedByAdminId: null,
        lockedByAdminName: null,
        lockExpiresAt: null,
      },
      resolvedAt: new Date().toISOString(),
      history: [
        ...details.history,
        {
          timestamp: new Date().toISOString(),
          action: 'Rejected',
          actorId: 'admin-1',
          actorName: 'Admin User',
          details: body.reason,
        },
      ],
    };

    return HttpResponse.json(
      { message: 'Share request rejected successfully' },
      {
        headers: {
          'X-Correlation-Id': `test-correlation-${Date.now()}`,
        },
      }
    );
  }),

  // POST /api/v1/admin/share-requests/:id/request-changes - Request changes
  http.post(
    `${API_BASE}/api/v1/admin/share-requests/:id/request-changes`,
    async ({ params, request }) => {
      const { id } = params;
      const details = detailedRequests[id as string];
      const body = (await request.json()) as RequestChangesData;

      if (!details) {
        return HttpResponse.json({ error: 'Share request not found' }, { status: 404 });
      }

      // Check lock
      const lock = lockState[id as string];
      if (!lock?.isLocked || lock.lockedByAdminId !== 'admin-1') {
        return HttpResponse.json(
          { error: 'You must acquire the review lock first' },
          { status: 403 }
        );
      }

      // Request changes
      const reqIndex = shareRequests.findIndex(r => r.id === id);
      if (reqIndex !== -1) {
        shareRequests[reqIndex] = {
          ...shareRequests[reqIndex],
          status: 'ChangesRequested',
          isInReview: false,
          reviewingAdminId: null,
          reviewingAdminName: null,
        };
      }

      // Release lock
      delete lockState[id as string];

      // Update detailed request
      detailedRequests[id as string] = {
        ...details,
        status: 'ChangesRequested',
        lockStatus: {
          isLocked: false,
          isLockedByCurrentAdmin: false,
          lockedByAdminId: null,
          lockedByAdminName: null,
          lockExpiresAt: null,
        },
        history: [
          ...details.history,
          {
            timestamp: new Date().toISOString(),
            action: 'ChangesRequested',
            actorId: 'admin-1',
            actorName: 'Admin User',
            details: body.feedback,
          },
        ],
      };

      return HttpResponse.json(
        { message: 'Changes requested successfully' },
        {
          headers: {
            'X-Correlation-Id': `test-correlation-${Date.now()}`,
          },
        }
      );
    }
  ),
];

/**
 * Helper to reset state between tests
 */
export const resetAdminShareRequestsState = () => {
  shareRequests = [
    {
      id: 'share-req-1',
      status: 'Pending' as ShareRequestStatus,
      contributionType: 'NewGame' as ContributionType,
      sourceGameId: 'game-1',
      gameTitle: 'Wingspan',
      gameThumbnailUrl: 'https://placehold.co/100x100/png?text=Wingspan',
      bggId: 266192,
      userId: 'user-1',
      userName: 'Sarah Johnson',
      userAvatarUrl: 'https://placehold.co/40x40/png?text=SJ',
      userTotalContributions: 15,
      userNotes: 'Great engine-building game about birds!',
      attachedDocumentCount: 3,
      createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
      isInReview: false,
      reviewingAdminId: null,
      reviewingAdminName: null,
      reviewStartedAt: null,
      targetSharedGameId: null,
      targetSharedGameTitle: null,
    },
    {
      id: 'share-req-2',
      status: 'InReview' as ShareRequestStatus,
      contributionType: 'AdditionalContent' as ContributionType,
      sourceGameId: 'game-2',
      gameTitle: 'Terraforming Mars',
      gameThumbnailUrl: 'https://placehold.co/100x100/png?text=TM',
      bggId: 167791,
      userId: 'user-2',
      userName: 'Mike Chen',
      userAvatarUrl: 'https://placehold.co/40x40/png?text=MC',
      userTotalContributions: 8,
      userNotes: 'Additional FAQ content for advanced strategies',
      attachedDocumentCount: 1,
      createdAt: new Date(Date.now() - 5 * 24 * 60 * 60 * 1000).toISOString(),
      isInReview: true,
      reviewingAdminId: 'admin-1',
      reviewingAdminName: 'Admin User',
      reviewStartedAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(),
      targetSharedGameId: 'shared-game-1',
      targetSharedGameTitle: 'Terraforming Mars',
    },
    {
      id: 'share-req-3',
      status: 'Pending' as ShareRequestStatus,
      contributionType: 'NewGame' as ContributionType,
      sourceGameId: 'game-3',
      gameTitle: 'Gloomhaven',
      gameThumbnailUrl: 'https://placehold.co/100x100/png?text=Gloomhaven',
      bggId: 174430,
      userId: 'user-3',
      userName: 'Emma Wilson',
      userAvatarUrl: 'https://placehold.co/40x40/png?text=EW',
      userTotalContributions: 23,
      userNotes: 'Complete campaign tracking system included',
      attachedDocumentCount: 5,
      createdAt: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(),
      isInReview: false,
      reviewingAdminId: null,
      reviewingAdminName: null,
      reviewStartedAt: null,
      targetSharedGameId: null,
      targetSharedGameTitle: null,
    },
  ];

  // Clear lock state
  Object.keys(lockState).forEach(key => delete lockState[key]);
};

/**
 * Helper to add a mock share request for testing
 */
export const addMockShareRequest = (request: Partial<AdminShareRequestDto>) => {
  const newRequest: AdminShareRequestDto = {
    id: request.id || generateUUID(),
    status: request.status || 'Pending',
    contributionType: request.contributionType || 'NewGame',
    sourceGameId: request.sourceGameId || generateUUID(),
    gameTitle: request.gameTitle || 'Test Game',
    gameThumbnailUrl: request.gameThumbnailUrl || null,
    bggId: request.bggId || null,
    userId: request.userId || generateUUID(),
    userName: request.userName || 'Test User',
    userAvatarUrl: request.userAvatarUrl || null,
    userTotalContributions: request.userTotalContributions || 0,
    userNotes: request.userNotes || null,
    attachedDocumentCount: request.attachedDocumentCount || 0,
    createdAt: request.createdAt || new Date().toISOString(),
    isInReview: request.isInReview || false,
    reviewingAdminId: request.reviewingAdminId || null,
    reviewingAdminName: request.reviewingAdminName || null,
    reviewStartedAt: request.reviewStartedAt || null,
    targetSharedGameId: request.targetSharedGameId || null,
    targetSharedGameTitle: request.targetSharedGameTitle || null,
  };

  shareRequests.push(newRequest);
  return newRequest;
};

/**
 * Export mock data for test assertions
 */
export { shareRequests as mockShareRequests };
