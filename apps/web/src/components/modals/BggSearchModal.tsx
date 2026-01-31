import { useState, useEffect } from 'react';

import Image from 'next/image';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { useDebounce } from '@/hooks/useDebounce';
import { api, BggSearchResult, BggGameDetails } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';

interface BggSearchModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSelect: (game: BggGameDetails) => void;
}

/**
 * BoardGameGeek search modal component.
 * Allows users to search BGG and import game metadata.
 * AI-13: https://github.com/DegrassiAaron/meepleai-monorepo/issues/420
 */
export function BggSearchModal({ isOpen, onClose, onSelect }: BggSearchModalProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<BggSearchResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [loadingDetails, setLoadingDetails] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Debounce search input (500ms)
  const debouncedQuery = useDebounce(query, 500);

  // Auto-search when debounced query changes
  useEffect(() => {
    if (debouncedQuery.length >= 3) {
      handleSearch(debouncedQuery);
    } else {
      setResults([]);
    }
  }, [debouncedQuery]);

  // Reset state when modal closes
  useEffect(() => {
    if (!isOpen) {
      setQuery('');
      setResults([]);
      setError(null);
    }
  }, [isOpen]);

  const handleSearch = async (searchQuery: string) => {
    setLoading(true);
    setError(null);

    try {
      const response = await api.bgg.search(searchQuery);
      setResults(response.results);

      if (response.results.length === 0) {
        setError('No games found. Try a different search term.');
      }
    } catch (err) {
      logger.error(
        'BGG search error',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('BggSearchModal', 'handleSearch', { query: searchQuery })
      );
      setError(
        'Failed to search BoardGameGeek. The service may be unavailable. Please try again later.'
      );
      setResults([]);
    } finally {
      setLoading(false);
    }
  };

  const handleSelectGame = async (bggId: number) => {
    setLoadingDetails(true);
    setError(null);

    try {
      const details = await api.bgg.getGameDetails(bggId);
      onSelect(details);
      onClose();
    } catch (err) {
      logger.error(
        'BGG game details error',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('BggSearchModal', 'handleSelectGame', { bggId })
      );
      setError('Failed to fetch game details. Please try again.');
    } finally {
      setLoadingDetails(false);
    }
  };

  if (!isOpen) {
    return null;
  }

  return (
    <div
      className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby="bgg-search-title"
    >
      <div
        className="bg-white rounded-lg shadow-xl p-6 w-full max-w-2xl max-h-[80vh] overflow-y-auto"
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex justify-between items-center mb-4">
          <h2 id="bgg-search-title" className="text-2xl font-bold">
            Import from BoardGameGeek
          </h2>
          <button
            onClick={onClose}
            className="text-gray-500 hover:text-gray-700 text-2xl leading-none"
            aria-label="Close modal"
          >
            &times;
          </button>
        </div>

        {/* Search Input */}
        <div className="mb-4">
          <input
            type="text"
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Search for a board game..."
            className="w-full px-4 py-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-blue-500 focus:border-transparent"
            aria-label="Search BoardGameGeek"
            autoFocus
          />
          <p className="text-sm text-gray-500 mt-1">Type at least 3 characters to search</p>
        </div>

        {/* Loading Indicator */}
        {loading && (
          <div className="text-center py-8">
            <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
            <p className="text-gray-600 mt-2">Searching BoardGameGeek...</p>
          </div>
        )}

        {/* Error Message */}
        {error && !loading && (
          <div
            className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded mb-4"
            role="alert"
          >
            <p>{error}</p>
          </div>
        )}

        {/* Search Results */}
        {!loading && results.length > 0 && (
          <div className="space-y-3">
            <p className="text-sm text-gray-600">
              Found {results.length} result{results.length !== 1 ? 's' : ''}
            </p>
            {results.map(result => (
              <div
                key={result.bggId}
                className="border border-gray-200 rounded-lg p-4 hover:bg-gray-50 transition-colors"
              >
                <div className="flex gap-4">
                  {/* Thumbnail */}
                  {result.thumbnailUrl && (
                    <Image
                      src={result.thumbnailUrl}
                      alt={`${result.name} thumbnail`}
                      width={80}
                      height={80}
                      className="object-cover rounded"
                      unoptimized
                    />
                  )}

                  {/* Game Info */}
                  <div className="flex-1">
                    <h3 className="font-semibold text-lg">
                      {result.name}
                      {result.yearPublished && (
                        <span className="text-gray-500 font-normal ml-2">
                          ({result.yearPublished})
                        </span>
                      )}
                    </h3>
                    <p className="text-sm text-gray-500 capitalize">{result.type}</p>
                  </div>

                  {/* Select Button */}
                  <LoadingButton
                    onClick={() => handleSelectGame(result.bggId)}
                    isLoading={loadingDetails}
                    loadingText="Loading..."
                    className="px-4 py-2 bg-blue-500 text-white rounded-md hover:bg-blue-600 transition-colors self-center"
                  >
                    Select
                  </LoadingButton>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* No Results Message */}
        {!loading && !error && query.length >= 3 && results.length === 0 && (
          <p className="text-center text-gray-500 py-8">
            No games found for &quot;{query}&quot;. Try a different search term.
          </p>
        )}

        {/* Instructions */}
        {!loading && query.length < 3 && (
          <p className="text-center text-gray-400 py-8">
            Enter a game name to search BoardGameGeek
          </p>
        )}
      </div>
    </div>
  );
}
