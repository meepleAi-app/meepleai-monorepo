import { describe, it, expect } from 'vitest';
import { SharedGameSchema, SharedGameDetailSchema } from '../shared-games.schemas';

const validListFixture = {
  id: '00000000-0000-4000-8000-000000000001',
  bggId: 1,
  title: 'Test Game',
  yearPublished: 2020,
  description: 'A test game',
  minPlayers: 1,
  maxPlayers: 4,
  playingTimeMinutes: 30,
  minAge: 8,
  complexityRating: 2.5,
  averageRating: 7.5,
  imageUrl: 'https://example.com/image.jpg',
  thumbnailUrl: 'https://example.com/thumb.jpg',
  status: 'Published' as const,
  isRagPublic: false,
  hasKnowledgeBase: false,
  createdAt: '2026-01-01T00:00:00Z',
  modifiedAt: null,
};

const validDetailFixture = {
  ...validListFixture,
  createdBy: '00000000-0000-4000-8000-000000000010',
  modifiedBy: null,
  rules: null,
  faqs: [],
  erratas: [],
  designers: [],
  publishers: [],
  categories: [],
  mechanics: [],
};

describe('SharedGameSchema #3611', () => {
  it('accetta il punto focale della cover quando presente (#3611)', () => {
    const parsed = SharedGameSchema.parse({
      ...validListFixture,
      coverFocalX: 0.5,
      coverFocalY: 0.2,
    });
    expect(parsed.coverFocalY).toBe(0.2);
  });

  it('resta valido quando il punto focale è assente (risposta da cache pre-#3611)', () => {
    const parsed = SharedGameSchema.parse(validListFixture);
    expect(parsed.coverFocalY).toBeUndefined();
  });
});

describe('SharedGameDetailSchema #3611', () => {
  it('accetta il punto focale della cover quando presente (#3611)', () => {
    const parsed = SharedGameDetailSchema.parse({
      ...validDetailFixture,
      coverFocalX: 0.5,
      coverFocalY: 0.2,
    });
    expect(parsed.coverFocalY).toBe(0.2);
  });

  it('resta valido quando il punto focale è assente (risposta da cache pre-#3611)', () => {
    const parsed = SharedGameDetailSchema.parse(validDetailFixture);
    expect(parsed.coverFocalY).toBeUndefined();
  });
});
