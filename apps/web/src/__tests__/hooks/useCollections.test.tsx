/**
 * ISSUE-4263: Generic UserCollection System - Test Suite
 * Comprehensive tests for generic collection hooks
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';

import {
  useCollectionStatus,
  useAddToCollection,
  useRemoveFromCollection,
} from '@/hooks/queries/useCollections';
import * as collectionsClient from '@/lib/api/clients/collectionsClient';
import type { CollectionStatusDto, EntityType } from '@/lib/api/schemas/collections.schemas';

// Mock the API client
vi.mock('@/lib/api/clients/collectionsClient');
vi.mock('sonner');

describe('useCollections hooks', () => {
  let queryClient: QueryClient;
  let wrapper: ({ children }: { children: ReactNode }) => JSX.Element;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });

    wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );

    vi.clearAllMocks();
  });

  describe('useCollectionStatus', () => {
    it('fetches collection status for player entity', async () => {
      // Arrange
      const entityId = '123e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'player';
      const mockStatus: CollectionStatusDto = {
        inCollection: true,
        isFavorite: false,
        associatedData: null,
      };

      vi.mocked(collectionsClient.getCollectionStatus).mockResolvedValue(mockStatus);

      // Act
      const { result } = renderHook(
        () => useCollectionStatus(entityType, entityId),
        { wrapper }
      );

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(mockStatus);
      expect(collectionsClient.getCollectionStatus).toHaveBeenCalledWith(entityType, entityId);
    });

    it('fetches collection status for event entity', async () => {
      // Arrange
      const entityId = '223e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'event';
      const mockStatus: CollectionStatusDto = {
        inCollection: false,
        isFavorite: false,
        associatedData: null,
      };

      vi.mocked(collectionsClient.getCollectionStatus).mockResolvedValue(mockStatus);

      // Act
      const { result } = renderHook(
        () => useCollectionStatus(entityType, entityId),
        { wrapper }
      );

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(mockStatus);
    });

    it('fetches collection status for session entity', async () => {
      // Arrange
      const entityId = '323e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'session';
      const mockStatus: CollectionStatusDto = {
        inCollection: true,
        isFavorite: true,
        associatedData: null,
      };

      vi.mocked(collectionsClient.getCollectionStatus).mockResolvedValue(mockStatus);

      // Act
      const { result } = renderHook(
        () => useCollectionStatus(entityType, entityId),
        { wrapper }
      );

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data?.inCollection).toBe(true);
      expect(result.current.data?.isFavorite).toBe(true);
    });

    it('fetches collection status for agent entity', async () => {
      // Arrange
      const entityId = '423e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'agent';
      const mockStatus: CollectionStatusDto = {
        inCollection: true,
        isFavorite: false,
        associatedData: null,
      };

      vi.mocked(collectionsClient.getCollectionStatus).mockResolvedValue(mockStatus);

      // Act
      const { result } = renderHook(
        () => useCollectionStatus(entityType, entityId),
        { wrapper }
      );

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(mockStatus);
    });

    it('fetches collection status for document entity', async () => {
      // Arrange
      const entityId = '523e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'document';
      const mockStatus: CollectionStatusDto = {
        inCollection: false,
        isFavorite: false,
        associatedData: null,
      };

      vi.mocked(collectionsClient.getCollectionStatus).mockResolvedValue(mockStatus);

      // Act
      const { result } = renderHook(
        () => useCollectionStatus(entityType, entityId),
        { wrapper }
      );

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(mockStatus);
    });

    it('fetches collection status for chatSession entity', async () => {
      // Arrange
      const entityId = '623e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'chatSession';
      const mockStatus: CollectionStatusDto = {
        inCollection: true,
        isFavorite: true,
        associatedData: null,
      };

      vi.mocked(collectionsClient.getCollectionStatus).mockResolvedValue(mockStatus);

      // Act
      const { result } = renderHook(
        () => useCollectionStatus(entityType, entityId),
        { wrapper }
      );

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(result.current.data).toEqual(mockStatus);
    });

    it('does not fetch when entityId is empty string', async () => {
      // Arrange
      const entityId = '';
      const entityType: EntityType = 'player';

      // Act
      const { result } = renderHook(
        () => useCollectionStatus(entityType, entityId),
        { wrapper }
      );

      // Assert
      expect(result.current.fetchStatus).toBe('idle');
      expect(collectionsClient.getCollectionStatus).not.toHaveBeenCalled();
    });

    it('respects enabled option', async () => {
      // Arrange
      const entityId = '123e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'player';

      // Act
      const { result } = renderHook(
        () => useCollectionStatus(entityType, entityId, { enabled: false }),
        { wrapper }
      );

      // Assert
      expect(result.current.fetchStatus).toBe('idle');
      expect(collectionsClient.getCollectionStatus).not.toHaveBeenCalled();
    });
  });

  describe('useAddToCollection', () => {
    it('adds entity to collection with optimistic update', async () => {
      // Arrange
      const entityId = '123e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'player';

      vi.mocked(collectionsClient.addToCollection).mockResolvedValue(undefined);

      // Act
      const { result } = renderHook(
        () => useAddToCollection(entityType, entityId),
        { wrapper }
      );

      result.current.mutate();

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(collectionsClient.addToCollection).toHaveBeenCalledWith(
        entityType,
        entityId,
        undefined
      );
    });

    it('adds entity with favorite flag', async () => {
      // Arrange
      const entityId = '223e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'event';
      const options = { isFavorite: true };

      vi.mocked(collectionsClient.addToCollection).mockResolvedValue(undefined);

      // Act
      const { result } = renderHook(
        () => useAddToCollection(entityType, entityId),
        { wrapper }
      );

      result.current.mutate(options);

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(collectionsClient.addToCollection).toHaveBeenCalledWith(
        entityType,
        entityId,
        options
      );
    });

    it('adds entity with notes', async () => {
      // Arrange
      const entityId = '323e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'agent';
      const options = { notes: 'My favorite agent for strategy games' };

      vi.mocked(collectionsClient.addToCollection).mockResolvedValue(undefined);

      // Act
      const { result } = renderHook(
        () => useAddToCollection(entityType, entityId),
        { wrapper }
      );

      result.current.mutate(options);

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(collectionsClient.addToCollection).toHaveBeenCalledWith(
        entityType,
        entityId,
        options
      );
    });

    it('invalidates queries on success', async () => {
      // Arrange
      const entityId = '423e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'document';

      vi.mocked(collectionsClient.addToCollection).mockResolvedValue(undefined);

      const invalidateQueriesSpy = vi.spyOn(queryClient, 'invalidateQueries');

      // Act
      const { result } = renderHook(
        () => useAddToCollection(entityType, entityId),
        { wrapper }
      );

      result.current.mutate();

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(invalidateQueriesSpy).toHaveBeenCalled();
    });
  });

  describe('useRemoveFromCollection', () => {
    it('removes entity from collection', async () => {
      // Arrange
      const entityId = '123e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'player';

      vi.mocked(collectionsClient.removeFromCollection).mockResolvedValue(undefined);

      // Set initial query data to simulate entity in collection
      queryClient.setQueryData(['collection-status', entityType, entityId], {
        inCollection: true,
        isFavorite: false,
        associatedData: null,
      });

      // Act
      const { result } = renderHook(
        () => useRemoveFromCollection(entityType, entityId),
        { wrapper }
      );

      result.current.remove();

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(collectionsClient.removeFromCollection).toHaveBeenCalledWith(
        entityType,
        entityId
      );
    });

    it('shows warning modal when entity has associated data', async () => {
      // Arrange
      const entityId = '223e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'event';
      const onRemovalWarning = vi.fn();

      vi.mocked(collectionsClient.removeFromCollection).mockResolvedValue(undefined);

      // Set query data with associated data
      queryClient.setQueryData(['collection-status', entityType, entityId], {
        inCollection: true,
        isFavorite: false,
        associatedData: {
          hasCustomAgent: true,
          hasPrivatePdf: false,
          chatSessionsCount: 2,
          gameSessionsCount: 0,
          checklistItemsCount: 0,
          labelsCount: 0,
        },
      });

      // Act
      const { result } = renderHook(
        () => useRemoveFromCollection(entityType, entityId, onRemovalWarning),
        { wrapper }
      );

      result.current.remove();

      // Assert
      expect(onRemovalWarning).toHaveBeenCalled();
      expect(collectionsClient.removeFromCollection).not.toHaveBeenCalled();
    });

    it('removes directly when no associated data', async () => {
      // Arrange
      const entityId = '323e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'session';
      const onRemovalWarning = vi.fn();

      vi.mocked(collectionsClient.removeFromCollection).mockResolvedValue(undefined);

      // Set query data without associated data
      queryClient.setQueryData(['collection-status', entityType, entityId], {
        inCollection: true,
        isFavorite: false,
        associatedData: null,
      });

      // Act
      const { result } = renderHook(
        () => useRemoveFromCollection(entityType, entityId, onRemovalWarning),
        { wrapper }
      );

      result.current.remove();

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(onRemovalWarning).not.toHaveBeenCalled();
      expect(collectionsClient.removeFromCollection).toHaveBeenCalled();
    });

    it('executes onConfirm callback when provided', async () => {
      // Arrange
      const entityId = '423e4567-e89b-12d3-a456-426614174000';
      const entityType: EntityType = 'agent';
      const onConfirm = vi.fn();

      vi.mocked(collectionsClient.removeFromCollection).mockResolvedValue(undefined);

      queryClient.setQueryData(['collection-status', entityType, entityId], {
        inCollection: true,
        isFavorite: false,
        associatedData: null,
      });

      // Act
      const { result } = renderHook(
        () => useRemoveFromCollection(entityType, entityId),
        { wrapper }
      );

      result.current.remove(onConfirm);

      // Assert
      await waitFor(() => expect(result.current.isSuccess).toBe(true));
      expect(onConfirm).toHaveBeenCalled();
    });
  });
});
