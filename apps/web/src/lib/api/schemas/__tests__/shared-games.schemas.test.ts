import { describe, it, expect } from 'vitest';
import {
  SharedGameDetailSchema,
  SharedGameDocumentSchema,
  SharedGameSchema,
} from '../shared-games.schemas';

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

// #5099: GET /api/v1/admin/shared-games/{id}/documents as the API sends it (enums by name, Program.cs). The schema
// wanted a number, so the list of the admin game page failed and «Imposta cover» was unreachable.
const documentAsSent = {
  id: '189c5d57-2dae-46bd-85e9-c55b2e8a0e83',
  sharedGameId: '91e1ba0a-098c-582f-e6bc-cefbf9eb60d8',
  pdfDocumentId: 'cc216014-19a3-4a59-bdba-d1219c18e422',
  documentType: 'Rulebook',
  version: '1.0',
  isActive: true,
  tags: [],
  createdAt: '2026-11-21T22:10:52.580869Z',
  createdBy: '95b7547b-f790-443b-94f1-c8b572e20a62',
};

describe('SharedGameDocumentSchema #5099', () => {
  it("accetta il documento come lo manda l'API e dà il tipo numerico che usano le schermate", () => {
    const parsed = SharedGameDocumentSchema.safeParse(documentAsSent);

    expect(parsed.error?.issues).toBeUndefined();
    expect(parsed.data?.documentType).toBe(0);
  });

  it.each([
    ['Errata', 1],
    ['Homerule', 2],
  ])('converte %s in %i', (name, value) => {
    expect(
      SharedGameDocumentSchema.parse({ ...documentAsSent, documentType: name }).documentType
    ).toBe(value);
  });

  it('accetta ancora il numero', () => {
    expect(
      SharedGameDocumentSchema.parse({ ...documentAsSent, documentType: 1 }).documentType
    ).toBe(1);
  });

  it('rifiuta un tipo che non esiste', () => {
    expect(
      SharedGameDocumentSchema.safeParse({ ...documentAsSent, documentType: 'Manual' }).success
    ).toBe(false);
    expect(SharedGameDocumentSchema.safeParse({ ...documentAsSent, documentType: 3 }).success).toBe(
      false
    );
  });
});
