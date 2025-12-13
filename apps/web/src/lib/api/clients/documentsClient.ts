/**
 * Documents API Client
 *
 * Issue #2051: Multi-document upload API client
 *
 * Handles document collection operations:
 * - Create collections with initial documents
 * - List collections for a game
 * - Add documents to existing collections
 * - Reorder documents within collections
 * - Delete collections and documents
 */

import type { HttpClient } from '../core/httpClient';
import {
  type CreateDocumentCollectionRequest,
  type DocumentCollection,
  type CollectionDocument,
  type AddDocumentToCollectionRequest,
  type ReorderDocumentRequest,
  type UpdateDocumentCollectionRequest,
  documentCollectionSchema,
  collectionDocumentSchema,
} from '../schemas/documents.schemas';
import { PdfDocumentDtoSchema, type PdfDocumentDto } from '../schemas/pdf.schemas';
import { z } from 'zod';

export interface DocumentsClient {
  /**
   * Get all PDF documents for a game (Issue #2051)
   *
   * @param gameId - The game ID
   * @returns List of PDF documents
   */
  getDocumentsByGame(gameId: string): Promise<PdfDocumentDto[]>;

  /**
   * Create a new document collection
   *
   * @param gameId - The game ID
   * @param request - Collection creation request
   * @returns Created document collection
   */
  createCollection(
    gameId: string,
    request: CreateDocumentCollectionRequest
  ): Promise<DocumentCollection>;

  /**
   * List all collections for a game
   *
   * @param gameId - The game ID
   * @returns Array of document collections
   */
  listCollections(gameId: string): Promise<DocumentCollection[]>;

  /**
   * Get a specific collection by ID
   *
   * @param gameId - The game ID
   * @param collectionId - The collection ID
   * @returns Document collection with details
   */
  getCollection(gameId: string, collectionId: string): Promise<DocumentCollection>;

  /**
   * Update collection metadata
   *
   * @param gameId - The game ID
   * @param collectionId - The collection ID
   * @param request - Update request
   * @returns Updated document collection
   */
  updateCollection(
    gameId: string,
    collectionId: string,
    request: UpdateDocumentCollectionRequest
  ): Promise<DocumentCollection>;

  /**
   * Delete a collection
   *
   * @param gameId - The game ID
   * @param collectionId - The collection ID
   */
  deleteCollection(gameId: string, collectionId: string): Promise<void>;

  /**
   * List documents in a collection
   *
   * @param gameId - The game ID
   * @param collectionId - The collection ID
   * @returns Array of documents in the collection
   */
  listDocuments(gameId: string, collectionId: string): Promise<CollectionDocument[]>;

  /**
   * Add a document to a collection
   *
   * @param gameId - The game ID
   * @param collectionId - The collection ID
   * @param request - Add document request
   * @returns Added document details
   */
  addDocument(
    gameId: string,
    collectionId: string,
    request: AddDocumentToCollectionRequest
  ): Promise<CollectionDocument>;

  /**
   * Reorder a document within a collection
   *
   * @param gameId - The game ID
   * @param collectionId - The collection ID
   * @param documentId - The document ID
   * @param request - Reorder request
   */
  reorderDocument(
    gameId: string,
    collectionId: string,
    documentId: string,
    request: ReorderDocumentRequest
  ): Promise<void>;

  /**
   * Remove a document from a collection
   *
   * @param gameId - The game ID
   * @param collectionId - The collection ID
   * @param documentId - The document ID
   */
  removeDocument(gameId: string, collectionId: string, documentId: string): Promise<void>;
}

export function createDocumentsClient({ httpClient }: { httpClient: HttpClient }): DocumentsClient {
  return {
    async getDocumentsByGame(gameId) {
      const response = await httpClient.get<{ pdfs: PdfDocumentDto[] }>(
        `/api/v1/games/${gameId}/pdfs`
      );
      return z.array(PdfDocumentDtoSchema).parse(response?.pdfs ?? []);
    },

    async createCollection(gameId, request) {
      const response = await httpClient.post<DocumentCollection>(
        `/api/v1/games/${gameId}/document-collections`,
        request
      );
      return documentCollectionSchema.parse(response);
    },

    async listCollections(gameId) {
      const response = await httpClient.get<DocumentCollection[]>(
        `/api/v1/games/${gameId}/document-collections`
      );
      return z.array(documentCollectionSchema).parse(response);
    },

    async getCollection(gameId, collectionId) {
      const response = await httpClient.get<DocumentCollection>(
        `/api/v1/games/${gameId}/document-collections/${collectionId}`
      );
      return documentCollectionSchema.parse(response);
    },

    async updateCollection(gameId, collectionId, request) {
      const response = await httpClient.put<DocumentCollection>(
        `/api/v1/games/${gameId}/document-collections/${collectionId}`,
        request
      );
      return documentCollectionSchema.parse(response);
    },

    async deleteCollection(gameId, collectionId) {
      await httpClient.delete(`/api/v1/games/${gameId}/document-collections/${collectionId}`);
    },

    async listDocuments(gameId, collectionId) {
      const response = await httpClient.get<CollectionDocument[]>(
        `/api/v1/games/${gameId}/document-collections/${collectionId}/documents`
      );
      return z.array(collectionDocumentSchema).parse(response);
    },

    async addDocument(gameId, collectionId, request) {
      const response = await httpClient.post<CollectionDocument>(
        `/api/v1/games/${gameId}/document-collections/${collectionId}/documents`,
        request
      );
      return collectionDocumentSchema.parse(response);
    },

    async reorderDocument(gameId, collectionId, documentId, request) {
      await httpClient.put(
        `/api/v1/games/${gameId}/document-collections/${collectionId}/documents/${documentId}/reorder`,
        request
      );
    },

    async removeDocument(gameId, collectionId, documentId) {
      await httpClient.delete(
        `/api/v1/games/${gameId}/document-collections/${collectionId}/documents/${documentId}`
      );
    },
  };
}
