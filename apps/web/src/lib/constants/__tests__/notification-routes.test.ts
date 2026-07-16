/**
 * ADR-075 (#2996): pins the FE NotificationRoutes value set and builder substitution. The BE twin
 * (NotificationRoutesTests.cs) pins the same golden set; scripts/lint-cross-lang-constants.sh is the
 * authoritative BE↔FE cross-language parity gate in CI.
 */
import { describe, it, expect } from 'vitest';

import {
  NotificationRoutes,
  libraryAgent,
  privateToolkit,
  contributionRequest,
  sharedGame,
  adminSharedGame,
  adminApprovalQueue,
  documentRoute,
  adminShareRequest,
  adminMechanicAnalysisReview,
  gameNight,
  game,
} from '../notification-routes';

// Golden set — MUST equal the const values in NotificationRoutes.cs (BE).
const GOLDEN_ROUTE_VALUES = [
  '/library/games/{id}/agent',
  '/library/private/{id}/toolkit',
  '/contributions/requests/{id}',
  '/shared-games/{id}',
  '/admin/shared-games/{id}',
  '/admin/approval-queue?gameId={id}',
  '/documents/{id}',
  '/admin/share-requests/{id}',
  '/admin/mechanic-analyses/{id}/review',
  '/game-nights/{id}',
  '/games/{id}',
  '/dashboard',
  '/account/suspended',
  '/contributions',
  '/contributions/requests?status=pending',
  '/admin/knowledge-base/queue',
  '/admin/knowledge-base/mechanic-extractor/dashboard',
  '/users/me/badges',
  '/settings/subscription',
  '/admin/agents/usage',
  '/admin/agents/strategy',
  '/admin/share-requests?sort=oldest',
  '/settings/notifications',
  '/library',
  '/achievements',
  '/sessions',
];

describe('NotificationRoutes (ADR-075 #2996)', () => {
  it('exposes exactly the golden route value set', () => {
    expect([...Object.values(NotificationRoutes)].sort()).toEqual([...GOLDEN_ROUTE_VALUES].sort());
  });

  it('builders substitute the {id} token', () => {
    const id = '11111111-2222-3333-4444-555555555555';
    expect(libraryAgent(id)).toBe(`/library/games/${id}/agent`);
    expect(privateToolkit(id)).toBe(`/library/private/${id}/toolkit`);
    expect(contributionRequest(id)).toBe(`/contributions/requests/${id}`);
    expect(sharedGame(id)).toBe(`/shared-games/${id}`);
    expect(adminSharedGame(id)).toBe(`/admin/shared-games/${id}`);
    expect(adminApprovalQueue(id)).toBe(`/admin/approval-queue?gameId=${id}`);
    expect(documentRoute(id)).toBe(`/documents/${id}`);
    expect(adminShareRequest(id)).toBe(`/admin/share-requests/${id}`);
    expect(adminMechanicAnalysisReview(id)).toBe(`/admin/mechanic-analyses/${id}/review`);
    expect(gameNight(id)).toBe(`/game-nights/${id}`);
    expect(game(id)).toBe(`/games/${id}`);
  });
});
