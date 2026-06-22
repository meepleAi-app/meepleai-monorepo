/**
 * KB Docs Client Tests (Phase 3 #1737)
 *
 * Tests for patchKbDocMetadata method covering PATCH /api/v1/kb-docs/{id}
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { createKbDocsClient, type KbDocsClient } from '../kbDocsClient';
import { UserKbDocDtoSchema } from '../../schemas/kb-docs.schemas';
import { type PatchKbDocMetadataRequest } from '../../schemas/kb-docs-patch.schemas';

// Mock HttpClient
const createMockHttpClient = () => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  patch: vi.fn(),
  delete: vi.fn(),
  postFile: vi.fn(),
});

describe('KbDocsClient.patchKbDocMetadata', () => {
  let client: KbDocsClient;
  let mockHttpClient: ReturnType<typeof createMockHttpClient>;

  beforeEach(() => {
    mockHttpClient = createMockHttpClient();
    client = createKbDocsClient({ httpClient: mockHttpClient as any });
  });

  it('should call httpClient.patch with correct path and body', async () => {
    const docId = '550e8400-e29b-41d4-a716-446655440000';
    const body: PatchKbDocMetadataRequest = {
      title: 'New Title',
      documentType: 'expansion',
      language: 'en',
      tags: ['strategy', 'cooperative'],
    };

    const mockResponse = {
      id: docId,
      gameId: '550e8400-e29b-41d4-a716-446655440001',
      gameName: 'Catan',
      fileName: 'rules.pdf',
      processingState: 'Ready',
      pageCount: 10,
      processedAt: '2026-05-31T10:00:00Z',
      uploadedAt: '2026-05-30T10:00:00Z',
      updatedAt: '2026-05-31T11:00:00Z',
      title: 'New Title',
      tags: ['strategy', 'cooperative'],
      updatedBy: '550e8400-e29b-41d4-a716-446655440002',
    };

    mockHttpClient.patch.mockResolvedValueOnce(mockResponse);

    const result = await client.patchKbDocMetadata(docId, body);

    expect(mockHttpClient.patch).toHaveBeenCalledWith(
      `/api/v1/kb-docs/${docId}`,
      body,
      UserKbDocDtoSchema
    );
    expect(result).toEqual(mockResponse);
  });

  it('should handle empty/optional fields in request body', async () => {
    const docId = '550e8400-e29b-41d4-a716-446655440000';
    const body: PatchKbDocMetadataRequest = {
      title: '',
      tags: [],
    };

    const mockResponse = {
      id: docId,
      gameId: '550e8400-e29b-41d4-a716-446655440001',
      gameName: 'Catan',
      fileName: 'rules.pdf',
      processingState: 'Ready',
      pageCount: 10,
      processedAt: '2026-05-31T10:00:00Z',
      uploadedAt: '2026-05-30T10:00:00Z',
      updatedAt: '2026-05-31T11:00:00Z',
      title: null,
      tags: [],
    };

    mockHttpClient.patch.mockResolvedValueOnce(mockResponse);

    const result = await client.patchKbDocMetadata(docId, body);

    expect(mockHttpClient.patch).toHaveBeenCalledWith(
      `/api/v1/kb-docs/${docId}`,
      body,
      UserKbDocDtoSchema
    );
    expect(result.title).toBeNull();
    expect(result.tags).toEqual([]);
  });

  it('should validate response with UserKbDocDtoSchema', async () => {
    const docId = '550e8400-e29b-41d4-a716-446655440000';
    const body: PatchKbDocMetadataRequest = { title: 'Updated' };

    const mockResponse = {
      id: docId,
      gameId: null,
      gameName: null,
      fileName: 'doc.pdf',
      processingState: 'Ready',
      pageCount: 5,
      processedAt: '2026-05-31T10:00:00Z',
      uploadedAt: '2026-05-30T10:00:00Z',
      updatedAt: '2026-05-31T11:00:00Z',
      title: 'Updated',
    };

    mockHttpClient.patch.mockResolvedValueOnce(mockResponse);

    const result = await client.patchKbDocMetadata(docId, body);

    expect(result).toBeDefined();
    expect(result.id).toBe(docId);
    expect(result.title).toBe('Updated');
  });

  it('should handle API errors from httpClient.patch', async () => {
    const docId = '550e8400-e29b-41d4-a716-446655440000';
    const body: PatchKbDocMetadataRequest = { title: 'New Title' };
    const error = new Error('API Error: 404 Not Found');

    mockHttpClient.patch.mockRejectedValueOnce(error);

    await expect(client.patchKbDocMetadata(docId, body)).rejects.toThrow(
      'API Error: 404 Not Found'
    );
  });
});
