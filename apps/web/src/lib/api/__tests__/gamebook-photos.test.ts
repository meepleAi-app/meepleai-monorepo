import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

import {
  GamebookSegmentSchema,
  GamebookPhotoArtifactSchema,
  TranslateChunkSchema,
  uploadPhoto,
  segmentPhoto,
} from '../gamebook-photos';

// Valid v4 UUIDs: position 14 = [1-8], position 19 = [89ab]
const CAMPAIGN_ID = '11111111-1111-4111-a111-111111111111';
const PHOTO_ID = '22222222-2222-4222-a222-222222222222';
const PARA_ID = '33333333-3333-4333-a333-333333333333';

const validSegment = {
  paragraphNumber: 3,
  sourceText: 'You are in a dark forest.',
  boundingBox: '10,20,100,50',
};

const validArtifact = {
  id: PHOTO_ID,
  campaignId: CAMPAIGN_ID,
  pageType: 'Storybook',
  status: 'Segmented',
  ocrFullText: 'full text here',
  segments: [validSegment],
  failureReason: null,
  createdAt: '2026-05-07T10:00:00Z',
  expiresAt: '2026-05-08T10:00:00Z',
};

// ---------------------------------------------------------------------------
// Schema tests
// ---------------------------------------------------------------------------

describe('GamebookSegmentSchema', () => {
  it('parses valid segment', () => {
    expect(() => GamebookSegmentSchema.parse(validSegment)).not.toThrow();
  });

  it('rejects negative paragraphNumber', () => {
    expect(() => GamebookSegmentSchema.parse({ ...validSegment, paragraphNumber: -1 })).toThrow();
  });

  it('rejects empty sourceText', () => {
    expect(() => GamebookSegmentSchema.parse({ ...validSegment, sourceText: '' })).toThrow();
  });

  it('accepts null boundingBox', () => {
    const result = GamebookSegmentSchema.parse({ ...validSegment, boundingBox: null });
    expect(result.boundingBox).toBeNull();
  });
});

describe('GamebookPhotoArtifactSchema', () => {
  it('parses valid artifact', () => {
    const result = GamebookPhotoArtifactSchema.parse(validArtifact);
    expect(result.id).toBe(PHOTO_ID);
    expect(result.segments).toHaveLength(1);
  });

  it('normalises null segments to empty array', () => {
    const result = GamebookPhotoArtifactSchema.parse({ ...validArtifact, segments: null });
    expect(result.segments).toEqual([]);
  });

  it('rejects unknown status', () => {
    expect(() =>
      GamebookPhotoArtifactSchema.parse({ ...validArtifact, status: 'Unknown' })
    ).toThrow();
  });
});

describe('TranslateChunkSchema', () => {
  it('parses full chunk', () => {
    const result = TranslateChunkSchema.parse({
      delta: 'Hello',
      isComplete: false,
      paragraphId: PARA_ID,
      appliedTerms: ['Dragon'],
    });
    expect(result.delta).toBe('Hello');
    expect(result.paragraphId).toBe(PARA_ID);
  });

  it('defaults delta to empty string when missing', () => {
    const result = TranslateChunkSchema.parse({ isComplete: true });
    expect(result.delta).toBe('');
  });
});

// ---------------------------------------------------------------------------
// Fetch behaviour tests
// ---------------------------------------------------------------------------

describe('gamebook-photos client', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('uploadPhoto sends multipart FormData with credentials', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify(validArtifact), {
        status: 201,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    const file = new File(['bytes'], 'page.jpg', { type: 'image/jpeg' });
    const result = await uploadPhoto(CAMPAIGN_ID, file, 'Storybook');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain(`/api/v1/gamebook/campaigns/${CAMPAIGN_ID}/photos`);
    expect(url).not.toContain('/segment');
    expect(init.method).toBe('POST');
    expect(init.credentials).toBe('include');
    expect(init.body).toBeInstanceOf(FormData);
    expect(result.id).toBe(PHOTO_ID);
  });

  it('uploadPhoto throws on non-2xx', async () => {
    fetchMock.mockResolvedValueOnce(new Response('bad request', { status: 400 }));
    const file = new File(['bytes'], 'page.jpg', { type: 'image/jpeg' });
    await expect(uploadPhoto(CAMPAIGN_ID, file, 'Storybook')).rejects.toThrow(/400/);
  });

  it('segmentPhoto POSTs to the segment sub-resource', async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify(validArtifact), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    const result = await segmentPhoto(CAMPAIGN_ID, PHOTO_ID);

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toContain(`${PHOTO_ID}/segment`);
    expect(init.method).toBe('POST');
    expect(init.credentials).toBe('include');
    expect(result.status).toBe('Segmented');
  });

  it('segmentPhoto throws on non-2xx', async () => {
    fetchMock.mockResolvedValueOnce(new Response('server error', { status: 500 }));
    await expect(segmentPhoto(CAMPAIGN_ID, PHOTO_ID)).rejects.toThrow(/500/);
  });
});
