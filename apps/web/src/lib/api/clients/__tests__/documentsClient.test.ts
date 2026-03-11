/**
 * Tests for Documents Client (Issue #2340)
 *
 * Coverage target: 90%+
 * Tests: Document collections CRUD, document operations, error handling
 */

import { createDocumentsClient } from '../documentsClient';
import { HttpClient } from '../../core/httpClient';
import type {
  DocumentCollection,
  CollectionDocument,
  CreateDocumentCollectionRequest,
  AddDocumentToCollectionRequest,
  ReorderDocumentRequest,
  UpdateDocumentCollectionRequest,
} from '../../schemas/documents.schemas';
import type { PdfDocumentDto } from '../../schemas/pdf.schemas';

type MockedHttpClient = {
  [K in keyof HttpClient]: ReturnType<typeof vi.fn>;
};

describe('createDocumentsClient', () => {
  let mockHttpClient: MockedHttpClient;
  let documentsClient: ReturnType<typeof createDocumentsClient>;

  const mockGameId = '123e4567-e89b-12d3-a456-426614174000';
  const mockCollectionId = '223e4567-e89b-12d3-a456-426614174000';
  const mockDocumentId = '323e4567-e89b-12d3-a456-426614174000';

  beforeEach(() => {
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      patch: vi.fn(),
      delete: vi.fn(),
      postFile: vi.fn(),
    } as any;

    documentsClient = createDocumentsClient({ httpClient: mockHttpClient });
  });

  describe('getDocumentsByGame', () => {
    it('should fetch documents for a game successfully', async () => {
      const mockDocuments: PdfDocumentDto[] = [
        {
          id: mockDocumentId,
          gameId: mockGameId,
          fileName: 'rules.pdf',
          filePath: '/uploads/rules.pdf',
          fileSizeBytes: 1024000,
          processingStatus: 'Completed',
          uploadedAt: '2024-01-01T00:00:00Z',
          processedAt: '2024-01-01T00:05:00Z',
          pageCount: 10,
          documentType: 'base',
          isPublic: false,
          processingState: 'Pending',
          progressPercentage: 0,
          retryCount: 0,
          maxRetries: 3,
          canRetry: false,
          errorCategory: null,
          processingError: null,
          documentCategory: 'Rulebook',
          baseDocumentId: null,
          isActiveForRag: true,
          hasAcceptedDisclaimer: false,
          versionLabel: null,
        },
      ];

      mockHttpClient.get.mockResolvedValueOnce({ pdfs: mockDocuments });

      const result = await documentsClient.getDocumentsByGame(mockGameId);

      expect(mockHttpClient.get).toHaveBeenCalledWith(`/api/v1/games/${mockGameId}/pdfs`);
      expect(result).toEqual(mockDocuments);
      expect(result).toHaveLength(1);
    });

    it('should return empty array when no documents found', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ pdfs: [] });

      const result = await documentsClient.getDocumentsByGame(mockGameId);

      expect(result).toEqual([]);
    });

    it('should handle null response', async () => {
      mockHttpClient.get.mockResolvedValueOnce({ pdfs: null });

      const result = await documentsClient.getDocumentsByGame(mockGameId);

      expect(result).toEqual([]);
    });

    it('should handle API errors', async () => {
      mockHttpClient.get.mockRejectedValueOnce(new Error('Network error'));

      await expect(documentsClient.getDocumentsByGame(mockGameId)).rejects.toThrow('Network error');
    });
  });

  describe('createCollection', () => {
    it('should create collection successfully', async () => {
      const request: CreateDocumentCollectionRequest = {
        name: 'Official Rules',
        description: 'Official rulebook',
        initialDocuments: [
          {
            pdfDocumentId: mockDocumentId,
            documentType: 'base',
            sortOrder: 0,
          },
        ],
      };

      const mockCollection: DocumentCollection = {
        id: mockCollectionId,
        gameId: mockGameId,
        name: 'Official Rules',
        description: 'Official rulebook',
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
        documentCount: 1,
      };

      mockHttpClient.post.mockResolvedValueOnce(mockCollection);

      const result = await documentsClient.createCollection(mockGameId, request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/document-collections`,
        request
      );
      expect(result).toEqual(mockCollection);
    });

    it('should handle validation errors', async () => {
      const request: CreateDocumentCollectionRequest = {
        name: '',
        description: '',
        initialDocuments: [],
      };

      mockHttpClient.post.mockRejectedValueOnce(new Error('Validation failed'));

      await expect(documentsClient.createCollection(mockGameId, request)).rejects.toThrow(
        'Validation failed'
      );
    });
  });

  describe('listCollections', () => {
    it('should list collections successfully', async () => {
      const mockCollections: DocumentCollection[] = [
        {
          id: mockCollectionId,
          gameId: mockGameId,
          name: 'Collection 1',
          description: 'First collection',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
          documentCount: 2,
        },
        {
          id: '333e4567-e89b-12d3-a456-426614174000',
          gameId: mockGameId,
          name: 'Collection 2',
          description: 'Second collection',
          createdAt: '2024-01-02T00:00:00Z',
          updatedAt: '2024-01-02T00:00:00Z',
          documentCount: 1,
        },
      ];

      mockHttpClient.get.mockResolvedValueOnce(mockCollections);

      const result = await documentsClient.listCollections(mockGameId);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/document-collections`
      );
      expect(result).toHaveLength(2);
    });

    it('should return empty array when no collections', async () => {
      mockHttpClient.get.mockResolvedValueOnce([]);

      const result = await documentsClient.listCollections(mockGameId);

      expect(result).toEqual([]);
    });
  });

  describe('getCollection', () => {
    it('should get specific collection successfully', async () => {
      const mockCollection: DocumentCollection = {
        id: mockCollectionId,
        gameId: mockGameId,
        name: 'Official Rules',
        description: 'Rulebook',
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
        documentCount: 3,
      };

      mockHttpClient.get.mockResolvedValueOnce(mockCollection);

      const result = await documentsClient.getCollection(mockGameId, mockCollectionId);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/document-collections/${mockCollectionId}`
      );
      expect(result).toEqual(mockCollection);
    });

    it('should handle not found error', async () => {
      mockHttpClient.get.mockRejectedValueOnce(new Error('Collection not found'));

      await expect(documentsClient.getCollection(mockGameId, mockCollectionId)).rejects.toThrow(
        'Collection not found'
      );
    });
  });

  describe('updateCollection', () => {
    it('should update collection successfully', async () => {
      const request: UpdateDocumentCollectionRequest = {
        name: 'Updated Name',
        description: 'Updated description',
      };

      const mockUpdated: DocumentCollection = {
        id: mockCollectionId,
        gameId: mockGameId,
        name: 'Updated Name',
        description: 'Updated description',
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T01:00:00Z',
        documentCount: 2,
      };

      mockHttpClient.put.mockResolvedValueOnce(mockUpdated);

      const result = await documentsClient.updateCollection(mockGameId, mockCollectionId, request);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/document-collections/${mockCollectionId}`,
        request
      );
      expect(result).toEqual(mockUpdated);
    });
  });

  describe('deleteCollection', () => {
    it('should delete collection successfully', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      await documentsClient.deleteCollection(mockGameId, mockCollectionId);

      expect(mockHttpClient.delete).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/document-collections/${mockCollectionId}`
      );
    });

    it('should handle delete errors', async () => {
      mockHttpClient.delete.mockRejectedValueOnce(new Error('Delete failed'));

      await expect(documentsClient.deleteCollection(mockGameId, mockCollectionId)).rejects.toThrow(
        'Delete failed'
      );
    });
  });

  describe('listDocuments', () => {
    it('should list documents in collection', async () => {
      const mockDocuments: CollectionDocument[] = [
        {
          id: mockDocumentId,
          pdfDocumentId: mockDocumentId,
          documentType: 'base',
          sortOrder: 0,
          fileName: 'rules.pdf',
          uploadedAt: '2024-01-01T00:00:00Z',
        },
      ];

      mockHttpClient.get.mockResolvedValueOnce(mockDocuments);

      const result = await documentsClient.listDocuments(mockGameId, mockCollectionId);

      expect(mockHttpClient.get).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/document-collections/${mockCollectionId}/documents`
      );
      expect(result).toEqual(mockDocuments);
    });

    it('should return empty array when no documents', async () => {
      mockHttpClient.get.mockResolvedValueOnce([]);

      const result = await documentsClient.listDocuments(mockGameId, mockCollectionId);

      expect(result).toEqual([]);
    });
  });

  describe('addDocument', () => {
    it('should add document to collection', async () => {
      const request: AddDocumentToCollectionRequest = {
        pdfDocumentId: mockDocumentId,
        documentType: 'base',
        sortOrder: 1,
      };

      const mockAdded: CollectionDocument = {
        id: '423e4567-e89b-12d3-a456-426614174000',
        pdfDocumentId: mockDocumentId,
        documentType: 'base',
        sortOrder: 1,
        fileName: 'rules.pdf',
        uploadedAt: '2024-01-01T00:00:00Z',
      };

      mockHttpClient.post.mockResolvedValueOnce(mockAdded);

      const result = await documentsClient.addDocument(mockGameId, mockCollectionId, request);

      expect(mockHttpClient.post).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/document-collections/${mockCollectionId}/documents`,
        request
      );
      expect(result).toEqual(mockAdded);
    });
  });

  describe('reorderDocument', () => {
    it('should reorder document successfully', async () => {
      const request: ReorderDocumentRequest = {
        newSortOrder: 2,
      };

      mockHttpClient.put.mockResolvedValueOnce(undefined);

      await documentsClient.reorderDocument(mockGameId, mockCollectionId, mockDocumentId, request);

      expect(mockHttpClient.put).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/document-collections/${mockCollectionId}/documents/${mockDocumentId}/reorder`,
        request
      );
    });
  });

  describe('removeDocument', () => {
    it('should remove document from collection', async () => {
      mockHttpClient.delete.mockResolvedValueOnce(undefined);

      await documentsClient.removeDocument(mockGameId, mockCollectionId, mockDocumentId);

      expect(mockHttpClient.delete).toHaveBeenCalledWith(
        `/api/v1/games/${mockGameId}/document-collections/${mockCollectionId}/documents/${mockDocumentId}`
      );
    });

    it('should handle removal errors', async () => {
      mockHttpClient.delete.mockRejectedValueOnce(new Error('Remove failed'));

      await expect(
        documentsClient.removeDocument(mockGameId, mockCollectionId, mockDocumentId)
      ).rejects.toThrow('Remove failed');
    });
  });
});
