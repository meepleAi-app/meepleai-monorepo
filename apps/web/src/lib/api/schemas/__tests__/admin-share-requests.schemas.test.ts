import { describe, it, expect } from 'vitest';

import {
  ApproveShareRequestResponseSchema,
  ShareRequestDetailsDtoSchema,
} from '../admin-share-requests.schemas';

// #5098: a seeded catalog game has a deterministic id that is not an RFC 4122 UUID (variant nibble 'e' here, the id
// of Azul in the seed). The review of a request on such a game failed its schema and never opened.
const SEEDED_GAME_ID = '91e1ba0a-098c-582f-e6bc-cefbf9eb60d8';
const REQUEST_ID = '11111111-1111-4111-8111-111111111111';

const game = (id: string) => ({
  id,
  title: 'Azul',
  yearPublished: 2017,
  description: 'Tiles.',
  thumbnailUrl: '',
  bggId: 230802,
  minPlayers: 2,
  maxPlayers: 4,
  playingTime: 45,
  complexity: 1.77,
  categories: [],
  mechanisms: [],
});

const details = (requestId: string) => ({
  id: requestId,
  status: 'Pending',
  contributionType: 'CoverChange',
  sourceGame: game(SEEDED_GAME_ID),
  targetSharedGame: game(SEEDED_GAME_ID),
  contributor: {
    userId: '55555555-5555-4555-8555-555555555555',
    userName: 'Ada',
    avatarUrl: null,
    joinedAt: '2026-01-01T00:00:00Z',
    totalContributions: 0,
    approvedContributions: 0,
    approvalRate: 0,
    badges: [],
  },
  userNotes: null,
  attachedDocuments: [],
  history: [],
  lockStatus: {
    isLocked: false,
    isLockedByCurrentAdmin: false,
    lockedByAdminId: null,
    lockedByAdminName: null,
    lockExpiresAt: null,
  },
  createdAt: '2026-11-21T22:14:02.756622Z',
  resolvedAt: null,
  coverChange: null,
});

describe('admin share request schemas, ids of seeded games (#5098)', () => {
  it('accepts the details of a request on a seeded catalog game', () => {
    const parsed = ShareRequestDetailsDtoSchema.safeParse(details(REQUEST_ID));

    expect(parsed.error?.issues).toBeUndefined();
    expect(parsed.data?.targetSharedGame?.id).toBe(SEEDED_GAME_ID);
  });

  it('accepts the approval of a proposal on a seeded catalog game', () => {
    // approve-game-proposal (UpdateCover for a cover change): the server approves, and a rejected response made the
    // screen report an error for an approval that had happened.
    const parsed = ApproveShareRequestResponseSchema.safeParse({
      shareRequestId: REQUEST_ID,
      status: 'Approved',
      targetSharedGameId: SEEDED_GAME_ID,
      resolvedAt: '2026-11-21T22:20:00Z',
    });

    expect(parsed.error?.issues).toBeUndefined();
  });

  it('still wants a real UUID for an id that is not a game', () => {
    const parsed = ShareRequestDetailsDtoSchema.safeParse(details(SEEDED_GAME_ID));

    expect(parsed.success).toBe(false);
    expect(parsed.error?.issues.map(i => i.path.join('.'))).toEqual(['id']);
  });
});
